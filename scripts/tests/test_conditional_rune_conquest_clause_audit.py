import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('rune_conquest_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
YI = '如果你拥有不少于八枚符文，则我获得{{S}}+4。'
GAREN = '当你征服一处战场时，如果你在该战场上拥有不少于四名单位，则抽两张牌。'


class ConditionalRuneConquestClauseAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.engine = self.root / 'src/Riftbound.Engine'
        self.engine.mkdir(parents=True)

    def write(self, filename, text):
        path = self.engine / filename
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)

    @staticmethod
    def consumer(name, body):
        return '    private static bool ' + name + '(Args args) {\n        ' + body + '\n    }\n'

    def rune_consumers(self):
        self.write('StaticAuraSpecRules.cs', self.consumer('ControllerRequirementsSatisfied',
            '!aura.RequiredControllerRuneCount.HasValue; EmpowerRules.ControlledRunes(state, controller) >= aura.RequiredControllerRuneCount.Value;'))
        gate = 'aura.RequiredControllerRuneCount.HasValue; !StaticAuraControllerRequirementsSatisfied(aura, state, cardObject.ControllerId);'
        self.write('CoreRuleEngine.cs', self.consumer('SourceObjectPowerStaticAuraApplies', gate)
            + self.consumer('SourceObjectKeywordStaticAuraApplies', gate)
            + self.consumer('StaticAuraControllerRequirementsSatisfied', 'StaticAuraSpecRules.ControllerRequirementsSatisfied(aura);'))
        self.write('MatchSession/State.Normalization.cs', 'StaticAuraSpecRules.ControllerRequirementsSatisfied(aura);')
        self.write('MatchSession/State.Auras.cs', self.consumer('SourceObjectPowerStaticAuraApplies',
            'aura.RequiredControllerRuneCount.HasValue; !StaticAuraControllerRequirementsSatisfied(aura, state, controllerId);'))
        self.write('CoreRuleEngine.AttachedRuleText.cs', 'SourceObjectKeywordStaticAuraApplies(state, unit, aura);')

    def conquest_consumers(self):
        self.write('CoreRuleEngine.Conquest.cs', 'CaptureLegendConquest(state);')
        self.write('CoreRuleEngine.LegendConquest.cs', self.consumer('CaptureLegendConquest',
            'IsLegendConquestMinimumUnitsDrawTrigger(spec); ControlledUnitsAtBattlefield(state); '
            'units.Count < definition.Spec.RequiredUnitCount; new LegendConquestDrawOrigin(); new TriggerQueueItemState();')
            + self.consumer('ResolveLegendConquest', 'if (context.Kind == LegendConquestDrawKind) return ResolveLegendConquestDraw(state, item);'))
        self.write('CoreRuleEngine.LegendConquestDraw.cs', self.consumer('ResolveLegendConquestDraw',
            'ValidLegendConquestDrawStack(state, item); ApplyDrawToPlayer(state, spec.DrawCount!.Value);'))
        self.write('MatchRecovery.cs', 'ValidLegendConquestState(state); ValidLegendConquestDrawStack(state);')

    @staticmethod
    def rune_node():
        return dict(kind='SOURCE_OBJECT_POWER', text=YI, layer='STATIC_AURA', duration='WHILE_SOURCE_ON_PUBLIC_FIELD', targetScope='SOURCE_OBJECT', participantScope='SOURCE_OBJECT',
                    powerDeltaPerParticipant=4, requiredControllerRuneCount=8)

    @staticmethod
    def conquest_node():
        return dict(kind='LEGEND_CONQUEST_MINIMUM_UNITS_DRAW', text=GAREN, timing='BATTLEFIELD_CONQUERED',
                    targetScope='CONTROLLED_UNITS_AT_THIS_BATTLEFIELD', requiredUnitCount=4, drawCount=2)

    def outcome(self, text, node=None, category=None):
        card = dict(cardNo='TEST-1', cardName='条件效果', cardCategory=category or ('legendary' if text == GAREN else 'hero_unit'),
                    cardEffect=text, behaviors=[dict(drawCount=2)], activations=[], legendActivations=[])
        if node:
            card['triggers' if text == GAREN else 'staticAuras'] = [dict(node=node)]
        return audit.audit_clause(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))['status']

    def test_a_registration_or_an_immediate_draw_does_not_certify_the_condition(self):
        self.write('CoreRuleEngine.cs', 'var draw = behavior.DrawCount; var might = behavior.PowerDelta;')
        self.assertEqual('missing', self.outcome(YI))
        self.assertEqual('missing', self.outcome(GAREN))

    def test_exact_live_rune_threshold_has_both_server_and_projection_consumers(self):
        self.rune_consumers()
        self.assertEqual('route', self.outcome(YI, self.rune_node()))

    def test_wrong_rune_count_amount_scope_or_extra_condition_stays_missing(self):
        self.rune_consumers()
        for key, value in [('requiredControllerRuneCount', 7), ('powerDeltaPerParticipant', 3),
                           ('targetScope', 'FRIENDLY_UNITS'), ('participantScope', 'BATTLEFIELD'),
                           ('requiredPlayerExperience', 3), ('layer', 'RULE_TEXT'), ('duration', 'UNTIL_END_OF_TURN'),
                           ('targetFilter', 'EXHAUSTED')]:
            with self.subTest(key=key):
                node = self.rune_node(); node[key] = value
                self.assertEqual('missing', self.outcome(YI, node))

    def test_rune_projection_or_copied_text_without_the_shared_count_is_not_enough(self):
        for filename in ['StaticAuraSpecRules.cs', 'CoreRuleEngine.cs', 'MatchSession/State.Auras.cs',
                         'MatchSession/State.Normalization.cs', 'CoreRuleEngine.AttachedRuleText.cs']:
            with self.subTest(filename=filename):
                self.rune_consumers(); self.write(filename, 'unrelated')
                self.assertEqual('missing', self.outcome(YI, self.rune_node()))

    def test_exact_conquest_condition_queues_an_independent_draw(self):
        self.conquest_consumers()
        self.assertEqual('route', self.outcome(GAREN, self.conquest_node()))

    def test_wrong_conquest_count_draw_timing_scope_or_payment_is_missing(self):
        self.conquest_consumers()
        for key, value in [('requiredUnitCount', 3), ('drawCount', 1), ('timing', 'BATTLEFIELD_HELD'),
                           ('targetScope', 'ALL_CONTROLLED_UNITS'), ('manaCost', 1), ('optional', True), ('powerCost', 1), ('exhaustsSource', True),
                           ('experienceCost', 1), ('requiredOverkillDamage', 3)]:
            with self.subTest(key=key):
                node = self.conquest_node(); node[key] = value
                self.assertEqual('missing', self.outcome(GAREN, node))

    def test_catalog_predicate_alone_does_not_cover_capture_queue_resolution_or_recovery(self):
        for filename in ['CoreRuleEngine.Conquest.cs', 'CoreRuleEngine.LegendConquest.cs',
                         'CoreRuleEngine.LegendConquestDraw.cs', 'MatchRecovery.cs']:
            with self.subTest(filename=filename):
                self.conquest_consumers(); self.write(filename, 'unrelated')
                self.assertEqual('missing', self.outcome(GAREN, self.conquest_node()))

    def test_wrong_source_categories_cannot_enter_the_live_consumers(self):
        self.rune_consumers(); self.conquest_consumers()
        self.assertEqual('missing', self.outcome(YI, self.rune_node(), 'legendary'))
        self.assertEqual('missing', self.outcome(GAREN, self.conquest_node(), 'hero_unit'))

    def test_an_unused_helper_cannot_replace_the_server_rune_threshold(self):
        self.rune_consumers()
        path = self.engine / 'CoreRuleEngine.cs'
        text = path.read_text()
        check = '!StaticAuraControllerRequirementsSatisfied(aura, state, cardObject.ControllerId)'
        # The copy/keyword path, projection and shared helper still exist. The
        # printed source's actual might consumer has lost its threshold check.
        text = text.replace(check, 'false', 1) + self.consumer('UnusedRuneCheck', check)
        path.write_text(text)
        self.assertEqual('missing', self.outcome(YI, self.rune_node()))

    def test_an_unused_draw_call_cannot_replace_the_live_conquest_dispatch(self):
        self.conquest_consumers()
        path = self.engine / 'CoreRuleEngine.LegendConquest.cs'
        text = path.read_text()
        instruction = 'return ResolveLegendConquestDraw(state, item);'
        text = text.replace(instruction, 'return NoopStackResolutionResult(state);')
        text += self.consumer('UnusedConquestDraw', instruction)
        path.write_text(text)
        self.assertEqual('missing', self.outcome(GAREN, self.conquest_node()))

    def test_an_unrelated_condition_remains_under_the_normal_auditor(self):
        card = dict(cardEffect='如果你有八张手牌，则我获得{{S}}+4。')
        clause = audit.card_clauses(card)[0]
        self.assertIsNone(audit.conditional_rune_and_conquest_route(card, clause, audit.Sources(self.root, [])))
