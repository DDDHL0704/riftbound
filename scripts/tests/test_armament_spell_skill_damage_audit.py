import copy
import unittest
from test_effect_clause_audit import audit


class ArmamentSpellSkillDamageAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(audit.ROOT, [])
        cls.text = '你的法术和技能造成的伤害+3（如果已贴附此牌）。'
        cls.card = dict(cardNo='SFD·191/221', cardCategory='exclusive_equipment', cardEffect='{{唯我}}',
            armamentText=dict(lowerText=cls.text),
            armament=dict(effectText=cls.text, spellSkillDamageBonus=3, grantedKeywords=[]))
        cls.clause = next(row for row in audit.card_clauses(cls.card) if row['location'] == 'armament')

    def outcome(self, card=None, clause=None, sources=None):
        answer = audit.armament_spell_skill_damage_route(card or self.card, clause or self.clause, sources or self.sources)
        return answer['status'] if answer else None

    def mutate(self, filename, token, replacement='REMOVED_CONSUMER', decoy=True):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(token, sources.files[path])
        sources.files[path] = sources.files[path].replace(token, replacement)
        if decoy:
            sources.files[path] += '\n    private static void UnusedDamageAuditDecoy() { ' + token + '; }\n'
        return sources

    def test_exact_lower_line_and_actual_shared_consumers_route(self):
        self.assertEqual('route', self.outcome())

    def test_missing_or_untyped_numeric_runtime_cannot_borrow_base_might(self):
        for value in [None, 0, 1, 2, 4, True, 3.0, '3']:
            card = copy.deepcopy(self.card); card['armament']['spellSkillDamageBonus'] = value
            card['armament']['baseMightBonus'] = 3
            self.assertEqual('missing', self.outcome(card=card))

    def test_upper_box_category_and_conditional_scope_cannot_borrow_the_lower_line(self):
        for key, value in [('location', 'upper'), ('kind', 'activated'), ('scope', self.text + '每回合首次')]:
            self.assertEqual('missing', self.outcome(clause=dict(self.clause, **{key: value})))
        card = copy.deepcopy(self.card); card['cardCategory'] = 'unit'
        self.assertEqual('missing', self.outcome(card=card))

    def test_other_parenthetical_conditions_are_not_discarded_as_reminders(self):
        for text in ['你的法术和技能造成的伤害+3（如果控制四名单位）。', '你的法术和技能造成的伤害+3。']:
            clause = dict(self.clause, text=text, scope=text)
            self.assertEqual('missing', self.outcome(clause=clause))

    def test_unknown_lower_runtime_semantics_keep_the_exact_family_missing(self):
        for key, value in [('perTargetBonus', 3), ('grantedKeywords', ['游走']), ('friendlyUnitsHereKeywords', ['游走'])]:
            card = copy.deepcopy(self.card); card['armament'][key] = value
            self.assertEqual('missing', self.outcome(card=card))

    def test_removed_catalog_or_damage_consumer_is_not_replaced_by_a_decoy(self):
        for filename, token in [
            ('CoreRuleEngine.ArmamentSpellDamage.cs', 'OfficialArmamentCatalog.Find(face)?.SpellSkillDamageBonus ?? 0'),
            ('CoreRuleEngine.DamageDistribution.cs', 'bonuses.AddRange(AttachedArmamentSpellSkillDamageBonuses(state, controller))'),
            ('CoreRuleEngine.cs', 'DistributionBonuses(state, controllerId, null).Sum(b => b.Amount)'),
            ('CoreRuleEngine.DistributedSpellDamage.cs', 'DistributionBonuses(state, player, targets.Select(id => state.ObjectLocations.GetValueOrDefault(id))'),
            ('CoreRuleEngine.AttackAreaDamage.cs', 'DistributionBonuses(state, item.ControllerId, null).Sum(bonus => bonus.Amount)')]:
            self.assertEqual('missing', self.outcome(sources=self.mutate(filename, token)))

    def test_the_current_host_controls_the_bonus_independently_of_gear_ownership(self):
        token = 'EffectiveFieldControllerId(state.PlayerZones, hostId, host) != controller'
        for replacement in ['gear.ControllerId != controller', 'gear.OwnerId != controller']:
            self.assertEqual('missing', self.outcome(sources=self.mutate('CoreRuleEngine.ArmamentSpellDamage.cs', token, replacement)))

    def test_face_up_attachment_and_same_public_location_are_required(self):
        for token in ['amount <= 0 || gear.IsFaceDown', 'out var host) || host.IsFaceDown',
            '!IsObjectOnField(state.PlayerZones, gear.ObjectId)', '!IsObjectOnField(state.PlayerZones, hostId)',
            '!IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, hostId)']:
            self.assertEqual('missing', self.outcome(sources=self.mutate('CoreRuleEngine.ArmamentSpellDamage.cs', token)))

    def test_face_up_standby_text_does_not_hide_an_otherwise_attached_source(self):
        for token in ['gear.Tags.Contains(CardObjectTags.Standby)', 'host.Tags.Contains(CardObjectTags.Standby)']:
            sources = self.mutate('CoreRuleEngine.ArmamentSpellDamage.cs', 'amount <= 0 || gear.IsFaceDown',
                'amount <= 0 || ' + token + ' || gear.IsFaceDown', decoy=False)
            self.assertEqual('missing', self.outcome(sources=sources))

    def test_original_capture_receipt_and_real_decline_retirement_are_required(self):
        for filename, token in [
            ('CoreRuleEngine.cs', 'payload[ArmamentDamageCaptureHashKey] = ArmamentDamageCaptureHash(trigger.DamageDistribution!'),
            ('CoreRuleEngine.FieldTriggers.cs', 'payload[DiscardedArmamentDamageHashKey] = MatchStateHasher.HashValue(item.StackItemId)'),
            ('MatchRecovery.cs', 'ValidateArmamentDamageBonusHistory(authoritativeState, commands, events, errors)')]:
            self.assertEqual('missing', self.outcome(sources=self.mutate(filename, token)))

    def test_recovery_checks_origin_and_parent_against_original_accepted_history(self):
        for token in ['state.PendingRuleChoice?.Origin', 'pending.Parent',
            'command.Accepted && command.CompletedTick == entry.Tick', 'expected.ExceptWith(retired)',
            '!expected.SetEquals(current.Select(item => item.Id))']:
            self.assertEqual('missing', self.outcome(sources=self.mutate('MatchRecovery.ArmamentDamageBonuses.cs', token)))

    def test_the_history_reader_handles_the_original_null_payload_boundary(self):
        token = 'entry.Event.Payload?.ContainsKey(CoreRuleEngine.ArmamentDamageCaptureHashKey) == true'
        self.assertEqual('missing', self.outcome(sources=self.mutate('MatchRecovery.ArmamentDamageBonuses.cs', token,
            'entry.Event.Payload.ContainsKey(CoreRuleEngine.ArmamentDamageCaptureHashKey)')))

    def test_mutable_resolution_fields_cannot_be_hashed_into_birth_receipts(self):
        for token in ['context.SourceLocation', 'context.Assignments', 'context.ResolutionDamage']:
            self.assertEqual('missing', self.outcome(sources=self.mutate('CoreRuleEngine.ArmamentSpellDamage.cs',
                'context.InitialDamage, context.InitialBonuses,', 'context.InitialDamage, context.InitialBonuses, ' + token + ',', decoy=False)))

    def test_existing_annie_equipment_skill_and_activated_spell_bonuses_are_preserved(self):
        for filename, token in [
            ('CoreRuleEngine.DamageDistribution.cs', 'AttachedRuleTextRules.Instances(state, source)'),
            ('CoreRuleEngine.DamageDistribution.cs', 'BattlefieldStaticAbilitySpecRules.IsBattlefieldTargetSpellSkillDamageBonusAbility'),
            ('CoreRuleEngine.cs', 'EquipmentSkillDamageBonus(state, controllerId, sourceItem)'),
            ('CoreRuleEngine.cs', 'ActivatedSpellDamageBonus(state, sourceItem)')]:
            self.assertEqual('missing', self.outcome(sources=self.mutate(filename, token)))

    def test_unrelated_lower_text_is_outside_this_family(self):
        for text in ['{{强攻3}}', '我造成的战斗伤害+3。', '你在此处的单位获得{{游走}}。']:
            self.assertIsNone(self.outcome(clause=dict(self.clause, text=text, scope=text)))
