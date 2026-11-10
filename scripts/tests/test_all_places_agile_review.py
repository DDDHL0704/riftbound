import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("agile_review_audit", ROOT / "scripts/audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class AllPlacesAgileReviewTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        records = json.loads((ROOT / "scripts/audit/reviewed-clauses/sfd-heroes.json").read_text())["records"]
        self.record = next(record for record in records if "SFD·054/221" in record["cardNos"])
        for entry in self.record["evidence"] + self.record["tests"]:
            target = self.root / entry["path"]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text((ROOT / entry["path"]).read_text())
        review = self.root / "scripts/audit/reviewed-clauses/jax.json"
        review.parent.mkdir(parents=True)
        review.write_text(json.dumps(dict(schemaVersion=1, records=[self.record]), ensure_ascii=False))
        self.card = dict(cardNo="SFD·054/221", behaviors=copy.deepcopy(self.record["runtime"]["behaviors"]))
        self.clause = dict(text=self.record["text"], scope=self.record["scope"], location="upper")

    def outcome(self):
        return audit.reviewed_clause_route(self.card, self.clause, audit.Sources(self.root, []))

    def test_effective_clause_requires_the_reviewed_all_places_execution_and_native_tests(self):
        self.assertEqual("route", self.outcome()["status"])
        self.assertIn("260114", self.outcome()["rules"])
        self.assertIn("各处", self.clause["text"])
        acquired = json.loads((ROOT / "data/official/card-catalog.zh-CN.json").read_text())
        raw = next(card for card in acquired["cards"] if card["cardNo"] == self.card["cardNo"])
        self.assertIn("你手牌中的每一件武装", raw["cardEffect"])
        test = self.root / self.record["tests"][0]["path"]
        test.write_text(test.read_text().replace("public async Task BothPrintingsGrantReactionAndAnIndependentPostEntryAttachment",
                                               "private async Task RemovedAttachmentTest"))
        self.assertEqual("review", self.outcome()["status"])

    def test_removing_any_actual_keyword_permission_capture_identity_or_history_consumer_reopens_review(self):
        cases = [
            ("CoreRuleEngine.RuleDerivedKeywords.cs", "AllPlacesEquipmentKeywords(state)"),
            ("CoreRuleEngine.EquipmentKeywordGrants.cs", "CanPlayDuringPriority = true"),
            ("CoreRuleEngine.UnitEvents.cs", ".Concat(CaptureGrantedAgilePlay(zones, cards, entered, player))"),
            ("CoreRuleEngine.AgilePlay.cs", "gear.ObjectGeneration != item.UnitEvent!.SourceGeneration"),
            ("CoreRuleEngine.ObservedUnitEvents.cs", "context.AgilePlayGrant"),
            ("MatchRecovery.ObservedUnitEvents.cs", "expected.ExceptWith(retired)"),
        ]
        for name, fragment in cases:
            with self.subTest(consumer=name):
                file = self.root / "src/Riftbound.Engine" / name
                original = file.read_text()
                self.assertIn(fragment, original)
                file.write_text(original.replace(fragment, "removed_consumer"))
                self.assertEqual("review", self.outcome()["status"])
                file.write_text(original)

    def test_an_unrelated_or_wrong_runtime_hero_cannot_reuse_the_review(self):
        self.card["behaviors"][0]["sourceUnitPower"] = 99
        self.assertEqual("review", self.outcome()["status"])
        self.card["cardNo"] = "SFD·119/221"
        self.assertIsNone(self.outcome())

    def test_an_unused_selector_decoy_cannot_replace_the_actual_dispatch(self):
        file = self.root / "src/Riftbound.Engine/CoreRuleEngine.UnitEvents.cs"
        code = file.read_text()
        branch = "context.Timing == AgilePlayTiming ? AgilePlaySpec"
        self.assertIn(branch, code)
        file.write_text(code.replace(branch, "false ? AgilePlaySpec") +
            "\n    private static UnitEventAbilitySpec? UnusedAgileSelectorDecoy(UnitEventContext context)\n"
            "        => " + branch + " : null;\n")
        self.assertEqual("review", self.outcome()["status"])

    def test_invalid_or_missing_member_evidence_does_not_fall_back_to_the_whole_file(self):
        record = copy.deepcopy(self.record)
        selector = next(entry for entry in record["evidence"]
                        if entry.get("member") == "UnitEventSpecForContext")
        review = self.root / "scripts/audit/reviewed-clauses/jax.json"
        for member in ["RemovedSelector", "", None, True]:
            selector["member"] = member
            review.write_text(json.dumps(dict(schemaVersion=1, records=[record]), ensure_ascii=False))
            with self.subTest(member=member):
                self.assertEqual("review", self.outcome()["status"])


if __name__ == "__main__":
    unittest.main()
