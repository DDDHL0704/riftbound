from pathlib import Path
import tempfile
import unittest

from test_effect_clause_audit import audit, card


TEXT = "当我进攻或防守时，让你在此处的另一名单位在本回合内获得{{S}}+3和{{壁垒}}。（其在战斗中首先承担伤害。）"
UNITS = '''
private static object CaptureUnitEvents(object state) {
    spec.Timing == "ATTACK_OR_DEFEND" && timing is "ATTACK" or "DEFEND";
}
    private static object CaptureAttackUnitEvents(object state) {
    CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK");
}
    private static object CaptureRoleUnitEvents(object state) {
    CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, role);
}
    private static object UnitEventTargets(object state) {
    spec.TargetsAnotherFriendlyHere; c.ObjectId != item.SourceObjectId;
    EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId;
    UnitEventTargetIsHere(state, item, c.ObjectId);
}
    private static object ResolveUnitEvent(object state) {
    spec.Effect == "MIGHT_FRIENDLY"; StackObjectRules.ReferencesCurrentObject(state, item, buffed);
    UnitEventTargets(state, item).Contains(buffed); ApplyPowerModifier(state, cards[buffed], spec.Amount);
    spec.GrantedCombatKeyword is { } keyword; TemporaryCombatKeywordRules.Prefix + keyword;
    TemporaryCombatKeywordRules.AppliedEffectId(cards[buffed], definition);
    UntilEndOfTurnEffects = [..cards[buffed].UntilEndOfTurnEffects, effect];
}
'''
HERE = '''private static bool UnitEventTargetIsHere(object state) {
    CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration);
    at.Zone == here.Zone; at.PlayerId == here.PlayerId; at.BattlefieldObjectId == here.BattlefieldObjectId;
}'''


class YuumiCombatSupportClauseAuditTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root = Path(temp.name); self.engine = self.root / "src/Riftbound.Engine"
        self.engine.mkdir(parents=True)
        self.profile = dict(timing="ATTACK_OR_DEFEND", effect="MIGHT_FRIENDLY", amount=3,
                            targetsAnotherFriendlyHere=True, grantedCombatKeyword="壁垒")
        self.units = self.engine / "CoreRuleEngine.UnitEvents.cs"; self.units.write_text(UNITS)
        (self.engine / "CoreRuleEngine.PermanentUnitEvents.cs").write_text(HERE)
        (self.engine / "MatchRecovery.cs").write_text("ValidUnitEvent(state); ValidUnitEventTargets(item);")

    def outcome(self, **overrides):
        value = card(TEXT, unitEventAbility=self.profile, **overrides)
        return audit.audit_clause(value, audit.card_clauses(value)[0], audit.Sources(self.root, []))["status"]

    def test_exact_profile_and_live_shared_consumers_have_a_route(self):
        self.assertEqual("route", self.outcome())

    def test_the_actual_battle_roles_here_filter_and_resolver_have_a_route(self):
        value = card(TEXT, unitEventAbility=self.profile)
        self.assertEqual("route", audit.audit_clause(value, audit.card_clauses(value)[0],
            audit.Sources(audit.ROOT, []))["status"])

    def test_shared_exact_timing_dispatch_is_followed_and_must_be_used(self):
        direct = 'spec.Timing == "ATTACK_OR_DEFEND" && timing is "ATTACK" or "DEFEND"'
        call = 'UnitEventTimingMatches(spec.Timing, timing)'
        body = 'declared?.Split("_OR_", StringSplitOptions.None).Contains(timing, StringComparer.Ordinal) == true'
        shared = UNITS.replace(direct, call) + '\n    private static bool UnitEventTimingMatches(string declared, string timing) => ' + body + ';\n'
        self.units.write_text(shared)
        self.assertEqual("route", self.outcome())
        for token in (call, body):
            with self.subTest(token=token):
                self.units.write_text(shared.replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())

    def test_substring_timing_or_an_unused_exact_matcher_cannot_certify_both_roles(self):
        direct = 'spec.Timing == "ATTACK_OR_DEFEND" && timing is "ATTACK" or "DEFEND"'
        body = 'declared?.Split("_OR_", StringSplitOptions.None).Contains(timing, StringComparer.Ordinal) == true'
        shared = UNITS.replace(direct, 'UnitEventTimingMatches(spec.Timing, timing)')
        for wrong in ('declared?.Contains(timing) == true', 'declared == "ATTACK"', 'false'):
            with self.subTest(wrong=wrong):
                self.units.write_text(shared + '\n    private static bool UnitEventTimingMatches(string declared, string timing) => ' + wrong
                    + ';\n    private static bool Unused(string declared, string timing) => ' + body + ';\n')
                self.assertEqual("missing", self.outcome())

    def test_a_base_registration_or_a_might_only_trigger_is_missing(self):
        self.profile = None; self.assertEqual("missing", self.outcome())
        self.profile = dict(timing="ATTACK_OR_DEFEND", effect="MIGHT_FRIENDLY", amount=3)
        self.assertEqual("missing", self.outcome())

    def test_wrong_amount_scope_keyword_timing_or_an_extra_cost_is_missing(self):
        original = self.profile.copy()
        for key, value in [("timing", "ATTACK"), ("effect", "MIGHT_SELF"), ("amount", 2),
                           ("targetsAnotherFriendlyHere", False), ("grantedCombatKeyword", "后排"),
                           ("optional", True), ("leadingPowerCost", 1), ("sourceInLegendZone", True)]:
            with self.subTest(key=key):
                self.profile = dict(original, **{key: value})
                self.assertEqual("missing", self.outcome())

    def test_removing_either_battle_role_or_a_live_target_check_is_missing(self):
        for token in ['CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK")',
                      "CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, role)",
                      "c.ObjectId != item.SourceObjectId", "UnitEventTargetIsHere(state, item, c.ObjectId)",
                      "StackObjectRules.ReferencesCurrentObject(state, item, buffed)"]:
            with self.subTest(token=token):
                self.units.write_text(UNITS.replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())

    def test_keywords_or_power_in_an_unused_helper_do_not_certify_the_resolver(self):
        for token in ["ApplyPowerModifier(state, cards[buffed]", "TemporaryCombatKeywordRules.AppliedEffectId(cards[buffed]",
                      "UntilEndOfTurnEffects = [..cards[buffed].UntilEndOfTurnEffects, effect]"]:
            with self.subTest(token=token):
                self.units.write_text(UNITS.replace(token, "disabled") + "\n    private static void Unused() { " + token + "; }")
                self.assertEqual("missing", self.outcome())

    def test_here_requires_the_same_source_generation_and_the_current_location(self):
        path = self.engine / "CoreRuleEngine.PermanentUnitEvents.cs"
        for token in ["item.UnitEvent!.SourceGeneration", "at.Zone == here.Zone", "at.PlayerId == here.PlayerId",
                      "at.BattlefieldObjectId == here.BattlefieldObjectId"]:
            with self.subTest(token=token):
                path.write_text(HERE.replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())

    def test_a_live_resolution_wrapper_is_followed_but_an_unused_instruction_is_not(self):
        instruction = UNITS.replace("ResolveUnitEvent(", "ResolveUnitEventInstruction(")
        self.units.write_text(instruction + "\n    private static object ResolveUnitEvent(object state) { ResolveUnitEventInstruction(state, item); }")
        self.assertEqual("route", self.outcome())
        self.units.write_text(instruction + "\n    private static object ResolveUnitEvent(object state) { return null; }")
        self.assertEqual("missing", self.outcome())

    def test_unrelated_cards_scopes_and_missing_recovery_cannot_borrow_the_route(self):
        value = card(TEXT, unitEventAbility=self.profile)
        clause = audit.card_clauses(value)[0]; clause["scope"] = "当我进攻时，抽一张牌。"
        self.assertEqual("missing", audit.audit_clause(value, clause, audit.Sources(self.root, []))["status"])
        value["cardCategory"] = "legendary"
        self.assertEqual("missing", audit.audit_clause(value, audit.card_clauses(value)[0], audit.Sources(self.root, []))["status"])
        (self.engine / "MatchRecovery.cs").write_text("unrelated")
        self.assertEqual("missing", self.outcome())
