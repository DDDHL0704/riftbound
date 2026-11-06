import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('legend_clause_audit', Path(__file__).parents[1] / 'audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class LegendActivationClauseAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.engine = self.root / 'src/Riftbound.Engine'
        self.engine.mkdir(parents=True)
        self.constants = [dict(value='LEGEND_BOON_ID', symbol='LegendCatalog.Boon')]

    def definition(self, **changes):
        return dict(abilityId='LEGEND_BOON_ID', effectKind='GRANT_BOON', manaCost=1, experienceCost=0,
                    powerCost=0, exhaustsSourceAsCost=True, requiredTargetCount=1,
                    requiresFriendlyUnitTarget=True, requiresAnyUnitTarget=False, flow='UNIT_EFFECT', **changes)

    def outcome(self, text='支付{{1}}，{{横置}}：给予一名友方单位增益。', definition=None):
        card = dict(cardNo='OGN·257/298', cardName='盲僧', cardCategory='legend', cardEffect=text,
                    behaviors=[], activations=[], legendActivations=['LEGEND_BOON_ID'])
        if definition is not None:
            card['legendActivationDefinitions'] = [definition]
        row = audit.card_clauses(card)[0]
        return audit.audit_clause(card, row, audit.Sources(self.root, self.constants))

    def consumers(self):
        (self.engine / 'CoreRuleEngine.LegendUnitEffect.cs').write_text(
            'if (kind == LegendAbilityEffectKinds.GrantBoon) GrantLegendBoon(); '
            'if (kind == LegendAbilityEffectKinds.GrantRoam) GrantLegendRoam();')
        (self.engine / 'CoreRuleEngine.cs').write_text('var id = LegendCatalog.Boon; return QueueLegendUnitEffect(state);')

    def test_source_id_and_an_immediate_switch_do_not_certify_a_legend_clause(self):
        self.consumers()
        self.assertEqual('review', self.outcome()['status'])

    def test_exact_friendly_boon_has_a_fee_scope_and_stack_consumer(self):
        self.consumers()
        self.assertEqual('route', self.outcome(definition=self.definition())['status'])

    def test_immediate_boon_is_missing_even_when_registered_and_paid(self):
        self.consumers()
        definition = self.definition()
        definition['flow'] = 'IMMEDIATE'
        self.assertEqual('missing', self.outcome(definition=definition)['status'])

    def test_ordinary_draw_switch_is_not_a_resource_skill(self):
        definition = dict(abilityId='DRAW', effectKind='DRAW_ONE', exhaustsSourceAsCost=True,
                          experienceCost=3, manaCost=0, powerCost=0, requiredTargetCount=0, flow='IMMEDIATE')
        self.assertEqual('missing', self.outcome('消耗3经验，{{横置}}：抽一张牌。', definition)['status'])

    def test_matching_flow_still_needs_both_queue_and_resolution_consumers(self):
        for name in ['CoreRuleEngine.cs', 'CoreRuleEngine.LegendUnitEffect.cs']:
            with self.subTest(name=name):
                self.consumers()
                (self.engine / name).unlink()
                self.assertNotEqual('route', self.outcome(definition=self.definition())['status'])

    def test_unqualified_unit_cannot_be_narrowed_to_friendly_unit(self):
        self.consumers()
        definition = self.definition()
        definition.update(manaCost=0, experienceCost=1)
        self.assertEqual('missing', self.outcome('消耗1经验，{{横置}}：给予一名单位{{增益}}。', definition)['status'])
        definition.update(requiresFriendlyUnitTarget=False, requiresAnyUnitTarget=True)
        self.assertEqual('route', self.outcome('消耗1经验，{{横置}}：给予一名单位{{增益}}。', definition)['status'])

    def test_wrong_mana_or_experience_amount_cannot_certify_the_clause(self):
        self.consumers()
        definition = self.definition()
        definition['manaCost'] = 2
        self.assertNotEqual('route', self.outcome(definition=definition)['status'])
        definition.update(manaCost=0, experienceCost=2, requiresFriendlyUnitTarget=False, requiresAnyUnitTarget=True)
        self.assertNotEqual('route', self.outcome('消耗1经验，{{横置}}：给予一名单位{{增益}}。', definition)['status'])

    def test_wrong_exhaust_cost_or_target_count_cannot_certify_the_clause(self):
        self.consumers()
        for changes in [dict(exhaustsSourceAsCost=False), dict(requiredTargetCount=2)]:
            with self.subTest(changes=changes):
                definition = self.definition()
                definition.update(changes)
                self.assertNotEqual('route', self.outcome(definition=definition)['status'])

    def test_roam_checks_its_own_effect_and_any_unit_scope(self):
        self.consumers()
        definition = self.definition()
        definition.update(effectKind='GRANT_ROAM', manaCost=0, requiresFriendlyUnitTarget=False, requiresAnyUnitTarget=True)
        text = '{{横置}}：让一名单位本回合内获得{{游走}}。'
        self.assertEqual('route', self.outcome(text, definition)['status'])
        definition['requiresFriendlyUnitTarget'] = True
        self.assertEqual('missing', self.outcome(text, definition)['status'])

    def test_resource_flow_is_not_classified_as_a_missing_ordinary_skill(self):
        definition = dict(abilityId='MANA', effectKind='GAIN_MANA', exhaustsSourceAsCost=True,
                          manaCost=0, powerCost=0, requiredTargetCount=0, generatedMana=1, flow='RESOURCE')
        self.consumers()
        self.assertNotEqual('missing', self.outcome('{{横置}}：{{获得}}{{1}}。', definition)['status'])
