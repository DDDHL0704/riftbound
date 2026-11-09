using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresNoConquestThisTurn { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SourceConqueredBeforeEnd { get; init; }
}

public sealed partial record TurnBattlefieldConquests
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ObjectBinding>? Units { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string DetachOriginDamageSourceEffect = "DETACH_ORIGIN_EQUIPMENT_DAMAGE_SOURCE";

    private static bool UnitConqueredDuringThisTurn(MatchState state, string source)
        => state.BattlefieldConquests is { } history && history.TurnNumber == state.TurnNumber
            && state.CardObjects.TryGetValue(source, out var unit)
            && (history.Units ?? []).Contains(new ObjectBinding(source, unit.ObjectGeneration));

    // CN383.4.c/g: record participants at the real action. The existing turn
    // conquest ledger later retains these immutable bindings through departures.
    private static void RecordNaturalUnitConquestBindings(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        string player, string battlefield, IReadOnlyList<string> units, List<GameEvent> events)
    {
        var index = events.FindLastIndex(e => e.Kind == "BATTLEFIELD_CONQUERED"
            && EventPayloadString(e, "playerId") == player
            && EventPayloadString(e, "battlefieldId") == battlefield);
        if (index < 0) return;
        var bindings = units.Distinct(StringComparer.Ordinal).Where(id => cards.TryGetValue(id, out var unit)
                && IsFaceUpNonStandbyUnit(unit) && IsObjectOnField(zones, id))
            .Select(id => new ObjectBinding(id, cards[id].ObjectGeneration)).ToArray();
        events[index] = events[index] with { Payload = new Dictionary<string, object?>(events[index].Payload) {
            ["unitConquests"] = bindings, ["conquestTurnNumber"] = state.TurnNumber } };
    }

    private static IEnumerable<TriggerQueueItemState> CaptureConditionalEndUnitEvents(MatchState state)
    {
        var sources = state.CardObjects.Values.Where(card =>
            EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) == state.TurnPlayerId)
            .Select(card => card.ObjectId);
        var ordinal = 0;
        foreach (var observation in CaptureUnitEvents(state.PlayerZones, state.CardObjects, sources, "END"))
        {
            var spec = UnitEventSpec(observation.Context.CardNo)!;
            if (spec.RequiresNoConquestThisTurn
                && UnitConqueredDuringThisTurn(state, observation.Source)) continue;
            var captured = spec.RequiresNoConquestThisTurn ? observation with {
                Context = observation.Context with { SourceConqueredBeforeEnd = false } } : observation;
            yield return UnitEventTrigger(captured, state.Tick + 1, ordinal++, "TURN_END_STEP_STARTED", TimingStates.NeutralClosed);
        }
    }

    private static bool ValidEndConquestCondition(UnitEventContext context, UnitEventAbilitySpec spec)
        => spec.RequiresNoConquestThisTurn
            ? spec.Timing == "END" && context.SourceConqueredBeforeEnd == false
            : context.SourceConqueredBeforeEnd is null;

    private static StackResolutionResult ResolveDetachOriginDamageSource(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var events = new List<GameEvent>();
        var destroyed = new HashSet<string>(StringComparer.Ordinal); var context = item.UnitEvent!;
        if (context.Origin?.Equipment is { } binding
            && cards.TryGetValue(binding.ObjectId, out var equipment)
            && equipment.ObjectGeneration == binding.Generation && !equipment.IsFaceDown
            && IsObjectOnField(zones, equipment.ObjectId) && equipment.AttachedToObjectId is { } attached)
            DetachArmamentForInstruction(state, item, zones, cards, events, attached, equipment.ObjectId);
        var current = state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones) };
        if (CombatMightSourceLocation(current, item.SourceObjectId, context.SourceGeneration) is not null)
        {
            var damage = ApplyDamageToCardObject(current, cards, item.ControllerId, item.SourceObjectId,
                ApplySpellSkillDamageBonuses(current, item.ControllerId, item.SourceObjectId, spec.Amount, item),
                destroyed, current.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId),
                PreventSpellAndSkillDamageThisTurnEffectId);
            events.Add(new("DAMAGE_APPLIED", "卸除指定武装后对原宿主造成伤害",
                BuildDamagePayload(item.SourceObjectId, item.SourceObjectId, damage)));
        }
        var cleanup = RunStateBasedCleanupLoop(current, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            RunePools = cleanup.RunePools, TriggerQueue = cleanup.TriggerQueue,
            DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds, Events = events,
            ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}

internal static class NaturalUnitConquestHistory
{
    internal static IReadOnlyList<ObjectBinding>? ReadBindings(IReadOnlyDictionary<string, object?> payload)
    {
        if (!payload.TryGetValue("unitConquests", out var raw)) return [];
        try
        {
            var bindings = raw is IReadOnlyList<ObjectBinding> typed ? typed
                : JsonSerializer.SerializeToElement(raw).Deserialize<ObjectBinding[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (bindings is null || bindings.Any(b => b is null || string.IsNullOrWhiteSpace(b.ObjectId) || b.Generation < 0)
                || bindings.Select(b => b.ObjectId).Distinct(StringComparer.Ordinal).Count() != bindings.Count) return null;
            return bindings;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException) { return null; }
    }
}
