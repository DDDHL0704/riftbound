import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('armament_trait_aura_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class ArmamentTraitAuraAuditTests(unittest.TestCase):
    def setUp(self):
        self.sources = audit.Sources(ROOT, [])
        self.trait = dict(cardNo='SFD·073/221', cardCategory='equipment', armament=dict(
            effectText='我拥有“机械”属性。', grantedTraits=['机械'], friendlyUnitsHereKeywords=[], grantedKeywords=[]))
        self.aura = dict(cardNo='SFD·192/221', cardCategory='exclusive_equipment', armament=dict(
            effectText='你在此处的单位获得{{游走}}。（我们可以向其他战场进行移动。）',
            grantedTraits=[], friendlyUnitsHereKeywords=['游走'], grantedKeywords=[]))

    def outcome(self, card=None, sources=None, **clause_changes):
        card = card or self.trait
        clause = dict(audit.split_clauses(card['armament']['effectText'], 'unit')[0], location='armament')
        clause.update(clause_changes)
        return audit.armament_trait_aura_route(card, clause, sources or self.sources)

    def mutate(self, filename, old, new, decoy=False):
        sources = copy.copy(self.sources)
        sources.files = dict(self.sources.files)
        path = 'src/Riftbound.Engine/' + filename
        self.assertEqual(1, sources.files[path].count(old), old)
        sources.files[path] = sources.files[path].replace(old, new, 1)
        if decoy:
            sources.files[path] += '\n    private static string UnusedEvidence() => "' + old + '";\n'
        return sources

    def test_both_exact_lower_box_routes_have_live_current_consumers(self):
        for card in (self.trait, self.aura):
            with self.subTest(card=card['cardNo']): self.assertEqual('route', self.outcome(card)['status'])

    def test_a_host_self_keyword_cannot_stand_in_for_a_friendly_here_aura(self):
        card = copy.deepcopy(self.aura)
        card['armament'].update(grantedKeywords=['游走'], friendlyUnitsHereKeywords=[])
        self.assertEqual('missing', self.outcome(card)['status'])

    def test_wrong_traits_and_other_semantic_fields_do_not_certify_the_sentence(self):
        for changes in [dict(grantedTraits=['精魂']), dict(grantedTraits=['机械', '精魂']), dict(copiesHostPrintedRulesText=True)]:
            card = copy.deepcopy(self.trait); card['armament'].update(changes)
            self.assertEqual('missing', self.outcome(card)['status'])

    def test_upper_text_or_a_different_full_scope_cannot_borrow_the_lower_grant(self):
        self.assertEqual('missing', self.outcome(location='upper')['status'])
        self.assertEqual('missing', self.outcome(scope='我拥有“机械”属性。如果我进攻，抽一张牌。')['status'])

    def test_mentioned_or_conditional_traits_are_not_unconditional_self_traits(self):
        for text in ['你的机械单位战力+1。', '如果我有增益，我拥有“机械”属性。']:
            self.assertIsNone(self.outcome(text=text, scope=text))

    def test_an_unused_provider_does_not_replace_the_live_rule_layer_call(self):
        sources = self.mutate('CoreRuleEngine.RuleDerivedKeywords.cs',
            '.Concat(AttachedArmamentTraitAndHereGrants(state))', '', decoy=True)
        self.assertEqual('missing', self.outcome(sources=sources)['status'])

    def test_the_ordinary_weapon_and_everywhere_keyword_routes_are_still_preserved(self):
        for token in ['AttachedArmamentKeywordGrants(state)', '.Concat(everywhereKeywords)']:
            sources = self.mutate('CoreRuleEngine.RuleDerivedKeywords.cs', token, '', decoy=True)
            self.assertEqual('missing', self.outcome(sources=sources)['status'])

    def test_hidden_or_detached_sources_require_the_actual_host_and_gear_checks(self):
        for old, new in [('hostId || gear.IsFaceDown', 'hostId'),
            ('out var host) || host.IsFaceDown', 'out var host)'),
            ('!IsObjectOnField(state.PlayerZones, hostId)', 'false')]:
            sources = self.mutate('CoreRuleEngine.ArmamentKeywords.cs', old, new, decoy=True)
            self.assertEqual('missing', self.outcome(sources=sources)['status'])

    def test_here_and_friendship_follow_the_host_rather_than_the_equipment_controller(self):
        for old, new in [
            ('var controller = EffectiveFieldControllerId(state.PlayerZones, hostId, host)',
                'var controller = EffectiveFieldControllerId(state.PlayerZones, gear.ObjectId, gear)'),
            ('!IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, hostId)', 'false')]:
            sources = self.mutate('CoreRuleEngine.ArmamentKeywords.cs', old, new, decoy=True)
            self.assertEqual('missing', self.outcome(self.aura, sources)['status'])

    def test_the_aura_includes_its_host_and_face_up_units_with_standby_text(self):
        for inserted in ['target.ObjectId != hostId', '!target.Tags.Contains(CardObjectTags.Standby)']:
            sources = self.mutate('CoreRuleEngine.ArmamentKeywords.cs',
                'Where(target => IsFaceUpNonStandbyUnit(target)', 'Where(target => ' + inserted + ' && IsFaceUpNonStandbyUnit(target)')
            self.assertEqual('missing', self.outcome(self.aura, sources)['status'])

    def test_a_real_printed_trait_basis_is_required_before_removing_the_old_layer(self):
        sources = self.mutate('CoreRuleEngine.RuleDerivedKeywords.cs',
            '.Concat(PrintedCardFactory.PrintedTags(AttachedRuleTextRules.Face(card)))', '', decoy=True)
        self.assertEqual('missing', self.outcome(sources=sources)['status'])

    def test_a_derived_tag_without_its_real_roam_permission_consumer_is_insufficient(self):
        sources = self.mutate('CoreRuleEngine.cs',
            '!HasRoamPermission(state, intent.PlayerId, command.SourceObjectId, sourceState)', 'false', decoy=True)
        self.assertEqual('missing', self.outcome(self.aura, sources)['status'])

    def test_an_unused_trait_reader_does_not_replace_the_actual_grant(self):
        sources = self.mutate('CoreRuleEngine.ArmamentKeywords.cs',
            'grants.AddRange((definition.GrantedTraits ?? []).Select(trait => (hostId, trait)))', '', decoy=True)
        self.assertEqual('missing', self.outcome(sources=sources)['status'])
