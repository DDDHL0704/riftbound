import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('standby_unit_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
READY = '当你正面朝下放置一张卡牌时，让我变为活跃状态。'
DAMAGE = '当你将一张卡牌从正面朝下的状态打出时，对一名敌方单位造成2点伤害。'


class StandbyUnitObserverAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card(text):
        return dict(cardNo='UNL-023/219', cardCategory='hero_unit', cardEffect=text,
                    behaviors=[dict(playsSourceToBaseAsUnit=True, sourceReadiesWhenControllerHidesCard=True,
                                    controllerFaceDownPlayEnemyDamage=2)])

    def status(self, text, card=None, sources=None):
        card = card or self.card(text)
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def test_each_printed_sentence_needs_its_actual_action_and_shared_consumers(self):
        self.assertEqual('route', self.status(READY))
        self.assertEqual('route', self.status(DAMAGE))

    def test_registration_wrong_amount_or_different_scope_cannot_certify_a_sentence(self):
        for text, key, value in [(READY, 'sourceReadiesWhenControllerHidesCard', False),
                                 (READY, 'sourceReadiesWhenControllerHidesCard', 1),
                                 (DAMAGE, 'controllerFaceDownPlayEnemyDamage', 1),
                                 (DAMAGE, 'controllerFaceDownPlayEnemyDamage', True),
                                 (READY, 'playsSourceToBaseAsUnit', False)]:
            with self.subTest(text=text, key=key, value=value):
                card = self.card(text); card['behaviors'][0][key] = value
                self.assertEqual('missing', self.status(text, card))
        card = self.card(DAMAGE); clause = audit.card_clauses(card)[0]
        self.assertEqual('missing', audit.audit_clause(card, dict(clause, scope='在你的回合中'), self.sources)['status'])
        self.assertIsNone(audit.standby_unit_observer_route(card, dict(clause, text=DAMAGE.replace('敌方', '友方')), self.sources))

    def test_unused_decoys_cannot_replace_original_action_confirmation_or_resolution(self):
        checks = [
            (READY, 'CoreRuleEngine.cs', 'if (stackItem.StandbyObserver is not null) return ResolveStandbyObserver(state, stackItem)'),
            (DAMAGE, 'CoreRuleEngine.cs', 'if (stackItem.StandbyObserver is not null) return ResolveStandbyObserver(state, stackItem)'),
            (DAMAGE, 'CoreRuleEngine.BattleResultContinuation.cs', 'collected = QueueStandbyPlayedObservers(collected)'),
            (DAMAGE, 'CoreRuleEngine.cs', 'CaptureStandbyPlayed(playedEvent, playerZones, cardObjects, stackItem.ControllerId)'),
            (READY, 'CoreRuleEngine.Standby.cs', 'QueueStandbyHiddenUnitObservers(QueueStandbyHiddenTokens('),
            (READY, 'CoreRuleEngine.StandbyUnitObservers.cs', 'IsFaceUpNonStandbyUnit(source)'),
            (READY, 'CoreRuleEngine.StandbyUnitObservers.cs', 'AttachedRuleTextRules.Instances(zones, cards, source)'),
            (READY, 'CoreRuleEngine.StandbyUnitObservers.cs', 'source.ObjectGeneration == context.Source.Generation'),
            (READY, 'CoreRuleEngine.StandbyUnitObservers.cs', 'ApplyReadyState(source,'),
            (DAMAGE, 'CoreRuleEngine.StandbyObservers.cs', 'CaptureStandbyUnitObservers(ev, zones, cards, player, StandbyPlayedEnemyDamageEffect)'),
            (DAMAGE, 'CoreRuleEngine.UnitEntryTriggers.cs', 'CaptureStandbyPlayed(ev, zones, cards, player)'),
            (DAMAGE, 'CoreRuleEngine.FieldTriggers.cs', 'PrepareStandbyUnitDamage(result, item)'),
            (DAMAGE, 'CoreRuleEngine.FieldTriggers.cs', 'ValidStandbyUnitDamageChoice(state, choice)'),
            (DAMAGE, 'CoreRuleEngine.TriggerTargetPayment.cs', 'StandbyUnitDamageTargets(state, item)'),
            (DAMAGE, 'CoreRuleEngine.StandbyUnitObservers.cs', 'ApplyDamageToCardObject(state, cards, item.ControllerId, target,'),
            (DAMAGE, 'MatchRecovery.StandbyUnitObservers.cs', 'MatchStateHasher.HashValue(item.TargetGenerations)'),
            (READY, 'MatchRecovery.cs', 'ValidateStandbyUnitObserverHistory(authoritativeState, commands, events, errors)'),
        ]
        for text, filename, token in checks:
            with self.subTest(text=text, filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_ACTUAL_CONSUMER')
                sources.files[path] += '\n    private static void UnusedStandbyObserverDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(text, sources=sources))

    def test_both_sentences_require_the_original_accepted_action_not_just_consistent_views(self):
        checks = [
            ('CoreRuleEngine.StandbyObservers.cs', '["standbyUnitCaptureHash"] = StandbyUnitCaptureHash(context)'),
            ('CoreRuleEngine.StandbyUnitObservers.cs', 'PredictBindingMatches(state, context.Source'),
            ('MatchRecovery.StandbyUnitObservers.cs', 'command.Accepted'),
            ('MatchRecovery.StandbyUnitObservers.cs', 'CoreRuleEngine.StandbyUnitActionHash(action.Event) == birth.ActionHash'),
        ]
        for filename, token in checks:
            for text in [READY, DAMAGE]:
                with self.subTest(filename=filename, token=token, text=text):
                    sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                    path = 'src/Riftbound.Engine/' + filename; self.assertIn(token, sources.files[path])
                    sources.files[path] = sources.files[path].replace(token, 'REMOVED_ACTION_AUTHENTICATION')
                    self.assertEqual('missing', self.status(text, sources=sources))
