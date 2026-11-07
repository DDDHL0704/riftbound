import copy
import unittest
from test_effect_clause_audit import audit


class ProvidedArmamentKeywordAuditTests(unittest.TestCase):
    TEXT = '你的每件武装都会提供{{强攻}}。（如果配装的单位是进攻方，则{{S}}+1。）'

    @classmethod
    def setUpClass(cls):
        cls.sources = audit.Sources(audit.ROOT, [])

    def card(self):
        return dict(cardNo='SFD·183/221', cardName='圣枪游侠', cardCategory='legendary', cardEffect=self.TEXT,
            staticAuras=[dict(node=dict(kind='FRIENDLY_ARMAMENT_PROVIDED_KEYWORD', layer='RULE_TEXT',
                duration='WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD', targetScope='FRIENDLY_EQUIPMENT',
                participantScope='FRIENDLY_PUBLIC_FIELD_EQUIPMENT', targetFilter='TAG:武装', grantedKeyword='强攻'), predicates=[])])

    def outcome(self, card=None, sources=None):
        card = card or self.card()
        return audit.audit_clause(card, audit.card_clauses(card)[0], sources or self.sources)['status']

    def changed(self, filename, old, new='REMOVED_CONSUMER'):
        result = copy.copy(self.sources)
        result.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertIn(old, result.files[path])
        result.files[path] = result.files[path].replace(old, new)
        return result

    def test_full_profile_and_shared_live_keyword_route_are_required(self):
        self.assertEqual('route', self.outcome())
        card = self.card(); card['cardNo'] = 'SFD·241/221'
        self.assertEqual('route', self.outcome(card))

    def test_registration_or_a_different_equipment_aura_does_not_provide_the_host_keyword(self):
        for profile in [[], [dict(node=dict(kind='FRIENDLY_EQUIPMENT_KEYWORD', grantedKeyword='强攻'), predicates=[])]]:
            card = self.card(); card['staticAuras'] = profile
            self.assertEqual('missing', self.outcome(card))

    def test_wrong_controller_scope_keyword_layer_or_extra_condition_cannot_certify_the_clause(self):
        for key, value in [('kind','SOURCE_ATTACKING_POWER'),('layer','POWER'),('targetScope','ENEMY_EQUIPMENT'),
                ('participantScope','FRIENDLY_PUBLIC_FIELD_UNITS'),('duration','UNTIL_END_OF_TURN'),
                ('targetFilter',''),('grantedKeyword','坚守'),('requiredPowerThreshold',5)]:
            with self.subTest(key=key):
                card = self.card(); card['staticAuras'][0]['node'][key] = value
                self.assertEqual('missing', self.outcome(card))

    def test_the_weapons_controller_and_the_legends_public_control_must_both_be_read(self):
        for token in ['EffectiveFieldControllerId(state.PlayerZones, gear.ObjectId, gear)',
                      'EffectivePublicStaticAuraSourceControllerId(state.PlayerZones, sourceId, source)']:
            self.assertEqual('missing', self.outcome(sources=self.changed('CoreRuleEngine.ArmamentKeywords.cs', token)))

    def test_a_profile_and_unused_provider_are_not_enough_without_the_host_layer(self):
        for filename, token in [('CoreRuleEngine.ArmamentKeywords.cs','.Concat(provided.GetValueOrDefault('),
                ('CoreRuleEngine.RuleDerivedKeywords.cs','.Concat(armamentKeywords.GetValueOrDefault(id) ?? [])'),
                ('CoreRuleEngine.CountedCombatKeywords.cs','CombatKeywordAmount(unit.RuleDerivedTags ?? [], keyword)')]:
            with self.subTest(filename=filename):
                self.assertEqual('missing', self.outcome(sources=self.changed(filename,token)))

    def test_battle_damage_also_requires_a_live_keyword_refresh(self):
        self.assertEqual('missing', self.outcome(sources=self.changed('CoreRuleEngine.cs',
            'state = RecomputeRuleDerivedKeywords(state with { PlayerZones = playerZones });')))

    def test_battle_refresh_must_not_reenter_its_own_keyword_dependencies(self):
        self.assertEqual('missing', self.outcome(sources=self.changed('CoreRuleEngine.cs',
            'if (conditionalPowerAuras)\n        {\n            state = RecomputeRuleDerivedKeywords(state with { PlayerZones = playerZones });',
            'state = RecomputeRuleDerivedKeywords(state with { PlayerZones = playerZones });')))

    def test_weapons_must_still_be_qualified_and_attached_at_the_same_public_location(self):
        for token in ['IsArmamentKeywordProvider(gear)',
                      'IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, host)']:
            self.assertEqual('missing', self.outcome(sources=self.changed('CoreRuleEngine.ArmamentKeywords.cs',token)))

    def test_the_old_combat_only_bonus_must_not_double_the_new_keyword(self):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        sources.files['src/Riftbound.Engine/CoreRuleEngine.cs'] += '\nCountLucianLegendEquipmentAssaultBonus(state, zones, id);\n'
        self.assertEqual('missing', self.outcome(sources=sources))

    def test_larger_scope_is_not_an_unconditional_weapon_grant(self):
        card = self.card(); clause = audit.card_clauses(card)[0]
        clause['scope'] = '如果我处于战场上，则' + clause['scope']
        self.assertEqual('missing', audit.audit_clause(card,clause,self.sources)['status'])

    def test_standby_keywords_do_not_disable_face_up_weapons_hosts_or_sources(self):
        for name in ('gear', 'unit', 'source'):
            with self.subTest(name=name):
                old = name + '.IsFaceDown'
                new = old + (' || ' if name == 'source' else ' && !') + name + '.Tags.Contains(CardObjectTags.Standby)'
                self.assertEqual('missing', self.outcome(sources=self.changed('CoreRuleEngine.ArmamentKeywords.cs', old, new)))

    def test_lower_box_keywords_require_public_binding_without_standby_keyword_exclusions(self):
        card = dict(armament=dict(grantedKeywords=['强攻2']))
        clause = dict(text='{{强攻2}}', kind='keyword', scope='{{强攻2}}', location='armament')
        self.assertEqual('route', audit.audit_armament_clause(card, clause, self.sources)['status'])
        for token in ['!gear.IsFaceDown', '!unit.IsFaceDown',
                      'IsObjectOnField(state.PlayerZones, gear.ObjectId)', 'IsObjectOnField(state.PlayerZones, host)']:
            with self.subTest(token=token):
                sources = self.changed('CoreRuleEngine.ArmamentKeywords.cs', token)
                self.assertEqual('missing', audit.audit_armament_clause(card, clause, sources)['status'])
        for name in ('gear', 'unit'):
            token = '!' + name + '.IsFaceDown'
            sources = self.changed('CoreRuleEngine.ArmamentKeywords.cs', token,
                token + ' && !' + name + '.Tags.Contains(CardObjectTags.Standby)')
            self.assertEqual('missing', audit.audit_armament_clause(card, clause, sources)['status'])
