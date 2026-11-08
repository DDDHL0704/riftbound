import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('spell_mana_alternative_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '如果你在本回合消耗了不低于{{4}}的费用来打出一个法术，则你可以选择支付{{蓝色}}来将我打出。'


class SpellManaAlternativeCostAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card(no='UNL-089/219'):
        return dict(cardNo=no, cardCategory='hero_unit', cardName='烬', cardEffect=TEXT,
            behaviors=[dict(playsSourceToBaseAsUnit=True,
                spellManaAlternativeCost=dict(minimumPaidMana=4, power=1, traits=['blue']))])

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def test_both_printings_need_the_actual_confirmed_payment_and_shared_price(self):
        for no in ['UNL-089/219', 'UNL-089a/219']:
            self.assertEqual('route', self.status(self.card(no)))

    def test_wrong_threshold_price_trait_or_nonunit_registration_cannot_pass(self):
        for delta in [dict(minimumPaidMana=3), dict(minimumPaidMana=True), dict(power=0), dict(power=True),
                      dict(traits=['red']), dict(onlyResolved=True)]:
            with self.subTest(delta=delta):
                card = self.card(); card['behaviors'][0]['spellManaAlternativeCost'].update(delta)
                self.assertEqual('missing', self.status(card))
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.status(card))
        card = self.card(); del card['behaviors'][0]['spellManaAlternativeCost']
        self.assertEqual('missing', self.status(card))

    def test_actual_consumers_cannot_be_replaced_with_unused_same_text(self):
        consumers = [
            ('CoreRuleEngine.SpellManaAlternativeCost.cs', 'history.TurnNumber == state.TurnNumber'),
            ('CoreRuleEngine.SpellManaAlternativeCost.cs', 'play.PlayerId == player'),
            ('CoreRuleEngine.SpellManaAlternativeCost.cs', '(play.Types & 4) != 0'),
            ('CoreRuleEngine.SpellManaAlternativeCost.cs', 'mana >= minimum'),
            ('CoreRuleEngine.SpellManaAlternativeCost.cs', 'chosen.Length != 1'),
            ('CoreRuleEngine.CardPlayHistory.cs', 'confirmedItem?.PlayCost?.PaidMana'),
            ('CoreRuleEngine.CardPlayHistory.cs', 'PaidMana = confirmed?.PaidMana'),
            ('CoreRuleEngine.cs', 'behavior.ManaCost, plan.TotalManaCost) : null'),
            ('CoreRuleEngine.BattleResultContinuation.cs', 'collected = RecordCardPlayHistory(state, collected)'),
            ('CoreRuleEngine.cs', 'out behavior, out var costsWithoutSpellManaAlternative)'),
            ('CoreRuleEngine.PlayManaCost.cs', 'behavior.AppliedSpellManaAlternativeCost is not null'),
            ('PrintedPowerCostRules.cs', 'new(alternative.Power, alternative.Traits)'),
            ('CoreRuleEngine.PlayCostPreview.cs', 'plan.Behavior.AppliedSpellManaAlternativeCost is { } alternative'),
            ('MatchSession.cs', 'CoreRuleEngine.SpellManaAlternativePlayBehaviors(state, playerId, behavior)'),
            ('MatchSession/Prompts.Metadata.cs', 'PlayCardPromptBehaviorsForSource(state, playerId, objectId)'),
            ('SpellManaPaymentHistoryRules.cs', 'receipt.Mana != play.PaidMana'),
            ('SpellManaPaymentHistoryRules.cs', 'c.Accepted'),
            ('SpellManaPaymentHistoryRules.cs', 'command.CommandType == CommandTypes.RevealCard'),
            ('SpellManaPaymentHistoryRules.cs', 'receipt.TurnNumber != state.TurnNumber'),
            ('MatchRecovery.cs', 'SpellManaPaymentHistoryRules.ValidateOriginalPayments(authoritativeState, commands, events)'),
            ('MatchRecovery.cs', 'SpellManaPaymentHistoryRules.ValidateOriginalPayments(paymentState, recovery.Commands, recovery.Events)'),
        ]
        for filename, token in consumers:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_ACTUAL_CONSUMER')
                sources.files[path] += '\n    private static void UnusedPaymentDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(sources=sources))

    def test_resumed_payments_require_the_original_intent_and_each_accepted_decision(self):
        consumers = [
            ('CoreRuleEngine.CardPlayHistory.cs', 'Tick = RuleChoices.Value?.CompletionTick ?? result.State.Tick'),
            ('SpellManaPaymentHistoryRules.cs', 'ValidConfirmationOrigin(play, birth!, command, commands, events)'),
            ('SpellManaPaymentHistoryRules.cs', 'IsOriginalPlay(command, play, birth)'),
            ('SpellManaPaymentHistoryRules.cs', 'original.SourceObjectId == play.Source.ObjectId'),
            ('SpellManaPaymentHistoryRules.cs', 'original.CardNo == play.CardNo'),
            ('SpellManaPaymentHistoryRules.cs', 'next.StartedEventSequence != previous.CompletedEventSequence'),
            ('SpellManaPaymentHistoryRules.cs', 'next.CommandType != CommandTypes.PayCost'),
            ('SpellManaPaymentHistoryRules.cs', 'PaymentWindow: CoreRuleEngine.RuleChoiceWindow'),
            ('SpellManaPaymentHistoryRules.cs', 'requests.Length != 1 || Text(requests[0].Event, "choiceId") != answer.PaymentId'),
            ('SpellManaPaymentHistoryRules.cs', 'Text(requests[0].Event, "playerId") != next.PlayerId'),
            ('SpellManaPaymentHistoryRules.cs', 'IsScenarioPreparation(completed, events)'),
            ('SpellManaPaymentHistoryRules.cs', 'command.TypedCommand?.TryRestore(out var typed)'),
            ('SpellManaPaymentHistoryRules.cs', 'Text(seeded[0].Event, "scenarioId") == scenario'),
        ]
        for filename, token in consumers:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_CONFIRMATION_LINK')
                sources.files[path] += '\n    private static void UnusedContinuationDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(sources=sources))

    def test_other_sentences_or_inherited_trigger_scopes_are_not_certified(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        self.assertEqual('missing', audit.audit_clause(card, dict(clause, scope='当我进攻时'), self.sources)['status'])
        for text in [TEXT.replace('一个法术', '任意卡牌'), TEXT.replace('本回合', '本局游戏')]:
            clause = dict(clause, text=text, scope=text)
            self.assertIsNone(audit.confirmed_spell_mana_alternative_route(card, clause, self.sources))
