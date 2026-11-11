import copy
import importlib.util
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parents[2]
SPEC = importlib.util.spec_from_file_location('ava_base_cost_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class AvaBaseCostAuditTests(unittest.TestCase):
    def setUp(self): self.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        spec = dict(timing='ATTACK', effect='PLAY_HAND_STANDBY_HERE', leadingPowerCost=1, leadingPowerTrait='blue')
        return dict(cardNo='OGN·107/298', cardCategory='unit',
            cardEffect='当我进攻时，你可以选择支付{{蓝色}}，以此从你的手牌中正面朝上打出一张带有{{待命}}技能的卡牌，无视费用。',
            unitEventAbility=spec, behaviors=[dict(playsSourceToBaseAsUnit=True, unitEventAbility=copy.deepcopy(spec))])

    def answer(self, card=None, sources=None):
        card = card or self.card()
        return audit.standby_hand_base_cost_waiver_route(card, audit.card_clauses(card)[0], sources or self.sources)

    def change(self, filename, member, old, new='removed', decoy=True):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        key = 'src/Riftbound.Engine/' + filename; code = sources.files[key]
        start = re.search(r'(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b' + re.escape(member) + r'\s*\(', code)
        self.assertIsNotNone(start)
        tail = code[start.end():]; end = re.search(r'\n    (?:private|internal|public) (?:static )?', tail)
        length = end.start() if end else len(tail); body = tail[:length]; self.assertIn(old, body)
        sources.files[key] = code[:start.end()] + body.replace(old, new) + tail[length:]
        if decoy: sources.files[key] += '\n    private static void UnusedAvaDecoy() { ' + body + ' }\n'
        return sources

    def test_the_exact_blue_confirmation_and_base_only_waiver_have_live_consumers(self):
        self.assertEqual('route', self.answer()['status'])

    def test_all_profiles_must_agree_on_the_actual_attack_and_blue_cost(self):
        for changes in [dict(timing='PLAY'), dict(leadingPowerTrait='red'), dict(leadingPowerCost=0),
            dict(leadingPowerCost=True), dict(leadingManaCost=1), dict(optional=True)]:
            card = self.card(); card['behaviors'][0]['unitEventAbility'].update(changes)
            self.assertEqual('missing', self.answer(card)['status'])
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.answer(card)['status'])

    def test_this_exact_definition_cannot_be_promoted_to_ignore_every_cost(self):
        sources = self.change('CoreRuleEngine.EffectPlay.cs', 'TryGetEffectPlayDefinition',
            'EffectPlayIgnoreAllCosts: false', 'EffectPlayIgnoreAllCosts: true')
        self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_the_base_waiver_and_hand_standby_location_restrictions_cannot_be_certified_by_a_dead_definition(self):
        for old in ['EffectPlayIgnoreBaseMana: true', 'EffectPlayIgnoreBasePower: true',
            'EffectPlayRequiresStandby: true', 'EffectPlayDestination: "SOURCE_HERE_IF_UNIT"']:
            self.assertEqual('missing', self.answer(sources=self.change('CoreRuleEngine.EffectPlay.cs', 'TryGetEffectPlayDefinition', old))['status'])

    def test_recovery_requires_base_only_permission_and_its_original_trigger_receipt(self):
        for old in ['ValidTriggerCostReceipt(state, parent)',
            'pending.SourceZone != "HAND" || pending.IgnoreAllCosts || !pending.IgnoreBaseMana || !pending.IgnoreBasePower']:
            self.assertEqual('missing', self.answer(sources=self.change('CoreRuleEngine.StandbyHandPlay.cs', 'ValidStandbyHandPlayHere', old))['status'])

    def test_live_source_here_is_bound_to_the_original_incarnation(self):
        old = 'CombatMightSourceLocation(state, parent.SourceObjectId, parent.UnitEvent!.SourceGeneration)'
        self.assertEqual('missing', self.answer(sources=self.change('CoreRuleEngine.StandbyHandPlay.cs', 'StandbyHandPlayHere', old))['status'])

    def test_additional_mana_and_tax_survive_the_shared_quote(self):
        for old in ['effect?.IgnoreBaseMana == true', 'additionalMana', '+ increase + spellIncrease']:
            self.assertEqual('missing', self.answer(sources=self.change('CoreRuleEngine.PlayManaCost.cs', 'CalculatePlayManaCost', old))['status'])

    def test_base_only_power_keeps_the_original_behavior_instead_of_the_all_cost_branch(self):
        old = 'behavior with { IgnorePrintedPowerCost = pending.IgnoreBasePower }'
        self.assertEqual('missing', self.answer(sources=self.change('CoreRuleEngine.EffectPlay.cs', 'EffectPlayBehavior', old))['status'])

    def test_resources_and_refusal_are_connected_to_the_actual_effect_play_command(self):
        for old in ['ResolveRefusePlayPayment(state, intent, pending, refusal)', 'ResolveTapRune(state, intent, tap)',
            'ResolveRecycleRune(state, intent, recycle)', 'ResolvePlayCard(playState, intent, play)']:
            self.assertEqual('missing', self.answer(sources=self.change('CoreRuleEngine.EffectPlay.cs', 'ResolveEffectPlayCommand', old))['status'])

    def test_a_zero_resource_declaration_cannot_be_falsely_refused(self):
        self.assertEqual('missing', self.answer(sources=self.change('CoreRuleEngine.RefusedPlayPayment.cs',
            'ResolveRefusePlayPayment', 'plan.TotalManaCost == 0 && plan.TotalPowerCost == 0'))['status'])

    def test_other_text_and_scopes_do_not_inherit_the_same_payment_proof(self):
        card = self.card(); row = audit.card_clauses(card)[0]; row['scope'] = '他处文字：' + row['scope']
        self.assertEqual('missing', audit.standby_hand_base_cost_waiver_route(card, row, self.sources)['status'])
        card['cardEffect'] = card['cardEffect'].replace('无视费用', '无视一切费用')
        self.assertIsNone(self.answer(card))


if __name__ == '__main__': unittest.main()
