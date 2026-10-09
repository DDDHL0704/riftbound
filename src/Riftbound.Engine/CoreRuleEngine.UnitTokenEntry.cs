using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult CreateUnitTokenBatch(MatchState state, StackItemState item,
        string factoryCardNo, int count, bool entersReady, string? battlefieldId = null, string? abilityId = null)
    {
        if (battlefieldId is not null && BattlefieldLocalRules.PreventsUnitPlay(state, "BATTLEFIELD:" + battlefieldId))
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (!P6TokenFactoryCatalog.TryGetByCardNo(factoryCardNo, out var factory))
            throw new InvalidOperationException($"Missing official unit token factory: {factoryCardNo}");
        for (var i = 0; i < count; i++)
        {
            var id = NextTokenObjectId(zones, cards, item.SourceObjectId, i + 1);
            var token = factory.CreateObject(id, item.ControllerId, item.ControllerId, isExhausted: !entersReady);
            token = token with { Tags = ApplyAzirSandSoldierTemperedTags(zones, cards, item.ControllerId, token.Tags) };
            token = ApplyUnitTokenEntryStaticAbility(zones, cards, item.ControllerId, id, token,
                out var ready, out var staticSourceId, out var staticSource, out var staticAbility);
            cards[id] = token;
            var destination = battlefieldId is null ? "BASE" : "BATTLEFIELD";
            zones[item.ControllerId] = battlefieldId is null
                ? zones[item.ControllerId] with { Base = zones[item.ControllerId].Base.Append(id).ToArray() }
                : zones[item.ControllerId] with { Battlefields = zones[item.ControllerId].Battlefields.Append(id).ToArray() };
            locations[id] = new(item.ControllerId, destination, battlefieldId);
            var payload = new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["abilityId"] = abilityId, ["tokenObjectId"] = id, ["tokenCardNo"] = token.CardNo,
                ["tokenName"] = factory.TokenFamilyName, ["power"] = token.Power, ["destinationZone"] = destination, ["battlefieldId"] = battlefieldId, ["tokenFactoryCardNo"] = token.TokenFactoryCardNo,
                ["tokenTags"] = token.Tags.ToArray(), ["isExhausted"] = token.IsExhausted, ["azirTempered"] = token.Tags.Contains(CardEquipmentKeywordNames.Tempered) };
            AddEntryStaticAbilityPayload(payload, ready ? staticAbility : null, staticSourceId, staticSource.CardNo);
            events.Add(CaptureUnitEntry(new("UNIT_TOKEN_CREATED", $"{item.SourceObjectId}打出{factory.TokenFamilyName}", payload), zones, cards));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
    }
}
