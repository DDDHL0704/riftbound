import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('paid_legend_movement_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
FEE = '你可以选择让你的传奇卡牌进入休眠状态，作为打出我的额外费用。'
MOVE = '当你打出我时，如果你支付了该额外费用，则可以将你任意数量的单位移动到一处开放的战场。'


class PaidLegendGroupMovementAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        event = dict(timing='PLAY', effect='MOVE_FRIENDLY_GROUP_TO_OPEN_BATTLEFIELD', optional=True,
                     anyNumberOfTargets=True, requiresSourcePlayLegendCost=True, preservesMovementCapture=True)
        return dict(cardNo='SFD·079/221', cardCategory='hero_unit', cardEffect=FEE + '\n' + MOVE,
                    behaviors=[dict(playsSourceToBaseAsUnit=True, mayExhaustLegendAdditionalCost=True,
                                    unitEventAbility=copy.deepcopy(event))], unitEventAbility=event)

    def status(self, index, card=None, sources=None):
        card = self.card() if card is None else card
        return audit.audit_clause(card, audit.card_clauses(card)[index], sources or self.sources)['status']

    def test_the_fee_and_independent_movement_each_need_their_live_consumer(self):
        self.assertEqual(['route', 'route'], [self.status(index) for index in [0, 1]])
        card = self.card(); card['unitEventAbility'] = None
        self.assertEqual('route', self.status(0, card))
        self.assertEqual('missing', self.status(1, card))

    def test_base_registration_or_a_similar_trigger_cannot_certify_the_actual_fee_and_condition(self):
        for flag in [False, 1, 1.0, 'true', None]:
            with self.subTest(flag=flag):
                card = self.card(); card['behaviors'][0]['mayExhaustLegendAdditionalCost'] = flag
                self.assertEqual(['missing', 'missing'], [self.status(index, card) for index in [0, 1]])
        for key, value in [('timing', 'HOLD'), ('effect', 'MOVE_ENEMY_HERE'), ('optional', False),
                           ('anyNumberOfTargets', False), ('requiresSourcePlayLegendCost', False),
                           ('requiresSourcePlayLegendCost', 1), ('requiresSourcePlayLegendCost', 1.0),
                           ('preservesMovementCapture', False)]:
            with self.subTest(key=key, value=value):
                card = self.card(); card['unitEventAbility'][key] = value
                self.assertEqual('missing', self.status(1, card))

    def test_a_separate_top_level_event_cannot_hide_a_wrong_actual_behavior_profile(self):
        card = self.card(); card['behaviors'][0]['unitEventAbility']['effect'] = 'DRAW'
        self.assertEqual('missing', self.status(1, card))

    def test_deleted_fee_and_history_consumers_are_not_replaced_by_unused_decoys(self):
        for filename, token in [
            ('CoreRuleEngine.LegendPlayCosts.cs', 'zones.LegendZone.Contains(id)'),
            ('CoreRuleEngine.cs', 'stackItem = stackItem with { LegendPlayCost = receipt }'),
            ('CoreRuleEngine.cs', 'item.StackItemId == stackItem.StackItemId ? stackItem : item'),
            ('MatchRecovery.LegendPlayCosts.cs', 'hash == MatchStateHasher.HashValue(receipt)'),
        ]:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_CONSUMER')
                sources.files[path] += '\n    private static void UnusedDecoy() { ' + token + '; }\n'
                self.assertEqual(['missing', 'missing'], [self.status(index, sources=sources) for index in [0, 1]])

    def test_deleted_capture_destination_and_movement_consumers_leave_the_trigger_missing(self):
        for filename, token in [
            ('CoreRuleEngine.SourcePlayConditions.cs', 'SourcePaidLegendPlayCost(entered, player) is not null'),
            ('CoreRuleEngine.PermanentUnitEvents.cs', 'OpenBattlefieldDestinations(state, item.ControllerId)'),
            ('CoreRuleEngine.LegendPlayCosts.cs', '!IsOpenBattlefield(state, field.ObjectId)'),
            ('CoreRuleEngine.LegendPlayCosts.cs', 'battlefield.ObjectGeneration != field.Generation'),
            ('CoreRuleEngine.ChosenMovement.cs', 'PayImposedMovementCost('),
            ('CoreRuleEngine.ObservedUnitEvents.cs', 'context.PaidLegendPlayCost'),
            ('MatchRecovery.UnitEventMovement.cs', 'original != CoreRuleEngine.DeclaredUnitEventMovementHash(item)'),
        ]:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_CONSUMER')
                sources.files[path] += '\n    private static void UnusedDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(1, sources=sources))
