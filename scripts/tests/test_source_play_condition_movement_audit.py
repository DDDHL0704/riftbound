import copy
import unittest

from test_effect_clause_audit import audit, card


TEXT = '当你在你的回合中将我从正面朝下的状态打出时，你可以选择移动任意一名位于不同位置的敌方单位到我所在的战场。'


class SourcePlayConditionMovementAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = audit.Sources(audit.ROOT, [])

    def setUp(self):
        self.sources = copy.copy(self.original)
        self.sources.files = self.original.files.copy()
        self.value = card('{{待命}}\n{{后排}}\n' + TEXT)
        self.value['cardNo'] = 'UNL-141/219'
        self.value['unitEventAbility'] = dict(timing='PLAY', effect='MOVE_ENEMY_HERE', optional=True,
            requiresSourcePlayedFromStandby=True, requiresSourcePlayedOnOwnTurn=True)
        self.value['behaviors'] = [dict(playsSourceToBaseAsUnit=True,
            unitEventAbility=copy.deepcopy(self.value['unitEventAbility']))]
        self.clause = audit.card_clauses(self.value)[-1]

    def outcome(self):
        return audit.source_play_condition_movement_route(self.value, self.clause, self.sources)

    def test_exact_clause_requires_both_original_conditions_and_live_shared_consumers(self):
        self.assertEqual('route', self.outcome()['status'])
        self.assertEqual('route', audit.audit_clause(self.value, self.clause, self.sources)['status'])

    def test_runtime_specification_rejects_missing_conditions_wrong_scalar_types_or_extra_effects(self):
        original = copy.deepcopy(self.value['unitEventAbility'])
        for key, value in [('requiresSourcePlayedFromStandby', False),
                           ('requiresSourcePlayedOnOwnTurn', False),
                           ('requiresSourcePlayedOnOwnTurn', 1),
                           ('optional', 1), ('optional', False), ('amount', 1), ('amount', 0.0),
                           ('timing', 'MOVE'), ('effect', 'MOVE_ENEMY'),
                           ('leadingPowerCost', 1), ('anyNumberOfTargets', True),
                           ('requiresSpellDuelAtUnitPlay', True)]:
            with self.subTest(key=key, value=value):
                event = dict(original, **{key: value})
                self.value['unitEventAbility'] = event
                self.value['behaviors'][0]['unitEventAbility'] = copy.deepcopy(event)
                self.assertEqual('missing', self.outcome()['status'])

    def test_runtime_top_level_and_profile_specifications_must_agree(self):
        self.value['behaviors'][0]['unitEventAbility']['requiresSourcePlayedOnOwnTurn'] = False
        self.assertEqual('missing', self.outcome()['status'])

    def test_unrelated_scope_category_or_printed_text_cannot_borrow_the_route(self):
        self.clause = dict(self.clause, scope='当你打出我时，移动一名敌方单位。')
        self.assertEqual('missing', self.outcome()['status'])
        self.clause = audit.card_clauses(self.value)[-1]
        self.value['cardCategory'] = 'spell'
        self.assertEqual('missing', self.outcome()['status'])
        self.value['cardCategory'] = 'hero_unit'
        self.value['cardEffect'] = '当你打出我时，移动一名敌方单位。'
        self.assertEqual('missing', self.outcome()['status'])
        self.assertIsNone(audit.source_play_condition_movement_route(self.value,
            audit.card_clauses(self.value)[0], self.sources))

    def test_native_play_must_be_targetless_and_cannot_add_damage_or_an_equipment_play(self):
        original = copy.deepcopy(self.value['behaviors'][0])
        for key, value in [('playsSourceToBaseAsUnit', False), ('playsSourceToBaseAsEquipment', True),
                           ('requiredTargetCount', 1), ('requiredTargetCount', False), ('damageAmount', 1)]:
            with self.subTest(key=key, value=value):
                self.value['behaviors'] = [dict(original, **{key: value})]
                self.assertEqual('missing', self.outcome()['status'])
        self.value['behaviors'] = [original, copy.deepcopy(original)]
        self.assertEqual('missing', self.outcome()['status'])

    def test_unused_decoys_cannot_replace_capture_validation_movement_or_original_history(self):
        for filename, token in [
            ('CoreRuleEngine.UnitEvents.cs', 'CaptureSourcePlayConditions(entered, player, observations)'),
            ('CoreRuleEngine.UnitEvents.cs', 'CaptureSourcePlayConditions(entered, player,'),
            ('CoreRuleEngine.SourcePlayConditions.cs', 'o.Controller == player'),
            ('CoreRuleEngine.SourcePlayConditions.cs', 'frame.TurnPlayerId == o.Controller'),
            ('CoreRuleEngine.UnitEvents.cs', 'ValidSourcePlayCondition(state, c, spec, source)'),
            ('StandbyRules.cs', 'generation == origin.SourceGeneration + 1'),
            ('CoreRuleEngine.UnitEvents.cs', 'IsUnitMovementDestination(state, c.ObjectId, destination)'),
            ('CoreRuleEngine.PermanentUnitEvents.cs', 'CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration)'),
            ('CoreRuleEngine.ObservedUnitEvents.cs', 'HasSourcePlayCondition('),
            ('CoreRuleEngine.ObservedUnitEvents.cs', 'context.SourcePlayCondition'),
            ('MatchRecovery.ObservedUnitEvents.cs', '!expected.SetEquals(current.Select(item => item.Id))'),
            ('MatchRecovery.cs', 'ValidateObservedUnitEventHistory(authoritativeState, commands, events, errors)'),
        ]:
            with self.subTest(filename=filename, token=token):
                path = 'src/Riftbound.Engine/' + filename
                original = self.sources.files[path]
                self.assertIn(token, original)
                self.sources.files[path] = original.replace(token, 'DISABLED') \
                    + '\n    private static void UnusedAuditDecoy() { ' + token + '; }\n'
                try:
                    self.assertEqual('missing', self.outcome()['status'])
                finally:
                    self.sources.files[path] = original
