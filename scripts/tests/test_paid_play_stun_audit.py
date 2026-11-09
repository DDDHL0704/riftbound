import copy
import unittest

from test_effect_clause_audit import audit, card

FEE = '你可以选择支付{{绿色}}，作为打出我的额外费用。'
STUN = '当你打出我时，如果你支付了该额外费用，则{{眩晕}}一名敌方单位。（使其在本回合内无法造成战斗伤害。）'
HOLD = '当我据守一处战场时，则你本回合下一次打出一名单位时，让其变为活跃状态，并给予其{{增益}}。'


class PaidPlayStunAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = audit.Sources(audit.ROOT, [])

    def setUp(self):
        self.sources = copy.copy(self.original)
        self.sources.files = self.original.files.copy()
        self.value = card('\n'.join([FEE, STUN, HOLD]))
        self.value['cardNo'] = 'UNL-052/219'
        self.value['staticAuras'] = []
        self.value['behaviors'] = [dict(
            manaCost=3, drawRecipientKind='CONTROLLER', mainDeckSelectionIsDraw=True,
            sourceUnitPower=3, sourceUnitIsExhausted=True, counteredStackItemDestination='GRAVEYARD',
            effectPlayDestination='ANY', effectPlayMaximumCount=1,
            playsSourceToBaseAsUnit=True, requiredTargetCount=1, statusEffectId='STUNNED',
            targetScope='ENEMY_UNIT', targetScopeIsExplicit=True, targetRequiredTag='CARD_TYPE:UNIT',
            targetEffectAdditionalPowerCost=1, targetEffectAdditionalPowerTrait='green')]

    def outcomes(self):
        return [audit.paid_target_play_stun_route(self.value, clause, self.sources)
                for clause in audit.card_clauses(self.value)[:2]]

    def require_missing(self, filename, token):
        path = 'src/Riftbound.Engine/' + filename
        original = self.sources.files[path]
        self.assertIn(token, original)
        self.sources.files[path] = original.replace(token, 'DISABLED')
        # An unused helper containing the old token cannot certify a live route.
        self.sources.files[path] += '\n    private static void UnusedAuditDecoy() { ' + token + '; }\n'
        try:
            self.assertEqual(['missing', 'missing'], [x['status'] for x in self.outcomes()])
        finally:
            self.sources.files[path] = original

    def test_both_clauses_require_the_actual_cost_capture_target_and_recovery_paths(self):
        self.assertEqual(['route', 'route'], [x['status'] for x in self.outcomes()])
        for clause in audit.card_clauses(self.value)[:2]:
            self.assertEqual('route', audit.audit_clause(self.value, clause, self.sources)['status'])

    def test_the_unimplemented_hold_reward_cannot_borrow_the_paid_stun_route(self):
        clause = audit.card_clauses(self.value)[2]
        self.assertIsNone(audit.paid_target_play_stun_route(self.value, clause, self.sources))
        self.assertEqual('missing', audit.audit_clause(self.value, clause, self.sources)['status'])

    def test_an_unrelated_hold_trigger_does_not_hide_the_two_paid_play_clauses(self):
        self.value['behaviors'][0]['unitEventAbility'] = dict(timing='HOLD', effect='CREATE_TURN_UNIT_PLAY_GRANT')
        self.assertEqual(['route', 'route'], [x['status'] for x in self.outcomes()])
        for timing in ['PLAY', 'PLAY_OR_HOLD', 'HOLD_OR_PLAY']:
            with self.subTest(timing=timing):
                self.value['behaviors'][0]['unitEventAbility']['timing'] = timing
                self.assertEqual(['missing', 'missing'], [x['status'] for x in self.outcomes()])

    def test_wrong_color_amount_or_scalar_type_cannot_certify_payment(self):
        original = self.value['behaviors'][0].copy()
        for key, changed in [('targetEffectAdditionalPowerTrait', 'blue'),
                             ('targetEffectAdditionalPowerCost', 2), ('targetEffectAdditionalPowerCost', True),
                             ('targetEffectAdditionalPowerCost', 1.0), ('targetEffectAdditionalPowerCost', '1')]:
            with self.subTest(key=key, changed=changed):
                self.value['behaviors'] = [dict(original, **{key: changed})]
                self.assertEqual(['missing', 'missing'], [x['status'] for x in self.outcomes()])

    def test_wrong_target_or_effect_and_extra_behaviors_do_not_match(self):
        original = self.value['behaviors'][0].copy()
        for key, changed in [('requiredTargetCount', 2), ('requiredTargetCount', True),
                             ('targetScope', 'BATTLEFIELD_UNIT'), ('targetScopeIsExplicit', False),
                             ('targetRequiredTag', 'CARD_TYPE:EQUIPMENT'), ('statusEffectId', 'DISARMED'),
                             ('damageAmount', 1), ('minTargetCount', 1), ('minTargetCount', False),
                             ('targetEffectAdditionalManaCost', 1), ('targetEffectAdditionalManaCost', 0.0),
                             ('playsSourceToBaseAsUnit', False), ('playsSourceToBaseAsEquipment', True),
                             ('unitEventAbility', {'effect': 'DRAW', 'timing': 'PLAY'})]:
            with self.subTest(key=key, changed=changed):
                self.value['behaviors'] = [dict(original, **{key: changed})]
                self.assertEqual(['missing', 'missing'], [x['status'] for x in self.outcomes()])
        self.value['behaviors'] = [original, original.copy()]
        self.assertEqual(['missing', 'missing'], [x['status'] for x in self.outcomes()])

    def test_the_full_paired_text_and_exact_clause_scope_are_required(self):
        clauses = audit.card_clauses(self.value)
        for index in [0, 1]:
            with self.subTest(index=index):
                clause = dict(clauses[index], scope='当我据守时，抽一张牌。')
                self.assertEqual('missing', audit.paid_target_play_stun_route(self.value, clause, self.sources)['status'])
        self.value['cardCategory'] = 'spell'
        self.assertEqual(['missing', 'missing'], [x['status'] for x in self.outcomes()])
        self.value['cardCategory'] = 'hero_unit'
        self.value['cardEffect'] = FEE
        self.assertEqual('missing', self.outcomes()[0]['status'])

    def test_cost_selection_must_reach_the_live_play_plan(self):
        for filename, token in [('CoreRuleEngine.cs', 'TryBuildOptionalCostPlan('),
                                ('CoreRuleEngine.cs', 'TryBuildTargetEffectAdditionalCost('),
                                ('CoreRuleEngine.cs', 'normalizedOptionalCosts.Count != expectedCostCount'),
                                ('CoreRuleEngine.cs', 'spendPowerCost == behavior.TargetEffectAdditionalPowerCost'),
                                ('CoreRuleEngine.cs', 'RuneTrait.Normalize(behavior.TargetEffectAdditionalPowerTrait)')]:
            with self.subTest(token=token): self.require_missing(filename, token)

    def test_paid_play_must_capture_a_live_independent_skill(self):
        for filename, token in [('CoreRuleEngine.PermanentConfirmation.cs',
                                 '!HasTargetEffectAdditionalCost(behavior) || TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)'),
                                ('CoreRuleEngine.PermanentConfirmation.cs', 'SourceConfirmed = true'),
                                ('CoreRuleEngine.PermanentConfirmation.cs', 'Append(ability)'),
                                ('CoreRuleEngine.PermanentPlayTargets.cs', 'MinTargetCount = current.RequiredTargetCount')]:
            with self.subTest(token=token): self.require_missing(filename, token)

    def test_target_confirmation_must_be_dispatched_and_bind_generation_and_ward(self):
        for filename, token in [('CoreRuleEngine.cs', 'result = ResolveChooseCardsRuntime(state, intent, command);'),
                                ('CoreRuleEngine.cs', 'pendingChoice.ChoiceWindow == "TRIGGER_CONFIRMATION"'),
                                ('CoreRuleEngine.FieldTriggers.cs', 'ValidPermanentPlayTargetSelection(state, item, selected)'),
                                ('CoreRuleEngine.FieldTriggers.cs', 'ResolveSpellshieldTargetTaxPower('),
                                ('CoreRuleEngine.FieldTriggers.cs', 'TargetGenerations = selected.ToDictionary')]:
            with self.subTest(token=token): self.require_missing(filename, token)

    def test_actual_stun_consumer_and_stun_observers_are_required(self):
        for token in ['ResolveStatusEffectIds(behavior', 'UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects',
                      'statusEffectId == "STUNNED"', 'CaptureStunObservers(applied,']:
            with self.subTest(token=token): self.require_missing('CoreRuleEngine.cs', token)

    def test_accepted_completion_must_produce_the_original_payment_receipt(self):
        for filename, token in [('CoreRuleEngine.cs', 'return ValueTask.FromResult(RecordPaidPermanentPlayHistory(state, completed));'),
                                ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'PaidPermanentPlaySkills(before)'),
                                ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'PaidPermanentPlaySkills(result.State)'),
                                ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'PaidPermanentPlayChoicesHash(item)'),
                                ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'events.Add(new(')]:
            with self.subTest(token=token): self.require_missing(filename, token)

    def test_original_cost_choice_hash_and_suspended_parent_liveness_are_required(self):
        for token in ['item.OptionalCosts', 'item.ControllerId', 'item.SourceObjectId',
                      'state.PendingRuleChoice?.Origin', 'frame.PendingEffectPlay?.Parent']:
            with self.subTest(token=token): self.require_missing('CoreRuleEngine.PaidPermanentPlayHistory.cs', token)

    def test_recovery_requires_original_accepted_command_interval_and_current_game(self):
        for token in ['CurrentGameAcceptedEventBoundary(state, commands, events)', 'command.Accepted',
                      'command.CompletedTick == record.Tick', 'command.StartedEventSequence < record.Sequence',
                      'command.CompletedEventSequence >= record.Sequence']:
            with self.subTest(token=token): self.require_missing('MatchRecovery.PaidPermanentPlayHistory.cs', token)

    def test_recovery_must_bind_current_cost_and_reject_deleted_or_resurrected_skills(self):
        for token in ['!captured.TryAdd(', '!pending.Add(', '!pending.Remove(',
                      'CoreRuleEngine.ValidPaidPermanentPlayChoices(item)',
                      'proof != CoreRuleEngine.PaidPermanentPlayChoicesHash(item)', '!pending.SetEquals(']:
            with self.subTest(token=token): self.require_missing('MatchRecovery.PaidPermanentPlayHistory.cs', token)
        self.require_missing('MatchRecovery.cs',
                             'ValidatePaidPermanentPlayHistory(authoritativeState, commands, events, errors)')
