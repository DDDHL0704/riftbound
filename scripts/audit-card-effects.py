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
    if re.search(r"不限数量|至多.*增益|不能|不可|可以.+(?:打出|移动)|跳过|改为|额外.+(?:次|个|张)|上限|仅(?:可|能)|只能|无法", bare):
        # A triggered rule change is still a trigger; check its timing first.
        if not re.match(r"(?:每当|当|在.+时)", bare):
            return "rule"
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
                  "cardColorList")) + (normalize("\n".join(unparenthesize(line) for line in card["cardEffect"].splitlines())),)


def source_fingerprint(root: Path) -> str:
    paths = sorted(p for directory in ("src/Riftbound.Engine", "src/Riftbound.CardCatalog", "src/Riftbound.Contracts", str(PROJECT))
                   for p in (root / directory).rglob("*")
                   if p.is_file() and p.suffix in (".cs", ".csproj") and not {"obj", "bin"} & set(p.parts))
    paths += sorted(p for p in (root / "data/official").rglob("*") if p.is_file() and p.suffix in (".json", ".gz"))
    if (script := root / "scripts/audit-card-effects.py").exists():
        paths.append(script)
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
    "PLAY": r"(?:打出我|我被打出|将我打出)", "PLAY_OR_CONQUEST": r"打出我|我被打出|我征服",
    "PLAY_OR_EXHAUST": r"打出我|我被打出|我变为休眠", "PLAY_OR_MOVE": r"打出我|我被打出|我移动",
    "PLAY_OR_LEAVE": r"打出我|我被打出|我离场", "PLAY_OR_START": r"打出我|我被打出|回合开始",
    "PLAY_AFTER_ANOTHER_CARD": r"鼓舞", "BECAME_EXHAUSTED": r"我变为休眠",
    "BECAME_READY": r"我变为活跃", "ANOTHER_UNIT_PLAYED": r"打出.*(?:另一|其他).*单位",
    "FRIENDLY_UNIT_PLAYED": r"打出.*单位", "ANOTHER_FRIENDLY_UNIT_EXHAUSTED": r"另一名友方单位变为休眠",
    "FRIENDLY_UNITS_EXHAUSTED_TOGETHER": r"同时.*友方单位变为休眠",
    "ENEMY_MIGHT_REDUCED_HERE": r"敌方单位战力降低", "SELF_DISCARDED": r"弃置我",
    "OPPONENT_EQUIPMENT_PLAYED": r"对手打出.*装备", "BATTLE_END": r"战斗结束",
    "ATTACK_OR_DEFEND": r"我进攻|我防守", "ATTACK": r"我进攻", "DEFEND": r"我防守",
    "CONQUEST": r"我征服", "MAIN": r"主阶段", "FRIENDLY_ATTACK_HERE": r"友方单位.*进攻",
}


def meaningful(value) -> bool:
    return value is not None and value is not False and value not in ("", "NONE", -1, 0)


def activation_matches(ability: dict, clause: dict) -> bool:
    cost, effect = re.split("[：:]", unparenthesize(clause["text"]), maxsplit=1)
    if bool(re.search(r"\{\{(?:横置|T)\}\}", cost)) != bool(ability.get("exhaustsSourceAsCost")):
        return False
    mana = sum(int(n) for n in re.findall(r"\{\{([0-9]+)\}\}", cost))
    if mana != ability.get("manaCost", 0):
        return False
    colors = re.findall(r"\{\{(A|红色|绿色|蓝色|橙色|紫色|黄色)\}\}", cost)
    if len(colors) != ability.get("powerCost", 0) + sum((ability.get("powerCostByTrait") or {}).values()):
        return False
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


def behavior_operations(card: dict, clause: dict, sources: Sources) -> dict | None:
    bare = unparenthesize(clause["text"])
    scope = unparenthesize(clause["scope"])
    if clause["kind"] == "trigger":
        for behavior in card.get("behaviors", []):
            event = behavior.get("unitEventAbility", {})
            if event and re.search(EVENT_TIMINGS.get(event["timing"], r"(?!)"), scope):
                timing = sources.references('"' + event["timing"] + '"')
                effect = sources.references('"' + event["effect"] + '"')
                if timing and effect:
                    return result("route", "事件技能：" + event["timing"] + "/" + event["effect"], timing + effect)
        if not re.search(r"打出我|我被打出|将我打出", scope):
            return None
    elif clause["kind"] not in ("effect", "static"):
        return None
    if clause["kind"] == "static" and not re.search(r"以(?:活跃|休眠)状态进场", bare):
        return None
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


