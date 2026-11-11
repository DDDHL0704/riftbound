import copy
import importlib.util
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('level_and_graveyard_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class LevelAndGraveyardPlayCostAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def center():
        return dict(cardNo='UNL-091/219', cardCategory='spell', cardEffect=('抽两张牌。\n'
            '{{等级6>}} 此牌的费用减少{{2}}。（如果你拥有不少于6经验，则获得该效果。）\n'
            '{{等级11>}} 改为此牌的费用减少{{4}}。'), behaviors=[dict(
                costReductionConditionKind='CONTROLLER_EXPERIENCE_THRESHOLD_COUNT',
                costReductionMana=2, costReductionExperienceThresholds=[6, 11])])

    @staticmethod
    def rhasa():
        return dict(cardNo='OGN·195/298', cardCategory='unit',
            cardEffect='你的废牌堆中每有一张牌，我的费用便减少{{1}}。', behaviors=[dict(
                playsSourceToBaseAsUnit=True, costReductionConditionKind='CONTROLLER_GRAVEYARD_CARD_COUNT',
                costReductionMana=1)])

    def statuses(self, card, sources=None):
        return [audit.level_and_graveyard_play_cost_route(card, clause, sources or self.sources)['status']
            for clause in audit.card_clauses(card) if '费用' in clause['text']]

    def changed_member(self, filename, member, token):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        body = audit.executable_member(sources, filename, member)
        self.assertEqual(1, sources.files[path].count(body))
        self.assertIn(token, body)
        # Preserve a correct but unused copy to ensure file-wide token checks fail.
        decoy = body.replace(member + '(', 'Unused' + member + '(', 1)
        changed = body.replace(token, 'REMOVED_LIVE_PRICE_CONSUMER')
        sources.files[path] = sources.files[path].replace(body, changed) + '\n' + decoy
        return sources

    def test_exact_two_level_and_all_card_graveyard_profiles_have_live_consumers(self):
        self.assertEqual(['route', 'route'], self.statuses(self.center()))
        self.assertEqual(['route'], self.statuses(self.rhasa()))

    def test_wrong_level_thresholds_and_mana_reopen_both_level_clauses(self):
        for change in [dict(costReductionExperienceThresholds=s) for s in
                       [None, [6], [6, 6], [3, 6, 11], [6, 10], [True, 11]]] + [
                       dict(costReductionMana=4), dict(costReductionMana=True),
                       dict(costReductionConditionKind='OTHER')]:
            with self.subTest(change=change):
                card = self.center(); card['behaviors'][0].update(change)
                self.assertEqual(['missing', 'missing'], self.statuses(card))

    def test_higher_level_must_be_printed_and_replace_the_lower_discount(self):
        card = self.center(); card['cardEffect'] = card['cardEffect'].splitlines()[1]
        self.assertEqual(['missing'], self.statuses(card))
        card = self.center(); card['behaviors'][0]['costReductionExperienceThresholds'] = [11, 6]
        self.assertEqual(['route', 'route'], self.statuses(card))

    def test_graveyard_profile_cannot_borrow_same_name_or_opponent_counter(self):
        for change in [dict(costReductionConditionKind='SAME_NAME_IN_CONTROLLER_GRAVEYARD'),
                       dict(costReductionMana=2), dict(costReductionMana=True),
                       dict(playsSourceToBaseAsUnit=False)]:
            with self.subTest(change=change):
                card = self.rhasa(); card['behaviors'][0].update(change)
                self.assertEqual(['missing'], self.statuses(card))

    def test_mana_only_text_does_not_certify_a_power_discount(self):
        for factory in [self.center, self.rhasa]:
            for change in [dict(costReductionPower=1), dict(costReductionPower=True),
                           dict(costReductionPowerTrait='purple')]:
                with self.subTest(factory=factory.__name__, change=change):
                    card = factory(); card['behaviors'][0].update(change)
                    self.assertTrue(all(x == 'missing' for x in self.statuses(card)))

    def test_each_actual_mode_and_scope_must_be_reviewed(self):
        for factory in [self.center, self.rhasa]:
            card = factory(); card['behaviors'].append(copy.deepcopy(card['behaviors'][0]))
            self.assertTrue(all(x == 'missing' for x in self.statuses(card)))
            card = factory()
            for clause in audit.card_clauses(card):
                if '费用' not in clause['text']: continue
                for changed in [dict(scope='如果我处于战斗中则' + clause['scope']), dict(location='lower')]:
                    scoped = dict(clause, **changed)
                    self.assertEqual('missing', audit.level_and_graveyard_play_cost_route(card, scoped, self.sources)['status'])

    def test_actual_level_branch_reads_current_actor_instead_of_unused_helper(self):
        sources = self.changed_member('CoreRuleEngine.ConditionalPlayCosts.cs', 'ConditionalPlayCostCount',
            'state.PlayerExperience.GetValueOrDefault(player) >= threshold')
        self.assertEqual(['missing', 'missing'], self.statuses(self.center(), sources))

    def test_actual_graveyard_branch_excludes_source_and_counts_all_card_types(self):
        for token in ['state.PlayerZones.TryGetValue(player, out var graveyardZones)',
                      'graveyardZones.Graveyard.Count(id => id != sourceObjectId)']:
            with self.subTest(token=token):
                sources = self.changed_member('CoreRuleEngine.ConditionalPlayCosts.cs', 'ConditionalPlayCostCount', token)
                self.assertEqual(['missing'], self.statuses(self.rhasa(), sources))

    def test_actual_price_prompt_quote_and_payment_cannot_be_replaced_by_decoys(self):
        mutations = [
            ('CoreRuleEngine.cs', 'ResolveCostReductionMana',
             '_ => behavior.CostReductionMana * ConditionalPlayCostCount(state, playerId, behavior, sourceObjectId)'),
            ('CoreRuleEngine.PlayManaCost.cs', 'CalculatePlayManaCost',
             'ResolveCostReductionMana(state, playerId, behavior, sourceObjectId)'),
            ('CoreRuleEngine.PlayManaCost.cs', 'CalculatePlayManaCost',
             'Math.Max(0, total - Math.Max(0, reduction.Floor))'),
            ('CoreRuleEngine.PlayManaCost.cs', 'MinimumPlayManaCost', 'sourceObjectId: sourceObjectId, destination: null)'),
            ('CoreRuleEngine.cs', 'TryBuildPlayCardPlan',
             'optionalCostManaReduction, optionalCosts, paymentTargets, command.SourceObjectId,'),
            ('CoreRuleEngine.cs', 'ResolvePlayCard', 'PaymentCostRules.TryCommitPayment('),
            ('CoreRuleEngine.PlayCostPreview.cs', 'PreviewPlayCard',
             'TryBuildPlayCardPlan(state, new("COST_PREVIEW", playerId, CommandTypes.PlayCard),'),
            ('MatchSession.cs', 'PromptMinimumManaCost',
             'CoreRuleEngine.MinimumPlayManaCost(state, playerId, behavior, sourceObjectId, additionalMana).Total'),
        ]
        for filename, member, token in mutations:
            with self.subTest(member=member, token=token):
                sources = self.changed_member(filename, member, token)
                self.assertEqual(['missing', 'missing'], self.statuses(self.center(), sources))
                self.assertEqual(['missing'], self.statuses(self.rhasa(), sources))

    def test_unrelated_cost_text_remains_for_its_own_matcher(self):
        for text in ['{{等级3>}} 我的费用减少{{2}}和{{绿色}}。',
                     '你的废牌堆中每有一张和我同名的卡牌，我的费用便减少{{2}}。']:
            card = self.rhasa(); card['cardEffect'] = text
            self.assertIsNone(audit.level_and_graveyard_play_cost_route(card, audit.card_clauses(card)[0], self.sources))
