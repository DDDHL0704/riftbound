import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('combat_spell_cost_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '如果我处于战斗中，则所有友方法术的费用减少{{1}}和{{A}}，不得低于{{1}}，而所有敌方法术的费用增加{{1}}和{{A}}。'


class CombatSpellCostAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        return dict(cardNo='SFD·146/221', cardCategory='hero_unit', cardEffect=TEXT,
                    behaviors=[dict(playsSourceToBaseAsUnit=True, combatSpellCostAdjustment=dict(
                        friendlyManaReduction=1, friendlyGenericPowerReduction=1, minimumFriendlyMana=1,
                        enemyManaIncrease=1, enemyGenericPowerIncrease=1))])

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def test_exact_profile_needs_both_sides_of_the_actual_shared_cost_pipeline(self):
        self.assertEqual('route', self.status())

    def test_missing_wrong_or_extra_conditions_cannot_certify_the_printed_clause(self):
        for key, value in [('friendlyManaReduction', 0), ('friendlyGenericPowerReduction', 0),
                           ('minimumFriendlyMana', 0), ('enemyManaIncrease', 0), ('enemyGenericPowerIncrease', 0),
                           ('friendlyManaReduction', True), ('onlyWhenAttacking', True)]:
            with self.subTest(key=key, value=value):
                card = self.card(); card['behaviors'][0]['combatSpellCostAdjustment'][key] = value
                self.assertEqual('missing', self.status(card))
        card = self.card(); del card['behaviors'][0]['combatSpellCostAdjustment']
        self.assertEqual('missing', self.status(card))
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.status(card))

    def test_actual_consumers_cannot_be_replaced_by_unused_same_text_helpers(self):
        checks = [
            ('CoreRuleEngine.CombatSpellCosts.cs', 'IsFaceUpNonStandbyUnit(source)'),
            ('CoreRuleEngine.CombatSpellCosts.cs', 'IsEstablishedBattleDuelParticipant(state, source.ObjectId)'),
            ('CoreRuleEngine.CombatSpellCosts.cs', 'battle.DefenderObjectIds.Contains(source.ObjectId)'),
            ('CoreRuleEngine.CombatSpellCosts.cs', 'source.ObjectId, battlefield)'),
            ('CoreRuleEngine.CombatSpellCosts.cs', '!IsSpellCostSourceInBattle(state, source)'),
            ('CoreRuleEngine.CombatSpellCosts.cs', 'EffectiveFieldControllerId(state.PlayerZones, id, source)'),
            ('CoreRuleEngine.CombatSpellCosts.cs', 'AttachedRuleTextRules.Instances(state, source)'),
            ('CoreRuleEngine.CombatSpellCosts.cs', 'source.Controller == player'),
            ('CoreRuleEngine.ConditionalPlayCosts.cs', 'source.Controller != player'),
            ('CoreRuleEngine.ConditionalPlayCosts.cs', 'source.Spec.EnemyGenericPowerIncrease'),
            ('CoreRuleEngine.PlayManaCost.cs', 'source.FriendlyManaReduction, source.MinimumFriendlyMana'),
            ('CoreRuleEngine.EmpoweredSpellCosts.cs', 'source.FriendlyGenericPowerReduction'),
            ('CoreRuleEngine.cs', 'CombatSpellCostReductionMana = manaCost.CombatSpellReduction'),
            ('CoreRuleEngine.PlayCostPreview.cs', '-plan.CombatSpellCostReductionPower'),
            ('MatchSession.cs', 'requirement["combatSpellCostReductionMana"] = manaCost.CombatSpellReduction'),
        ]
        for filename, token in checks:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename; self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_ACTUAL_CONSUMER')
                sources.files[path] += '\n    private static void UnusedCombatCostDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(sources=sources))

    def test_trigger_scopes_and_other_spell_cost_sentences_are_not_certified(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        self.assertEqual('missing', audit.audit_clause(card, dict(clause, scope='当我进攻时'), self.sources)['status'])
        for text in [TEXT.replace('战斗中', '战场上'), TEXT.replace('法术', '单位')]:
            self.assertIsNone(audit.combat_spell_cost_route(card, dict(clause, text=text), self.sources))
