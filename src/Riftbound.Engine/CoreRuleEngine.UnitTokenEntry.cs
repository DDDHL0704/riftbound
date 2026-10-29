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
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (!P6TokenFactoryCatalog.TryGetByCardNo(factoryCardNo, out var factory))
            throw new InvalidOperationException($"Missing official unit token factory: {factoryCardNo}");
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        for (var i = 0; i < count; i++)
        {
            var id = NextTokenObjectId(zones, cards, visibleSource == "HIDDEN" ? item.ControllerId + "-PUBLIC" : visibleSource, i + 1);
            var token = factory.CreateObject(id, item.ControllerId, item.ControllerId, isExhausted: !entersReady);
            token = token with { Tags = ApplyAzirSandSoldierTemperedTags(zones, cards, item.ControllerId, token.Tags) };
            var hastePaid = ChooseTokenHaste(state, item.ControllerId, item.SourceObjectId, token, zones, cards, pools, events);
            if (hastePaid) token = token with { IsExhausted = false };
            token = ApplyUnitTokenEntryStaticAbility(zones, cards, item.ControllerId, id, token,
                out var ready, out var staticSourceId, out var staticSource, out var staticAbility);
            cards[id] = token;
            var destination = battlefieldId is null ? "BASE" : "BATTLEFIELD";
            zones[item.ControllerId] = battlefieldId is null
                ? zones[item.ControllerId] with { Base = zones[item.ControllerId].Base.Append(id).ToArray() }
                : zones[item.ControllerId] with { Battlefields = zones[item.ControllerId].Battlefields.Append(id).ToArray() };
            locations[id] = new(item.ControllerId, destination, battlefieldId);
            var payload = new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = visibleSource,
                ["abilityId"] = abilityId, ["tokenObjectId"] = id, ["tokenCardNo"] = token.CardNo,
                ["tokenName"] = factory.TokenFamilyName, ["power"] = token.Power, ["destinationZone"] = destination, ["battlefieldId"] = battlefieldId, ["tokenFactoryCardNo"] = token.TokenFactoryCardNo,
                ["tokenTags"] = token.Tags.ToArray(), ["isExhausted"] = token.IsExhausted, ["azirTempered"] = token.Tags.Contains(CardEquipmentKeywordNames.Tempered) };
            AddEntryStaticAbilityPayload(payload, ready ? staticAbility : null, staticSourceId, staticSource.CardNo);
            AddTokenHastePayload(payload, hastePaid);
            events.Add(CaptureUnitEntry(new("UNIT_TOKEN_CREATED", $"{(visibleSource == "HIDDEN" ? "技能" : visibleSource)}打出{factory.TokenFamilyName}", payload), zones, cards, state.UntilEndOfTurnEffects));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
