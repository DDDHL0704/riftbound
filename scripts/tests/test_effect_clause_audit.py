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
        self.assertFalse(audit.activation_matches(dict(abilityId="SCORE", effectKind="SCORE", manaCost=4, powerCost=4, exhaustsSourceAsCost=True), c))
        self.assertTrue(audit.activation_matches(dict(abilityId="SCORE", effectKind="SCORE", manaCost=4, powerCostByTrait={"blue": 4}, exhaustsSourceAsCost=True), c))

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

    def test_native_bridge_needs_an_actual_native_entry_matching_the_printed_skill(self):
        (self.engine / "CoreRuleEngine.BorrowedExhaust.cs").write_text('if (spec.Kind == "DAMAGE_UNIT") ResolveDamage();')
        text = "{{横置}}：对战场上的一名单位造成2点伤害。"
        parsed = dict(kind="DAMAGE_UNIT", targets=1, damage=2, targetScope="BATTLEFIELD_UNIT")
        sources = audit.Sources(self.root, [], [dict(text=text, method="BorrowedExhaustParser.Parse", value=[parsed])])
        c = card(text, nativeActivations=[parsed])
        row = audit.split_clauses(text)[0]
        self.assertEqual("route", audit.audit_clause(c, row, sources)["status"])
        c["nativeActivations"] = [dict(parsed, damage=3)]
        self.assertEqual("missing", audit.audit_clause(c, row, sources)["status"])
        c["nativeActivations"] = [dict(parsed, recycledCardCostCount=3)]
        self.assertEqual("missing", audit.audit_clause(c, row, sources)["status"])
        c["nativeActivations"] = [dict(parsed, empoweredMight=0)]
        self.assertEqual("missing", audit.audit_clause(c, row, sources)["status"])
        c["nativeActivations"] = [dict(parsed, index=7, mana=0, swift=False, typedPower={})]
        self.assertEqual("route", audit.audit_clause(c, row, sources)["status"])

    def test_battlefield_limitation_requires_a_matching_executable_skill(self):
        (self.engine / "CoreRuleEngine.cs").write_text("if (ability.RequiresBattlefieldSource) CheckSource();")
        sources = audit.Sources(self.root, [])
        c = card("我必须位于战场中才能使用此技能。")
        row = audit.split_clauses(c["cardEffect"])[0]
        self.assertEqual("review", audit.audit_clause(c, row, sources)["status"])
        c["activations"] = [dict(requiresBattlefieldSource=True)]
        self.assertEqual("route", audit.audit_clause(c, row, sources)["status"])

    def test_native_might_route_can_use_the_shared_resolvers_ordinary_branch(self):
        path = self.engine / "CoreRuleEngine.BorrowedExhaust.cs"
        path.write_text('var amount = spec.Might; ApplyPowerModifier(state, amount);')
        text = "{{迅捷>}} 摧毁此牌，{{横置}}：让一名单位在本回合内{{S}}+2。"
        parsed = dict(kind="MIGHT_UNIT", targets=1, might=2, swift=True, destroysSourceAsCost=True, targetScope="ANY_UNIT")
        probe = dict(text=text, method="BorrowedExhaustParser.Parse", value=[parsed])
        c = card(text, nativeActivations=[parsed])
        row = audit.split_clauses(text)[0]
        self.assertEqual("route", audit.audit_clause(c, row, audit.Sources(self.root, [], [probe]))["status"])
        path.write_text('var amount = spec.Might;')
        self.assertEqual("missing", audit.audit_clause(c, row, audit.Sources(self.root, [], [probe]))["status"])

    def test_an_equipment_play_draw_does_not_certify_its_separate_exhaust_draw(self):
        (self.engine / "CoreRuleEngine.Equipment.cs").write_text('var draw = behavior.DrawCount;')
        sources = audit.Sources(self.root, [])
        c = card("当你打出此牌时，抽一张牌。\n支付{{1}}和{{绿色}}，{{横置}}，摧毁此牌：抽一张牌。")
        c["cardCategory"] = "equipment"
        c["behaviors"] = [dict(playsSourceToBaseAsEquipment=True, drawCount=1)]
        self.assertEqual(["route", "missing"], [audit.audit_clause(c, row, sources)["status"]
                                               for row in audit.split_clauses(c["cardEffect"], "equipment")])

    def test_dynamic_runtime_event_catalog_does_not_require_a_hand_written_behavior(self):
        (self.engine / "CoreRuleEngine.UnitEvents.cs").write_text('if (spec.Timing == "FRIENDLY_UNIT_PLAYED" && spec.Effect == "PLAYED_UNIT_CALL_RUNES") CallRunes();')
        sources = audit.Sources(self.root, [])
        c = card("当你打出一名{{强力}}单位时，你可以选择让我变为休眠状态，以此召出一枚休眠的符文。",
                 unitEventAbility=dict(timing="FRIENDLY_UNIT_PLAYED", effect="PLAYED_UNIT_CALL_RUNES", amount=1))
        self.assertEqual("route", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_leave_draw_and_rune_call_require_both_printed_counts(self):
        text = '当此牌离场时，抽一张牌并召出一枚休眠的符文。'
        event = dict(effect='DRAW', timing='LEAVE', amount=1, runeCallCount=1)
        self.assertTrue(audit.event_operations_match(event, text))
        self.assertFalse(audit.event_operations_match(dict(event, amount=2), text))
        self.assertFalse(audit.event_operations_match(dict(event, runeCallCount=0), text))
        self.assertFalse(audit.event_operations_match(dict(event, runeCallCount=2), text))

    def test_unrecognized_runtime_event_effects_stay_in_review(self):
        (self.engine / "CoreRuleEngine.UnitEvents.cs").write_text(
            'if (spec.Timing == "BECAME_EXHAUSTED" && spec.Effect == "FUTURE_EFFECT") ResolveFutureEffect();')
        sources = audit.Sources(self.root, [])
        for instruction in ("抽一张牌", "做一种全新的事情"):
            c = card("当我变为休眠状态时，" + instruction + "。",
                     unitEventAbility=dict(timing="BECAME_EXHAUSTED", effect="FUTURE_EFFECT", amount=1))
            row = audit.split_clauses(c["cardEffect"])[0]
            self.assertEqual("review", audit.audit_clause(c, row, sources)["status"])

    def test_matching_event_timing_does_not_certify_unrelated_partial_or_wrong_count_effects(self):
        (self.engine / "CoreRuleEngine.UnitEvents.cs").write_text('if (spec.Timing == "BECAME_EXHAUSTED" && spec.Effect == "DRAW") ApplyDrawToPlayer();')
        sources = audit.Sources(self.root, [])
        for instruction, expected in [("抽一张牌", "route"), ("抽两张牌", "review"),
                                      ("对一名单位造成2点伤害", "review"),
                                      ("抽一张牌，然后召出一枚符文", "review")]:
            c = card("当我变为休眠状态时，" + instruction + "。")
            c["behaviors"] = [dict(unitEventAbility=dict(timing="BECAME_EXHAUSTED", effect="DRAW", amount=1))]
            self.assertEqual(expected, audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_unqualified_movement_is_not_covered_by_a_fixed_base_destination(self):
        (self.engine / "CoreRuleEngine.Movement.cs").write_text(
            "behavior.MovesTargetToBase; behavior.MovesTargetsToChosenLocation;")
        sources = audit.Sources(self.root, [])
        cases = [
            ("移动一名敌方单位。", dict(movesTargetToBase=True, targetScope="ENEMY_BATTLEFIELD_UNIT"), "missing"),
            ("移动一名友方单位。", dict(movesTargetToBase=True, targetScope="FRIENDLY_UNIT"), "missing"),
            ("移动一名敌方单位。", dict(movesTargetsToChosenLocation=True, targetScope="ENEMY_UNIT"), "route"),
            ("将一名敌方单位移动到其所属的基地。", dict(movesTargetToBase=True, targetScope="ENEMY_BATTLEFIELD_UNIT"), "route"),
        ]
        for text, behavior, expected in cases:
            with self.subTest(text=text, behavior=behavior):
                c = card(text); c["cardCategory"] = "spell"; c["behaviors"] = [behavior]
                self.assertEqual(expected, audit.audit_clause(c, audit.card_clauses(c)[0], sources)["status"])

    def test_counted_passives_require_the_printed_count_and_the_shared_consumer(self):
        (self.engine / "CoreRuleEngine.Counted.cs").write_text("behavior.DamageImmunityAfterMoves; UnitMovementRules.CannotTakeDamage(card); behavior.MovementScore; ResolveMovementScore(item); behavior.FriendlyLastBreathAdditionalTriggers; CaptureLastBreathRepetitions(state);")
        sources = audit.Sources(self.root, [])
        cases = [
            ("在本回合内，如果我移动了两次，则我免疫伤害。", dict(damageImmunityAfterMoves=2), dict(damageImmunityAfterMoves=3)),
            ("当我在一个回合内进行了第三次移动，你获得1分。", dict(movementScore=dict(ordinal=3, scoreAmount=1)), dict(movementScore=dict(ordinal=2, scoreAmount=1))),
            ("你的所有{{绝念}}效果可以额外触发一次。", dict(friendlyLastBreathAdditionalTriggers=1), dict(friendlyLastBreathAdditionalTriggersHere=1))]
        for text, correct, wrong in cases:
            c = card(text)
            c["behaviors"] = [correct]
            row = audit.split_clauses(text)[0]
            self.assertEqual("route", audit.audit_clause(c, row, sources)["status"])
            c["behaviors"] = [wrong]
            self.assertEqual("review", audit.audit_clause(c, row, sources)["status"])
        self.assertEqual("trigger", audit.split_clauses(cases[1][0])[0]["kind"])

    def test_reveal_count_damage_and_recycle_need_the_complete_defend_spec(self):
        (self.engine / "CoreRuleEngine.StandbyDefend.cs").write_text("CaptureStandbyDefendTriggers(state); ResolveStandbyDefendTrigger(state); behavior.DefendTriggerMainDeckLookCount; behavior.DefendTriggerDamagePerCountedCard; behavior.DefendTriggerRecyclesLookedMainDeckCards; behavior.DefendTriggerMaxTargetCount; behavior.DefendTriggerTargetScope; behavior.DefendTriggerCountedTag;")
        targets = self.engine / "CoreRuleEngine.FieldTriggers.cs"
        targets.write_text('if (context.Kind == "STANDBY_DEFEND") return c.ControllerId != item.ControllerId && IsCombatMightTargetHere(state, item, id);')
        sources = audit.Sources(self.root, [])
        text = "当我防守时，选择一名此处的敌方单位，展示你主牌堆顶部的五张牌。当中每有一张带有{{待命}}技能的卡牌，便对该单位造成1点伤害，然后将展示的牌回收。\n当我进攻时，抽一张牌。"
        c = card(text)
        c["behaviors"] = [dict(defendTriggerMaxTargetCount=1, defendTriggerTargetScope="ENEMY_UNIT_AT_SOURCE_BATTLEFIELD",
                               defendTriggerMainDeckLookCount=5, defendTriggerCountedTag="待命",
                               defendTriggerDamagePerCountedCard=1, defendTriggerRecyclesLookedMainDeckCards=True)]
        rows = audit.split_clauses(text)
        self.assertEqual(["route", "route", "review"], [audit.audit_clause(c, row, sources)["status"] for row in rows])
        c["behaviors"][0]["defendTriggerRecyclesLookedMainDeckCards"] = False
        self.assertEqual(["review", "review", "review"], [audit.audit_clause(c, row, sources)["status"] for row in rows])
        c["behaviors"][0]["defendTriggerRecyclesLookedMainDeckCards"] = True
        targets.write_text('var kind = "STANDBY_DEFEND";')
        sources = audit.Sources(self.root, [])
        self.assertEqual(["review", "review", "review"], [audit.audit_clause(c, row, sources)["status"] for row in rows])

    def test_legacy_runtime_hold_route_does_not_certify_a_separate_skill(self):
        (self.engine / "CoreRuleEngine.Hold.cs").write_text('var definition = HeldDefinition(no); ResolveHeldStackItem(state);')
        sources = audit.Sources(self.root, [])
        c = card("当我据守一处战场时，你获得的分数+1。\n{{横置}}：抽一张牌。", legacyRoutes={"HeldDefinition": dict(kind="SCORE", amount=1)})
        self.assertEqual(["route", "missing"], [audit.audit_clause(c, row, sources)["status"] for row in audit.split_clauses(c["cardEffect"])])

    def test_runtime_serialization_can_omit_a_zero_held_amount(self):
        (self.engine / "CoreRuleEngine.Hold.cs").write_text('HeldDefinition(no); ResolveHeldStackItem(state);')
        sources = audit.Sources(self.root, [])
        c = card("当我据守一处战场时，把我送回所属的手牌。", legacyRoutes={"HeldDefinition": dict(kind="RETURN_SELF")})
        self.assertEqual("route", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_legacy_hold_count_and_timing_have_to_match_the_instruction(self):
        (self.engine / "CoreRuleEngine.Hold.cs").write_text('HeldDefinition(no); ResolveHeldStackItem(state);')
        sources = audit.Sources(self.root, [])
        for text in ["当我进攻时，抽两张牌。", "当我据守一处战场时，抽一张牌。", "当我据守一处战场时，抽两张牌，然后召出一枚符文。"]:
            c = card(text, legacyRoutes={"HeldDefinition": dict(kind="DRAW", amount=2)})
            self.assertEqual("review", audit.audit_clause(c, audit.split_clauses(text)[0], sources)["status"])

    def test_a_legacy_reader_alone_requires_the_actual_resolution_route(self):
        (self.engine / "CoreRuleEngine.Hold.cs").write_text('HeldDefinition(no);')
        sources = audit.Sources(self.root, [])
        c = card("当我据守一处战场时，抽两张牌。", legacyRoutes={"HeldDefinition": dict(kind="DRAW", amount=2)})
        self.assertEqual("review", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_legacy_starting_hand_condition_is_not_a_generic_draw_registration(self):
        (self.engine / "CoreRuleEngine.BeginningDraw.cs").write_text('BeginningDrawKind(no); ResolveBeginningDraw(state);')
        sources = audit.Sources(self.root, [])
        c = card("在你的回合开始阶段，如果你的手牌不足两张，则再抽一张牌。", legacyRoutes={"BeginningDrawKind": "HAND_BELOW_TWO"})
        self.assertEqual("route", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])
        c["legacyRoutes"] = {"BeginningDrawKind": "FACEDOWN_AT_START"}
        self.assertEqual("review", audit.audit_clause(c, audit.split_clauses(c["cardEffect"])[0], sources)["status"])

    def test_draw_count_must_match_the_printed_number(self):
        (self.engine / "CoreRuleEngine.Draw.cs").write_text('var amount = behavior.DrawCount; var selected = behavior.DrawsSelectedMainDeckTarget;')
        sources = audit.Sources(self.root, [])
        c = card("当你打出此牌时，抽两张牌。")
        row = audit.split_clauses(c["cardEffect"])[0]
        c["behaviors"] = [dict(drawCount=1)]
        self.assertEqual("review", audit.audit_clause(c, row, sources)["status"])
        c["behaviors"] = [dict(drawsSelectedMainDeckTarget=True)]
        self.assertEqual("review", audit.audit_clause(c, row, sources)["status"])
        c["behaviors"] = [dict(drawCount=2)]
        self.assertEqual("route", audit.audit_clause(c, row, sources)["status"])

    def test_activation_cost_requires_the_actual_color_and_not_only_its_quantity(self):
        row = audit.split_clauses("支付{{1}}和{{绿色}}，{{横置}}：抽一张牌。")[0]
        ability = dict(exhaustsSourceAsCost=True, manaCost=1, powerCostByTrait=dict(green=1), simpleEffect=dict(drawCount=1))
        self.assertTrue(audit.activation_matches(ability, row))
        self.assertFalse(audit.activation_matches(dict(ability, powerCostByTrait=dict(red=1)), row))
        self.assertFalse(audit.activation_matches(dict(ability, powerCost=1, powerCostByTrait={}), row))

    def test_inventory_fingerprint_changes_with_shared_rule_source(self):
        before = audit.source_fingerprint(self.root)
        (self.engine / "CoreRuleEngine.cs").write_text("changed")
        self.assertNotEqual(before, audit.source_fingerprint(self.root))


if __name__ == "__main__":
    unittest.main()
