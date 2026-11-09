import copy
import unittest

from test_effect_clause_audit import audit, card

FEE = '你可以选择支付{{橙色}}{{橙色}}，作为打出我的额外费用。'
MOVE = '当你打出我时，如果你支付了该额外费用，则可以将一件敌方装备移动到你的基地。'
CONTROL = '你控制这件装备，直到我离场为止。'
ATTACH = '如果它是一件武装，则将其贴附到我身上。'


class PaidEquipmentTheftAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = audit.Sources(audit.ROOT, [])

    def setUp(self):
        self.sources = copy.copy(self.original)
        self.sources.files = self.original.files.copy()
        self.value = card('\n'.join([FEE, MOVE + CONTROL + ATTACH]))
        self.value['cardNo'] = 'SFD·109/221'
        self.value['cardCategory'] = 'hero_unit'
        self.value['behaviors'] = [dict(playsSourceToBaseAsUnit=True, requiredTargetCount=1, minTargetCount=0,
            targetScope='ENEMY_UNIT_OR_EQUIPMENT', targetScopeIsExplicit=True, targetRequiredTag='CARD_TYPE:EQUIPMENT',
            targetEffectAdditionalPowerCost=2, targetEffectAdditionalPowerTrait='orange',
            paidPlayTargetsOptional=True, stealsTargetEquipmentUntilSourceLeaves=True)]

    def outcomes(self):
        return [audit.paid_equipment_theft_route(self.value, clause, self.sources)
                for clause in audit.card_clauses(self.value)]

    def test_all_four_clauses_require_the_actual_independent_paid_theft(self):
        self.assertEqual(['route'] * 4, [item['status'] for item in self.outcomes()])
        for clause in audit.card_clauses(self.value):
            self.assertEqual('route', audit.audit_clause(self.value, clause, self.sources)['status'])

    def test_runtime_export_omits_the_default_zero_minimum(self):
        del self.value['behaviors'][0]['minTargetCount']
        self.assertEqual(['route'] * 4, [item['status'] for item in self.outcomes()])

    def test_wrong_fee_scope_card_type_or_optional_target_shape_cannot_certify_any_clause(self):
        original = self.value['behaviors'][0].copy()
        for field, value in [('targetEffectAdditionalPowerCost', 1), ('targetEffectAdditionalPowerCost', True),
                             ('targetEffectAdditionalPowerCost', 2.0), ('targetEffectAdditionalPowerTrait', 'green'),
                             ('targetScope', 'ANY_EQUIPMENT'), ('targetRequiredTag', 'CARD_TYPE:UNIT'),
                             ('requiredTargetCount', 2), ('minTargetCount', 1), ('minTargetCount', False),
                             ('damageAmount', 1), ('damageAmount', False), ('targetEffectAdditionalManaCost', 1),
                             ('paidPlayTargetsOptional', False), ('stealsTargetEquipmentUntilSourceLeaves', False)]:
            with self.subTest(field=field, value=value):
                self.value['behaviors'] = [dict(original, **{field: value})]
                self.assertEqual(['missing'] * 4, [item['status'] for item in self.outcomes()])

    def test_source_fragments_must_remain_in_the_live_consumer(self):
        for filename, token in [
            ('CoreRuleEngine.cs', 'TryBuildTargetEffectAdditionalCost('),
            ('CoreRuleEngine.cs', 'TryResolveSourceStealEnemyEquipment('),
            ('CoreRuleEngine.cs', 'RecordEquipmentControlHistory(state, completed)'),
            ('CoreRuleEngine.cs', 'EquipmentControl = null'),
            ('CoreRuleEngine.EquipmentTargets.cs', 'FindFieldObjectLocation(state.PlayerZones, hostId) != location'),
            ('CoreRuleEngine.PermanentConfirmation.cs', 'declaration.StealsTargetEquipmentUntilSourceLeaves'),
            ('CoreRuleEngine.PermanentPlayTargets.cs', '!current.PaidPlayTargetsOptional'),
            ('CoreRuleEngine.FieldTriggers.cs', 'TargetGenerations = selected.ToDictionary'),
            ('CoreRuleEngine.EquipmentTheft.cs', 'MovementTargetIsCurrent(current, item, target)'),
            ('CoreRuleEngine.EquipmentTheft.cs', 'PayImposedMovementCost('),
            ('CoreRuleEngine.EquipmentTheft.cs', 'MoveAttachedEquipmentWithHost(zones, attached, item.ControllerId, target, MoveUnitBaseZone)'),
            ('CoreRuleEngine.EquipmentTheft.cs', 'if (hasSource)'),
            ('CoreRuleEngine.EquipmentTheft.cs', 'TryAttachEquipment('),
            ('CoreRuleEngine.EquipmentTheft.cs', 'source.ObjectGeneration == grant.Source.Generation'),
            ('CoreRuleEngine.PaidPermanentPlayHistory.cs', 'item.PermanentPlaySource'),
            ('CoreRuleEngine.EquipmentControlHistory.cs', '!string.IsNullOrWhiteSpace(grant.Source.ObjectId)'),
            ('MatchRecovery.EquipmentControlHistory.cs', 'proof != row.Proof')]:
            with self.subTest(token=token):
                path = 'src/Riftbound.Engine/' + filename
                original = self.sources.files[path]
                self.assertIn(token, original)
                self.sources.files[path] = original.replace(token, 'DISABLED') + '\n    private static void UnusedAuditDecoy() { ' + token + '; }\n'
                try:
                    self.assertEqual(['missing'] * 4, [item['status'] for item in self.outcomes()])
                finally:
                    self.sources.files[path] = original

    def test_the_paired_text_and_original_scope_are_required(self):
        clause = audit.card_clauses(self.value)[1]
        clause['scope'] = '当我据守时，抽一张牌。'
        self.assertEqual('missing', audit.paid_equipment_theft_route(self.value, clause, self.sources)['status'])
        self.value['cardEffect'] = FEE
        self.assertEqual('missing', self.outcomes()[0]['status'])


if __name__ == '__main__':
    unittest.main()
