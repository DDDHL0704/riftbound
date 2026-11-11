import copy
import unittest
from test_effect_clause_audit import audit

TEXT = '当我据守一处战场时，则你本回合下一次打出一名单位时，让其变为活跃状态，并给予其{{增益}}。'


class HeldNextUnitReadyBoonAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = audit.Sources(audit.ROOT, [])

    def setUp(self):
        self.sources = copy.copy(self.original)
        self.sources.files = self.original.files.copy()
        self.card = dict(cardNo='UNL-052/219', cardCategory='hero_unit', cardEffect=TEXT,
            behaviors=[dict(playsSourceToBaseAsUnit=True, turnUnitPlayGrantKind='PLAY_READY_BOON',
                turnUnitPlayGrantNextOnly=True, unitEventAbility=dict(timing='HOLD', effect='CREATE_TURN_UNIT_PLAY_GRANT'))])

    def status(self):
        return audit.held_next_unit_ready_boon_route(self.card, audit.card_clauses(self.card)[0], self.sources)['status']

    def missing_consumer(self, filename, token):
        path = 'src/Riftbound.Engine/' + filename
        original = self.sources.files[path]
        self.assertIn(token, original)
        self.sources.files[path] = original.replace(token, 'REMOVED_HELD_UNIT_GRANT_CONSUMER')
        self.sources.files[path] += '\n    private static void UnusedAuditDecoy() { ' + token + '; }\n'
        try: self.assertEqual('missing', self.status())
        finally: self.sources.files[path] = original

    def test_live_route_replaces_only_the_exact_held_sentence(self):
        self.assertEqual('route', self.status())
        self.assertEqual('route', audit.audit_clause(self.card, audit.card_clauses(self.card)[0], self.sources)['status'])
        clause = audit.card_clauses(self.card)[0]
        self.assertIsNone(audit.held_next_unit_ready_boon_route(self.card,dict(clause,text=TEXT.replace('据守','征服')),self.sources))
        for key,value in [('kind','static'),('scope','当你打出我时')]:
            self.assertEqual('missing',audit.held_next_unit_ready_boon_route(self.card,dict(clause,**{key:value}),self.sources)['status'])
        self.card['cardCategory']='spell';self.assertEqual('missing',self.status())

    def test_wrong_timing_effect_and_additional_conditions_cannot_match(self):
        original = self.card['behaviors'][0]['unitEventAbility'].copy()
        for key,value in [('timing','PLAY'),('timing','HOLD_OR_CONQUEST'),('effect','READY_SELF'),
                ('amount',1),('amount',False),('amount',0.0),('optional',True),('leadingManaCost',1),
                ('anyNumberOfTargets',True),('requiredFriendlyUnitTagsForScore',['鸟类'])]:
            with self.subTest(key=key,value=value):
                self.card['behaviors'][0]['unitEventAbility']=dict(original,**{key:value})
                self.assertEqual('missing',self.status())

    def test_replacement_entry_ready_is_not_a_delayed_ready_and_boon_effect(self):
        original=copy.deepcopy(self.card['behaviors'][0])
        for key,value in [('turnUnitPlayGrantKind','READY_ENTRY'),('turnUnitPlayGrantKind','PLAY_BOON'),
                ('turnUnitPlayGrantNextOnly',False),('turnUnitPlayGrantNextOnly',1),
                ('turnUnitPlayGrantCount',2),('turnUnitPlayGrantCount',False),('turnUnitPlayGrantCount',0.0),
                ('turnUnitPlayGrantAnyBattlefield',True),('playsSourceToBaseAsUnit',False)]:
            with self.subTest(key=key,value=value):
                self.card['behaviors']=[dict(original,**{key:value})]
                self.assertEqual('missing',self.status())

    def test_actual_hold_capture_dispatch_and_no_target_shape_are_required(self):
        for filename,token in [('CoreRuleEngine.Hold.cs','CaptureUnitEvents(state.PlayerZones, state.CardObjects, selectedUnits, "HOLD")'),
                ('CoreRuleEngine.UnitEvents.cs','ValidUnitEventTurnPlayGrantStack(item)'),
                ('CoreRuleEngine.UnitEvents.cs','return ResolveUnitEventTurnPlayGrant(state, item)')]:
            with self.subTest(token=token):self.missing_consumer(filename,token)

    def test_ordinary_play_cannot_create_the_held_grant(self):
        self.missing_consumer('CoreRuleEngine.TurnUnitPlayGrants.cs','b.UnitEventAbility?.Effect == UnitEventTurnPlayGrantEffect')

    def test_real_unit_event_origin_and_captured_source_generation_are_required(self):
        for filename,token in [('CoreRuleEngine.UnitEventTurnPlayGrants.cs','new UnitEventTurnPlayGrantOrigin(context, item.TimingAlias)'),
                ('CoreRuleEngine.UnitEventTurnPlayGrants.cs','item.SourceObjectId, context.SourceGeneration'),
                ('CoreRuleEngine.UnitEventTurnPlayGrants.cs','context.SourceGeneration == grant.Source.Generation'),
                ('CoreRuleEngine.TurnUnitPlayGrants.cs','ReadUnitEventTurnPlayGrantOrigin(f[9])')]:
            with self.subTest(token=token):self.missing_consumer(filename,token)

    def test_creating_the_grant_must_not_require_a_current_field_source(self):
        path='src/Riftbound.Engine/CoreRuleEngine.UnitEventTurnPlayGrants.cs';original=self.sources.files[path]
        anchor='var origin = new UnitEventTurnPlayGrantOrigin(context, item.TimingAlias);'
        self.assertIn(anchor,original)
        for gate in ['if (!IsObjectOnField(state.PlayerZones, item.SourceObjectId)) return NoopStackResolutionResult(state);',
                'var current = CombatMightSourceLocation(state, item.SourceObjectId, context.SourceGeneration);']:
            with self.subTest(gate=gate):
                self.sources.files[path]=original.replace(anchor,gate+' '+anchor)
                self.assertEqual('missing',self.status())

    def test_first_actual_play_consumption_and_new_unit_incarnation_binding_are_required(self):
        for token in ['ActiveUnitPlayGrants(effects, controller, UnitEventReadyBoonGrantKind)',
                'ConsumedUnitPlayGrants.Contains(grant.Marker)', 'ConsumedUnitPlayGrants.Add(context.Grant.Marker)',
                'effects.Add(TurnUnitPlayGrantUseMarker(', 'Generation = Math.Max(context.Unit.Generation, old.ObjectGeneration + 1)',
                'unit.ObjectGeneration != c.Unit.Generation']:
            with self.subTest(token=token):self.missing_consumer('CoreRuleEngine.TurnUnitPlayGrants.cs',token)

    def test_independent_resolution_must_use_shared_ready_then_boon_in_order(self):
        for token in ['ApplyReadyState(unit, instruction','ApplyBoon(cards[unit.ObjectId]']:
            with self.subTest(token=token):self.missing_consumer('CoreRuleEngine.TurnUnitPlayGrants.cs',token)
        for token in ['ReadyByEffectPermittedByFieldRules(', 'CaptureReadiedObject(', 'TryGrantBoonForInstruction(']:
            with self.subTest(token=token):self.missing_consumer('CoreRuleEngine.cs',token)

    def test_malformed_inventory_profiles_cannot_crash_or_claim_a_route(self):
        for profiles in [None, {}, [], [None], ['registered'], [self.card['behaviors'][0], {}]]:
            with self.subTest(profiles=profiles):
                original = self.card['behaviors']
                self.card['behaviors'] = profiles
                try: self.assertEqual('missing', self.status())
                finally: self.card['behaviors'] = original

    def test_each_authoritative_and_public_ceased_source_consumer_is_required(self):
        path = 'src/Riftbound.Engine/MatchRecovery.cs'
        original = self.sources.files[path]
        names = ['ValidateAuthoritativeStateStackItemObjectReferences',
            'ValidateSpectatorSnapshotStackObjectReferences',
            'ValidateSnapshotTimingTriggerQueuePayloadValues',
            'ValidateSpectatorTriggerQueuePayloadValues']
        for name in names:
            with self.subTest(name=name):
                start = original.index(' ' + name + '(')
                # Select the declaration, not a prior call site.
                import re
                declaration = list(re.finditer(r'(?m)^    private static [^\n]+\b' + name + r'\(', original))[-1]
                start = declaration.start()
                following = re.search(r'(?m)^    private static ', original[declaration.end():])
                end = declaration.end() + following.start() if following else len(original)
                code = original[start:end]
                token = 'HasValidatedCeasedUnitEventTurnPlaySource('
                self.assertIn(token, code)
                self.sources.files[path] = original[:start] + code.replace(token, 'REMOVED_CEASED_GRANT_SOURCE(') + original[end:]
                self.sources.files[path] += '\n    private static void UnusedAuditDecoy() { ' + token + '); }\n'
                try: self.assertEqual('missing', self.status())
                finally: self.sources.files[path] = original

    def test_original_receipts_and_private_origin_projection_are_required(self):
        for filename,token in [('CoreRuleEngine.UnitEventTurnPlayGrants.cs','"UNIT_EVENT_TURN_PLAY_GRANT_CREATED"'),
                ('CoreRuleEngine.UnitEventTurnPlayGrants.cs','"UNIT_EVENT_TURN_PLAY_GRANT_USED"'),
                ('CoreRuleEngine.UnitEventTurnPlayGrants.cs','"UNIT_EVENT_TURN_PLAY_SKILL_CAPTURED"'),
                ('CoreRuleEngine.UnitEventTurnPlayGrants.cs','"UNIT_EVENT_TURN_PLAY_SKILL_RETIRED"'),
                ('CoreRuleEngine.cs','RecordUnitEventTurnPlayHistory(state,'),
                ('MatchRecovery.UnitEventTurnPlayGrants.cs','CurrentGameAcceptedEventBoundary(state, commands, events)'),
                ('MatchRecovery.UnitEventTurnPlayGrants.cs','expectedSkills.SetEquals('),
                ('MatchRecovery.UnitEventTurnPlayGrants.cs','CoreRuleEngine.TimingCaptureHash(context, origin.TimingAlias)'),
                ('MatchRecovery.cs','ValidateUnitEventTurnPlayHistory(authoritativeState, commands, events, errors)'),
                ('MatchSession/Projection.Timing.cs','UnitEventOrigin = null'),
                ('MatchSession/Projection.Timing.cs','"TURN_UNIT_PLAY_GRANT_HASH:" + MatchStateHasher.HashValue(context.Grant)')]:
            with self.subTest(token=token):self.missing_consumer(filename,token)

    def test_a_null_origin_cannot_borrow_the_ceased_source_exception(self):
        self.missing_consumer('MatchRecovery.UnitEventTurnPlayGrants.cs',
            'UnitEventOrigin?.Context?.TokenFactoryCardNo')

    def test_earned_skills_require_shared_ordering_and_actual_play_witness(self):
        for filename, token in [
                ('CoreRuleEngine.BattleResultContinuation.cs', 'DeferCompanionPermanentPlayOrdering(state, collected)'),
                ('CoreRuleEngine.UnitPlayMight.cs', 'trigger.TurnUnitPlayBoon?.Unit'),
                ('CoreRuleEngine.UnitPlayMight.cs', 'queue.Any(t => t.PermanentPlayStackItemId == item.StackItemId)'),
                ('CoreRuleEngine.UnitPlayMight.cs', 'if (enqueuedAtTick is not { } capturedTick) return false'),
                ('CoreRuleEngine.UnitPlayMight.cs', 'state.CardPlays.Resolved.Any(play => play.PlayId == item.StackItemId && play.Source == unit'),
                ('CoreRuleEngine.UnitPlayMight.cs', 'play.CardNo == item.CardNo && play.PlayerId == item.ControllerId && play.Tick == capturedTick'),
                ('CoreRuleEngine.UnitPlayMight.cs', 'ValidTurnUnitPlayBoon(state, boon'),
                ('PermanentPlayTriggerRecoveryRules.cs', 'CoreRuleEngine.ValidPermanentPlayCompanion(state, companion, item, trigger.EnqueuedAtTick)')]:
            with self.subTest(token=token):
                self.missing_consumer(filename, token)

    def test_unit_play_grants_must_be_collected_before_deferring_legacy_skills(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.BattleResultContinuation.cs'
        original = self.sources.files[path]
        first = 'collected = QueueTurnUnitPlayBoons(state, collected);'
        second = 'collected = DeferCompanionPermanentPlayOrdering(state, collected);'
        self.assertEqual(1, original.count(first))
        self.assertEqual(1, original.count(second))
        before, after = original.index(first), original.index(second)
        self.assertLess(before, after)
        self.assertEqual('route', self.status())
        self.sources.files[path] = (original[:before] + second
            + original[before + len(first):after] + first + original[after + len(second):])
        self.assertEqual('missing', self.status())
