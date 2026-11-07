import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("boon_mode_audit", ROOT / "scripts/audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class BoonModeActivationAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        modes = [
            ("CONSUME_BOON_DAMAGE_2", dict(battlefieldUnitDamage=2), 1, 2),
            ("CONSUME_BOON_STUN_UNIT", dict(stunsTarget=True), 1, 0),
            ("CONSUME_BOON_READY_SELF", dict(readiesSource=True), 0, 0),
            ("CONSUME_BOON_ROAM_SELF", dict(grantsSourceRoam=True), 0, 0),
        ]
        return dict(cardNo="OGN·157/298", cardName="乌迪尔", cardCategory="hero_unit",
                    cardEffect=("消耗我的增益：从以下中选择一个你本回合内尚未选过的效果-\n"
                                "·对战场上的一名单位造成2点伤害。\n·眩晕战场上的一名单位。\n"
                                "·让我变为活跃状态。\n·使我本回合内获得{{游走}}。"),
                    activations=[dict(abilityId=mode, effectKind=mode, requiredTargetCount=targets,
                        damageAmount=damage, appliesSpellshieldTargetTax=bool(targets),
                        targetScope="BATTLEFIELD_UNIT" if targets else "", supportsCopiedRuleText=True,
                        sourceRequiredTag="CARD_TYPE:UNIT",
                        simpleEffect=dict(effect, consumesSourceBoonAsCost=True, oncePerTurn=True))
                        for mode, effect, targets, damage in modes])

    def statuses(self, card=None, sources=None):
        card = card or self.card()
        return [audit.audit_clause(card, row, sources or self.sources)["status"]
                for row in audit.card_clauses(card)]

    def changed(self, file, token, decoy=False):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        path = "src/Riftbound.Engine/" + file
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, "REMOVED_BOUND_CONSUMER")
        if decoy:
            sources.files[path] += "\n    private static void UnusedDecoy() { " + token + " }\n"
        return sources

    def test_all_five_clauses_require_real_cost_history_and_mode_consumers(self):
        self.assertEqual(["route"] * 5, self.statuses())

    def test_one_absent_mode_reopens_its_sentence_and_the_mode_header(self):
        card = self.card()
        del card["activations"][1]
        self.assertEqual(["missing", "route", "missing", "route", "route"], self.statuses(card))

    def test_cost_or_round_limits_cannot_be_substituted_by_a_registration(self):
        for key, value in [("consumesSourceBoonAsCost", False), ("oncePerTurn", False),
                           ("oncePerTurnPerPlayer", True), ("exhaustFriendlyUnitMinimumPowerAsCost", 0),
                           ("sourcePowerBonus", 1)]:
            with self.subTest(key=key):
                card = self.card()
                card["activations"][0]["simpleEffect"][key] = value
                self.assertEqual(["missing", "missing", "route", "route", "route"], self.statuses(card))

    def test_wrong_damage_scope_extra_payment_or_speed_is_not_the_printed_mode(self):
        for key, value in [("damageAmount", 3), ("targetScope", "FIELD_UNIT"), ("powerCost", 1),
                           ("exhaustsSourceAsCost", True), ("reactionSpeed", True),
                           ("appliesSpellshieldTargetTax", False), ("maximumTargetCount", 0)]:
            with self.subTest(key=key):
                card = self.card()
                card["activations"][0][key] = value
                self.assertEqual(["missing", "missing", "route", "route", "route"], self.statuses(card))

    def test_paid_cost_source_limits_and_token_departure_must_stay_executable(self):
        for file, token in [
            ("CoreRuleEngine.SimpleActivatedAbilities.cs", "BoonRules.Count(source) > 0"),
            ("CoreRuleEngine.SimpleActivatedAbilities.cs", "source = BoonRules.Consume(source);"),
            ("CoreRuleEngine.SimpleActivatedAbilities.cs", "ConsumedSourceBoon = consumedSourceBoon"),
            ("CoreRuleEngine.Tokens.cs", "!ValidSimpleActivation(before with { Tick = result.State.Tick }, item)"),
            ("CoreRuleEngine.cs", "return ResolveSimpleActivation(state, intent, command, ability);"),
            ("CoreRuleEngine.cs", "return ResolveSimpleActivationEffect(state, stackItem, simpleAbility);"),
            ("MatchRecovery.PerPlayerActivationUses.cs", "expected.SetEquals(actual)"),
            ("MatchRecovery.PerPlayerActivationUses.cs", "var abilities = PerPlayerActivationAbilities;"),
            ("MatchRecovery.PerPlayerActivationUses.cs", '.Where(a => a.SimpleEffect is { OncePerTurn: true })'),
        ]:
            with self.subTest(token=token):
                self.assertEqual(["missing"] * 5, self.statuses(sources=self.changed(file, token)))

    def test_unused_cost_decoys_cannot_certify_the_real_activation(self):
        sources = self.changed("CoreRuleEngine.SimpleActivatedAbilities.cs",
                               "source = BoonRules.Consume(source);", decoy=True)
        self.assertEqual(["missing"] * 5, self.statuses(sources=sources))

    def test_unused_correct_filter_cannot_certify_the_recovery_table(self):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        path = "src/Riftbound.Engine/MatchRecovery.PerPlayerActivationUses.cs"
        correct = ".Where(a => a.SimpleEffect is { OncePerTurn: true })"
        self.assertIn(correct, sources.files[path])
        text = sources.files[path].replace(correct,
            ".Where(a => a.SimpleEffect is { OncePerTurn: true, OncePerTurnPerPlayer: true })")
        end = text.rfind("}")
        sources.files[path] = (text[:end] + "\n    private static void UnusedCorrectFilter() { "
            "_ = P4ActivatedAbilityCatalog.GetAll()" + correct + "; }\n" + text[end:])
        self.assertEqual(["missing"] * 5, self.statuses(sources=sources))

    def test_an_unused_correct_dispatch_cannot_replace_the_real_activation_call(self):
        sources = self.changed("CoreRuleEngine.cs",
                               "return ResolveSimpleActivation(state, intent, command, ability);", decoy=True)
        self.assertEqual(["missing"] * 5, self.statuses(sources=sources))

    def test_missing_pre_cost_target_witness_reopens_only_the_targeted_options(self):
        for token in [
            "var tokenTargets = targets.Where(id => state.CardObjects.TryGetValue(id, out var target)",
            ".Where(id => state.CardObjects.ContainsKey(id))",
            "TargetGenerations = targetGenerations.Count == 0 ? null : targetGenerations",
        ]:
            with self.subTest(token=token):
                sources = self.changed("CoreRuleEngine.SimpleActivatedAbilities.cs", token)
                self.assertEqual(["missing", "missing", "missing", "route", "route"],
                                 self.statuses(sources=sources))

    def test_removing_only_the_roam_consumer_keeps_the_other_three_effects(self):
        sources = self.changed("CoreRuleEngine.SimpleActivatedAbilities.cs", "if (spec.GrantsSourceRoam")
        self.assertEqual(["missing", "route", "route", "route", "missing"], self.statuses(sources=sources))

    def test_larger_cost_scope_and_unknown_modal_lines_are_not_independent_skills(self):
        card = self.card()
        for row in audit.card_clauses(card):
            scoped = dict(row, scope="如果我处于战斗中，则" + row["scope"])
            self.assertEqual("missing", audit.audit_clause(card, scoped, self.sources)["status"])
        unknown = dict(text="·抽一张牌。", kind="activated",
                       scope=audit.card_clauses(card)[0]["text"], location="upper")
        self.assertEqual("missing", audit.audit_clause(card, unknown, self.sources)["status"])
        self.assertFalse(audit.activation_matches(card["activations"][0], unknown))

    def test_unrelated_text_on_the_same_card_keeps_its_own_audit_binding(self):
        card = self.card()
        for row in [dict(text="{{急速}}", kind="keyword", scope="{{急速}}"),
                    dict(text="{{横置}}：抽一张牌。", kind="activated", scope="{{横置}}：抽一张牌。")]:
            self.assertIsNone(audit.boon_mode_activation_route(card, row, self.sources))


if __name__ == "__main__":
    unittest.main()
