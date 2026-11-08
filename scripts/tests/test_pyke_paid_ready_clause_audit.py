from pathlib import Path
import tempfile
import unittest

from test_effect_clause_audit import audit, card

TEXT = "当你打出我时，如果你支付了该额外费用，则让我变为活跃状态，并让我本回合内{{S}}+2。"
SOURCE = '''
    internal static object PlayInstructionSource(object state) {
        state.CardPlays.Resolved.FirstOrDefault(play => play.PlayId == item.StackItemId
            && play.CardNo == item.CardNo && play.PlayerId == item.ControllerId
            && play.Source.ObjectId == item.SourceObjectId);
    }
    internal static bool SourceOptionalReadyPowerCostWasPaid(object state) {
        RuneTrait.Normalize(behavior.SourceReadyPowerModifierAdditionalPowerTrait);
        behavior.SourceReadyPowerModifierAdditionalPowerCost > 0; behavior.SourceReadyPowerModifierAmount != 0;
        item.OptionalCosts.Any(cost => TryParseSpendPowerOptionalCost(cost));
        amount == behavior.SourceReadyPowerModifierAdditionalPowerCost; RuneTrait.Normalize(trait) == requiredTrait;
    }
'''
CONFIRM = '''
    private static bool HasPendingPermanentPlayAbility(object state) {
        OfficialPrintedTimingRules.HasPlayTrigger(behavior.CardNo);
        behavior.SourceReadyPowerModifierAdditionalPowerCost <= 0 || SourceOptionalReadyPowerCostWasPaid(behavior, item);
    }
    private static object PendingPermanentPlayAbility(object state) {
        SourceConfirmed = true; "TRIGGER_QUEUED"; state.StackItems.Where(x).Append(ability);
    }
'''
CORE = '''
    private static object ResolveStackItemEffectCore(object state) {
        HasPendingPermanentPlayAbility(state, stackItem, behavior);
        PendingPermanentPlayAbility(state, stackItem, playerZones, cardObjects, events);
            TryResolveSourceUnitOptionalReadyPower(
                state, playerZones);
    }
    private static bool TryResolveSourceUnitOptionalReadyPower(object state) {
        SourceOptionalReadyPowerCostWasPaid(behavior, stackItem); !stackItem.SourceConfirmed;
        PlayInstructionSource(state, stackItem); sourceState.ObjectGeneration != source.Generation;
        !IsFaceUpNonStandbyUnit(sourceState); !IsObjectOnField(playerZones, source.ObjectId);
        var canReady = ReadyByEffectPermittedByFieldRules(playerZones, cardObjects, sourceState);
        if (canReady) AddReadiedObservation(playerZones, cardObjects, sourceState, events);
        if (canReady) events.Add(CaptureSelfReady(readied));
        ApplyDirectUntilEndPowerModifier(sourceState, behavior.SourceReadyPowerModifierAmount);
        IsExhausted = canReady ? false : sourceState.IsExhausted; cardObjects[stackItem.SourceObjectId] = nextSourceState;
    }
'''
RECOVERY = '''
    internal static bool Valid(object state) {
        CoreRuleEngine.SourceOptionalReadyPowerCostWasPaid(optionalReady, item);
        instruction.SourceReadyPowerModifierAdditionalPowerCost > 0;
        CoreRuleEngine.PlayInstructionSource(state, item); entered.ObjectGeneration < receipt.Generation;
    }
'''


class PykePaidReadyClauseAuditTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.root = Path(temp.name); self.engine = self.root / "src/Riftbound.Engine"; self.engine.mkdir(parents=True)
        self.code = {"CoreRuleEngine.cs": CORE, "CoreRuleEngine.PermanentPlaySource.cs": SOURCE,
                     "CoreRuleEngine.PermanentConfirmation.cs": CONFIRM, "PermanentPlayTriggerRecoveryRules.cs": RECOVERY,
                     "MatchRecovery.cs": "PermanentPlayTriggerRecoveryRules.Valid(state, item);"}
        for name, value in self.code.items(): (self.engine / name).write_text(value)
        self.behavior = dict(playsSourceToBaseAsUnit=True, sourceReadyPowerModifierAdditionalPowerCost=1,
                             sourceReadyPowerModifierAdditionalPowerTrait="red", sourceReadyPowerModifierAmount=2)

    def outcome(self):
        value = card(TEXT); value["behaviors"] = [self.behavior]
        return audit.audit_clause(value, audit.card_clauses(value)[0], audit.Sources(self.root, []))["status"]

    def test_complete_paid_trigger_and_original_source_consumer_has_a_route(self):
        self.assertEqual("route", self.outcome())

    def test_the_actual_shared_resolution_and_recovery_consumers_have_a_route(self):
        value = card(TEXT); value["behaviors"] = [self.behavior]
        self.assertEqual("route", audit.audit_clause(value, audit.card_clauses(value)[0],
            audit.Sources(audit.ROOT, []))["status"])

    def test_registration_alone_or_a_wrong_amount_cost_trait_or_type_is_missing(self):
        original = self.behavior.copy()
        for key, value in [("sourceReadyPowerModifierAmount", 3), ("sourceReadyPowerModifierAdditionalPowerCost", 2),
                           ("sourceReadyPowerModifierAdditionalPowerTrait", "blue"), ("playsSourceToBaseAsUnit", False),
                           ("sourceReadyPowerModifierAdditionalPowerCost", True)]:
            with self.subTest(key=key, value=value):
                self.behavior = dict(original, **{key: value}); self.assertEqual("missing", self.outcome())
        self.behavior = {}; self.assertEqual("missing", self.outcome())

    def test_unpaid_capture_or_an_uncalled_resolver_cannot_be_certified(self):
        for filename, token in [("CoreRuleEngine.PermanentConfirmation.cs", "SourceOptionalReadyPowerCostWasPaid(behavior, item)"),
                                ("CoreRuleEngine.cs", "TryResolveSourceUnitOptionalReadyPower(\n                state,")]:
            with self.subTest(filename=filename):
                (self.engine / filename).write_text(self.code[filename].replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())
                (self.engine / filename).write_text(self.code[filename])

    def test_capturing_a_condition_without_a_live_independent_skill_producer_is_missing(self):
        for filename, token in [("CoreRuleEngine.cs", "PendingPermanentPlayAbility(state, stackItem, playerZones, cardObjects, events)"),
                                ("CoreRuleEngine.PermanentConfirmation.cs", "SourceConfirmed = true"),
                                ("CoreRuleEngine.PermanentConfirmation.cs", "Append(ability)")]:
            with self.subTest(filename=filename, token=token):
                (self.engine / filename).write_text(self.code[filename].replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())
                (self.engine / filename).write_text(self.code[filename])

    def test_same_physical_id_is_insufficient_without_generation_face_field_and_receipt(self):
        for token in ["!stackItem.SourceConfirmed", "PlayInstructionSource(state, stackItem)",
                      "sourceState.ObjectGeneration != source.Generation", "!IsFaceUpNonStandbyUnit(sourceState)",
                      "!IsObjectOnField(playerZones, source.ObjectId)"]:
            with self.subTest(token=token):
                (self.engine / "CoreRuleEngine.cs").write_text(CORE.replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())

    def test_receipt_matches_the_original_play_controller_card_and_physical_source(self):
        for token in ["play.PlayId == item.StackItemId", "play.CardNo == item.CardNo",
                      "play.PlayerId == item.ControllerId", "play.Source.ObjectId == item.SourceObjectId"]:
            with self.subTest(token=token):
                (self.engine / "CoreRuleEngine.PermanentPlaySource.cs").write_text(SOURCE.replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())

    def test_paid_cost_predicate_cannot_be_replaced_by_a_generic_power_marker(self):
        for token in ["item.OptionalCosts.Any(", "TryParseSpendPowerOptionalCost(",
                      "amount == behavior.SourceReadyPowerModifierAdditionalPowerCost", "RuneTrait.Normalize(trait) == requiredTrait"]:
            with self.subTest(token=token):
                (self.engine / "CoreRuleEngine.PermanentPlaySource.cs").write_text(SOURCE.replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())

    def test_power_or_ready_tokens_in_an_unused_helper_do_not_certify_the_live_consumer(self):
        for token in ["ApplyDirectUntilEndPowerModifier(", "IsExhausted = canReady ? false : sourceState.IsExhausted",
                      "cardObjects[stackItem.SourceObjectId] = nextSourceState"]:
            with self.subTest(token=token):
                (self.engine / "CoreRuleEngine.cs").write_text(CORE.replace(token, "disabled") +
                    "\n    private static void Unused() { " + token + "; }")
                self.assertEqual("missing", self.outcome())

    def test_the_actual_field_rule_gate_and_conditional_ready_events_are_required(self):
        for token in ["var canReady = ReadyByEffectPermittedByFieldRules(playerZones, cardObjects, sourceState)",
                      "if (canReady) AddReadiedObservation(", "if (canReady) events.Add(CaptureSelfReady("]:
            with self.subTest(token=token):
                (self.engine / "CoreRuleEngine.cs").write_text(CORE.replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())

    def test_recovery_and_exact_text_scope_are_required(self):
        for filename, token in [("PermanentPlayTriggerRecoveryRules.cs", "entered.ObjectGeneration < receipt.Generation"),
                                ("MatchRecovery.cs", "PermanentPlayTriggerRecoveryRules.Valid(")]:
            with self.subTest(filename=filename):
                (self.engine / filename).write_text(self.code[filename].replace(token, "disabled"))
                self.assertEqual("missing", self.outcome())
                (self.engine / filename).write_text(self.code[filename])
        value = card(TEXT); value["behaviors"] = [self.behavior]; clause = audit.card_clauses(value)[0]
        clause["scope"] = "当我据守时，抽一张牌。"
        self.assertEqual("missing", audit.audit_clause(value, clause, audit.Sources(self.root, []))["status"])
        clause["text"] = "当你打出我时，抽一张牌。"
        self.assertIsNone(audit.paid_source_ready_might_route(value, clause, audit.Sources(self.root, [])))
