from pathlib import Path
import shutil
import tempfile
import unittest

from test_effect_clause_audit import audit


class PaidPowerUnitPassiveAuditTests(unittest.TestCase):
    TEXT = '在本回合内，如果你至少支付了{{A}}{{A}}，则我获得{{S}}+2和{{游走}}。'
    FILES = ['PaymentCostRules.cs', 'CoreRuleEngine.PowerPayments.cs', 'CoreRuleEngine.ConditionalSourceUnits.cs',
             'CoreRuleEngine.cs', 'CoreRuleEngine.RuleDerivedKeywords.cs', 'CoreRuleEngine.RuleChoiceCommits.cs',
             'CoreRuleEngine.RuleChoices.cs', 'CoreRuleEngine.LastKnownMight.cs', 'MatchRecovery.PowerPayments.cs',
             'MatchRecovery.cs', 'CoreRuleEngine.MightyTransitions.cs', 'CoreRuleEngine.RuleChoiceResources.cs']

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.engine = self.root / 'src/Riftbound.Engine'
        self.engine.mkdir(parents=True)
        for name in self.FILES:
            shutil.copy(audit.ROOT / 'src/Riftbound.Engine' / name, self.engine / name)

    def card(self, text=TEXT, required=2, delta=2):
        return dict(cardNo='TEST', cardName='测试', cardCategory='hero_unit', cardEffect=text,
                    behaviors=[dict(playsSourceToBaseAsUnit=True,
                        conditionalSourceUnitConditionKind='CONTROLLER_PAID_POWER_THIS_TURN',
                        conditionalSourceUnitRequiredPaidPower=required,
                        conditionalSourceUnitPowerBonus=delta, conditionalSourceUnitTags='游走')])

    def outcome(self, card=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))['status']

    def replace(self, filename, old, new):
        path = self.engine / filename
        text = path.read_text()
        self.assertIn(old, text)
        path.write_text(text.replace(old, new))

    def test_the_live_shared_debit_and_conditional_consumers_cover_the_sentence(self):
        self.assertEqual('route', self.outcome())

    def test_amounts_are_read_from_the_sentence_rather_than_a_card_number(self):
        text = self.TEXT.replace('{{A}}{{A}}', '{{A}}{{A}}{{A}}').replace('+2', '+4')
        self.assertEqual('route', self.outcome(self.card(text, 3, 4)))

    def test_registration_with_a_wrong_threshold_bonus_keyword_or_condition_is_missing(self):
        for key, value in [('playsSourceToBaseAsUnit', False), ('conditionalSourceUnitRequiredPaidPower', 1),
                           ('conditionalSourceUnitPowerBonus', 1), ('conditionalSourceUnitTags', '急速'),
                           ('conditionalSourceUnitConditionKind', 'CONTROLLER_GAINED_EXPERIENCE_THIS_TURN')]:
            with self.subTest(key=key):
                card = self.card(); card['behaviors'][0][key] = value
                self.assertEqual('missing', self.outcome(card))

    def test_every_actual_consumer_is_required_even_with_a_registered_profile(self):
        for name in self.FILES:
            with self.subTest(name=name):
                path = self.engine / name; original = path.read_text()
                path.write_text('unrelated')
                self.assertEqual('missing', self.outcome())
                path.write_text(original)

    def test_discarding_the_debit_result_cannot_be_certified(self):
        self.replace('PaymentCostRules.cs', 'runePools[plan.PlayerId] = paidPower with',
                     'runePools[plan.PlayerId] = currentPool with')
        self.assertEqual('missing', self.outcome())

    def test_an_unused_counter_recorder_does_not_cover_accepted_or_paused_commands(self):
        self.replace('CoreRuleEngine.RuleChoices.cs', 'return RecordPowerPayments(state,', 'return IgnorePowerPayments(state,')
        self.assertEqual('missing', self.outcome())

    def test_an_unused_condition_helper_does_not_cover_the_live_contribution(self):
        self.replace('CoreRuleEngine.ConditionalSourceUnits.cs',
                     'ConditionalSourceUnitConditionApplies(s.Behavior, controller, state)',
                     'AlwaysApplies(s.Behavior, controller, state)')
        self.assertEqual('missing', self.outcome())

    def test_keywords_cannot_hide_a_missing_numeric_consumer(self):
        self.replace('CoreRuleEngine.cs',
                     'ConditionalSourceUnitContributions(state, card).Sum(contribution => contribution.PowerDelta)', '0')
        self.assertEqual('missing', self.outcome())

    def test_runtime_recovery_must_invoke_the_original_history_check(self):
        self.replace('MatchRecovery.cs', 'ValidatePowerPaymentHistory(authoritativeState, commands, events, errors)',
                     'IgnorePowerPaymentHistory(authoritativeState, commands, events, errors)')
        self.assertEqual('missing', self.outcome())

    def test_paid_resource_mighty_capture_requires_its_own_chronology(self):
        self.replace('CoreRuleEngine.RuleChoiceCommits.cs',
                     'RecordMightyTransitions(context, completed, beforeMight);\n                completed = StampNewTriggerQueueTimes(context, completed)',
                     'RecordMightyTransitions(context, completed, beforeMight)')
        self.assertEqual('missing', self.outcome())

    def test_resuming_the_parent_must_not_repeat_the_committed_crossing(self):
        self.replace('CoreRuleEngine.MightyTransitions.cs',
                     'if (step.AlreadyCaptured) previous = step.After',
                     'if (false) previous = step.After')
        self.assertEqual('missing', self.outcome())

    def test_mighty_payment_inputs_must_match_the_original_capture(self):
        self.replace('MatchRecovery.PowerPayments.cs', 'MightyPowerPaymentHistoryHash(capture.Context)',
                     'HashCurrentCounterInstead(capture.Context)')
        self.assertEqual('missing', self.outcome())

    def test_a_cost_event_without_the_new_actual_debit_cannot_certify_the_history(self):
        self.replace('CoreRuleEngine.PowerPayments.cs', '[PowerPaymentReceiptKey] = receipt',
                     '["ignoredCounter"] = receipt')
        self.assertEqual('missing', self.outcome())

    def test_the_recovery_reader_must_read_the_original_receipt_payload(self):
        self.replace('MatchRecovery.PowerPayments.cs',
                     'TryReadObjectValue(record.Event.Payload, CoreRuleEngine.PowerPaymentReceiptKey, out var payload)',
                     'ReadCurrentCounterOnly(state, out var payload)')
        self.assertEqual('missing', self.outcome())
