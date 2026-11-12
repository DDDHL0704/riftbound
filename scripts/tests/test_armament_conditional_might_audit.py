import copy
import unittest
from unittest.mock import patch
from test_effect_clause_audit import audit


class ConditionalArmamentMightAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = audit.Sources(audit.ROOT, [])

    def setUp(self):
        self.sources = copy.copy(self.original)
        self.sources.files = dict(self.original.files)

    @staticmethod
    def card(kind='attached'):
        text, amount, condition, threshold = {
            'attached': ('如果此牌是在本回合贴附到我身上的，则我额外获得{{S}}+2。', 2, 'ATTACHED_THIS_TURN', 0),
            'level': ('{{等级3}}我额外获得{{S}}+1。（如果你拥有不少于3经验，则获得该效果。）', 1, 'CONTROLLER_EXPERIENCE_AT_LEAST', 3),
            'here': ('如果我所在的战场上受你控制的其他单位有且仅有一个，则我获得{{S}}+2。', 2, 'EXACT_OTHER_FRIENDLY_UNITS_HERE', 1),
        }[kind]
        return dict(cardNo='FIXTURE', cardCategory='equipment', cardEffect='',
            armamentText=dict(lowerText=text), armament=dict(effectText=text,
                conditionalMight=dict(amount=amount, condition=condition, threshold=threshold)))

    def status(self, card=None, clause=None):
        card = card or self.card()
        clause = clause or audit.card_clauses(card)[-1]
        return audit.audit_clause(card, clause, self.sources)['status']

    def remove_consumer(self, kind, filename, token):
        path = 'src/Riftbound.Engine/' + filename
        original = self.sources.files[path]
        self.assertIn(token, original)
        self.sources.files[path] = original.replace(token, 'REMOVED_CONDITIONAL_CONSUMER')
        self.sources.files[path] += '\n    private static void UnusedAuditDecoy() { ' + token + '; }\n'
        try:
            self.assertEqual('missing', self.status(self.card(kind)))
        finally:
            self.sources.files[path] = original

    def test_three_precise_lower_conditions_use_live_additive_might(self):
        for kind in ['attached', 'level', 'here']:
            with self.subTest(kind=kind):
                self.assertEqual('route', self.status(self.card(kind)))

    def test_a_runtime_field_alone_or_wrong_numeric_type_never_proves_a_clause(self):
        for kind in ['attached', 'level', 'here']:
            original = self.card(kind)
            for key, value in [('amount', 9), ('amount', True), ('amount', 1.0), ('amount', '2'),
                               ('condition', 'ALWAYS'), ('threshold', False), ('threshold', '3'),
                               ('threshold', 3.0), ('unmappedRequirement', True)]:
                with self.subTest(kind=kind, key=key, value=value):
                    card = copy.deepcopy(original)
                    card['armament']['conditionalMight'][key] = value
                    self.assertEqual('missing', self.status(card))
            card = copy.deepcopy(original)
            card['armament'].pop('conditionalMight')
            self.assertEqual('missing', self.status(card))

    def test_mismatched_host_text_scope_kind_or_category_stays_missing(self):
        card = self.card()
        for key, value in [('scope', '当你打出此牌时'), ('kind', 'keyword')]:
            self.assertEqual('missing', self.status(card, dict(audit.card_clauses(card)[-1], **{key: value})))
        card['armament']['effectText'] = '我获得{{S}}+2。'
        self.assertEqual('missing', self.status(card))
        card = self.card(); card['cardCategory'] = 'unit'
        self.assertEqual('missing', self.status(card))

    def test_host_public_attachment_controller_and_actual_might_consumers_are_required(self):
        for kind in ['attached', 'level', 'here']:
            for filename, token in [
                ('CoreRuleEngine.cs', '+ ArmamentConditionalMightContributions(state, zones, card).Sum(c => c.Amount)'),
                ('CoreRuleEngine.ArmamentConditionalMight.cs', 'gear.AttachedToObjectId == host.ObjectId'),
                ('CoreRuleEngine.ArmamentConditionalMight.cs', '!IsObjectOnField(zones, host.ObjectId)'),
                ('CoreRuleEngine.ArmamentConditionalMight.cs', 'EffectiveFieldControllerId(zones, host.ObjectId, host)'),
            ]:
                with self.subTest(kind=kind, token=token):
                    self.remove_consumer(kind, filename, token)

    def test_level_and_exact_here_predicates_cannot_be_replaced_by_unused_decoys(self):
        for kind, token in [('level', 'state.PlayerExperience.GetValueOrDefault(controller) >= bonus.Threshold'),
                ('here', 'other.ObjectId != host.ObjectId'),
                ('here', 'EffectiveFieldControllerId(zones, other.ObjectId, other) == controller'),
                ('here', 'at.BattlefieldObjectId == here.BattlefieldObjectId'),
                ('here', 'others.Length == bonus.Threshold')]:
            with self.subTest(token=token):
                self.remove_consumer(kind, 'CoreRuleEngine.ArmamentConditionalMight.cs', token)

    def test_attachment_needs_real_transition_cleanup_and_original_accepted_history(self):
        checks = [
            ('CoreRuleEngine.AttachmentObservers.cs', 'if (gear.AttachedToObjectId == unitId) return false'),
            ('CoreRuleEngine.AttachmentObservers.cs', 'payload[PrivateArmamentAttachmentTurn] = new ObjectBinding(gearId, gear.ObjectGeneration)'),
            ('CoreRuleEngine.BattleResultContinuation.cs', 'return RecordArmamentAttachmentTurnHistory(state, NormalizeTurnEffectMarkers('),
            ('CoreRuleEngine.ArmamentConditionalMight.cs', 'payload.Remove(PrivateArmamentAttachmentTurn)'),
            ('CoreRuleEngine.cs', 'UntilEndOfTurnEffects = []'),
            ('MatchRecovery.cs', 'ValidateArmamentAttachmentTurnHistory(authoritativeState, commands, events, errors)'),
            ('MatchRecovery.ArmamentConditionalMight.cs', 'state.PendingRuleChoice?.Origin'),
            ('MatchRecovery.ArmamentConditionalMight.cs', 'entry.Sequence > boundary && entry.Tick <= frame.Tick'),
            ('MatchRecovery.ArmamentConditionalMight.cs', 'command.Accepted && command.CompletedTick == entry.Tick'),
            ('MatchRecovery.ArmamentConditionalMight.cs', 'attached.Clear()'),
            ('MatchRecovery.ArmamentConditionalMight.cs', 'attached.Contains(CoreRuleEngine.ArmamentAttachmentTurnObject(card))'),
        ]
        for filename, token in checks:
            with self.subTest(token=token):
                self.remove_consumer('attached', filename, token)

    def test_calls_inside_unused_wrappers_and_ambiguous_declarations_cannot_claim_the_host_consumer(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.ArmamentConditionalMight.cs'
        original = self.sources.files[path]
        anchor = '    internal static IReadOnlyList<ArmamentConditionalMightContribution> ArmamentConditionalMightContributions('
        self.assertIn(anchor, original)
        token = 'gear.AttachedToObjectId == host.ObjectId'
        decoy = '    private static void UnusedWrapper() { _ = ArmamentConditionalMightContributions(null!, null!, null!); _ = "' + token + '"; }\n'
        self.sources.files[path] = original.replace(anchor, decoy + anchor)
        # Keep the wrapper's string while removing the actual predicate.
        start = self.sources.files[path].index(anchor)
        self.sources.files[path] = self.sources.files[path][:start] + self.sources.files[path][start:].replace(token, 'false')
        self.assertEqual('missing', self.status())
        self.sources.files[path] = original + '\n    internal static IReadOnlyList<ArmamentConditionalMightContribution> ArmamentConditionalMightContributions(MatchState state) => [];\n'
        self.assertEqual('missing', self.status())

    def test_unknown_lower_effects_and_upper_clauses_do_not_borrow_the_conditional_binding(self):
        card = self.card()
        row = audit.card_clauses(card)[-1]
        self.assertIsNone(audit.armament_conditional_might_route(card, dict(row, location='upper'), self.sources))
        card['armamentText']['lowerText'] = '如果我正在进攻，则我获得{{S}}+2。'
        card['armament']['effectText'] = card['armamentText']['lowerText']
        self.assertNotEqual('route', self.status(card))

    def test_a_reviewed_fallback_cannot_hide_a_missing_live_might_consumer(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.cs'
        self.sources.files[path] = self.sources.files[path].replace(
            '+ ArmamentConditionalMightContributions(state, zones, card).Sum(c => c.Amount)', '')
        with patch.object(audit, 'reviewed_clause_route', return_value=audit.result('route', 'unrelated reviewed evidence')):
            self.assertEqual('missing', self.status())
