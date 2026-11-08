import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('friendly_unit_trait_score_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '当我征服或据守一处战场时，如果你的单位合计具有以下所有属性标签，则你获得1分 — “鸟类”、“猫科”、“犬形”和“魄罗”。'
TAGS = ['鸟类', '猫科', '犬形', '魄罗']


class FriendlyUnitTraitScoreAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        profiles = [dict(mode=mode, sourceUnitTags=tag, playsSourceToBaseAsUnit=True,
            unitEventAbility=dict(timing='CONQUEST_OR_HOLD', effect='SCORE_IF_FRIENDLY_UNITS_HAVE_ALL_TAGS',
                                  amount=1, requiredFriendlyUnitTagsForScore=list(TAGS)))
            for mode, tag in zip(['CHOOSE_BIRD', 'CHOOSE_CAT', 'CHOOSE_DOG', 'CHOOSE_PORO'], TAGS)]
        return dict(cardNo='UNL-177/219', cardCategory='hero_unit', cardEffect=TEXT, behaviors=profiles)

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def remove_actual_consumer(self, filename, token):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, 'REMOVED_ACTUAL_TRAIT_SCORE_CONSUMER')
        sources.files[path] += '\n    private static void UnusedTraitScoreDecoy() { ' + token + '; }\n'
        return sources

    def test_both_printings_require_the_same_exact_dual_timing_score_in_all_four_play_modes(self):
        for no in ['UNL-177/219', 'UNL-177a/219']:
            with self.subTest(no=no):
                card = self.card(); card['cardNo'] = no
                self.assertEqual('route', self.status(card))

    def test_a_missing_mode_or_wrong_tags_amount_timing_or_hidden_cost_cannot_certify_the_sentence(self):
        for missing in range(4):
            card = self.card(); del card['behaviors'][missing]
            self.assertEqual('missing', self.status(card))
        mutations = [('timing', 'CONQUEST'), ('timing', 'HOLD'), ('amount', 2),
                     ('requiredFriendlyUnitTagsForScore', TAGS[:-1]),
                     ('requiredFriendlyUnitTagsForScore', TAGS + ['鸟类']),
                     ('requiredFriendlyUnitTagsForScore', ['人类']), ('leadingPowerCost', 1), ('optional', True)]
        for key, value in mutations:
            for mode in range(4):
                with self.subTest(key=key, value=value, mode=mode):
                    card = self.card(); card['behaviors'][mode]['unitEventAbility'][key] = value
                    self.assertEqual('missing', self.status(card))
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.status(card))

    def test_unused_helpers_cannot_replace_real_capture_current_state_score_or_original_history_consumers(self):
        checks = [
            ('CoreRuleEngine.UnitEvents.cs', 'AttachedRuleTextRules.Instances(zones, cards, card)'),
            ('CoreRuleEngine.UnitEvents.cs', 'CaptureConquestEventObservations(state, conquerors)'),
            ('CoreRuleEngine.Conquest.cs', 'CaptureConquestUnitEvents(state with'),
            ('CoreRuleEngine.ConquestUnitEvents.cs', 'CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, "CONQUEST")'),
            ('CoreRuleEngine.Hold.cs', 'CaptureUnitEvents(state.PlayerZones, state.CardObjects, selectedUnits, "HOLD")'),
            ('CoreRuleEngine.UnitEvents.cs', 'ResolveFriendlyUnitTraitScore(state, item, spec)'),
            ('CoreRuleEngine.UnitEvents.cs', 'ValidFriendlyUnitTraitScoreStack(item)'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'GetControlledFieldUnitObjectIds(state.PlayerZones, state.CardObjects, controller)'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'Where(card => IsFaceUpNonStandbyUnit(card))'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'SelectMany(card => card.Tags)'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'required.All(tags.Contains)'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'HasRequiredFriendlyUnitTags(state, item.ControllerId, spec.RequiredFriendlyUnitTagsForScore!)'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'TryPreventPointGain(state, item.ControllerId, amount,'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'scores[item.ControllerId] += amount'),
            ('CoreRuleEngine.UnitTraitScore.cs', 'scores[item.ControllerId] >= EffectiveWinningScore(state)'),
            ('CoreRuleEngine.cs', 'payload["timingCaptureHash"] = TimingCaptureHash(timingContext, trigger.TimingAlias)'),
            ('CoreRuleEngine.BattlefieldTimingAliases.cs', 'UnitEventTimingMatches(unitEvent.Timing, "HOLD")'),
            ('MatchRecovery.BattlefieldTimingAliases.cs', 'c.Accepted && c.CompletedTick == queued[0].Tick'),
            ('MatchRecovery.BattlefieldTimingAliases.cs', 'CoreRuleEngine.TimingCaptureHash(item.Context, item.Alias)'),
            ('MatchRecovery.cs', 'ValidateBattlefieldTimingHistory(authoritativeState, commands, events, errors)'),
        ]
        for filename, token in checks:
            with self.subTest(filename=filename, token=token):
                self.assertEqual('missing', self.status(sources=self.remove_actual_consumer(filename, token)))

    def test_checking_the_tag_condition_at_capture_would_miss_real_response_supplied_tags(self):
        for name in ['CoreRuleEngine.UnitEvents.cs', 'CoreRuleEngine.ConquestUnitEvents.cs', 'CoreRuleEngine.Hold.cs']:
            with self.subTest(name=name):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + name
                if name == 'CoreRuleEngine.UnitEvents.cs':
                    token = 'UnitEventTimingMatches(spec.Timing, timing)'
                elif name == 'CoreRuleEngine.ConquestUnitEvents.cs':
                    token = 'CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, "CONQUEST")'
                else:
                    token = 'CaptureUnitEvents(state.PlayerZones, state.CardObjects, selectedUnits, "HOLD")'
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, token + ' && HasRequiredFriendlyUnitTags(state, controller, required)')
                self.assertEqual('missing', self.status(sources=sources))
