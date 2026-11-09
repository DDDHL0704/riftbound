import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('attack_armament_play_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '当我进攻时，你可以选择打出一件法力费用不高于{{2}}的武装，无视其费用，然后将其贴附到我身上。'


class AttackArmamentPlayAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        return dict(cardNo='SFD·024/221', cardCategory='hero_unit', cardEffect=TEXT,
                    behaviors=[dict(playsSourceToBaseAsUnit=True)],
                    unitEventAbility=dict(timing='ATTACK', effect='PLAY_HAND_ARMAMENT_THEN_ATTACH_SOURCE', amount=2, optional=True))

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def test_exact_attack_permission_requires_the_real_play_and_independent_attachment(self):
        self.assertEqual('route', self.status())

    def test_unit_registration_and_incorrect_event_profiles_do_not_certify_the_clause(self):
        for key, value in [('timing', 'DEFEND'), ('effect', 'ATTACH_CAPTURED_ARMAMENT_TO_SOURCE'),
                           ('amount', 1), ('amount', True), ('amount', '2'), ('optional', False), ('optional', 1), ('optional', 1.0)]:
            with self.subTest(key=key, value=value):
                card = self.card(); card['unitEventAbility'][key] = value
                self.assertEqual('missing', self.status(card))
        card = self.card(); card['unitEventAbility'] = None
        self.assertEqual('missing', self.status(card))
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.status(card))

    def test_unused_decoys_cannot_replace_real_armament_payment_and_capture_consumers(self):
        checks = [
            ('CoreRuleEngine.EffectPlay.cs', 'TryGetArmamentPlayAttachmentDefinition(parent, out definition)'),
            ('CoreRuleEngine.EffectPlay.cs', 'ArmamentEffectPlaySourceAllowed(card, definition)'),
            ('CoreRuleEngine.EffectPlay.cs', 'CapturePlayedArmamentAttachment(result, pending, play)'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', '!IsTokenObject(card)'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'EffectPlayCandidateCostAllowed(card, definition)'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'EffectPlayIgnoreBasePower: true'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'pending.IgnoreAllCosts'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'ReflexiveTick = result.State.Tick'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'play.Source == gear.Binding'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'play.Tick == context.ReflexiveTick'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'currentGear.ObjectGeneration != gear.Binding.Generation'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'host.ObjectGeneration != context.SourceGeneration'),
            ('CoreRuleEngine.ArmamentPlayAttachment.cs', 'TryAttachEquipment('),
            ('CoreRuleEngine.UnitEventModes.cs', 'Effect = AttachPlayedArmamentEffect'),
            ('CoreRuleEngine.UnitEvents.cs', 'return ResolvePlayedArmamentAttachment(state, item)'),
            ('CoreRuleEngine.ObservedUnitEvents.cs', 'context.ReflexiveTick'),
            ('MatchRecovery.ObservedUnitEvents.cs', 'CoreRuleEngine.ObservedUnitEventBirthTick(item.Context)'),
            ('CoreRuleEngine.TriggerCosts.cs', 'IsOptionalArmamentPlayEvent(optional)'),
            ('MatchRecovery.ArmamentPlayAttachment.cs', 'command.Accepted'),
            ('CoreRuleEngine.UnitPlayMight.cs', '.Other?.Binding'),
            ('CoreRuleEngine.UnitPlayMight.cs', 'ValidArmamentPlayAttachmentContext('),
            ('CoreRuleEngine.PermanentPlayTargets.cs', 'behavior.RequiredTargetCount > 0 || IsAgileDirectPlayAttachRepresentative(behavior)'),
            ('CoreRuleEngine.DeclaredChoices.cs', 'HasIndependentPermanentTargets(behavior) && targets.Count == 0'),
            ('CoreRuleEngine.FieldTriggers.cs', 'IsAgilePermanentPlaySkill(item)'),
            ('CoreRuleEngine.FieldTriggers.cs', '["targetGenerations"] = bound.TargetGenerations'),
            ('CoreRuleEngine.FieldTriggers.cs', 'confirmationPayload["targetGenerations"] = confirmed.TargetGenerations'),
            ('MatchRecovery.UnitEventTargets.cs', 'CoreRuleEngine.IsAgilePermanentPlaySkill(item)'),
            ('MatchRecovery.cs', 'ValidArmamentPlayAttachmentPermission(state, armamentHandPlay)'),
        ]
        for filename, token in checks:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_CONSUMER')
                sources.files[path] += '\n    private static void UnusedRellDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(sources=sources))

    def test_unrelated_clause_scopes_cannot_borrow_the_armament_path(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        self.assertEqual('missing', audit.audit_clause(card, dict(clause, scope='当我防守时'), self.sources)['status'])
        for text in [TEXT.replace('{{2}}', '{{3}}'), TEXT.replace('武装', '单位'), TEXT.replace('进攻', '据守')]:
            self.assertIsNone(audit.attack_armament_play_attachment_route(card, dict(clause, text=text), self.sources))

    def test_the_second_earned_attachment_cannot_be_blocked_by_the_first(self):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/CoreRuleEngine.cs'
        before = '|| !gear.Tags.Contains(CardObjectTags.EquipmentCard)) return false;'
        after = '|| !gear.Tags.Contains(CardObjectTags.EquipmentCard) || !string.IsNullOrWhiteSpace(gear.AttachedToObjectId)) return false;'
        self.assertEqual(1, sources.files[path].count(before))
        sources.files[path] = sources.files[path].replace(before, after)
        sources.files[path] += '\n    private static void UnusedAttachmentDecoy() { TryAttachEquipment(); }\n'
        self.assertEqual('missing', self.status(sources=sources))
