using System.Globalization;
using System.Text;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record DelayedDestructionRewardSpec(string EquipmentTokenName = "", int RuneCallCount = 0);

public sealed record DelayedDestructionContext(string InstanceId, string CardNo, string ControllerId,
    ObjectBinding Source, ObjectBinding Unit, int TurnNumber, int ExecutionIndex, long CreatedTick)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DestroyedUnitFact? Destroyed { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string DelayedDestructionEffect = "DELAYED_UNIT_DESTRUCTION_REWARD";
    internal const string DelayedDestructionPrefix = "DELAYED_UNIT_DESTRUCTION|";

    internal static string DelayedDestructionRewardLabel(DelayedDestructionContext context)
        => !CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior)
            || behavior.DelayedDestructionReward is not { } reward ? "处理延迟效果"
            : reward.RuneCallCount > 0 ? $"召出 {reward.RuneCallCount} 枚休眠符文"
            : $"打出一个休眠{reward.EquipmentTokenName}";

    private static string EncodeDelayedIdentity(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static string? DecodeDelayedIdentity(string value)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return null; }
    }

    private static string DelayedDestructionMarker(DelayedDestructionContext c) => string.Join('|',
        DelayedDestructionPrefix.TrimEnd('|'), EncodeDelayedIdentity(c.InstanceId), EncodeDelayedIdentity(c.CardNo),
        EncodeDelayedIdentity(c.ControllerId), EncodeDelayedIdentity(c.Source.ObjectId), c.Source.Generation,
        EncodeDelayedIdentity(c.Unit.ObjectId), c.Unit.Generation, c.TurnNumber, c.ExecutionIndex, c.CreatedTick);

    internal static DelayedDestructionContext? ReadDelayedDestruction(string marker)
    {
        var f = marker.Split('|');
        if (f.Length != 11 || f[0] != DelayedDestructionPrefix.TrimEnd('|')
            || DecodeDelayedIdentity(f[1]) is not { Length: > 0 } instance
            || DecodeDelayedIdentity(f[2]) is not { Length: > 0 } card
            || DecodeDelayedIdentity(f[3]) is not { Length: > 0 } controller
            || DecodeDelayedIdentity(f[4]) is not { Length: > 0 } source
            || !long.TryParse(f[5], NumberStyles.None, CultureInfo.InvariantCulture, out var sourceGeneration)
            || DecodeDelayedIdentity(f[6]) is not { Length: > 0 } unit
            || !long.TryParse(f[7], NumberStyles.None, CultureInfo.InvariantCulture, out var unitGeneration)
            || !int.TryParse(f[8], NumberStyles.None, CultureInfo.InvariantCulture, out var turn)
            || !int.TryParse(f[9], NumberStyles.None, CultureInfo.InvariantCulture, out var execution)
            || !long.TryParse(f[10], NumberStyles.None, CultureInfo.InvariantCulture, out var tick)) return null;
        var context = new DelayedDestructionContext(instance, card, controller, new(source, sourceGeneration),
            new(unit, unitGeneration), turn, execution, tick);
        return marker == DelayedDestructionMarker(context) ? context : null;
    }

    internal static bool ValidDelayedDestruction(MatchState state, DelayedDestructionContext c,
        string? source = null, string? controller = null, string? cardNo = null)
        => c.Source is { Generation: >= 0 } && c.Unit is { Generation: >= 0 }
            && c.TurnNumber == state.TurnNumber && c.CreatedTick > 0 && c.CreatedTick <= state.Tick
            && c.ExecutionIndex >= 0 && c.Source.ObjectId != c.Unit.ObjectId
            && c.InstanceId == $"STACK-{c.CreatedTick.ToString(CultureInfo.InvariantCulture)}-{c.Source.ObjectId}:DESTRUCTION:{c.ExecutionIndex.ToString(CultureInfo.InvariantCulture)}:{c.Unit.ObjectId}"
            && (source is null || source == c.Source.ObjectId) && (controller is null || controller == c.ControllerId)
            && (cardNo is null || cardNo == c.CardNo) && state.Seats.ContainsKey(c.ControllerId)
            && CardBehaviorRegistry.TryGetByCardNo(c.CardNo, out var behavior) && behavior.DelayedDestructionReward is not null
            && state.CardObjects.TryGetValue(c.Source.ObjectId, out var printedSource)
            && printedSource.ObjectGeneration >= c.Source.Generation
            && (printedSource.ObjectGeneration != c.Source.Generation || printedSource.CardNo == c.CardNo)
            && (c.Destroyed is null
                ? state.CardObjects.TryGetValue(c.Unit.ObjectId, out var unit) && unit.ObjectGeneration >= c.Unit.Generation
                : c.Destroyed.ObjectId == c.Unit.ObjectId && c.Destroyed.Generation == c.Unit.Generation
                    && state.Seats.ContainsKey(c.Destroyed.OwnerId) && state.Seats.ContainsKey(c.Destroyed.ControllerId)
                    && (state.CardObjects.TryGetValue(c.Unit.ObjectId, out var dead)
                        ? dead.ObjectGeneration > c.Unit.Generation
                        : P6TokenFactoryCatalog.TryGetByCardNo(c.Destroyed.CardNo, out _)));

    internal static bool ValidCurrentDelayedDestructions(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(e => e is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(DelayedDestructionPrefix, StringComparison.Ordinal)).ToArray();
        if (markers.Distinct(StringComparer.Ordinal).Count() != markers.Length) return false;
        return markers.All(marker => ReadDelayedDestruction(marker) is { } c && ValidDelayedDestruction(state, c)
            && state.CardObjects[c.Unit.ObjectId].ObjectGeneration == c.Unit.Generation
            && IsObjectOnField(state.PlayerZones, c.Unit.ObjectId));
    }

    private static void ScheduleDelayedDestruction(MatchState state, StackItemState item, CardObjectState unit,
        List<string> effects, List<GameEvent> events)
    {
        var tickText = item.StackItemId[6..^(item.SourceObjectId.Length + 1)];
        var createdTick = long.Parse(tickText, CultureInfo.InvariantCulture);
        var context = new DelayedDestructionContext($"{item.StackItemId}:DESTRUCTION:{item.CompletedRepeatExecutions}:{unit.ObjectId}",
            item.CardNo!, item.ControllerId, new(item.SourceObjectId, state.CardObjects[item.SourceObjectId].ObjectGeneration),
            new(unit.ObjectId, unit.ObjectGeneration), state.TurnNumber, item.CompletedRepeatExecutions, createdTick);
        var marker = DelayedDestructionMarker(context);
        effects.Add(marker);
        events.Add(new("DELAYED_DESTRUCTION_SCHEDULED", "本回合该单位被摧毁时触发延迟技能", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = unit.ObjectId, ["delayedDestruction"] = context }));
    }

    private static ResolutionResult RecordDelayedDestruction(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State; var effects = state.UntilEndOfTurnEffects.ToList();
        var contexts = before.UntilEndOfTurnEffects.Concat(effects)
            .Where(e => e.StartsWith(DelayedDestructionPrefix, StringComparison.Ordinal))
            .Select(ReadDelayedDestruction).OfType<DelayedDestructionContext>()
            .Concat(result.Events.Where(e => e.Kind == "DELAYED_DESTRUCTION_SCHEDULED")
                .Select(e => e.Payload.GetValueOrDefault("delayedDestruction")).OfType<DelayedDestructionContext>())
            .DistinctBy(c => c.InstanceId).ToArray();
        var events = result.Events.ToList(); var queue = state.TriggerQueue.ToList();
        foreach (var death in result.Events.Select(e => e.Payload.GetValueOrDefault("capturedUnitDeath"))
            .OfType<CapturedUnitDeath>().DistinctBy(d => d.Id))
        {
            foreach (var context in contexts.Where(c => c.TurnNumber == death.TurnNumber && c.TurnNumber == state.TurnNumber
                && c.Unit.ObjectId == death.Unit.ObjectId && c.Unit.Generation == death.Unit.Generation))
            {
                effects.Remove(DelayedDestructionMarker(context));
                if (state.Status != MatchStatuses.InProgress) continue;
                var trigger = new TriggerQueueItemState($"{death.Id}:{context.InstanceId}", context.ControllerId,
                    context.Source.ObjectId, DelayedDestructionEffect, TriggerTimings.UnitDestroyed) {
                    SourceCardNo = context.CardNo, DelayedDestruction = context with { Destroyed = death.Unit } };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        // A departure changes incarnation. Source departure is immaterial (CN392).
        effects.RemoveAll(marker => marker.StartsWith(DelayedDestructionPrefix, StringComparison.Ordinal)
            && (ReadDelayedDestruction(marker) is not { } c || c.TurnNumber != state.TurnNumber
                || !state.CardObjects.TryGetValue(c.Unit.ObjectId, out var unit) || unit.ObjectGeneration != c.Unit.Generation
                || !IsObjectOnField(state.PlayerZones, c.Unit.ObjectId)));
        state = state with { UntilEndOfTurnEffects = effects, TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveDelayedDestruction(MatchState state, StackItemState item)
    {
        var context = item.DelayedDestruction!;
        if (context.Destroyed is null || item.EffectKind != DelayedDestructionEffect
            || !ValidDelayedDestruction(state, context, item.SourceObjectId, item.ControllerId, item.CardNo))
            throw new InvalidOperationException("Invalid delayed destruction context.");
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId,
            item.SourceObjectId, item.EffectKind, TriggerTimings.UnitDestroyed)) };
        CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior);
        var reward = behavior.DelayedDestructionReward!;
        if (reward.EquipmentTokenName.Length > 0)
            CreateLegendEquipmentToken(zones, cards, context.ControllerId, context.Source.ObjectId, item.EffectKind,
                reward.EquipmentTokenName, [CardObjectTags.EquipmentCard], true, events, context.CardNo);
        if (reward.RuneCallCount > 0)
        {
            var called = CallRunes(zones, cards, context.ControllerId, reward.RuneCallCount);
            events.Add(new("RUNES_CALLED", "延迟技能召出休眠的符文", new Dictionary<string, object?> {
                ["sourceObjectId"] = context.Source.ObjectId, ["playerId"] = context.ControllerId,
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray() }));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }
}
