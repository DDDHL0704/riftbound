from pathlib import Path
import tempfile
import unittest

from test_effect_clause_audit import audit


class LegendTargetTriggerClauseAuditTests(unittest.TestCase):
    TEXT = "当你将一名友方单位选为目标时，你可以选择让我变为休眠状态并支付{{A}}，以此让该单位变为活跃状态。"

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.engine = self.root / "src/Riftbound.Engine"
        self.engine.mkdir(parents=True)
        self.card = dict(cardNo="TEST", cardName="测试传奇", cardCategory="legendary", cardEffect=self.TEXT,
                         legendActivationDefinitions=[dict(effectKind="READY_FRIENDLY_UNIT",
                             requiresPendingFriendlyUnitTarget=True, manaCost=1, flow="IMMEDIATE")])

    def outcome(self, constants=None):
        return audit.audit_clause(self.card, audit.split_clauses(self.card["cardEffect"])[0],
                                  audit.Sources(self.root, constants or []))

    def test_a_manual_legend_menu_does_not_implement_a_target_selection_trigger(self):
        self.assertEqual("missing", self.outcome()["status"])
        self.card["legendActivationDefinitions"][0].update(manaCost=0, powerCost=1, flow="UNIT_EFFECT")
        self.assertEqual("missing", self.outcome()["status"])

    def test_an_actual_registered_trigger_consumer_takes_precedence(self):
        (self.engine / "CoreRuleEngine.cs").write_text("if (TriggerKinds.FriendlyTargetReady == kind) ResolveTargetReady();")
        self.card["triggers"] = [dict(node=dict(kind="FRIENDLY_TARGET_READY", text=self.TEXT))]
        result = self.outcome([dict(symbol="TriggerKinds.FriendlyTargetReady", value="FRIENDLY_TARGET_READY")])
        self.assertEqual("route", result["status"])

    def test_an_unmapped_new_trigger_shape_is_kept_for_review_without_the_old_manual_route(self):
        self.card["legendActivationDefinitions"] = []
        self.assertEqual("review", self.outcome()["status"])

    def test_a_different_trigger_is_not_mistaken_for_the_target_selection_gap(self):
        self.card["cardEffect"] = "当你征服一处战场时，你可以选择支付{{1}}，以此让我变为活跃状态。"
        self.assertEqual("review", self.outcome()["status"])
