import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('targeted_spell_cost_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '你以我为目标的法术费用减少{{1}}或{{A}}。'


class TargetedSpellCostAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card(no='SFD·141/221'):
        return dict(cardNo=no, cardCategory='hero_unit', cardEffect=TEXT,
                    behaviors=[dict(playsSourceToBaseAsUnit=True, targetedSpellManaOrPowerReduction=1)])

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def test_both_printings_require_the_joint_shared_price_consumer(self):
        for no in ['SFD·141/221', 'SFD·141a/221']:
            self.assertEqual('route', self.status(self.card(no)))

    def test_wrong_amount_or_a_unit_registration_alone_cannot_certify_the_clause(self):
        for value in [None, 0, 2, True, '1']:
            with self.subTest(value=value):
                card = self.card()
                if value is None:
                    del card['behaviors'][0]['targetedSpellManaOrPowerReduction']
                else:
                    card['behaviors'][0]['targetedSpellManaOrPowerReduction'] = value
                self.assertEqual('missing', self.status(card))
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.status(card))

    def test_removing_a_real_consumer_cannot_be_hidden_in_an_unused_helper(self):
        checks = [
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', '!IsSpellPlayBehavior(spell)'),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', 'ids.Distinct(StringComparer.Ordinal)'),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', 'IsFaceUpNonStandbyUnit(source)'),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', 'EffectiveFieldControllerId(state.PlayerZones, id, source) != player'),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', 'AttachedRuleTextRules.Instances(state, source)'),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', 'new ObjectBinding(id, source.ObjectGeneration)'),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', '!selected.TryAdd('),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', 'mode == "MANA" ? source.Amount : 0'),
            ('CoreRuleEngine.TargetedSpellCostReductions.cs', 'mode == "POWER" ? source.Amount : 0'),
            ('CoreRuleEngine.cs', 'targetedReduction: reduction.Power'),
            ('CoreRuleEngine.cs', '.OrderByDescending(CanPayPrice)'),
            ('CoreRuleEngine.cs', 'manaSourceSelection: validateRepeats ? manaSpend : null'),
            ('CoreRuleEngine.cs', 'totalManaCost = selectedPrice.Mana'),
            ('CoreRuleEngine.cs', 'AttributePowerReduction(selectedPrice.Reduction.Power)'),
            ('CoreRuleEngine.cs', 'paymentAudit["targetedSpellCostReductionChoices"] = selectedPrice.Reduction.ChoiceIds'),
            ('CoreRuleEngine.EmpoweredSpellCosts.cs', '+ optionalPlayReduction + targetedReduction'),
            ('CoreRuleEngine.PlayCostPreview.cs', '-plan.TargetedSpellCostReductionMana, -plan.TargetedSpellCostReductionPower'),
        ]
        for filename, token in checks:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_CONSUMER')
                sources.files[path] += '\n    private static void UnusedTargetCostDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(sources=sources))

    def test_other_effects_and_trigger_scopes_do_not_borrow_the_discount_path(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        self.assertEqual('missing', audit.audit_clause(card, dict(clause, scope='当我进攻时'), self.sources)['status'])
        for text in [TEXT.replace('或', '和'), TEXT.replace('法术', '技能'), TEXT.replace('我', '其他友方单位')]:
            self.assertIsNone(audit.targeted_spell_cost_route(card, dict(clause, text=text), self.sources))
