import copy
import unittest
from test_effect_clause_audit import audit

TEXT = '当我进攻时，你可以选择将自己任意数量的指示物单位移动到此战场。'

class FriendlyTokenMovementAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = audit.Sources(audit.ROOT, [])

    def setUp(self):
        self.sources = copy.copy(self.original)
        self.sources.files = self.original.files.copy()
        self.card = dict(cardNo='SFD·177/221', cardCategory='hero_unit', cardEffect=TEXT,
            behaviors=[dict(playsSourceToBaseAsUnit=True, unitEventAbility=dict(
                timing='ATTACK', effect='MOVE_FRIENDLY_UNIT_TOKENS_HERE', optional=True, anyNumberOfTargets=True))])

    def status(self):
        return audit.friendly_token_attack_movement_route(self.card, audit.card_clauses(self.card)[0], self.sources)['status']

    def missing_consumer(self, filename, token):
        path = 'src/Riftbound.Engine/' + filename
        original = self.sources.files[path]
        self.assertIn(token, original)
        self.sources.files[path] = original.replace(token, 'REMOVED_TOKEN_MOVEMENT_CONSUMER')
        self.sources.files[path] += '\n    private static void UnusedAuditDecoy() { ' + token + '; }\n'
        try: self.assertEqual('missing', self.status())
        finally: self.sources.files[path] = original

    def test_both_printings_match_the_same_live_generic_route(self):
        for no in ['SFD·177/221', 'SFD·177a/221']:
            self.card['cardNo'] = no
            self.assertEqual('route', self.status())
            self.assertEqual('route', audit.audit_clause(self.card, audit.card_clauses(self.card)[0], self.sources)['status'])

    def test_wrong_timing_effect_count_and_scalar_types_cannot_match(self):
        original = self.card['behaviors'][0]['unitEventAbility'].copy()
        for key, value in [('timing','DEFEND'), ('effect','MOVE_ENEMY_HERE'),
                ('optional',False), ('optional',1), ('anyNumberOfTargets',False), ('anyNumberOfTargets',1),
                ('amount',1), ('amount',False), ('amount',0.0), ('leadingPowerCost',1)]:
            with self.subTest(key=key,value=value):
                self.card['behaviors'][0]['unitEventAbility'] = dict(original, **{key:value})
                self.assertEqual('missing', self.status())

    def test_wrong_profile_and_clause_scope_do_not_borrow_the_route(self):
        clause = audit.card_clauses(self.card)[0]
        for key,value in [('kind','static'), ('scope','当我据守时')]:
            self.assertEqual('missing', audit.friendly_token_attack_movement_route(self.card,dict(clause,**{key:value}),self.sources)['status'])
        self.card['cardCategory']='spell';self.assertEqual('missing',self.status())
        self.card['cardCategory']='hero_unit';self.card['behaviors'] *= 2;self.assertEqual('missing',self.status())
        self.assertIsNone(audit.friendly_token_attack_movement_route(self.card,dict(clause,text=TEXT.replace('指示物','装备')),self.sources))

    def test_only_current_public_friendly_unit_tokens_can_be_targets(self):
        for token in ['IsFaceUpNonStandbyUnit(c) && IsTokenObject(c)',
                'EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == item.ControllerId',
                'IsUnitMovementDestination(state, c.ObjectId, tokenDestination)',
                'MovementRestrictionRules.CanMove(c, item.ControllerId)']:
            with self.subTest(token=token):self.missing_consumer('CoreRuleEngine.UnitEvents.cs',token)

    def test_actual_attack_producer_and_root_resolution_dispatch_are_required(self):
        for filename, token in [('CoreRuleEngine.UnitEvents.cs','CaptureUnitEvents(state.PlayerZones, state.CardObjects, attackers, "ATTACK")'),
                ('CoreRuleEngine.BattleStartRoles.cs','CaptureAttackUnitEvents(state, firstAttackers,'),
                ('CoreRuleEngine.cs','if (stackItem.UnitEvent is not null) return ResolveUnitEvent(state, stackItem)'),
                ('CoreRuleEngine.PermanentUnitEvents.cs','return ResolveFriendlyUnitTokenMovement(state, item)')]:
            with self.subTest(token=token):self.missing_consumer(filename,token)

    def test_arbitrary_target_count_requires_unique_original_generation_bindings(self):
        for token in ['AnyNumberOfTargets ? legal.Length : 1',
                'item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count',
                'item.TargetGenerations.Count == item.TargetObjectIds.Count']:
            with self.subTest(token=token):self.missing_consumer('CoreRuleEngine.UnitEvents.cs',token)
        self.missing_consumer('CoreRuleEngine.FieldTriggers.cs','TargetGenerations = selected.ToDictionary')

    def test_resolution_must_read_current_source_location_and_recheck_surviving_targets(self):
        for filename,token in [('CoreRuleEngine.PermanentUnitEvents.cs','CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration)'),
                ('CoreRuleEngine.CombatMight.cs','source.ObjectGeneration == generation'),
                ('CoreRuleEngine.TokenUnitMovement.cs','UnitEventTargets(state, item).ToHashSet(StringComparer.Ordinal)'),
                ('CoreRuleEngine.TokenUnitMovement.cs','StackObjectRules.ReferencesCurrentObject(state, item, id)'),
                ('CoreRuleEngine.TokenUnitMovement.cs','if (destination is null)'),
                ('CoreRuleEngine.TokenUnitMovement.cs','if (selected.Length == 0)')]:
            with self.subTest(token=token):self.missing_consumer(filename,token)

    def test_a_single_batch_requires_shared_fees_simultaneous_observation_and_gear_follow(self):
        self.missing_consumer('CoreRuleEngine.TokenUnitMovement.cs','item, selected, destination, events)')
        for token in ['PayImposedMovementCost(', 'CurrentMovers, events)', 'CaptureMovementBatchObservers(',
                'MoveAttachedEquipmentWithHost(', '["simultaneousSourceObjectIds"] = movable']:
            with self.subTest(token=token):self.missing_consumer('CoreRuleEngine.ChosenMovement.cs',token)

    def test_original_attack_capture_and_token_departure_are_required(self):
        for filename, token in [('CoreRuleEngine.BattlefieldTimingAliases.cs', 'UnitEventTimingMatches(unitEvent.Timing, "ATTACK")'),
                ('CoreRuleEngine.cs', 'TimingCaptureHash(timingContext, trigger.TimingAlias)'),
                ('MatchRecovery.BattlefieldTimingAliases.cs', 'hash != CoreRuleEngine.TimingCaptureHash(item.Context, item.Alias)'),
                ('MatchRecovery.BattlefieldTimingAliases.cs', '"TOKEN_CEASED_TO_EXIST"'),
                ('MatchRecovery.BattlefieldTimingAliases.cs', 'CoreRuleEngine.ValidUnitEventTargets(stack)')]:
            with self.subTest(token=token):self.missing_consumer(filename,token)

    def test_original_target_confirmation_cannot_be_replaced_by_coherent_views(self):
        for filename, token in [('CoreRuleEngine.FieldTriggers.cs', '["targetGenerations"] = bound.TargetGenerations'),
                ('CoreRuleEngine.FieldTriggers.cs', '["targetGenerations"] = confirmed.TargetGenerations'),
                ('MatchRecovery.UnitEventTargets.cs', 'CurrentGameAcceptedEventBoundary(state, commands, events)'),
                ('MatchRecovery.UnitEventTargets.cs', 'frame.PendingEffectPlay?.Parent'),
                ('MatchRecovery.UnitEventTargets.cs', 'targets.SequenceEqual(item.TargetObjectIds)'),
                ('MatchRecovery.UnitEventTargets.cs', 'MatchStateHasher.HashValue(generations) == MatchStateHasher.HashValue(item.TargetGenerations)'),
                ('MatchRecovery.UnitEventTargets.cs', 'confirmations.Length != 1'),
                ('MatchRecovery.UnitEventTargets.cs', 'command.Accepted'),
                ('MatchRecovery.UnitEventTargets.cs', 'command.PlayerId == item.ControllerId'),
                ('MatchRecovery.UnitEventTargets.cs', 'command.StartedEventSequence < confirmations[0].Sequence'),
                ('MatchRecovery.UnitEventTargets.cs', 'command.CompletedEventSequence >= confirmations[0].Sequence'),
                ('MatchRecovery.cs', 'ValidateUnitEventTargetHistory(authoritativeState, commands, events, errors)')]:
            with self.subTest(token=token):self.missing_consumer(filename,token)
