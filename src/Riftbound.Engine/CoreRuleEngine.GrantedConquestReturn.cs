using System.Globalization;
using System.Text;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string GrantedConquestReturnEffect = "UNIT_GRANTED_CONQUEST_RETURN_BASE";
    internal const string GrantedConquestReturnPrefix = "GRANTED_CONQUEST_RETURN_BASE|";

    private static string EncodeGrantIdentity(string id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id));
    private static string? DecodeGrantIdentity(string encoded)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
        catch (FormatException) { return null; }
    }

    private static void GrantConquestReturn(MatchState state, IDictionary<string, CardObjectState> cards,
        StackItemState item, string unitId, List<GameEvent> events)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            || !behavior.GrantsConquestReturnToBaseAfterMovement || !cards.TryGetValue(unitId, out var unit)) return;
        var sourceGeneration = state.CardObjects[item.SourceObjectId].ObjectGeneration;
        var grant = string.Join('|', GrantedConquestReturnPrefix.TrimEnd('|'),
            state.TurnNumber.ToString(CultureInfo.InvariantCulture), EncodeGrantIdentity(item.SourceObjectId),
            sourceGeneration.ToString(CultureInfo.InvariantCulture), EncodeGrantIdentity(item.CardNo!),
            EncodeGrantIdentity(item.StackItemId), unit.ObjectGeneration.ToString(CultureInfo.InvariantCulture),
            item.CompletedRepeatExecutions.ToString(CultureInfo.InvariantCulture), EncodeGrantIdentity(unitId));
        cards[unitId] = unit with { UntilEndOfTurnEffects = [..unit.UntilEndOfTurnEffects, grant] };
        events.Add(new("TEMPORARY_CONQUEST_ABILITY_GRANTED", "单位本回合获得可选征服回基地技能", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = unitId,
            ["effectId"] = grant, ["duration"] = "UNTIL_END_OF_TURN", ["executionIndex"] = item.CompletedRepeatExecutions }));
    }

    private static bool ValidConquestGrant(string grant, long generation, int? currentTurn = null, string? unitId = null)
    {
        if (!grant.StartsWith(GrantedConquestReturnPrefix, StringComparison.Ordinal)) return false;
        var fields = grant.Split('|');
        return fields.Length == 9 && int.TryParse(fields[1], out var turn) && turn > 0
            && (currentTurn is null || currentTurn == turn)
            && DecodeGrantIdentity(fields[2]) is { Length: > 0 }
            && long.TryParse(fields[3], out var sourceGeneration) && sourceGeneration >= 0
            && DecodeGrantIdentity(fields[4]) is { } cardNo && CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior)
            && behavior.GrantsConquestReturnToBaseAfterMovement
            && DecodeGrantIdentity(fields[5]) is { Length: > 0 } stackId
            && DecodeGrantIdentity(fields[2]) is { } sourceId && stackId.StartsWith("STACK-", StringComparison.Ordinal)
            && stackId.Length > sourceId.Length + 7
            && stackId.EndsWith("-" + sourceId, StringComparison.Ordinal)
            && long.TryParse(stackId[6..^(sourceId.Length + 1)], out var sourceTick) && sourceTick > 0
            && stackId == "STACK-" + sourceTick.ToString(CultureInfo.InvariantCulture) + "-" + sourceId
            && long.TryParse(fields[6], out var unitGeneration) && unitGeneration == generation
            && int.TryParse(fields[7], out var execution) && execution >= 0
            && DecodeGrantIdentity(fields[8]) is { Length: > 0 } unitIdentity && (unitId is null || unitId == unitIdentity);
    }

    internal static bool ValidCurrentConquestGrants(MatchState state)
    {
        foreach (var unit in state.CardObjects.Values)
        {
            var grants = unit.UntilEndOfTurnEffects.Where(e => e.StartsWith(GrantedConquestReturnPrefix, StringComparison.Ordinal)).ToArray();
            if (grants.Length != grants.Distinct(StringComparer.Ordinal).Count()) return false;
            foreach (var grant in grants)
            {
                if (!IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(state.PlayerZones, unit.ObjectId)
                    || !ValidConquestGrant(grant, unit.ObjectGeneration, state.TurnNumber, unit.ObjectId)) return false;
                var fields = grant.Split('|'); var sourceId = DecodeGrantIdentity(fields[2])!;
                var sourceGeneration = long.Parse(fields[3], CultureInfo.InvariantCulture);
                var sourceStackId = DecodeGrantIdentity(fields[5])!;
                if (long.Parse(sourceStackId[6..^(sourceId.Length + 1)], CultureInfo.InvariantCulture) > state.Tick) return false;
                if (!state.CardObjects.TryGetValue(sourceId, out var source) || sourceGeneration > source.ObjectGeneration
                    || sourceGeneration == source.ObjectGeneration && source.CardNo != DecodeGrantIdentity(fields[4])) return false;
            }
        }
        return true;
    }

    internal static bool ValidCapturedConquestGrant(MatchState state, UnitConquestContext context, string unitId)
    {
        if (context.TemporaryGrant is null) return true;
        if (!ValidConquestGrant(context.TemporaryGrant, context.SourceGeneration, state.TurnNumber, unitId)) return false;
        if (state.CardObjects.TryGetValue(unitId, out var unit) && unit.ObjectGeneration < context.SourceGeneration) return false;
        var fields = context.TemporaryGrant.Split('|');
        var sourceId = DecodeGrantIdentity(fields[2])!;
        var sourceGeneration = long.Parse(fields[3], CultureInfo.InvariantCulture);
        var sourceStackId = DecodeGrantIdentity(fields[5])!;
        if (long.Parse(sourceStackId[6..^(sourceId.Length + 1)], CultureInfo.InvariantCulture) > state.Tick) return false;
        return state.CardObjects.TryGetValue(sourceId, out var source) && sourceGeneration <= source.ObjectGeneration
            && (sourceGeneration < source.ObjectGeneration || source.CardNo == DecodeGrantIdentity(fields[4]));
    }

    private static IEnumerable<TriggerQueueItemState> CaptureGrantedConquestReturns(MatchState state, string player,
        string field, IReadOnlyList<string> unitIds, long tick)
    {
        foreach (var id in unitIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var unit) || !IsFaceUpNonStandbyUnit(unit)
                || EffectiveFieldControllerId(state.PlayerZones, id, unit) != player
                || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != field) continue;
            var grants = unit.UntilEndOfTurnEffects.Where(e => e.StartsWith(GrantedConquestReturnPrefix, StringComparison.Ordinal)).ToArray();
            var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects, state.ObjectLocations,
                player, field, "BATTLEFIELD_CONQUERED");
            for (var i = 0; i < grants.Length; i++)
                if (ValidConquestGrant(grants[i], unit.ObjectGeneration, state.TurnNumber, unit.ObjectId))
                    for (var repeat = 0; repeat < repeats; repeat++)
                        yield return new TriggerQueueItemState($"granted-conquest-return-{tick}-{field}-{id}-{i}-{repeat}", player, id,
                            GrantedConquestReturnEffect, "BATTLEFIELD_CONQUERED") {
                            UnitConquest = new(unit.CardNo!, unit.ObjectGeneration, field, null, grants[i]) };
        }
    }

    private static StackResolutionResult ResolveGrantedConquestReturn(MatchState state, StackItemState item)
    {
        var result = NoopStackResolutionResult(state);
        if (!ValidConquestDamageStack(state, item) || item.TargetObjectIds.SingleOrDefault() != item.SourceObjectId
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var unit)
            || unit.ObjectGeneration != item.UnitConquest!.SourceGeneration || !IsFaceUpNonStandbyUnit(unit)
            || !IsObjectOnField(state.PlayerZones, unit.ObjectId)) return result;
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones).ToDictionary(p => p.Key, p => p.Value);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, [unit.ObjectId],
            "BASE:" + EffectiveFieldControllerId(zones, unit.ObjectId, unit), events);
        return result with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools, Events = events };
    }
}
