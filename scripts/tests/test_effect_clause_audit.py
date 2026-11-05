import importlib.util
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location("effect_audit", Path(__file__).parents[1] / "audit-card-effects.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


def card(text, **kwargs):
    return dict(cardNo="OGN·078/298", cardName="李青", subTitle="苦修者", cardCategory="hero_unit",
                cardEffect=text, behaviors=[], activations=[], legendActivations=[], **kwargs)


class EffectClauseAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.engine = self.root / "src/Riftbound.Engine"
        self.engine.mkdir(parents=True)
        self.sources = audit.Sources(self.root, [])

    def test_lee_sin_has_three_clauses_not_an_extra_reminder(self):
        clauses = audit.split_clauses("{{坚守}}（如果我是防守方，则{{S}}+1。）\n{{横置}}：给予我增益。（我获得一个{{S}}+1增益。）\n我可以拥有不限数量的增益。")
        self.assertEqual(["keyword", "activated", "rule"], [c["kind"] for c in clauses])
        self.assertEqual("{{横置}}：给予我增益。（我获得一个{{S}}+1增益。）", clauses[1]["text"])
        c = card("unused")
        self.assertEqual(["missing", "missing"], [audit.audit_clause(c, r, self.sources)["status"] for r in clauses[1:]])

    def test_quotes_reminders_and_modal_colons_do_not_create_source_activations(self):
        texts = ["此处的单位获得“{{横置}}：获得1经验。”", "所有友方传奇具有“{{T}}：给予一名单位在本回合内{{S}}+3。”",
                 "装配{{1}}（支付{{1}}：将此牌贴附到你控制的一名单位上。）", "从以下中选择一个：抽一张牌。"]
        for text in texts:
            with self.subTest(text=text):
                self.assertNotEqual("activated", audit.split_clauses(text)[0]["kind"])

    def test_nested_parentheses_and_quoted_granted_rule_are_not_split(self):
        self.assertEqual(["给予单位“当我进攻时，抽一张牌。让我变为活跃状态。”。", "抽一张牌。"],
                         audit.segments("给予单位“当我进攻时，抽一张牌。让我变为活跃状态。”。抽一张牌。"))
        self.assertEqual("{{狩猎}}", audit.unparenthesize("{{狩猎}}（获得经验（每次1点）。）"))

    def test_wholly_parenthesized_token_rules_are_preserved(self):
        text = "（当我被打出时，变为某张卡牌的复制体。我不会获得该卡牌的打出效果。）"
        self.assertIn("复制体", audit.unparenthesize(text))
        self.assertEqual(1, len(audit.split_clauses(text)))

    def test_continuations_keep_activation_timing(self):
        rows = audit.split_clauses("{{横置}}：对一名单位造成3点伤害。我必须位于战场中才能使用此技能。")
        self.assertEqual(["activated", "activated"], [r["kind"] for r in rows])
        self.assertEqual(rows[0]["text"], rows[1]["scope"])

    def test_modal_activation_keeps_each_mode_in_its_owners_scope(self):
        rows = audit.split_clauses("消耗我的增益：从以下中选择一个效果-\n·对战场上的一名单位造成2点伤害。\n·让我变为活跃状态。")
        self.assertEqual(["activated"] * 3, [r["kind"] for r in rows])
        self.assertEqual(rows[0]["scope"], rows[2]["scope"])

    def test_while_source_is_on_field_is_continuous_not_a_trigger(self):
        self.assertEqual("rule", audit.split_clauses("当我在场上时，你可以选择将友方单位打出到一处开放的战场。")[0]["kind"])
        self.assertEqual("static", audit.split_clauses("当我在场上时，友方单位{{S}}+1。")[0]["kind"])
        self.assertEqual("trigger", audit.split_clauses("当我移动时，抽一张牌。")[0]["kind"])

    def test_separate_reminder_does_not_become_a_second_equip_skill(self):
        rows = audit.split_clauses("{{装配}}—支付{{黄色}}，摧毁一名友方单位。\n（支付此费用：将此牌贴附到你控制的一名单位上。）")
        self.assertEqual(1, len(rows))
        self.assertEqual("keyword", rows[0]["kind"])

    def test_dedicated_parser_is_matched_to_exact_clause_not_whole_card(self):
        (self.engine / "CoreRuleEngine.cs").write_text("var permissions = StandbyParser.PreventsEnemyFaceDownPlayHere(text);")
        probes = [dict(text="对手的{{待命}}卡牌无法在此处被翻开打出。", method="StandbyParser.PreventsEnemyFaceDownPlayHere", value=True)]
        sources = audit.Sources(self.root, [], probes)
        c = card("对手的{{待命}}卡牌无法在此处被翻开打出。\n{{横置}}：抽一张牌。")
        outcomes = [audit.audit_clause(c, clause, sources)["status"] for clause in audit.split_clauses(c["cardEffect"])]
        self.assertEqual(["route", "missing"], outcomes)

    def test_borrowed_donor_support_does_not_certify_the_original_source(self):
        (self.engine / "BorrowedExhaustRules.cs").write_text("var donor = BorrowedExhaustParser.Parse(text);")
        text = "{{横置}}：给予我增益。"
        sources = audit.Sources(self.root, [], [dict(text=text, method="BorrowedExhaustParser.Parse", value=[dict(kind="BOON_SELF")])])
        self.assertEqual("missing", audit.audit_clause(card(text), audit.split_clauses(text)[0], sources)["status"])

    def test_numeric_damage_mismatch_is_not_covered_by_another_damage_field(self):
        (self.engine / "CoreRuleEngine.cs").write_text("var damage = behavior.DamageAmount;")
        sources = audit.Sources(self.root, [])
        c = card("当你打出我时，造成3点伤害。")
        c["behaviors"] = [dict(damageAmount=2)]
        self.assertEqual("review", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_printed_combat_keyword_requires_actual_object_tag_and_amount(self):
        (self.engine / "CoreRuleEngine.cs").write_text("var power = CardCombatKeywordNames.Assault;")
        sources = audit.Sources(self.root, [])
        c = card("{{强攻2}}")
        c["behaviors"] = [dict(playsSourceToBaseAsUnit=True)]
        clause = audit.split_clauses(c["cardEffect"])[0]
        self.assertEqual("missing", audit.audit_clause(c, clause, sources)["status"])
        c["behaviors"][0]["sourceUnitTags"] = "强攻"
        self.assertEqual("missing", audit.audit_clause(c, clause, sources)["status"])
        c["behaviors"][0]["sourceUnitTags"] = "强攻2"
        self.assertEqual("route", audit.audit_clause(c, clause, sources)["status"])

    def test_natural_back_row_text_needs_an_actual_keyword_on_every_printing(self):
        (self.engine / "CoreRuleEngine.cs").write_text("var order = CardCombatKeywordNames.BackRow;")
        sources = audit.Sources(self.root, [])
        c = card("我在战斗中最后承担伤害。")
        c["behaviors"] = [dict(playsSourceToBaseAsUnit=True)]
        row = audit.split_clauses(c["cardEffect"])[0]
        self.assertEqual("missing", audit.audit_clause(c, row, sources)["status"])
        c["behaviors"][0]["sourceUnitTags"] = "后排"
        self.assertEqual("route", audit.audit_clause(c, row, sources)["status"])
        alternate = dict(c, cardNo="ALT", behaviors=[dict(playsSourceToBaseAsUnit=True)])
        outcome = audit.build_report(dict(cards=[c, alternate]), sources)
        self.assertEqual({"missing": 1}, outcome["counts"])
        self.assertEqual(["ALT"], outcome["cards"][0]["clauses"][0]["affectedPrints"])

    def test_tokens_use_factory_tags_without_a_play_card_registration(self):
        (self.engine / "CoreRuleEngine.cs").write_text("var tax = CardResourceKeywordNames.Spellshield;")
        sources = audit.Sources(self.root, [])
        c = card("{{法盾}}", tokenTags=["法盾"])
        self.assertEqual("route", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_art_dedup_keeps_subtitles_different_effects_and_gameplay_stats(self):
        original = card("{{坚守}}（防守时战力增加。）")
        art = dict(original, cardNo="OGN·078a/298", cardEffect="{{坚守}}")
        self.assertEqual(audit.effect_identity(original), audit.effect_identity(art))
        for changes in ({"subTitle": "别的副标题"}, {"power": 6}, {"cardEffect": "抽一张牌。"}):
            self.assertNotEqual(audit.effect_identity(original), audit.effect_identity(dict(art, **changes)))

    def test_admission_and_parser_status_alone_never_support_a_clause(self):
        c = card("当你打出我时，抽一张牌。")
        c["behaviors"] = [dict(playsSourceToBaseAsUnit=True)]
        c["triggers"] = [dict(node=dict(kind="on-play", text=c["cardEffect"], status="implemented"), predicates=[])]
        c["effects"] = [dict(templateId="draw", status="implemented", drawCount=1)]
        self.assertEqual("review", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], self.sources)["status"])

    def test_dedicated_activation_parser_consumer_is_found_without_primary_catalog(self):
        (self.engine / "SourceBoonMightRules.cs").write_text("var kind = ActivatedAbilityKinds.ConsumeSourceBoonGrantSelfMight;")
        sources = audit.Sources(self.root, [dict(symbol="ActivatedAbilityKinds.ConsumeSourceBoonGrantSelfMight", value="BOON_MIGHT")])
        c = card("消耗我的增益：让我本回合内{{S}}+4。", activatedAbilities=[dict(costText="消耗我的增益", effectText="让我本回合内{{S}}+4", kind="BOON_MIGHT")])
        self.assertEqual("route", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_source_entry_is_insufficient_for_a_different_activation(self):
        ability = dict(abilityId="DRAW", effectKind="DRAW", manaCost=1)
        c = audit.split_clauses("支付{{4}}和{{蓝色}}{{蓝色}}{{蓝色}}{{蓝色}}，{{横置}}：获得1分。")[0]
        self.assertFalse(audit.activation_matches(ability, c))
        self.assertTrue(audit.activation_matches(dict(abilityId="SCORE", effectKind="SCORE", manaCost=4, powerCost=4, exhaustsSourceAsCost=True), c))

    def test_composite_effect_requires_every_operation(self):
        (self.engine / "CoreRuleEngine.cs").write_text("var draw = behavior.DrawCount;")
        sources = audit.Sources(self.root, [])
        c = card("抽一张牌，然后召出一枚符文。")
        c["cardCategory"] = "spell"
        c["behaviors"] = [dict(drawCount=1)]
        row = audit.split_clauses(c["cardEffect"], "spell")[0]
        self.assertEqual("review", audit.audit_clause(c, row, sources)["status"])

    def test_consumer_comments_do_not_count(self):
        (self.engine / "CoreRuleEngine.cs").write_text("// TODO: ActivatedAbilityKinds.Foo\n/* ActivatedAbilityKinds.Foo */")
        sources = audit.Sources(self.root, [dict(symbol="ActivatedAbilityKinds.Foo", value="FOO")])
        self.assertEqual([], sources.value_routes("FOO"))

    def test_all_equivalent_prints_must_have_route(self):
        (self.engine / "CoreRuleEngine.cs").write_text("var draw = behavior.DrawCount;")
        sources = audit.Sources(self.root, [])
        a = card("当你打出我时，抽一张牌。")
        a["behaviors"] = [dict(drawCount=1)]
        b = dict(a, cardNo="OGN·078a/298", behaviors=[])
        report = audit.build_report(dict(cards=[a, b]), sources)
        self.assertEqual(1, report["effectGroups"])
        self.assertEqual({"review": 1}, report["counts"])

    def test_typed_power_dictionary_is_part_of_the_activated_cost(self):
        row = audit.split_clauses("支付{{1}}和{{蓝色}}：抽一张牌。")[0]
        ability = dict(manaCost=1, powerCostByTrait={"blue": 1}, effectKind="DRAW")
        self.assertTrue(audit.activation_matches(ability, row))
        self.assertFalse(audit.activation_matches(dict(ability, powerCostByTrait={"blue": 2}), row))

    def test_legacy_complete_text_reader_does_not_cover_unrelated_sentences(self):
        (self.engine / "BoonRules.cs").write_text('var unlimited = card.CardEffect.Contains("我可以拥有不限数量的增益", StringComparison.Ordinal);')
        sources = audit.Sources(self.root, [])
        c = card("我可以拥有不限数量的增益。\n{{横置}}：给予我增益。")
        self.assertEqual(["route", "missing"], [audit.audit_clause(c, row, sources)["status"]
                                                for row in audit.split_clauses(c["cardEffect"])])

    def test_short_text_fragment_cannot_certify_the_whole_condition(self):
        (self.engine / "CoreRuleEngine.Skills.cs").write_text('var unit = card.CardEffect.Contains("我可以拥有");')
        sources = audit.Sources(self.root, [])
        c = card("我可以拥有不限数量的增益。")
        self.assertEqual("missing", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_inventory_fingerprint_changes_with_shared_rule_source(self):
        before = audit.source_fingerprint(self.root)
        (self.engine / "CoreRuleEngine.cs").write_text("changed")
        self.assertNotEqual(before, audit.source_fingerprint(self.root))


if __name__ == "__main__":
    unittest.main()
