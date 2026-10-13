using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SourceReplaySpec(int GenericPowerCost);
public sealed record SourceDamageOccurrence(string StackItemId, long Tick, ObjectBinding Unit, int Amount,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CardNo = null);
public sealed record SourceReplayProgress(ObjectBinding Source, string CardNo, int TurnNumber,
    IReadOnlyList<SourceDamageOccurrence> DamageOccurrences, bool IsReplay);
public sealed record SourceReplayContinuation(SourceReplayProgress Progress, ObjectBinding Unit,
    string ControllerId, bool DecisionCompleted = false);

public sealed partial class CoreRuleEngine
{
    internal static int SourceReplayPowerCost(MatchState state, string player)
        => state.PendingEffectPlay is { PlayerId: var actor, SourceReplay: not null } p && actor == player
            && CardBehaviorRegistry.TryGetByEffectKind(p.Parent.EffectKind, out var behavior)
            ? behavior.SourceReplay?.GenericPowerCost ?? 0 : 0;

    internal static bool ValidSourceReplayProgress(MatchState state, SourceReplayProgress p, StackItemState item)
        => p.Source is { Generation: >= 0 } && p.Source.ObjectId == item.SourceObjectId && p.CardNo == item.CardNo
            && p.TurnNumber == state.TurnNumber && p.DamageOccurrences is not null
            && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var b) && b.SourceReplay is not null
            && state.CardObjects.TryGetValue(p.Source.ObjectId, out var source) && source.ObjectGeneration == p.Source.Generation
            && source.CardNo == p.CardNo && p.DamageOccurrences.All(d => d is not null && d.Amount > 0
                && d.Tick >= 0 && d.Tick <= state.Tick && d.Unit is { Generation: >= 0 }
                && !string.IsNullOrWhiteSpace(d.Unit.ObjectId) && d.Unit.ObjectId != item.SourceObjectId
                && ValidDamageOccurrenceSource(d, item.SourceObjectId)
                && (state.CardObjects.TryGetValue(d.Unit.ObjectId, out var recipient) ? recipient.ObjectGeneration >= d.Unit.Generation
                    : d.CardNo is not null && P6TokenFactoryCatalog.TryGetByCardNo(d.CardNo, out _)));

    private static bool ValidDamageOccurrenceSource(SourceDamageOccurrence d, string source)
        => !string.IsNullOrWhiteSpace(d.StackItemId) && d.StackItemId.StartsWith("STACK-", StringComparison.Ordinal) && d.StackItemId.EndsWith("-" + source, StringComparison.Ordinal)
            && d.StackItemId.Length > source.Length + 7
            && long.TryParse(d.StackItemId[6..^(source.Length + 1)], out var tick) && tick > 0 && tick <= d.Tick
            && d.StackItemId == $"STACK-{tick}-{source}";

    internal static bool ValidSourceReplayPending(MatchState state, PendingEffectPlayState pending)
    {
        if (pending.SourceReplay is not { DecisionCompleted: false } c || pending.Parent.SourceReplay is not { } progress
            || pending.Parent.SourceReplayDecisionCompleted
            || !ValidSourceReplayProgress(state, progress, pending.Parent)
            || MatchStateHasher.HashValue(c.Progress) != MatchStateHasher.HashValue(progress)
            || !state.Seats.ContainsKey(c.ControllerId) || c.ControllerId != pending.PlayerId
            || c.Unit is not { Generation: >= 0 } || !pending.Parent.TargetObjectIds.Contains(c.Unit.ObjectId)
                && !(pending.Parent.RepeatExecutions?.Any(e => e.TargetObjectIds.Contains(c.Unit.ObjectId)) ?? false)
            || pending.SourceZone != "STACK" || pending.DestinationPolicy != "STACK" || !pending.Optional
            || !pending.IgnoreBaseMana || !pending.IgnoreBasePower || pending.IgnoreAllCosts || pending.ManaReduction != 0
            || pending.ViewedCardIds is not null || pending.RevealedHand is not null
            || pending.Sources.Count != 1 || pending.Sources.GetValueOrDefault(pending.Parent.SourceObjectId, -1) != progress.Source.Generation
            || !state.ObjectLocations.TryGetValue(progress.Source.ObjectId, out var location) || location.Zone != "STACK") return false;
        var execution = pending.Parent.RepeatExecutions is { } repeats
            ? pending.Parent.CompletedRepeatExecutions > 0 && pending.Parent.CompletedRepeatExecutions <= repeats.Count
                ? repeats[pending.Parent.CompletedRepeatExecutions - 1] : null
            : null;
        if (pending.Parent.RepeatExecutions is not null && execution is null
            || pending.Parent.RepeatExecutions is null && pending.Parent.CompletedRepeatExecutions != 0) return false;
        var targets = execution?.TargetObjectIds ?? pending.Parent.TargetObjectIds;
        var generations = execution?.TargetGenerations ?? pending.Parent.TargetGenerations;
        if (!targets.Contains(c.Unit.ObjectId) || generations?.GetValueOrDefault(c.Unit.ObjectId, -1) != c.Unit.Generation) return false;
        if (state.CardObjects.TryGetValue(c.Unit.ObjectId, out var unit) && unit.ObjectGeneration == c.Unit.Generation
            && IsObjectOnField(state.PlayerZones, unit.ObjectId) && EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) != c.ControllerId) return false;
        return true;
    }

    private static PendingEffectPlayState SourceReplayChoice(MatchState state, StackItemState parent,
        SourceReplayProgress progress, ObjectBinding target, string controller)
    {
        parent = parent with { SourceReplay = progress };
        return new($"SOURCE-REPLAY:{state.Tick + 1}:{parent.StackItemId}:{parent.CompletedRepeatExecutions}", controller,
            parent, "STACK", new Dictionary<string, long> { [parent.SourceObjectId] = progress.Source.Generation },
            true, true, 0, "STACK", true) { SourceReplay = new(progress, target, controller) };
    }

    private static StackResolutionResult CompleteSourceReplayDecision(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (!state.StackItems.Any(i => i.StackItemId != item.StackItemId && i.SourceObjectId == item.SourceObjectId)
            && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior))
            CompleteSpellSource(zones, cards, item, behavior);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards };
    }

    private static StackItemState SourceReplayChild(MatchState state, StackItemState child)
    {
        if (state.PendingEffectPlay?.SourceReplay is { } c)
            return child with { SourceReplay = c.Progress with { IsReplay = true }, SourceReplayDecisionCompleted = false };
        if (!CardBehaviorRegistry.TryGetByEffectKind(child.EffectKind, out var b) || b.SourceReplay is null) return child;
        var source = state.CardObjects[child.SourceObjectId];
        var generation = source.ObjectGeneration + (state.ObjectLocations.GetValueOrDefault(source.ObjectId)?.Zone == "STACK" ? 0 : 1);
        return child with { SourceReplay = new(new(source.ObjectId, generation), child.CardNo!, state.TurnNumber, [], false) };
    }

    private static IReadOnlyList<StackItemState> PropagateSourceReplayProgress(MatchState state, StackItemState item,
        SourceReplayProgress progress)
        => state.StackItems.Select(i => i.SourceObjectId == item.SourceObjectId && i.StackItemId != item.StackItemId
            && i.SourceReplay is { } old ? i with { SourceReplay = progress with { IsReplay = old.IsReplay } } : i).ToArray();
}
