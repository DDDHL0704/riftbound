#!/usr/bin/env python3
"""Compare individual CN rules clauses with the loaded server's executable routes.

Run: python3 scripts/audit-card-effects.py --refresh --markdown docs/CARD_BACKLOG.md
The .NET exporter uses the same reviewed additions/errata as the server. Cached
inventories are rejected after source changes. A route match is not a gameplay
certificate; unresolved/partial matches stay in the review queue.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
from functools import lru_cache
import json
from pathlib import Path
import random
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PROJECT = Path("scripts/audit/effect-inventory")
KINDS = {"keyword": "关键词", "activated": "激活技能", "trigger": "触发",
         "static": "静态效果", "rule": "规则修改", "effect": "结算效果"}
STATUSES = {"route": "已找到实现路径", "missing": "缺失", "review": "待确认", "verified": "已人工核对"}
TOKEN = re.compile(r"\{\{([^{}]+)\}\}")
KEYWORDS = {"强攻", "坚守", "壁垒", "后排", "游走", "法盾", "伏击", "迅捷", "反应",
            "待命", "急速", "瞬息", "预知", "狩猎", "武装", "部署", "回响", "强大", "流转", "百炼", "缴械", "灵便", "装配", "强化"}
# Ordering only, never rules authority: these appear in Riot's published LA
# Best-Of lists (2026-09-30). This is not a current CN metagame usage estimate.
TOURNAMENT_PRIORITY_NAMES = {"占卜贝壳", "奇妙行囊", "无主宝藏", "先锋军备"}


def keyword_family(token: str) -> str:
    for word in sorted(KEYWORDS, key=len, reverse=True):
        if token.startswith(word) and ">" not in token:
            return word
    return token


def normalize(text: str) -> str:
    text = text.replace("{{T}}", "{{横置}}")
    return re.sub(r"[\s，,。；;：:、—\-\u200b“”「」\".]", "", text)


def unparenthesize(text: str) -> str:
    """Remove reminders without throwing away a wholly parenthesized token rule."""
    result, depth = [], 0
    for ch in text:
        if ch in "（(":
            depth += 1
        elif ch in "）)" and depth:
            depth -= 1
        elif not depth:
            result.append(ch)
    bare = "".join(result).strip()
    return bare if bare else text.strip("（）()")


def segments(text: str) -> list[str]:
    """Sentence boundaries inside reminders and quoted granted text are not clauses."""
    result, current, stack = [], [], []
    closes = {"（": "）", "(": ")", "“": "”", "「": "」"}
    for ch in text:
        current.append(ch)
        if ch in closes:
            stack.append(closes[ch])
        elif stack and ch == stack[-1]:
            stack.pop()
        if ch in "。；;\n" and not stack:
            if "".join(current).strip():
                result.append("".join(current).strip())
            current = []
    if "".join(current).strip():
        result.append("".join(current).strip())
    combined = []
    for piece in result:
        if combined and piece.startswith(("（", "(")) and piece.rstrip("。；;").endswith(("）", ")")):
            combined[-1] += piece
        else:
            combined.append(piece)
    return combined


def implicit_recycle_activation_count(text: str) -> int | None:
    # CN post-event FAQ, Future Forge: this printed instruction is an activated
    # skill; choose graveyard targets before paying source destruction.
    if text.lstrip().startswith(("（", "(", "“", "「")):
        return None
    match = re.fullmatch(r"你可以选择摧毁此装备[，,]以此从任意废牌堆中回收最多([0-9一二三四五六七八九十]+)张卡牌[。.]?",
                         unparenthesize(text).strip())
    if not match:
        return None
    raw = match[1]
    count = int(raw) if raw.isdecimal() else dict(zip("一二三四五六七八九十", range(1, 11))).get(raw, 0)
    return count if count > 0 else None


def is_activation(text: str) -> bool:
    if implicit_recycle_activation_count(text) is not None:
        return True
    # Colons in choice menus, granted quoted skills and trigger payments are not
    # independent source activations.
    head = re.split("[：:]", text, maxsplit=1)[0]
    return bool(re.search("[：:]", text) and not text.startswith(("（", "(")) and not re.search("[“「]", head) and not re.match(r"(?:每当|当|在.+时)", head)
                and re.search(r"\{\{(?:横置|T|[0-9]+)\}\}|支付|解除我的强化|弃置.*手牌|摧毁此牌|放逐|消耗|从.*回收|让.+休眠", head))


def clause_kind(text: str, category: str) -> str:
    bare = unparenthesize(text)
    if is_activation(bare):
        return "activated"
    if re.match(r"当我在场上时[，,]", bare):
        return "rule" if re.search(r"可以.+(?:打出|移动)|不能|无法|改为", bare) else "static"
    if re.search(r"不限数量|至多.*增益|不能|不可|可以.+(?:打出|移动)|跳过|改为|效果也是.+效果|额外.+(?:次|个|张)|上限|仅(?:可|能)|只能|无法", bare):
        # A triggered rule change is still a trigger; check its timing first.
        if not re.match(r"(?:每当|当|在.+时)", bare):
            return "rule"
    if re.match(r"当我在一个回合内进行了第[^，,]+次移动[，,]", bare):
        return "trigger"
    if re.search(r"(?:每当|当[^，。]*时|在你的?.*阶段|回合结束时|\{\{(?:鼓舞|绝念|征服|驻守|进攻|防守)[^}]*\}\})", bare):
        return "trigger"
    return "effect" if "spell" in category else "static"


def split_clauses(text: str, category: str = "unit") -> list[dict]:
    result = []
    mode_owner = None
    # Newlines start separate printed abilities. Continuation sentences on the
    # same line inherit the trigger/activation's scope and retain their own text.
    for line in text.splitlines():
        remainder = line.strip()
        if not remainder:
            continue
        if result and remainder.startswith(("（", "(")) and remainder.endswith(("）", ")")):
            result[-1]["text"] += remainder
            continue
        if mode_owner and re.match(r"[·•*\-]|\{\{(?:红色|绿色|蓝色|橙色|紫色|黄色)\}\}", remainder):
            for piece in segments(remainder):
                result.append(dict(text=piece, kind=mode_owner["kind"], scope=mode_owner["scope"]))
            continue
        mode_owner = None
        while remainder:
            match = TOKEN.match(remainder)
            if not match:
                break
            word = keyword_family(match[1])
            if word not in KEYWORDS:
                break
            end = match.end()
            while parameter := TOKEN.match(remainder, end):
                if not re.fullmatch(r"[0-9]+|A|红色|绿色|蓝色|橙色|紫色|黄色", parameter[1]):
                    break
                end = parameter.end()
            if word in ("强化", "装配") and remainder[end:].lstrip().startswith(("—", "-")):
                # Alternative payment is part of the keyword, not a second
                # ordinary activated ability.
                end = len(remainder)
            if remainder[end:end + 1] in ("（", "("):
                depth = 0
                for pos in range(end, len(remainder)):
                    if remainder[pos] in "（(":
                        depth += 1
                    elif remainder[pos] in "）)":
                        depth -= 1
                        if depth == 0:
                            end = pos + 1
                            break
            result.append(dict(text=remainder[:end], kind="keyword", scope=remainder[:end]))
            remainder = remainder[end:].lstrip(" 。；;\t")
        pieces = segments(remainder)
        scope, inherited = "", ""
        for piece in pieces:
            if not normalize(piece):
                continue
            kind = clause_kind(piece, category)
            if kind in ("activated", "trigger"):
                scope, inherited = piece, kind
            elif inherited and not re.match(r"(?:我可以拥有|我具有|我以|此处的)", piece):
                kind = inherited
            else:
                scope, inherited = piece, ""
            result.append(dict(text=piece, kind=kind, scope=scope or piece))
            if re.search(r"(?:选择|执行).*(?:效果|以下)|从以下", piece):
                mode_owner = result[-1]
    return result


def effect_identity(card: dict) -> tuple:
    # Art, series, region, API id and print number are deliberately excluded.
    # Subtitles, costs, power, tags and colors are gameplay-relevant.
    return tuple(json.dumps(sorted(card.get(key, [])) if key == "cardColorList" else card.get(key), ensure_ascii=False, sort_keys=True) for key in
                 ("cardName", "subTitle", "cardCategory", "energy", "power", "returnEnergy", "tag",
                  "cardColorList")) + (normalize("\n".join(unparenthesize(line) for line in card["cardEffect"].splitlines())),
                  normalize(unparenthesize("" if card.get("armamentText", {}).get("reminderOnly")
                      else card.get("armamentText", {}).get("lowerText", ""))),
                  card.get("armament", {}).get("baseMightBonus", 0) if card.get("armament") else 0)


def card_clauses(card: dict) -> list[dict]:
    upper = [dict(row, location="upper") for row in split_clauses(card["cardEffect"], card.get("cardCategory", ""))]
    lower = card.get("armamentText", {})
    if lower.get("reminderOnly"):
        return upper
    return upper + [dict(row, location="armament") for row in split_clauses(lower.get("lowerText", ""), "unit")]


def attach_armament_sources(inventory: dict, path: Path) -> dict:
    document = json.loads(path.read_text())
    if document.get("schemaVersion") != 1:
        raise ValueError("武装中文来源版本不支持")
    normalize_no = lambda no: no.replace("·", "-").upper()
    rows = {normalize_no(row["cardNo"]): row for row in document["records"]}
    if len(rows) != len(document["records"]):
        raise ValueError("武装中文来源卡号重复")
    cards = []
    count = 0
    for original in inventory["cards"]:
        card = dict(original)
        runtime = card.get("armament")
        if runtime is not None or "equipment" in (card.get("cardCategory") or "") and (
                "武装" in (card.get("tag") or "") or "装配" in (card.get("cardEffect") or "")):
            row = rows.get(normalize_no(card["cardNo"]))
            if row is None or runtime is None:
                raise ValueError("缺少武装下栏中文来源或运行时定义：" + card["cardNo"])
            if not row.get("sourceImageUrl", "").startswith("https://cdn.playloltcg.com/") or not re.fullmatch(r"[0-9a-f]{64}", row.get("sourceImageSha256", "")):
                raise ValueError("武装中文卡图来源或摘要无效：" + card["cardNo"])
            if runtime.get("baseMightBonus", 0) != row["baseMightBonus"]:
                raise ValueError("武装下栏战力与官方中文卡图不符：" + card["cardNo"])
            if runtime.get("effectText", "") != row["lowerText"]:
                raise ValueError("武装下栏原文与官方中文卡图不符：" + card["cardNo"])
            card["armamentText"] = row
            count += 1
        cards.append(card)
    return dict(inventory, cards=cards, armamentSourceCoverage=count)


def source_fingerprint(root: Path) -> str:
    paths = sorted(p for directory in ("src/Riftbound.Engine", "src/Riftbound.CardCatalog", "src/Riftbound.Contracts", str(PROJECT))
                   for p in (root / directory).rglob("*")
                   if p.is_file() and p.suffix in (".cs", ".csproj") and not {"obj", "bin"} & set(p.parts))
    paths += sorted(p for p in (root / "data/official").rglob("*") if p.is_file() and p.suffix in (".json", ".gz"))
    if (script := root / "scripts/audit-card-effects.py").exists():
        paths.append(script)
    paths += sorted((root / "scripts/audit/reviewed-clauses").glob("*.json"))
    digest = hashlib.sha256()
    for path in paths:
        digest.update(str(path.relative_to(root)).encode())
        digest.update(path.read_bytes())
    return digest.hexdigest()


class Sources:
    def __init__(self, root: Path, constants: list[dict], probes: list[dict] = ()):
        self.root = root
        self.files = {}
        for path in sorted((root / "src/Riftbound.Engine").rglob("*.cs")):
            if {"obj", "bin"} & set(path.parts) or path.name in ("CardBehaviorRegistry.cs", "P4ActivatedAbilityCatalog.cs", "KeywordCoverageReporter.cs"):
                continue
            # Ignore comments: a deferred TODO is not an executable route.
            text = re.sub(r"/\*.*?\*/|//[^\n]*", "", path.read_text(), flags=re.S)
            self.files[str(path.relative_to(root))] = text
        self.symbols = defaultdict(list)
        for entry in constants:
            self.symbols[entry["value"]].append(entry["symbol"])
        self.exact_text_readers = defaultdict(list)
        for path, text in self.files.items():
            if not (Path(path).name.startswith("CoreRuleEngine.") or Path(path).name == "BoonRules.cs"):
                continue
            for literal in re.findall(r'CardEffect\.(?:Contains|StartsWith)\("([^"\n]+)"', text):
                # Complete legacy sentences only. Keyword/condition fragments
                # and a parser's donor support do not certify a whole clause.
                if len(re.findall(r"[一-鿿]", literal)) >= 12:
                    self.exact_text_readers[normalize(unparenthesize(literal))].append(path)
        self.probes = defaultdict(list)
        for probe in probes:
            self.probes[normalize(probe["text"])].append(probe)
        self.reviewed_clauses = defaultdict(list)
        for path in sorted((root / "scripts/audit/reviewed-clauses").glob("*.json")):
            document = json.loads(path.read_text())
            if document.get("schemaVersion") != 1:
                raise ValueError("已审逐句补充版本不支持：" + str(path))
            for record in document["records"]:
                if (record.get("status") not in ("route", "missing") or not record.get("evidence")
                        or not record.get("rules") or not record.get("cardNos")
                        or not record.get("text") or not record.get("scope")):
                    raise ValueError("已审逐句补充必须包含结论和代码证据：" + str(path))
                if record["status"] == "route" and not record.get("tests"):
                    raise ValueError("已实现的逐句补充必须包含规则测试：" + str(path))
                if any(not entry.get("contains") or not entry["path"].startswith("src/Riftbound.Engine/")
                       or not entry["path"].endswith(".cs") for entry in record["evidence"]):
                    raise ValueError("已审代码证据必须指向引擎 C# 及具体代码片段：" + str(path))
                if record["status"] == "route" and not any(
                        Path(entry["path"]).name.startswith(("CoreRuleEngine", "CardPermission", "OfficialDeck"))
                        for entry in record["evidence"]):
                    raise ValueError("已审路径必须包含执行消费者：" + str(path))
                if any(not test.get("methods") or not test["path"].startswith("tests/Riftbound.ConformanceTests/")
                       for test in record.get("tests", [])):
                    raise ValueError("已审测试证据必须指向规则测试方法：" + str(path))
                for no in record["cardNos"]:
                    key = (no, normalize(record["text"]), normalize(record["scope"]))
                    if self.reviewed_clauses[key]:
                        raise ValueError("已审逐句补充重复：" + repr(key))
                    self.reviewed_clauses[key].append(record)

    @lru_cache(maxsize=None)
    def references(self, token: str, *, exclude: str = "") -> list[str]:
        return [path for path, text in self.files.items() if path != exclude and token in text]

    def node_routes(self, item: dict) -> list[str]:
        routes = []
        for predicate in item.get("predicates", []):
            cls, _ = predicate.split(".", 1)
            routes += self.references(predicate, exclude=f"src/Riftbound.Engine/{cls}.cs")
        if not routes:
            for symbol in self.symbols.get(item["node"].get("kind", ""), []):
                # A switch in actual play/projection/recovery code is evidence;
                # another parser/profile or map builder alone is not.
                routes += [p for p in self.references(symbol) if any(x in Path(p).name for x in ("CoreRuleEngine", "State.", "MatchSession", "MatchRecovery"))]
        return sorted(set(routes))

    def value_routes(self, value: str) -> list[str]:
        return sorted(set(p for symbol in self.symbols.get(value, []) for p in self.references(symbol)))

    def field_routes(self, name: str) -> list[str]:
        pascal = name[0].upper() + name[1:]
        return self.references("." + pascal)


# Each operation needs a non-default executable field. Text parser template
# status is deliberately not used. Composite sentences require every operation.
OPERATIONS = [
    ("draw", r"抽(?:取)?[一二两三四五六七八九十0-9]*张(?:牌|卡牌)",
     ("drawCount", "drawAfterInsightCount", "sourceDrawCount", "sourceBoonDrawCount", "levelDrawOnPlayCount", "drawsControllerAndOtherPlayers", "discardsAllPlayersHandsThenDraws", "drawsSelectedMainDeckTarget", "drawsControllerAndOtherPlayers")),
    ("damage", r"造成[^。；]*伤害", ("damageAmount", "conditionalDamageAmount", "damageDistributionAmount", "otherEnemyBattlefieldUnitsDamageAmount", "damagesSecondTargetByFirstTargetPower", "dealsMutualTargetPowerDamage", "dealsSourceAndTargetPowerDamage", "playDamagePerLocation", "damageAmountFromTargetMarkedDamage")),
    ("might", r"\{\{S\}\}[+-][0-9]+|战力翻倍", ("powerModifierAmount", "secondaryPowerModifierAmount", "sourceReadyPowerModifierAmount", "conditionalSourceUnitPowerBonus", "levelSourceUnitPowerBonus", "appliesPowerModifierToSourceUnit", "basePowerUntilEndOfTurn")),
    ("boon", r"(?:给予|给我)[^。；]*增益", ("grantsBoon", "grantsBoonToAllFriendlyUnits", "grantsBoonToSourceUnit")),
    ("ready", r"(?:变为|成为)活跃|让[^。；]*活跃", ("readiesTarget", "readiesAllFriendlyUnits", "readiesAllEnemyUnitsOnPlay", "readiesFirstTargetAndDamagesSecondByFirstPower", "readiesTargetsAfterChosenMovement", "readiesSourceAndAddsReturnedEquipmentManaPower")),
    ("stun", r"眩晕", ("statusEffectId", "secondaryStatusEffectId")),
    ("return", r"返回[^。；]*手牌|送回[^。；]*手牌", ("returnsTargetToHand", "returnsAllUnitsToHand", "returnsAllFieldObjectsToHand", "returnsGraveyardTargetToHand", "returnsTargetToHandIfAlreadyHasStatusEffect")),
    ("destroy", r"摧毁", ("destroysTarget", "destroysAllUnits", "destroysAllEquipment", "destroysTargetIfAlreadyHasStatusEffect", "destroysFirstTargetAndBuffsSecondByDestroyedPower", "eachPlayerKeepsOneUnitBeforeDestroyingOthers")),
    ("banish", r"放逐", ("banishesTarget", "banishesTargetThenPlaysToBase", "banishesAllFriendlyGraveyardUnits", "banishesSourceOnResolution", "counteredStackItemDestination")),
    ("recycle", r"回收", ("recyclesTargets", "recyclesUnselectedMainDeckLookCards", "recyclesSelectedMainDeckTargets", "recyclesUnkeptSacredJudgmentCards")),
    ("move", r"移动|召回", ("movesTargetToBase", "movesTargetsToChosenLocation", "movesWeakerEnemiesAtSelectedBattlefieldToBase", "movesTargetToOpenBattlefield", "swapsTargetLocations", "swapsSourceWithFirstTargetLocation", "commandedMovement", "targetReferencedMovement")),
    ("rune", r"召出.*符文", ("runeCallCount", "callsRuneForControllerAndOtherPlayers", "runeCallCountAfterTargetReturn")),
    ("insight", r"洞察", ("insightCount", "performsInsight")),
    ("burn", r"燃烧", ("burnCount", "burnAfterEffectCount")),
    ("token", r"打出[^。；]*(?:指示物|“随从”|“机器人”|“黄沙士兵”|“影分身”|“战鹰”)",
     ("createdBaseUnitTokenCount", "createdBaseEquipmentTokenCount", "createdBattlefieldEquipmentTokenCount", "createdBaseUnitTokenCopiesFirstTarget")),
    ("experience", r"获得[一二两三四五六七八九十0-9]+(?:点)?经验", ("gainExperienceOnPlay", "gainExperienceAfterEffect", "gainExperienceOnPlayPerFriendlyFieldUnit")),
    ("discard", r"弃置[^。；]*手牌", ("discardsAllPlayersHandsThenDraws", "discardsTargetFromHand", "discardsTargetFromOwnerHand")),
]


EVENT_TIMINGS = {
    "SECOND_CARD_PLAYED": r"打出第二张牌", "FIRST_CARD_PLAYED": r"打出.*第一张牌",
    "PLAY": r"(?:打出(?:我|此牌)|(?:我|此牌)被打出|将(?:我|此牌)打出)", "PLAY_OR_CONQUEST": r"打出(?:我|此牌)|(?:我|此牌)被打出|我征服",
    "PLAY_OR_EXHAUST": r"打出(?:我|此牌)|(?:我|此牌)被打出|我变为休眠", "PLAY_OR_MOVE": r"打出(?:我|此牌)|(?:我|此牌)被打出|我移动",
    "PLAY_OR_LEAVE": r"打出(?:我|此牌)|(?:我|此牌)被打出|我离场", "PLAY_OR_START": r"打出(?:我|此牌)|(?:我|此牌)被打出|回合开始",
    "PLAY_AFTER_ANOTHER_CARD": r"鼓舞", "BECAME_EXHAUSTED": r"我变为休眠",
    "BECAME_READY": r"我变为活跃", "ANOTHER_UNIT_PLAYED": r"打出.*(?:另一|其他).*单位",
    "FRIENDLY_UNIT_PLAYED": r"打出.*单位", "ANOTHER_FRIENDLY_UNIT_EXHAUSTED": r"另一名友方单位变为休眠",
    "FRIENDLY_UNITS_EXHAUSTED_TOGETHER": r"同时.*友方单位变为休眠",
    "ENEMY_MIGHT_REDUCED_HERE": r"敌方单位战力降低", "SELF_DISCARDED": r"弃置我",
    "OPPONENT_EQUIPMENT_PLAYED": r"对手打出.*装备", "BATTLE_END": r"战斗结束",
    "ATTACK_OR_DEFEND": r"我进攻|我防守", "ATTACK": r"我进攻", "DEFEND": r"我防守",
    "CONQUEST": r"我征服", "MAIN": r"主阶段", "FRIENDLY_ATTACK_HERE": r"友方单位.*进攻",
    "LEAVE": r"(?:我|此牌)离场",
}


def meaningful(value) -> bool:
    return value is not None and value is not False and value not in ("", "NONE", -1, 0)


def legend_field_activation_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    if card.get("cardCategory") != "legendary":
        return None
    scope = unparenthesize(clause["scope"])
    if not re.search("[：:]", scope):
        return None
    cost, effect = re.split("[：:]", scope, maxsplit=1)
    effect = normalize(effect)
    shapes = {
        normalize("在战场和其所属基地之间移动一名友方单位"): ("MOVE_FRIENDLY_UNIT", True, False, False, False),
        normalize("将战场上一名处于休眠状态的友方单位移动到其基地"): ("MOVE_FRIENDLY_UNIT", True, True, True, False),
        normalize("让战场上的一名友方单位返回其所属的手牌"): ("RETURN_BATTLEFIELD_UNIT_AND_CREATE_COIN", True, True, False, False),
        normalize("将你拥有的一张位于英雄区域或场上的提莫单位牌放入你的手牌"): ("RETURN_OWNED_TEEMO_UNIT_TO_HAND", False, False, False, True),
    }
    draw = effect == normalize("抽一张牌") and bool(re.search(r"消耗[0-9]+经验", cost))
    shape = ("DRAW_ONE", False, False, False, False) if draw else shapes.get(effect)
    if shape is None:
        return None
    primary = clause["scope"] == clause["text"]
    gold = normalize(unparenthesize(clause["text"])) == normalize("打出一个休眠的“金币”装备指示物")
    if not primary and not (gold and shape[0] == "RETURN_BATTLEFIELD_UNIT_AND_CREATE_COIN"):
        return None
    expected, friendly, battlefield, exhausted, owned = shape
    mana = sum(int(n) for n in re.findall(r"\{\{([0-9]+)\}\}", cost))
    experience = sum(int(n) for n in re.findall(r"消耗([0-9]+)经验", cost))
    token = f"SPEND_EXPERIENCE:{experience}" if experience else f"SPEND_MANA:{mana}" if mana else ""
    candidates = [ability for ability in card.get("legendActivationDefinitions", [])
                  if activation_cost_matches(ability, dict(clause, text=clause["scope"]))
                  and ability.get("requiredCostToken", "") == token
                  and ability.get("effectKind") == expected]
    if not candidates:
        return result("missing", "传奇技能缺少与本句费用和效果匹配的运行时规格")
    scoped = [ability for ability in candidates if ability.get("requiredTargetCount", 0) == (0 if draw else 1)
              and bool(ability.get("requiresFriendlyUnitTarget")) == friendly
              and bool(ability.get("requiresBattlefieldTarget")) == battlefield
              and bool(ability.get("requiresExhaustedTarget")) == exhausted
              and bool(ability.get("requiresOwnedTeemoUnitTarget")) == owned
              and not ability.get("requiresAnyUnitTarget") and ability.get("timingKind") == "MAIN_OPEN"]
    if not scoped:
        return result("missing", "传奇技能的时机、目标数量、区域、拥有权或休眠条件与牌面不符")
    flow = "DISCARD_DRAW" if draw else "FIELD_EFFECT"
    if not any(ability.get("flow") == flow for ability in scoped):
        return result("missing" if any(ability.get("flow") == "IMMEDIATE" for ability in scoped) else "review",
                      "普通传奇技能没有对应的独立结算路径；需核对实际响应窗口")
    resolver = "CoreRuleEngine.LegendDiscardDraw.cs" if draw else "CoreRuleEngine.LegendFieldEffect.cs"
    def consumer(symbol: str, filename: str) -> list[str]:
        return [path for path in sources.references(symbol) if Path(path).name == filename]
    if draw and consumer("&& !(ability.EffectKind == LegendAbilityEffectKinds.DrawOne", "CoreRuleEngine.cs"):
        return result("missing", "普通抽牌技能仍被分派条件排除在结算链之外")
    required = [("QueueLegendDiscardDraw(" if draw else "QueueLegendFieldEffect(", "CoreRuleEngine.cs"),
                ("if (IsLegendDiscardDrawEffect(ability.EffectKind))" if draw
                 else "if (IsLegendFieldEffect(ability.EffectKind))", "CoreRuleEngine.cs")]
    if draw:
        required += [("ResolveDiscardDrawStackItem(", resolver)]
    elif expected == "MOVE_FRIENDLY_UNIT":
        required += [("TryMoveFieldUnitByEffect(", resolver), ("SameOrigin", resolver),
                     ("DeclareLegendFieldDestination(", "CoreRuleEngine.cs")]
    else:
        required += [("ResolveUnitReturnInstructions(", resolver), ("TryReturnTargetToHand(", "CoreRuleEngine.ReturnSkills.cs")]
        if owned:
            required += [("LegalTarget, ability.RequiresOwnedTeemoUnitTarget", resolver)]
        if gold:
            required += [("if (createsGold)", "CoreRuleEngine.ReturnSkills.cs"),
                         ("CreateBaseEquipmentTokens(", "CoreRuleEngine.ReturnSkills.cs"),
                         ("ability.EffectKind == LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin", resolver)]
    paths = [consumer(symbol, filename) for symbol, filename in required]
    if all(paths):
        return result("route", "逐句传奇费用／范围及独立移动、返手或抽牌结算", sorted({p for row in paths for p in row}))
    return result("review", "存在匹配的传奇技能规格；仍需核对实际分派与结算消费者")


def activation_cost_matches(ability: dict, clause: dict) -> bool:
    if implicit_recycle_activation_count(clause["text"]) is not None:
        simple = ability.get("simpleEffect", {})
        return (simple.get("destroysSourceAsCost", False) and not ability.get("exhaustsSourceAsCost")
                and not any(ability.get(key) for key in ("manaCost", "powerCost", "experienceCost", "powerCostByTrait"))
                and not any(simple.get(key) for key in ("discardsHandCardAsCost", "discardsEquipmentAsCost",
                    "removesSourceEmpowermentAsCost", "destroyFriendlyTagAsCost"))
                and simple.get("exhaustFriendlyUnitMinimumPowerAsCost") is None)
    cost = re.split("[：:]", unparenthesize(clause["text"]), maxsplit=1)[0]
    if bool(re.search(r"\{\{(?:横置|T)\}\}", cost)) != bool(ability.get("exhaustsSourceAsCost")):
        return False
    mana = sum(int(n) for n in re.findall(r"\{\{([0-9]+)\}\}", cost))
    if mana != ability.get("manaCost", 0):
        return False
    colors = re.findall(r"\{\{(A|红色|绿色|蓝色|橙色|紫色|黄色)\}\}", cost)
    trait_names = {"红色": "red", "绿色": "green", "蓝色": "blue", "橙色": "orange", "紫色": "purple", "黄色": "yellow"}
    printed_traits = Counter(trait_names[color] for color in colors if color != "A")
    actual_traits = {trait: amount for trait, amount in (ability.get("powerCostByTrait") or {}).items() if amount}
    if colors.count("A") != ability.get("powerCost", 0) or printed_traits != actual_traits:
        return False
    experience = sum(int(n) for n in re.findall(r"消耗([0-9]+)经验", cost))
    return experience == ability.get("experienceCost", 0)


def activation_matches(ability: dict, clause: dict) -> bool:
    if not activation_cost_matches(ability, clause):
        return False
    if (maximum := implicit_recycle_activation_count(clause["text"])) is not None:
        return (ability.get("kind") == "DESTROY_SOURCE_RECYCLE_GRAVEYARD"
                and ability.get("simpleEffect", {}).get("recyclesGraveyardTargets", False)
                and ability.get("sourceRequiredTag") == "CARD_TYPE:EQUIPMENT"
                and ability.get("requiredTargetCount", 0) == 0 and ability.get("maximumTargetCount") == maximum
                and not any(ability.get(key) for key in ("reactionSpeed", "swiftSpeed", "requiresBattlefieldSource")))
    parts = re.split("[：:]", unparenthesize(clause["text"]), maxsplit=1)
    # Modal bullet lines inherit their cost from a separate scope. Their own
    # binding must audit that scope; they cannot be treated as standalone skills.
    if len(parts) != 2:
        return False
    _, effect = parts
    numeric_damage = re.search(r"造成([0-9]+)点伤害", effect)
    if numeric_damage and ability.get("damageAmount") and ability["damageAmount"] != int(numeric_damage[1]):
        return False
    numeric_might = re.search(r"\{\{S\}\}([+-][0-9]+)", effect)
    if numeric_might and ability.get("powerModifierAmount") and ability["powerModifierAmount"] != int(numeric_might[1]):
        return False
    simple = ability.get("simpleEffect", {})
    label = ability.get("effectKind", "") + " " + ability.get("kind", "")
    checks = {
        "draw": bool(simple.get("drawCount")) or "DRAW" in label,
        "damage": bool(ability.get("damageAmount") or simple.get("battlefieldUnitDamage")) or "DAMAGE" in label,
        "might": bool(ability.get("powerModifierAmount") or simple.get("sourcePowerBonus")) or "POWER" in label or "MIGHT" in label,
        "boon": bool(simple.get("grantsBoonOrPower")) or "BOON" in label,
        "ready": bool(simple.get("readiesSource") or simple.get("readiesUnitTarget")) or "READY" in label,
        "move": any(s in label for s in ("MOVE", "SWAP", "RECALL")) or simple.get("grantsPaidRecall", False),
        "stun": simple.get("stunsTarget", False) or "STUN" in label,
        "return": "RETURN" in label or "RECALL" in label,
        "token": any(v for k, v in simple.items() if k.startswith("created")) or "CREATE" in label,
        "rune": bool(simple.get("runeCallCount")), "insight": "INSIGHT" in label,
        "destroy": simple.get("destroysSourceOnResolution", False) or "DESTROY" in label,
        "banish": simple.get("banishesSourceThenReplaysToBase", False) or "BANISH" in label,
        "recycle": "RECYCLE" in label, "burn": "BURN" in label,
        "discard": simple.get("discardsHandCardAsCost", False) or "DISCARD" in label,
        "experience": "EXPERIENCE" in label,
    }
    operations = [op for op, pattern, _ in OPERATIONS if re.search(pattern, effect)]
    if operations:
        return all(checks.get(op, False) for op in operations)
    return bool("{{获得}}" in effect and (ability.get("generatedMana") or ability.get("generatedPower") or ability.get("generatedPowerByTrait"))) or bool(re.search(r"获得.*分", effect) and "SCORE" in label)


def legend_activation_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    parts = re.split("[：:]", unparenthesize(clause["text"]), maxsplit=1)
    if len(parts) != 2:
        return None
    effect = parts[1].strip()
    candidates = [a for a in card.get("legendActivationDefinitions", []) if activation_cost_matches(a, clause)]
    boon = re.fullmatch(r"给予一名(友方)?单位(?:\{\{增益\}\}|增益)[。.]?", effect)
    roam = re.fullmatch(r"让一名单位本回合内获得\{\{游走\}\}[。.]?", effect)
    expected = "GRANT_BOON" if boon else "GRANT_ROAM" if roam else None
    relevant = [a for a in candidates if a.get("effectKind") == expected] if expected else []
    if relevant:
        friendly = bool(boon and boon[1])
        scoped = [a for a in relevant if a.get("requiredTargetCount") == 1
                  and bool(a.get("requiresFriendlyUnitTarget")) == friendly
                  and (friendly or a.get("requiresAnyUnitTarget"))]
        if not scoped:
            return result("missing", "传奇技能的目标数量或敌我范围与牌面不符")
        stacked = [a for a in scoped if a.get("flow") == "UNIT_EFFECT"]
        if not stacked:
            return result("missing", "普通传奇技能仍直接结算；缺少技能入链及响应窗口")
        resolver = [p for p in sources.references("LegendAbilityEffectKinds." + ("GrantBoon" if boon else "GrantRoam"))
                    if Path(p).name == "CoreRuleEngine.LegendUnitEffect.cs"]
        dispatch = [p for p in sources.references("QueueLegendUnitEffect(") if Path(p).name == "CoreRuleEngine.cs"]
        if resolver and dispatch:
            return result("route", "逐句传奇费用／目标范围及共享入链技能结算", resolver + dispatch)
    # A matching effect switch is not enough for any ordinary legend ability.
    # Resource skills remain distinct; other families need their own mapping.
    if any(a.get("flow") == "IMMEDIATE" and activation_matches(a, clause) for a in candidates):
        return result("missing", "已登记的非资源传奇技能仍直接结算；缺少技能入链及响应窗口")
    return None


def event_operations_match(event: dict, text: str) -> bool:
    # A matching timing is not evidence for an unrelated or partial effect.
    # The trigger condition itself is not an instruction (e.g. "becomes ready").
    body = re.sub(r"^.*?时[，,]", "", text, count=1)
    # A self-boon consumer cannot certify a different clause that grants boons
    # to a chosen unit or every friendly unit at the source's location.
    if event.get("effect") in {"BOON_SELF", "BOON_SELF_AND_READY"} and not re.search(
            r"(?:给予我|给我|我获得|让我获得)[^。；]*增益", body):
        return False
    capabilities = {
        "DRAW": {"draw"}, "DISCARD_DRAW": {"draw", "discard"},
        "MIGHT_SELF": {"might"}, "MIGHT_FRIENDLY": {"might"}, "MIGHT_ATTACKERS": {"might"},
        "MIGHT_SELF_AND_READY": {"might", "ready"}, "MATCH_POWER_THEN_BUFF": {"might"},
        "DESTROY_FRIENDLY_EQUIPMENT_MIGHT_SELF": {"might", "destroy"},
        "READY_SELF": {"ready"}, "READY_ANOTHER_FRIENDLY": {"ready"}, "READY_HERE": {"ready"},
        "BOON_SELF": {"boon"}, "BOON_FRIENDLY": {"boon"}, "BOON_SELF_AND_READY": {"boon", "ready"},
        "CALL_RUNES_PER_CONSUMED_BOON": {"rune"}, "PLAYED_UNIT_CALL_RUNES": {"rune"}, "INSIGHT": {"insight"},
        "CREATE_UNIT_TOKEN": {"token"}, "CREATE_EQUIPMENT_TOKEN": {"token"},
        "MOVE_ENEMY_HERE": {"move"}, "MOVE_ENEMY": {"move"}, "MOVE_ENEMIES_TO_BASE": {"move"},
        "DESTROY_SELF": {"destroy"}, "DESTROY_CHEAPER_EQUIPMENT": {"destroy"},
        "DESTROY_DAMAGED_ENEMIES_HERE": {"destroy"},
        "BANISH_ENTERED_EQUIPMENT": {"banish"}, "DAMAGE_ENEMIES_HERE": {"damage"},
        "BANISH_HAND_GRAVEYARD_BURN": {"banish", "burn"}, "BANISH_LINKED_UNIT_HERE": {"banish"},
    }
    supported = set(capabilities.get(event["effect"], ()))
    if event.get("leadingDestroyOtherFriendlyPermanents"):
        supported.add("destroy")
    if event.get("runeCallCount"):
        supported.add("rune")
    required = {op for op, pattern, _ in OPERATIONS if re.search(pattern, body)}
    if event["effect"] not in capabilities or not required <= supported:
        return False
    if event["effect"] == "DESTROY_DAMAGED_ENEMIES_HERE" and not re.fullmatch(
            r"摧毁此处所有已受伤的敌方单位。?", body.strip()):
        return False
    if event["effect"] in ("DRAW", "DISCARD_DRAW"):
        match = re.search(r"抽([一二两三四五六七八九十0-9]+)张牌", body)
        if match:
            digits = dict(zip("一二三四五六七八九十", range(1, 11)), 两=2)
            count = int(match[1]) if match[1].isdigit() else digits.get(match[1])
            if count != event.get("amount", 0):
                return False
    if "rune" in required and event.get("runeCallCount"):
        match = re.search(r"召出([一二两三四五六七八九十0-9]+)(?:枚|张|个)[^。；]*符文", body)
        if not match:
            return False
        digits = dict(zip("一二三四五六七八九十", range(1, 11)), 两=2)
        count = int(match[1]) if match[1].isdigit() else digits.get(match[1])
        if count != event["runeCallCount"]:
            return False
    return True


def event_timing_routes(timing: str, sources: Sources) -> list[str]:
    direct = sources.references('"' + timing + '"')
    if direct:
        return direct
    if "_OR_" not in timing:
        return []
    # Union event names are interpreted by the shared capture matcher. They
    # need not appear as hard-coded strings in executable routing anymore.
    return [path for path in sources.references("UnitEventTimingMatches(spec.Timing, timing)")
            if Path(path).name == "CoreRuleEngine.UnitEvents.cs"
            and 'declared?.Split("_OR_", StringSplitOptions.None).Contains(timing, StringComparer.Ordinal)' in sources.files[path]]


def behavior_operations(card: dict, clause: dict, sources: Sources) -> dict | None:
    bare = unparenthesize(clause["text"])
    scope = unparenthesize(clause["scope"])
    if clause["kind"] == "trigger":
        events = [card.get("unitEventAbility")] + [behavior.get("unitEventAbility") for behavior in card.get("behaviors", [])]
        for event in events:
            if event and re.search(EVENT_TIMINGS.get(event["timing"], r"(?!)"), scope) and event_operations_match(event, bare):
                timing = event_timing_routes(event["timing"], sources)
                effect = sources.references('"' + event["effect"] + '"')
                if timing and effect:
                    return result("route", "事件技能：" + event["timing"] + "/" + event["effect"], timing + effect)
        if not re.search(r"打出(?:我|此牌)|(?:我|此牌)被打出|将(?:我|此牌)打出", scope):
            return None
    elif clause["kind"] not in ("effect", "static"):
        return None
    if clause["kind"] == "static" and not re.search(r"以(?:活跃|休眠)状态进场", bare):
        return None
    # CN 355.4.a: an unqualified movement instruction permits any other
    # legal field location. A fixed recall-to-base branch is only partial.
    behaviors = card.get("behaviors", [])
    if re.fullmatch(r"移动一名(?:敌方|友方)单位。?", bare.strip()) and behaviors             and any(b.get("movesTargetToBase") for b in behaviors)             and not any(b.get("movesTargetsToChosenLocation") for b in behaviors):
        return result("missing", "未限定终点的移动仅实现移回基地（核心355.4.a）",
                      sources.field_routes("movesTargetToBase"))
    for behavior in card.get("behaviors", []):
        if re.search(r"以(?:活跃|休眠)状态进场", bare):
            field = "sourceEquipmentIsExhausted" if behavior.get("playsSourceToBaseAsEquipment") else "sourceUnitIsExhausted"
            if behavior.get("playsSourceToBaseAsEquipment") or behavior.get("playsSourceToBaseAsUnit"):
                if bool(behavior.get(field)) == ("休眠" in bare):
                    return result("route", "入场状态字段：" + field, sources.field_routes(field))
        found, evidence = [], []
        for op, pattern, fields in OPERATIONS:
            if re.search(pattern, bare):
                candidates = [f for f in fields if meaningful(behavior.get(f)) and not (f == "counteredStackItemDestination" and behavior[f] != "EXILE")]
                number = re.search(r"造成([0-9]+)点伤害", bare) if op == "damage" else re.search(r"\{\{S\}\}([+-][0-9]+)", bare) if op == "might" else None
                if number:
                    candidates = [f for f in candidates if not isinstance(behavior[f], int) or isinstance(behavior[f], bool) or behavior[f] == int(number[1])]
                if op == "draw" and (printed := re.search(r"抽(?:取)?([一二两三四五六七八九十0-9]+)张(?:牌|卡牌)", bare)):
                    digits = dict(zip("一二三四五六七八九十", range(1, 11)), 两=2)
                    amount = int(printed[1]) if printed[1].isdigit() else digits.get(printed[1])
                    if amount is not None:
                        candidates = [field for field in candidates if type(behavior[field]) is int and behavior[field] == amount]
                paths = [p for f in candidates for p in sources.field_routes(f)]
                if not candidates or not paths:
                    break
                found += candidates
                evidence += paths
        else:
            if found:
                return result("route", "结算字段：" + ", ".join(sorted(set(found))), evidence)
    return None


def node_matches(clause: dict, item: dict) -> bool:
    needle = normalize(unparenthesize(clause["text"]))
    haystack = normalize(unparenthesize(item["node"].get("text", "")))
    return bool(needle and needle in haystack)


def result(status: str, reason: str, evidence=()) -> dict:
    return dict(status=status, reason=reason, evidence=sorted(set(evidence)))


def inventory_contains(actual, expected) -> bool:
    """A reviewed route must still have its concrete exported specification.

    Dictionaries select fields; list entries must match distinct actual entries.
    Scalars (including booleans vs integers) compare exactly. An empty expected
    list means the runtime list really is empty, not a vacuous match.
    """
    if isinstance(expected, dict):
        return isinstance(actual, dict) and all(key in actual and inventory_contains(actual[key], value)
                                                for key, value in expected.items())
    if isinstance(expected, list):
        if not isinstance(actual, list) or not expected:
            return actual == expected
        def match(remaining, candidates):
            if not remaining:
                return True
            return any(inventory_contains(candidate, remaining[0])
                       and match(remaining[1:], candidates[:i] + candidates[i + 1:])
                       for i, candidate in enumerate(candidates))
        return match(expected, actual)
    return type(actual) is type(expected) and actual == expected


def reviewed_clause_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    """Fallback for individually reviewed clauses, never a card-wide override.

    Evidence is part of the match: exact print, clause and scope; current runtime
    fields; executable source fragments; and named rule tests. Removing any of
    these reopens review. Ordinary matchers run first so O-line implementations
    can supersede a recorded gap without editing the review supplement.
    """
    key = (card["cardNo"], normalize(clause["text"]), normalize(clause["scope"]))
    records = sources.reviewed_clauses.get(key, [])
    for record in records:
        if clause.get("location", "upper") != record.get("location", "upper"):
            continue
        evidence, stale = [], []
        if not inventory_contains(card, record.get("runtime", {})):
            stale.append("运行时规格")
        for entry in record["evidence"]:
            path = sources.root / entry["path"]
            code = re.sub(r"/\*.*?\*/|//[^\n]*", "", path.read_text(), flags=re.S) if path.exists() else ""
            if not entry.get("contains") or not all(fragment in code for fragment in entry["contains"]):
                stale.append(entry["path"])
            evidence.append(entry["path"])
        for test in record.get("tests", []):
            path = sources.root / test["path"]
            code = re.sub(r"/\*.*?\*/|//[^\n]*", "", path.read_text(), flags=re.S) if path.exists() else ""
            for method in test["methods"]:
                declaration = (r"\[(?:Fact|Theory)\b[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                               r"public\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void)\s+"
                               + re.escape(method) + r"\s*\(")
                if not re.search(declaration, code):
                    stale.append(test["path"] + "::" + method)
                evidence.append(test["path"] + "::" + method)
        if stale:
            return result("review", "已审证据发生变化，需重新核对：" + "、".join(stale), evidence)
        answer = result(record["status"], "逐句已审：" + record["reason"], evidence)
        answer["rules"] = record["rules"]
        return answer
    return None


def keyword_route(card: dict, clause: dict, sources: Sources) -> dict:
    token = TOKEN.match(clause["text"])[1]
    word = keyword_family(token)
    behaviors = card.get("behaviors", [])
    tags = "|".join([b.get("sourceUnitTags", "") + "|" + b.get("sourceEquipmentTags", "") for b in behaviors] + card.get("tokenTags", []))
    # These keywords are read from effective rules text, not only object tags.
    text_routes = {"待命": "Standby", "伏击": "Ambush", "预知": "PrintedPredict",
                   "回响": "Echo", "武装": "Armament", "部署": "Deploy", "流转": "Flow",
                   "百炼": "Tempered", "缴械": "Disarm", "灵便": "Nimble", "强化": "Empower", "装配": "Attach"}
    if word in text_routes:
        routes = [p for p in sources.references(text_routes[word]) if "CoreRuleEngine" in p]
        if routes:
            return result("route", "共享流程读取牌面关键词", routes)
    if word == "迅捷" and any(b.get("canPlayDuringSpellDuel") for b in behaviors):
        return result("route", "登记迅捷出牌窗口", sources.references("behavior.CanPlayDuringSpellDuel"))
    if word == "反应" and (any(b.get("canPlayDuringPriority") for b in behaviors) or card.get("activations") or card.get("legendActivations")):
        return result("route", "反应出牌／技能窗口", sources.references("ReactionSpeed"))
    if word in tags:
        if word in ("强攻", "坚守", "法盾", "狩猎"):
            expected = int(token[len(word):] or "1")
            amounts = [int(t[len(word):] or "1") for t in tags.split("|") if re.fullmatch(re.escape(word) + r"[0-9]*", t)]
            if expected not in amounts:
                return result("missing", "关键词数值与实际物体标签不一致")
        if word == "急速" and not any(b.get("hasteReadyManaCost", 0) or b.get("hasteReadyPowerCost", 0) for b in behaviors):
            return result("review", "只有急速标签，需核对额外费用分支")
        routes = sources.references('"' + word + '"') + sources.references({"强攻": "Assault", "坚守": "Steadfast", "法盾": "Spellshield", "狩猎": "Hunt"}.get(word, word))
        if routes:
            return result("route", "物体关键词及共享执行流程", routes)
    if word in ("强攻", "坚守", "法盾", "狩猎", "后排", "壁垒", "游走", "瞬息") and behaviors:
        return result("missing", "实际物体构造缺少此牌面关键词")
    return result("review", "未找到该关键词的物体／费用／文本消费路径")


def has_public_armament_keyword_binding(sources: Sources) -> bool:
    text = sources.files.get('src/Riftbound.Engine/CoreRuleEngine.ArmamentKeywords.cs', '')
    required = ['!gear.IsFaceDown', '!unit.IsFaceDown',
                'IsObjectOnField(state.PlayerZones, gear.ObjectId)', 'IsObjectOnField(state.PlayerZones, host)',
                'IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host)']
    return all(token in text for token in required) and not any(
        name + '.Tags.Contains(CardObjectTags.Standby)' in text for name in ('gear', 'unit'))


def audit_armament_clause(card: dict, clause: dict, sources: Sources) -> dict:
    runtime = card.get("armament", {})
    bare = normalize(unparenthesize(clause["text"]))
    if clause["kind"] == "keyword":
        token = TOKEN.match(clause["text"])[1]
        grants = runtime.get("grantedKeywords", []) or []
        matches = token in grants or (token == keyword_family(token)
            and token in ("强攻", "坚守", "法盾", "狩猎") and token + "1" in grants)
        grant_paths = [path for path in sources.references(".GrantedKeywords")
                       if Path(path).name == "CoreRuleEngine.ArmamentKeywords.cs"]
        if matches and grant_paths:
            if not has_public_armament_keyword_binding(sources):
                return result('missing', '武装关键词缺少公开区域／背面状态检查，或误将待命关键词当作背面状态', grant_paths)
            return result("route", "贴附武装向宿主授予具体关键词", grant_paths)
    replacement = {
        normalize("如果我被摧毁，则改为摧毁守护天使。"),
        normalize("移除我所受到的伤害，让我变为休眠状态，并将我召回。")}
    if bare in replacement and runtime.get("replacesHostDestruction"):
        flags, outcomes = sources.references(".ReplacesHostDestruction"), sources.references('"ATTACHED_GEAR"')
        if flags and outcomes:
            return result("route", "贴附武装的摧毁替换及移除伤害／休眠召回", flags + outcomes)
    # Upper-box activations, the equipment's own tags and donor parsers do not
    # grant a lower-box skill to its host. Unknown future runtime fields keep
    # the clause in review until their concrete consumer is mapped.
    known = {"cardNo", "sourceCardNo", "baseMightBonus", "effectText", "cardImageUrl",
             "copiesHostPrintedRulesText", "copiesChosenFriendlyUnit", "replacesHostDestruction", "grantedKeywords"}
    if any(meaningful(value) for key, value in runtime.items() if key not in known):
        return result("review", "存在新增武装规格；需核对下栏句子及宿主执行消费者")
    # These bounded readers only support reviewed host event/death templates.
    # They do not make other lower-box rules or keywords uncertain again.
    bounded_readers = {"ArmamentUnitEventRules.cs", "CoreRuleEngine.ArmamentLastBreath.cs"}
    readers = [path for reader in ("armament.EffectText", "definition.EffectText", "gearDefinition.EffectText")
               for path in sources.references(reader) if Path(path).name not in bounded_readers]
    if readers:
        return result("review", "存在武装文字消费者；需核对本句及宿主范围")
    return result("missing", "未登记与该句匹配的武装宿主效果或关键词；上栏接入不代表下栏生效")


def native_activation_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    def signature(spec):
        # Export defaults are omitted; parser probes include them. Every actual
        # payment/effect field participates, including newly added parser fields.
        # An isolated clause has a fresh index and cannot see the following
        # sentence's battlefield limitation, which is audited independently.
        nullable_numbers = {"empoweredMana", "empoweredMight", "exhaustedUnitMinimumMightAsCost"}
        return {key: value for key, value in spec.items()
                if key not in ("index", "requiresSourceBattlefield")
                and (value is not None if key in nullable_numbers else value not in (None, False, 0, "", {}, []))}
    owner = []
    for probe in sources.probes.get(normalize(clause["scope"]), []):
        if probe["method"] != "BorrowedExhaustParser.Parse":
            continue
        for parsed in probe["value"]:
            owner += [native for native in card.get("nativeActivations", [])
                      if signature(native) == signature(parsed)]
    if clause["text"] == clause["scope"] and owner:
        routes = [path for native in owner for path in sources.references('"' + native["kind"] + '"')
                  if "CoreRuleEngine" in path]
        if any(native["kind"] == "MIGHT_UNIT" for native in owner):
            # The shared resolver's ordinary else branch applies spec.Might;
            # it does not need another literal comparison with MIGHT_UNIT.
            routes += [path for path in sources.references("spec.Might")
                       if Path(path).name == "CoreRuleEngine.BorrowedExhaust.cs"
                       and path in sources.references("ApplyPowerModifier(")]
        if routes:
            return result("route", "原生横置技能规格及共享执行分支：" + owner[0]["kind"], routes)
    if re.fullmatch(r"(?:我必须位于战场[中上]才能使用此技能|只有我位于战场时，才能使用我的技能)[。.]?",
                    unparenthesize(clause["text"])):
        candidates = owner or card.get("activations", [])
        if candidates and all(a.get("requiresSourceBattlefield") or a.get("requiresBattlefieldSource") for a in candidates):
            return result("route", "激活技能的来源位置限制", sources.references("RequiresSourceBattlefield")
                          + sources.references("RequiresBattlefieldSource"))
    return None


def counted_behavior_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    bare = unparenthesize(clause["text"])
    for behavior in card.get("behaviors", []):
        field, consumer = None, None
        defended_reveal = r"当我防守时，选择一名此处的敌方单位，展示你主牌堆顶部的五张牌[。.]?"
        defended_damage = r"当中每有一张带有\{\{待命\}\}技能的卡牌，便对该单位造成1点伤害，然后将展示的牌回收[。.]?"
        defend_fields = dict(defendTriggerMaxTargetCount=1, defendTriggerTargetScope="ENEMY_UNIT_AT_SOURCE_BATTLEFIELD",
                             defendTriggerMainDeckLookCount=5, defendTriggerCountedTag="待命",
                             defendTriggerDamagePerCountedCard=1, defendTriggerRecyclesLookedMainDeckCards=True)
        if re.fullmatch(defended_reveal, unparenthesize(clause["scope"])) and (
                re.fullmatch(defended_reveal, bare) or re.fullmatch(defended_damage, bare)) and all(
                behavior.get(key) == value for key, value in defend_fields.items()):
            if sources.references("ResolveStandbyDefendTrigger(") and sources.references("CaptureStandbyDefendTriggers("):
                evidence = [path for key in defend_fields for path in sources.field_routes(key)]
                targets = [path for path in sources.references('"STANDBY_DEFEND"')
                           if Path(path).name == "CoreRuleEngine.FieldTriggers.cs"
                           and "IsCombatMightTargetHere(" in sources.files[path]
                           and "c.ControllerId != item.ControllerId" in sources.files[path]]
                # The generic field-trigger target selector enforces this scope
                # directly, rather than reading the registry's descriptive field.
                if targets and all(sources.field_routes(key) for key in defend_fields if key != "defendTriggerTargetScope"):
                    return result("route", "防守展示、待命计数伤害与回收的完整规格及结算分支", evidence
                                  + targets + sources.references("ResolveStandbyDefendTrigger("))
        if re.fullmatch(r"(?:在本回合内，如果我移动了两次，则我免疫伤害|如果我已在本回合内移动了两次，则我无法受到伤害)[。.]?", bare) and behavior.get("damageImmunityAfterMoves") == 2:
            field, consumer = "damageImmunityAfterMoves", "UnitMovementRules.CannotTakeDamage"
        if re.fullmatch(r"当我在一个回合内进行了第三次移动，你获得1分[。.]?", bare) and behavior.get("movementScore") == dict(ordinal=3, scoreAmount=1):
            field, consumer = "movementScore", "ResolveMovementScore("
        if re.fullmatch(r"你的所有\{\{绝念\}\}效果可以额外触发一次[。.]?", bare) and behavior.get("friendlyLastBreathAdditionalTriggers") == 1:
            field, consumer = "friendlyLastBreathAdditionalTriggers", "CaptureLastBreathRepetitions("
        if field and sources.field_routes(field) and sources.references(consumer):
            return result("route", "计数规格及共享执行流程：" + field,
                          sources.field_routes(field) + sources.references(consumer))
    return None


def legacy_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    """Match concrete runtime identity readers to their individual instructions."""
    bare = unparenthesize(clause["text"])
    routes = card.get("legacyRoutes", {})
    digits = {"一": 1, "二": 2, "两": 2, "三": 3, "四": 4, "五": 5}
    def number(value):
        return int(value) if value.isdigit() else digits.get(value)
    matched = None
    held = routes.get("HeldDefinition", {})
    prefix = re.fullmatch(r"(?:当(?:我|你)据守(?:一处战场|此处)?时)[，,]\s*(.+?)[。.]?", bare)
    if prefix and held:
        body = normalize(prefix[1])
        kind, amount = held["kind"], held.get("amount", 0)
        patterns = {
            "DRAW": r"抽([一二两三四五0-9]+)张牌",
            "SCORE": r"你获得(?:的分数\+|)([一二两三四五0-9]+)(?:分)?",
            "EXPERIENCE": r"(?:你)?获得([一二两三四五0-9]+)(?:点)?经验",
            "ROBOT": r"打出([一二两三四五0-9]+)名3\{\{S\}\}的机器人到你的基地",
            "MINION": r"(?:在你的基地)?打出([一二两三四五0-9]+)名1\{\{S\}\}的随从(?:单位指示物)?(?:到你的基地)?",
        }
        value = re.fullmatch(patterns[kind], body) if kind in patterns else None
        if value and number(value[1]) == amount:
            matched = "HeldDefinition", "ResolveHeldStackItem", f"据守 {kind}/{amount}"
        if kind == "RETURN_SELF" and re.fullmatch(r"把我送回所属的手牌|让(?:我|此牌)返回(?:其)?所属的手牌", body):
            matched = "HeldDefinition", "ResolveHeldStackItem", "据守来源返手"
    if routes.get("BeginningDrawKind") == "HAND_BELOW_TWO" and re.fullmatch(
            r"在你的回合开始阶段，如果你的手牌不足两张，则再抽一张牌[。.]?", bare):
        matched = "BeginningDrawKind", "ResolveBeginningDraw", "回合开始手牌不足两张时抽一张牌"
    recycle = re.fullmatch(r"在你的回合开始阶段，从(?:你的)?废牌堆中?回收([一二两三四五0-9]+)张牌[。.]?", bare)
    if recycle and number(recycle[1]) == routes.get("BeginningRecycleCount"):
        matched = "BeginningRecycleCount", "BeginGraveyardRecycle", "回合开始从废牌堆回收"
    if routes.get("IsUnitBattleWinDrawPrinting") and re.fullmatch(r"当我赢得一场战斗时，抽一张牌[。.]?", bare):
        matched = "IsUnitBattleWinDrawPrinting", "ResolveUnitBattleWinDraw", "单位赢得战斗抽牌"
    if routes.get("IsDravenBattleWinScorePrinting") and re.fullmatch(r"每回合首次，当我赢得战斗时，你获得1分[。.]?", bare):
        matched = "IsDravenBattleWinScorePrinting", "ResolveDravenBattleWinScore", "首次赢得战斗得分"
    if routes.get("IsKhazixBattleWinPrinting") and re.fullmatch(r"当你赢得一场战斗时，获得1经验[。.]?", bare):
        matched = "IsKhazixBattleWinPrinting", "ResolveKhazixBattleWin", "传奇赢得战斗获得经验"
    if matched:
        reader, consumer, description = matched
        evidence = sources.references(reader + "(") + sources.references(consumer + "(")
        if sources.references(reader + "(") and sources.references(consumer + "("):
            return result("route", "运行时来源分组及结算分支：" + description, evidence)
    return None


def rune_domain_reveal_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    root_text = normalize("当我进攻时，展示你符文牌堆顶部的一张牌，然后将其回收。")
    if clause["kind"] != "trigger" or normalize(unparenthesize(clause["scope"])) != root_text:
        return None
    text = normalize(unparenthesize(clause["text"]))
    branches = {
        normalize("{{红色}}—对此处的一名敌方单位造成2点伤害，并对此处所有其他敌方单位造成1点伤害。"): ("red", "DAMAGE_ENEMY_AND_OTHERS_HERE", 2, 1),
        normalize("{{蓝色}}—抽一张牌。"): ("blue", "DRAW", 1, 0),
        normalize("{{黄色}}—眩晕一名敌方单位。"): ("yellow", "STUN_ENEMY", 0, 0),
    }
    header = text in (normalize("根据展示的符文特性执行以下效果："), normalize("根据展示的符文特性，进行一次："))
    if text != root_text and not header and text not in branches:
        return None
    def consumer(symbol, filename):
        return [p for p in sources.references(symbol) if Path(p).name == filename]
    common = consumer("ResolveRuneDomainReveal(", "CoreRuleEngine.UnitEvents.cs")
    common += consumer("WithUnitEvents(", "CoreRuleEngine.RuneDomainReveal.cs")
    common += consumer("CaptureRecycleObservers(", "CoreRuleEngine.RuneDomainReveal.cs")
    root_consumer = all(consumer(symbol, filename) for symbol, filename in [
        ("ResolveRuneDomainReveal(", "CoreRuleEngine.UnitEvents.cs"),
        ("WithUnitEvents(", "CoreRuleEngine.RuneDomainReveal.cs"),
        ("CaptureRecycleObservers(", "CoreRuleEngine.RuneDomainReveal.cs"),
        ("ValidRevealedRuneContext(", "CoreRuleEngine.UnitEvents.cs"),
        ("RuneDomainModeApplies(", "CoreRuleEngine.UnitEventModes.cs"),
    ])
    events = [card.get("unitEventAbility")] + [b.get("unitEventAbility") for b in card.get("behaviors", [])]
    for event in events:
        if not event or event.get("timing") != "ATTACK" or event.get("effect") != "REVEAL_RECYCLE_RUNE_DOMAIN" or not root_consumer:
            continue
        if any(meaningful(v) for k, v in event.items() if k not in ("timing", "effect", "modes")):
            continue
        modes = event.get("modes") or []
        if not modes or any(not m.get("id") for m in modes) or len({m["id"] for m in modes}) != len(modes):
            continue
        if text == root_text:
            return result("route", "进攻展示并回收符文，使用共享回收观察器和独立触发", common)
        if header:
            if {m.get("requiredRevealedRuneTrait") for m in modes} == {"red", "blue", "yellow"} and len(modes) == 3:
                return result("route", "依展示符文特性创建三类内嵌式触发", common)
            continue
        trait, effect, amount, other = branches[text]
        selected = [m for m in modes if m.get("requiredRevealedRuneTrait") == trait]
        if len(selected) != 1:
            continue
        mode = selected[0]
        if mode.get("effect") != effect or mode.get("amount", 0) != amount or mode.get("otherEnemyDamageAmount", 0) != other:
            continue
        allowed = {"id", "label", "effect", "amount", "requiredRevealedRuneTrait", "otherEnemyDamageAmount"}
        if any(meaningful(v) for k, v in mode.items() if k not in allowed):
            continue
        required = ([("ResolveRuneDomainDamage(", "CoreRuleEngine.UnitEvents.cs"),
                     ("spec.OtherEnemyDamageAmount", "CoreRuleEngine.RuneDomainReveal.cs"),
                     ("ResolveUnitEventAreaDamage(", "CoreRuleEngine.RuneDomainReveal.cs")]
                    if trait == "red" else [("ApplyDrawToPlayer(", "CoreRuleEngine.UnitEvents.cs")] if trait == "blue"
                    else [("\"STUN_ENEMY\"", "CoreRuleEngine.UnitEventBoonInstructions.cs")])
        if all(consumer(symbol, filename) for symbol, filename in required):
            return result("route", "展示符文后的独立颜色分支及精确效果数值", common + [p for symbol, name in required for p in consumer(symbol, name)])
    return result("missing", "进攻展示／回收符文或对应颜色的独立技能缺少精确规格及执行消费者")


def colored_legacy_activation_fee(card: dict, clause: dict, sources: Sources) -> dict | None:
    if clause["kind"] != "activated" or clause["scope"] != clause["text"]:
        return None
    shape = normalize(unparenthesize(clause["text"]))
    profiles = {
        normalize("支付{{2}}和{{红色}}：让我本回合内战力翻倍。"): (
            "PAY_2_RED_DOUBLE_POWER", "VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN", "ResolveActivateAbility", 0, 0, False),
        normalize("支付{{红色}}，{{横置}}：对一名单位造成3点伤害。"): (
            "PAY_RED_EXHAUST_DAMAGE_3", "XERATH_PAY_RED_EXHAUST_DAMAGE_3", "ResolveXerathDamageAbility", 1, 3, True),
    }
    if shape not in profiles:
        return None
    ability_id, effect, method, targets, damage, battlefield = profiles[shape]
    candidates = [ability for ability in card.get("activations", [])
        if ability.get("abilityId") == ability_id and ability.get("effectKind") == effect
        and activation_matches(ability, clause) and ability.get("requiredTargetCount", 0) == targets
        and ability.get("damageAmount", 0) == damage and bool(ability.get("requiresBattlefieldSource")) == battlefield
        and bool(ability.get("appliesSpellshieldTargetTax")) == battlefield
        and not any(meaningful(ability.get(key)) for key in ["simpleEffect", "kind", "isResourceSkill",
            "paymentOnlyResource", "reactionSpeed", "swiftSpeed", "sourceRequiredTag", "sourceScope",
            "requiredControllerCardNoOnField", "requiresBaseEquipmentSource"])]
    path = "src/Riftbound.Engine/CoreRuleEngine.cs"
    core = sources.files.get(path, "")
    def region(name):
        # Restrict proof to the actual consumer, not a neighboring skill that
        # happens to read the same fee fields. Engine members use four spaces.
        found = re.search(r"(?ms)^    private static ResolutionResult " + re.escape(name)
            + r"\(.*?(?=^    (?:private|internal|public|protected)\s|\Z)", core)
        return found.group(0) if found else ""
    consumer = region(method)
    total = "var totalPowerCost = ability.PowerCost + powerCostByTrait.Values.Sum()" \
        + (" + spellshieldTaxPower;" if battlefield else ";")
    generic = "genericPowerCost: " + ("(ability.PowerCost + spellshieldTaxPower)" if battlefield else "ability.PowerCost")
    required = ["P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability)", total, generic,
        "totalPowerCost: totalPowerCost", "powerCostByTrait: powerCostByTrait", "TryApplyOrderedPaymentResources(",
        "PaymentCostRules.AuthorizePayment(", "PaymentCostRules.TryCommitPayment(", "PaymentCostRules.BuildCostPaidPayload("]
    dispatcher = region("ResolveActivateAbility")
    routed = "P4ActivatedAbilityCatalog.TryGetByAbilityId(command.AbilityId, out var ability)" in dispatcher
    if battlefield:
        routed = routed and "return ResolveXerathDamageAbility(state, intent, command, ability);" in dispatcher
    if candidates and routed and all(token in consumer for token in required):
        return result("route", "精确印刷有色费用及共享付款消费者，通用符能按核心163.2.b可代付", [path])
    return result("missing", "缺少印刷有色费用，或真实激活消费者未将颜色要求交给共享付款校验")


def native_unit_activation_binding(card: dict, clause: dict, sources: Sources) -> dict:
    xerath = normalize(unparenthesize(clause["text"])).startswith(normalize("支付{{红色}}，{{横置}}"))
    ability_id = "PAY_RED_EXHAUST_DAMAGE_3" if xerath else "PAY_2_RED_DOUBLE_POWER"
    profiles = [ability for ability in card.get("activations", []) if ability.get("abilityId") == ability_id]
    if not profiles or not all(ability.get("bindsUnitSource")
            and bool(ability.get("modifiesUnitSource")) == (not xerath) for ability in profiles):
        return result("missing", "有色费用已实现；原生技能尚未绑定来源代际及自身效果的场地限制")
    core_path = "src/Riftbound.Engine/CoreRuleEngine.cs"
    consumer = re.search(r"(?ms)^    private static ResolutionResult "
        + ("ResolveXerathDamageAbility" if xerath else "ResolveActivateAbility")
        + r"\(.*?(?=^    (?:private|internal|public|protected)\s|\Z)", sources.files.get(core_path, ""))
    if not consumer or "return BindNativeUnitActivationSource(state, new ResolutionResult(" not in consumer.group(0):
        return result("missing", "有色费用已实现；实际激活消费者未捕获原生来源代际")
    required = {
        core_path: [
            "if (!ValidNativeUnitActivation(state, stackItem))",
            "nativeAbility.ModifiesUnitSource && !NativeUnitActivationSourceStillOnField(state, stackItem)",
            "stackItem = MaskTargetsFromPreviousGenerations(state, stackItem);",
            "!IsParsedUnitHeldContext(resolvedItem.HeldContext) && resolvedItem.NativeUnitActivation is null",
            '"targetObjectIds"] = resolvedItem.UnitEvent is null && resolvedItem.NativeUnitActivation is null',
            "return RedactNativeUnitActivationSource(state, stackItem, Resolve"
                + ("XerathDamage" if xerath else "ViDoublePower") + "AbilityStackItem(state, stackItem));"],
        "src/Riftbound.Engine/CoreRuleEngine.NativeUnitActivations.cs": [
            "if (!result.Accepted || !ability.BindsUnitSource)",
            "new(source.ObjectId, source.ObjectGeneration)", "source.CopyFaceTransitions?.Count ?? 0",
            "internal static bool ValidNativeUnitActivation(",
            "SelfAbilitySourceRules.Valid(state, context.Source, context.CardNo, context.FaceRevision,",
            "usesPrintedFace: true", "item.TargetGenerations", "MatchStateHasher.HashValue(expected)",
            "source.ObjectGeneration == context.Source.Generation && IsFaceUpNonStandbyUnit(source)",
            "IsObjectOnField(state.PlayerZones, source.ObjectId)",
            "ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator)",
            'pair.Key is "sourceObjectId" or "unitSourceObjectId"'],
        "src/Riftbound.Engine/SelfAbilitySourceRules.cs": [
            "item.NativeUnitActivation is { CeasedSource: null } native",
            "departed.Contains(native.Source.ObjectId) && ValidNativeUnitActivation(before, item)",
            "NativeUnitActivation = native with { CeasedSource = SelfAbilitySourceRules.Capture("],
        "src/Riftbound.Engine/MatchSession.cs": [
            "NativeUnitActivation = item.NativeUnitActivation",
            "item.TargetedPowerAbility is not null || item.NativeUnitActivation is not null",
            "item.TargetedPowerAbility is null && item.NativeUnitActivation is null"],
        "src/Riftbound.Engine/MatchRecovery/Authoritative.Resources.cs": [
            "if (!CoreRuleEngine.ValidNativeUnitActivation(authoritativeState, item))"],
        "src/Riftbound.Engine/MatchRecovery.cs": [
            "stackItem.NativeUnitActivation is { CeasedSource: not null } && CoreRuleEngine.ValidNativeUnitActivation(authoritativeState, stackItem)",
            "item.NativeUnitActivation is not null && CoreRuleEngine.ValidNativeUnitActivation(state, item)"],
    }
    if all(all(token in sources.files.get(path, "") for token in tokens) for path, tokens in required.items()):
        return result("route", "印刷有色费用及共享付款；绑定来源代际、独立入链、消失凭据、隐私投影与恢复；目标复用共享代际校验", list(required))
    return result("missing", "有色费用已实现；仍缺少完整来源代际、离场续接、隐私投影或恢复消费者")


def experience_play_cost_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    texts = ["{{等级3>}} 我的费用减少{{2}}和{{绿色}}。",
             "{{等级6>}} 改为我的费用减少{{4}}和{{绿色}}{{绿色}}。",
             "{{等级11>}} 改为我的费用减少{{6}}和{{绿色}}{{绿色}}{{绿色}}。"]
    shapes = {normalize(text) for text in texts}
    if normalize(unparenthesize(clause["text"])) not in shapes:
        return None
    if normalize(unparenthesize(clause["scope"])) != normalize(unparenthesize(clause["text"])):
        return result("missing", "等级减费句子的适用范围与独立经验报价不一致，不能忽略额外条件")
    if not shapes.issubset({normalize(unparenthesize(row["text"])) for row in card_clauses(card)}):
        return result("missing", "等级减费必须匹配完整三档文本，不能授予未印刷的更高档减费")
    def correct(behavior):
        steps = behavior.get("costReductionExperienceThresholds")
        return behavior.get("playsSourceToBaseAsUnit") \
            and behavior.get("costReductionConditionKind") == "CONTROLLER_EXPERIENCE_THRESHOLD_COUNT" \
            and behavior.get("costReductionMana") == 2 and behavior.get("costReductionPower") == 1 \
            and behavior.get("costReductionPowerTrait") == "green" \
            and isinstance(steps, list) and all(type(step) is int for step in steps) and sorted(steps) == [3, 6, 11]
    if not any(correct(behavior) for behavior in card.get("behaviors", [])):
        return result("missing", "缺少经验3／6／11最高档替换为减2／4／6法力及1／2／3绿色符能的规格")
    counter = sources.files.get("src/Riftbound.Engine/CoreRuleEngine.ConditionalPlayCosts.cs", "")
    branch = re.search(r"CardCostReductionConditionKinds\.ControllerExperienceThresholdCount\s*=>\s*"
                       r"(.*?)(?=,\s*CardCostReductionConditionKinds\.|,\s*_\s*=>)", counter, re.S)
    if not branch or not all(token in branch.group(1) for token in (
            "behavior.CostReductionExperienceThresholds?.Count(threshold",
            "state.PlayerExperience.GetValueOrDefault(player) >= threshold")):
        return result("missing", "实际经验减费分支没有读取当前操作者的有效档位，其他分支或未调用代码不足以认证")
    requirements = {
        "CoreRuleEngine.ConditionalPlayCosts.cs": [
            "CardCostReductionConditionKinds.ControllerExperienceThresholdCount",
            "behavior.CostReductionExperienceThresholds?.Count(threshold",
            "state.PlayerExperience.GetValueOrDefault(player) >= threshold",
            "var reduction = behavior.CostReductionPower * ConditionalPlayCostCount(state, player, behavior);",
            "Math.Max(0, p.Value - reduction)"],
        "CoreRuleEngine.cs": [
            "var manaCost = CalculatePlayManaCost(state, intent.PlayerId, behavior, extraManaCost,"],
        "CoreRuleEngine.PlayManaCost.cs": [
            "ResolveCostReductionMana(state, playerId, behavior, sourceObjectId)",
            "var cost = CalculatePlayManaCost(state, playerId, behavior, additionalMana, optionalReduction,",
            "Math.Max(0, total - Math.Max(0, reduction.Floor))"],
        "CoreRuleEngine.EmpoweredSpellCosts.cs": [
            "PrintedPowerCostRules.OptionsForPlay(behavior, printedChoice, extraGeneric, extraTyped, additionalPrinted)",
            "ConditionalPlayPowerCost(state, player, behavior,"],
    }
    evidence = ["src/Riftbound.Engine/" + name for name in requirements]
    actual_mana = re.search(r"behavior\.CostReductionMana \* ConditionalPlayCostCount\(state, playerId, behavior(?:, sourceObjectId)?\)",
                            sources.files.get("src/Riftbound.Engine/CoreRuleEngine.cs", ""))
    # Other printed cost reductions can compose before the level reduction.
    # Require the live typed-price expression while allowing that composition.
    actual_power = re.search(
        r"\.SelectMany\(\s*a\s*=>\s*ReduceSpellPower\(\s*a\.GenericPower,\s*"
        r"ConditionalPlayPowerCost\(\s*state,\s*player,\s*behavior,\s*"
        r"(?:ApplyBoonPlayPowerReduction\(\s*behavior,\s*)?"
        r"ApplySacrificePowerReduction\(\s*a\.TypedPower,\s*sacrificeReduction\)\s*\)?\s*\)",
        sources.files.get("src/Riftbound.Engine/CoreRuleEngine.EmpoweredSpellCosts.cs", ""), re.S)
    if not actual_mana or not actual_power or not all(all(token in sources.files.get("src/Riftbound.Engine/" + name, "") for token in tokens)
                                  for name, tokens in requirements.items()):
        return result("missing", "经验减费尚未读取当前操作者经验，或未接通共享法力／有色符能的报价与实际付款")
    return result("route", "当前经验最高档同时减少法力和绿色符能，沿用共享报价／付款和零下限", evidence)


def conditional_rune_and_conquest_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    """Require the printed condition and its live or captured shared consumer."""
    text = normalize(unparenthesize(clause["text"]))
    number = lambda value: int(value) if value.isdigit() else {"一": 1, "两": 2, "二": 2, "三": 3,
        "四": 4, "五": 5, "六": 6, "七": 7, "八": 8, "九": 9, "十": 10}.get(value, -1)
    rune = re.fullmatch(r"如果你拥有不少于([0-9一两二三四五六七八九十]+)枚符文则我获得\{\{S\}\}\+([0-9]+)", text)
    conquest = re.fullmatch(r"当你征服一处战场时如果你在该战场上拥有不少于([0-9一两二三四五六七八九十]+)名单位则抽([0-9一两二三四五六七八九十]+)张牌", text)
    if not rune and not conquest:
        return None
    def method_has(filename, name, *symbols):
        # Restrict evidence to the named live consumer. A neighboring unused
        # helper or a surviving registration cannot replace its instruction.
        text = sources.files.get("src/Riftbound.Engine/" + filename, "")
        body = re.search(r"(?ms)^    (?:private|internal|public|protected)\s+static\s+"
            r"[\w<>,?.\[\] ]+\s+" + re.escape(name)
            + r"\(.*?(?=^    (?:private|internal|public|protected)\s|\Z)", text)
        return body is not None and all(symbol in body.group(0) for symbol in symbols)

    def consumers(requirements):
        evidence = []
        for filename, symbols in requirements.items():
            path = "src/Riftbound.Engine/" + filename
            if not all(symbol in sources.files.get(path, "") for symbol in symbols):
                return []
            evidence.append(path)
        return evidence
    if rune:
        minimum, amount = map(number, rune.groups())
        nodes = [item["node"] for item in card.get("staticAuras", []) if node_matches(clause, item)]
        exact = card.get("cardCategory") in ("unit", "hero_unit") and minimum > 0 and amount > 0 and any(
            n.get("kind") == "SOURCE_OBJECT_POWER" and n.get("layer") == "STATIC_AURA"
            and n.get("duration") == "WHILE_SOURCE_ON_PUBLIC_FIELD" and n.get("targetScope") == "SOURCE_OBJECT"
            and n.get("participantScope") == "SOURCE_OBJECT" and n.get("powerDeltaPerParticipant") == amount
            and n.get("requiredControllerRuneCount") == minimum and not n.get("requiredPlayerExperience")
            and not n.get("targetFilter") for n in nodes)
        evidence = consumers({
            "StaticAuraSpecRules.cs": ["aura.RequiredControllerRuneCount", "EmpowerRules.ControlledRunes(state, controller)"],
            "CoreRuleEngine.cs": ["aura.RequiredControllerRuneCount", "StaticAuraSpecRules.ControllerRequirementsSatisfied("],
            "MatchSession/State.Normalization.cs": ["StaticAuraSpecRules.ControllerRequirementsSatisfied("],
            "MatchSession/State.Auras.cs": ["aura.RequiredControllerRuneCount", "StaticAuraControllerRequirementsSatisfied("],
            "CoreRuleEngine.AttachedRuleText.cs": ["SourceObjectKeywordStaticAuraApplies(state, unit, aura)"],
        })
        bound = all(method_has("CoreRuleEngine.cs", name,
                "aura.RequiredControllerRuneCount.HasValue",
                "!StaticAuraControllerRequirementsSatisfied(aura, state, cardObject.ControllerId)")
            for name in ("SourceObjectPowerStaticAuraApplies", "SourceObjectKeywordStaticAuraApplies")) \
            and method_has("MatchSession/State.Auras.cs", "SourceObjectPowerStaticAuraApplies",
                "aura.RequiredControllerRuneCount.HasValue",
                "!StaticAuraControllerRequirementsSatisfied(aura, state, controllerId)") \
            and method_has("StaticAuraSpecRules.cs", "ControllerRequirementsSatisfied",
                "!aura.RequiredControllerRuneCount.HasValue",
                "EmpowerRules.ControlledRunes(state, controller) >= aura.RequiredControllerRuneCount.Value")
        if exact and evidence and bound:
            return result("route", "当前控制者场上符文阈值的实时自身加成，含复制文字重算", evidence)
        return result("missing", "缺少精确符文阈值、自身战力规格或实时共享消费者")
    minimum, draws = map(number, conquest.groups())
    nodes = [item["node"] for item in card.get("triggers", []) if node_matches(clause, item)]
    exact = card.get("cardCategory") == "legendary" and minimum > 0 and draws > 0 and any(
        n.get("kind") == "LEGEND_CONQUEST_MINIMUM_UNITS_DRAW" and n.get("timing") == "BATTLEFIELD_CONQUERED"
        and n.get("targetScope") == "CONTROLLED_UNITS_AT_THIS_BATTLEFIELD" and n.get("requiredUnitCount") == minimum
        and n.get("drawCount") == draws and not any(n.get(key) for key in
            ("optional", "manaCost", "powerCost", "experienceCost", "exhaustsSource", "requiredOverkillDamage")) for n in nodes)
    evidence = consumers({
        "CoreRuleEngine.Conquest.cs": ["CaptureLegendConquest(state"],
        "CoreRuleEngine.LegendConquest.cs": ["IsLegendConquestMinimumUnitsDrawTrigger", "units.Count < definition.Spec.RequiredUnitCount",
            "LegendConquestDrawOrigin", "TriggerQueueItemState(", "ResolveLegendConquestDraw(state, item)"],
        "CoreRuleEngine.LegendConquestDraw.cs": ["ValidLegendConquestDrawStack(state, item)", "ApplyDrawToPlayer(", "spec.DrawCount!.Value"],
        "MatchRecovery.cs": ["ValidLegendConquestState(", "ValidLegendConquestDrawStack("],
    })
    bound = method_has("CoreRuleEngine.LegendConquest.cs", "CaptureLegendConquest",
            "ControlledUnitsAtBattlefield(", "units.Count < definition.Spec.RequiredUnitCount",
            "new TriggerQueueItemState(") \
        and method_has("CoreRuleEngine.LegendConquest.cs", "ResolveLegendConquest",
            "context.Kind == LegendConquestDrawKind", "return ResolveLegendConquestDraw(state, item);") \
        and method_has("CoreRuleEngine.LegendConquestDraw.cs", "ResolveLegendConquestDraw",
            "ValidLegendConquestDrawStack(state, item)", "ApplyDrawToPlayer(", "spec.DrawCount!.Value")
    if exact and evidence and bound:
        return result("route", "征服时捕获本战场单位数，普通入链后独立抽牌", evidence)
    return result("missing", "缺少本战场征服时阈值及可响应的独立抽牌触发，登记或即时抽牌不足以认证")

def friendly_target_ready_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    if not re.fullmatch(r"当你将一名友方单位选为目标时你可以选择让我变为休眠状态并支付\{\{A\}\}以此让该单位变为活跃状态", text):
        return None
    event = card.get("unitEventAbility") or {}
    allowed = {"timing", "effect", "amount", "leadingPowerCost", "leadingSourceExhaust", "optional", "sourceInLegendZone"}
    exact = card.get("cardCategory") == "legendary" and event.get("timing") == "FRIENDLY_UNIT_TARGETED" \
        and event.get("effect") == "READY_SELECTED_EVENT_UNIT" and event.get("leadingPowerCost") == 1 \
        and event.get("leadingSourceExhaust") is True and event.get("optional") is True \
        and event.get("sourceInLegendZone") is True and not event.get("amount") \
        and not any(meaningful(value) for key, value in event.items() if key not in allowed)
    exact = exact and any(node_matches(clause, item)
        and item["node"].get("kind") == "CONTROLLER_TARGETED_FRIENDLY_UNIT_PAY_EXHAUST_READY"
        and item["node"].get("timing") == "CONTROLLER_TARGETED_FRIENDLY_UNIT"
        and item["node"].get("targetScope") == "SELECTED_FRIENDLY_EVENT_UNIT"
        and item["node"].get("powerCost") == 1 and item["node"].get("exhaustsSource") is True
        and item["node"].get("optional") is True and item["node"].get("unitReadyCount") == 1
        and not any(meaningful(item["node"].get(key)) for key in ["manaCost", "powerCostTrait", "oncePerTurn"])
        for item in card.get("triggers", []))
    requirements = {
        "CoreRuleEngine.TargetSelectionObservers.cs": ["IsFriendlyUnitTargetedReadyTrigger", "CaptureUnitEvents(",
            "UnitEventTrigger(", "ApplyReadyState(", "PredictBindingMatches("],
        "CoreRuleEngine.SelfActionObservers.cs": ["CaptureFriendlyUnitTargetedObservers(", "QueueTargetSelectionObservers(",
            "HasConfirmedTargets(", "SPELL_CHOICES_REMADE"],
        "CoreRuleEngine.UnitEvents.cs": ["TargetSelectionLegendEvents.Value.GetValueOrDefault(",
            "ValidTargetSelectionContext(", "ResolveSelectedEventUnitReady("],
        "CoreRuleEngine.TriggerCosts.cs": ["exhaustSpec.LeadingPowerCost", "LeadingSourceExhaust", "CompleteTriggerCost("],
        "MatchRecovery.cs": ["ValidUnitEvent(", "ValidUnitEventTargets("],
    }
    evidence = []
    for filename, symbols in requirements.items():
        path = "src/Riftbound.Engine/" + filename
        if not all(symbol in sources.files.get(path, "") for symbol in symbols):
            exact = False
        evidence.append(path)
    if exact:
        return result("route", "确认友方单位目标时捕获独立触发，横置并付任意符能后活跃原物体", evidence)
    return result("missing", "缺少选择友方单位时的实际触发、横置及任意符能前置费用，或原物体的独立活跃结算")
def source_attack_damage_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    if clause['kind'] != 'trigger' or clause['scope'] != clause['text']:
        return None
    text = normalize(unparenthesize(clause['text'])).replace('{{强攻}}', '强攻')
    keyword = text == '当我进攻时对此处的一名敌方单位造成等同于我强攻数值的伤害'
    fixed = re.fullmatch(r'当我进攻时对此处的一名敌方单位造成([1-9][0-9]*)点伤害', text)
    if not keyword and not fixed:
        return None
    kind = 'UNIT_ATTACK_SOURCE_KEYWORD_DAMAGE' if keyword else 'UNIT_ATTACK_FIXED_DAMAGE'
    amount = None if keyword else int(fixed[1])
    allowed = {'kind', 'timing', 'text', 'reason', 'targetScope', 'damageAmount', 'damageSourceKeyword'}
    precise = any(b.get('playsSourceToBaseAsUnit') for b in card.get('behaviors', [])) and any(
        n.get('kind') == kind and n.get('timing') == 'UNIT_ATTACK' and n.get('targetScope') == 'ENEMY_UNIT_HERE'
        and n.get('damageAmount') == amount and n.get('damageSourceKeyword') == ('强攻' if keyword else None)
        and not any(meaningful(value) for key, value in n.items() if key not in allowed)
        for item in card.get('triggers', []) for n in [item.get('node', {})])
    requirements = {
        'CoreRuleEngine.CombatMight.cs': ['IsSupportedCombatSourceDamage(t)',
            'TriggerKinds.UnitAttackSourceKeywordDamage' if keyword else 'TriggerKinds.UnitAttackFixedDamage',
            'CaptureCombatMightTriggers(', 'spec.Timing == TriggerTimings.UnitAttack && !attacking.Contains(id)',
            'AttachedRuleTextRules.Instances(state, source)', 'source.ObjectGeneration == generation'],
        'CoreRuleEngine.AttackMightDamage.cs': ['spec.DamageSourceKeyword is { } keyword',
            'ResolveCurrentCountedCombatKeyword(state, source, keyword)',
            'spec.DamageAmount ?? ResolveCurrentFieldUnitPower(state, source)',
            'source.ObjectGeneration != context.SourceGeneration', '!IsObjectOnField(state.PlayerZones, source.ObjectId)',
            'FieldTriggerTargets(state, item)', 'if (baseAmount <= 0) return result;',
            'ApplySpellSkillDamageBonuses(state, item.ControllerId, target, baseAmount)'],
        'CoreRuleEngine.FieldTriggers.cs': ['IsCombatMightTargetHere(state, item, c.ObjectId)',
            'TargetProtectionRules.IsLegalActivatedSkillTarget(', 'ResolveSourceMightDamage(state, item)'],
    }
    evidence = []
    for name, symbols in requirements.items():
        path = 'src/Riftbound.Engine/' + name
        precise = precise and all(symbol in sources.files.get(path, '') for symbol in symbols)
        evidence.append(path)
    dispatch = [path for path in sources.references('CaptureCombatMightTriggers(')
                if Path(path).name != 'CoreRuleEngine.CombatMight.cs']
    precise = precise and bool(dispatch)
    evidence += dispatch
    if fixed and sources.references('ResolveSourceAttackDamageToFirstDefenderTriggers('):
        precise = False
    return result('route', '精确进攻／敌方此处规格，共享当前数值／固定伤害、目标代际与法盾响应流程', evidence) if precise else \
        result('missing', '进攻伤害缺少精确数值来源、目标选择及当前来源位置的独立结算消费者')


def standby_unit_observer_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    hidden = text == normalize("当你正面朝下放置一张卡牌时，让我变为活跃状态。")
    damage = re.fullmatch(r"当你将一张卡牌从正面朝下的状态打出时对一名敌方单位造成([1-9][0-9]*)点伤害", text)
    if not hidden and not damage:
        return None
    behaviors = card.get("behaviors", [])
    exact = clause["kind"] == "trigger" and normalize(unparenthesize(clause["scope"])) == text \
        and card.get("cardCategory") in {"unit", "hero_unit"} and len(behaviors) == 1 \
        and behaviors[0].get("playsSourceToBaseAsUnit") is True
    if exact:
        exact = behaviors[0].get("sourceReadiesWhenControllerHidesCard") is True if hidden else \
            type(behaviors[0].get("controllerFaceDownPlayEnemyDamage")) is int \
                and behaviors[0]["controllerFaceDownPlayEnemyDamage"] == int(damage[1])
    prefix = "src/Riftbound.Engine/"

    def member(filename, name):
        code = sources.files.get(prefix + filename, "")
        start = re.search(r"(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b" + re.escape(name) + r"\s*\(", code)
        if not start:
            return ""
        tail = code[start.end():]
        end = re.search(r"(?m)^    (?:private|internal|public) ", tail)
        return code[start.start():start.end()] + (tail[:end.start()] if end else tail)

    checks = [
        ("CoreRuleEngine.StandbyUnitObservers.cs", "CaptureStandbyUnitObservers", [
            "GetControlledFieldUnitObjectIds(zones, cards, controller)", "IsFaceUpNonStandbyUnit(source)",
            "AttachedRuleTextRules.Instances(zones, cards, source)", "source.ObjectGeneration", "instance.Origin",
            "behavior.SourceReadiesWhenControllerHidesCard" if hidden else "behavior.ControllerFaceDownPlayEnemyDamage",
            "StandbyUnitActionHash(action)"]),
        ("CoreRuleEngine.StandbyObservers.cs", "QueueStandbyPlayedObservers", [
            "observed with", "CapturedTick = result.State.Tick", "StandbyObserverEventKind(effect)",
            '["standbyUnitCaptureHash"] = StandbyUnitCaptureHash(context)', "queue.Add(trigger)"]),
        ("CoreRuleEngine.StandbyObservers.cs", "ValidStandbyObserver", ["ValidStandbyUnitObservation(state, c, effect, source, controller)"]),
        ("CoreRuleEngine.StandbyObservers.cs", "ResolveStandbyObserver", ["ResolveStandbyUnitObserver(state, item)"]),
        ("CoreRuleEngine.cs", "ResolveStackItemEffectCore", [
            "if (stackItem.StandbyObserver is not null) return ResolveStandbyObserver(state, stackItem)"]),
        ("CoreRuleEngine.StandbyUnitObservers.cs", "ValidStandbyUnitObservation", [
            "birth.ControllerId", "birth.CapturedTick > state.Tick", "AttachedRuleTextRules.ValidCaptured(",
            "PredictBindingMatches(state, context.Source", 'birth.RuleTextInstanceId == $"PRINTED:']),
        ("CoreRuleEngine.StandbyUnitObservers.cs", "ResolveStandbyUnitObserver", [
            "ValidStandbyUnitObserverStack(state, item)", "source.ObjectGeneration == context.Source.Generation",
            "ApplyReadyState(source," if hidden else "ApplySpellSkillDamageBonuses(state, item.ControllerId, target,",
            "CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo)" if hidden else "ApplyDamageToCardObject(state, cards, item.ControllerId, target,",
            "IsObjectOnField(state.PlayerZones, source.ObjectId)" if hidden else "RunStateBasedCleanupLoop(state, zones, cards, item,"]),
        ("MatchRecovery.cs", "ValidateTurnSequenceState", [
            "CoreRuleEngine.ValidStandbyUnitObserverStack(state, item)", "CoreRuleEngine.StandbyObserverEventKind(trigger.EffectKind)"]),
        ("MatchRecovery.cs", "ValidateFrame", ["ValidateStandbyUnitObserverHistory(authoritativeState, commands, events, errors)"]),
        ("MatchRecovery.StandbyUnitObservers.cs", "ValidateStandbyUnitObserverHistory", [
            'ev.Event.Kind == "TRIGGER_QUEUED"', "CoreRuleEngine.StandbyUnitCaptureHash(context)",
            "command.Accepted", "command.CompletedTick == queued[0].Tick", "CoreRuleEngine.StandbyUnitActionHash(action.Event) == birth.ActionHash"]),
    ]
    if hidden:
        checks += [
            ("CoreRuleEngine.Standby.cs", "ResolveStandbyHide", ["QueueStandbyHiddenUnitObservers(QueueStandbyHiddenTokens("]),
            ("CoreRuleEngine.StandbyUnitObservers.cs", "QueueStandbyHiddenUnitObservers", [
                'action.Kind != "CARD_HIDDEN"', "StandbyHiddenReadyEffect", "QueueStandbyPlayedObservers(result with"]),
        ]
    else:
        checks += [
            ("CoreRuleEngine.StandbyObservers.cs", "CaptureStandbyPlayed", ["CaptureStandbyUnitObservers(ev, zones, cards, player, StandbyPlayedEnemyDamageEffect)"]),
            ("CoreRuleEngine.BattleResultContinuation.cs", "CollectResolutionEffects", ["collected = QueueStandbyPlayedObservers(collected)"]),
            ("CoreRuleEngine.cs", "PlaySourceEquipmentToBase", ["CaptureStandbyPlayed(playedEvent, playerZones, cardObjects, stackItem.ControllerId)"]),
            ("CoreRuleEngine.SpellCompletion.cs", "ResolveStackItemEffect", [
                "result.CompletedCardPlayIds?.Contains(item.StackItemId) == true || deferCompletion || item.PlayCost is null",
                "result.PendingCardChoice is not null || result.PendingEffectPlay is not null",
                "if (item.PlayedFromStandby is not null)", "new GameEvent(FaceDownSpellCompletedEvent,",
                "CaptureStandbyPlayed(action,", "result.PlayerZones, result.CardObjects, item.ControllerId)"]),
            ("CoreRuleEngine.UnitEntryTriggers.cs", "CaptureUnitEntry", ["CaptureStandbyPlayed(ev, zones, cards, player)"]),
            ("CoreRuleEngine.FieldTriggers.cs", "NeedsTriggerConfirmation", ["StandbyPlayedEnemyDamageEffect", "i.TargetGenerations is null"]),
            ("CoreRuleEngine.FieldTriggers.cs", "PrepareTriggerConfirmation", ["PrepareStandbyUnitDamage(result, item)"]),
            ("CoreRuleEngine.FieldTriggers.cs", "ValidTriggerChoice", ["ValidStandbyUnitDamageChoice(state, choice)"]),
            ("CoreRuleEngine.TriggerTargetPayment.cs", "ValidTriggerTargetPayment", ["StandbyPlayedEnemyDamageEffect && ValidStandbyUnitObserverStack(state, item)"]),
            ("CoreRuleEngine.TriggerTargetPayment.cs", "ResolveTriggerTargetPayment", ["StandbyUnitDamageTargets(state, item)"]),
            ("CoreRuleEngine.StandbyUnitObservers.cs", "StandbyUnitDamageTargets", [
                "EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) != item.ControllerId",
                "TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId)"]),
            ("MatchRecovery.StandbyUnitObservers.cs", "ValidateStandbyUnitObserverHistory", [
                '"PAYMENT_WINDOW_OPENED" : "TRIGGER_CONFIRMED"', '"targetGenerations"',
                "MatchStateHasher.HashValue(item.TargetGenerations)"]),
        ]
    # A completed-spell route cannot certify an earlier confirmation bypass.
    exact = exact and (hidden or "CaptureStandbyPlayed(" not in member("CoreRuleEngine.cs", "ResolvePlayCard"))
    exact = exact and all(all(token in member(filename, name) for token in tokens) for filename, name, tokens in checks)
    paths = sorted({prefix + filename for filename, _, _ in checks})
    return result("route" if exact else "missing", "按实际藏匿／背面打出行动捕获控制者、来源及每份复制文本，独立确认并结算；恢复核对原始行动与目标代次"
                  if exact else "缺少精确的待命行动监听规格、独立结算、目标付款或原始历史核验消费者", paths)


def combat_spell_cost_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    match = re.fullmatch(r"如果我处于战斗中则所有友方法术的费用减少\{\{([1-9][0-9]*)\}\}和\{\{A\}\}不得低于\{\{([0-9]+)\}\}而所有敌方法术的费用增加\{\{([1-9][0-9]*)\}\}和\{\{A\}\}", text)
    if not match:
        return None
    expected = dict(friendlyManaReduction=int(match[1]), friendlyGenericPowerReduction=1,
                    minimumFriendlyMana=int(match[2]), enemyManaIncrease=int(match[3]), enemyGenericPowerIncrease=1)
    behaviors = card.get("behaviors", [])
    spec = behaviors[0].get("combatSpellCostAdjustment") if len(behaviors) == 1 else None
    exact = clause["kind"] == "static" and normalize(unparenthesize(clause["scope"])) == text \
        and card.get("cardCategory") in {"unit", "hero_unit"} and len(behaviors) == 1 \
        and behaviors[0].get("playsSourceToBaseAsUnit") is True and spec == expected \
        and all(type(value) is int for value in (spec or {}).values())
    prefix = "src/Riftbound.Engine/"

    def member(filename, name):
        code = sources.files.get(prefix + filename, "")
        start = re.search(r"(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b" + re.escape(name) + r"\s*\(", code)
        if not start:
            return ""
        tail = code[start.end():]
        end = re.search(r"(?m)^    (?:private|internal|public) ", tail)
        return code[start.start():start.end()] + (tail[:end.start()] if end else tail)

    checks = [
        ("CoreRuleEngine.CombatSpellCosts.cs", "IsSpellCostSourceInBattle", [
            "IsFaceUpNonStandbyUnit(source)", "IsEstablishedBattleDuelParticipant(state, source.ObjectId)",
            "battle.IsActive", "battle.AttackerObjectIds.Contains(source.ObjectId)",
            "battle.DefenderObjectIds.Contains(source.ObjectId)",
            "IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, source.ObjectId, battlefield)"]),
        ("CoreRuleEngine.CombatSpellCosts.cs", "CombatSpellCostSources", [
            "PublicStaticAuraSourceObjectIds(state.PlayerZones)", "!IsSpellCostSourceInBattle(state, source)",
            "EffectiveFieldControllerId(state.PlayerZones, id, source)", "AttachedRuleTextRules.Instances(state, source)",
            "behavior.CombatSpellCostAdjustment is { } spec", "yield return (controller, spec)"]),
        ("CoreRuleEngine.CombatSpellCosts.cs", "FriendlyCombatSpellCostReducers", [
            "!IsSpellPlayBehavior(behavior)", "EffectPlayIgnoresAllCosts(state, player)",
            "CombatSpellCostSources(state).Where(source => source.Controller == player)"]),
        ("CoreRuleEngine.ConditionalPlayCosts.cs", "OpponentSpellCostIncreases", [
            "!IsSpellPlayBehavior(behavior)", "EffectPlayIgnoresAllCosts(state, player)",
            "CombatSpellCostSources(state).Where(source => source.Controller != player)",
            "mana + source.Spec.EnemyManaIncrease", "power + source.Spec.EnemyGenericPowerIncrease"]),
        ("CoreRuleEngine.PlayManaCost.cs", "CalculatePlayManaCost", [
            "OpponentSpellCostIncreases(state, playerId, behavior).Mana", "FriendlyCombatSpellCostReducers(state, playerId, behavior)",
            'source.FriendlyManaReduction, source.MinimumFriendlyMana', 'OrderByDescending(item => item.Floor)',
            'total - Math.Max(0, reduction.Floor)', 'applied.GetValueOrDefault("combatSpell")']),
        ("CoreRuleEngine.EmpoweredSpellCosts.cs", "SpellPlayPowerOptions", [
            "FriendlyCombatSpellCostReducers(state, player, behavior).Sum(source => source.FriendlyGenericPowerReduction)",
            "OpponentSpellCostIncreases(state, player, behavior).Power", "ReduceSpellPower(a.GenericPower,"]),
        ("CoreRuleEngine.cs", "TryBuildPlayCardPlan", [
            "CalculatePlayManaCost(state, intent.PlayerId, behavior, extraManaCost,", "SpellPlayPowerOptions(state, intent.PlayerId, behavior,",
            "CombatSpellCostReductionMana = manaCost.CombatSpellReduction", "CombatSpellCostReductionPower = combatSpellPowerReduction",
            "PaymentCostRules.AuthorizePayment("]),
        ("CoreRuleEngine.cs", "ResolvePlayCard", ["PaymentCostRules.TryCommitPayment(", '["mana"] = plan.TotalManaCost']),
        ("CoreRuleEngine.PlayCostPreview.cs", "PreviewPlayCard", [
            "TryBuildPlayCardPlan(state,", '-plan.CombatSpellCostReductionMana, -plan.CombatSpellCostReductionPower',
            "plan.OpponentSpellCostIncreaseMana, plan.OpponentSpellCostIncreasePower"]),
        ("MatchSession.cs", "PlayCardSourceRequirement", [
            "CoreRuleEngine.MinimumPlayManaCost(state, playerId, behavior, sourceObjectId)",
            "CoreRuleEngine.SpellPowerReductionChoices(state, playerId, behavior)",
            'requirement["combatSpellCostReductionMana"] = manaCost.CombatSpellReduction']),
        ("CoreRuleEngine.PlayManaCost.cs", "MinimumPlayManaCost", ["CalculatePlayManaCost(state, playerId, behavior,"]),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens) for filename, name, tokens in checks)
    paths = sorted({prefix + filename for filename, _, _ in checks})
    return result("route", "按当前来源战斗身份、控制者与复制文本重算双方的共享法术总费用；增费先于减费，保留各减费的法力下限与符能分配", paths) \
        if exact else result("missing", "缺少精确的来源战斗条件、友敌双方费用规格或实际共享报价／付款消费者", paths)


def friendly_token_attack_movement_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    sentence = normalize('当我进攻时，你可以选择将自己任意数量的指示物单位移动到此战场。')
    if normalize(unparenthesize(clause['text'])) != sentence:
        return None
    profiles = card.get('behaviors', [])
    expected = dict(timing='ATTACK', effect='MOVE_FRIENDLY_UNIT_TOKENS_HERE',
                    optional=True, anyNumberOfTargets=True)
    exact = clause['kind'] == 'trigger' and normalize(unparenthesize(clause['scope'])) == sentence \
        and card.get('cardCategory') in {'unit', 'hero_unit'} and len(profiles) == 1
    if exact:
        profile = profiles[0]; spec = profile.get('unitEventAbility')
        exact = profile.get('playsSourceToBaseAsUnit') is True and isinstance(spec, dict) \
            and inventory_contains(spec, expected) \
            and type(spec.get('amount', 0)) is int and spec.get('amount', 0) == 0 \
            and not any(meaningful(value) for key, value in spec.items() if key not in expected)
    prefix = 'src/Riftbound.Engine/'
    def member(filename, name):
        code = sources.files.get(prefix + filename, '')
        starts = list(re.finditer(r'(?m)^    (?:private|internal|public) (?:static )?[^()\n{}=]*\b'
                          + re.escape(name) + r'\s*\(', code))
        if not starts:
            return ''
        # Private overloads hold the shared implementation after public wrappers.
        start = starts[-1]
        rest = code[start.start():]; offset = start.end() - start.start()
        end = re.search(r'(?m)^    (?:private|internal|public) ', rest[offset:])
        return rest[:offset + end.start()] if end else rest
    targets = member('CoreRuleEngine.UnitEvents.cs', 'UnitEventTargets')
    # Bind filters to this case. An unrelated enemy/friendly case or unused
    # method containing the expected token cannot certify this effect's scope.
    branch = re.search(r'FriendlyUnitTokensMoveHere\s*=>(.*?)\n\s*"MOVE_ENEMY_HERE"\s*=>', targets, re.S)
    # CN355.9 / 359.3.e.6: targets need only satisfy the printed noun scope.
    # Moving restrictions and already being here are checked at resolution.
    target_scope = ('IsFaceUpNonStandbyUnit(c) && IsTokenObject(c) && '
                    'EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId')
    exact = exact and branch is not None and re.sub(r'\s+', '', branch[1]).rstrip(',') \
        == re.sub(r'\s+', '', target_scope)
    exact = exact and 'internal const string FriendlyUnitTokensMoveHere = \"MOVE_FRIENDLY_UNIT_TOKENS_HERE\";' in sources.files.get(prefix + 'CoreRuleEngine.TokenUnitMovement.cs', '')
    checks = [
        ('CoreRuleEngine.BattlefieldTimingAliases.cs', 'TimingCaptureContext', [
            'UnitEventTimingMatches(unitEvent.Timing, "ATTACK")']),
        ('CoreRuleEngine.cs', 'BuildTriggerQueuedEvent', [
            'TimingCaptureContext(trigger)', 'TimingCaptureHash(timingContext, trigger.TimingAlias)']),
        ('MatchRecovery.BattlefieldTimingAliases.cs', 'ValidateBattlefieldTimingHistory', [
            'hash != CoreRuleEngine.TimingCaptureHash(item.Context, item.Alias)',
            '"TOKEN_CEASED_TO_EXIST"', 'c.Accepted']),
        ('MatchRecovery.BattlefieldTimingAliases.cs', 'HasValidatedCeasedBattlefieldUnitEventSource', [
            'P6TokenFactoryCatalog.IsTokenFactory(token)', 'CoreRuleEngine.TimingCaptureContext(stack)',
            'CoreRuleEngine.ValidUnitEventTargets(stack)']),
        ('CoreRuleEngine.FieldTriggers.cs', 'ResolveTriggerTargetConfirmation', [
            'item.StandbyObserver?.Unit is not null || item.UnitEvent is not null',
            '["targetGenerations"] = bound.TargetGenerations']),
        ('CoreRuleEngine.FieldTriggers.cs', 'CompleteTriggerTargetConfirmation', [
            'item.StandbyObserver?.Unit is not null || item.UnitEvent is not null',
            '["stackItemId"] = item.StackItemId', '["targetGenerations"] = confirmed.TargetGenerations']),
        ('MatchRecovery.UnitEventTargets.cs', 'ValidateUnitEventTargetHistory', [
            'CurrentGameAcceptedEventBoundary(state, commands, events)', 'state.PendingRuleChoice?.Origin',
            'frame.PendingEffectPlay?.Parent', 'record.Sequence > boundary',
            'if (item.TargetGenerations is null)', 'record.Tick <= frame.Tick',
            'record.Event.Kind is "PAYMENT_WINDOW_OPENED" or "TRIGGER_CONFIRMED"',
            'generations is not null', 'command.CompletedTick == record.Tick',
            'command.StartedEventSequence < record.Sequence', 'command.CompletedEventSequence >= record.Sequence',
            'targets.SequenceEqual(item.TargetObjectIds)',
            'MatchStateHasher.HashValue(generations) == MatchStateHasher.HashValue(item.TargetGenerations)',
            'confirmations.Length != 1', 'command.Accepted', 'command.PlayerId == item.ControllerId',
            'command.CompletedTick == confirmations[0].Tick',
            'command.StartedEventSequence < confirmations[0].Sequence',
            'command.CompletedEventSequence >= confirmations[0].Sequence']),
        ('MatchRecovery.cs', 'ValidateFrame', [
            'ValidateBattlefieldTimingHistory(authoritativeState, commands, events, errors)',
            'ValidateUnitEventTargetHistory(authoritativeState, commands, events, errors)']),
        ('CoreRuleEngine.cs', 'ResolveStackItemEffectCore', ['if (stackItem.UnitEvent is not null) return ResolveUnitEvent(state, stackItem)']),
        ('CoreRuleEngine.UnitEvents.cs', 'ResolveUnitEvent', ['ResolveUnitEventInstruction(state, item)']),
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureUnitEvents', ['IsObjectOnField(zones, id)',
            'AttachedRuleTextRules.Instances(zones, cards, card)', 'UnitEventTimingMatches(spec.Timing, timing)']),
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureAttackUnitEvents', [
            'CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK")', 'UnitEventTrigger(']),
        ('CoreRuleEngine.BattleStartRoles.cs', 'RecordBattleDuelRoles', [
            'CaptureFirstBattleRoles(', 'CaptureAttackUnitEvents(state, firstAttackers,', '.Select(BuildTriggerQueuedEvent)']),
        ('CoreRuleEngine.UnitEvents.cs', 'NeedsUnitEventTarget', ['FriendlyUnitTokensMoveHere']),
        ('CoreRuleEngine.UnitEvents.cs', 'UnitEventChoice', [
            'UnitEventSpec(item.UnitEvent!.CardNo)!.Optional ? 0 : 1',
            'UnitEventSpec(item.UnitEvent.CardNo)!.AnyNumberOfTargets ? legal.Length : 1']),
        ('CoreRuleEngine.UnitEvents.cs', 'ValidUnitEventTargets', [
            'AnyNumberOfTargets || item.TargetObjectIds.Count == 1',
            'item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count',
            'item.TargetGenerations.Count == item.TargetObjectIds.Count']),
        ('CoreRuleEngine.UnitEvents.cs', 'ResolveUnitEventInstruction', [
            'FriendlyUnitTokensMoveHere', 'return ResolvePermanentUnitEventInstruction(state, item, spec)']),
        ('CoreRuleEngine.PermanentUnitEvents.cs', 'ResolvePermanentUnitEventInstruction', [
            'if (spec.Effect == FriendlyUnitTokensMoveHere)', 'return ResolveFriendlyUnitTokenMovement(state, item)']),
        ('CoreRuleEngine.PermanentUnitEvents.cs', 'UnitEventHereDestination', [
            'CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration)',
            'Zone: "BATTLEFIELD"']),
        ('CoreRuleEngine.CombatMight.cs', 'CombatMightSourceLocation', [
            'source.ObjectGeneration == generation', 'IsObjectOnField(state.PlayerZones, sourceId)',
            'state.ObjectLocations.TryGetValue(sourceId, out var location)']),
        ('CoreRuleEngine.TokenUnitMovement.cs', 'ResolveFriendlyUnitTokenMovement', [
            'var destination = UnitEventHereDestination(state, item)', 'if (destination is null)',
            'UnitEventTargets(state, item).ToHashSet(StringComparer.Ordinal)',
            'StackObjectRules.ReferencesCurrentObject(state, item, id)',
            'if (selected.Length == 0)', 'MoveFieldUnitsToChosenLocation(', 'item, selected, destination, events)',
            'ObjectLocations = ReconcileObjectLocations(locations, zones)', 'RunePools = pools']),
        ('CoreRuleEngine.ChosenMovement.cs', 'MoveFieldUnitsToChosenLocation', [
            'cards[id].ObjectGeneration, cards[id].ControllerId', 'MovementTargetIsCurrent(current, item, id)',
            'IsUnitMovementDestination(current, id, destination)',
            'MovementRestrictionRules.CanMove(unit, item.ControllerId)',
            'PayImposedMovementCost(', 'CurrentMovers, events)', 'CaptureMovementBatchObservers(',
            'MoveAttachedEquipmentWithHost(', '["simultaneousSourceObjectIds"] = movable']),
        ('CoreRuleEngine.FieldTriggers.cs', 'ResolveTriggerTargetConfirmation', ['TargetGenerations = selected.ToDictionary']),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens)
                          for filename, name, tokens in checks)
    return result('route' if exact else 'missing',
        '进攻时独立确认任意数量友方单位指示物；结算重查原代际来源的当前位置与各目标，再按一批执行移动、装备跟随及施加费用；恢复绑定原始捕获和目标确认'
        if exact else '缺少进攻触发、任意数量友方单位指示物范围、结算时当前来源位置，或共享批量移动与原代际确认消费者',
        sorted({prefix + filename for filename, _, _ in checks}))


def stun_source_movement_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    if text != normalize("当你{{眩晕}}战场上的一名敌方单位时，你可以选择将我移动到该战场。"):
        return None
    behaviors = card.get("behaviors", [])
    exact = clause["kind"] == "trigger" and normalize(unparenthesize(clause["scope"])) == text \
        and card.get("cardCategory") in {"unit", "hero_unit"} and len(behaviors) == 1 \
        and behaviors[0].get("playsSourceToBaseAsUnit") is True \
        and behaviors[0].get("sourceMayMoveToStunnedEnemyBattlefield") is True
    prefix = "src/Riftbound.Engine/"

    def members(filename, name):
        code = sources.files.get(prefix + filename, "")
        starts = re.finditer(r"(?m)^    (?:private|internal|public) (?:static )?[^\n{;=]*\b" + re.escape(name) + r"\s*\(", code)
        bodies = []
        for match in starts:
            tail = code[match.end():]
            end = re.search(r"(?m)^    (?:private|internal|public) ", tail)
            bodies.append(code[match.start():match.end()] + (tail[:end.start()] if end else tail))
        return bodies

    checks = [
        ("CoreRuleEngine.StunObservers.cs", "StunMoveSourceBehavior", ["behavior.SourceMayMoveToStunnedEnemyBattlefield"]),
        ("CoreRuleEngine.StunObservers.cs", "CaptureStunObservers", [
            'var actor = cause.ControllerId', '["stunningPlayerId"] = actor',
            "!IsEnemyFieldUnit(zones, cards, actor, targetId)", 'at.Zone == "BATTLEFIELD"',
            "fieldCard.ObjectGeneration", "GetControlledFieldUnitObjectIds(zones, cards, actor)",
            "IsFaceUpNonStandbyUnit(source)", "AttachedRuleTextRules.Instances(zones, cards, source)",
            "field is not null && StunMoveSourceBehavior(instance.CardNo)", "StunMoveSourceKind)",
            "Battlefield: kind == StunMoveSourceKind ? field : null"]),
        ("CoreRuleEngine.StunObservers.cs", "QueueStunObservers", [
            "capture.Context", "context.Kind == StunMoveSourceKind ? result.State.Tick",
            "queue.Add(trigger)", '["stunSourceMovementCaptureHash"] = StunSourceMovementCaptureHash(context)']),
        ("CoreRuleEngine.BattleResultContinuation.cs", "CollectResolutionEffects", ["QueueStunObservers(state, result)"]),
        ("CoreRuleEngine.TriggerCosts.cs", "LeadingTriggerCost", ["item.FieldContext?.Kind == StunMoveSourceKind ? new()"]),
        ("CoreRuleEngine.TriggerCosts.cs", "ValidLeadingCostContext", ["StunMoveSourceKind ? ValidStunSourceMovementStack(item)"]),
        ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostChoices", [
            "Kind: StunMoveSourceKind", "ValidLeadingCostContext(item)", "ValidFieldContextState(state, movement,",
            "? [item.SourceObjectId] : []"]),
        ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostChoice", ["? OptionalTriggerWindow : TriggerCostWindow"]),
        ("CoreRuleEngine.TriggerCosts.cs", "ValidTriggerCostChoice", ["item.FieldContext?.Kind != StunMoveSourceKind"]),
        ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostSourceGeneration", ["item.FieldContext?.SourceGeneration"]),
        ("CoreRuleEngine.TriggerCosts.cs", "ValidTriggerCostReceipt", ["ValidStunSourceMovementStack(item)"]),
        ("CoreRuleEngine.TriggerCosts.cs", "CompleteTriggerCost", ["EquipmentDeathReturnKind or StunMoveSourceKind",
            "ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator)"]),
        ("CoreRuleEngine.FieldTriggers.cs", "NeedsTriggerConfirmation", ["StunMoveSourceKind"]),
        ("CoreRuleEngine.FieldTriggers.cs", "ResolveFieldTrigger", ["IsStunObserverKind(observer.Kind)", "ResolveStunObserver(state, item)"]),
        ("CoreRuleEngine.StunObservers.cs", "ResolveStunObserver", ["context.Kind == StunMoveSourceKind) return ResolveStunSourceMovement(state, item)"]),
        ("CoreRuleEngine.StunSourceMovement.cs", "ValidStunSourceMovementStack", [
            "ValidStunObserverShape(context, item.EffectKind, item.CardNo)", "context.StunObserver.Actor == item.ControllerId",
            "item.TargetObjectIds.Count == 0 && item.TargetGenerations is null", "!item.SourceConfirmed"]),
        ("CoreRuleEngine.StunSourceMovement.cs", "ResolveStunSourceMovement", [
            "!ValidStunSourceMovementStack(item)", "!ValidTriggerCostReceipt(state, item)",
            "source.ObjectGeneration != birth.Source.Generation", "battlefield.ObjectGeneration != field.Generation",
            "IsFaceUpNonStandbyUnit(source)", "TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, source.ObjectId,",
            'new(item.ControllerId, "BATTLEFIELD", field.ObjectId)', "RunStateBasedCleanupLoop(state, zones, cards, item, pools,"]),
        ("MatchRecovery.StunSourceMovement.cs", "HasCapturedStunSourceMovement", ["CoreRuleEngine.ValidFieldContextState(state, context,"]),
        ("MatchRecovery.StunSourceMovement.cs", "ValidateStunSourceMovementHistory", [
            "CoreRuleEngine.StunSourceMovementCaptureHash(entry.Context)", '"stunSourceMovementCaptureHash"',
            "command.Accepted", "command.CompletedTick == queued[0].Tick", '"stunningPlayerId"',
            "target == origin.Stunned.ObjectId", '"STUNNED"', "CommandTypes.ChooseCards",
            '"TRIGGER-COST:" + item.StackItemId', "window == CoreRuleEngine.OptionalTriggerWindow",
            "selected.SequenceEqual(new[] { item.SourceObjectId })"]),
        ("MatchRecovery.cs", "ValidateFrame", ["ValidateStunSourceMovementHistory(authoritativeState, commands, events, errors)"]),
        ("MatchRecovery.cs", "ValidateSpellContinuations", ["CoreRuleEngine.ValidStunSourceMovementStack(item)"]),
        ("MatchRecovery.cs", "CapturedBattleSourceIsPrivate", ["HasCapturedStunSourceMovement(state, item)"]),
    ]
    for filename, name in [
            ("CoreRuleEngine.BorrowedExhaust.cs", "ResolveBorrowedExhaustStack"),
            ("CoreRuleEngine.MovedEnemyStun.cs", "ResolveMovedEnemyStun"),
            ("CoreRuleEngine.PaidUnitStun.cs", "ResolvePaidUnitStun"),
            ("CoreRuleEngine.SimpleActivatedAbilities.cs", "ResolveSimpleActivationEffect"),
            ("CoreRuleEngine.TargetedPowerAbility.cs", "ResolveTargetedPowerModifierAbilityStackItem"),
            ("CoreRuleEngine.UnitEntryTriggers.cs", "ResolveUnitEntryTrigger"),
            ("CoreRuleEngine.UnitEventBoonInstructions.cs", "ApplyUnitEventStun"),
            ("CoreRuleEngine.cs", "ResolveStackItemEffectCore"),
            ("CoreRuleEngine.cs", "ResolveShadowStunAbilityStackItem")]:
        checks.append((filename, name, ["CaptureStunObservers("]))
    exact = exact and all((bodies := members(filename, name))
        and all(all(token in body for token in tokens) for body in bodies) for filename, name, tokens in checks)
    paths = sorted({prefix + filename for filename, _, _ in checks})
    return result("route" if exact else "missing", "共享眩晕观察按施加者捕获敌方战场、来源及每份文本；独立可选确认后通过效果移动，恢复核对原始眩晕和答复"
        if exact else "缺少精确的眩晕敌方战场条件、来源独立移动确认、效果移动或原始历史核验消费者", paths)


def conditional_unit_experience_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    poppy = normalize("当你打出我时，如果对手得分距离胜利得分不超过3分，则让我变为活跃状态，并获得3经验。")
    khazix = normalize("当我进攻或防守时，如果此处有一名落单的敌方单位，则让我本回合内{{S}}+2并获得2经验。")
    if text not in (poppy, khazix):
        return None
    ready = text == poppy
    expected = dict(timing="PLAY" if ready else "ATTACK_OR_DEFEND", effect="READY_SELF" if ready else "MIGHT_SELF",
                    amount=0 if ready else 2, independentExperienceGain=3 if ready else 2,
                    requiredOpponentVictoryDistance=3 if ready else 0, requiresIsolatedEnemyHere=not ready)
    spec = card.get("unitEventAbility") or {}
    exact = "unit" in card.get("cardCategory", "") and clause["kind"] == "trigger" \
        and normalize(clause["scope"]) == text \
        and all(spec.get(key, False if isinstance(value, bool) else 0) == value for key, value in expected.items()) \
        and not any(meaningful(value) for key, value in spec.items() if key not in expected)
    prefix = "src/Riftbound.Engine/"
    units = sources.files.get(prefix + "CoreRuleEngine.UnitEvents.cs", "")
    experience = sources.files.get(prefix + "CoreRuleEngine.UnitEventExperience.cs", "")

    def method(source, name):
        # Look inside the live method, so an unused helper elsewhere cannot
        # certify the event condition or independent resolution continuation.
        match = re.search(r"(?:private|internal|public) static [^\n]+ " + re.escape(name) + r"\(", source)
        return re.split(r"\n    (?:private|internal|public) static ", source[match.end():], maxsplit=1)[0] if match else ""

    resolve = method(units, "ResolveUnitEvent")
    experience_call = "AppendIndependentUnitEventExperience(state, item, result)"
    experience_returned = "return " + experience_call + ";" in resolve or (
        re.search(r"(?m)^\s*result = " + re.escape(experience_call) + ";", resolve)
        and "return source == item.SourceObjectId ? result : result with { Events = result.Events.Select" in resolve)
    exact = exact and "ResolveUnitEventInstruction(state, item)" in resolve \
        and experience_returned
    append = method(experience, "AppendIndependentUnitEventExperience")
    exact = exact and "GainExperience(result.PlayerExperience, item.ControllerId, spec.IndependentExperienceGain," in append \
        and "PlayerExperience = experience" in append and "Events = events" in append
    evidence = [prefix + "CoreRuleEngine.UnitEvents.cs", prefix + "CoreRuleEngine.UnitEventExperience.cs"]
    if ready:
        capture = method(units, "CaptureOtherUnitPlayed")
        check = method(sources.files.get(prefix + "CoreRuleEngine.cs", ""), "OpponentWithinWinningScoreDistance")
        boon = sources.files.get(prefix + "CoreRuleEngine.UnitEventBoonInstructions.cs", "")
        exact = exact and all(symbol in capture for symbol in ["RequiredOpponentVictoryDistance > 0",
            "observations = observations.Where(", "OpponentWithinWinningScoreDistance(current, o.Controller,",
            "UnitEventSpec(o.Context.CardNo)!.RequiredOpponentVictoryDistance"])
        exact = exact and "opponentScore >= EffectiveWinningScore(state) - distance" in check \
            and '"READY_SELF"' in units and "ResolveUnitEventBoonInstruction(state, item, spec)" in units \
            and "context.SourceGeneration" in boon and "ApplyReadyState(" in boon
        evidence += [prefix + "CoreRuleEngine.cs", prefix + "CoreRuleEngine.UnitEventBoonInstructions.cs"]
    else:
        condition = method(experience, "UnitEventCaptureConditionSatisfied")
        exact = exact and all(".Where(o => UnitEventCaptureConditionSatisfied(state, o))" in method(units, name)
            for name in ["CaptureAttackUnitEvents", "CaptureRoleUnitEvents"])
        exact = exact and all(symbol in condition for symbol in ["RequiresIsolatedEnemyHere", 'here.Zone != "BATTLEFIELD"',
            "IsFaceUpNonStandbyUnit(card)", "!= observation.Controller", "== here.BattlefieldObjectId",
            ".GroupBy(card => EffectiveFieldControllerId(", ".Any(group => group.Count() == 1)"])
        instruction = method(units, "ResolveUnitEventInstruction")
        exact = exact and all(symbol in instruction for symbol in ['spec.Effect == "MIGHT_SELF"',
            "source.ObjectGeneration == context.SourceGeneration", "ApplyDirectUntilEndPowerModifier(", "spec.Amount"])
    recovery = sources.files.get(prefix + "MatchRecovery.cs", "")
    exact = exact and "ValidUnitEvent(" in recovery and "ValidUnitEventTargets(" in recovery
    evidence.append(prefix + "MatchRecovery.cs")
    if exact:
        return result("route", "触发时检查胜利分差／此处敌方落单，普通结算链处理原来源，独立获得经验", evidence)
    return result("missing", "缺少精确的触发条件、原来源活跃／本回合战力或独立经验结算路径")


def confirmed_spell_mana_alternative_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    match = re.fullmatch(r"如果你在本回合消耗了不低于\{\{([1-9][0-9]*)\}\}的费用来打出一个法术则你可以选择支付\{\{(红色|绿色|蓝色|黄色|橙色|紫色)\}\}来将我打出", text)
    if not match:
        return None
    trait = dict(红色="red", 绿色="green", 蓝色="blue", 黄色="yellow", 橙色="orange", 紫色="purple")[match[2]]
    expected = dict(minimumPaidMana=int(match[1]), power=1, traits=[trait])
    behaviors = card.get("behaviors", [])
    exact = clause["kind"] == "rule" and normalize(unparenthesize(clause["scope"])) == text \
        and card.get("cardCategory") in {"unit", "hero_unit"} and len(behaviors) == 1 \
        and behaviors[0].get("playsSourceToBaseAsUnit") is True
    spec = behaviors[0].get("spellManaAlternativeCost", {}) if len(behaviors) == 1 else {}
    exact = exact and spec == expected and type(spec.get("minimumPaidMana")) is int and type(spec.get("power")) is int
    prefix = "src/Riftbound.Engine/"

    def member(filename, name):
        code = sources.files.get(prefix + filename, "")
        start = re.search(r"(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b" + re.escape(name) + r"\s*\(", code)
        if not start:
            return ""
        tail = code[start.end():]
        end = re.search(r"(?m)^    (?:private|internal|public) ", tail)
        return code[start.start():start.end()] + (tail[:end.start()] if end else tail)

    checks = [
        ("CoreRuleEngine.SpellManaAlternativeCost.cs", "ControllerConfirmedSpellWithPaidMana", [
            "Finalized: { } plays", "history.TurnNumber == state.TurnNumber", "PaidMana: { } mana",
            "play.PlayerId == player", "(play.Types & 4) != 0", "mana >= minimum"]),
        ("CoreRuleEngine.SpellManaAlternativeCost.cs", "SpellManaAlternativePlayBehaviors", [
            "ControllerConfirmedSpellWithPaidMana(state, player, spec.MinimumPaidMana)",
            "AppliedSpellManaAlternativeCost = null", "AppliedSpellManaAlternativeCost = payable"]),
        ("CoreRuleEngine.SpellManaAlternativeCost.cs", "TrySelectSpellManaAlternativeCost", [
            "chosen.Length != 1", "SpellManaAlternativePlayBehaviors(state, player, behavior)",
            "SpellManaAlternativeCostChoiceId(cost) == chosen[0].Trim()", "if (variant is null) return false"]),
        ("CoreRuleEngine.cs", "TryBuildPlayCardPlan", [
            "TrySelectSpellManaAlternativeCost(state, intent.PlayerId, behavior, command.OptionalCosts,",
            "out behavior, out var costsWithoutSpellManaAlternative)", "OptionalCosts = costsWithoutSpellManaAlternative",
            "EffectPlaySources(state, intent.PlayerId).Contains(command.SourceObjectId, StringComparer.Ordinal)",
            "SpellPlayPowerOptions(state, intent.PlayerId, behavior,", "totalManaCost: totalManaCost",
            "PaymentCostRules.AuthorizePayment("]),
        ("CoreRuleEngine.cs", "ResolvePlayCard", [
            "PaymentCostRules.TryCommitPayment(", "PlayCost = IsSpellPlayBehavior(behavior) ? new(",
            "behavior.ManaCost, plan.TotalManaCost) : null", "CaptureCardPlayHistory(events[0]",
            '["playId"] = stackItem.StackItemId', '["mana"] = plan.TotalManaCost']),
        ("CoreRuleEngine.BattleResultContinuation.cs", "CollectResolutionEffects", [
            "collected = RecordCardPlayHistory(state, collected)"]),
        ("CoreRuleEngine.PlayManaCost.cs", "CalculatePlayManaCost", [
            "behavior.AppliedSpellManaAlternativeCost is not null", "Math.Max(0, additionalMana", "+ increase + spellIncrease"]),
        ("PrintedPowerCostRules.cs", "ForPlay", [
            "behavior.AppliedSpellManaAlternativeCost is { } alternative ? new(alternative.Power, alternative.Traits)"]),
        ("CoreRuleEngine.PlayCostPreview.cs", "PreviewPlayCard", [
            "TryBuildPlayCardPlan(state,", "plan.Behavior.AppliedSpellManaAlternativeCost is { } alternative",
            "alternative.Power - PrintedPowerCostRules.ForCard(plan.Behavior.CardNo).Amount"]),
        ("MatchSession.cs", "PlayCardPromptBehaviorsForSource", [
            "CoreRuleEngine.SpellManaAlternativePlayBehaviors(state, playerId, behavior)"]),
        ("MatchSession/Prompts.Metadata.cs", "PlayCardChoices", [
            "PlayCardPromptBehaviorsForSource(state, playerId, objectId)"]),
        ("MatchSession.cs", "PlayCardOptionalCostChoicesForBehavior", [
            "CoreRuleEngine.SpellManaAlternativeCostChoiceId(alternative)"]),
        ("CoreRuleEngine.CardPlayHistory.cs", "RecordCardPlayHistory", [
            "PaidMana = !observation.Resolved && (entry.Types & 4) != 0 ? confirmedItem?.PlayCost?.PaidMana : null",
            "new ConfirmedSpellManaPayment(entry.Source, entry.CardNo, entry.PlayerId,",
            "entry.PlayId, paidMana, before.TurnNumber, entry.Tick)", "PaidMana = confirmed?.PaidMana",
            "Tick = !observation.Resolved && entry.PaidMana is not null",
            "? RuleChoices.Value?.CompletionTick ?? result.State.Tick : result.State.Tick"]),
        ("SpellManaPaymentHistoryRules.cs", "ValidateOriginalPayments", [
            "state.CardPlays?.Finalized", 'e.Event.Kind == "CARD_PLAYED"', 'e.Event.Kind == "COST_PAID"',
            "ReadReceipt(birth.Event)", "c.Accepted", "c.CompletedTick == birth.Tick",
            "ValidConfirmationOrigin(play, birth!, command, commands, events)",
            "receipt.Source != play.Source", "receipt.Mana != play.PaidMana",
            "receipt.TurnNumber != state.TurnNumber", 'Number(payment.Event, "mana") != play.PaidMana']),
        ("SpellManaPaymentHistoryRules.cs", "ValidConfirmationOrigin", [
            "if (birth.Tick != play.Tick) return false", "IsOriginalPlay(completed, play, birth)",
            "command.Accepted && command.CompletedTick < completed.CompletedTick",
            "IsOriginalPlay(command, play, birth)", "next.StartedTick != previous.CompletedTick",
            "next.StartedEventSequence != previous.CompletedEventSequence",
            "next.CommandType != CommandTypes.PayCost",
            "ReadCommand(next) is not PayCostCommand", "PaymentWindow: CoreRuleEngine.RuleChoiceWindow",
            "PaymentChoiceIds.Count: 1", 'e.Event.Kind == "RULE_CHOICE_REQUESTED"',
            'requests.Length != 1 || Text(requests[0].Event, "choiceId") != answer.PaymentId',
            'Text(requests[0].Event, "playerId") != next.PlayerId', "previous == completed",
            "IsScenarioPreparation(completed, events)", "play.Tick > completed.StartedTick"]),
        ("SpellManaPaymentHistoryRules.cs", "IsOriginalPlay", [
            "command.PlayerId == play.PlayerId", "command.CompletedTick == command.StartedTick + 1",
            'play.PlayId == $"STACK-{command.CompletedTick}-{play.Source.ObjectId}"',
            "command.CommandType == CommandTypes.PlayCard", "command.CommandType == CommandTypes.RevealCard",
            'Flag(birth.Event, "playedFromStandby")', "original.SourceObjectId == play.Source.ObjectId",
            "original.CardNo == play.CardNo"]),
        ("SpellManaPaymentHistoryRules.cs", "ReadCommand", [
            "GameCommandJsonMapper.Map(raw)", "command.TypedCommand?.TryRestore(out var typed)"]),
        ("SpellManaPaymentHistoryRules.cs", "IsScenarioPreparation", [
            "command.TypedCommand?.TryScenario(out var typed)", 'e.Event.Kind == "DEV_SCENARIO_SEEDED"',
            'Text(seeded[0].Event, "scenarioId") == scenario', 'Text(seeded[0].Event, "playerId") == command.PlayerId']),
        ("MatchRecovery.cs", "ValidateFrame", [
            "SpellManaPaymentHistoryRules.ValidateOriginalPayments(authoritativeState, commands, events)"]),
        ("MatchRecovery.cs", "ValidateRecoveryFrameAsync", [
            "SpellManaPaymentHistoryRules.ValidateOriginalPayments(paymentState, recovery.Commands, recovery.Events)"]),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens) for filename, name, tokens in checks)
    paths = sorted({prefix + filename for filename, _, _ in checks})
    return result("route", "按本回合施法者确认时的单次实际法力付款解锁显式替代基础费用；共享报价、付款、提示和原始历史恢复",
                  paths) if exact else result("missing", "缺少精确实际付款门槛、显式替代基础费用或共享报价／付款／原始历史恢复消费者", paths)


def token_play_observer_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    if clause["kind"] != "trigger" or clause["scope"] != clause["text"] \
            or normalize(unparenthesize(clause["text"])) != normalize("当你打出一名指示物单位时，让我本回合内{{S}}+1。"):
        return None
    expected = dict(timing="FRIENDLY_UNIT_PLAYED", effect="MIGHT_SELF", amount=1, requiresPlayedUnitToken=True)
    events = [card.get("unitEventAbility")] + [b.get("unitEventAbility") for b in card.get("behaviors", [])]
    precise = any(b.get("playsSourceToBaseAsUnit") for b in card.get("behaviors", [])) and any(
        event and all(event.get(key) == value for key, value in expected.items())
        and not any(meaningful(value) for key, value in event.items() if key not in expected) for event in events)
    path = "src/Riftbound.Engine/CoreRuleEngine.UnitEvents.cs"
    code = sources.files.get(path, "")
    start = code.find("private static GameEvent CaptureOtherUnitPlayed(")
    end = code.find("private static GameEvent CaptureEquipmentPlayed(", start)
    capture = code[start:end] if 0 <= start < end else ""
    capture_tokens = ['"UNIT_TOKEN_CREATED"', '"FRIENDLY_UNIT_PLAYED"',
        'EffectiveFieldControllerId(zones, c.ObjectId, c) == player',
        '!UnitEventSpec(o.Context.CardNo)!.RequiresPlayedUnitToken', '|| TokenObjectRules.IsToken(entered)',
        'return WithUnitEvents(ev, observations);']
    requirements = {
        "CoreRuleEngine.UnitEntryTriggers.cs": ["CaptureOtherUnitPlayed(ev, zones, cards, entered, player, turnEffects)"],
        "CoreRuleEngine.UnitTokenEntry.cs": ['CaptureUnitEntry(new("UNIT_TOKEN_CREATED"'],
        "CoreRuleEngine.UnitEvents.cs": [
            "IsObjectOnField(zones, id)", "!card.IsFaceDown", "UnitEventTrigger(captured,",
            'spec.Effect == "MIGHT_SELF"', "source.ObjectGeneration == context.SourceGeneration",
            "IsObjectOnField(zones, source.ObjectId)",
            "ApplyDirectUntilEndPowerModifier(source, source.ObjectId, item.SourceObjectId, context.CardNo,",
            '"UNIT_EVENT", spec.Amount, source.Power + spec.Amount'],
    }
    if precise and all(token in capture for token in capture_tokens) \
            and all(all(token in sources.files.get("src/Riftbound.Engine/" + name, "") for token in tokens)
                    for name, tokens in requirements.items()):
        return result("route", "己方指示物单位入场的实际观察、独立响应技能和同代来源的本回合战力加成",
                      ["src/Riftbound.Engine/" + name for name in requirements])
    return result("missing", "缺少己方打出指示物单位的精确条件、实际入场观察或同代来源的独立本回合加成")


def another_friendly_here_grant_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    match = re.fullmatch(r"当我进攻或防守时让你在此处的另一名单位在本回合内获得\{\{S\}\}\+([1-9][0-9]*)和\{\{(壁垒|后排)\}\}", text)
    if not match:
        return None
    amount, keyword = int(match[1]), match[2]
    profiles = [card.get("unitEventAbility")] + [b.get("unitEventAbility") for b in card.get("behaviors", [])]
    supported = {"timing", "effect", "amount", "targetsAnotherFriendlyHere", "grantedCombatKeyword"}
    exact = clause["kind"] == "trigger" and normalize(unparenthesize(clause["scope"])) == text \
        and card.get("cardCategory") in ("unit", "hero_unit") and any(
            p and p.get("timing") == "ATTACK_OR_DEFEND" and p.get("effect") == "MIGHT_FRIENDLY"
            and p.get("amount") == amount and p.get("targetsAnotherFriendlyHere") is True
            and p.get("grantedCombatKeyword") == keyword
            and not any(value for key, value in p.items() if key not in supported) for p in profiles)
    prefix = "src/Riftbound.Engine/"
    units = sources.files.get(prefix + "CoreRuleEngine.UnitEvents.cs", "")
    def method(source, name):
        found = re.search(r"(?:private|internal|public) static [^\n]+ " + re.escape(name) + r"\(", source)
        return re.split(r"\n    (?:private|internal|public) static ", source[found.end():], maxsplit=1)[0] if found else ""
    capture = method(units, "CaptureUnitEvents")
    targets = method(units, "UnitEventTargets")
    resolve = method(units, "ResolveUnitEvent")
    if "ResolveUnitEventInstruction(state, item)" in resolve:
        resolve = method(units, "ResolveUnitEventInstruction")
    permanent = sources.files.get(prefix + "CoreRuleEngine.PermanentUnitEvents.cs", "")
    here = method(permanent, "UnitEventTargetIsHere")
    role_timing = 'spec.Timing == "ATTACK_OR_DEFEND" && timing is "ATTACK" or "DEFEND"' in capture \
        or 'UnitEventTimingMatches(spec.Timing, timing)' in capture \
            and 'declared?.Split("_OR_", StringSplitOptions.None).Contains(timing, StringComparer.Ordinal) == true' \
                in method(units, "UnitEventTimingMatches")
    exact = exact and role_timing \
        and 'CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK")' in method(units, "CaptureAttackUnitEvents") \
        and "CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, role)" in method(units, "CaptureRoleUnitEvents")
    exact = exact and all(symbol in targets for symbol in [".TargetsAnotherFriendlyHere", "c.ObjectId != item.SourceObjectId",
        "EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId", "UnitEventTargetIsHere(state, item, c.ObjectId)"])
    exact = exact and all(symbol in here for symbol in ["CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration)",
        "at.Zone == here.Zone", "at.PlayerId == here.PlayerId", "at.BattlefieldObjectId == here.BattlefieldObjectId"])
    direct_keyword = all(symbol in resolve for symbol in ["TemporaryCombatKeywordRules.Prefix + keyword",
        "TemporaryCombatKeywordRules.AppliedEffectId(cards[buffed]",
        "UntilEndOfTurnEffects = [..cards[buffed].UntilEndOfTurnEffects, effect]"])
    group_path = prefix + "CoreRuleEngine.UnitEventGroupMight.cs"
    keyword_helper = method(sources.files.get(group_path, ""), "GrantUnitEventCombatKeyword")
    shared_keyword = "GrantUnitEventCombatKeyword(state, item, cards, events, buffed, keyword)" in resolve \
        and all(symbol in keyword_helper for symbol in ["TemporaryCombatKeywordRules.Prefix + keyword",
            "TemporaryCombatKeywordRules.AppliedEffectId(cards[target]", "TemporaryCombatKeywordRules.IsValid(effect)",
            "UntilEndOfTurnEffects = [..cards[target].UntilEndOfTurnEffects, effect]"])
    exact = exact and all(symbol in resolve for symbol in ['spec.Effect == "MIGHT_FRIENDLY"',
        "StackObjectRules.ReferencesCurrentObject(state, item, buffed)", "UnitEventTargets(state, item).Contains(buffed)",
        "ApplyPowerModifier(state, cards[buffed]", "spec.Amount", "spec.GrantedCombatKeyword is { } keyword"]) \
        and (direct_keyword or shared_keyword)
    recovery = sources.files.get(prefix + "MatchRecovery.cs", "")
    exact = exact and "ValidUnitEvent(" in recovery and "ValidUnitEventTargets(" in recovery
    evidence = [prefix + p for p in ["CoreRuleEngine.UnitEvents.cs", "CoreRuleEngine.PermanentUnitEvents.cs", "MatchRecovery.cs"]]
    if shared_keyword:
        evidence.append(group_path)
    return result("route", "进攻／防守确认此处另一名友军，结算重查原代际与当前位置，授予本回合战力和战斗关键词", evidence) if exact else result(
        "missing", "缺少精确进攻／防守规格、此处另一名友方目标，或本回合战力及关键词的共享结算")


def paid_target_play_stun_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    fee = normalize('你可以选择支付{{绿色}}，作为打出我的额外费用。')
    stun = normalize('当你打出我时，如果你支付了该额外费用，则{{眩晕}}一名敌方单位。')
    text = normalize(unparenthesize(clause['text']))
    if text not in {fee, stun}:
        return None
    effect = normalize(unparenthesize(card.get('cardEffect', '')))
    profiles = card.get('behaviors', [])
    exact = clause['kind'] == ('rule' if text == fee else 'trigger') \
        and normalize(unparenthesize(clause['scope'])) == text \
        and card.get('cardCategory') in {'unit', 'hero_unit'} and fee in effect and stun in effect \
        and len(profiles) == 1
    expected = dict(playsSourceToBaseAsUnit=True, requiredTargetCount=1, statusEffectId='STUNNED',
        targetScope='ENEMY_UNIT', targetScopeIsExplicit=True, targetRequiredTag='CARD_TYPE:UNIT',
        targetEffectAdditionalPowerCost=1, targetEffectAdditionalPowerTrait='green')
    if exact:
        profile = profiles[0]
        # A separate Hold sentence does not constrain the paid play trigger.
        unit_event = profile.get('unitEventAbility')
        exact = inventory_contains(profile, expected) \
            and all(type(profile.get(key, 0)) is int and profile.get(key, 0) == 0
                for key in ['damageAmount', 'minTargetCount', 'targetEffectAdditionalManaCost']) \
            and (not meaningful(unit_event) or isinstance(unit_event, dict) and unit_event.get('timing') == 'HOLD') \
            and not meaningful(profile.get('playsSourceToBaseAsEquipment'))
    prefix = 'src/Riftbound.Engine/'
    def member(filename, name):
        code = sources.files.get(prefix + filename, '')
        start = re.search(r'(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b'
            + re.escape(name) + r'\s*\(', code)
        if not start:
            return ''
        rest = code[start.start():]
        offset = start.end() - start.start()
        end = re.search(r'(?m)^    (?:private|internal|public) ', rest[offset:])
        return rest[:offset + end.start()] if end else rest
    checks = [
        ('CoreRuleEngine.cs', 'ResolveCoreAsync', [
            'return Complete(ResolvePlayCard(state, intent, playCardCommand));',
            'TryResolveP0ContractCommand(state, intent, command, out var p0ContractResult)',
            'return ValueTask.FromResult(RecordPaidPermanentPlayHistory(state, completed));']),
        ('CoreRuleEngine.cs', 'ResolvePlayCard', ['TryBuildPlayCardPlan(state, intent, command', 'ConfirmPlayedPermanent(']),
        ('CoreRuleEngine.cs', 'TryBuildPlayCardPlan', ['TryBuildOptionalCostPlan(',
            'HasIndependentPermanentTargets(behavior) && TargetEffectAdditionalCostPaid(behavior, optionalCosts)']),
        ('CoreRuleEngine.cs', 'TryBuildOptionalCostPlan', ['TryBuildTargetEffectAdditionalCost(',
            'extraPowerCostByTrait = targetEffectExtraPowerCostByTrait']),
        ('CoreRuleEngine.cs', 'TryBuildTargetEffectAdditionalCost', ['normalizedOptionalCosts.Count != expectedCostCount',
            'TryParseSpendPowerOptionalCost(', 'spendPowerCost == behavior.TargetEffectAdditionalPowerCost',
            'RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait)']),
        ('CoreRuleEngine.PermanentConfirmation.cs', 'HasPendingPermanentPlayAbility', [
            'OfficialPrintedTimingRules.HasPlayTrigger(',
            '!HasTargetEffectAdditionalCost(behavior) || TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)']),
        ('CoreRuleEngine.PermanentConfirmation.cs', 'PendingPermanentPlayAbility', [
            'SourceConfirmed = true', '"TRIGGER_QUEUED"', 'Append(ability)']),
        ('CoreRuleEngine.PermanentPlayTargets.cs', 'PermanentPlayTargetBehavior', [
            'TargetEffectAdditionalCostPaid(current, item.OptionalCosts)', 'MinTargetCount = current.RequiredTargetCount']),
        ('CoreRuleEngine.PermanentPlayTargets.cs', 'PreparePermanentPlayTargets', [
            'PermanentPlayTargetChoice(', 'ValidPermanentPlayTargetSelection(', 'ResolveTriggerTargetConfirmation(']),
        ('CoreRuleEngine.cs', 'TryResolveP0ContractCommand', [
            'CommandTypes.ChooseCards', 'result = ResolveChooseCardsRuntime(state, intent, command);']),
        ('CoreRuleEngine.cs', 'ResolveChooseCardsRuntime', [
            'pendingChoice.ChoiceWindow == "TRIGGER_CONFIRMATION"', 'ResolveTriggerTargetConfirmation(']),
        ('CoreRuleEngine.FieldTriggers.cs', 'ResolveTriggerTargetConfirmation', [
            'ValidPermanentPlayTargetSelection(state, item, selected)', 'ResolveSpellshieldTargetTaxPower(',
            'TargetGenerations = selected.ToDictionary']),
        ('CoreRuleEngine.cs', 'ResolveStackItemEffectCore', [
            'HasPendingPermanentPlayAbility(state, stackItem, behavior)', 'PendingPermanentPlayAbility(',
            'ResolveStatusEffectIds(behavior', 'UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects',
            'statusEffectId == "STUNNED"', 'CaptureStunObservers(applied,']),
        ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'ValidPaidPermanentPlayChoices', [
            'item.OptionalCosts is not null', 'TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)',
            'TryBuildConfirmedPermanentTargetAdditionalCost(item, behavior']),
        ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'PaidPermanentPlaySkills', [
            'state.PendingRuleChoice?.Origin', 'frame.PendingEffectPlay?.Parent', 'IsPaidPermanentPlaySkill(item)']),
        ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'PaidPermanentPlayChoicesHash', [
            'item.StackItemId', 'item.SourceObjectId', 'item.ControllerId', 'item.CardNo', 'item.EffectKind', 'item.OptionalCosts']),
        ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'RecordPaidPermanentPlayHistory', [
            'PaidPermanentPlaySkills(before)', 'PaidPermanentPlaySkills(result.State)',
            'ev.Kind == kind', 'PaidPermanentPlayCaptured', 'PaidPermanentPlayRetired',
            'PaidPermanentPlayChoicesHash(item)', 'events.Add(new(']),
        ('MatchRecovery.PaidPermanentPlayHistory.cs', 'ValidatePaidPermanentPlayHistory', [
            'CurrentGameAcceptedEventBoundary(state, commands, events)', 'command.Accepted',
            'command.CompletedTick == record.Tick', 'command.StartedEventSequence < record.Sequence',
            'command.CompletedEventSequence >= record.Sequence', '!captured.TryAdd(', '!pending.Add(',
            '!pending.Remove(', 'CoreRuleEngine.ValidPaidPermanentPlayChoices(item)',
            'proof != CoreRuleEngine.PaidPermanentPlayChoicesHash(item)', '!pending.SetEquals(']),
        ('MatchRecovery.cs', 'ValidateFrame', [
            'requireAcceptedBattleHistory && authoritativeState is not null',
            'ValidatePaidPermanentPlayHistory(authoritativeState, commands, events, errors)']),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens)
        for filename, name, tokens in checks)
    evidence = sorted({prefix + filename for filename, _, _ in checks})
    return result('route', '额外绿色费用在打出时独立声明并付款；入场后确认眩晕目标及法盾，独立技能与原始已接受费用和存续记录绑定', evidence) if exact else result(
        'missing', '缺少绿色额外打出费用、已付后的独立眩晕消费者，或原始费用选择／技能存续校验')


def paid_equipment_theft_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    sentences = [normalize(text) for text in [
        '你可以选择支付{{橙色}}{{橙色}}，作为打出我的额外费用。',
        '当你打出我时，如果你支付了该额外费用，则可以将一件敌方装备移动到你的基地。',
        '你控制这件装备，直到我离场为止。',
        '如果它是一件武装，则将其贴附到我身上。']]
    text = normalize(unparenthesize(clause['text']))
    if text not in sentences:
        return None
    index = sentences.index(text)
    exact = clause['kind'] == ('rule' if index == 0 else 'trigger') \
        and normalize(unparenthesize(clause['scope'])) == sentences[0 if index == 0 else 1] \
        and card.get('cardCategory') in {'unit', 'hero_unit'} \
        and all(sentence in normalize(unparenthesize(card.get('cardEffect', ''))) for sentence in sentences)
    profiles = card.get('behaviors', [])
    exact = exact and len(profiles) == 1 and inventory_contains(profiles[0], dict(
        playsSourceToBaseAsUnit=True, requiredTargetCount=1,
        targetScope='ENEMY_UNIT_OR_EQUIPMENT', targetScopeIsExplicit=True, targetRequiredTag='CARD_TYPE:EQUIPMENT',
        targetEffectAdditionalPowerCost=2, targetEffectAdditionalPowerTrait='orange',
        paidPlayTargetsOptional=True, stealsTargetEquipmentUntilSourceLeaves=True))
    exact = exact and all(type(profiles[0].get(key, 0)) is int and profiles[0].get(key, 0) == 0
        for key in ['damageAmount', 'minTargetCount', 'targetEffectAdditionalManaCost'])
    prefix = 'src/Riftbound.Engine/'

    def member(filename, name):
        code = sources.files.get(prefix + filename, '')
        found = re.search(r'(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b' + re.escape(name) + r'\s*\(', code)
        if not found:
            return ''
        rest = code[found.start():]
        offset = found.end() - found.start()
        end = re.search(r'(?m)^    (?:private|internal|public) ', rest[offset:])
        return rest[:offset + end.start()] if end else rest

    checks = [
        ('CoreRuleEngine.cs', 'ResolveCoreAsync', ['return Complete(ResolvePlayCard(state, intent, playCardCommand));',
            'RecordEquipmentControlHistory(state, completed)', 'RecordPaidPermanentPlayHistory(state, completed)']),
        ('CoreRuleEngine.cs', 'ResolvePlayCard', ['TryBuildPlayCardPlan(state, intent, command', 'ConfirmPlayedPermanent(']),
        ('CoreRuleEngine.cs', 'TryBuildPlayCardPlan', ['TryBuildOptionalCostPlan(',
            'HasIndependentPermanentTargets(behavior) && TargetEffectAdditionalCostPaid(behavior, optionalCosts)']),
        ('CoreRuleEngine.cs', 'TryBuildOptionalCostPlan', ['TryBuildTargetEffectAdditionalCost(']),
        ('CoreRuleEngine.cs', 'TryBuildTargetEffectAdditionalCost', ['normalizedOptionalCosts.Count != expectedCostCount',
            'spendPowerCost == behavior.TargetEffectAdditionalPowerCost', 'RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait)']),
        ('CoreRuleEngine.PermanentConfirmation.cs', 'HasPendingPermanentPlayAbility', [
            '!HasTargetEffectAdditionalCost(behavior) || TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)']),
        ('CoreRuleEngine.PermanentConfirmation.cs', 'PendingPermanentPlayAbility', [
            'SourceConfirmed = true', 'declaration.StealsTargetEquipmentUntilSourceLeaves',
            'PermanentPlaySource = new(item.SourceObjectId, cards[item.SourceObjectId].ObjectGeneration)', 'Append(ability)']),
        ('CoreRuleEngine.PermanentPlayTargets.cs', 'PermanentPlayTargetBehavior', ['!current.PaidPlayTargetsOptional']),
        ('CoreRuleEngine.PermanentPlayTargets.cs', 'PreparePermanentPlayTargets', ['PermanentPlayTargetChoice(', 'ResolveTriggerTargetConfirmation(']),
        ('CoreRuleEngine.cs', 'ResolveChooseCardsRuntime', ['pendingChoice.ChoiceWindow == "TRIGGER_CONFIRMATION"', 'ResolveTriggerTargetConfirmation(']),
        ('CoreRuleEngine.FieldTriggers.cs', 'ResolveTriggerTargetConfirmation', ['ValidPermanentPlayTargetSelection(state, item, selected)',
            'ResolveSpellshieldTargetTaxPower(', 'TargetGenerations = selected.ToDictionary']),
        ('CoreRuleEngine.cs', 'ResolveStackItemEffectCore', ['TryResolveSourceStealEnemyEquipment(']),
        ('CoreRuleEngine.cs', 'IsEquipmentObject', ['HasValidFieldEquipmentAttachment(state, objectId)']),
        ('CoreRuleEngine.cs', 'TryDestroyControlledFieldTarget', ['HasValidFieldObjectIdentity(state, playerZones, cardObjects, targetObjectId)']),
        ('CoreRuleEngine.EquipmentTargets.cs', 'HasValidFieldObjectIdentity', ['HasValidFieldEquipmentAttachment(state with']),
        ('CoreRuleEngine.cs', 'IsEnemyEquipmentObject', ['EffectiveFieldControllerId(state.PlayerZones, objectId, state.CardObjects[objectId]) != playerId']),
        ('CoreRuleEngine.EquipmentTargets.cs', 'HasValidFieldEquipmentAttachment', ['FieldObjectTypeRules.HasUnitType(gear)',
            'FindFieldObjectLocation(state.PlayerZones, hostId) != location', 'equipmentAt.BattlefieldObjectId == hostAt.BattlefieldObjectId']),
        ('CoreRuleEngine.cs', 'TryGainControlOfTargetToBase', ['EquipmentControl = null']),
        ('CoreRuleEngine.cs', 'TryGainControlOfTargetToBattlefield', ['EquipmentControl = null']),
        ('CoreRuleEngine.EquipmentTheft.cs', 'TryResolveSourceStealEnemyEquipment', ['behavior.StealsTargetEquipmentUntilSourceLeaves',
            '!item.SourceConfirmed', 'TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)',
            'MovementTargetIsCurrent(current, item, target)', 'IsEnemyEquipmentObject(current, item.ControllerId, target)',
            'MovementRestrictionRules.CanMove(', 'PayImposedMovementCost(', 'CaptureMovementBatchObservers(', 'CaptureUnitEvents(',
            'item.PermanentPlaySource ?? PlayInstructionSource(state, item)', 'unit.ObjectGeneration == binding.Generation',
            'AddFieldObjectToLocation(zones, item.ControllerId, MoveUnitBaseZone, target)', 'if (hasSource)',
            'EquipmentControl = new(original, underlying,', 'new(item.StackItemId, source!, item.ControllerId, reason)',
            'AttachedEquipmentObjectIds(cards, target)', 'MoveAttachedEquipmentWithHost(zones, attached, item.ControllerId, target, MoveUnitBaseZone)',
            'equipment.Tags.Contains(CardEquipmentKeywordNames.Weapon', 'TryAttachEquipment(']),
        ('CoreRuleEngine.EquipmentTheft.cs', 'ReturnSourceStolenEquipmentForMissingSources', [
            'lease.Equipment != new ObjectBinding(gear.ObjectId, gear.ObjectGeneration)',
            'source.ObjectGeneration == grant.Source.Generation', 'IsObjectOnField(zones, source.ObjectId)',
            'active.LastOrDefault()?.ControllerId ?? lease.UnderlyingControllerId', 'AttachedToObjectId = attachment']),
        ('CoreRuleEngine.cs', 'RunStateBasedCleanupLoop', ['ReturnSourceStolenEquipmentForMissingSources(', 'ApplyUnattachedEquipmentCleanup(']),
        ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'PaidPermanentPlayChoicesHash', ['item.OptionalCosts', 'item.PermanentPlaySource']),
        ('MatchRecovery.PaidPermanentPlayHistory.cs', 'ValidatePaidPermanentPlayHistory', ['command.Accepted',
            'proof != CoreRuleEngine.PaidPermanentPlayChoicesHash(item)', '!pending.SetEquals(']),
        ('MatchRecovery.EquipmentControlHistory.cs', 'ValidateEquipmentControlHistory', ['ValidEquipmentControlLeases(state)',
            'command.Accepted', '!captured.TryAdd(', 'proof != row.Proof', '!pending.SetEquals(']),
        ('CoreRuleEngine.EquipmentControlHistory.cs', 'RecordEquipmentControlHistory', ['EquipmentControlReceipts(before)',
            'EquipmentControlReceipts(result.State)', 'EquipmentControlCaptured', 'EquipmentControlRetired', 'events.Add(new(']),
        ('CoreRuleEngine.EquipmentControlHistory.cs', 'ValidEquipmentControlLeases', ['lease.Equipment.Generation == card.ObjectGeneration',
            'card.ControllerId == lease.Grants.LastOrDefault()?.ControllerId', '!string.IsNullOrWhiteSpace(grant.Source.ObjectId)']),
        ('MatchRecovery.cs', 'ValidateFrame', ['ValidateEquipmentControlHistory(authoritativeState, commands, events, errors)',
            'ValidatePaidPermanentPlayHistory(authoritativeState, commands, events, errors)']),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens) for filename, name, tokens in checks)
    evidence = sorted({prefix + filename for filename, _, _ in checks})
    return result('route', '共享额外费用先付款，入场后可选敌方装备；独立技能重查目标，移动后按原来源代际取得控制／贴附，持续效果与原始接受历史绑定', evidence) if exact else result(
        'missing', '缺少精确橙色额外费用、入场后可选装备目标、移动／控制／贴附消费者或原始来源与持续时间凭据')


def paid_source_ready_might_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    sentence = normalize("当你打出我时，如果你支付了该额外费用，则让我变为活跃状态，并让我本回合内{{S}}+2。")
    if normalize(unparenthesize(clause["text"])) != sentence:
        return None
    exact = clause["kind"] == "trigger" and normalize(unparenthesize(clause["scope"])) == sentence \
        and card.get("cardCategory") in {"unit", "hero_unit"}
    behaviors = card.get("behaviors", [])
    exact = exact and len(behaviors) == 1 and inventory_contains(behaviors[0], {
        "playsSourceToBaseAsUnit": True, "sourceReadyPowerModifierAdditionalPowerCost": 1,
        "sourceReadyPowerModifierAdditionalPowerTrait": "red", "sourceReadyPowerModifierAmount": 2})
    prefix = "src/Riftbound.Engine/"
    def member(filename, name):
        code = sources.files.get(prefix + filename, "")
        start = re.search(r"(?m)^    (?:private|internal|public) static [^\n]*\b" + re.escape(name) + r"\s*\(", code)
        if not start:
            return ""
        rest = code[start.start():]
        end = re.search(r"(?m)^    (?:private|internal|public) static ", rest[start.end() - start.start():])
        return rest[:start.end() - start.start() + end.start()] if end else rest
    checks = [
        ("CoreRuleEngine.PermanentPlaySource.cs", "SourceOptionalReadyPowerCostWasPaid", [
            "behavior.SourceReadyPowerModifierAdditionalPowerCost > 0", "behavior.SourceReadyPowerModifierAmount != 0",
            "RuneTrait.Normalize(behavior.SourceReadyPowerModifierAdditionalPowerTrait)", "item.OptionalCosts.Any(",
            "TryParseSpendPowerOptionalCost(", "amount == behavior.SourceReadyPowerModifierAdditionalPowerCost",
            "RuneTrait.Normalize(trait) == requiredTrait"]),
        ("CoreRuleEngine.PermanentPlaySource.cs", "PlayInstructionSource", [
            "state.CardPlays.Resolved.FirstOrDefault(", "play.PlayId == item.StackItemId", "play.CardNo == item.CardNo",
            "play.PlayerId == item.ControllerId", "play.Source.ObjectId == item.SourceObjectId"]),
        ("CoreRuleEngine.PermanentConfirmation.cs", "HasPendingPermanentPlayAbility", [
            "OfficialPrintedTimingRules.HasPlayTrigger(", "behavior.SourceReadyPowerModifierAdditionalPowerCost <= 0",
            "SourceOptionalReadyPowerCostWasPaid(behavior, item)"]),
        ("CoreRuleEngine.cs", "ResolveStackItemEffectCore", [
            "HasPendingPermanentPlayAbility(state, stackItem, behavior)",
            "PendingPermanentPlayAbility(state, stackItem, playerZones, cardObjects, events)",
            "TryResolveSourceUnitOptionalReadyPower(\n                state,"]),
        ("CoreRuleEngine.PermanentConfirmation.cs", "PendingPermanentPlayAbility", [
            "SourceConfirmed = true", '"TRIGGER_QUEUED"', "state.StackItems.Where(", "Append(ability)"]),
        ("CoreRuleEngine.cs", "TryResolveSourceUnitOptionalReadyPower", [
            "SourceOptionalReadyPowerCostWasPaid(behavior, stackItem)", "!stackItem.SourceConfirmed",
            "PlayInstructionSource(state, stackItem)", "sourceState.ObjectGeneration != source.Generation",
            "!IsFaceUpNonStandbyUnit(sourceState)", "!IsObjectOnField(playerZones, source.ObjectId)",
            "ApplyDirectUntilEndPowerModifier(", "behavior.SourceReadyPowerModifierAmount",
            "var canReady = ReadyByEffectPermittedByFieldRules(playerZones, cardObjects, sourceState)",
            "if (canReady) AddReadiedObservation(", "IsExhausted = canReady ? false : sourceState.IsExhausted",
            "if (canReady) events.Add(CaptureSelfReady(",
            "cardObjects[stackItem.SourceObjectId] = nextSourceState"]),
        ("PermanentPlayTriggerRecoveryRules.cs", "Valid", [
            "CoreRuleEngine.SourceOptionalReadyPowerCostWasPaid(optionalReady, item)",
            "instruction.SourceReadyPowerModifierAdditionalPowerCost > 0", "CoreRuleEngine.PlayInstructionSource(state, item)",
            "entered.ObjectGeneration < receipt.Generation"]),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens) for filename, name, tokens in checks)
    exact = exact and any("PermanentPlayTriggerRecoveryRules.Valid(" in code
        for path, code in sources.files.items() if "MatchRecovery" in Path(path).name)
    evidence = sorted({prefix + filename for filename, _, _ in checks})
    return result("route", "已付额外费才捕获独立入场技能，按成功打出回执核验场上原代际后活跃并授予本回合战力", evidence) if exact else result(
        "missing", "缺少已付额外费用触发条件、独立技能消费者，或来源打出回执／场上原代际检查")


def provided_armament_keyword_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause['text']))
    if text != normalize('你的每件武装都会提供{{强攻}}。'):
        return None
    if normalize(unparenthesize(clause['scope'])) != text:
        return result('missing', '武装提供关键词的范围与句子不一致')
    provider = sources.files.get('src/Riftbound.Engine/CoreRuleEngine.ArmamentKeywords.cs', '')
    if not has_public_armament_keyword_binding(sources) or 'source.Tags.Contains(CardObjectTags.Standby)' in provider:
        return result('missing', '正面单位、武装或来源拥有待命关键词不会失去其公开关键词效果')
    profile = dict(kind='FRIENDLY_ARMAMENT_PROVIDED_KEYWORD', layer='RULE_TEXT',
        duration='WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD', targetScope='FRIENDLY_EQUIPMENT',
        participantScope='FRIENDLY_PUBLIC_FIELD_EQUIPMENT', targetFilter='TAG:武装', grantedKeyword='强攻')
    candidates = [row['node'] for row in card.get('staticAuras', [])]
    if not any(all(node.get(key) == value for key, value in profile.items())
               and not any(meaningful(value) for key, value in node.items()
                   if key not in set(profile) | {'text', 'status', 'reason'}) for node in candidates):
        return result('missing', '缺少友方武装向其宿主提供强攻的持续规格；战斗数值加成不能替代关键词')
    requirements = {
        'CoreRuleEngine.ArmamentKeywords.cs': [
            'PublicStaticAuraSourceObjectIds(state.PlayerZones)',
            'source.IsFaceDown',
            'var provided = FriendlyArmamentProvidedKeywordGrants(state);',
            'IsArmamentKeywordProvider(gear)',
            'EffectiveFieldControllerId(state.PlayerZones, gear.ObjectId, gear)',
            'EffectivePublicStaticAuraSourceControllerId(state.PlayerZones, sourceId, source)',
            'aura.Kind != StaticAuraKinds.FriendlyArmamentProvidedKeyword',
            'keywords.Add(aura.GrantedKeyword)',
            'IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host)',
            '.Concat(provided.GetValueOrDefault(',
        ],
        'CoreRuleEngine.RuleDerivedKeywords.cs': [
            'var armamentKeywords = AttachedArmamentKeywordGrants(state);',
            '.Concat(armamentKeywords.GetValueOrDefault(id) ?? [])',
            'var amount = keywords.Sum(k => CombatKeywordAmount([k], counted));',
            'RuleDerivedTags = derived.Length == 0 ? null : derived',
        ],
        'CoreRuleEngine.CountedCombatKeywords.cs': [
            'var evaluated = RecomputeRuleDerivedKeywords(state);',
            'CombatKeywordAmount(unit.RuleDerivedTags ?? [], keyword)',
        ],
        'CoreRuleEngine.cs': [
            'if (conditionalPowerAuras)\n        {\n            state = RecomputeRuleDerivedKeywords(state with { PlayerZones = playerZones });',
            'cardObject = state.CardObjects.GetValueOrDefault(objectId) ?? cardObject;',
        ],
    }
    evidence = ['src/Riftbound.Engine/' + name for name in requirements]
    if not all(all(token in sources.files.get('src/Riftbound.Engine/' + name, '') for token in tokens)
               for name, tokens in requirements.items()):
        return result('missing', '缺少按武装控制者持续提供宿主关键词及共享当前关键词读取的完整执行路径', evidence)
    if 'CountLucianLegendEquipmentAssaultBonus(' in sources.files.get('src/Riftbound.Engine/CoreRuleEngine.cs', ''):
        return result('missing', '旧战斗专用加成尚未移除，可能重复计算武装强攻', evidence)
    return result('route', '按武装当前控制者持续提供宿主强攻，并复用当前关键词与战斗计算', evidence)

def enemy_death_source_clause_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    bare = normalize(unparenthesize(clause['text']))
    ready = normalize('当任意数量的敌方单位被摧毁时，让我变为活跃状态。')
    gold = normalize('每回合限一次，如果我位于战场上，当一名敌方单位被摧毁时，打出一个休眠的“金币”装备指示物。')
    if bare not in (ready,gold):
        return None
    if normalize(unparenthesize(clause['scope'])) != bare:
        return result('missing','敌方摧毁触发的条件或范围与本句不一致')
    is_ready = bare == ready
    profile = dict(kind='SOURCE_ENEMY_BATCH_DESTROYED_READY' if is_ready else 'SOURCE_BATTLEFIELD_ENEMY_DESTROYED_ONCE_GOLD',
        timing='UNIT_DESTROYED',targetScope='ENEMY_DESTROYED_UNIT')
    profile.update(dict(readiesSource=True) if is_ready else dict(oncePerTurn=True,createdTokenCount=1,createdTokenName='金币',
        createdTokenDestination='BASE',createdTokenExhausted=True,requiresSourceBattlefieldAtTrigger=True))
    if not any(all(row['node'].get(key)==value for key,value in profile.items())
        and not any(meaningful(value) for key,value in row['node'].items() if key not in set(profile)|{'text','reason','status'})
        for row in card.get('triggers',[])):
        return result('missing','缺少匹配敌方关系、批次或每回合限制及来源位置的死亡触发规格')
    needed={
        'CoreRuleEngine.DeathObservers.cs':[
            'var observerController = EffectivePublicStaticAuraSourceControllerId(zones, source.ObjectId, source);',
            '!source.IsFaceDown',
            'IsEnemySourceDeathKind(spec.Kind)',
            'c.SourceGeneration <= currentSource.ObjectGeneration',
            'zones.Values.Any(zone => zone.LegendZone.Contains(source.ObjectId))',
            'return ResolveEnemySourceDeathObserver(state, item, c, spec, resolved);',
        ],
        'CoreRuleEngine.ConditionalDeathObservers.cs':['return ValidEnemySourceDeathRelation(context, controller);'],
        'CoreRuleEngine.EnemyDeathSourceTriggers.cs':[
            'context.Destroyed.ControllerId != controller',
            'source.ObjectGeneration == context.SourceGeneration' if is_ready else 'CreateLegendEquipmentToken(zones, cards, item.ControllerId',
        ],
        'UnitDestroyedTriggerSpecRules.cs':[
            'IsEnemyBatchReadySourceTrigger' if is_ready else 'IsBattlefieldEnemyDestroyedOnceGoldTrigger',
        ],
    }
    needed['CoreRuleEngine.DeathObservers.cs'] += [
        'or TriggerKinds.SourceEnemyBatchDestroyedReady',
        '!observedBatches.Add((death.Batch, observer.ObjectId, observer.Context.RuleTextInstanceId))'] if is_ready else [
        'spec.RequiresSourceBattlefieldAtTrigger == true && capturedSource.Zone != "BATTLEFIELD"',
        'or TriggerKinds.SourceBattlefieldEnemyDestroyedOnceGold)',
        'effects.Contains(marker, StringComparer.Ordinal)',
        'effects = AddUntilEndOfTurnEffect(effects, marker)']
    paths=['src/Riftbound.Engine/'+name for name in needed]
    if is_ready:
        needed['CoreRuleEngine.EnemyDeathSourceTriggers.cs'].append('!source.IsFaceDown')
    public_source_paths = ['src/Riftbound.Engine/CoreRuleEngine.DeathObservers.cs']
    if is_ready:
        public_source_paths.append('src/Riftbound.Engine/CoreRuleEngine.EnemyDeathSourceTriggers.cs')
    if any('!source.Tags.Contains(CardObjectTags.Standby)' in sources.files.get(path, '') for path in public_source_paths):
        return result('missing','待命关键词不能替代背面状态及区域检查；正面打出的单位仍可拥有待命关键词',paths)
    if not all(all(token in sources.files.get('src/Riftbound.Engine/'+name,'') for token in tokens) for name,tokens in needed.items()):
        return result('missing','缺少按死亡时控制关系捕获、限制次数并独立入链结算的共享消费者',paths)
    if 'ResolveSivirEnemyDestroyedReadyTriggers(' in sources.files.get('src/Riftbound.Engine/CoreRuleEngine.cs',''):
        return result('missing','仍存在按所属者立即活跃的旧捷径，不能认证可响应触发',paths)
    return result('route','共享死亡观察：按控制关系捕获，批次或每回合限制，入链后处理活跃或独立金币',paths)


def boon_mode_activation_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    header = "消耗我的增益：从以下中选择一个你本回合内尚未选过的效果-"
    modes = {
        "·对战场上的一名单位造成2点伤害。":
            ("CONSUME_BOON_DAMAGE_2", dict(battlefieldUnitDamage=2), 1, 2),
        "·眩晕战场上的一名单位。":
            ("CONSUME_BOON_STUN_UNIT", dict(stunsTarget=True), 1, 0),
        "·让我变为活跃状态。":
            ("CONSUME_BOON_READY_SELF", dict(readiesSource=True), 0, 0),
        "·使我本回合内获得{{游走}}。":
            ("CONSUME_BOON_ROAM_SELF", dict(grantsSourceRoam=True), 0, 0),
    }
    text, scope = normalize(unparenthesize(clause["text"])), normalize(unparenthesize(clause["scope"]))
    head = normalize(header)
    if clause["kind"] != "activated" or head not in scope:
        return None
    if scope != head or text not in {head, *(normalize(t) for t in modes)}:
        return result("missing", "增益模式必须绑定完整费用及本回合未选过限制；未知选项不能借用其他技能")

    # Zero is meaningful for nullable costs/target limits. In particular an
    # extra minimum-might-zero exhaustion cost must not disappear as a default.
    nullable = {"maximumTargetCount", "generatedManaIfSourceEmpowered",
                "powerModifierAmountIfSourceEmpowered", "exhaustFriendlyUnitMinimumPowerAsCost", "runeCallCount"}
    metadata = {"sourceCardNo", "displayName", "reason"}

    def signature(spec):
        return {key: signature(value) if isinstance(value, dict) else value
                for key, value in spec.items() if key not in metadata
                and (value is not None if key in nullable else meaningful(value))}

    core = "src/Riftbound.Engine/CoreRuleEngine.SimpleActivatedAbilities.cs"
    dispatch = "src/Riftbound.Engine/CoreRuleEngine.cs"
    recovery = "src/Riftbound.Engine/MatchRecovery.PerPlayerActivationUses.cs"
    tokens = "src/Riftbound.Engine/CoreRuleEngine.Tokens.cs"
    paths = [core, dispatch, recovery, tokens]

    def method_has(path, name, *markers):
        body = re.search(r"(?ms)^    (?:private|internal|public|protected) static [^\n]*\b"
                         + re.escape(name) + r"\(.*?(?=^    (?:private|internal|public|protected)\s|\Z)",
                         sources.files.get(path, ""))
        return body is not None and all(marker in body.group(0) for marker in markers)

    tracked_uses = re.search(
        r"(?ms)^    private static readonly [^;\n]*\bPerPlayerActivationAbilities\s*=.*?;",
        sources.files.get(recovery, ""))
    common = (
        method_has(dispatch, "ResolveActivateAbility",
                   "if (ability.SimpleEffect is not null)",
                   "return ResolveSimpleActivation(state, intent, command, ability);")
        and method_has(dispatch, "ResolveStackItemEffectCore",
                       "if (stackItem.SimpleActivation is not null",
                       "return ResolveSimpleActivationEffect(state, stackItem, simpleAbility);")
        and method_has(core, "IsSimpleAbilitySource",
                   "ability.SimpleEffect?.ConsumesSourceBoonAsCost != true || BoonRules.Count(source) > 0")
        and method_has(core, "ResolveSimpleActivation", "IsSimpleAbilitySource(afterResources",
                       "if (spec.ConsumesSourceBoonAsCost)", "source = BoonRules.Consume(source);",
                       "ConsumedSourceBoon = consumedSourceBoon", "RunStateBasedCleanupLoop(afterResources",
                       'payload["sourceGeneration"] = source.ObjectGeneration',
                       "SimpleActivationUse(new(source.ObjectId, source.ObjectGeneration)")
        and method_has(core, "SimpleActivationInstanceAvailable",
                       "ability.SimpleEffect?.OncePerTurn != true", "SimpleActivationUse(new(source.ObjectId",
                       "instance.Origin is null ? null : instance.Id")
        and method_has(core, "ValidSimpleActivation", "context.ConsumedSourceBoon != context.Source")
        and method_has(tokens, "FinalizeTokenDepartures",
                       "!ValidSimpleActivation(before with { Tick = result.State.Tick }, item)",
                       "CeasedSource = new(source.TokenFactoryCardNo ?? source.CardNo!, result.State.Tick)")
        and tracked_uses is not None
        and ".Where(a => a.SimpleEffect is { OncePerTurn: true })" in tracked_uses.group(0)
        and method_has(recovery, "ValidatePerPlayerActivationUseHistory",
                       "var abilities = PerPlayerActivationAbilities;",
                       'TryReadObjectString(payload, "abilityInstanceId"', 'TryReadObjectLong(payload, "sourceGeneration"',
                       "accepted[entry.Tick].Any", "CoreRuleEngine.SimpleActivationUse(",
                       "expected.SetEquals(actual)")
    )
    if not common:
        return result("missing", "缺少增益费用、付款前目标绑定、技能实例次数或离场恢复的实际共享消费者", paths)

    def bound(mode):
        ability_id, effect, targets, damage = modes[mode]
        expected = dict(abilityId=ability_id, effectKind=ability_id, requiredTargetCount=targets,
                        damageAmount=damage, appliesSpellshieldTargetTax=bool(targets),
                        targetScope="BATTLEFIELD_UNIT" if targets else "", supportsCopiedRuleText=True,
                        sourceRequiredTag="CARD_TYPE:UNIT",
                        simpleEffect=dict(effect, consumesSourceBoonAsCost=True, oncePerTurn=True))
        candidates = [a for a in card.get("activations", []) if a.get("abilityId") == ability_id]
        if not candidates or not all(signature(a) == signature(expected) for a in candidates):
            return False
        if targets and not (
            method_has(core, "IsSimpleAbilityTarget",
                       "ability.TargetScope == CardTargetScopes.BattlefieldUnit",
                       'state.ObjectLocations.GetValueOrDefault(id)?.Zone == "BATTLEFIELD"')
            and method_has(core, "ResolveSimpleActivation",
                           "var tokenTargets = targets.Where(id => state.CardObjects.TryGetValue(id, out var target)",
                           "state.CardObjects[id].TokenFactoryCardNo ?? state.CardObjects[id].CardNo!",
                           ".Where(id => state.CardObjects.ContainsKey(id))",
                           ".ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal);",
                           "TargetGenerations = targetGenerations.Count == 0 ? null : targetGenerations")
        ):
            return False
        consumer = {
            "CONSUME_BOON_DAMAGE_2": ("if (spec.BattlefieldUnitDamage > 0)",
                                    "item.TargetGenerations![target] == cards[target].ObjectGeneration",
                                    "ApplyDamageToCardObject("),
            "CONSUME_BOON_STUN_UNIT": ("if (spec.StunsTarget)",
                                     'Concat(["STUNNED"])', "CaptureStunObservers("),
            "CONSUME_BOON_READY_SELF": ("if (spec.ReadiesSource",
                                      "source.ObjectGeneration == item.SimpleActivation!.Source.Generation",
                                      "ApplyReadyState(", "if (spec.SourcePowerBonus != 0)"),
            "CONSUME_BOON_ROAM_SELF": ("if (spec.GrantsSourceRoam",
                                     "roaming.ObjectGeneration == item.SimpleActivation!.Source.Generation",
                                     "GrantLegendRoam("),
        }[ability_id]
        return method_has(core, "ResolveSimpleActivationEffect", *consumer)

    if text == head:
        declared = {normalize(row["text"]) for row in card_clauses(card) if normalize(row["scope"]) == head}
        ok = declared == {head, *(normalize(t) for t in modes)} and all(bound(mode) for mode in modes)
    else:
        ok = bound(next(mode for mode in modes if normalize(mode) == text))
    return result("route" if ok else "missing",
                  "共享增益付款、独立模式次数及入链结算；目标和来源保留付款前代际，复制文本分别计数"
                  if ok else "缺少与本选项的费用、位置、数值和回合限制完全匹配的激活规格／实际结算",
                  paths)


def friendly_unit_trait_score_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    sentence = normalize('当我征服或据守一处战场时，如果你的单位合计具有以下所有属性标签，则你获得1分 — “鸟类”、“猫科”、“犬形”和“魄罗”。')
    if normalize(unparenthesize(clause['text'])) != sentence:
        return None
    tags = ['鸟类', '猫科', '犬形', '魄罗']
    modes = {'CHOOSE_BIRD': '鸟类', 'CHOOSE_CAT': '猫科', 'CHOOSE_DOG': '犬形', 'CHOOSE_PORO': '魄罗'}
    profiles = card.get('behaviors', [])
    expected = dict(timing='CONQUEST_OR_HOLD', effect='SCORE_IF_FRIENDLY_UNITS_HAVE_ALL_TAGS',
                    amount=1, requiredFriendlyUnitTagsForScore=tags)
    def exact_profile(profile):
        spec = profile.get('unitEventAbility')
        return isinstance(spec, dict) and inventory_contains(spec, expected) \
            and len(spec['requiredFriendlyUnitTagsForScore']) == len(tags) \
            and profile.get('playsSourceToBaseAsUnit') is True \
            and profile.get('sourceUnitTags') == modes.get(profile.get('mode')) \
            and not any(meaningful(v) for k, v in spec.items() if k not in expected)

    exact = clause['kind'] == 'trigger' and normalize(unparenthesize(clause['scope'])) == sentence \
        and card.get('cardCategory') in {'unit', 'hero_unit'} and isinstance(profiles, list) and len(profiles) == 4 \
        and all(isinstance(p, dict) and type(p.get('mode')) is str for p in profiles) \
        and {p.get('mode') for p in profiles} == set(modes)
    exact = exact and all(exact_profile(p) for p in profiles)
    prefix = 'src/Riftbound.Engine/'

    def members(filename, name):
        code = sources.files.get(prefix + filename, '')
        starts = re.finditer(r'(?m)^    (?:private|internal|public) (?:static )?[^\n{;=]*\b'
                            + re.escape(name) + r'\s*\(', code)
        bodies = []
        for match in starts:
            tail = code[match.end():]
            end = re.search(r'(?m)^    (?:private|internal|public) ', tail)
            bodies.append(code[match.start():match.end()] + (tail[:end.start()] if end else tail))
        return bodies

    checks = [
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureUnitEvents', [
            'IsFaceUpNonStandbyUnit(card)', 'IsObjectOnField(zones, id)',
            'AttachedRuleTextRules.Instances(zones, cards, card)', 'UnitEventTimingMatches(spec.Timing, timing)']),
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureConquestUnitEvents', ['CaptureConquestEventObservations(state, conquerors)']),
        ('CoreRuleEngine.Conquest.cs', 'ResolveConquestTriggerEffects', [
            'CaptureConquestUnitEvents(state with', 'conqueringUnitObjectIds, resolutionTick, playerId, battlefieldId)',
            'events.AddRange(recastTriggers.Select(BuildTriggerQueuedEvent))']),
        ('CoreRuleEngine.UnitEvents.cs', 'UnitEventTrigger', [
            'observation.Controller, observation.Source, UnitEventEffect, eventKind, timing',
            'UnitEvent = captured with { CapturedTick = captured.CapturedTick > 0 ? captured.CapturedTick : tick }']),
        ('CoreRuleEngine.cs', 'BuildTriggerQueuedEvent', [
            'TimingCaptureContext(trigger)', 'payload["timingCaptureHash"] = TimingCaptureHash(timingContext, trigger.TimingAlias)']),
        ('CoreRuleEngine.ConquestUnitEvents.cs', 'CaptureConquestEventObservations', [
            'CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, "CONQUEST")']),
        ('CoreRuleEngine.Hold.cs', 'QueueHeldAbilities', [
            'HeldUnitsAt(state, field, player)', 'CaptureUnitEvents(state.PlayerZones, state.CardObjects, selectedUnits, "HOLD")',
            'UnitEventTrigger(observation,', 'Add(trigger)']),
        ('CoreRuleEngine.BattleResultContinuation.cs', 'CollectResolutionEffects', ['QueueUnitEvents(state, result)']),
        ('CoreRuleEngine.UnitEvents.cs', 'QueueUnitEvents', [
            'UnitEventTrigger(captured, result.State.Tick,', 'queue.Add(trigger)', 'BuildTriggerQueuedEvent(trigger)']),
        ('CoreRuleEngine.UnitEvents.cs', 'ResolveUnitEvent', [
            'ResolveUnitEventInstruction(state, item)', 'ResolutionResult.VisibleObjectIdForViewer(']),
        ('CoreRuleEngine.UnitEvents.cs', 'ResolveUnitEventInstruction', [
            'ValidUnitEvent(state, context,', 'SelectedUnitEventSpec(context)',
            'spec.Effect == FriendlyUnitTraitScoreEffect', 'ResolveFriendlyUnitTraitScore(state, item, spec)']),
        ('CoreRuleEngine.UnitEvents.cs', 'ValidUnitEventTargets', [
            'SelectedUnitEventSpec(traitScore)?.Effect == FriendlyUnitTraitScoreEffect',
            'ValidFriendlyUnitTraitScoreStack(item)']),
        ('CoreRuleEngine.UnitTraitScore.cs', 'IsFriendlyUnitTraitScoreSpec', [
            'Effect: FriendlyUnitTraitScoreEffect, Amount: > 0', 'RequiredFriendlyUnitTagsForScore: { Count: > 0 } tags',
            'tags.Distinct(StringComparer.Ordinal).Count() == tags.Count']),
        ('CoreRuleEngine.UnitTraitScore.cs', 'HasRequiredFriendlyUnitTags', [
            'GetControlledFieldUnitObjectIds(state.PlayerZones, state.CardObjects, controller)',
            'Where(card => IsFaceUpNonStandbyUnit(card))', 'SelectMany(card => card.Tags)', 'required.All(tags.Contains)']),
        ('CoreRuleEngine.UnitTraitScore.cs', 'ResolveFriendlyUnitTraitScore', [
            'ValidFriendlyUnitTraitScoreStack(item)',
            'HasRequiredFriendlyUnitTags(state, item.ControllerId, spec.RequiredFriendlyUnitTagsForScore!)',
            '? ResolveUnitEventPointGain(state, item, spec.Amount)', ': NoopStackResolutionResult(state)']),
        ('CoreRuleEngine.UnitTraitScore.cs', 'ValidFriendlyUnitTraitScoreStack', [
            'item.TargetObjectIds.Count == 0 && item.TargetGenerations is null',
            'item.TriggerCost is null && item.DamageAmount == 0 && item.EffectRepeatCount == 1']),
        ('CoreRuleEngine.UnitTraitScore.cs', 'ResolveUnitEventPointGain', [
            'TryPreventPointGain(state, item.ControllerId, amount,',
            'NormalizeScoresForSeats(state)', 'scores[item.ControllerId] += amount',
            'scores[item.ControllerId] >= EffectiveWinningScore(state)',
            'ResolutionResult.VisibleObjectIdForViewer(']),
        ('CoreRuleEngine.BattlefieldTimingAliases.cs', 'TimingCaptureContext', []),
        ('MatchRecovery.BattlefieldTimingAliases.cs', 'BattlefieldTimingContinuations', [
            'CoreRuleEngine.TimingCaptureContext(trigger)', 'CoreRuleEngine.TimingCaptureContext(item)']),
        ('MatchRecovery.BattlefieldTimingAliases.cs', 'ValidateBattlefieldTimingHistory', [
            'BattlefieldTimingContinuations(state)', '"timingCaptureHash"',
            'CoreRuleEngine.TimingCaptureHash(item.Context, item.Alias)', 'c.Accepted && c.CompletedTick == queued[0].Tick',
            '"TRIGGERS_MOVED_TO_STACK"', 'e.Event.Kind == "TOKEN_CEASED_TO_EXIST"',
            'e.Tick >= ceasedUnit.CapturedTick', 'target == item.Source',
            'c.Accepted && c.CompletedTick == e.Tick']),
        ('MatchRecovery.BattlefieldTimingAliases.cs', 'HasValidatedCeasedBattlefieldUnitEventSource', [
            'state.CardObjects.ContainsKey(source)', 'P6TokenFactoryCatalog.IsTokenFactory(token)',
            'CoreRuleEngine.TimingCaptureContext(stack) is UnitEventContext',
            'CoreRuleEngine.ValidUnitEvent(state, context, stack.EffectKind, source, stack.CardNo, controller)',
            'CoreRuleEngine.ValidUnitEventTargets(stack)']),
        ('MatchRecovery.cs', 'ValidateSpectatorSnapshotStackObjectReferences', [
            'HasValidatedCeasedBattlefieldUnitEventSource(authoritativeState, stackItemId,']),
        ('MatchRecovery.cs', 'ValidateSnapshotTimingTriggerQueuePayloadValues', [
            'HasValidatedCeasedBattlefieldUnitEventSource(authoritativeState, triggerId,']),
        ('MatchRecovery.cs', 'ValidateSpectatorTriggerQueuePayloadValues', [
            'HasValidatedCeasedBattlefieldUnitEventSource(authoritativeState, triggerId,']),
        ('MatchRecovery.cs', 'ValidateFrame', [
            'ValidateBattlefieldTimingHistory(authoritativeState, commands, events, errors)']),
    ]
    exact = exact and all((bodies := members(filename, name))
        and all(all(token in body for token in tokens) for body in bodies) for filename, name, tokens in checks)
    exact = exact and any('UnitEventTimingMatches(unitEvent.Timing, "HOLD")' in body
        and 'UnitEventTimingMatches(unitEvent.Timing, "CONQUEST")' in body and '? unitEvent : null' in body
        for body in members('CoreRuleEngine.BattlefieldTimingAliases.cs', 'TimingCaptureContext'))
    # A precondition at capture would miss a tag supplied by an actual response.
    exact = exact and all('HasRequiredFriendlyUnitTags(' not in body
        for filename, name in [('CoreRuleEngine.UnitEvents.cs', 'CaptureUnitEvents'),
                               ('CoreRuleEngine.ConquestUnitEvents.cs', 'CaptureConquestEventObservations'),
                               ('CoreRuleEngine.Hold.cs', 'QueueHeldAbilities')]
        for body in members(filename, name))
    return result('route' if exact else 'missing',
        '共享征服／坚守按每份文本独立捕获；结算时检查当前公开友方场上单位的四类标签，经得分限制与胜负消费者，并核验原始时机捕获'
        if exact else '缺少每份文本的征服／坚守独立捕获、结算时当前友方四类标签检查或共享得分／原始历史消费者',
        sorted({prefix + filename for filename, _, _ in checks}))


def last_breath_recycle_runes_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    sentence = normalize("{{绝念}}—回收我，以此让你的所有符文变为活跃状态。")
    if normalize(unparenthesize(clause["text"])) != sentence:
        return None
    expected = dict(kind="UNIT_LAST_BREATH_RECYCLE_SELF_READY_ALL_RUNES", timing="UNIT_DESTROYED",
                    targetScope="SOURCE_UNIT", recycleCount=1, recycleSourceZone="GRAVEYARD",
                    recycleDestinationZone="MAIN_DECK", runeReadyCount=None, runeCallCount=None, drawCount=None)
    exact = clause["kind"] == "trigger" and normalize(unparenthesize(clause["scope"])) == sentence \
        and card.get("cardCategory") in {"unit", "hero_unit"} \
        and any(b.get("playsSourceToBaseAsUnit") for b in card.get("behaviors", []))
    exact = exact and any(
        all(entry.get("node", {}).get(k) == v for k, v in expected.items())
        and normalize(unparenthesize(entry["node"].get("text", ""))) == sentence
        and not any(meaningful(v) for k, v in entry["node"].items() if k not in {*expected, "text", "reason"})
        and "UnitDestroyedTriggerSpecRules.IsLastBreathRecycleSelfReadyAllRunesTrigger" in entry.get("predicates", [])
        for entry in card.get("triggers", []))

    def member(path, name, class_marker=None):
        if path in sources.files:
            code = sources.files[path]
        elif path == "src/Riftbound.CardCatalog/RuleTextParsers.cs":
            try:
                code = re.sub(r"/\*.*?\*/|//[^\n]*", "", (sources.root / path).read_text(), flags=re.S)
            except OSError:
                code = ""
        else:
            code = ""
        if class_marker:
            code = code.split(class_marker, 1)[-1]
        start = re.search(r"(?m)^    (?:private|internal|public) static [^\n]*\b" + re.escape(name) + r"\s*\(", code)
        if not start:
            return ""
        rest = code[start.start():]
        end = re.search(r"(?m)^    (?:private|internal|public) static ", rest[start.end() - start.start():])
        return rest[:start.end() - start.start() + end.start()] if end else rest

    engine = "src/Riftbound.Engine/"
    checks = [
        ("CoreRuleEngine.DeathObservers.cs", "CaptureUnitDeath", [
            "RuneCalls = CaptureLastBreathRuneCalls(state, zones, cards, unit, deathId)"]),
        ("CoreRuleEngine.LastBreathRuneCall.cs", "CaptureLastBreathRuneCalls", [
            "UnitDestroyedTriggerSpecRules.LastBreathRuneCallTriggers(instance.CardNo)", "before.ObjectGeneration"]),
        ("UnitDestroyedTriggerSpecRules.cs", "LastBreathRuneCallTriggers", [
            "IsLastBreathRecycleSelfReadyAllRunesTrigger(t)"]),
        ("CoreRuleEngine.FieldTriggers.cs", "NeedsTriggerConfirmation", ["NeedsTriggerCostConfirmation(i)"]),
        ("CoreRuleEngine.FieldTriggers.cs", "PrepareTriggerConfirmation", [
            "state.StackItems.FirstOrDefault(NeedsTriggerConfirmation)",
            "if (NeedsTriggerCostConfirmation(item)) return PrepareTriggerCostConfirmation(result, item)"]),
        ("CoreRuleEngine.TriggerCosts.cs", "PrepareTriggerCostConfirmation", [
            "TriggerCostChoices(state, item).Length < Math.Max(1, cost.DestroyPermanents)",
            "return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item))",
            "PendingCardChoice = cost.Power + cost.Mana == 0 ? TriggerCostChoice(state, item) : null"]),
        ("CoreRuleEngine.TriggerCosts.cs", "LeadingTriggerCost", [
            "HasLastBreathSourceRecycleCost(item) ? new(RecycleSelf: true)"]),
        ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostChoices", ["LastBreathSourceRecycleChoices(state, item)"]),
        ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostChoice", ["RecycleSelf: false", "? OptionalTriggerWindow : TriggerCostWindow"]),
        ("CoreRuleEngine.TriggerCosts.cs", "ValidTriggerCostChoice", ["!HasLastBreathSourceRecycleCost(item)"]),
        ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostSourceGeneration", ["item.LastBreathRuneCall!.Source.Generation + 1"]),
        ("CoreRuleEngine.TriggerCosts.cs", "ResolveTriggerCostChoice", ["PayLastBreathSourceRecycleCost(state, item, selected)"]),
        ("CoreRuleEngine.TriggerRecycleCost.cs", "HasLastBreathSourceRecycleCost", [
            "TryGetLastBreathRuneCallSpec(context, out var spec)", "IsLastBreathRecycleSelfReadyAllRunesTrigger(spec)"]),
        ("CoreRuleEngine.TriggerRecycleCost.cs", "LastBreathSourceRecycleChoices", [
            "ValidLastBreathRuneCallStack(state, item)", "IsTokenObject(source)",
            "source.ObjectGeneration != TriggerCostSourceGeneration(item)", "owner.Graveyard.Contains(source.ObjectId"]),
        ("CoreRuleEngine.TriggerRecycleCost.cs", "PayLastBreathSourceRecycleCost", [
            "selected.Count != 1", "selected.SequenceEqual(LastBreathSourceRecycleChoices(state, item))",
            "RecycleGraveyardCards(state, zones, cards, source.OwnerId!, source.ObjectId,",
            "recyclingPlayerId: item.ControllerId", "SourceWasRecycled = true", "return CompleteTriggerCost("]),
        ("CoreRuleEngine.TriggerCosts.cs", "ValidTriggerCostReceipt", [
            "receipt.Source.Generation != TriggerCostSourceGeneration(item)", "receipt.SourceWasRecycled != cost.RecycleSelf",
            "recycled.ObjectGeneration <= receipt.Source.Generation"]),
        ("CoreRuleEngine.TriggerCosts.cs", "CompleteTriggerCost", [
            "HasLastBreathSourceRecycleCost(item) ? ResolutionResult.VisibleObjectIdForViewer("]),
        ("CoreRuleEngine.cs", "ResolveChooseCardsRuntime", [
            "if (pendingChoice.ChoiceWindow is TriggerCostWindow or OptionalTriggerWindow) return ResolveTriggerCostChoice(state, pendingChoice, submittedObjectIds)"]),
        ("CoreRuleEngine.cs", "ApplyReadyState", [
            "ReadyByEffectPermittedByFieldRules(playerZones, cardObjects, targetState)", "IsExhausted = false",
            "CaptureReadiedObject(playerZones, cardObjects, targetState)", "return nextTargetState"]),
        ("CoreRuleEngine.cs", "RecycleGraveyardCards", [
            "recyclingPlayerId ?? playerId", "MainDeck = zones.MainDeck.Concat(randomizedCardIds).ToArray()",
            ".Where(objectId => !recycledSet.Contains(objectId))", "CaptureRecycleObservers(new GameEvent("]),
        ("CoreRuleEngine.cs", "ResolveStackItemEffectCore", [
            "or TriggerKinds.UnitLastBreathRecycleSelfReadyAllRunes", "return ResolveUnitLastBreathCallRuneStackItem(state, stackItem)"]),
        ("CoreRuleEngine.cs", "ResolveUnitLastBreathCallRuneStackItem", [
            "stackItem.EffectKind == TriggerKinds.UnitLastBreathRecycleSelfReadyAllRunes", "return ResolveLastBreathReadyAllRunes(state, stackItem)"]),
        ("CoreRuleEngine.LastBreathRuneCall.cs", "ResolveLastBreathReadyAllRunes", [
            "item.TriggerCost is not { SourceWasRecycled: true }", "ValidTriggerCostReceipt(state, item)",
            "z.Base.Concat(z.Battlefields)", "CardObjectTags.RuneCard", "!rune.IsFaceDown",
            "EffectiveFieldControllerId(zones, id, rune) == item.ControllerId",
            "ApplyReadyState(cards[id], behavior, visible, id", "ResolutionResult.VisibleObjectIdForViewer("]),
        ("MatchRecovery.cs", "Validate", ["ValidateFrame("]),
        ("MatchRecovery.cs", "ValidateFrame", ["ValidateLastBreathRecycleCostHistory(authoritativeState, commands, events, errors)"]),
        ("MatchRecovery.LastBreathRecycleCost.cs", "ValidateLastBreathRecycleCostHistory", [
            'e.Event.Kind == "CARDS_RECYCLED"', '"sourceGeneration"', '"deathId"', '"cardIds"',
            'e.Event.Kind == "TRIGGER_CONFIRMED"', "command.Accepted", "CommandTypes.ChooseCards",
            "command.CompletedTick == recycled[0].Tick", "command.CompletedEventSequence >= confirmed[0].Sequence",
            "choices[0].Trim() == receipt.Source.ObjectId"]),
    ]
    exact = exact and all(all(token in member(engine + file, name) for token in tokens)
                          for file, name, tokens in checks)
    parser = "src/Riftbound.CardCatalog/RuleTextParsers.cs"
    exact = exact and "TryParseUnitLastBreathRecycleSelfReadyAllRunes(text, out var recycledSourceRunes)" \
        in member(parser, "Parse", "public static class TriggerParser")
    exact = exact and "UnitLastBreathRecycleSelfReadyAllRunes" \
        in sources.files.get(engine + "MatchRecovery.LastBreathRuneCall.cs", "")
    evidence = sorted({engine + file for file, _, _ in checks} | {parser})
    return result("route", "确认时回收同一死亡后的原来源，保留已付历史，响应后活跃技能控制者当前场上全部符文", evidence) if exact else result(
        "missing", "缺少原死亡废牌代际的回收确认费用、已接受付款凭据或全部当前友方场上符文的独立活跃结算", evidence)


def battle_win_and_paid_combat_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause["text"]))
    paid_text = normalize("当我进攻或防守时，你可以选择支付{{红色}}，以此让我在本回合内{{S}}+2。")
    gold_text = normalize("当我赢得战斗时，打出一个休眠的“金币”装备指示物。")
    draw = re.fullmatch(r"每?当我赢得(?:一场)?战斗时抽([一两二三四五六七八九十0-9]+)张牌", text)
    if text not in {paid_text, gold_text} and not draw:
        return None
    exact = clause["kind"] == "trigger" and normalize(unparenthesize(clause["scope"])) == text \
        and card.get("cardCategory") in {"unit", "hero_unit"} \
        and any(b.get("playsSourceToBaseAsUnit") for b in card.get("behaviors", []))
    engine = "src/Riftbound.Engine/"

    def member(path, name, class_marker=None):
        code = sources.files.get(path, "")
        if not code and path == "src/Riftbound.CardCatalog/RuleTextParsers.cs":
            try:
                code = re.sub(r"/\*.*?\*/|//[^\n]*", "", (sources.root / path).read_text(), flags=re.S)
            except OSError:
                pass
        if class_marker:
            if class_marker not in code:
                return ""
            code = code.split(class_marker, 1)[1]
        start = re.search(r"(?m)^    (?:private|internal|public) static [^\n]*\b" + re.escape(name) + r"\s*\(", code)
        if not start:
            return ""
        rest = code[start.start():]
        end = re.search(r"(?m)^    (?:private|internal|public) static ", rest[start.end() - start.start():])
        return rest[:start.end() - start.start() + end.start()] if end else rest

    checks = []
    if text == paid_text:
        expected = dict(timing="ATTACK_OR_DEFEND", effect="MIGHT_SELF", amount=2,
                        leadingPowerCost=1, leadingPowerTrait="red", optional=True)
        event = card.get("unitEventAbility") or {}
        exact = exact and all(event.get(k) == v for k, v in expected.items()) \
            and not any(meaningful(v) for k, v in event.items() if k not in expected)
        checks = [
            ("CoreRuleEngine.UnitEvents.cs", "UnitEventSpec", ["card.UnitEventAbility"]),
            ("CoreRuleEngine.UnitEvents.cs", "CaptureUnitEvents", [
                "AttachedRuleTextRules.Instances(zones, cards, card)", "UnitEventTimingMatches(spec.Timing, timing)"]),
            ("CoreRuleEngine.UnitEvents.cs", "UnitEventTimingMatches", [
                'declared?.Split("_OR_", StringSplitOptions.None).Contains(timing, StringComparer.Ordinal)']),
            ("CoreRuleEngine.BattleStartRoles.cs", "RecordBattleDuelRoles", ["CaptureAttackUnitEvents(", "CaptureRoleUnitEvents("]),
            ("CoreRuleEngine.UnitEvents.cs", "CaptureAttackUnitEvents", [
                'CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK")']),
            ("CoreRuleEngine.UnitEvents.cs", "CaptureRoleUnitEvents", [
                "CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, role)"]),
            ("CoreRuleEngine.FieldTriggers.cs", "NeedsTriggerConfirmation", ["NeedsTriggerCostConfirmation(i)"]),
            ("CoreRuleEngine.FieldTriggers.cs", "PrepareTriggerConfirmation", [
                "state.StackItems.FirstOrDefault(NeedsTriggerConfirmation)",
                "if (NeedsTriggerCostConfirmation(item)) return PrepareTriggerCostConfirmation(result, item)"]),
            ("CoreRuleEngine.TriggerCosts.cs", "LeadingTriggerCost", [
                "SelectedUnitEventSpec(unitEvent)", "Power: eventSpec.LeadingPowerCost", "PowerTrait: eventSpec.LeadingPowerTrait"]),
            ("CoreRuleEngine.TriggerCosts.cs", "PrepareTriggerCostConfirmation", [
                "PendingPayment = cost.Power + cost.Mana > 0 ? TriggerCostPayment(item) : null"]),
            ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostPayment", [
                "TriggerCostWindow, item.ControllerId", "powerCost: LeadingTriggerCost(item)!.Power",
                "powerCostByTrait: LeadingTriggerCost(item)!.PowerTrait", 'legalPaymentChoiceIds: ["PAY", "DECLINE"]']),
            ("CoreRuleEngine.cs", "ResolvePendingPayCost", [
                "intent.PlayerId, pendingPayment.PlayerId", "pendingPayment.PaymentWindow == TriggerCostWindow",
                "return ResolveTriggerCostPayment(state, intent, pendingPayment, submittedChoices, paymentChoiceIds.Count)"]),
            ("CoreRuleEngine.TriggerCosts.cs", "ResolveTriggerCostPayment", [
                'choices[0] == "DECLINE"', "CompleteTriggerCost(state, item, null, [])",
                "powerCostByTrait: pending.PowerCostByTrait", "PaymentCostRules.TryCommitPayment(",
                "RunePools = committed.RunePools", "Power: pending.PowerCost", "return CompleteTriggerCost(paid, item"]),
            ("CoreRuleEngine.TriggerCosts.cs", "CompleteTriggerCost", [
                "item with { TriggerCost = receipt }", '"TRIGGER_CONFIRMED"']),
            ("CoreRuleEngine.TriggerCosts.cs", "ValidTriggerCostReceipt", [
                "receipt.Source.Generation != TriggerCostSourceGeneration(item)", "receipt.Power == cost.Power"]),
            ("CoreRuleEngine.UnitEvents.cs", "ResolveUnitEvent", ["ResolveUnitEventInstruction(state, item)"]),
            ("CoreRuleEngine.UnitEvents.cs", "ResolveUnitEventInstruction", [
                'spec.Effect == "MIGHT_SELF"', "source.ObjectGeneration == context.SourceGeneration",
                "IsObjectOnField(zones, source.ObjectId)", "ApplyDirectUntilEndPowerModifier(",
                "spec.Amount, source.Power + spec.Amount"]),
            ("CoreRuleEngine.cs", "ApplyDirectUntilEndPowerModifier", [
                '"UNTIL_END_OF_TURN"', "Power = resultingPower", "UntilEndOfTurnPowerModifiers = powerModifierLedger"]),
            ("CoreRuleEngine.cs", "ApplyTurnEndCleanup", [
                "Power = objectState.Power - objectState.UntilEndOfTurnPowerModifier", "UntilEndOfTurnPowerModifiers = []"]),
        ]
        description = "进攻／防守时按原来源确认可选红色费用，响应后给予同代场上来源本回合+2"
    else:
        gold = text == gold_text
        digits = dict(zip("一二三四五六七八九十", range(1, 11)), 两=2)
        count = 1 if gold else int(draw[1]) if draw[1].isdigit() else digits.get(draw[1])
        exact = exact and count is not None and count > 0
        expected = dict(kind="UNIT_BATTLE_WIN_CREATE_DORMANT_GOLD" if gold else "UNIT_BATTLE_WIN_DRAW",
                        timing="UNIT_BATTLE_WON", targetScope="SOURCE_UNIT")
        expected.update(dict(createdTokenCount=1, createdTokenName="金币", createdTokenDestination="BASE", createdTokenExhausted=True)
                        if gold else dict(drawCount=count))
        predicate = "UnitBattleWinTriggerSpecRules." + ("IsUnitBattleWinDormantGoldTrigger" if gold else "IsUnitBattleWinDrawTrigger")
        exact = exact and any(all(t.get("node", {}).get(k) == v for k, v in expected.items())
            and normalize(unparenthesize(t["node"].get("text", ""))) == text and predicate in t.get("predicates", [])
            and not any(meaningful(v) for k, v in t["node"].items() if k not in {*expected, "text", "reason"})
            for t in card.get("triggers", []))
        parser = member("src/Riftbound.CardCatalog/RuleTextParsers.cs", "Parse", "public static class TriggerParser")
        exact = exact and ("TriggerKinds.UnitBattleWinCreateDormantGold" if gold else "TriggerKinds.UnitBattleWinDraw") in parser
        checks = [
            ("CoreRuleEngine.BattleResultContinuation.cs", "DetermineBattleResultAfterCleanup", [
                "CaptureUnitBattleWinDrawObservations(resultState, battlefield, winner", "attackers, defenders, resultState.Tick)"]),
            ("CoreRuleEngine.BattleWinTriggers.cs", "CaptureUnitBattleWinDrawObservations", [
                "card.ControllerId != winnerPlayerId", "IsVisibleFieldUnitObject(state.CardObjects, id)",
                'location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId != battlefieldId',
                "AttachedRuleTextRules.Instances(state, card)", "UnitBattleWinTriggerSpecRules.Rewards(instance.CardNo)",
                "new(id, card.ObjectGeneration)", "Origin: instance.Origin", "TriggerIndex = index"]),
            ("CoreRuleEngine.BattleWinTriggers.cs", "HasValidUnitBattleWinDrawShape", [
                "UnitBattleWinTriggerSpecRules.TryGet(context.CardNo, context.TriggerIndex, out var spec)",
                "context.DrawCount == spec.DrawCount.GetValueOrDefault()", "context.GoldCount == spec.CreatedTokenCount.GetValueOrDefault()"]),
            ("CoreRuleEngine.cs", "ResolveStackItemEffectCore", [
                "if (stackItem.UnitBattleWinDraw is { } unitBattleWin) return ResolveUnitBattleWinDraw(state, stackItem, unitBattleWin)"]),
            ("CoreRuleEngine.BattleWinTriggers.cs", "ResolveUnitBattleWinDraw", [
                "HasValidUnitBattleWinDrawShape(context)", "ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator)"]),
            ("BattleResultRecoveryRules.cs", "ValidSkill", [
                "CoreRuleEngine.HasValidUnitBattleWinDrawShape(unit)", "AttachedRuleTextRules.ValidCaptured(state, unit.Origin",
                "c.SurvivingAttackerObjectIds : c.SurvivingDefenderObjectIds).Contains(source)"]),
            ("BattleResultRecoveryRules.cs", "ValidateOriginalTriggers", [
                'e.Event.Kind == "TRIGGER_QUEUED"', '"battleWinContext"', "HasOriginalCommand(origins.SingleOrDefault())"]),
            ("MatchRecovery.cs", "ValidateRecoveryFrameAsync", ["BattleResultRecoveryRules.ValidateOriginalTriggers(recovery)"]),
            ("MatchRecovery.cs", "ValidateSpectatorSnapshotStackObjectReferences", [
                "BattleResultRecoveryRules.IsCapturedSkill(authoritativeState, drawSkill)",
                "!CapturedBattleSourceIsPrivate(authoritativeState, drawSkill)",
                "sourceReferences = knownObjectIds.Append(drawSkill.SourceObjectId)"]),
        ]
        if gold:
            checks += [
                ("UnitBattleWinTriggerSpecRules.cs", "IsUnitBattleWinDormantGoldTrigger", [
                    "spec.Kind == TriggerKinds.UnitBattleWinCreateDormantGold", "spec.Timing == TriggerTimings.UnitBattleWon",
                    "spec.CreatedTokenDestination == TriggerZones.Base", "spec.CreatedTokenExhausted == true"]),
                ("CoreRuleEngine.BattleWinTriggers.cs", "ResolveUnitBattleWinDraw", [
                    "if (context.GoldCount > 0)", "i < context.GoldCount", "CreateLegendEquipmentToken(zones, cards, context.ControllerId",
                    "spec.CreatedTokenExhausted == true", "publicSourceObjectId: visibleSource"]),
                ("CoreRuleEngine.cs", "CreateLegendEquipmentToken", [
                    "tokenDefinition.CreateObject(", "ApplyTokenEntryStaticAbility(",
                    "Base = zones.Base.Concat([tokenObjectId]).ToArray()", "CaptureEquipmentPlayed(",
                    '["sourceObjectId"] = publicSourceObjectId ?? sourceObjectId']),
            ]
        else:
            checks += [
                ("UnitBattleWinTriggerSpecRules.cs", "IsUnitBattleWinDrawTrigger", [
                    "spec.Kind == TriggerKinds.UnitBattleWinDraw", "spec.DrawCount is > 0"]),
                ("CoreRuleEngine.BattleWinTriggers.cs", "ResolveUnitBattleWinDraw", [
                    "ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, context.ControllerId,",
                    "context.DrawCount, state.RngCursor, events, deferNormalVictory: true)"]),
                ("CoreRuleEngine.cs", "ApplyDrawToPlayer", [
                    "MainDeck = drawResult.MainDeck", "Hand = drawPlayerZones.Hand.Concat(drawResult.DrawnCards).ToArray()",
                    "playerZones[playerId] = drawPlayerZones", "BuildCardDrawEvents("]),
            ]
        description = "实际获胜且存活的参战单位逐份捕获奖励，保留原结果和技能出生；金币／抽牌在控制和征服前独立结算"
    exact = exact and all(all(token in member(engine + file, name) for token in tokens)
                          for file, name, tokens in checks)
    evidence = sorted({engine + file for file, _, _ in checks})
    if text != paid_text:
        evidence.append("src/Riftbound.CardCatalog/RuleTextParsers.cs")
    return result("route" if exact else "missing", description if exact else
                  "缺少与本句数值、费用和来源绑定完全匹配的事件规格、真实确认／结算或原始结果校验", evidence)


def held_next_unit_ready_boon_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    sentence = normalize('当我据守一处战场时，则你本回合下一次打出一名单位时，让其变为活跃状态，并给予其{{增益}}。')
    if normalize(unparenthesize(clause['text'])) != sentence:
        return None
    profiles = card.get('behaviors', [])
    expected = dict(timing='HOLD', effect='CREATE_TURN_UNIT_PLAY_GRANT')
    exact = clause['kind'] == 'trigger' and normalize(unparenthesize(clause['scope'])) == sentence \
        and card.get('cardCategory') in {'unit', 'hero_unit'} and isinstance(profiles, list) and len(profiles) == 1 \
        and isinstance(profiles[0], dict)
    if exact:
        profile = profiles[0]; spec = profile.get('unitEventAbility')
        exact = profile.get('playsSourceToBaseAsUnit') is True \
            and profile.get('turnUnitPlayGrantKind') == 'PLAY_READY_BOON' \
            and profile.get('turnUnitPlayGrantNextOnly') is True \
            and type(profile.get('turnUnitPlayGrantCount', 0)) is int and profile.get('turnUnitPlayGrantCount', 0) == 0 \
            and not meaningful(profile.get('turnUnitPlayGrantAnyBattlefield')) \
            and isinstance(spec, dict) and inventory_contains(spec, expected) \
            and type(spec.get('amount', 0)) is int and spec.get('amount', 0) == 0 \
            and not any(meaningful(value) for key, value in spec.items() if key not in expected)
    prefix = 'src/Riftbound.Engine/'
    def member(filename, name):
        code = sources.files.get(prefix + filename, '')
        starts = list(re.finditer(r'(?m)^    (?:private|internal|public) (?:static )?[^()\n{}=]*\b'
            + re.escape(name) + r'\s*\(', code))
        if not starts:
            return ''
        start = starts[-1]; rest = code[start.start():]; offset = start.end() - start.start()
        end = re.search(r'(?m)^    (?:private|internal|public) ', rest[offset:])
        return rest[:offset + end.start()] if end else rest
    checks = [
        ('CoreRuleEngine.Hold.cs', 'QueueHeldAbilities', [
            'HeldUnitsAt(state, field, player)', 'CaptureUnitEvents(state.PlayerZones, state.CardObjects, selectedUnits, "HOLD")',
            'UnitEventTrigger(observation,', 'Add(trigger)']),
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureUnitEvents', [
            'IsObjectOnField(zones, id)', 'AttachedRuleTextRules.Instances(zones, cards, card)',
            'UnitEventTimingMatches(spec.Timing, timing)']),
        ('CoreRuleEngine.UnitEvents.cs', 'ValidUnitEventTargets', [
            'SelectedUnitEventSpec(grant)?.Effect == UnitEventTurnPlayGrantEffect', 'ValidUnitEventTurnPlayGrantStack(item)']),
        ('CoreRuleEngine.UnitEvents.cs', 'ResolveUnitEventInstruction', [
            'if (spec.Effect == UnitEventTurnPlayGrantEffect) return ResolveUnitEventTurnPlayGrant(state, item)']),
        ('CoreRuleEngine.UnitEventTurnPlayGrants.cs', 'ResolveUnitEventTurnPlayGrant', [
            'ValidUnitEventTurnPlayGrantStack(item)', 'new UnitEventTurnPlayGrantOrigin(context, item.TimingAlias)',
            'item.SourceObjectId, context.SourceGeneration', 'context.CardNo, item.StackItemId, 0, behavior.TurnUnitPlayGrantKind, origin',
            'AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, marker)']),
        ('CoreRuleEngine.UnitEventTurnPlayGrants.cs', 'ValidUnitEventTurnPlayGrant', [
            'context.SourceGeneration == grant.Source.Generation', 'grant.Kind == UnitEventReadyBoonGrantKind',
            'b.TurnUnitPlayGrantNextOnly && grant.ExecutionIndex == 0',
            'ValidUnitEvent(state, context, UnitEventEffect, grant.Source.ObjectId, grant.CardNo, grant.ControllerId)',
            'ValidTimingAlias(state, origin.TimingAlias, grant.Source.ObjectId, grant.ControllerId, context)']),
        ('CoreRuleEngine.TurnUnitPlayGrants.cs', 'AddTurnUnitPlayGrants', [
            'b.UnitEventAbility?.Effect == UnitEventTurnPlayGrantEffect']),
        ('CoreRuleEngine.TurnUnitPlayGrants.cs', 'ReadTurnUnitPlayGrant', [
            'ReadUnitEventTurnPlayGrantOrigin(f[9])', 'UnitEventOrigin = origin']),
        ('CoreRuleEngine.TurnUnitPlayGrants.cs', 'ValidTurnUnitPlayGrant', [
            'MatchStateHasher.HashValue(parsed) != MatchStateHasher.HashValue(grant)',
            'grant.TurnNumber != state.TurnNumber',
            'if (grant.UnitEventOrigin is not null) return ValidUnitEventTurnPlayGrant(state, grant, b)']),
        ('CoreRuleEngine.TurnUnitPlayGrants.cs', 'CaptureTurnUnitPlayGrants', [
            '"UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED"',
            'ActiveUnitPlayGrants(effects, controller, UnitEventReadyBoonGrantKind)',
            'ConsumedUnitPlayGrants.Contains(grant.Marker)', 'ConsumedUnitPlayGrants.Add(context.Grant.Marker)']),
        ('CoreRuleEngine.TurnUnitPlayGrants.cs', 'QueueTurnUnitPlayBoons', [
            'effects.Add(TurnUnitPlayGrantUseMarker(', 'Generation = Math.Max(context.Unit.Generation, old.ObjectGeneration + 1)',
            'context.Grant.Kind is "PLAY_BOON" or UnitEventReadyBoonGrantKind ? context : null']),
        ('CoreRuleEngine.TurnUnitPlayGrants.cs', 'ResolveTurnUnitPlayBoon', [
            'unit.ObjectGeneration != c.Unit.Generation', 'IsFieldUnitObject(state, unit.ObjectId)',
            'if (c.Grant.Kind == UnitEventReadyBoonGrantKind)', 'ApplyReadyState(', 'ApplyBoon(']),
        ('CoreRuleEngine.BattleResultContinuation.cs', 'CollectResolutionEffects', [
            'QueueTurnUnitPlayBoons(state, collected)', 'DeferCompanionPermanentPlayOrdering(state, collected)']),
        ('CoreRuleEngine.UnitPlayMight.cs', 'CompanionPlayedUnit', ['trigger.TurnUnitPlayBoon?.Unit']),
        ('CoreRuleEngine.UnitPlayMight.cs', 'DeferCompanionPermanentPlayOrdering', [
            'CompanionPlayedUnit(t) is not null',
            'queue.Any(t => t.PermanentPlayStackItemId == item.StackItemId)',
            'PermanentPlayStackItemId = item.StackItemId']),
        ('CoreRuleEngine.UnitPlayMight.cs', 'ValidPermanentPlayCompanion', [
            'if (enqueuedAtTick is not { } capturedTick) return false',
            'state.CardPlays.Resolved.Any(play => play.PlayId == item.StackItemId && play.Source == unit',
            'play.CardNo == item.CardNo && play.PlayerId == item.ControllerId && play.Tick == capturedTick',
            'ValidTurnUnitPlayBoon(state, boon']),
        ('PermanentPlayTriggerRecoveryRules.cs', 'ValidQueued', [
            'CoreRuleEngine.ValidPermanentPlayCompanion(state, companion, item, trigger.EnqueuedAtTick)']),
        ('CoreRuleEngine.cs', 'ApplyReadyState', [
            'CardStaticAbilitySpecRules.CardCannotBecomeActive', 'ReadyByEffectPermittedByFieldRules(',
            'CaptureReadiedObject(', 'CaptureSelfReady(']),
        ('CoreRuleEngine.cs', 'ApplyBoon', ['TryGrantBoonForInstruction(', '"BOON_GRANTED"']),
        ('CoreRuleEngine.UnitEventTurnPlayGrants.cs', 'RecordUnitEventTurnPlayHistory', [
            '"UNIT_EVENT_TURN_PLAY_GRANT_CREATED"', '"UNIT_EVENT_TURN_PLAY_GRANT_USED"',
            '"UNIT_EVENT_TURN_PLAY_SKILL_CAPTURED"', '"UNIT_EVENT_TURN_PLAY_SKILL_RETIRED"',
            'UnitEventTurnPlaySkills(before)', 'UnitEventTurnPlaySkills(result.State)']),
        ('CoreRuleEngine.cs', 'ResolveCoreAsync', ['RecordUnitEventTurnPlayHistory(state,']),
        ('MatchRecovery.UnitEventTurnPlayGrants.cs', 'ValidateUnitEventTurnPlayHistory', [
            'CurrentGameAcceptedEventBoundary(state, commands, events)', 'state.PendingRuleChoice?.Origin',
            'command.Accepted', 'expectedGrants.SetEquals(', 'expectedUses.SetEquals(', 'expectedSkills.SetEquals(',
            'CoreRuleEngine.UnitEventTurnPlaySkillUse(skill.Context)', 'CoreRuleEngine.TimingCaptureHash(context, origin.TimingAlias)',
            '"TRIGGERS_MOVED_TO_STACK"', '"TOKEN_CEASED_TO_EXIST"']),
        ('MatchRecovery.UnitEventTurnPlayGrants.cs', 'HasValidatedCeasedUnitEventTurnPlaySource', [
            'UnitEventOrigin?.Context?.TokenFactoryCardNo', 'P6TokenFactoryCatalog.IsTokenFactory(factory)',
            'CoreRuleEngine.ValidTurnUnitPlayBoon(state, skill.Context']),
        ('MatchRecovery.cs', 'ValidateFrame', ['ValidateUnitEventTurnPlayHistory(authoritativeState, commands, events, errors)']),
        ('MatchRecovery.cs', 'ValidateAuthoritativeStateStackItemObjectReferences', ['HasValidatedCeasedUnitEventTurnPlaySource(']),
        ('MatchRecovery.cs', 'ValidateSpectatorSnapshotStackObjectReferences', ['HasValidatedCeasedUnitEventTurnPlaySource(']),
        ('MatchRecovery.cs', 'ValidateSnapshotTimingTriggerQueuePayloadValues', ['HasValidatedCeasedUnitEventTurnPlaySource(']),
        ('MatchRecovery.cs', 'ValidateSpectatorTriggerQueuePayloadValues', ['HasValidatedCeasedUnitEventTurnPlaySource(']),
        ('MatchSession/Projection.Timing.cs', 'PublicTurnUnitPlayBoonContext', [
            '"TURN_UNIT_PLAY_GRANT_HASH:" + MatchStateHasher.HashValue(context.Grant)', 'UnitEventOrigin = null']),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens)
        for filename, name, tokens in checks)
    collection = member('CoreRuleEngine.BattleResultContinuation.cs', 'CollectResolutionEffects')
    ready_grants = 'QueueTurnUnitPlayBoons(state, collected)'
    legacy_ordering = 'DeferCompanionPermanentPlayOrdering(state, collected)'
    exact = exact and ready_grants in collection and legacy_ordering in collection \
        and collection.index(ready_grants) < collection.index(legacy_ordering)
    factory = member('CoreRuleEngine.UnitEventTurnPlayGrants.cs', 'ResolveUnitEventTurnPlayGrant')
    exact = exact and all(token not in factory for token in ['state.CardObjects[item.SourceObjectId]',
        'IsObjectOnField(', 'CombatMightSourceLocation('])
    resolution = member('CoreRuleEngine.TurnUnitPlayGrants.cs', 'ResolveTurnUnitPlayBoon')
    exact = exact and 'ApplyReadyState(' in resolution and 'ApplyBoon(' in resolution \
        and resolution.index('ApplyReadyState(') < resolution.index('ApplyBoon(')
    return result('route' if exact else 'missing',
        '据守独立技能创建本回合下一次单位打出的延迟触发；新单位正常进场后另行活跃并增益；支持原代际来源离场、单次消费及原始历史恢复'
        if exact else '缺少据守来源的延迟授权、下一次真实单位打出捕获、独立活跃／增益结算或原始创建／消费／结束记录消费者',
        sorted({prefix + filename for filename, _, _ in checks}))


def source_play_condition_movement_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    wanted = normalize('当你在你的回合中将我从正面朝下的状态打出时，你可以选择移动任意一名位于不同位置的敌方单位到我所在的战场。')
    if normalize(unparenthesize(clause['text'])) != wanted:
        return None
    profiles = card.get('behaviors', [])
    exact = clause['kind'] == 'trigger' and normalize(unparenthesize(clause['scope'])) == wanted \
        and wanted in normalize(unparenthesize(card.get('cardEffect', ''))) \
        and card.get('cardCategory') in {'unit', 'hero_unit'} and len(profiles) == 1
    if exact:
        profile = profiles[0]
        event = card.get('unitEventAbility')
        expected = dict(timing='PLAY', effect='MOVE_ENEMY_HERE', optional=True,
            requiresSourcePlayedFromStandby=True, requiresSourcePlayedOnOwnTurn=True)
        exact = profile.get('playsSourceToBaseAsUnit') is True \
            and not meaningful(profile.get('playsSourceToBaseAsEquipment')) \
            and isinstance(event, dict) and inventory_contains(event, expected) \
            and inventory_contains(profile.get('unitEventAbility'), event) \
            and inventory_contains(event, profile.get('unitEventAbility')) \
            and all(type(profile.get(key, 0)) is int and profile.get(key, 0) == 0
                for key in ['requiredTargetCount', 'damageAmount']) \
            and type(event.get('amount', 0)) is int and event.get('amount', 0) == 0 \
            and not any(meaningful(value) for key, value in event.items() if key not in expected)
    prefix = 'src/Riftbound.Engine/'
    def member(filename, name):
        code = sources.files.get(prefix + filename, '')
        start = re.search(r'(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b'
            + re.escape(name) + r'\s*\(', code)
        if not start:
            return ''
        rest = code[start.start():]
        offset = start.end() - start.start()
        end = re.search(r'(?m)^    (?:private|internal|public) ', rest[offset:])
        return rest[:offset + end.start()] if end else rest
    checks = [
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureOtherUnitPlayed', [
            'CaptureSourcePlayConditions(entered, player, observations)']),
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureEquipmentPlayed', [
            'CaptureSourcePlayConditions(entered, player,']),
        ('CoreRuleEngine.SourcePlayConditions.cs', 'HasSourcePlayCondition', [
            'spec.RequiresSourcePlayedFromStandby', 'spec.RequiresSourcePlayedOnOwnTurn']),
        ('CoreRuleEngine.SourcePlayConditions.cs', 'CaptureSourcePlayConditions', [
            'o.Source == entered.ObjectId', 'o.Controller == player', 'entered.LastStandbyPlay',
            'RequiresSourcePlayedFromStandby', 'RequiresSourcePlayedOnOwnTurn',
            'frame.TurnPlayerId == o.Controller', 'SourcePlayCondition = new(']),
        ('CoreRuleEngine.UnitEvents.cs', 'ValidUnitEvent', [
            'ValidSourcePlayCondition(state, c, spec, source)']),
        ('CoreRuleEngine.SourcePlayConditions.cs', 'ValidSourcePlayCondition', [
            'context.SourcePlayCondition', 'condition.PlayingPlayerId != condition.TurnPlayerId',
            'condition.FromStandby', 'StandbyRules.ValidUnitEventPlayedOrigin(']),
        ('StandbyRules.cs', 'ValidUnitEventPlayedOrigin', [
            'ValidFields(state, origin)', 'origin.ObjectId == source',
            'generation == origin.SourceGeneration + 1', 'playedTurn > origin.TurnNumber',
            'origin.ControllerId == player']),
        ('CoreRuleEngine.UnitEvents.cs', 'NeedsUnitEventTarget', ['"MOVE_ENEMY_HERE"']),
        ('CoreRuleEngine.UnitEvents.cs', 'UnitEventChoice', [
            '.Optional ? 0 : 1', '.AnyNumberOfTargets ? legal.Length : 1']),
        ('CoreRuleEngine.UnitEvents.cs', 'UnitEventTargets', [
            '"MOVE_ENEMY_HERE"', 'EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId',
            'IsUnitMovementDestination(state, c.ObjectId, destination)', 'MovementRestrictionRules.CanMove(c, item.ControllerId)']),
        ('CoreRuleEngine.PermanentUnitEvents.cs', 'UnitEventHereDestination', [
            'CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration)', '"BATTLEFIELD"']),
        ('CoreRuleEngine.PermanentUnitEvents.cs', 'ResolvePermanentUnitEventInstruction', [
            'StackObjectRules.ReferencesCurrentObject(state, item, target)',
            'spec.Effect == "MOVE_ENEMY_HERE" ? UnitEventHereDestination(state, item)',
            'MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, [target], destination, events)']),
        ('CoreRuleEngine.ObservedUnitEvents.cs', 'RequiresObservedUnitEventHistory', ['HasSourcePlayCondition(']),
        ('CoreRuleEngine.ObservedUnitEvents.cs', 'ObservedUnitEventCaptureHash', ['context.SourcePlayCondition']),
        ('MatchRecovery.ObservedUnitEvents.cs', 'ValidateObservedUnitEventHistory', [
            'CoreRuleEngine.RequiresObservedUnitEventHistory(', 'Scope(state.PendingRuleChoice?.Origin)',
            'command.Accepted', 'command.StartedEventSequence < entry.Sequence',
            'command.CompletedEventSequence >= entry.Sequence',
            'CoreRuleEngine.ObservedUnitEventCaptureHash(item.Context, item.Source, item.Controller)',
            '!expected.SetEquals(current.Select(item => item.Id))']),
        ('MatchRecovery.cs', 'ValidateFrame', [
            'ValidateObservedUnitEventHistory(authoritativeState, commands, events, errors)']),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens)
        for filename, name, tokens in checks)
    evidence = sorted({prefix + filename for filename, _, _ in checks})
    return result('route', '从背面且在己方回合的源牌打出事实独立捕获；确认单个不同位置敌方单位，按原来源当前战场执行共享移动，并核对原始接受历史', evidence) if exact else result(
        'missing', '缺少背面／己方回合的原始打出条件、独立敌方单位移动，或来源代际及原始捕获校验')


def attack_armament_play_attachment_route(card: dict, clause: dict, sources: Sources) -> dict | None:
    text = normalize(unparenthesize(clause['text']))
    expected_text = normalize('当我进攻时，你可以选择打出一件法力费用不高于{{2}}的武装，无视其费用，然后将其贴附到我身上。')
    if text != expected_text:
        return None
    expected = dict(timing='ATTACK', effect='PLAY_HAND_ARMAMENT_THEN_ATTACH_SOURCE', amount=2, optional=True)
    spec = card.get('unitEventAbility') or {}
    exact = clause['kind'] == 'trigger' and normalize(unparenthesize(clause['scope'])) == text \
        and card.get('cardCategory') in {'unit', 'hero_unit'} \
        and any(b.get('playsSourceToBaseAsUnit') is True for b in card.get('behaviors', [])) \
        and all(spec.get(k) == v for k, v in expected.items()) and type(spec.get('amount')) is int \
        and not any(meaningful(v) for k, v in spec.items() if k not in expected)
    prefix = 'src/Riftbound.Engine/'

    def member(filename, name):
        code = sources.files.get(prefix + filename, '')
        start = re.search(r'(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b' + re.escape(name) + r'\s*\(', code)
        if not start:
            return ''
        rest = code[start.start():]
        end = re.search(r'(?m)^    (?:private|internal|public) (?:static )?', rest[start.end() - start.start():])
        return rest[:start.end() - start.start() + end.start()] if end else rest

    checks = [
        ('CoreRuleEngine.UnitEvents.cs', 'CaptureAttackUnitEvents', ['CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK")']),
        ('CoreRuleEngine.EffectPlay.cs', 'TryGetEffectPlayDefinition', ['TryGetArmamentPlayAttachmentDefinition(parent, out definition)']),
        ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'TryGetArmamentPlayAttachmentDefinition', [
            'IsArmamentPlayAttachmentPermission(parent)', 'EffectPlaySourceZone: "HAND"',
            'EffectPlayMaximumManaCost: UnitEventSpec(parent.CardNo)!.Amount',
            'EffectPlayIgnoreBaseMana: true', 'EffectPlayIgnoreBasePower: true',
            'EffectPlayOptional: false', 'EffectPlayRequiresArmament = true']),
        ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'ArmamentEffectPlaySourceAllowed', [
            '!card.IsFaceDown', '!IsTokenObject(card)', 'OfficialArmamentCatalog.Find(card.CardNo)',
            'gear.PlaysSourceToBaseAsEquipment', 'EffectPlayCandidateCostAllowed(card, definition)']),
        ('CoreRuleEngine.EffectPlay.cs', 'EffectPlaySourceMatches', [
            '!definition.EffectPlayRequiresArmament || ArmamentEffectPlaySourceAllowed(card, definition)']),
        ('CoreRuleEngine.UnitEvents.cs', 'ResolveUnitEventInstruction', [
            'spec.Effect is StandbyHandPlayHereEffect or PlayHandArmamentThenAttachEffect',
            'return BeginEffectPlay(state, item, definition)',
            'if (spec.Effect == AttachPlayedArmamentEffect) return ResolvePlayedArmamentAttachment(state, item)']),
        ('CoreRuleEngine.EffectPlay.cs', 'ResolveEffectPlayCommand', [
            'ResolvePlayCard(playState, intent, play)', 'if (!result.Accepted)',
            'CapturePlayedArmamentAttachment(result, pending, play)']),
        ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'CapturePlayedArmamentAttachment', [
            'IsArmamentPlayAttachmentPermission(pending.Parent)', 'EQUIPMENT_PLAYED_TO_BASE',
            'EQUIPMENT_PLAYED_TO_BATTLEFIELD', 'ReflexiveTick = result.State.Tick',
            'new(gear.ObjectId, gear.ObjectGeneration)', 'WithUnitEvents(', 'PlayedArmamentAttachmentCreatedEvent']),
        ('CoreRuleEngine.UnitEventModes.cs', 'SelectedUnitEventSpec', [
            'HasArmamentPlayAttachment(context) && context.ReflexiveTick is not null', 'Effect = AttachPlayedArmamentEffect']),
        ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'ResolvePlayedArmamentAttachment', [
            'currentGear.ObjectGeneration != gear.Binding.Generation',
            'host.ObjectGeneration != context.SourceGeneration', 'TryAttachEquipment(']),
        ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'ValidArmamentPlayAttachmentContext', [
            'context.ReflexiveTick <= context.CapturedTick', 'PredictBindingMatches(',
            'state.CardPlays?.Resolved.Any(', 'play.Source == gear.Binding', 'play.Tick == context.ReflexiveTick']),
        ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'ValidArmamentPlayAttachmentPermission', [
            '!ValidUnitEvent(', '!ValidUnitEventTargets(parent)', 'pending.IgnoreAllCosts',
            'pending.DestinationPolicy != "ANY"', 'pending.SourceZone != "HAND"',
            'parent.TriggerCost is null', 'ValidTriggerCostReceipt(state, parent)', 'expected.Count == pending.Sources.Count', 'pending.Sources.GetValueOrDefault(pair.Key, -1) == pair.Value']),
        ('CoreRuleEngine.TriggerCosts.cs', 'LeadingTriggerCost', ['IsOptionalArmamentPlayEvent(optional)']),
        ('CoreRuleEngine.TriggerCosts.cs', 'TriggerCostChoices', ['IsOptionalArmamentPlayEvent(optionalPlay)']),
        ('MatchRecovery.ArmamentPlayAttachment.cs', 'ValidateArmamentPlayAttachmentConfirmationHistory', [
            'TRIGGER_CONFIRMED', 'item.TriggerCost is null', 'command.Accepted', 'command.CompletedEventSequence >= original[0].Sequence']),
        ('MatchRecovery.ObservedUnitEvents.cs', 'ValidateObservedUnitEventHistory', ['ValidateArmamentPlayAttachmentConfirmationHistory(state, commands, events, errors)']),
        ('CoreRuleEngine.ObservedUnitEvents.cs', 'RequiresObservedUnitEventHistory', ['HasArmamentPlayAttachment(context)']),
        ('CoreRuleEngine.ObservedUnitEvents.cs', 'ObservedUnitEventCaptureHash', ['context.ReflexiveTick']),
        ('CoreRuleEngine.ObservedUnitEvents.cs', 'ObservedUnitEventBirthTick', ['context.ReflexiveTick']),
        ('MatchRecovery.ObservedUnitEvents.cs', 'ValidateObservedUnitEventHistory', [
            'CoreRuleEngine.ObservedUnitEventBirthTick(item.Context)', 'command.Accepted',
            'CoreRuleEngine.ObservedUnitEventCaptureHash(item.Context, item.Source, item.Controller)',
            '!expected.SetEquals(current.Select(item => item.Id))']),
        ('CoreRuleEngine.UnitPlayMight.cs', 'CompanionPlayedUnit', ['HasArmamentPlayAttachment(', '.Other?.Binding']),
        ('CoreRuleEngine.UnitPlayMight.cs', 'ValidPermanentPlayCompanion', ['ValidArmamentPlayAttachmentContext(']),
        ('MatchRecovery.cs', 'ValidateSpellContinuations', ['ValidArmamentPlayAttachmentPermission(state, armamentHandPlay)']),
        ('MatchRecovery.cs', 'ValidateAuthoritativeState', ['ValidateSpellContinuations(authoritativeState, errors)']),
    ]
    exact = exact and all(all(token in member(filename, name) for token in tokens)
        for filename, name, tokens in checks)
    evidence = sorted({prefix + filename for filename, _, _ in checks})
    return result('route', '进攻技能结算时可选真实打出手牌中法力至多2的武装，仅免基础费用；成功打出后创建独立贴附技能，绑定原武装与来源代际，并与灵便共用触发排序及原始历史校验', evidence) if exact else result(
        'missing', '缺少限定武装的真实效果打出、独立内嵌贴附、共同排序，或原始对象／付款／捕获历史校验')


def audit_clause(card: dict, clause: dict, sources: Sources) -> dict:
    if clause.get("location") == "armament":
        matched = audit_armament_clause(card, clause, sources)
        if matched["status"] != "route" and (reviewed := reviewed_clause_route(card, clause, sources)):
            return reviewed
        return matched
    if armament_play := attack_armament_play_attachment_route(card, clause, sources):
        return armament_play
    if held_next_unit := held_next_unit_ready_boon_route(card, clause, sources):
        return held_next_unit
    if conditional_move := source_play_condition_movement_route(card, clause, sources):
        return conditional_move
    if trait_score := friendly_unit_trait_score_route(card, clause, sources):
        return trait_score
    if battle_reward := battle_win_and_paid_combat_route(card, clause, sources):
        return battle_reward
    kind = clause["kind"]
    if conditional := conditional_rune_and_conquest_route(card, clause, sources):
        return conditional
    if standby_unit := standby_unit_observer_route(card, clause, sources):
        return standby_unit
    if token_movement := friendly_token_attack_movement_route(card, clause, sources):
        return token_movement
    if stun_movement := stun_source_movement_route(card, clause, sources):
        return stun_movement
    if combat_cost := combat_spell_cost_route(card, clause, sources):
        return combat_cost
    if spell_mana_cost := confirmed_spell_mana_alternative_route(card, clause, sources):
        return spell_mana_cost
    if grant := another_friendly_here_grant_route(card, clause, sources):
        return grant
    if token_play := token_play_observer_route(card, clause, sources):
        return token_play
    if paid_stun := paid_target_play_stun_route(card, clause, sources):
        return paid_stun
    if equipment_theft := paid_equipment_theft_route(card, clause, sources):
        return equipment_theft
    if paid_source := paid_source_ready_might_route(card, clause, sources):
        return paid_source
    if recycle_runes := last_breath_recycle_runes_route(card, clause, sources):
        return recycle_runes
    if boon_mode := boon_mode_activation_route(card, clause, sources):
        return boon_mode
    if provided := provided_armament_keyword_route(card, clause, sources):
        return provided

    if enemy_death := enemy_death_source_clause_route(card, clause, sources):
        return enemy_death
    if colored := colored_legacy_activation_fee(card, clause, sources):
        if colored["status"] == "route":
            return native_unit_activation_binding(card, clause, sources)
        return colored
    if level_cost := experience_play_cost_route(card, clause, sources):
        return level_cost
    if experience := conditional_unit_experience_route(card, clause, sources):
        return experience
    if targeted := friendly_target_ready_route(card, clause, sources):
        return targeted
    if damage := source_attack_damage_route(card, clause, sources):
        return damage
    keep_recycle = r"每名玩家分别选择自己的(?:两|2)名单位(?:两|2)件装备(?:两|2)枚符文和(?:两|2)张手牌留在原位然后回收其余卡牌"
    if re.fullmatch(keep_recycle, normalize(unparenthesize(clause["text"]))):
        if any(b.get("recyclesUnkeptSacredJudgmentCards") for b in card.get("behaviors", [])):
            if any(b.get("recyclesUnkeptSacredJudgmentCards") and b.get("requiredTargetCount", 0) != 0
                   for b in card.get("behaviors", [])):
                return result("missing", "仍由施法者预提交双方保留目标；缺少各玩家结算选择及按种类回收")
            if reviewed := reviewed_clause_route(card, clause, sources):
                return reviewed
            return result("missing", "缺少已审的逐玩家结算选择、私有手牌与所属牌堆分类回收证据")
        return result("review", "须独立核对各玩家私有保留选择与符文／主牌分类回收，普通回收字段不足以认证")
    if rune := rune_domain_reveal_route(card, clause, sources):
        return rune
    if counted := counted_behavior_route(card, clause, sources):
        return counted
    if legacy := legacy_route(card, clause, sources):
        return legacy
    if routes := sources.exact_text_readers.get(normalize(unparenthesize(clause["text"]))):
        return result("route", "共享流程读取本句完整文本", routes)
    if native := native_activation_route(card, clause, sources):
        return native
    for probe in sources.probes.get(normalize(clause["text"]), []):
        # A donor can be executable when copied by Heimerdinger while its own
        # source still lacks an activation. Donor syntax is not a native route.
        if probe["method"] == "BorrowedExhaustParser.Parse":
            continue
        routes = sources.references(probe["method"])
        if routes:
            return result("route", "逐句解析及消费：" + probe["method"], routes)
    if kind == "keyword":
        return keyword_route(card, clause, sources)
    if normalize(clause["text"]) == "我在战斗中最后承担伤害":
        tags = "|".join(b.get("sourceUnitTags", "") for b in card.get("behaviors", []))
        return result("route", "后排的文字表述及实际物体标签", sources.references("BackRow")) if "后排" in tags else result("missing", "后排的文字表述未映射到实际物体标签")
    if kind == "activated" and (legend := legend_field_activation_route(card, clause, sources)):
        return legend
    families = ("triggers", "staticAuras", "staticAbilities", "replacements")
    for family in families:
        for item in card.get(family, []):
            if node_matches(clause, item) and (routes := sources.node_routes(item)):
                return result("route", f"{family}: {item['node']['kind']}", routes)
    if kind == "trigger" and card.get("cardCategory") == "legendary" and normalize(clause["text"]) == normalize(
            "当你将一名友方单位选为目标时，你可以选择让我变为休眠状态并支付{{A}}，以此让该单位变为活跃状态"):
        if any(ability.get("effectKind") == "READY_FRIENDLY_UNIT" and ability.get("requiresPendingFriendlyUnitTarget")
               for ability in card.get("legendActivationDefinitions", [])):
            return result("missing", "只有手动传奇入口；缺少选取目标触发、可选费用确认及正确的符能付款")
    if answer := behavior_operations(card, clause, sources):
        return answer
    if kind == "activated":
        for ability in card.get("activatedAbilities", []):
            text = ability.get("costText", "") + "：" + ability.get("effectText", "")
            if normalize(unparenthesize(clause["text"])) in normalize(unparenthesize(text)) and ability.get("kind"):
                routes = [p for symbol in sources.symbols.get(ability["kind"], []) for p in sources.references(symbol)]
                if routes:
                    return result("route", "独立激活技能规格：" + ability["kind"], routes)
        if clause["scope"] == clause["text"]:
            for ability in card.get("activations", []):
                if not activation_matches(ability, clause):
                    continue
                if implicit_recycle_activation_count(clause["text"]) is not None:
                    required = [
                        ("spec.DestroysSourceAsCost ? source.ObjectId", "CoreRuleEngine.SimpleActivatedAbilities.cs"),
                        ("!SimpleActivationTargetCountValid(ability, targets.Count)", "CoreRuleEngine.SimpleActivatedAbilities.cs"),
                        ("if (spec.RecyclesGraveyardTargets) return ResolveActivatedGraveyardRecycle(state, item)", "CoreRuleEngine.SimpleActivatedAbilities.cs"),
                        ("IsAnyGraveyardCard(", "CoreRuleEngine.ActivatedGraveyardRecycle.cs"),
                        ("RecycleGraveyardCards(", "CoreRuleEngine.ActivatedGraveyardRecycle.cs")]
                    paths = [[path for path in sources.references(symbol) if Path(path).name == filename]
                             for symbol, filename in required]
                    if all(paths):
                        return result("route", "先选择目标并支付来源自毁费用，独立回收仍合法的废牌目标",
                                      sorted({path for row in paths for path in row}))
                    continue
                routes = sources.value_routes(ability.get("effectKind", "")) + sources.value_routes(ability.get("abilityId", ""))
                if ability.get("kind"):
                    routes += sources.value_routes(ability["kind"])
                if ability.get("simpleEffect"):
                    routes += sources.references("ability.SimpleEffect")
                if routes:
                    return result("route", "已登记的激活技能及执行分支：" + ability["abilityId"], routes)
            if outcome := legend_activation_route(card, clause, sources):
                return outcome
        if reviewed := reviewed_clause_route(card, clause, sources):
            return reviewed
        if not card.get("activations") and not card.get("legendActivations"):
            # Quoted/granted skills need independent scope review before calling
            # the source's absent activation a real missing implementation.
            if "具有“" not in clause["scope"]:
                return result("missing", "激活技能目录未找到与本句费用、结算和限制匹配的来源项")
        return result("review", "存在技能来源项；仍需逐句核对费用、结算及限制")
    if reviewed := reviewed_clause_route(card, clause, sources):
        return reviewed
    if "不限数量" in clause["text"]:
        return result("missing", "未找到该规则例外的静态规格或执行路径")
    return result("review", "未建立本句到具体执行字段／消费者的对应关系")


def priority(card: dict) -> tuple[int, str]:
    category = card.get("cardCategory", "")
    return (0 if card["cardName"] in TOURNAMENT_PRIORITY_NAMES or any(x in category for x in ("hero", "legend", "champion")) else 1, card["cardNo"])


def build_report(inventory: dict, sources: Sources) -> dict:
    groups = defaultdict(list)
    for card in inventory["cards"]:
        groups[effect_identity(card)].append(card)
    cards = []
    for prints in groups.values():
        prints.sort(key=lambda c: (bool(re.search(r"[a-z*]", c["cardNo"].split("·")[-1])), c["cardNo"]))
        card = prints[0]
        rows = []
        for index, clause in enumerate(card_clauses(card), 1):
            outcomes = [audit_clause(p, clause, sources) for p in prints]
            # Equivalent art still needs every admitted print's route to exist.
            worst = max(outcomes, key=lambda o: {"verified": 0, "route": 1, "review": 2, "missing": 3}[o["status"]])
            row = dict(clause, **worst, index=index)
            row["affectedPrints"] = [p["cardNo"] for p, outcome in zip(prints, outcomes) if outcome["status"] == worst["status"]]
            if clause["location"] == "armament":
                row["sourceImages"] = [dict(cardNo=p["cardNo"], url=p["armamentText"]["sourceImageUrl"],
                    sha256=p["armamentText"]["sourceImageSha256"]) for p in prints]
            row["id"] = card["cardNo"] + ":" + clause["location"] + ":" + hashlib.sha256(clause["text"].encode()).hexdigest()[:12]
            rows.append(row)
        cards.append(dict(cardNo=card["cardNo"], name=card["cardName"] + ("·" + card["subTitle"] if card.get("subTitle") else ""),
                          prints=[p["cardNo"] for p in prints], priority=priority(card)[0], clauses=rows))
    cards.sort(key=lambda c: (c["priority"], c["cardNo"]))
    counts = Counter(row["status"] for card in cards for row in card["clauses"])
    return dict(schemaVersion=1, prints=len(inventory["cards"]), effectGroups=len(cards),
                clauseCount=sum(counts.values()), armamentSourceCoverage=inventory.get("armamentSourceCoverage", 0),
                counts=dict(sorted(counts.items())), cards=cards)


def markdown(report: dict) -> str:
    counts = report["counts"]
    lines = ["# 卡牌逐句效果清单", "", "由 `python3 scripts/audit-card-effects.py --refresh --markdown docs/CARD_BACKLOG.md` 生成。",
             "官方基准目录、运行时已审补充与中国区勘误共用服务端加载流程；同名不同副标题／效果分别核对，同效果异画共用句子，所有印次都检查实现路径。", "",
             f"另含 {report.get('armamentSourceCoverage', 0)} 个武装印次的下栏：逐张对照官方中文卡图，原文、卡图链接和 SHA256 保存在 `data/official/armament-text.zh-CN.json`。下栏单独检查宿主授予与触发，不用装备自身的上栏技能或关键词充作实现依据。下栏战力数值另与运行时逐项比较，不计入句数；风味文字和上栏复制效果的重复提示不另计。", "",
             f"当前 {report['prints']} 个印次、{report['effectGroups']} 项不同效果、{report['clauseCount']} 句：" + "，".join(f"{STATUSES[k]} {v}" for k, v in counts.items()) + "。", "",
             "“已找到实现路径”表示本句匹配到具体规格及执行消费者，不能据此证明所有规则边界正确；只有整张牌登记、解析器识别、旧夹具说明或关键词总表的状态，不算本句实现。未建立对应关系的句子保留“待确认”，不会直接当作规则缺陷。", "",
             "优先列英雄／传奇，以及在官方[洛杉矶赛事牌表（2026-09-30）](https://playriftbound.com/en-us/news/organizedplay/los-angeles-top-decks/)中出现的占卜贝壳、奇妙行囊、无主宝藏、先锋军备；牌表仅作排序参考，不代表中国区实时使用率。其余牌随后处理。李青·苦修者及增益计数由 N 线处理。", ""]
    for tier, title in ((0, "优先：赛事牌表出现的标准牌、英雄与传奇"), (1, "其他卡牌")):
        pending = [(c, r) for c in report["cards"] if c["priority"] == tier for r in c["clauses"] if r["status"] in ("missing", "review")]
        lines += [f"## {title}（{len(pending)} 句）", "", "| 卡号／牌名 | 原文句子 | 缺失类型／核对状态 |", "|---|---|---|"]
        for card, row in sorted(pending, key=lambda pair: (pair[1]["status"] != "missing", pair[0]["cardNo"], pair[1]["index"])):
            text = ("**武装下栏：** " if row.get("location") == "armament" else "") + row["text"].replace("|", "\\|").replace("\n", " ")
            numbers = "／".join(row["affectedPrints"])
            lines.append(f"| {numbers} {card['name']} | {text} | {STATUSES[row['status']]} · {KINDS[row['kind']]}：{row['reason']} |")
        lines.append("")
    return "\n".join(lines)


def refresh_inventory(root: Path, path: Path) -> dict:
    import os
    env = dict(os.environ, DOTNET_PROCESSOR_COUNT="1")
    subprocess.run(["dotnet", "build", str(PROJECT), "-m:1", "--nologo", "--verbosity", "quiet"], cwd=root, env=env, check=True)
    subprocess.run(["dotnet", "run", "--no-build", "--project", str(PROJECT), "--", str(path)], cwd=root, env=env, check=True)
    data = json.loads(path.read_text())
    data = attach_armament_sources(data, root / "data/official/armament-text.zh-CN.json")
    texts = sorted({clause["text"] for card in data["cards"] for clause in card_clauses(card)})
    inputs, outputs = path.with_name("probe-inputs.json"), path.with_name("probe-results.json")
    inputs.write_text(json.dumps(texts, ensure_ascii=False))
    subprocess.run(["dotnet", "run", "--no-build", "--project", str(PROJECT), "--", "--probes", str(inputs), str(outputs)], cwd=root, env=env, check=True)
    data["textProbes"] = json.loads(outputs.read_text())
    data["sourceFingerprint"] = source_fingerprint(root)
    path.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n")
    return data


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refresh", action="store_true", help="重新构建并导出运行时目录（单 dotnet 槽位）")
    parser.add_argument("--inventory", type=Path, default=Path("var/effect-audit/inventory.json"))
    parser.add_argument("--json", type=Path, default=Path("var/effect-audit/report.json"))
    parser.add_argument("--markdown", type=Path)
    parser.add_argument("--sample", type=int, default=12, help="每类可重复随机抽查样本数，种子固定为 20261105")
    args = parser.parse_args(argv)
    path = (ROOT / args.inventory).resolve()
    inventory = refresh_inventory(ROOT, path) if args.refresh or not path.exists() else json.loads(path.read_text())
    if inventory.get("sourceFingerprint") != source_fingerprint(ROOT):
        parser.error("运行时清单已过期；请使用 --refresh 重新导出")
    if inventory.get("schemaVersion") != 1 or not inventory.get("cards"):
        parser.error("运行时清单为空或版本不支持")
    try:
        inventory = attach_armament_sources(inventory, ROOT / "data/official/armament-text.zh-CN.json")
    except ValueError as error:
        parser.error(str(error))
    sources = Sources(ROOT, inventory["constants"], inventory.get("textProbes", []))
    report = build_report(inventory, sources)
    rng = random.Random(20261105)
    report["samples"] = {}
    for status in STATUSES:
        rows = [dict(cardNo=c["cardNo"], name=c["name"], **r) for c in report["cards"] for r in c["clauses"] if r["status"] == status]
        report["samples"][status] = rng.sample(rows, min(args.sample, len(rows)))
    output = ROOT / args.json
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    if args.markdown:
        (ROOT / args.markdown).write_text(markdown(report))
    print(json.dumps({key: report[key] for key in ("prints", "effectGroups", "clauseCount", "counts")}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
