import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('armament_effect_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


def equipment(text='{{装配红色}}', lower='{{强攻2}}', **kwargs):
    return dict(cardNo='SFD·009/221', cardName='锯齿短匕', cardCategory='equipment', cardEffect=text,
                armamentText=dict(lowerText=lower, sourceImageUrl='https://cdn.playloltcg.com/card.png', sourceImageSha256='a' * 64),
                armament=dict(baseMightBonus=0, effectText=lower), **kwargs)


class ArmamentClauseAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.engine = self.root / 'src/Riftbound.Engine'
        self.engine.mkdir(parents=True)
        self.sources = audit.Sources(self.root, [])

    def outcome(self, card):
        return audit.audit_clause(card, audit.card_clauses(card)[-1], audit.Sources(self.root, []))

    def public_grant_consumer(self):
        (self.engine / 'CoreRuleEngine.ArmamentKeywords.cs').write_text('''
            if (!gear.IsFaceDown && !unit.IsFaceDown
                && IsObjectOnField(state.PlayerZones, gear.ObjectId)
                && IsObjectOnField(state.PlayerZones, host)
                && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host))
                Grant(definition.GrantedKeywords);
        ''')

    def document(self, card, **changes):
        path = self.root / 'source.json'
        row = dict(cardNo=card['cardNo'], **card['armamentText'], baseMightBonus=0, **changes)
        path.write_text(json.dumps(dict(schemaVersion=1, records=[row])))
        return path

    def reviewed_host_trigger(self, location="armament"):
        text = "当我进攻或防守时，对此处的一名敌方单位造成2点伤害。"
        card = equipment(lower=text)
        card["cardNo"] = "SFD·016/221"
        (self.engine / "ArmamentUnitEventRules.cs").write_text("var text = definition.EffectText; GrantHostTrigger();")
        (self.engine / "CoreRuleEngine.UnitEvents.cs").write_text("CaptureHostEvent();")
        tests = self.root / "tests/Riftbound.ConformanceTests/GrantTests.cs"
        tests.parent.mkdir(parents=True)
        tests.write_text("[Fact] public void HostActuallyDealsDamage() { }")
        directory = self.root / "scripts/audit/reviewed-clauses"
        directory.mkdir(parents=True)
        (directory / "grant.json").write_text(json.dumps(dict(schemaVersion=1, records=[dict(
            cardNos=[card["cardNo"]], text=text, scope=text, location=location, status="route",
            reason="Bound host skill", rules="Official lower box and core 719",
            runtime=dict(armament=dict(effectText=text)),
            evidence=[dict(path="src/Riftbound.Engine/ArmamentUnitEventRules.cs", contains=["GrantHostTrigger();"]),
                      dict(path="src/Riftbound.Engine/CoreRuleEngine.UnitEvents.cs", contains=["CaptureHostEvent();"])],
            tests=[dict(path="tests/Riftbound.ConformanceTests/GrantTests.cs", methods=["HostActuallyDealsDamage"])])])))
        return card

    def test_reviewed_lower_trigger_requires_runtime_consumer_and_native_test(self):
        card = self.reviewed_host_trigger()
        self.assertEqual("route", self.outcome(card)["status"])
        (self.engine / "ArmamentUnitEventRules.cs").write_text("var text = definition.EffectText;")
        self.assertEqual("review", self.outcome(card)["status"])

    def test_upper_review_cannot_certify_the_lower_host_trigger(self):
        card = self.reviewed_host_trigger(location="upper")
        self.assertEqual("missing", self.outcome(card)["status"])

    def test_reviewed_lower_route_reopens_if_runtime_or_test_evidence_changes(self):
        card = self.reviewed_host_trigger()
        card["armament"]["effectText"] = "造成9点伤害"
        self.assertEqual("review", self.outcome(card)["status"])
        card["armament"]["effectText"] = card["armamentText"]["lowerText"]
        (self.root / "tests/Riftbound.ConformanceTests/GrantTests.cs").write_text("public void HostActuallyDealsDamage() { }")
        self.assertEqual("review", self.outcome(card)["status"])

    def test_bounded_host_trigger_reader_does_not_reopen_unrelated_missing_rules(self):
        (self.engine / "ArmamentUnitEventRules.cs").write_text("var text = definition.EffectText; GrantHostTrigger();")
        self.assertEqual("missing", self.outcome(equipment(lower="我的据守效果也是征服效果，反之亦然。"))["status"])

    def test_bounded_death_reader_does_not_reopen_unimplemented_host_effects(self):
        (self.engine / "CoreRuleEngine.ArmamentLastBreath.cs").write_text("var text = definition.EffectText;")
        for text in ("当我征服一处战场时，给予我增益。", "我拥有“机械”属性。"):
            with self.subTest(text=text):
                self.assertEqual("missing", self.outcome(equipment(lower=text))["status"])

    def test_bounded_death_reader_and_runtime_spec_need_reviewed_native_evidence(self):
        (self.engine / "CoreRuleEngine.ArmamentLastBreath.cs").write_text("var text = definition.EffectText;")
        card = equipment(lower="{{绝念}}——抽一张牌。", unitEventAbility=dict(
            timing="LAST_BREATH", effect="DRAW", amount=1))
        self.assertEqual("missing", self.outcome(card)["status"])

    def test_hold_and_conquer_equivalence_is_a_rule_modification(self):
        row = audit.card_clauses(equipment(lower='我的据守效果也是征服效果，反之亦然。'))[-1]
        self.assertEqual('rule', row['kind'])

    def test_upper_and_lower_are_separate_clauses_with_distinct_hosts(self):
        card = equipment()
        rows = audit.card_clauses(card)
        self.assertEqual(['upper', 'armament'], [row['location'] for row in rows])
        self.assertEqual(['keyword', 'keyword'], [row['kind'] for row in rows])
        self.assertEqual('{{强攻2}}', rows[1]['text'])

    def test_equipment_own_keyword_and_generic_donor_parser_cannot_certify_host_grant(self):
        card = equipment(behaviors=[dict(sourceEquipmentTags='武装|强攻2')])
        (self.engine / 'CoreRuleEngine.cs').write_text('var a = CardCombatKeywordNames.Assault;')
        (self.engine / 'BorrowedExhaustRules.cs').write_text('var b = BorrowedExhaustParser.Parse(text);')
        self.assertEqual('missing', self.outcome(card)['status'])

    def test_host_keyword_needs_exact_amount_and_actual_grant_consumer(self):
        card = equipment()
        card['armament']['grantedKeywords'] = ['强攻2']
        self.assertEqual('missing', self.outcome(card)['status'])
        (self.engine / 'CoreRuleEngine.ArmamentKeywords.cs').write_text('var grants = definition.GrantedKeywords;')
        self.assertEqual('missing', self.outcome(card)['status'])
        self.public_grant_consumer()
        self.assertEqual('route', self.outcome(card)['status'])
        card['armament']['grantedKeywords'] = ['强攻']
        self.assertEqual('missing', self.outcome(card)['status'])
        card['armament']['grantedKeywords'] = ['强攻21']
        self.assertEqual('missing', self.outcome(card)['status'])
        card['armament']['grantedKeywords'] = ['坚守2']
        self.assertEqual('missing', self.outcome(card)['status'])

    def test_default_one_keyword_can_use_explicit_one_grant(self):
        card = equipment(lower='{{法盾}}')
        card['armament']['grantedKeywords'] = ['法盾1']
        self.public_grant_consumer()
        self.assertEqual('route', self.outcome(card)['status'])

    def test_upper_draw_and_activation_do_not_certify_lower_death_trigger(self):
        card = equipment('当你打出此牌时，抽一张牌。', '{{绝念}}——抽一张牌。', behaviors=[dict(drawCount=1)])
        (self.engine / 'CoreRuleEngine.cs').write_text('var count = behavior.DrawCount;')
        self.assertEqual('missing', self.outcome(card)['status'])
        self.assertEqual('trigger', audit.card_clauses(card)[-1]['kind'])

    def test_guardian_replacement_requires_flag_and_both_consumer_stages(self):
        card = equipment(lower='如果我被摧毁，则改为摧毁守护天使。移除我所受到的伤害，让我变为休眠状态，并将我召回。')
        card['armament']['replacesHostDestruction'] = True
        path = self.engine / 'CoreRuleEngine.DestructionReplacement.cs'
        path.write_text('var replacement = definition.ReplacesHostDestruction;')
        self.assertEqual('missing', self.outcome(card)['status'])
        path.write_text('var replacement = definition.ReplacesHostDestruction; if (kind == "ATTACHED_GEAR") Recall(host);')
        rows = audit.card_clauses(card)[1:]
        self.assertEqual(['route', 'route'], [audit.audit_clause(card, row, audit.Sources(self.root, []))['status'] for row in rows])
        card['armament']['replacesHostDestruction'] = False
        self.assertEqual('missing', self.outcome(card)['status'])

    def test_replacement_flag_does_not_cover_another_lower_effect(self):
        card = equipment(lower='当我移动时，抽一张牌。')
        card['armament']['replacesHostDestruction'] = True
        (self.engine / 'CoreRuleEngine.cs').write_text('var replacement = definition.ReplacesHostDestruction; var kind = "ATTACHED_GEAR";')
        self.assertEqual('missing', self.outcome(card)['status'])

    def test_future_runtime_lower_fields_require_review_until_mapped(self):
        card = equipment(lower='当我移动时，抽一张牌。')
        card['armament']['newMoveDrawCount'] = 1
        self.assertEqual('review', self.outcome(card)['status'])

    def test_future_text_consumer_requires_review_until_mapped(self):
        card = equipment(lower='当我移动时，抽一张牌。')
        (self.engine / 'CoreRuleEngine.cs').write_text('Apply(armament.EffectText);')
        self.assertEqual('review', self.outcome(card)['status'])

    def test_lower_effect_is_part_of_effect_identity_but_art_is_not(self):
        card = equipment()
        same = equipment(lower='{{强攻2}}（战力增加。）')
        self.assertEqual(audit.effect_identity(card), audit.effect_identity(same))
        self.assertNotEqual(audit.effect_identity(card), audit.effect_identity(equipment(lower='{{坚守2}}')))
        self.assertNotEqual(audit.effect_identity(card), audit.effect_identity(dict(card, armament=dict(baseMightBonus=1))))
        same['armamentText']['sourceImageSha256'] = 'b' * 64
        self.assertEqual(audit.effect_identity(card), audit.effect_identity(same))

    def test_lower_reminder_is_preserved_in_source_without_double_counting(self):
        card = equipment(lower='（我是所选单位的复制体。）')
        card['armamentText']['reminderOnly'] = True
        self.assertEqual(1, len(audit.card_clauses(card)))
        self.assertEqual(audit.effect_identity(equipment(lower='')), audit.effect_identity(card))

    def test_report_links_every_alternate_art_to_independent_image_source(self):
        card = equipment()
        alternate = dict(card, cardNo='SFD·009a/221', armamentText=dict(card['armamentText'], sourceImageSha256='b' * 64))
        report = audit.build_report(dict(cards=[card, alternate], armamentSourceCoverage=2), self.sources)
        self.assertEqual(1, report['effectGroups'])
        self.assertEqual(2, report['clauseCount'])
        lower = report['cards'][0]['clauses'][1]
        self.assertEqual(['SFD·009/221', 'SFD·009a/221'], lower['affectedPrints'])
        self.assertEqual(['a' * 64, 'b' * 64], [row['sha256'] for row in lower['sourceImages']])
        self.assertIn('**武装下栏：** {{强攻2}}', audit.markdown(report))

    def test_all_equivalent_prints_need_the_lower_grant(self):
        card = equipment()
        card['armament']['grantedKeywords'] = ['强攻2']
        alternate = dict(card, cardNo='ALT', armament=dict(baseMightBonus=0))
        self.public_grant_consumer()
        report = audit.build_report(dict(cards=[card, alternate]), audit.Sources(self.root, []))
        lower = report['cards'][0]['clauses'][1]
        self.assertEqual('missing', lower['status'])
        self.assertEqual(['ALT'], lower['affectedPrints'])

    def test_source_joins_normalized_printing_and_validates_zero_might(self):
        card = equipment()
        path = self.document(card)
        card = dict(card, cardNo='SFD-009/221', armament=dict(effectText='{{强攻2}}'))
        result = audit.attach_armament_sources(dict(cards=[card]), path)
        self.assertEqual(1, result['armamentSourceCoverage'])
        self.assertEqual('{{强攻2}}', result['cards'][0]['armamentText']['lowerText'])

    def test_unrecorded_armament_never_silently_disappears(self):
        card = equipment()
        path = self.document(card)
        with self.assertRaisesRegex(ValueError, '下栏中文来源'):
            audit.attach_armament_sources(dict(cards=[dict(card, cardNo='NEW')]), path)
        for tag in ['武装', '', None]:
            with self.subTest(tag=tag), self.assertRaisesRegex(ValueError, '运行时定义'):
                audit.attach_armament_sources(dict(cards=[dict(card, armament=None, tag=tag)]), path)

    def test_bad_source_authority_hash_might_and_duplicate_are_rejected(self):
        card = equipment()
        for changes in [dict(sourceImageUrl='https://community.example/card.png'), dict(sourceImageSha256='bad')]:
            path = self.document(card)
            data = json.loads(path.read_text())
            data['records'][0].update(changes)
            path.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, '卡图来源或摘要'):
                audit.attach_armament_sources(dict(cards=[card]), path)
        path = self.document(card)
        with self.assertRaisesRegex(ValueError, '下栏原文'):
            audit.attach_armament_sources(dict(cards=[dict(card, armament=dict(effectText='wrong'))]), path)
        with self.assertRaisesRegex(ValueError, '下栏战力'):
            audit.attach_armament_sources(dict(cards=[dict(card, armament=dict(baseMightBonus=1))]), path)
        data = json.loads(path.read_text())
        data['records'].append(dict(data['records'][0]))
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, '卡号重复'):
            audit.attach_armament_sources(dict(cards=[card]), path)

    def test_real_cn_sources_cover_all_current_definitions_and_empty_english_lower(self):
        root = Path(__file__).parents[2]
        metadata = json.loads((root / 'data/official/armaments.json').read_text())
        self.assertEqual('data/official/armament-text.zh-CN.json', metadata['sourceSnapshot'])
        self.assertEqual(hashlib.sha256((root / metadata['sourceSnapshot']).read_bytes()).hexdigest(), metadata['sourceSha256'])
        definitions = metadata['records']
        cards = [dict(cardNo=row['cardNo'], cardCategory='equipment', armament=row) for row in definitions]
        result = audit.attach_armament_sources(dict(cards=cards), root / 'data/official/armament-text.zh-CN.json')
        self.assertEqual(44, result['armamentSourceCoverage'])
        self.assertTrue(all(card['armament']['cardImageUrl'] == card['armamentText']['sourceImageUrl'] for card in result['cards']))
        for no in ['VEN·011', 'VEN·027', 'VEN·073', 'RAD·150/167']:
            card = next(card for card in result['cards'] if card['cardNo'] == no)
            self.assertTrue(card['armamentText']['lowerText'])
        trinity = next(card for card in result['cards'] if card['cardNo'] == 'SFD·115/221')
        self.assertIn('获得的分数+1', trinity['armamentText']['lowerText'])


    def bounded_movement_reader(self):
        code = (Path(__file__).resolve().parents[2] / 'src/Riftbound.Engine/CoreRuleEngine.ArmamentMovementProtection.cs').read_text()
        path = self.engine / 'CoreRuleEngine.ArmamentMovementProtection.cs'
        path.write_text(code)
        return path, code

    def test_exact_bounded_enemy_move_reader_does_not_reopen_other_lower_boxes(self):
        self.bounded_movement_reader()
        for lower in ['如果此牌是在本回合贴附到我身上的，则我额外获得{{S}}+2。',
                      '我拥有“机械”属性。', '你的法术和技能造成的伤害+3（如果已贴附此牌）。',
                      '你在此处的单位获得{{游走}}。', '我无法被敌方法术和技能移动。']:
            with self.subTest(lower=lower):
                # A bounded reader is not itself evidence that any lower box executes.
                self.assertEqual('missing', self.outcome(equipment(lower=lower))['status'])

    def test_an_additional_reader_in_the_same_movement_file_remains_unmapped(self):
        path, code = self.bounded_movement_reader()
        path.write_text(code + '\nvar future = definition.EffectText;')
        self.assertEqual('review', self.outcome(equipment(lower='当我移动时，抽一张牌。'))['status'])

    def test_bounded_reader_needs_exact_sentence_equality_and_actual_file(self):
        path, code = self.bounded_movement_reader()
        for changed in [code.replace('== "我无法被敌方法术和技能移动"', '!= "我无法被敌方法术和技能移动"'),
                        code.replace('"我无法被敌方法术和技能移动"', '"当我移动时，抽一张牌"')]:
            with self.subTest(changed=changed):
                path.write_text(changed)
                self.assertEqual('review', self.outcome(equipment(lower='当我移动时，抽一张牌。'))['status'])
        path.unlink()
        (self.engine / 'CoreRuleEngine.UnknownArmamentReader.cs').write_text(code)
        self.assertEqual('review', self.outcome(equipment(lower='当我移动时，抽一张牌。'))['status'])
