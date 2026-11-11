import copy
import importlib.util
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('yi_level_cost_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class YiLevelCostAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        return dict(cardNo='UNL-059/219', cardName='易', cardCategory='hero_unit',
            cardEffect=('{{等级3>}} 我的费用减少{{2}}和{{绿色}}。\n'
                        '{{等级6>}} 改为我的费用减少{{4}}和{{绿色}}{{绿色}}。\n'
                        '{{等级11>}} 改为我的费用减少{{6}}和{{绿色}}{{绿色}}{{绿色}}。'),
            behaviors=[dict(playsSourceToBaseAsUnit=True,
                costReductionConditionKind='CONTROLLER_EXPERIENCE_THRESHOLD_COUNT',
                costReductionMana=2, costReductionPower=1, costReductionPowerTrait='green',
                costReductionExperienceThresholds=[3, 6, 11])])

    def statuses(self, card, sources=None):
        return [audit.audit_clause(card, row, sources or self.sources)['status'] for row in audit.card_clauses(card)]

    def changed_source(self, filename, token):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, 'REMOVED_PRICING_CONSUMER')
        return sources

    def test_all_three_clauses_use_the_actual_highest_level_price(self):
        self.assertEqual(['route'] * 3, self.statuses(self.card()))
        card = self.card(); card['behaviors'][0]['costReductionExperienceThresholds'] = [11, 3, 6]
        self.assertEqual(['route'] * 3, self.statuses(card))

    def test_another_printed_reduction_can_compose_before_the_experience_discount(self):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/CoreRuleEngine.EmpoweredSpellCosts.cs'
        original = sources.files[path]
        direct = 'ApplySacrificePowerReduction(a.TypedPower, sacrificeReduction)'
        composed = 'ApplyBoonPlayPowerReduction(behavior, ' + direct + ')'
        self.assertIn(direct, original)
        if composed not in original:
            sources.files[path] = original.replace(direct, composed)
        self.assertEqual(['route'] * 3, self.statuses(self.card(), sources))

    def test_removing_the_level_counter_from_a_composed_price_reopens_the_clause(self):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/CoreRuleEngine.EmpoweredSpellCosts.cs'
        original = sources.files[path]
        direct = 'ConditionalPlayPowerCost(state, player, behavior, ApplySacrificePowerReduction(a.TypedPower, sacrificeReduction))'
        composed = 'ApplyBoonPlayPowerReduction(behavior, ApplySacrificePowerReduction(a.TypedPower, sacrificeReduction))'
        if direct in original:
            sources.files[path] = original.replace(direct, composed)
        else:
            nested = 'ConditionalPlayPowerCost(state, player, behavior, ' + composed + ')'
            self.assertIn(nested, original)
            sources.files[path] = original.replace(nested, composed)
        self.assertEqual(['missing'] * 3, self.statuses(self.card(), sources))

    def test_wrong_levels_amounts_and_colors_reopen_all_three_clauses(self):
        changes = [dict(costReductionExperienceThresholds=steps)
                   for steps in [None, [3], [3, 3, 11], [3, 6, 10], [0, 3, 6, 11], [True, 6, 11]]]
        changes += [dict(costReductionConditionKind='OTHER'), dict(costReductionMana=6),
                    dict(costReductionPower=3), dict(costReductionPowerTrait='blue'),
                    dict(playsSourceToBaseAsUnit=False)]
        for change in changes:
            with self.subTest(change=change):
                card = self.card(); card['behaviors'][0].update(change)
                self.assertEqual(['missing'] * 3, self.statuses(card))

    def test_unprinted_later_levels_cannot_be_granted_by_a_catalog_field(self):
        card = self.card(); card['cardEffect'] = card['cardEffect'].splitlines()[0]
        self.assertEqual(['missing'], self.statuses(card))

    def test_a_larger_conditional_scope_cannot_use_the_unconditional_price_profile(self):
        card = self.card()
        for clause in audit.card_clauses(card):
            with self.subTest(text=clause['text']):
                scoped = dict(clause, scope='如果我处于战斗中，则' + clause['scope'])
                self.assertEqual('missing', audit.experience_play_cost_route(card, scoped, self.sources)['status'])

    def test_experience_must_be_read_from_the_current_actor(self):
        sources = self.changed_source('CoreRuleEngine.ConditionalPlayCosts.cs',
            'state.PlayerExperience.GetValueOrDefault(player) >= threshold')
        self.assertEqual(['missing'] * 3, self.statuses(self.card(), sources))

    def test_an_unused_correct_counter_cannot_certify_the_actual_zero_discount_branch(self):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/CoreRuleEngine.ConditionalPlayCosts.cs'
        original = sources.files[path]
        branch = re.search(r'(CardCostReductionConditionKinds\.ControllerExperienceThresholdCount\s*=>\s*)'
                           r'(.*?)(?=,\s*CardCostReductionConditionKinds\.)', original, re.S)
        self.assertIsNotNone(branch)
        changed = original[:branch.start(2)] + '0' + original[branch.end(2):]
        decoy = ('\n    private static int UnusedExperienceCount(MatchState state, string player, '
                 'CardBehaviorDefinition behavior) => ' + branch.group(2) + ';\n')
        at = changed.rfind('}')
        sources.files[path] = changed[:at] + decoy + changed[at:]
        self.assertEqual(['missing'] * 3, self.statuses(self.card(), sources))

    def test_quote_and_actual_mana_payment_must_both_use_the_counter(self):
        for name, token in [
            ('CoreRuleEngine.cs', 'var manaCost = CalculatePlayManaCost(state, intent.PlayerId, behavior, extraManaCost,'),
            ('CoreRuleEngine.cs', re.search(
                r'behavior\.CostReductionMana \* ConditionalPlayCostCount\(state, playerId, behavior(?:, sourceObjectId)?\)',
                self.sources.files['src/Riftbound.Engine/CoreRuleEngine.cs']).group(0)),
            ('CoreRuleEngine.PlayManaCost.cs', 'ResolveCostReductionMana(state, playerId, behavior, sourceObjectId)'),
            ('CoreRuleEngine.PlayManaCost.cs', 'Math.Max(0, total - Math.Max(0, reduction.Floor))')]:
            with self.subTest(name=name, token=token):
                self.assertEqual(['missing'] * 3, self.statuses(self.card(), self.changed_source(name, token)))

    def test_the_typed_power_price_and_its_floor_need_real_consumers(self):
        for name, token in [
            ('CoreRuleEngine.ConditionalPlayCosts.cs', 'var reduction = behavior.CostReductionPower * ConditionalPlayCostCount(state, player, behavior);'),
            ('CoreRuleEngine.ConditionalPlayCosts.cs', 'Math.Max(0, p.Value - reduction)'),
            ('CoreRuleEngine.EmpoweredSpellCosts.cs', 'ConditionalPlayPowerCost(state, player, behavior,')]:
            with self.subTest(name=name, token=token):
                self.assertEqual(['missing'] * 3, self.statuses(self.card(), self.changed_source(name, token)))

    def test_other_discount_sentences_are_not_certified_by_this_profile(self):
        for text in ['{{等级6>}} 此牌的费用减少{{2}}。', '你的废牌堆中每有一张牌，我的费用便减少{{1}}。']:
            card = self.card(); card['cardEffect'] = text
            self.assertIsNone(audit.experience_play_cost_route(card, audit.card_clauses(card)[0], self.sources))
