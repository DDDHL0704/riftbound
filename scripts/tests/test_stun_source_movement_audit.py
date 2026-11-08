import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('stun_source_movement_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '当你{{眩晕}}战场上的一名敌方单位时，你可以选择将我移动到该战场。'


class StunSourceMovementAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        return dict(cardNo='UNL-055/219', cardCategory='hero_unit', cardEffect=TEXT,
                    behaviors=[dict(playsSourceToBaseAsUnit=True, sourceMayMoveToStunnedEnemyBattlefield=True)])

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def remove_actual_consumer(self, filename, token):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, 'REMOVED_ACTUAL_STUN_MOVEMENT_CONSUMER')
        sources.files[path] += '\n    private static void UnusedStunMovementDecoy() { ' + token + '; }\n'
        return sources

    def test_the_exact_sentence_requires_the_shared_optional_source_movement_profile(self):
        for printing in ['UNL-055/219', 'UNL-055a/219']:
            with self.subTest(printing=printing):
                card = self.card(); card['cardNo'] = printing
                self.assertEqual('route', self.status(card))

    def test_missing_or_wrong_flags_and_scope_cannot_certify_the_sentence(self):
        for key in ['playsSourceToBaseAsUnit', 'sourceMayMoveToStunnedEnemyBattlefield']:
            for value in [False, None, 1, 'true']:
                with self.subTest(key=key, value=value):
                    card = self.card(); card['behaviors'][0][key] = value
                    self.assertEqual('missing', self.status(card))
        card = self.card(); card['behaviors'] *= 2
        self.assertEqual('missing', self.status(card))
        card = self.card(); card['cardCategory'] = 'spell'
        self.assertEqual('missing', self.status(card))
        card = self.card(); clause = audit.card_clauses(card)[0]
        for key, value in [('scope', '当我进攻时'), ('kind', 'static')]:
            with self.subTest(key=key):
                self.assertEqual('missing', audit.audit_clause(card, dict(clause, **{key: value}), self.sources)['status'])
        self.assertIsNone(audit.stun_source_movement_route(card, dict(clause, text=TEXT.replace('敌方', '友方')), self.sources))

    def test_unused_helpers_cannot_replace_capture_confirmation_movement_or_history(self):
        checks = [
            ('CoreRuleEngine.StunObservers.cs', '["stunningPlayerId"] = actor'),
            ('CoreRuleEngine.StunObservers.cs', '!IsEnemyFieldUnit(zones, cards, actor, targetId)'),
            ('CoreRuleEngine.StunObservers.cs', 'at.Zone == "BATTLEFIELD"'),
            ('CoreRuleEngine.StunObservers.cs', 'AttachedRuleTextRules.Instances(zones, cards, source)'),
            ('CoreRuleEngine.StunObservers.cs', 'field is not null && StunMoveSourceBehavior(instance.CardNo)'),
            ('CoreRuleEngine.StunObservers.cs', 'context.Kind == StunMoveSourceKind ? result.State.Tick'),
            ('CoreRuleEngine.StunObservers.cs', '["stunSourceMovementCaptureHash"] = StunSourceMovementCaptureHash(context)'),
            ('CoreRuleEngine.BattleResultContinuation.cs', 'QueueStunObservers(state, result)'),
            ('CoreRuleEngine.TriggerCosts.cs', 'item.FieldContext?.Kind == StunMoveSourceKind ? new()'),
            ('CoreRuleEngine.TriggerCosts.cs', 'StunMoveSourceKind ? ValidStunSourceMovementStack(item)'),
            ('CoreRuleEngine.TriggerCosts.cs', 'ValidFieldContextState(state, movement,'),
            ('CoreRuleEngine.TriggerCosts.cs', '? OptionalTriggerWindow : TriggerCostWindow'),
            ('CoreRuleEngine.TriggerCosts.cs', 'item.FieldContext?.Kind != StunMoveSourceKind'),
            ('CoreRuleEngine.TriggerCosts.cs', 'EquipmentDeathReturnKind or StunMoveSourceKind'),
            ('CoreRuleEngine.StunObservers.cs', 'context.Kind == StunMoveSourceKind) return ResolveStunSourceMovement(state, item)'),
            ('CoreRuleEngine.StunSourceMovement.cs', 'source.ObjectGeneration != birth.Source.Generation'),
            ('CoreRuleEngine.StunSourceMovement.cs', 'battlefield.ObjectGeneration != field.Generation'),
            ('CoreRuleEngine.StunSourceMovement.cs', 'TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, source.ObjectId,'),
            ('MatchRecovery.StunSourceMovement.cs', 'command.Accepted'),
            ('MatchRecovery.StunSourceMovement.cs', 'target == origin.Stunned.ObjectId'),
            ('MatchRecovery.StunSourceMovement.cs', 'selected.SequenceEqual(new[] { item.SourceObjectId })'),
            ('MatchRecovery.cs', 'ValidateStunSourceMovementHistory(authoritativeState, commands, events, errors)'),
        ]
        for filename, token in checks:
            with self.subTest(filename=filename, token=token):
                self.assertEqual('missing', self.status(sources=self.remove_actual_consumer(filename, token)))

    def test_every_actual_stun_producer_must_call_the_observer_in_its_resolver(self):
        for filename in ['CoreRuleEngine.BorrowedExhaust.cs', 'CoreRuleEngine.MovedEnemyStun.cs',
                         'CoreRuleEngine.PaidUnitStun.cs', 'CoreRuleEngine.SimpleActivatedAbilities.cs',
                         'CoreRuleEngine.TargetedPowerAbility.cs', 'CoreRuleEngine.UnitEntryTriggers.cs',
                         'CoreRuleEngine.UnitEventBoonInstructions.cs', 'CoreRuleEngine.cs']:
            with self.subTest(filename=filename):
                self.assertEqual('missing', self.status(sources=self.remove_actual_consumer(filename, 'CaptureStunObservers(')))
