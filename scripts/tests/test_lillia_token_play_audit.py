import copy
import importlib.util
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('lillia_token_play_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class LilliaTokenPlayAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card(no='UNL-058/219'):
        return dict(cardNo=no, cardName='莉莉娅', cardCategory='hero_unit',
            cardEffect='当你打出一名指示物单位时，让我本回合内{{S}}+1。',
            behaviors=[dict(playsSourceToBaseAsUnit=True, unitEventAbility=dict(
                timing='FRIENDLY_UNIT_PLAYED', effect='MIGHT_SELF', amount=1, requiresPlayedUnitToken=True))])

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def changed_source(self, filename, token):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, 'REMOVED_OBSERVER_CONSUMER')
        return sources

    def test_both_printings_require_the_live_token_play_observer(self):
        for no in ['UNL-058/219', 'UNL-058a/219']:
            with self.subTest(no=no):
                self.assertEqual('route', self.status(self.card(no)))

    def test_wrong_condition_timing_amount_and_optional_costs_are_not_certified(self):
        for change in [dict(requiresPlayedUnitToken=False), dict(timing='PLAY'), dict(effect='READY_SELF'),
                       dict(amount=2), dict(optional=True), dict(leadingPowerCost=1), dict(sourceInLegendZone=True)]:
            with self.subTest(change=change):
                card = self.card(); card['behaviors'][0]['unitEventAbility'].update(change)
                self.assertEqual('missing', self.status(card))
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.status(card))

    def test_real_token_entry_must_reach_the_shared_observer(self):
        for name, token in [
            ('CoreRuleEngine.UnitEntryTriggers.cs', 'CaptureOtherUnitPlayed('),
            ('CoreRuleEngine.UnitTokenEntry.cs', 'CaptureUnitEntry(new("UNIT_TOKEN_CREATED"'),
            ('CoreRuleEngine.UnitEvents.cs', 'UnitEventTrigger(captured,')]:
            with self.subTest(name=name):
                self.assertEqual('missing', self.status(sources=self.changed_source(name, token)))

    def test_entry_accepts_the_confirmed_play_receipt_but_rejects_changed_observer_arguments(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.UnitEntryTriggers.cs'
        original = self.sources.files[path]
        for suffix, expected in [('', 'route'), (', playedItem', 'route'),
                                 (', anotherItem', 'missing'), (', playedItem, extra', 'missing')]:
            with self.subTest(suffix=suffix):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                sources.files[path], count = re.subn(r'CaptureOtherUnitPlayed\([^;]+?\);',
                    'CaptureOtherUnitPlayed(ev, zones, cards, entered, player, turnEffects' + suffix + ');', original)
                self.assertEqual(1, count)
                self.assertEqual(expected, self.status(sources=sources))

    def test_an_unused_entry_observer_call_cannot_replace_the_live_entry_hook(self):
        sources = self.changed_source('CoreRuleEngine.UnitEntryTriggers.cs', 'CaptureOtherUnitPlayed(')
        path = 'src/Riftbound.Engine/CoreRuleEngine.UnitEntryTriggers.cs'
        sources.files[path] += '\n    private static void Unused() { CaptureOtherUnitPlayed(ev, zones, cards, entered, player, turnEffects, playedItem); }\n'
        self.assertEqual('missing', self.status(sources=sources))

    def test_the_actual_capture_requires_own_controller_and_intrinsic_token_identity(self):
        for token in ['EffectiveFieldControllerId(zones, c.ObjectId, c) == player',
                      '|| TokenObjectRules.IsToken(entered)', '!UnitEventSpec(o.Context.CardNo)!.RequiresPlayedUnitToken']:
            with self.subTest(token=token):
                self.assertEqual('missing', self.status(sources=self.changed_source('CoreRuleEngine.UnitEvents.cs', token)))

    def test_unused_token_filter_code_cannot_certify_the_actual_capture(self):
        token = '|| TokenObjectRules.IsToken(entered)'
        sources = self.changed_source('CoreRuleEngine.UnitEvents.cs', token)
        path = 'src/Riftbound.Engine/CoreRuleEngine.UnitEvents.cs'
        sources.files[path] += '\nprivate static bool UnusedTokenCheck(CardObjectState entered) => false ' + token + ';\n'
        self.assertEqual('missing', self.status(sources=sources))

    def test_generation_field_and_end_of_turn_consumers_are_required(self):
        for token in ['source.ObjectGeneration == context.SourceGeneration', 'IsObjectOnField(zones, source.ObjectId)',
                      'ApplyDirectUntilEndPowerModifier(source, source.ObjectId, item.SourceObjectId, context.CardNo,']:
            with self.subTest(token=token):
                self.assertEqual('missing', self.status(sources=self.changed_source('CoreRuleEngine.UnitEvents.cs', token)))

    def test_other_scopes_and_sentences_do_not_match_this_observer(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        self.assertIsNone(audit.token_play_observer_route(card, dict(clause, scope='当我进攻时'), self.sources))
        for text in ['当你打出一件装备时，让我变为活跃状态。', '当任意玩家打出一名指示物单位时，让我本回合内{{S}}+1。']:
            self.assertIsNone(audit.token_play_observer_route(card, dict(clause, text=text, scope=text), self.sources))