def audit_clause(card: dict, clause: dict, sources: Sources) -> dict:
    kind = clause["kind"]
    if routes := sources.exact_text_readers.get(normalize(unparenthesize(clause["text"]))):
        return result("route", "共享流程读取本句完整文本", routes)
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
            for ability in card.get("legendActivations", []):
                if routes := sources.value_routes(ability):
                    return result("route", "传奇技能来源及执行分支：" + ability, routes)
        if not card.get("activations") and not card.get("legendActivations"):
            # Quoted/granted skills need independent scope review before calling
            # the source's absent activation a real missing implementation.
            if "具有“" not in clause["scope"]:
                return result("missing", "激活技能目录和传奇技能目录均无来源项")
        return result("review", "存在技能来源项；仍需逐句核对费用、结算及限制")
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
        for index, clause in enumerate(split_clauses(card["cardEffect"], card.get("cardCategory", "")), 1):
            outcomes = [audit_clause(p, clause, sources) for p in prints]
            # Equivalent art still needs every admitted print's route to exist.
            worst = max(outcomes, key=lambda o: {"verified": 0, "route": 1, "review": 2, "missing": 3}[o["status"]])
            row = dict(clause, **worst, index=index)
            row["affectedPrints"] = [p["cardNo"] for p, outcome in zip(prints, outcomes) if outcome["status"] == worst["status"]]
            row["id"] = card["cardNo"] + ":" + hashlib.sha256(clause["text"].encode()).hexdigest()[:12]
            rows.append(row)
        cards.append(dict(cardNo=card["cardNo"], name=card["cardName"] + ("·" + card["subTitle"] if card.get("subTitle") else ""),
                          prints=[p["cardNo"] for p in prints], priority=priority(card)[0], clauses=rows))
    cards.sort(key=lambda c: (c["priority"], c["cardNo"]))
    counts = Counter(row["status"] for card in cards for row in card["clauses"])
    return dict(schemaVersion=1, prints=len(inventory["cards"]), effectGroups=len(cards),
                clauseCount=sum(counts.values()), counts=dict(sorted(counts.items())), cards=cards)


def markdown(report: dict) -> str:
    counts = report["counts"]
    lines = ["# 卡牌逐句效果清单", "", "由 `python3 scripts/audit-card-effects.py --refresh --markdown docs/CARD_BACKLOG.md` 生成。",
             "官方基准目录、运行时已审补充与中国区勘误共用服务端加载流程；同名不同副标题／效果分别核对，同效果异画共用句子，所有印次都检查实现路径。", "",
             f"当前 {report['prints']} 个印次、{report['effectGroups']} 项不同效果、{report['clauseCount']} 句：" + "，".join(f"{STATUSES[k]} {v}" for k, v in counts.items()) + "。", "",
             "“已找到实现路径”表示本句匹配到具体规格及执行消费者，不能据此证明所有规则边界正确；只有整张牌登记、解析器识别、旧夹具说明或关键词总表的状态，不算本句实现。未建立对应关系的句子保留“待确认”，不会直接当作规则缺陷。", "",
             "优先列英雄／传奇，以及在官方[洛杉矶赛事牌表（2026-09-30）](https://playriftbound.com/en-us/news/organizedplay/los-angeles-top-decks/)中出现的占卜贝壳、奇妙行囊、无主宝藏、先锋军备；牌表仅作排序参考，不代表中国区实时使用率。其余牌随后处理。李青·苦修者及增益计数由 N 线处理。", ""]
    for tier, title in ((0, "优先：赛事牌表出现的标准牌、英雄与传奇"), (1, "其他卡牌")):
        pending = [(c, r) for c in report["cards"] if c["priority"] == tier for r in c["clauses"] if r["status"] in ("missing", "review")]
        lines += [f"## {title}（{len(pending)} 句）", "", "| 卡号／牌名 | 原文句子 | 缺失类型／核对状态 |", "|---|---|---|"]
        for card, row in sorted(pending, key=lambda pair: (pair[1]["status"] != "missing", pair[0]["cardNo"], pair[1]["index"])):
            text = row["text"].replace("|", "\\|").replace("\n", " ")
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
    texts = sorted({clause["text"] for card in data["cards"] for clause in split_clauses(card["cardEffect"], card["cardCategory"])})
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
