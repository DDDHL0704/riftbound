import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('target_selection_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)
TEXT = '当你将一名友方单位选为目标时，你可以选择让我变为休眠状态并支付{{A}}，以此让该单位变为活跃状态。'


class TargetSelectionClauseAuditTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.engine = self.root / 'src/Riftbound.Engine'
        self.engine.mkdir(parents=True)

    def consumers(self):
        for name, content in {
            'CoreRuleEngine.TargetSelectionObservers.cs': 'IsFriendlyUnitTargetedReadyTrigger(spec); CaptureUnitEvents(zones); '
                'UnitEventTrigger(observation); ApplyReadyState(unit); PredictBindingMatches(state);',
            'CoreRuleEngine.SelfActionObservers.cs': 'CaptureFriendlyUnitTargetedObservers(state); QueueTargetSelectionObservers(state); '
                'HasConfirmedTargets(state); SPELL_CHOICES_REMADE;',
            'CoreRuleEngine.UnitEvents.cs': 'TargetSelectionLegendEvents.Value.GetValueOrDefault(no); '
                'ValidTargetSelectionContext(state); ResolveSelectedEventUnitReady(state);',
            'CoreRuleEngine.TriggerCosts.cs': 'exhaustSpec.LeadingPowerCost; LeadingSourceExhaust; CompleteTriggerCost(state);',
            'MatchRecovery.cs': 'ValidUnitEvent(state); ValidUnitEventTargets(item);',
        }.items():
            (self.engine / name).write_text(content)

    @staticmethod
    def definition():
        return dict(timing='FRIENDLY_UNIT_TARGETED', effect='READY_SELECTED_EVENT_UNIT', amount=0,
                    leadingPowerCost=1, leadingSourceExhaust=True, optional=True, sourceInLegendZone=True)

    @staticmethod
    def node():
        return dict(kind='CONTROLLER_TARGETED_FRIENDLY_UNIT_PAY_EXHAUST_READY', text=TEXT,
                    timing='CONTROLLER_TARGETED_FRIENDLY_UNIT', targetScope='SELECTED_FRIENDLY_EVENT_UNIT',
                    powerCost=1, exhaustsSource=True, optional=True, unitReadyCount=1)

    def outcome(self, spec=None, node=None, category='legendary'):
        card = dict(cardNo='SFD·195/221', cardName='刀锋舞者', cardCategory=category, cardEffect=TEXT,
                    behaviors=[], legendActivations=['OLD_MANUAL_REACTION'], activations=[],
                    legendActivationDefinitions=[dict(effectKind='READY_FRIENDLY_UNIT', manaCost=1)],
                    unitEventAbility=spec)
        if node:
            card['triggers'] = [dict(node=node)]
        return audit.audit_clause(card, audit.card_clauses(card)[0], audit.Sources(self.root, []))['status']

    def test_a_manual_reaction_with_a_pending_target_is_not_the_printed_trigger(self):
        self.consumers()
        self.assertEqual('missing', self.outcome())

    def test_exact_trigger_fee_source_scope_and_independent_resolution_have_consumers(self):
        self.consumers()
        self.assertEqual('route', self.outcome(self.definition(), self.node()))

    def test_wrong_fee_timing_effect_or_scope_is_not_certified(self):
        self.consumers()
        for key, value in [('timing', 'MAIN'), ('effect', 'READY_HERE'), ('leadingPowerCost', 0),
                           ('leadingSourceExhaust', False), ('optional', False), ('sourceInLegendZone', False),
                           ('amount', 1), ('leadingManaCost', 1), ('leadingPowerTrait', 'green')]:
            with self.subTest(key=key):
                spec = self.definition(); spec[key] = value
                self.assertEqual('missing', self.outcome(spec, self.node()))

    def test_extra_once_per_turn_or_empowerment_conditions_do_not_match_the_printed_clause(self):
        self.consumers()
        for key in ['oncePerTurn', 'requiresEmpowered', 'requiresNoDamageThisTurn', 'leadingSelfBanish']:
            with self.subTest(key=key):
                spec = self.definition(); spec[key] = True
                self.assertEqual('missing', self.outcome(spec, self.node()))

    def test_a_parser_node_without_the_live_profile_is_not_enough(self):
        self.consumers()
        self.assertEqual('missing', self.outcome(None, self.node()))

    def test_the_live_profile_also_needs_its_printed_trigger_node(self):
        self.consumers()
        self.assertEqual('missing', self.outcome(self.definition()))

    def test_wrong_printed_power_scope_or_target_count_stays_missing(self):
        self.consumers()
        for key, value in [('powerCost', 0), ('targetScope', 'ANY_UNIT_ON_FIELD'), ('unitReadyCount', 2),
                           ('exhaustsSource', False), ('optional', False), ('timing', 'UNIT_READY'),
                           ('manaCost', 1), ('powerCostTrait', 'green'), ('oncePerTurn', True)]:
            with self.subTest(key=key):
                node = self.node(); node[key] = value
                self.assertEqual('missing', self.outcome(self.definition(), node))

    def test_capture_confirmation_resolution_cost_and_recovery_are_all_required(self):
        for filename in ['CoreRuleEngine.TargetSelectionObservers.cs', 'CoreRuleEngine.SelfActionObservers.cs',
                         'CoreRuleEngine.UnitEvents.cs', 'CoreRuleEngine.TriggerCosts.cs', 'MatchRecovery.cs']:
            with self.subTest(filename=filename):
                self.consumers(); (self.engine / filename).write_text('unrelated')
                self.assertEqual('missing', self.outcome(self.definition(), self.node()))

    def test_the_legend_profile_cannot_certify_a_unit_or_equipment(self):
        self.consumers()
        self.assertEqual('missing', self.outcome(self.definition(), self.node(), 'unit'))
