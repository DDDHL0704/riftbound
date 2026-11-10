using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record StartPhaseUnitDeathOrigin(int TurnNumber, string TurnPlayerId, string OccurrenceHash);
internal sealed record StartPhaseDeathUnitEvent(string Source, string Controller, UnitEventContext Context);

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StartPhaseUnitDeathOrigin? StartPhaseUnitDeath { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string FirstFriendlyStartPhaseDeathTiming = "FIRST_FRIENDLY_START_PHASE_DEATH";

    private static IReadOnlyList<StartPhaseDeathUnitEvent> CaptureStartPhaseDeathUnitEvents(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        DestroyedUnitFact unit, string deathId)
    {
        if (state.Phase != MatchPhases.TurnStart || unit.ControllerId != state.TurnPlayerId) return [];
        var origin = new StartPhaseUnitDeathOrigin(state.TurnNumber, state.TurnPlayerId,
            MatchStateHasher.HashValue(new { deathId, state.TurnNumber, state.TurnPlayerId }));
        // Only surviving observers see another object's departure (CN383.2.c).
        var sources = cards.Values.Where(card => card.ObjectId != unit.ObjectId
            && !RuleChoices.Value!.CurrentDestructions.Contains(card.ObjectId)
            && EffectivePublicStaticAuraSourceControllerId(zones, card.ObjectId, card) == state.TurnPlayerId)
            .Select(card => card.ObjectId);
        return CaptureUnitEvents(zones, cards, sources, FirstFriendlyStartPhaseDeathTiming)
            .Select(observation => new StartPhaseDeathUnitEvent(observation.Source, observation.Controller,
                observation.Context with { StartPhaseUnitDeath = origin })).ToArray();
    }

    private static void QueueFirstStartPhaseDeathUnitEvents(MatchState before, MatchState state, TurnDeathLedger ledger,
        IReadOnlyList<CapturedUnitDeath> deaths, List<TriggerQueueItemState> queue, List<GameEvent> events)
    {
        if (state.Status != MatchStatuses.InProgress) return;
        var seen = (ledger.TurnNumber == state.TurnNumber ? ledger.StartPhaseUnitControllers ?? [] : [])
            .ToHashSet(StringComparer.Ordinal);
        var frame = RuleChoices.Value!;
        foreach (var batch in deaths.GroupBy(death => death.Batch))
            foreach (var player in batch.Select(death => death.StartPhaseTurnPlayerId).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                // This is the player's first qualifying death, even if the
                // observer was absent; a later new copy cannot reset it.
                if (!seen.Add(player)) continue;
                var observations = batch.Where(death => death.StartPhaseTurnPlayerId == player)
                    .SelectMany(death => death.StartPhaseUnitEvents.Select(observation => (Death: death, Observation: observation)))
                    .GroupBy(pair => MatchStateHasher.HashValue(new { pair.Observation.Source, pair.Observation.Controller,
                        pair.Observation.Context.CardNo, pair.Observation.Context.SourceGeneration, pair.Observation.Context.Origin }));
                foreach (var group in observations)
                {
                    var candidates = group.ToArray(); var first = candidates[0];
                    var options = candidates.Select(pair => new RuleChoiceOption(pair.Death.Id,
                        "选择本次友方单位被摧毁作为首次触发。", [pair.Death.Unit.ObjectId])).ToArray();
                    var selected = options.Length == 1 ? options[0].Id : frame.Choose(first.Observation.Controller,
                        "选择同时发生的首次开始阶段摧毁。", options, kind: "FIRST_DEATH_OCCURRENCE", preview: state);
                    var earned = candidates.Single(pair => pair.Death.Id == selected).Observation;
                    var timing = before.SpellDuelState.IsActive ? TimingStates.SpellDuelOpen
                        : before.StackItems.LastOrDefault()?.TimingContext;
                    var trigger = UnitEventTrigger(new(earned.Source, earned.Controller, earned.Context),
                        state.Tick, frame.UnitEventOrdinal++, "UNIT_DESTROYED", timing);
                    queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
                }
            }
    }

    private static bool ValidStartPhaseDeathUnitEvent(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec, string? controller)
        => spec.Timing != FirstFriendlyStartPhaseDeathTiming ? context.StartPhaseUnitDeath is null
            : spec.Effect == EachPlayerDestroyUnitEffect && spec.RequiredUnitDestructionScope == OpponentsDestructionScope
                && context.StartPhaseUnitDeath is { } origin && origin.TurnNumber == state.TurnNumber
                && state.Seats.ContainsKey(origin.TurnPlayerId) && origin.TurnPlayerId == state.TurnPlayerId
                && (controller is null || origin.TurnPlayerId == controller)
                && origin.OccurrenceHash.Length == 64 && origin.OccurrenceHash.All(Uri.IsHexDigit);
}
