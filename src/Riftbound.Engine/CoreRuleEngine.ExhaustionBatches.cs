using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool IsOptionalUnitEventDraw(UnitEventContext context)
        => SelectedUnitEventSpec(context) is { Optional: true, Effect: "DRAW", LeadingSourceExhaust: false,
            LeadingPowerCost: 0, LeadingManaCost: 0, LeadingSelfBanish: false, LeadingDestroyOtherFriendlyPermanents: 0 };

    private static string UnitEventOnceMarker(string source, UnitEventContext context)
        => $"UNIT_EVENT_ONCE:{source}:{context.SourceGeneration}:{context.CardNo}:"
            + (context.Origin is { } origin
                ? $"{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}" : "PRINTED");

    private static bool UnitEventOnceUsed(MatchState state, string source, UnitEventContext context)
        => UnitEventSpec(context.CardNo)?.OncePerTurn == true
            && state.UntilEndOfTurnEffects.Contains(UnitEventOnceMarker(source, context), StringComparer.Ordinal);

    // One call represents one simultaneous instruction, never a whole command.
    // Capture its active units and observers before any individual transition;
    // later damage, ready instructions or departures cannot erase this event.
    private static void ApplyExhaustBatch(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        string player, IEnumerable<string> targets, CardBehaviorDefinition behavior,
        StackItemState item, List<GameEvent> events)
    {
        var ids = targets.Distinct(StringComparer.Ordinal).ToArray();
        CaptureExhaustionBatch(state, zones, cards, player, ids);
        foreach (var id in ids)
        {
            if (!cards.TryGetValue(id, out var unit)) continue;
            cards[id] = ApplyExhaustState(zones, cards, unit, behavior, item, id, out var exhausted);
            events.Add(exhausted);
        }
    }

    private static void CaptureExhaustionBatch(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, IEnumerable<string> targets)
    {
        var ids = targets.Distinct(StringComparer.Ordinal).ToArray();
        var changed = ids.Where(id => cards.TryGetValue(id, out var unit) && !unit.IsExhausted
                && IsFaceUpNonStandbyUnit(unit) && IsObjectOnField(zones, id)
                && EffectiveFieldControllerId(zones, id, unit) == player)
            .Select(id => cards[id]).Select(unit => new UnitEventOtherObject(
                new(unit.ObjectId, unit.ObjectGeneration), unit.CardNo ?? unit.TokenFactoryCardNo!,
                unit.CopyFaceTransitions?.Count ?? 0, unit.TokenFactoryCardNo)).ToArray();
        if (changed.Length > 0 && RuleChoices.Value is { } frame)
        {
            var sources = cards.Values.Where(c => EffectiveFieldControllerId(zones, c.ObjectId, c) == player)
                .Select(c => c.ObjectId);
            frame.ExhaustedUnitEvents.AddRange(CaptureUnitEvents(zones, cards, sources, "FRIENDLY_UNITS_EXHAUSTED_TOGETHER")
                .Where(o => UnitEventSpec(o.Context.CardNo) is { MinimumExhaustedUnits: > 0 } spec
                    && changed.Length >= spec.MinimumExhaustedUnits && !UnitEventOnceUsed(state, o.Source, o.Context))
                .Select(o => o with { Context = o.Context with { ExhaustedUnits = changed } }));
        }
    }

    private static bool ValidExhaustionBatchContext(MatchState state, UnitEventContext context, UnitEventAbilitySpec spec)
        => spec.MinimumExhaustedUnits == 0 ? context.ExhaustedUnits is null
            : context.Timing == "FRIENDLY_UNITS_EXHAUSTED_TOGETHER"
                && context.ExhaustedUnits is { } units && units.Count >= spec.MinimumExhaustedUnits
                && units.Select(u => u?.Binding.ObjectId).Distinct(StringComparer.Ordinal).Count() == units.Count
                && units.All(u => u is { Binding.Generation: >= 0, FaceRevision: >= 0 }
                    && TryPredictEnteredFace(u.CardNo, out var face) && face.Tags.Contains(CardObjectTags.UnitCard)
                    && PredictBindingMatches(state, u.Binding, u.CardNo, u.FaceRevision, context.CapturedTick, u.TokenFactoryCardNo));
}
