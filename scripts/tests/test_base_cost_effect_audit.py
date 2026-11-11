"""Regressions for exact base-cost permissions and executable payment consumers."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("base_cost_audit", ROOT / "scripts/audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class BaseCostEffectAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])
        cls.records = {}
        for path in (ROOT / "scripts/audit/reviewed-clauses").glob("*.json"):
            for record in json.loads(path.read_text())["records"]:
                if "无视其费用" in record["text"] or "无视费用" in record["text"]:
                    for no in record["cardNos"]:
                        cls.records[no] = record

    def sample(self, no):
        record = self.records[no]
        base = dict(effectPlayIgnoreBaseMana=True, effectPlayIgnoreBasePower=True)
        profiles = {
            "OGN·025/298": dict(behaviors=[dict(deckPlayKind="OPPONENT_REVEAL_PLAY", effectPlayAllowsAnyCard=True)]),
            "RAD·018/167": dict(behaviors=[dict(deckPlayKind="OPPONENT_REVEAL_PLAY", effectPlayAllowsAnyCard=True)]),
            "RAD·115/167": dict(activations=[dict(powerCostByTrait={"purple": 1}, exhaustsSourceAsCost=True, requiredTargetCount=1,
                                                  simpleEffect=dict(returnsFriendlyUnitThenFreePlayUpToPower=True))]),
            "RAD·135/167": dict(behaviors=[dict(base, effectPlaySourceZone="GRAVEYARD", effectPlayMaximumCount=3,
                                                effectPlayMaximumManaCost=3, effectPlayMaximumPowerCost=1,
                                                effectPlayRequiresUnitOrEquipment=True, effectPlayOptional=True)]),
            "RAD·136/167": dict(behaviors=[dict(unitEventAbility=dict(effect="BANISH_LINKED_UNIT_HERE"), heldPlaysLinkedExile=True)],
                                legacyRoutes=dict(HeldDefinition=dict(kind="PLAY_LINKED_EXILE"))),
            "RAD·140/167": dict(behaviors=[dict(base, deckPlayKind="OWNED_FIELD_UNIT_REPLAY",
                                                effectPlayDestination="ORIGINAL_LOCATION_OR_BASE")]),
        }
        return (dict(cardNo=no, **profiles[no]),
                dict(text=record["text"], scope=record["scope"], kind="effect"))



    def test_all_six_permissions_require_their_live_base_only_payment_route(self):
        for no in ("OGN·025/298", "RAD·018/167", "RAD·115/167", "RAD·135/167", "RAD·136/167", "RAD·140/167"):
            with self.subTest(no=no):
                card, clause = self.sample(no)
                result = audit.base_cost_effect_play_route(card, clause, self.sources)
                self.assertIsNotNone(result)
                self.assertEqual("route", result["status"])
                self.assertIn("src/Riftbound.Engine/CoreRuleEngine.EffectPlay.cs", result["evidence"])

    def test_all_costs_flag_or_missing_base_component_cannot_certify_a_permission(self):
        for no in ("OGN·025/298", "RAD·018/167", "RAD·135/167", "RAD·140/167"):
            for change in (dict(effectPlayIgnoreAllCosts=True), dict(effectPlayIgnoreBaseMana=False),
                           dict(effectPlayIgnoreBasePower=False)):
                if no in ("OGN·025/298", "RAD·018/167") and "effectPlayIgnoreAllCosts" not in change:
                    continue  # This flow derives its base components from DeckPlayKind.
                with self.subTest(no=no, change=change):
                    card, clause = self.sample(no)
                    card["behaviors"][0].update(change)
                    self.assertNotEqual("route", audit.base_cost_effect_play_route(card, clause, self.sources)["status"])

    def test_removing_any_shared_payment_consumer_reopens_each_permission(self):
        for no in ("OGN·025/298", "RAD·018/167", "RAD·115/167", "RAD·135/167", "RAD·136/167", "RAD·140/167"):
            for filename in ("CoreRuleEngine.cs", "CoreRuleEngine.PlayManaCost.cs", "CoreRuleEngine.EffectPlay.cs"):
                with self.subTest(no=no, removed=filename):
                    sources = copy.copy(self.sources)
                    sources.files = dict(self.sources.files)
                    sources.files.pop("src/Riftbound.Engine/" + filename)
                    self.assertNotEqual("route", audit.base_cost_effect_play_route(*self.sample(no), sources)["status"])

    def test_removed_continuation_and_recovery_guards_are_not_a_route(self):
        mutations = (
            ("RAD·018/167", "CoreRuleEngine.DeckPlaySequence.cs", "pending.IgnoreAllCosts == expected.IgnoreAllCosts"),
            ("RAD·115/167", "CoreRuleEngine.SimpleActivatedAbilities.cs", "!pending.Optional || pending.IgnoreAllCosts"),
            ("RAD·115/167", "CoreRuleEngine.ReturnUnitPlay.cs", "DestinationPolicy: \"\", Optional: true,"),
            ("RAD·135/167", "CoreRuleEngine.EffectPlay.cs", "pending.IgnoreBasePower == behavior.EffectPlayIgnoreBasePower"),
            ("RAD·136/167", "CoreRuleEngine.LinkedUnitExile.cs", "&& !pending.IgnoreAllCosts &&"),
            ("RAD·140/167", "CoreRuleEngine.DeckPlaySequence.cs", "IgnoreAllCosts: definition.EffectPlayIgnoreAllCosts)"),
        )
        for no, filename, fragment in mutations:
            with self.subTest(no=no, fragment=fragment):
                sources = copy.copy(self.sources)
                sources.files = dict(self.sources.files)
                path = "src/Riftbound.Engine/" + filename
                self.assertIn(fragment, sources.files[path])
                sources.files[path] = sources.files[path].replace(fragment, "REMOVED_CONSUMER")
                self.assertNotEqual("route", audit.base_cost_effect_play_route(*self.sample(no), sources)["status"])

    def test_an_old_reviewed_route_cannot_recertify_a_broken_cost_waiver(self):
        card, clause = self.sample("OGN·025/298")
        card["behaviors"][0]["effectPlayIgnoreAllCosts"] = True
        self.assertEqual("missing", audit.audit_clause(card, clause, self.sources)["status"])
        card["behaviors"][0].pop("effectPlayIgnoreAllCosts")
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        sources.files.pop("src/Riftbound.Engine/CoreRuleEngine.PlayManaCost.cs")
        self.assertEqual("review", audit.audit_clause(card, clause, sources)["status"])

    def test_the_original_all_costs_constructor_defects_are_reported_as_missing(self):
        mutations = (
            ("RAD·018/167", "CoreRuleEngine.DeckPlaySequence.cs",
             'IgnoreAllCosts: definition.EffectPlayIgnoreAllCosts)',
             'IgnoreAllCosts: c.Kind == "OPPONENT_REVEAL_PLAY" || definition.EffectPlayIgnoreAllCosts)'),
            ("RAD·115/167", "CoreRuleEngine.ReturnUnitPlay.cs",
             'DestinationPolicy: "", Optional: true,', 'DestinationPolicy: "", Optional: true, IgnoreAllCosts: true,'),
            ("RAD·136/167", "CoreRuleEngine.LinkedUnitExile.cs",
             '0, "ANY", false)', '0, "ANY", false, IgnoreAllCosts: true)'),
        )
        for no, filename, before, after in mutations:
            with self.subTest(no=no):
                sources = copy.copy(self.sources)
                sources.files = dict(self.sources.files)
                path = "src/Riftbound.Engine/" + filename
                self.assertIn(before, sources.files[path])
                sources.files[path] = sources.files[path].replace(before, after)
                self.assertEqual("missing", audit.base_cost_effect_play_route(*self.sample(no), sources)["status"])

    def test_different_scope_and_unreviewed_ignore_cost_sentences_stay_unmatched(self):
        card, clause = self.sample("RAD·115/167")
        self.assertIsNone(audit.base_cost_effect_play_route(card, dict(clause, scope="当我进攻时"), self.sources))
        self.assertIsNone(audit.base_cost_effect_play_route(card, dict(clause, text="打出一张牌，无视其费用。"), self.sources))

    def test_wrong_power_cost_limit_location_and_trigger_profile_are_not_certified(self):
        for no, mutate in (
            ("RAD·115/167", lambda c: c["activations"][0].update(powerCostByTrait={"red": 1})),
            ("RAD·115/167", lambda c: c["activations"][0].update(requiredTargetCount=0)),
            ("RAD·135/167", lambda c: c["behaviors"][0].update(effectPlayMaximumManaCost=4)),
            ("RAD·136/167", lambda c: c["behaviors"][0]["unitEventAbility"].update(effect="DRAW")),
            ("RAD·140/167", lambda c: c["behaviors"][0].update(effectPlayDestination="ANY")),
        ):
            with self.subTest(no=no):
                card, clause = self.sample(no)
                mutate(card)
                self.assertNotEqual("route", audit.base_cost_effect_play_route(card, clause, self.sources)["status"])
