from pathlib import Path
import shutil
import tempfile
import unittest

from test_effect_clause_audit import audit


class ConditionalUnitExperienceAuditTests(unittest.TestCase):
    POPPY = "当你打出我时，如果对手得分距离胜利得分不超过3分，则让我变为活跃状态，并获得3经验。"
    KHAZIX = "当我进攻或防守时，如果此处有一名落单的敌方单位，则让我本回合内{{S}}+2并获得2经验。"
    FILES = ["CoreRuleEngine.UnitEvents.cs", "CoreRuleEngine.UnitEventExperience.cs", "CoreRuleEngine.cs",
             "CoreRuleEngine.UnitEventBoonInstructions.cs", "MatchRecovery.cs"]

    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.engine = self.root / "src/Riftbound.Engine"
        self.engine.mkdir(parents=True)
        for name in self.FILES:
            shutil.copy(audit.ROOT / "src/Riftbound.Engine" / name, self.engine / name)

    def card(self, ready):
        return dict(cardNo="TEST", cardName="测试", cardCategory="hero_unit",
                    cardEffect=self.POPPY if ready else self.KHAZIX,
                    unitEventAbility=dict(timing="PLAY" if ready else "ATTACK_OR_DEFEND",
                        effect="READY_SELF" if ready else "MIGHT_SELF", amount=0 if ready else 2,
                        independentExperienceGain=3 if ready else 2, requiredOpponentVictoryDistance=3 if ready else 0,
                        requiresIsolatedEnemyHere=not ready))

    def outcome(self, card):
        return audit.audit_clause(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))["status"]

    def replace(self, filename, old, new):
        path = self.engine / filename
        source = path.read_text()
        self.assertIn(old, source)
        path.write_text(source.replace(old, new))

    def test_both_exact_live_profiles_have_their_full_consumers(self):
        for ready in [True, False]:
            self.assertEqual("route", self.outcome(self.card(ready)))

    def test_registration_or_parser_alone_does_not_certify_either_clause(self):
        for ready in [True, False]:
            card = self.card(ready)
            del card["unitEventAbility"]
            card["effects"] = [dict(templateId="gain_experience", status="implemented")]
            self.assertEqual("missing", self.outcome(card))

    def test_wrong_condition_effect_amount_or_experience_stays_missing(self):
        for ready in [True, False]:
            for key, value in [("timing", "MAIN"), ("effect", "DRAW"), ("amount", 9),
                               ("independentExperienceGain", 0), ("requiredOpponentVictoryDistance", 2),
                               ("requiresIsolatedEnemyHere", ready)]:
                with self.subTest(ready=ready, key=key):
                    card = self.card(ready)
                    card["unitEventAbility"][key] = value
                    self.assertEqual("missing", self.outcome(card))

    def test_unprinted_fees_or_restrictions_cannot_be_hidden_in_the_profile(self):
        for key, value in [("leadingPowerCost", 1), ("optional", True), ("sourceInLegendZone", True),
                           ("requiresEmpowered", True), ("modes", [dict(id="extra")])]:
            card = self.card(True)
            card["unitEventAbility"][key] = value
            self.assertEqual("missing", self.outcome(card))

    def test_experience_must_follow_the_live_resolution_even_when_self_instruction_noops(self):
        self.replace("CoreRuleEngine.UnitEvents.cs", "result = AppendIndependentUnitEventExperience(state, item, result);",
                     "result = result;")
        for ready in [True, False]:
            self.assertEqual("missing", self.outcome(self.card(ready)))

    def test_unused_experience_result_does_not_certify_the_returned_continuation(self):
        self.replace("CoreRuleEngine.UnitEvents.cs", "result = AppendIndependentUnitEventExperience(state, item, result);",
                     "var unused_result = AppendIndependentUnitEventExperience(state, item, result);")
        for ready in [True, False]:
            self.assertEqual("missing", self.outcome(self.card(ready)))

    def test_redaction_must_return_the_experience_augmented_result(self):
        self.replace("CoreRuleEngine.UnitEvents.cs",
                     "return source == item.SourceObjectId ? result : result with { Events = result.Events.Select",
                     "return source == item.SourceObjectId ? unchanged : unchanged with { Events = unchanged.Events.Select")
        for ready in [True, False]:
            self.assertEqual("missing", self.outcome(self.card(ready)))

    def test_direct_return_of_the_experience_continuation_is_still_recognized(self):
        path = self.engine / "CoreRuleEngine.UnitEvents.cs"
        source = path.read_text()
        before, marker, after = source.partition("private static StackResolutionResult ResolveUnitEvent(")
        self.assertTrue(marker)
        _, next_marker, rest = after.partition("private static StackResolutionResult ResolveUnitEventInstruction(")
        self.assertTrue(next_marker)
        path.write_text(before + marker + "MatchState state, StackItemState item)\n    {\n"
                        "        var result = ResolveUnitEventInstruction(state, item);\n"
                        "        return AppendIndependentUnitEventExperience(state, item, result);\n    }\n\n    "
                        + next_marker + rest)
        for ready in [True, False]:
            self.assertEqual("route", self.outcome(self.card(ready)))

    def test_unused_victory_distance_helper_does_not_certify_the_play_capture(self):
        self.replace("CoreRuleEngine.UnitEvents.cs", "OpponentWithinWinningScoreDistance(current, o.Controller,",
                     "UnusedCondition(current, o.Controller,")
        self.assertEqual("missing", self.outcome(self.card(True)))

    def test_hardcoded_five_points_does_not_certify_the_current_victory_goal(self):
        self.replace("CoreRuleEngine.cs", "opponentScore >= EffectiveWinningScore(state) - distance", "opponentScore >= 5")
        self.assertEqual("missing", self.outcome(self.card(True)))

    def test_both_battle_roles_must_actually_apply_the_isolation_filter(self):
        filename = "CoreRuleEngine.UnitEvents.cs"
        original = (self.engine / filename).read_text()
        for name in ["CaptureAttackUnitEvents", "CaptureRoleUnitEvents"]:
            with self.subTest(name=name):
                before, marker, after = original.partition("private static TriggerQueueItemState[] " + name + "(")
                self.assertTrue(marker)
                after = after.replace(".Where(o => UnitEventCaptureConditionSatisfied(state, o))", "", 1)
                (self.engine / filename).write_text(before + marker + after)
                self.assertEqual("missing", self.outcome(self.card(False)))

    def test_owner_based_or_global_isolation_is_not_the_printed_condition(self):
        path = self.engine / "CoreRuleEngine.UnitEventExperience.cs"
        original = path.read_text()
        for old, new in [("== here.BattlefieldObjectId", "!= null"),
                         (".GroupBy(card => EffectiveFieldControllerId(", ".GroupBy(card => OwnerOf("),
                         (".Any(group => group.Count() == 1)", ".Any()")]:
            with self.subTest(old=old):
                self.assertIn(old, original)
                path.write_text(original.replace(old, new))
                self.assertEqual("missing", self.outcome(self.card(False)))

    def test_the_wrong_scope_or_a_different_sentence_is_not_certified(self):
        card = self.card(True)
        clause = audit.card_clauses(card)[0]
        clause["scope"] = "当你获得经验时。"
        self.assertEqual("missing", audit.audit_clause(card, clause, audit.Sources(self.root, []))["status"])
        card["cardEffect"] = self.POPPY.replace("3经验", "4经验")
        self.assertNotEqual("route", self.outcome(card))

    def test_independent_experience_requires_the_captured_controller_and_resulting_experience(self):
        self.replace("CoreRuleEngine.UnitEventExperience.cs",
                     "GainExperience(result.PlayerExperience, item.ControllerId, spec.IndependentExperienceGain,",
                     "GainExperience(state.PlayerExperience, state.TurnPlayerId, spec.IndependentExperienceGain,")
        for ready in [True, False]:
            self.assertEqual("missing", self.outcome(self.card(ready)))
