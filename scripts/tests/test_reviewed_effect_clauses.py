import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location("reviewed_audit", Path(__file__).parents[1] / "audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class ReviewedEffectClauseTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.engine = self.root / "src/Riftbound.Engine/CoreRuleEngine.cs"
        self.engine.parent.mkdir(parents=True)
        self.engine.write_text("CaptureEnemyStuns(); GrantFriendlyBoon();")
        self.test = self.root / "tests/Riftbound.ConformanceTests/StunTests.cs"
        self.test.parent.mkdir(parents=True)
        self.test.write_text("[Fact]\npublic async Task EnemyStunGrantsFriendlyBoon() { AssertBoon(); }")
        self.document = self.root / "scripts/audit/reviewed-clauses/stun.json"
        self.document.parent.mkdir(parents=True)
        self.text = "每当你眩晕任意数量的敌方单位时，给予一名友方单位增益。"
        self.card = dict(cardNo="OGN·261/298", cardName="曙光女神", cardCategory="legend",
                         cardEffect=self.text, behaviors=[], legacyRoutes={"stunBoon": True})
        self.clause = audit.split_clauses(self.text, "legend")[0]
        self.record = dict(cardNos=["OGN·261/298"], text=self.text, scope=self.text,
                           status="route", reason="分组敌方眩晕后给予友方增益", rules="核心383.3、702–705",
                           runtime={"legacyRoutes": {"stunBoon": True}},
                           evidence=[dict(path=str(self.engine.relative_to(self.root)),
                                          contains=["CaptureEnemyStuns();", "GrantFriendlyBoon();"])],
                           tests=[dict(path=str(self.test.relative_to(self.root)),
                                       methods=["EnemyStunGrantsFriendlyBoon"])])
        self.write()

    def write(self):
        self.document.write_text(json.dumps(dict(schemaVersion=1, records=[self.record]), ensure_ascii=False))

    def outcome(self, card=None, clause=None):
        return audit.audit_clause(card or self.card, clause or self.clause, audit.Sources(self.root, []))

    def test_review_needs_both_runtime_consumers_and_named_rule_test(self):
        result = self.outcome()
        self.assertEqual("route", result["status"])
        self.assertIn("核心383.3", result["rules"])
        self.assertIn(str(self.test.relative_to(self.root)) + "::EnemyStunGrantsFriendlyBoon", result["evidence"])
        self.engine.write_text("CaptureEnemyStuns();")
        self.assertEqual("review", self.outcome()["status"])

    def test_code_comments_cannot_replace_executable_consumers(self):
        self.engine.write_text("CaptureEnemyStuns(); // GrantFriendlyBoon();")
        self.assertEqual("review", self.outcome()["status"])
        self.engine.write_text("CaptureEnemyStuns(); /* GrantFriendlyBoon(); */")
        self.assertEqual("review", self.outcome()["status"])

    def test_missing_or_commented_out_test_reopens_review(self):
        original = self.test.read_text()
        self.test.write_text("/* " + original + " */")
        self.assertEqual("review", self.outcome()["status"])
        self.test.unlink()
        self.assertEqual("review", self.outcome()["status"])

    def test_a_public_helper_with_the_old_test_name_is_not_test_evidence(self):
        self.test.write_text("public async Task EnemyStunGrantsFriendlyBoon() { AssertBoon(); }")
        self.assertEqual("review", self.outcome()["status"])
        self.test.write_text('[Theory, MemberData(nameof(Cases))]\npublic Task EnemyStunGrantsFriendlyBoon() { return Run(); }')
        self.assertEqual("route", self.outcome()["status"])

    def test_runtime_boolean_cannot_be_replaced_by_a_number_or_unrelated_field(self):
        for routes in ({}, {"stunBoon": False}, {"stunBoon": 1}, {"otherBoon": True}):
            with self.subTest(routes=routes):
                self.assertEqual("review", self.outcome(dict(self.card, legacyRoutes=routes))["status"])

    def test_exact_print_clause_scope_and_location_are_independent_requirements(self):
        self.assertEqual("review", self.outcome(dict(self.card, cardNo="OGN·306/298"))["status"])
        self.assertEqual("review", self.outcome(clause=dict(self.clause, text=self.text.replace("敌方", "友方")))["status"])
        self.assertEqual("review", self.outcome(clause=dict(self.clause, scope="当我进攻时，抽一张牌。"))["status"])
        result = audit.reviewed_clause_route(self.card, dict(self.clause, location="armament"), audit.Sources(self.root, []))
        self.assertIsNone(result)

    def test_equivalent_art_still_needs_its_own_review_and_evidence(self):
        other = dict(self.card, cardNo="OGN·306/298")
        report = audit.build_report(dict(cards=[self.card, other]), audit.Sources(self.root, []))
        self.assertEqual({"review": 1}, report["counts"])
        self.record["cardNos"].append(other["cardNo"])
        self.write()
        report = audit.build_report(dict(cards=[self.card, other]), audit.Sources(self.root, []))
        self.assertEqual({"route": 1}, report["counts"])

    def test_new_concrete_implementation_supersedes_a_recorded_gap(self):
        self.record.update(status="missing", reason="尚无抽牌结算", tests=[], runtime={},
                           text="当你打出我时，抽一张牌。", scope="当你打出我时，抽一张牌。")
        self.write()
        card = dict(self.card, cardEffect=self.record["text"], cardCategory="unit", behaviors=[])
        clause = audit.split_clauses(card["cardEffect"])[0]
        self.assertEqual("missing", self.outcome(card, clause)["status"])
        self.engine.write_text(self.engine.read_text() + " var count = behavior.DrawCount;")
        card["behaviors"] = [dict(drawCount=1)]
        self.assertEqual("route", self.outcome(card, clause)["status"])

    def test_empty_lists_are_absence_checks_and_list_entries_cannot_reuse_one_node(self):
        self.assertTrue(audit.inventory_contains([], []))
        self.assertFalse(audit.inventory_contains([{}], []))
        self.assertFalse(audit.inventory_contains([dict(kind="DRAW")], [dict(kind="DRAW"), dict(kind="DRAW")]))
        modes = [dict(id="A", amount=1), dict(id="B", amount=2)]
        self.assertTrue(audit.inventory_contains(modes, [dict(id="B", amount=2)]))
        self.assertFalse(audit.inventory_contains(modes, [dict(id="B", amount=1)]))

    def test_duplicate_reviews_and_evidence_free_certification_are_errors(self):
        cases = [dict(self.record, tests=[]), dict(self.record, rules=""),
                 dict(self.record, evidence=[]), dict(self.record, evidence=[dict(path="src/Riftbound.Engine/CardBehaviorRegistry.cs", contains=["entry"])]),
                 dict(self.record, tests=[dict(path=str(self.test.relative_to(self.root)), methods=[])])]
        for record in cases:
            with self.subTest(record=record):
                self.document.write_text(json.dumps(dict(schemaVersion=1, records=[record])))
                with self.assertRaises(ValueError):
                    audit.Sources(self.root, [])
        self.document.write_text(json.dumps(dict(schemaVersion=1, records=[self.record, copy.deepcopy(self.record)])))
        with self.assertRaises(ValueError):
            audit.Sources(self.root, [])

    def test_explicit_rule_question_keeps_code_evidence_without_claiming_implementation(self):
        self.record.update(status="review", reason="待确认（规则）：已知触发时机；控制效果撤销次序尚无原文。", tests=[])
        self.write()
        result = self.outcome()
        self.assertEqual("review", result["status"])
        self.assertIn("待确认（规则）", result["reason"])
        self.assertIn("核心383.3", result["rules"])
        self.assertIn(str(self.engine.relative_to(self.root)), result["evidence"])

    def test_rule_question_still_requires_explicit_label_official_rules_and_code_evidence(self):
        self.record.update(status="review", tests=[])
        cases = [dict(self.record, reason="规则不明"),
                 dict(self.record, reason="待确认（规则）：控制权疑点", rules=""),
                 dict(self.record, reason="待确认（规则）：控制权疑点", evidence=[])]
        for record in cases:
            with self.subTest(record=record):
                self.document.write_text(json.dumps(dict(schemaVersion=1, records=[record])))
                with self.assertRaises(ValueError):
                    audit.Sources(self.root, [])

    def test_review_data_changes_invalidate_the_cached_runtime_inventory(self):
        before = audit.source_fingerprint(self.root)
        self.record["reason"] += "（复查）"
        self.write()
        self.assertNotEqual(before, audit.source_fingerprint(self.root))


if __name__ == "__main__":
    unittest.main()
