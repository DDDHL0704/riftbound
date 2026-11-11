import copy
import importlib.util
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parents[2]
SPEC = importlib.util.spec_from_file_location('alternative_echo_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class AlternativeEchoAuditTests(unittest.TestCase):
    def setUp(self):
        self.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        text = ('{{回响}} — 支付{{1}}/支付{{A}}/支付{{1}}和{{A}}（你可以选择支付各额外费用一次，以重复多次此法术效果。）\n'
            '从以下中选择一个未选过的效果 —\n- 抽一张牌。\n- 对战场上的一名单位造成2点伤害。\n'
            '- 对基地中的一名单位造成3点伤害。\n- 让战场上的一名单位在本回合内{{S}}-4。')
        modes = [dict(mode='DRAW_1', effectKind='PERFECT_FINALE_DRAW_1', drawCount=1),
            dict(mode='BATTLEFIELD_UNIT_DAMAGE_2', effectKind='PERFECT_FINALE_BATTLEFIELD_DAMAGE_2', requiredTargetCount=1, damageAmount=2, targetScope='BATTLEFIELD_UNIT'),
            dict(mode='BASE_UNIT_DAMAGE_3', effectKind='PERFECT_FINALE_BASE_DAMAGE_3', requiredTargetCount=1, damageAmount=3, targetScope='BASE_UNIT'),
            dict(mode='BATTLEFIELD_UNIT_POWER_MINUS_4', effectKind='PERFECT_FINALE_BATTLEFIELD_POWER_MINUS_4', requiredTargetCount=1, powerModifierAmount=-4, targetScope='BATTLEFIELD_UNIT')]
        return dict(cardNo='UNL-182/219', cardName='完美谢幕', cardCategory='exclusive_spell', cardEffect=text,
            supportsEchoRepeat=True, printedEchoCosts=[dict(id='ECHO', mana=1), dict(id='ECHO:PRINTED:2', genericPower=1),
                dict(id='ECHO:PRINTED:3', mana=1, genericPower=1)],
            behaviors=[dict(manaCost=4, requiresDistinctRepeatModes=True, **mode) for mode in modes])

    def answers(self, card=None, sources=None):
        card = card or self.card()
        return [audit.printed_alternative_echo_route(card, row, sources or self.sources)
            for row in audit.card_clauses(card)[:3]]

    def missing(self, card=None, sources=None):
        self.assertEqual(['missing'] * 3, [answer['status'] for answer in self.answers(card, sources)])

    def change(self, filename, member, old, new='removed', decoy=True):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        key = 'src/Riftbound.Engine/' + filename; code = sources.files[key]
        start = re.search(r'(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b' + re.escape(member) + r'\s*\(', code)
        self.assertIsNotNone(start)
        tail = code[start.end():]; end = re.search(r'\n    (?:private|internal|public) (?:static )?', tail)
        length = end.start() if end else len(tail); body = tail[:length]; self.assertIn(old, body)
        sources.files[key] = code[:start.end()] + body.replace(old, new) + tail[length:]
        if decoy: sources.files[key] += '\n    private static void UnusedFinaleDecoy() { ' + body + ' }\n'
        return sources

    def test_three_original_clause_ids_and_scopes_are_preserved(self):
        rows = audit.card_clauses(self.card())
        self.assertEqual(7, len(rows))
        self.assertEqual('{{回响}}', rows[0]['text'])
        self.assertTrue(rows[1]['text'].startswith('— 支付{{1}}/'))
        self.assertEqual('从以下中选择一个未选过的效果 —', rows[2]['text'])
        self.assertEqual(['route'] * 3, [answer['status'] for answer in self.answers()])

    def test_each_independent_fee_has_its_printed_resource_amount_and_unique_identity(self):
        for index, changes in [(0, dict(mana=2)), (1, dict(genericPower=0)), (2, dict(mana=0)),
            (1, dict(typedPower={'red': 1})), (2, dict(id='ECHO')), (0, dict(mana=True)),
            (2, dict(printedCardNo='UNL-182/219'))]:
            with self.subTest(index=index, changes=changes):
                card = self.card(); card['printedEchoCosts'][index].update(changes); self.missing(card)
        card = self.card(); card['printedEchoCosts'].pop(); self.missing(card)

    def test_all_four_modes_must_share_the_distinctness_constraint_and_exact_price(self):
        for index in range(4):
            for changes in [dict(requiresDistinctRepeatModes=False), dict(manaCost=3), dict(mode='OTHER')]:
                card = self.card(); card['behaviors'][index].update(changes); self.missing(card)
        card = self.card(); card['behaviors'].append(copy.deepcopy(card['behaviors'][0])); self.missing(card)

    def test_each_effect_and_target_zone_is_checked_and_unprinted_operations_are_rejected(self):
        for index, changes in [(0, dict(drawCount=2)), (1, dict(damageAmount=3)),
            (2, dict(targetScope='BATTLEFIELD_UNIT')), (3, dict(powerModifierAmount=-3)),
            (1, dict(grantsBoon=True)), (0, dict(requiredTargetCount=1))]:
            card = self.card(); card['behaviors'][index].update(changes); self.missing(card)

    def test_permanent_play_and_an_unreviewed_mode_set_cannot_bypass_the_legacy_distinct_repeat_guard(self):
        for change in [dict(playsSourceToBaseAsUnit=True), dict(playsSourceToBaseAsEquipment=True),
            dict(spellModes={'choiceCount': 1})]:
            card = self.card(); card['behaviors'][0].update(change); self.missing(card)

    def test_parser_reads_every_declared_fee_and_assigns_independent_ids(self):
        for old in ['Regex.Matches(', ".Split('/')", 'costs.Count == 0 ? EchoOptionalCostNames.Echo : $"ECHO:PRINTED:{costs.Count + 1}"']:
            self.missing(sources=self.change('EchoCostRules.cs', 'ParsePrintedCosts', old))

    def test_serialized_counter_destination_is_neutral_only_when_the_counter_instruction_is_disabled(self):
        card = self.card()
        for profile in card['behaviors']: profile['counteredStackItemDestination'] = 'GRAVEYARD'
        self.assertEqual(['route'] * 3, [answer['status'] for answer in self.answers(card)])
        card['behaviors'][0]['countersTargetStackSpell'] = True; self.missing(card)
        card['behaviors'][0]['countersTargetStackSpell'] = False
        card['behaviors'][0]['counteredStackItemDestination'] = 'BANISHED'; self.missing(card)

    def test_availability_and_selection_use_all_printed_fees_without_paying_unselected_ones(self):
        for member, old in [('Available', 'costs.AddRange(PrintedCostsFor(behavior.CardNo));'),
            ('Selected', 'Available(state, player, behavior).Where(cost => choices.Contains(cost.Id, StringComparer.Ordinal))')]:
            self.missing(sources=self.change('EchoCostRules.cs', member, old))

    def test_actual_cost_planner_charges_every_selected_resource_and_counts_extra_executions(self):
        for old in ['EchoCostRules.Selected(state, playerId, behavior, normalizedOptionalCosts)',
            'echoCosts.Sum(cost => cost.Mana)', 'echoCosts.Sum(cost => cost.GenericPower)',
            'PrintedPowerCostRules.Combine(typed, cost.TypedPower)', 'effectRepeatCount = 1 + echoCosts.Count']:
            self.missing(sources=self.change('CoreRuleEngine.cs', 'TryBuildOptionalCostPlan', old))

    def test_live_plan_and_quote_use_the_same_repeat_validation(self):
        for filename, member, old in [('CoreRuleEngine.cs', 'TryBuildPlayCardPlan', 'TryBuildRepeatExecutions(state, intent, command, behavior, targetObjectIds,'),
            ('CoreRuleEngine.cs', 'ResolvePlayCard', 'TryBuildPlayCardPlan(state, intent, command'),
            ('CoreRuleEngine.PlayCostPreview.cs', 'PreviewPlayCard', 'TryBuildPlayCardPlan(state,')]:
            self.missing(sources=self.change(filename, member, old))

    def test_prompt_offers_the_real_independent_ids_and_complete_costs(self):
        for old in ['EchoCostRules.Available(state, playerId, behavior)', 'Math.Max(0, echo.Mana - echoReduction)', 'echo.TypedPower']:
            self.missing(sources=self.change('MatchSession.cs', 'PlayCardOptionalCostChoicesForBehavior', old))

    def test_mode_uniqueness_is_checked_against_all_already_chosen_executions(self):
        for old in ['behavior.RequiresDistinctRepeatModes', 'result.Any(execution => execution.EffectKind == repeatPlan.Behavior.EffectKind)', 'choices.Count != count - 1']:
            self.missing(sources=self.change('CoreRuleEngine.Repeats.cs', 'TryBuildRepeatExecutions', old))

    def test_recovery_checks_the_captured_plan_in_its_live_validation_method(self):
        self.missing(sources=self.change('MatchRecovery.cs', 'ValidateSpellContinuations', 'CoreRuleEngine.ValidDistinctRepeatModes(item)'))
        self.missing(sources=self.change('CoreRuleEngine.Repeats.cs', 'ValidDistinctRepeatModes',
            'executions.Select(execution => execution.EffectKind).Distinct(StringComparer.Ordinal).Count() == executions.Count'))

    def test_each_execution_resolves_once_without_a_second_card_play_or_payment(self):
        for old in ['ResolveStackItemEffect(current, item', 'EffectRepeatCount = 1, RepeatExecutions = null, PlayCost = null', 'SPELL_EXECUTION_COMPLETED']:
            self.missing(sources=self.change('CoreRuleEngine.Repeats.cs', 'ResolveSeparateSpellExecutions', old))

    def test_other_scopes_and_card_text_are_not_certified_by_the_finale_profile(self):
        card = self.card(); row = audit.card_clauses(card)[2]; row['scope'] = '来自其他技能：' + row['scope']
        self.assertEqual('missing', audit.printed_alternative_echo_route(card, row, self.sources)['status'])
        card = self.card(); card['cardEffect'] = card['cardEffect'].replace('支付{{1}}/', '支付{{2}}/')
        self.assertEqual([None] * 3, self.answers(card))
        card = self.card(); card['cardCategory'] = 'unit'; self.missing(card)


if __name__ == '__main__':
    unittest.main()
