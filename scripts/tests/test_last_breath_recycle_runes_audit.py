import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("last_breath_recycle_audit", ROOT / "scripts/audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class LastBreathRecycleRunesAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        text = "{{绝念}}—回收我，以此让你的所有符文变为活跃状态。（当我被摧毁后，发动此效果。）"
        node = dict(kind="UNIT_LAST_BREATH_RECYCLE_SELF_READY_ALL_RUNES", timing="UNIT_DESTROYED",
                    text=text, targetScope="SOURCE_UNIT", recycleCount=1,
                    recycleSourceZone="GRAVEYARD", recycleDestinationZone="MAIN_DECK")
        return dict(cardNo="OGN·110/298", cardName="艾克", cardCategory="hero_unit", cardEffect=text,
                    behaviors=[dict(playsSourceToBaseAsUnit=True)], triggers=[dict(node=node,
                        predicates=["UnitDestroyedTriggerSpecRules.IsLastBreathRecycleSelfReadyAllRunesTrigger"])])

    def status(self, card=None, sources=None):
        card = card or self.card()
        rows = audit.card_clauses(card)
        self.assertEqual(1, len(rows))
        return audit.audit_clause(card, rows[0], sources or self.sources)["status"]

    def changed(self, file, token, decoy=False):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        path = "src/Riftbound.Engine/" + file
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, "REMOVED_BOUND_CONSUMER")
        if decoy:
            sources.files[path] += "\n    private static void UnusedDecoy() { " + token + " }\n"
        return sources

    def test_the_real_cost_capture_dispatch_readiness_and_history_are_all_required(self):
        self.assertEqual("route", self.status())

    def test_a_registration_without_the_runtime_trigger_is_missing(self):
        card = self.card()
        card["triggers"] = []
        self.assertEqual("missing", self.status(card))

    def test_the_exact_recycle_source_count_destination_and_kind_must_match(self):
        for key, value in [("kind", "SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1"),
                           ("timing", "UNIT_ENTERED"), ("targetScope", "FRIENDLY_UNIT"),
                           ("recycleCount", 2), ("recycleSourceZone", "HAND"),
                           ("recycleDestinationZone", "BASE"), ("runeReadyCount", 0), ("runeReadyCount", 1), ("optional", True)]:
            with self.subTest(key=key):
                card = self.card()
                card["triggers"][0]["node"][key] = value
                self.assertEqual("missing", self.status(card))

    def test_the_runtime_shape_predicate_cannot_be_replaced_by_a_generic_last_breath(self):
        card = self.card()
        card["triggers"][0]["predicates"] = ["UnitDestroyedTriggerSpecRules.IsLastBreathCallRuneOneTrigger"]
        self.assertEqual("missing", self.status(card))

    def test_a_spell_or_nested_different_scope_is_not_certified(self):
        card = self.card()
        card["cardCategory"] = "spell"
        self.assertEqual("missing", self.status(card))
        card = self.card()
        clause = audit.card_clauses(card)[0]
        clause["scope"] = "打出一名单位，然后" + clause["scope"]
        self.assertEqual("missing", audit.audit_clause(card, clause, self.sources)["status"])

    def test_cost_payment_and_the_graveyard_generation_cannot_be_bypassed(self):
        for file, token in [
            ("CoreRuleEngine.TriggerCosts.cs", "HasLastBreathSourceRecycleCost(item) ? new(RecycleSelf: true)"),
            ("CoreRuleEngine.TriggerCosts.cs", "RecycleSelf: false"),
            ("CoreRuleEngine.TriggerCosts.cs", "item.LastBreathRuneCall!.Source.Generation + 1"),
            ("CoreRuleEngine.TriggerCosts.cs", "PayLastBreathSourceRecycleCost(state, item, selected)"),
            ("CoreRuleEngine.TriggerRecycleCost.cs", "source.ObjectGeneration != TriggerCostSourceGeneration(item)"),
            ("CoreRuleEngine.TriggerRecycleCost.cs", "IsTokenObject(source)"),
            ("CoreRuleEngine.TriggerRecycleCost.cs", "owner.Graveyard.Contains(source.ObjectId"),
            ("CoreRuleEngine.TriggerRecycleCost.cs", "recyclingPlayerId: item.ControllerId"),
        ]:
            with self.subTest(token=token):
                self.assertEqual("missing", self.status(sources=self.changed(file, token)))

    def test_unused_readiness_code_does_not_replace_the_bound_dispatch_and_consumer(self):
        for file, token in [
            ("CoreRuleEngine.cs", "return ResolveUnitLastBreathCallRuneStackItem(state, stackItem)"),
            ("CoreRuleEngine.cs", "return ResolveLastBreathReadyAllRunes(state, stackItem)"),
            ("CoreRuleEngine.LastBreathRuneCall.cs", "ApplyReadyState(cards[id], behavior, visible, id"),
            ("CoreRuleEngine.LastBreathRuneCall.cs", "EffectiveFieldControllerId(zones, id, rune) == item.ControllerId"),
            ("CoreRuleEngine.LastBreathRuneCall.cs", "CardObjectTags.RuneCard"),
        ]:
            with self.subTest(token=token):
                self.assertEqual("missing", self.status(sources=self.changed(file, token, decoy=True)))

    def test_an_unpaid_or_forged_receipt_or_unused_history_validator_reopens_the_clause(self):
        for file, token in [
            ("CoreRuleEngine.TriggerCosts.cs", "receipt.SourceWasRecycled != cost.RecycleSelf"),
            ("CoreRuleEngine.TriggerCosts.cs", "recycled.ObjectGeneration <= receipt.Source.Generation"),
            ("MatchRecovery.cs", "ValidateLastBreathRecycleCostHistory(authoritativeState, commands, events, errors)"),
            ("MatchRecovery.LastBreathRecycleCost.cs", "command.Accepted"),
            ("MatchRecovery.LastBreathRecycleCost.cs", "command.CompletedTick == recycled[0].Tick"),
            ("MatchRecovery.LastBreathRecycleCost.cs", "choices[0].Trim() == receipt.Source.ObjectId"),
        ]:
            with self.subTest(token=token):
                self.assertEqual("missing", self.status(sources=self.changed(file, token, decoy=True)))

    def test_the_same_clause_cannot_certify_a_rune_call_or_another_cards_keyword(self):
        card = self.card()
        card["triggers"][0]["node"]["text"] = "{{绝念}}—召出一枚休眠的符文。"
        self.assertEqual("missing", self.status(card))

    def test_public_confirmation_and_readiness_keep_the_hidden_source_masked(self):
        for file, token in [
            ("CoreRuleEngine.TriggerCosts.cs", "HasLastBreathSourceRecycleCost(item) ? ResolutionResult.VisibleObjectIdForViewer("),
            ("CoreRuleEngine.LastBreathRuneCall.cs", "var visible = item with { SourceObjectId = ResolutionResult.VisibleObjectIdForViewer("),
        ]:
            with self.subTest(token=token):
                self.assertEqual("missing", self.status(sources=self.changed(file, token)))

    def test_a_parser_helper_without_its_real_parse_dispatch_does_not_certify_the_clause(self):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        path = "src/Riftbound.CardCatalog/RuleTextParsers.cs"
        code = (ROOT / path).read_text()
        token = "TryParseUnitLastBreathRecycleSelfReadyAllRunes(text, out var recycledSourceRunes)"
        self.assertIn(token, code)
        sources.files[path] = code.replace(token, "REMOVED_DISPATCH") + "\n    private static void UnusedParser() { " + token + " }\n"
        self.assertEqual("missing", self.status(sources=sources))

    def test_unused_helpers_cannot_replace_the_real_confirmation_and_command_dispatch(self):
        for file, token in [
            ("CoreRuleEngine.FieldTriggers.cs", "NeedsTriggerCostConfirmation(i)"),
            ("CoreRuleEngine.FieldTriggers.cs", "state.StackItems.FirstOrDefault(NeedsTriggerConfirmation)"),
            ("CoreRuleEngine.FieldTriggers.cs", "if (NeedsTriggerCostConfirmation(item)) return PrepareTriggerCostConfirmation(result, item)"),
            ("CoreRuleEngine.TriggerCosts.cs", "TriggerCostChoices(state, item).Length < Math.Max(1, cost.DestroyPermanents)"),
            ("CoreRuleEngine.TriggerCosts.cs", "return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item))"),
            ("CoreRuleEngine.TriggerCosts.cs", "PendingCardChoice = cost.Power + cost.Mana == 0 ? TriggerCostChoice(state, item) : null"),
            ("CoreRuleEngine.cs", "if (pendingChoice.ChoiceWindow is TriggerCostWindow or OptionalTriggerWindow) return ResolveTriggerCostChoice(state, pendingChoice, submittedObjectIds)"),
        ]:
            with self.subTest(token=token):
                self.assertEqual("missing", self.status(sources=self.changed(file, token, decoy=True)))

    def test_unused_state_writes_cannot_replace_readiness_or_removal_from_the_graveyard(self):
        for token in [
            "ReadyByEffectPermittedByFieldRules(playerZones, cardObjects, targetState)",
            "IsExhausted = false",
            "CaptureReadiedObject(playerZones, cardObjects, targetState)",
            "return nextTargetState",
            ".Where(objectId => !recycledSet.Contains(objectId))",
        ]:
            with self.subTest(token=token):
                self.assertEqual("missing", self.status(sources=self.changed("CoreRuleEngine.cs", token, decoy=True)))


if __name__ == "__main__":
    unittest.main()
