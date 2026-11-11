import copy
import importlib.util
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parents[2]
SPEC = importlib.util.spec_from_file_location('crew_equipment_audit', ROOT / 'scripts/audit-card-effects.py')
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class CrewEquipmentReadyAuditTests(unittest.TestCase):
    def setUp(self):
        self.sources = audit.Sources(ROOT, [])

    @staticmethod
    def card():
        return dict(cardNo='OGN·091/298', cardName='竞技场勤务小队', cardCategory='unit',
            cardEffect='当你打出一件装备时，让我变为活跃状态。',
            behaviors=[dict(playsSourceToBaseAsUnit=True,
                unitEventAbility=dict(timing='FRIENDLY_EQUIPMENT_PLAYED', effect='READY_SELF'))])

    def answer(self, card=None, row=None, sources=None):
        card = card or self.card()
        return audit.friendly_equipment_ready_route(card, row or audit.card_clauses(card)[0], sources or self.sources)

    def change(self, filename, member, old, new='removed', decoy=False):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        key = 'src/Riftbound.Engine/' + filename; code = sources.files[key]
        start = re.search(r'(?m)^    (?:private|internal|public) (?:static )?[^\n]*\b' + re.escape(member) + r'\s*\(', code)
        self.assertIsNotNone(start)
        tail = code[start.end():]
        end = re.search(r'\n    (?:private|internal|public) (?:static )?', tail)
        length = end.start() if end else len(tail)
        body = tail[:length]; self.assertIn(old, body)
        sources.files[key] = code[:start.end()] + body.replace(old, new) + tail[length:]
        if decoy:
            sources.files[key] += '\n    private static void UnusedCrewDecoy() { ' + body + ' }\n'
        return sources

    def test_the_precise_gear_observer_uses_live_capture_response_and_original_history(self):
        self.assertEqual('route', self.answer()['status'])

    def test_timing_effect_and_every_unprinted_meaningful_condition_are_rejected(self):
        for change in [dict(timing='PLAY'), dict(effect='DRAW'), dict(amount=1), dict(optional=True),
            dict(requiresPlayedUnitToken=True), dict(leadingManaCost=1), dict(oncePerTurn=True),
            dict(requiresEmpowered=True), dict(sourceInLegendZone=True), dict(anyNumberOfTargets=True)]:
            with self.subTest(change=change):
                card = self.card(); card['behaviors'][0]['unitEventAbility'].update(change)
                self.assertEqual('missing', self.answer(card)['status'])

    def test_donor_native_unit_route_and_all_exported_profiles_must_agree(self):
        card = self.card(); card['behaviors'][0]['playsSourceToBaseAsUnit'] = False
        self.assertEqual('missing', self.answer(card)['status'])
        card = self.card(); card['unitEventAbility'] = dict(timing='PLAY', effect='READY_SELF')
        self.assertEqual('missing', self.answer(card)['status'])
        card = self.card(); card['cardCategory'] = 'equipment'
        self.assertEqual('missing', self.answer(card)['status'])

    def test_other_card_text_and_granted_text_scope_cannot_be_silently_certified(self):
        card = self.card(); card['cardEffect'] = '当你打出一名单位时，让我变为活跃状态。'
        self.assertIsNone(self.answer(card))
        row = audit.card_clauses(self.card())[0]; row['scope'] = '其他来源授予：' + row['scope']
        self.assertEqual('missing', self.answer(row=row)['status'])

    def test_the_equipment_event_itself_requires_the_playing_players_current_control(self):
        sources = self.change('CoreRuleEngine.UnitEvents.cs', 'CaptureEquipmentPlayed',
            'EffectiveFieldControllerId(zones, c.ObjectId, c) == player',
            'EffectiveFieldControllerId(zones, c.ObjectId, c) != player', decoy=True)
        self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_hidden_off_field_and_copied_text_filters_are_live(self):
        for old in ['!card.IsFaceDown', 'IsFaceUpNonStandbyUnit(card)', 'IsObjectOnField(zones, id)',
            'AttachedRuleTextRules.Instances(zones, cards, card)', 'UnitEventTimingMatches(spec.Timing, timing)']:
            sources = self.change('CoreRuleEngine.UnitEvents.cs', 'CaptureUnitEvents', old, decoy=True)
            self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_real_queue_must_receive_captured_skills_instead_of_a_dead_helper(self):
        for old in ['UnitEventTrigger(captured,', 'queue.Add(trigger)', 'TriggerQueue = queue']:
            sources = self.change('CoreRuleEngine.UnitEvents.cs', 'QueueUnitEvents', old, decoy=True)
            self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_source_generation_field_presence_and_ready_instruction_are_bound_to_the_live_consumer(self):
        for old in ['source.ObjectGeneration != context.SourceGeneration', '!IsFaceUpNonStandbyUnit(source)',
            '!IsObjectOnField(state.PlayerZones, source.ObjectId)',
            'ApplyReadyState(source, behavior with { ReadiesTarget = true }']:
            sources = self.change('CoreRuleEngine.UnitEventBoonInstructions.cs', 'ResolveUnitEventBoonInstruction', old, decoy=True)
            self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_current_ready_prohibitions_are_checked_by_the_shared_ready_instruction(self):
        for old in ['CardStaticAbilitySpecRules.CardCannotBecomeActive(targetState.CardNo)',
            'ReadyByEffectPermittedByFieldRules(playerZones, cardObjects, targetState)']:
            sources = self.change('CoreRuleEngine.cs', 'ApplyReadyState', old, decoy=True)
            self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_both_real_equipment_and_real_token_entry_call_the_capture(self):
        for member, old in [('PlaySourceEquipmentToBase',
            'playedEvent = CaptureEquipmentPlayed(playedEvent, playerZones, cardObjects, equipmentState, stackItem.ControllerId);'),
            ('CreateLegendEquipmentToken', 'CaptureEquipmentPlayed(new GameEvent(')]:
            sources = self.change('CoreRuleEngine.cs', member, old, decoy=True)
            self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_the_previous_immediate_ready_shortcut_is_rejected(self):
        sources = copy.copy(self.sources); sources.files = dict(self.sources.files)
        key = 'src/Riftbound.Engine/CoreRuleEngine.cs'
        sources.files[key] += '\nResolveSourceReadyOnEquipmentPlayedTriggers();\n'
        self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_the_original_capture_hash_is_mandatory_for_this_timing(self):
        sources = self.change('CoreRuleEngine.ObservedUnitEvents.cs', 'RequiresObservedUnitEventHistory',
            '"FRIENDLY_EQUIPMENT_PLAYED"', decoy=True)
        self.assertEqual('missing', self.answer(sources=sources)['status'])
        sources = self.change('CoreRuleEngine.cs', 'BuildTriggerQueuedEvent',
            'payload["observedUnitEventCaptureHash"]', decoy=True)
        self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_pending_origin_and_erased_completed_observed_skills_use_original_records(self):
        for old in ['Scope(state.PendingRuleChoice?.Origin)', 'CoreRuleEngine.ObservedUnitEventCaptureHash',
            '!expected.SetEquals(current.Select(item => item.Id))']:
            sources = self.change('MatchRecovery.ObservedUnitEvents.cs', 'ValidateObservedUnitEventHistory', old, decoy=True)
            self.assertEqual('missing', self.answer(sources=sources)['status'])


    def test_the_actual_played_equipment_binding_is_captured_on_the_friendly_observation(self):
        sources = self.change('CoreRuleEngine.UnitEvents.cs', 'CaptureEquipmentPlayed',
            '"FRIENDLY_EQUIPMENT_PLAYED").Select(o => o with { Context = o.Context with { Other = new(', decoy=True)
        self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_a_simultaneous_printed_skill_must_reach_the_live_companion_coordinator(self):
        sources = self.change('CoreRuleEngine.UnitPlayMight.cs', 'CompanionPlayedUnit',
            'equipment.Other?.Binding', decoy=True)
        self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_companion_recovery_binds_the_original_equipment_capture_and_card_play(self):
        sources = self.change('CoreRuleEngine.UnitPlayMight.cs', 'ValidPermanentPlayCompanion',
            'equipment.CapturedTick == capturedTick', decoy=True)
        self.assertEqual('missing', self.answer(sources=sources)['status'])

    def test_an_unused_ordering_helper_does_not_certify_the_actual_resolution(self):
        sources = self.change('CoreRuleEngine.BattleResultContinuation.cs', 'CollectResolutionEffects',
            'DeferCompanionPermanentPlayOrdering(state, collected)', decoy=True)
        self.assertEqual('missing', self.answer(sources=sources)['status'])


if __name__ == '__main__':
    unittest.main()
