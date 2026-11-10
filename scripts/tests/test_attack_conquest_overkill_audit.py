import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('attack_conquest_overkill_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '当我通过进攻征服一处战场时，如果你给敌方单位造成了不低于5点的过量伤害，则你可以选择对一名敌方单位造成等同于该过量伤害的伤害。'


class AttackConquestOverkillAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        return dict(cardNo='SFD·120/221', cardCategory='hero_unit', cardEffect=TEXT,
                    behaviors=[dict(playsSourceToBaseAsUnit=True)],
                    unitEventAbility=dict(timing='CONQUEST', effect='DAMAGE_ENEMY_UNIT',
                                          optional=True, minimumAssignedOverkill=5,
                                          requiresAttackingConquest=True, damageUsesAssignedOverkill=True))

    def status(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def test_the_attack_conquest_uses_the_original_excess_assignment_for_independent_enemy_damage(self):
        self.assertEqual('route', self.status())

    def test_a_unit_or_similar_trigger_does_not_certify_a_wrong_attack_or_overkill_requirement(self):
        for key, value in [('timing', 'PLAY'), ('effect', 'DAMAGE_ENEMIES_HERE'),
                           ('minimumAssignedOverkill', 4), ('minimumAssignedOverkill', True),
                           ('minimumAssignedOverkill', '5'), ('requiresAttackingConquest', False),
                           ('requiresAttackingConquest', 1), ('damageUsesAssignedOverkill', False),
                           ('damageUsesAssignedOverkill', 1), ('optional', False), ('optional', 1), ('optional', 1.0)]:
            with self.subTest(key=key, value=value):
                card = self.card(); card['unitEventAbility'][key] = value
                self.assertEqual('missing', self.status(card))
        card = self.card(); card['unitEventAbility'] = None
        self.assertEqual('missing', self.status(card))

    def test_unused_decoys_do_not_replace_live_capture_amount_target_and_history_consumers(self):
        checks = [
            ('CoreRuleEngine.Conquest.cs', 'battlefieldId, assignedOverkillDamageToEnemyUnits)'),
            ('CoreRuleEngine.UnitEvents.cs', 'CaptureConquestEventObservations(state, conquerors, assignedOverkill)'),
            ('CoreRuleEngine.ConquestUnitEvents.cs', 'source.IsAttacking'),
            ('CoreRuleEngine.ConquestUnitEvents.cs', 'assignedOverkill >= minimum'),
            ('CoreRuleEngine.UnitEventModes.cs', 'Amount = assigned'),
            ('CoreRuleEngine.UnitEvents.cs', 'c.AssignedOverkill is { } assigned && assigned >= minimum'),
            ('CoreRuleEngine.RuneDomainReveal.cs', 'onlyPrimary: spec.Effect != RuneDomainDamageEffect'),
            ('CoreRuleEngine.DeployedEquipment.cs', 'onlyPrimary ? primary is null ? [] : new[] { primary } : UnitEventEnemiesHere(state, item)'),
            ('CoreRuleEngine.ObservedUnitEvents.cs', 'context?.AssignedOverkill is not null'),
            ('CoreRuleEngine.ObservedUnitEvents.cs', 'HashValue(new { capture, context.AssignedOverkill })'),
            ('MatchRecovery.ObservedUnitEvents.cs', '!expected.SetEquals(current.Select(item => item.Id))'),
            ('MatchRecovery.UnitEventTargets.cs', 'targets.SequenceEqual(item.TargetObjectIds)'),
        ]
        for filename, token in checks:
            with self.subTest(filename=filename, token=token):
                sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
                path = 'src/Riftbound.Engine/' + filename
                self.assertIn(token, sources.files[path])
                sources.files[path] = sources.files[path].replace(token, 'REMOVED_CONSUMER')
                sources.files[path] += '\n    private static void UnusedDecoy() { ' + token + '; }\n'
                self.assertEqual('missing', self.status(sources=sources))

    def test_a_call_on_an_unrelated_declaration_line_cannot_replace_the_actual_attack_check(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.ConquestUnitEvents.cs'
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        code = sources.files[path]
        anchor = '    private static IEnumerable<UnitEventObservation> CaptureConquestEventObservations('
        start = code.index(anchor)
        predicate = 'UnitEventSpecForContext(o.Context)?.RequiresAttackingConquest != true'
        # A legal unused wrapper calls the real capture, but its string literals
        # are not executable guards inside that capture's actual method body.
        fragments = [predicate,
            'state.CardObjects.TryGetValue(o.Source, out var source) && source.IsAttacking',
            'MinimumAssignedOverkill is not { } minimum', 'assignedOverkill >= minimum',
            'AssignedOverkill = UnitEventSpecForContext(o.Context)?.MinimumAssignedOverkill is not null ? assignedOverkill : null']
        decoy = ('    private static void UnusedCaptureProbe() { _ = CaptureConquestEventObservations(null!, []);\n'
                 + ''.join('        _ = "' + token + '";\n' for token in fragments) + '    }\n\n')
        sources.files[path] = code[:start] + decoy + code[start:].replace(predicate, 'false')
        self.assertEqual('missing', self.status(sources=sources))

    def test_ambiguous_capture_overloads_reopen_the_clause_instead_of_choosing_the_first(self):
        path = 'src/Riftbound.Engine/CoreRuleEngine.ConquestUnitEvents.cs'
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        sources.files[path] += ('\n    private static IEnumerable<UnitEventObservation> CaptureConquestEventObservations('
            'MatchState state, IEnumerable<string> units, int assignedOverkill, bool unused) => [];\n')
        self.assertEqual('missing', self.status(sources=sources))

    def test_other_conditions_cannot_borrow_the_attack_conquest_path(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        self.assertEqual('missing', audit.audit_clause(card, dict(clause, scope='当我防守时'), self.sources)['status'])
        for text in [TEXT.replace('5', '3'), TEXT.replace('进攻', '防守'), TEXT.replace('敌方单位', '友方单位')]:
            self.assertIsNone(audit.attack_conquest_overkill_route(card, dict(clause, text=text), self.sources))
