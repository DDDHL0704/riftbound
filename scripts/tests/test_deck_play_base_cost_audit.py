"""A reveal/kill deck route must never certify an all-costs waiver."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('deck_base_cost_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class DeckPlayBaseCostAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])
        cls.records = json.loads((ROOT / 'scripts/audit/reviewed-clauses/deck-play-base-cost-waivers.json').read_text())['records']

    def sample(self, hook):
        record = self.records[1 if hook else 0]
        card = dict(cardNo=record['cardNos'][0], cardCategory='equipment')
        if hook:
            card['activations'] = [dict(kind='DESTROY_FRIENDLY_UNIT_LOOK_TOP_PLAY_POWER_PLUS_ONE_RECYCLE_REST',
                requiredTargetCount=1, exhaustsSourceAsCost=True, requiresBaseEquipmentSource=True,
                mainDeckLookCount=5, playPowerDelta=1, playCardFilter='CARD_TYPE:UNIT', recycleUnplayedLookedCards=True,
                manaCost=1, powerCostByTrait={'yellow': 1})]
        else:
            card['triggers'] = [dict(node=dict(kind='TURN_END_REVEAL_UNTIL_UNIT', timing='TURN_END',
                banishCount=1, playCount=1, playOriginZone='BANISHED', playDestinationZone='STACK'))]
        return card, dict(text=record['text'], scope=record['scope'], kind='activated' if hook else 'trigger', location='upper')

    def changed(self, filename, before, after):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(before, sources.files[path])
        sources.files[path] = sources.files[path].replace(before, after)
        return sources

    def test_both_real_profiles_require_the_base_only_execution_and_recovery_route(self):
        for hook in (False, True):
            result = audit.audit_clause(*self.sample(hook), self.sources)
            self.assertEqual('route', result['status'])
            self.assertIn('src/Riftbound.Engine/CoreRuleEngine.RefusedPlayPayment.cs', result['evidence'])

    def test_an_all_costs_constructor_reopens_the_defect_before_reviewed_evidence(self):
        sources = self.changed('CoreRuleEngine.EffectPlay.cs', 'EffectPlayIgnoreAllCosts: false);', 'EffectPlayIgnoreAllCosts: true);')
        for hook in (False, True):
            self.assertEqual('missing', audit.audit_clause(*self.sample(hook), sources)['status'])

    def test_each_positive_base_component_is_required_in_the_actual_producer(self):
        for component in ('Mana', 'Power'):
            sources = self.changed('CoreRuleEngine.EffectPlay.cs', f'EffectPlayIgnoreBase{component}: true', f'EffectPlayIgnoreBase{component}: false')
            for hook in (False, True):
                self.assertEqual('review', audit.audit_clause(*self.sample(hook), sources)['status'])

    def test_wrong_identity_free_play_kind_and_timing_profiles_are_not_certified(self):
        for hook in (False, True):
            card, clause = self.sample(hook)
            card['cardCategory'] = 'unit'
            self.assertEqual('review', audit.audit_clause(card, clause, self.sources)['status'])
        card, clause = self.sample(False)
        card['triggers'][0]['node']['timing'] = 'TURN_START'
        self.assertEqual('review', audit.audit_clause(card, clause, self.sources)['status'])
        for key, value in (('mainDeckLookCount', 6), ('playPowerDelta', 2), ('powerCostByTrait', {'red': 1})):
            card, clause = self.sample(True)
            card['activations'][0][key] = value
            self.assertEqual('review', audit.audit_clause(card, clause, self.sources)['status'])

    def test_missing_shared_payment_or_resumption_consumers_cannot_use_an_old_reviewed_route(self):
        for hook in (False, True):
            for filename in ('CoreRuleEngine.EffectPlay.cs', 'CoreRuleEngine.PlayManaCost.cs',
                             'CoreRuleEngine.DeckPlaySequence.cs', 'CoreRuleEngine.RefusedPlayPayment.cs',
                             'CoreRuleEngine.DestroyDeckPlay.cs' if hook else 'CoreRuleEngine.EndStep.cs'):
                with self.subTest(hook=hook, filename=filename):
                    sources = copy.copy(self.sources)
                    sources.files = dict(self.sources.files)
                    sources.files.pop('src/Riftbound.Engine/' + filename)
                    self.assertEqual('review', audit.audit_clause(*self.sample(hook), sources)['status'])

    def test_each_restored_waiver_flag_must_match_the_producer(self):
        for flag in ('IgnoreAllCosts', 'IgnoreBaseMana', 'IgnoreBasePower'):
            sources = self.changed('CoreRuleEngine.DeckPlaySequence.cs', f'pending.{flag} == expected.{flag}', 'true')
            for hook in (False, True):
                self.assertEqual('review', audit.audit_clause(*self.sample(hook), sources)['status'])

    def test_moving_the_guard_to_a_comment_does_not_restore_the_executable_route(self):
        fragment = 'pending.IgnoreAllCosts == expected.IgnoreAllCosts'
        sources = self.changed('CoreRuleEngine.DeckPlaySequence.cs', fragment, 'true /* ' + fragment + ' */')
        for hook in (False, True):
            self.assertEqual('review', audit.audit_clause(*self.sample(hook), sources)['status'])

    def test_different_scope_and_explicit_all_costs_text_are_not_matched(self):
        for hook in (False, True):
            card, clause = self.sample(hook)
            self.assertIsNone(audit.deck_play_base_cost_waiver_route(card, dict(clause, scope='当我进攻时'), self.sources))
            self.assertIsNone(audit.deck_play_base_cost_waiver_route(card, dict(clause, text=clause['text'].replace('无视费用', '无视一切费用')), self.sources))
