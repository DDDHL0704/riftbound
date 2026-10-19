using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DamageDestructionContext(string InstanceId, string CreationStackId, string CardNo,
    string ControllerId, ObjectBinding Source, int TurnNumber, long CreatedAtTick, int ExecutionIndex,
    ObjectBinding? Unit = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? DamagedUnit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DamagedAtTick { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DamageOrdinal { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DamagedTokenFactoryCardNo { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string DamageDestructionEffect = "CAPTURED_DAMAGE_DESTRUCTION";
    private const string DamageDestructionPrefix = "DAMAGE_DESTRUCTION|";
    private static readonly JsonSerializerOptions DamageDestructionJson = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> DamageDestructionSpecs = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Select(c => (c.CardNo, Spec: TriggerParser.Parse(c.CardEffect).FirstOrDefault(t => t.Kind is
                TriggerKinds.SpellTurnUnitDamagedDestroy or TriggerKinds.SpellNextDamageOrInspiredDestroy)))
            .Where(c => c.Spec is not null).ToDictionary(c => c.CardNo, c => c.Spec!, StringComparer.Ordinal));

    private static TriggerSpec? DamageDestructionSpec(string cardNo) => DamageDestructionSpecs.Value.GetValueOrDefault(cardNo);
    private static string DamageDestructionMarker(DamageDestructionContext c)
        => DamageDestructionPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(c, DamageDestructionJson));
    internal static DamageDestructionContext? ReadDamageDestruction(string marker)
    {
        try {
            if (!marker.StartsWith(DamageDestructionPrefix, StringComparison.Ordinal)) return null;
            var c = JsonSerializer.Deserialize<DamageDestructionContext>(Convert.FromBase64String(marker[DamageDestructionPrefix.Length..]), DamageDestructionJson);
            return c is not null && DamageDestructionMarker(c) == marker ? c : null;
        }
        catch (Exception e) when (e is FormatException or JsonException) { return null; }
    }

    internal static bool ValidDamageDestruction(MatchState state, DamageDestructionContext c,
        string? sourceId = null, string? actor = null, string? cardNo = null)
        => c.Source is { Generation: >= 0 } && !string.IsNullOrEmpty(c.Source.ObjectId)
            && !string.IsNullOrEmpty(c.CreationStackId) && c.ExecutionIndex >= 0
            && c.InstanceId == $"{c.CreationStackId}:DAMAGE-DESTRUCTION:{c.ExecutionIndex}"
            && c.TurnNumber == state.TurnNumber && c.CreatedAtTick > 0 && c.CreatedAtTick <= state.Tick
            && !string.IsNullOrWhiteSpace(c.ControllerId) && !string.IsNullOrWhiteSpace(c.CardNo)
            && state.Seats.ContainsKey(c.ControllerId) && PrintedSpellSources.Value.Contains(c.CardNo)
            && (sourceId is null || sourceId == c.Source.ObjectId) && (actor is null || actor == c.ControllerId)
            && (cardNo is null || cardNo == c.CardNo)
            && DamageDestructionSpec(c.CardNo) is { } spec
            && (spec.Kind == TriggerKinds.SpellTurnUnitDamagedDestroy ? c.Unit is null
                : c.Unit is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(c.Unit.ObjectId))
            && state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) && source.ObjectGeneration >= c.Source.Generation
            && (source.ObjectGeneration != c.Source.Generation || source.CardNo == c.CardNo)
            && (c.DamagedUnit is null ? c.DamagedAtTick is null && c.DamageOrdinal is null && c.DamagedTokenFactoryCardNo is null
                : c.DamagedUnit.Generation >= 0 && !string.IsNullOrWhiteSpace(c.DamagedUnit.ObjectId) && c.DamagedAtTick >= c.CreatedAtTick
                    && c.DamagedAtTick <= state.Tick && c.DamageOrdinal >= 0
                    && (c.DamagedTokenFactoryCardNo is null || P6TokenFactoryCatalog.TryGetByCardNo(c.DamagedTokenFactoryCardNo, out _))
                    && (state.CardObjects.TryGetValue(c.DamagedUnit.ObjectId, out var damaged)
                        ? damaged.ObjectGeneration >= c.DamagedUnit.Generation && damaged.TokenFactoryCardNo == c.DamagedTokenFactoryCardNo
                            && (damaged.ObjectGeneration != c.DamagedUnit.Generation
                                || IsObjectOnField(state.PlayerZones, damaged.ObjectId) && !damaged.IsFaceDown)
                        : c.DamagedTokenFactoryCardNo is not null)
                    && (c.Unit is null || c.Unit == c.DamagedUnit));

    internal static bool ValidDamageDestructionState(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(effect => effect is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(DamageDestructionPrefix, StringComparison.Ordinal)).ToArray();
        return markers.Distinct(StringComparer.Ordinal).Count() == markers.Length
            && markers.All(m => ReadDamageDestruction(m) is { DamagedUnit: null } c && ValidDamageDestruction(state, c)
                && (c.Unit is null || state.CardObjects.TryGetValue(c.Unit.ObjectId, out var unit)
                    && unit.ObjectGeneration == c.Unit.Generation && IsObjectOnField(state.PlayerZones, unit.ObjectId)));
    }

    private static void ScheduleDamageDestruction(MatchState state, StackItemState item, ObjectBinding? unit,
        List<string> effects, List<GameEvent> events)
    {
        var source = AttributionSource(item.SourceObjectId)!;
        var c = new DamageDestructionContext($"{item.StackItemId}:DAMAGE-DESTRUCTION:{item.CompletedRepeatExecutions}",
            item.StackItemId, item.CardNo, item.ControllerId, new(source.ObjectId, source.ObjectGeneration),
            state.TurnNumber, state.Tick + 1, item.CompletedRepeatExecutions, unit);
        effects.Add(DamageDestructionMarker(c));
        events.Add(new("DAMAGE_DESTRUCTION_SCHEDULED", unit is null ? "本回合单位受到伤害后，独立触发摧毁技能" : "原单位本回合下次受伤后，独立触发摧毁技能",
            new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["damageDestruction"] = c }));
    }

    private static ResolutionResult RecordDamageDestruction(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        if (!before.UntilEndOfTurnEffects.Any(e => e.StartsWith(DamageDestructionPrefix, StringComparison.Ordinal))
            && !result.State.UntilEndOfTurnEffects.Any(e => e.StartsWith(DamageDestructionPrefix, StringComparison.Ordinal))
            && !result.Events.Any(e => e.Payload.GetValueOrDefault("damageDestruction") is DamageDestructionContext)) return result;
        var state = result.State; var effects = state.UntilEndOfTurnEffects.ToList();
        var contexts = before.UntilEndOfTurnEffects.Concat(effects).Where(e => e.StartsWith(DamageDestructionPrefix, StringComparison.Ordinal))
            .Select(ReadDamageDestruction).OfType<DamageDestructionContext>()
            .Concat(result.Events.Select(e => e.Payload.GetValueOrDefault("damageDestruction")).OfType<DamageDestructionContext>())
            .DistinctBy(c => c.InstanceId).ToArray();
        var used = new HashSet<string>(); var queue = state.TriggerQueue.ToList(); var events = new List<GameEvent>();
        for (var ordinal = 0; ordinal < result.Events.Count; ordinal++)
        {
            var ev = result.Events[ordinal]; events.Add(ev);
            if (ev.Kind != "DAMAGE_APPLIED" || ev.Payload.GetValueOrDefault("damage") is not int damage || damage <= 0
                || ev.Payload.GetValueOrDefault("targetObjectId") is not string id
                || ev.Payload.GetValueOrDefault("targetGeneration") is not long generation) continue;
            foreach (var c in contexts.Where(c => c.TurnNumber == state.TurnNumber && !used.Contains(c.InstanceId)
                && (c.Unit is null || c.Unit.ObjectId == id && c.Unit.Generation == generation)))
            {
                var captured = c with { DamagedUnit = new(id, generation), DamagedAtTick = state.Tick, DamageOrdinal = ordinal,
                    DamagedTokenFactoryCardNo = ev.Payload.GetValueOrDefault("targetTokenFactoryCardNo") as string };
                if (c.Unit is not null) { used.Add(c.InstanceId); effects.Remove(DamageDestructionMarker(c)); }
                if (state.Status != MatchStatuses.InProgress) continue;
                var trigger = new TriggerQueueItemState(DamageDestructionTriggerId(captured), c.ControllerId,
                    c.Source.ObjectId, DamageDestructionEffect, "DAMAGE_APPLIED") { SourceCardNo = c.CardNo, DamageDestruction = captured };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        effects.RemoveAll(m => m.StartsWith(DamageDestructionPrefix, StringComparison.Ordinal)
            && (ReadDamageDestruction(m) is not { } c || c.TurnNumber != state.TurnNumber || c.Unit is { } target
                && (!state.CardObjects.TryGetValue(target.ObjectId, out var unit) || unit.ObjectGeneration != target.Generation
                    || !IsObjectOnField(state.PlayerZones, target.ObjectId))));
        if (queue.Count == state.TriggerQueue.Count && effects.SequenceEqual(state.UntilEndOfTurnEffects)) return result;
        state = state with { UntilEndOfTurnEffects = effects, TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static string DamageDestructionTriggerId(DamageDestructionContext c)
        => $"damage-destroy:{c.InstanceId}:{c.DamagedAtTick}:{c.DamageOrdinal}";

    private static StackResolutionResult ResolveDamageDestruction(MatchState state, StackItemState item)
    {
        var c = item.DamageDestruction!;
        if (c.DamagedUnit is null || item.EffectKind != DamageDestructionEffect
            || !ValidDamageDestruction(state, c, item.SourceObjectId, item.ControllerId, item.CardNo))
            throw new InvalidOperationException("Invalid captured damage destruction.");
        var events = new List<GameEvent> { BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId,
            item.SourceObjectId, item.EffectKind, "DAMAGE_APPLIED")) };
        if (!state.CardObjects.TryGetValue(c.DamagedUnit.ObjectId, out var target)
            || target.ObjectGeneration != c.DamagedUnit.Generation || !IsObjectOnField(state.PlayerZones, target.ObjectId))
            return NoopStackResolutionResult(state) with { Events = events };
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), state.RunePools, objectLocations: state.ObjectLocations,
            explicitDestroyObjectIds: new HashSet<string> { target.ObjectId }, explicitDestroyReason: "DELAYED_DAMAGE_DESTRUCTION");
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
            Events = events.Concat(removal.Events).ToArray(), TriggerQueue = removal.TriggerQueue,
            DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds };
    }
}
