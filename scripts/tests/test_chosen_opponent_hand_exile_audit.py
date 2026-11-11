import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('hand_exile_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
SENTENCES = ['当你打出我时，选择一名对手。', '让该对手展示自己的手牌。',
             '选择一张对手以此方式展示的卡牌，将其放逐。',
             '当该对手据守一处战场时，让这张被放逐的牌返回其手牌（即使我已经不在场上）。']


class ChosenOpponentHandExileAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        event = dict(timing='PLAY', effect='BANISH_CHOSEN_OPPONENT_HAND_UNTIL_HOLD',
                     requiresChosenOpponent=True, nextOpponentHoldEffect='RETURN_CAPTURED_HAND_EXILE')
        return dict(cardNo='UNL-169/219', cardCategory='hero_unit', cardEffect=''.join(SENTENCES),
                    behaviors=[dict(playsSourceToBaseAsUnit=True, unitEventAbility=copy.deepcopy(event))],
                    unitEventAbility=event)

    def statuses(self, card=None, sources=None):
        card = card or self.card()
        return [audit.audit_clause(card, clause, sources or self.sources)['status']
                for clause in audit.card_clauses(card)]

    def test_every_sentence_needs_the_real_reveal_banish_and_independent_return_chain(self):
        self.assertEqual(['route'] * 4, self.statuses())

    def test_a_shared_choose_opponent_sentence_does_not_reclassify_unrelated_cards(self):
        card = self.card(); card['cardNo'] = 'UNL-130/219'
        card['cardEffect'] = SENTENCES[0] + '该对手在其基地打出一名单位指示物。'
        clause = audit.card_clauses(card)[0]
        self.assertIsNone(audit.chosen_opponent_hand_exile_route(card, clause, self.sources))

    def test_base_registration_or_token_only_opponent_support_cannot_certify_the_hand_skill(self):
        for flag in [False, 1, 1.0, 'true', None]:
            with self.subTest(flag=flag):
                card = self.card(); card['unitEventAbility']['requiresChosenOpponent'] = flag
                self.assertEqual(['missing'] * 4, self.statuses(card))
        for key, value in [('effect', 'DRAW'), ('timing', 'HOLD'), ('nextOpponentHoldEffect', 'MOVE_ENEMY')]:
            with self.subTest(key=key):
                card = self.card(); card['unitEventAbility'][key] = value
                self.assertEqual(['missing'] * 4, self.statuses(card))
        card = self.card(); card['unitEventAbility']['unitTokenPlayedByChosenOpponent'] = True
        del card['unitEventAbility']['requiresChosenOpponent']
        self.assertEqual(['missing'] * 4, self.statuses(card))
        card = self.card(); card['behaviors'][0]['unitEventAbility']['effect'] = 'DRAW'
        self.assertEqual(['missing'] * 4, self.statuses(card))

    def test_removing_a_consumer_cannot_be_hidden_in_an_unused_method_or_comment(self):
        for filename, token in [
            ('CoreRuleEngine.cs', 'if (stackItem.UnitEvent is not null) return ResolveUnitEvent(state, stackItem)'),
            ('CoreRuleEngine.InformationPermissions.cs', 'SelectedUnitEventSpec(unit)?.Effect == BanishChosenOpponentHandEffect'),
            ('CoreRuleEngine.TurnSequence.cs', 'QueueOpponentHoldUnitEvents(state, heldFields, events)'),
            ('CoreRuleEngine.RevealedHandChoices.cs', 'BanishRevealedHandCard('),
            ('CoreRuleEngine.RevealedHandChoices.cs', 'context.OwnerId != RevealedHandOwner(state, top)'),
            ('CoreRuleEngine.HandExileReturn.cs', 'card.ObjectGeneration != exile.Card.Generation'),
            ('CoreRuleEngine.ObjectContinuity.cs', 'RetireDepartedScheduledUnitEvents('),
            ('MatchRecovery.UnitEventHandChoices.cs', 'originalHash != MatchStateHasher.HashValue(actual.HandContext)'),
            ('MatchRecovery.ScheduledUnitEvents.cs', 'MatchStateHasher.HashValue(schedule) != original.Hash'),
        ]:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_CONSUMER')
                sources.files[path] += '\n    private static void UnusedDecoy() { ' + token + '; }\n'
                sources.files[path] += '\n// ' + token + '\n'
                self.assertEqual(['missing'] * 4, self.statuses(sources=sources))

    def test_a_call_on_an_unused_declaration_line_cannot_replace_the_real_return_guard(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.HandExileReturn.cs'
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        code = sources.files[path]
        anchor = '    private static StackResolutionResult ReturnCapturedHandExile('
        start = code.index(anchor)
        guard = 'card.ObjectGeneration != exile.Card.Generation'
        fragments = ['own.Banished.Contains(exile.Card.ObjectId)', guard,
                     'ResetCardOutsidePlay(', 'Hand = own.Hand.Append(card.ObjectId)']
        # A legal unused wrapper calls the real method. Quoted fragments in
        # that wrapper do not implement the return's generation check.
        decoy = ('    private static void UnusedReturnProbe() { _ = ReturnCapturedHandExile(null!, null!);\n'
                 + ''.join('        _ = "' + token + '";\n' for token in fragments) + '    }\n\n')
        sources.files[path] = code[:start] + decoy + code[start:].replace(guard, 'false')
        self.assertEqual(['missing'] * 4, self.statuses(sources=sources))

    def test_ambiguous_return_overloads_cannot_borrow_the_first_declarations_body(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.HandExileReturn.cs'
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        sources.files[path] += ('\n    private static StackResolutionResult ReturnCapturedHandExile('
            'MatchState state, StackItemState item, bool unused) => NoopStackResolutionResult(state);\n')
        self.assertEqual(['missing'] * 4, self.statuses(sources=sources))
