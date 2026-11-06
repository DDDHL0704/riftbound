import importlib.util
from pathlib import Path
import shutil
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('source_attack_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class SourceAttackDamageAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        repository = Path(__file__).parents[2]
        for name in ['CoreRuleEngine.cs', 'CoreRuleEngine.CombatMight.cs', 'CoreRuleEngine.BattleStartRoles.cs',
                     'CoreRuleEngine.AttackMightDamage.cs', 'CoreRuleEngine.FieldTriggers.cs']:
            destination = self.root / 'src/Riftbound.Engine' / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(repository / 'src/Riftbound.Engine' / name, destination)

    @staticmethod
    def card(keyword=True):
        text = ('当我进攻时，对此处的一名敌方单位造成等同于我{{强攻}}数值的伤害。' if keyword
                else '当我进攻时，对此处的一名敌方单位造成1点伤害。')
        node = dict(kind='UNIT_ATTACK_SOURCE_KEYWORD_DAMAGE' if keyword else 'UNIT_ATTACK_FIXED_DAMAGE',
                    timing='UNIT_ATTACK', text=text, targetScope='ENEMY_UNIT_HERE')
        node.update(dict(damageSourceKeyword='强攻') if keyword else dict(damageAmount=1))
        return dict(cardNo='TEST', cardName='test', cardCategory='unit', cardEffect=text,
                    behaviors=[dict(playsSourceToBaseAsUnit=True)], triggers=[dict(node=node)])

    def answer(self, card):
        return audit.source_attack_damage_route(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))

    def mutate(self, name, old, new='removed'):
        path = self.root / 'src/Riftbound.Engine' / name
        previous = path.read_text(); self.assertIn(old, previous)
        path.write_text(previous.replace(old, new)); return path, previous

    def test_exact_native_profiles_and_all_consumers_are_required(self):
        for keyword in [True, False]: self.assertEqual('route', self.answer(self.card(keyword))['status'])

    def test_wrong_scope_timing_kind_amount_keyword_or_unprinted_costs_are_missing(self):
        for keyword in [True, False]:
            for change in [dict(timing='UNIT_DEFEND'), dict(targetScope='ENEMY_UNIT'), dict(kind='OTHER'),
                           dict(damageAmount=9), dict(damageSourceKeyword='坚守'), dict(optional=True), dict(manaCost=1)]:
                with self.subTest(keyword=keyword, change=change):
                    card = self.card(keyword); card['triggers'][0]['node'].update(change)
                    self.assertEqual('missing', self.answer(card)['status'])

    def test_a_native_unit_route_is_required_even_if_donor_text_is_executable(self):
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.answer(card)['status'])

    def test_keyword_and_fixed_value_readers_cannot_be_replaced_with_might(self):
        for symbol in ['ResolveCurrentCountedCombatKeyword(state, source, keyword)',
                       'spec.DamageAmount ?? ResolveCurrentFieldUnitPower(state, source)']:
            path, old = self.mutate('CoreRuleEngine.AttackMightDamage.cs', symbol)
            for keyword in [True, False]: self.assertEqual('missing', self.answer(self.card(keyword))['status'])
            path.write_text(old)

    def test_generation_location_and_zero_damage_boundaries_are_required(self):
        for symbol in ['source.ObjectGeneration != context.SourceGeneration',
                       '!IsObjectOnField(state.PlayerZones, source.ObjectId)', 'if (baseAmount <= 0) return result;']:
            path, old = self.mutate('CoreRuleEngine.AttackMightDamage.cs', symbol)
            self.assertEqual('missing', self.answer(self.card())['status']); path.write_text(old)

    def test_a_declared_capture_without_its_real_dispatch_is_not_a_route(self):
        for name in ['CoreRuleEngine.cs', 'CoreRuleEngine.BattleStartRoles.cs']:
            self.mutate(name, 'CaptureCombatMightTriggers(')
        for keyword in [True, False]: self.assertEqual('missing', self.answer(self.card(keyword))['status'])

    def test_attack_roles_here_targets_and_shared_ward_checks_are_required(self):
        cases = [('CoreRuleEngine.CombatMight.cs', 'spec.Timing == TriggerTimings.UnitAttack && !attacking.Contains(id)'),
                 ('CoreRuleEngine.FieldTriggers.cs', 'IsCombatMightTargetHere(state, item, c.ObjectId)'),
                 ('CoreRuleEngine.FieldTriggers.cs', 'TargetProtectionRules.IsLegalActivatedSkillTarget(')]
        for name, symbol in cases:
            path, old = self.mutate(name, symbol)
            self.assertEqual('missing', self.answer(self.card())['status']); path.write_text(old)

    def test_the_old_first_defender_shortcut_must_be_removed(self):
        path = self.root / 'src/Riftbound.Engine/CoreRuleEngine.cs'
        path.write_text(path.read_text() + '\nResolveSourceAttackDamageToFirstDefenderTriggers();\n')
        self.assertEqual('missing', self.answer(self.card(False))['status'])

    def test_other_scopes_and_sentences_are_not_silently_certified(self):
        card = self.card(); row = audit.card_clauses(card)[0]; row['scope'] = '其他授予的技能'
        self.assertIsNone(audit.source_attack_damage_route(card, row, audit.Sources(self.root, [])))
        card = self.card(); card['cardEffect'] = card['cardEffect'].replace('强攻', '强攻2')
        self.assertIsNone(self.answer(card))


if __name__ == '__main__':
    unittest.main()
