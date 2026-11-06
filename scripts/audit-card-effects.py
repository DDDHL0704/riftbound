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


def is_activation(text: str) -> bool:
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


def activation_cost_matches(ability: dict, clause: dict) -> bool:
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
    _, effect = re.split("[：:]", unparenthesize(clause["text"]), maxsplit=1)
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
    effect = re.split("[：:]", unparenthesize(clause["text"]), maxsplit=1)[1].strip()
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


def behavior_operations(card: dict, clause: dict, sources: Sources) -> dict | None:
    bare = unparenthesize(clause["text"])
    scope = unparenthesize(clause["scope"])
    if clause["kind"] == "trigger":
        events = [card.get("unitEventAbility")] + [behavior.get("unitEventAbility") for behavior in card.get("behaviors", [])]
        for event in events:
            if event and re.search(EVENT_TIMINGS.get(event["timing"], r"(?!)"), scope) and event_operations_match(event, bare):
                timing = sources.references('"' + event["timing"] + '"')
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
    if any(sources.references(reader) for reader in ("armament.EffectText", "definition.EffectText", "gearDefinition.EffectText")):
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


def audit_clause(card: dict, clause: dict, sources: Sources) -> dict:
    if clause.get("location") == "armament":
        return audit_armament_clause(card, clause, sources)
    kind = clause["kind"]
    keep_recycle = r"每名玩家分别选择自己的(?:两|2)名单位(?:两|2)件装备(?:两|2)枚符文和(?:两|2)张手牌留在原位然后回收其余卡牌"
    if re.fullmatch(keep_recycle, normalize(unparenthesize(clause["text"]))):
        if any(b.get("recyclesUnkeptSacredJudgmentCards") for b in card.get("behaviors", [])):
            return result("missing", "仍由施法者预提交双方保留目标；缺少各玩家结算选择及按种类回收")
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
    families = ("triggers", "staticAuras", "staticAbilities", "replacements")
    for family in families:
        for item in card.get(family, []):
            if node_matches(clause, item) and (routes := sources.node_routes(item)):
                return result("route", f"{family}: {item['node']['kind']}", routes)
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
