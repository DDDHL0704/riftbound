import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('rune_domain_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = ('当我进攻时，展示你符文牌堆顶部的一张牌，然后将其回收。根据展示的符文特性执行以下效果：\n'
        '{{红色}}—对此处的一名敌方单位造成2点伤害，并对此处所有其他敌方单位造成1点伤害。\n'
        '{{蓝色}}—抽一张牌。\n{{黄色}}—眩晕一名敌方单位。')


def definition():
    return dict(timing='ATTACK', effect='REVEAL_RECYCLE_RUNE_DOMAIN', modes=[
        dict(id='RED', label='红色', requiredRevealedRuneTrait='red', effect='DAMAGE_ENEMY_AND_OTHERS_HERE', amount=2, otherEnemyDamageAmount=1),
        dict(id='BLUE', label='蓝色', requiredRevealedRuneTrait='blue', effect='DRAW', amount=1),
        dict(id='YELLOW', label='黄色', requiredRevealedRuneTrait='yellow', effect='STUN_ENEMY')])


class RuneDomainClauseAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.engine = self.root / 'src/Riftbound.Engine'
        self.engine.mkdir(parents=True)
        self.clauses = audit.split_clauses(TEXT, 'hero_unit')

    def consumers(self):
        for name, content in {
            'CoreRuleEngine.UnitEvents.cs': 'ResolveRuneDomainReveal(state, item); ValidRevealedRuneContext(state, c, player); ResolveRuneDomainDamage(state, item, spec); ApplyDrawToPlayer(state);',
            'CoreRuleEngine.RuneDomainReveal.cs': 'WithUnitEvents(ev, observations); CaptureRecycleObservers(ev, zones, cards); ResolveUnitEventAreaDamage(state, item, spec.OtherEnemyDamageAmount);',
            'CoreRuleEngine.UnitEventModes.cs': 'RuneDomainModeApplies(context, mode);',
            'CoreRuleEngine.UnitEventBoonInstructions.cs': 'if (spec.Effect == "STUN_ENEMY") ApplyStun(target);',
        }.items():
            (self.engine / name).write_text(content)

    def outcomes(self, spec=None):
        card = dict(cardNo='TEST-200', cardName='展示符文', cardCategory='hero_unit', cardEffect=TEXT,
                    behaviors=[dict(playsSourceToBaseAsUnit=True, drawCount=1, damageAmount=2,
                                    unitEventAbility=spec)])
        sources = audit.Sources(self.root, [])
        return [audit.audit_clause(card, row, sources)['status'] for row in self.clauses]

    def test_keeps_reveal_header_and_each_domain_in_the_attack_scope(self):
        self.assertEqual(5, len(self.clauses))
        self.assertEqual(['trigger'] * 5, [c['kind'] for c in self.clauses])
        self.assertTrue(all(c['scope'] == self.clauses[0]['text'] for c in self.clauses))

    def test_registration_and_unconditional_draw_do_not_certify_the_attack(self):
        self.consumers()
        (self.engine / 'CoreRuleEngine.cs').write_text('var count = behavior.DrawCount; var damage = behavior.DamageAmount;')
        self.assertEqual(['missing'] * 5, self.outcomes())

    def test_metadata_without_live_consumers_stays_missing(self):
        self.assertEqual(['missing'] * 5, self.outcomes(definition()))

    def test_independent_domain_specs_and_consumers_cover_each_clause(self):
        self.consumers()
        self.assertEqual(['route'] * 5, self.outcomes(definition()))

    def test_the_wrong_red_amount_area_trait_effect_or_cost_does_not_certify_red(self):
        self.consumers()
        for key, value in [('amount', 1), ('otherEnemyDamageAmount', 0), ('requiredRevealedRuneTrait', 'green'),
                           ('effect', 'DAMAGE_ENEMIES_HERE'), ('leadingManaCost', 1)]:
            with self.subTest(key=key):
                spec = definition()
                spec['modes'][0][key] = value
                outcomes = self.outcomes(spec)
                self.assertEqual('missing', outcomes[2])
                self.assertEqual('route', outcomes[3])

    def test_reveal_requires_both_recycling_and_independent_trigger_creation(self):
        self.consumers()
        file = self.engine / 'CoreRuleEngine.RuneDomainReveal.cs'
        content = file.read_text()
        for symbol in ['CaptureRecycleObservers', 'WithUnitEvents']:
            with self.subTest(symbol=symbol):
                file.write_text(content.replace(symbol, 'NotExecuted'))
                self.assertEqual(['missing'] * 5, self.outcomes(definition()))
        file.write_text(content)

    def test_domain_selection_and_captured_reveal_validation_are_required(self):
        self.consumers()
        for name in ['CoreRuleEngine.UnitEventModes.cs', 'CoreRuleEngine.UnitEvents.cs']:
            with self.subTest(name=name):
                file = self.engine / name
                content = file.read_text()
                file.write_text('unrelated code')
                self.assertEqual(['missing'] * 5, self.outcomes(definition()))
                file.write_text(content)

    def test_extra_root_conditions_and_duplicate_mode_ids_are_not_equivalent(self):
        self.consumers()
        spec = definition()
        spec['requiresEmpowered'] = True
        self.assertEqual(['missing'] * 5, self.outcomes(spec))
        spec = definition()
        spec['modes'][1]['id'] = spec['modes'][0]['id']
        self.assertEqual(['missing'] * 5, self.outcomes(spec))

    def test_another_card_text_is_left_for_the_normal_audit(self):
        row = dict(self.clauses[0], text='当我进攻时，抽一张牌。', scope='当我进攻时，抽一张牌。')
        self.assertIsNone(audit.rune_domain_reveal_route({}, row, audit.Sources(self.root, [])))
