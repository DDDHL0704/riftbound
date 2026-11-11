import copy
import unittest
from unittest.mock import patch
from test_effect_clause_audit import audit


class BattlefieldUnitPlayAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.after = audit.Sources(audit.ROOT, [])
        cls.before = copy.copy(cls.after)
        cls.before.files = dict(cls.after.files)
        cls.before.files['src/Riftbound.Engine/CoreRuleEngine.BattlefieldUnitPlayTriggers.cs'] = ''
        cls.cards = {}
        for no, text, node in [
            ('UNL-218/219', '当一名玩家在此处打出一名单位时，该玩家可以选择支付{{1}}，以此给予该单位{{增益}}。',
                dict(kind='BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON', timing='BATTLEFIELD_UNIT_PLAYED',
                    targetScope='PLAYED_UNIT_AT_THIS_BATTLEFIELD', manaCost=1, boonCount=1)),
            ('UNL-215/219', '每回合首次，当玩家在此处打出一名非指示物单位时，该玩家可以选择将自己在此处控制的另一名单位移动到其基地。',
                dict(kind='BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE', timing='BATTLEFIELD_UNIT_PLAYED',
                    targetScope='OTHER_CONTROLLED_UNIT_AT_THIS_BATTLEFIELD', moveCount=1,
                    moveDestination='OWNER_BASE', oncePerTurn=True, excludesTokens=True)),
        ]:
            cls.cards[no] = dict(cardNo=no, cardCategory='battlefield', cardEffect=text,
                triggers=[dict(node=dict(node, text=text))])

    def status(self, no, sources=None, card=None, clause=None):
        card = card or self.cards[no]
        clause = clause or audit.card_clauses(card)[0]
        value = audit.battlefield_unit_play_route(card, clause, sources or self.after)
        return value['status'] if value is not None else None

    def test_absent_independent_family_cannot_be_replaced_by_old_inline_paths(self):
        for no in self.cards:
            self.assertEqual('missing', self.status(no, self.before))

    def test_exact_official_runtime_specs_have_live_independent_consumers(self):
        for no in self.cards:
            self.assertEqual('route', self.status(no))

    def test_incorrect_runtime_kind_scope_quantity_and_typed_booleans_fail_closed(self):
        for no, fields in [
            ('UNL-218/219', [('manaCost', 0), ('manaCost', True), ('manaCost', '1'), ('manaCost', 1.0),
                            ('boonCount', 2), ('targetScope', 'ANY_UNIT')]),
            ('UNL-215/219', [('moveCount', True), ('moveCount', 2), ('oncePerTurn', 1), ('oncePerTurn', False),
                            ('excludesTokens', False), ('excludesTokens', 1.0), ('moveDestination', 'ANY_BATTLEFIELD')]),
        ]:
            for key, value in fields:
                with self.subTest(no=no, key=key, value=value):
                    card = copy.deepcopy(self.cards[no]); card['triggers'][0]['node'][key] = value
                    self.assertEqual('missing', self.status(no, card=card))
        for no in self.cards:
            card = copy.deepcopy(self.cards[no]); card['triggers'][0]['node']['extraUnimplementedPayment'] = 1
            self.assertEqual('missing', self.status(no, card=card))

    def test_other_categories_clause_locations_and_scopes_do_not_borrow_battlefield_evidence(self):
        for no in self.cards:
            card = copy.deepcopy(self.cards[no]); card['cardCategory'] = 'unit'
            self.assertEqual('missing', self.status(no, card=card))
            clause = audit.card_clauses(self.cards[no])[0]
            for key, value in [('kind', 'static'), ('location', 'armament'), ('scope', '当我进攻时')]:
                self.assertEqual('missing', self.status(no, clause=dict(clause, **{key: value})))

    def test_removed_live_consumers_cannot_be_replaced_by_an_unused_decoy(self):
        checks = [
            ('CoreRuleEngine.UnitEntryTriggers.cs', 'CaptureBattlefieldUnitPlay(ev, zones, cards, entered, player)'),
            ('CoreRuleEngine.BattleResultContinuation.cs', 'collected = QueueBattlefieldUnitPlays(state, collected)'),
            ('CoreRuleEngine.cs', 'AddBattlefieldUnitPlayReceipt(trigger, payload)'),
            ('CoreRuleEngine.FieldTriggers.cs', 'ResolveBattlefieldUnitPlay(state, item)'),
            ('CoreRuleEngine.BattlefieldUnitPlayTriggers.cs', 'BattlefieldTriggerSpecRules.TriggersForCard(cardNo)'),
            ('CoreRuleEngine.BattlefieldUnitPlayTriggers.cs', 'actor != player'),
            ('CoreRuleEngine.BattlefieldUnitPlayTriggers.cs', 'observation.Player, observation.Source, FieldTriggerEffect, ev.Kind'),
            ('MatchRecovery.cs', 'ValidateBattlefieldUnitPlayHistory(authoritativeState, commands, events, errors)'),
            ('MatchRecovery.BattlefieldUnitPlays.cs', 'state.PendingRuleChoice?.Origin'),
            ('MatchRecovery.BattlefieldUnitPlays.cs', 'command.Accepted && command.CompletedTick == entry.Tick'),
        ]
        for filename, token in checks:
            for no in self.cards:
                with self.subTest(no=no, token=token):
                    source = copy.copy(self.after); source.files = dict(self.after.files)
                    path = 'src/Riftbound.Engine/' + filename; self.assertIn(token, source.files[path])
                    source.files[path] = source.files[path].replace(token, 'REMOVED_LIVE_CONSUMER') \
                        + '\n    private static void UnusedBattlefieldAuditDecoy() { ' + token + '; }\n'
                    self.assertEqual('missing', self.status(no, source))

    def test_first_event_and_real_payment_consumers_are_required_for_their_exact_family(self):
        for no, filename, token in [
            ('UNL-215/219', 'CoreRuleEngine.BattlefieldUnitPlayTriggers.cs', 'if (!used.Add(marker)) continue'),
            ('UNL-215/219', 'CoreRuleEngine.BattlefieldUnitPlayTriggers.cs', 'token is null && ev.Kind != "UNIT_TOKEN_CREATED"'),
            ('UNL-215/219', 'MatchRecovery.BattlefieldUnitPlays.cs', 'turn != frame.TurnNumber'),
            ('UNL-215/219', 'MatchRecovery.BattlefieldUnitPlays.cs', 'CoreRuleEngine.BattlefieldUnitPlayTargetHash(selected)'),
            ('UNL-218/219', 'CoreRuleEngine.TriggerCosts.cs', 'item.FieldContext?.Kind == BattlefieldEntryBoonKind ? new(Mana: 1)'),
            ('UNL-218/219', 'CoreRuleEngine.BattlefieldUnitPlayTriggers.cs', 'unit.ObjectGeneration == origin.Unit.Generation'),
            ('UNL-218/219', 'CoreRuleEngine.BattlefieldUnitPlayTriggers.cs', 'GrantLegendBoon(cards, unit.ObjectId, item.ControllerId, item.SourceObjectId, context.Kind, events, zones)'),
        ]:
            with self.subTest(no=no, token=token):
                source = copy.copy(self.after); source.files = dict(self.after.files)
                path = 'src/Riftbound.Engine/' + filename; self.assertIn(token, source.files[path])
                source.files[path] = source.files[path].replace(token, 'REMOVED_EXACT_FAMILY_CONSUMER') \
                    + '\n    private static void UnusedBattlefieldFamilyDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(no, source))

    def test_ambiguous_declarations_fail_closed_and_unused_calls_are_not_declarations(self):
        source = copy.copy(self.after); source.files = dict(self.after.files)
        path = 'src/Riftbound.Engine/CoreRuleEngine.BattlefieldUnitPlayTriggers.cs'
        source.files[path] += '\n    private static bool QueueBattlefieldUnitPlays(MatchState state) => true;\n'
        for no in self.cards:
            self.assertEqual('missing', self.status(no, source))
        source.files[path] = self.after.files[path] + '\n    private static object UnusedWrapper() => QueueBattlefieldUnitPlays(null!, null!);\n'
        for no in self.cards:
            self.assertEqual('route', self.status(no, source))

    def test_a_reviewed_fallback_cannot_hide_a_removed_independent_consumer(self):
        for no, card in self.cards.items():
            with self.subTest(no=no), patch.object(audit, 'reviewed_clause_route',
                    return_value=audit.result('route', 'unrelated reviewed evidence')):
                clause = audit.card_clauses(card)[0]
                self.assertEqual('missing', audit.audit_clause(card, clause, self.before)['status'])


class SourcePlayHereUnitTokenAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = audit.Sources(audit.ROOT, [])

    def setUp(self):
        self.sources = copy.copy(self.original)
        self.sources.files = dict(self.original.files)

    @staticmethod
    def card():
        text = '当你打出我时，在此处额外打出一名1{{S}}的“随从”。'
        return dict(cardNo='OGN·211/298', cardCategory='unit', cardEffect=text,
            behaviors=[dict(playsSourceToBaseAsUnit=True, createdBaseUnitTokenCount=1)],
            triggers=[dict(node=dict(kind='SOURCE_UNIT_PLAYED_OR_SCORED_CREATE_UNIT', timing='SOURCE_UNIT_PLAYED',
                text=text, targetScope='SOURCE_UNIT', createdTokenCount=1, createdTokenPower=1,
                createdTokenName='随从', createdTokenDestination='SOURCE_LOCATION', createdTokenExhausted=True))])

    def status(self, card=None, clause=None):
        card = card or self.card()
        return audit.audit_clause(card, clause or audit.card_clauses(card)[0], self.sources)['status']

    def test_exact_here_spec_replaces_legacy_base_only_route(self):
        self.assertEqual('route', self.status())
        card = self.card(); card['triggers'] = []
        self.assertEqual('missing', self.status(card))

    def test_a_reviewed_fallback_cannot_hide_an_old_base_only_instruction(self):
        card = self.card(); card['triggers'] = []
        with patch.object(audit, 'reviewed_clause_route', return_value=audit.result('route', 'unrelated reviewed evidence')):
            self.assertEqual('missing', self.status(card))

    def test_wrong_destination_timing_quantity_shape_and_extra_costs_fail_closed(self):
        original = self.card()
        for key, value in [('kind', 'on-play'), ('timing', 'SOURCE_UNIT_PLAYED_OR_SCORED'),
                ('targetScope', 'ALL_FRIENDLY_UNITS'), ('createdTokenDestination', 'OWNER_BASE'),
                ('createdTokenCount', 2), ('createdTokenCount', True), ('createdTokenPower', '1'),
                ('createdTokenPower', 1.0), ('createdTokenName', '机器人'), ('createdTokenExhausted', False),
                ('createdTokenExhausted', 1), ('createdTokenKeywords', ['游走']), ('optional', True), ('manaCost', 1)]:
            with self.subTest(key=key, value=value):
                card = copy.deepcopy(original); card['triggers'][0]['node'][key] = value
                self.assertEqual('missing', self.status(card))

    def test_malformed_duplicate_and_mismatched_scope_specs_cannot_match(self):
        for entries in [None, {}, [], [None], ['registered'], [dict(node=None)],
                        self.card()['triggers'] * 2]:
            with self.subTest(entries=entries):
                card = self.card(); card['triggers'] = entries
                self.assertEqual('missing', self.status(card))
        for key, value in [('scope', '当我据守一处战场时'), ('kind', 'static'), ('location', 'armament')]:
            self.assertEqual('missing', audit.source_play_here_unit_token_route(self.card(),
                dict(audit.card_clauses(self.card())[0], **{key: value}), self.sources)['status'])

    def test_actual_factory_capture_independent_dispatch_location_and_no_duplicate_consumers_are_required(self):
        checks = [
            ('CoreRuleEngine.SourceUnitTokens.cs', 'f.DefaultPower == spec.CreatedTokenPower'),
            ('CoreRuleEngine.SourceUnitTokens.cs', 'new(source.ObjectId, source.ObjectGeneration)'),
            ('CoreRuleEngine.SourceUnitTokens.cs', 'CapturedTick = result.State.Tick'),
            ('CoreRuleEngine.UnitEntryTriggers.cs', 'CaptureSourcePlayTokens(ev, zones, cards, entered, player)'),
            ('CoreRuleEngine.BattleResultContinuation.cs', 'QueueSourceUnitTokens(state, result)'),
            ('CoreRuleEngine.cs', 'sourceTokens.Timing is TriggerTimings.SourceUnitPlayed or TriggerTimings.SourceUnitPlayedOrScored)\n            return false'),
            ('CoreRuleEngine.cs', 'if (stackItem.SourceUnitTokens is not null) return ResolveSourceUnitTokens(state, stackItem)'),
            ('CoreRuleEngine.SourceUnitTokens.cs', 'if (!TrySourceUnitTokenDestination(state, item, out var battlefield)) return NoopStackResolutionResult(state)'),
            ('CoreRuleEngine.SourceUnitTokens.cs', 'source.ObjectGeneration != context.Source.Generation'),
            ('CoreRuleEngine.SourceUnitTokens.cs', 'BattlefieldLocalRules.PreventsUnitPlay('),
            ('CoreRuleEngine.SourceUnitTokens.cs', 'battlefieldId: battlefield'),
        ]
        for filename, token in checks:
            with self.subTest(token=token):
                path = 'src/Riftbound.Engine/' + filename; original = self.sources.files[path]
                self.assertIn(token, original)
                self.sources.files[path] = original.replace(token, 'REMOVED_HERE_CONSUMER')
                self.sources.files[path] += '\n    private static void UnusedHereDecoy() { ' + token + '; }\n'
                try:
                    self.assertEqual('missing', self.status())
                finally:
                    self.sources.files[path] = original

    def test_the_runtime_selector_must_reach_the_parsed_source_instruction(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.SourceUnitTokens.cs'; original = self.sources.files[path]
        token = 'TriggerKinds.SourceUnitPlayedOrScoredCreateUnit or TriggerKinds.UnitBattlefieldHeldCreateUnitHere'
        self.assertIn(token, original)
        self.sources.files[path] = original.replace(token, 'TriggerKinds.Unimplemented')
        self.sources.files[path] += '\n    private static void Unused() { _ = "' + token + '"; }\n'
        self.assertEqual('missing', self.status())

    def test_ambiguous_here_destination_overloads_cannot_choose_a_convenient_match(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.SourceUnitTokens.cs'
        self.sources.files[path] += '\n    private static bool TrySourceUnitTokenDestination(MatchState state) => true;\n'
        self.assertEqual('missing', self.status())
