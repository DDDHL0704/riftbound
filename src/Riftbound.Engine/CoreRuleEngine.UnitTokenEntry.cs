using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static void AppendUnitTokenBatch(MatchState state, StackItemState item, string factoryCardNo,
        int count, bool entersReady, Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, List<GameEvent> events,
        Dictionary<string, ObjectLocationState>? locations = null, IReadOnlyList<string>? turnEffects = null,
        string? battlefieldId = null, string? abilityId = null, IReadOnlyList<string>? grantedKeywords = null,
        bool allowLocationChoice = true)
    {
        var context = state with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(locations ?? state.ObjectLocations, zones),
            UntilEndOfTurnEffects = turnEffects ?? state.UntilEndOfTurnEffects };
        var batch = CreateUnitTokenBatch(context, item, factoryCardNo, count, entersReady, battlefieldId,
            abilityId, grantedKeywords, allowLocationChoice);
        foreach (var pair in batch.PlayerZones) zones[pair.Key] = pair.Value;
        foreach (var pair in batch.CardObjects) cards[pair.Key] = pair.Value;
        foreach (var pair in batch.RunePools) pools[pair.Key] = pair.Value;
        if (locations is not null && batch.ObjectLocations is { } resolvedLocations)
            foreach (var pair in resolvedLocations) locations[pair.Key] = pair.Value;
        events.AddRange(batch.Events);
    }

    private static StackResolutionResult CreateUnitTokenBatch(MatchState state, StackItemState item,
        string factoryCardNo, int count, bool entersReady, string? battlefieldId = null, string? abilityId = null,
        IReadOnlyList<string>? grantedKeywords = null, bool allowLocationChoice = true)
    {
        if (count <= 0 || battlefieldId is not null && BattlefieldLocalRules.PreventsUnitPlay(state, "BATTLEFIELD:" + battlefieldId))
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (!P6TokenFactoryCatalog.TryGetByCardNo(factoryCardNo, out var factory))
            throw new InvalidOperationException($"Missing official unit token factory: {factoryCardNo}");
        P6TokenFactoryCatalog.TryGetByCardNo("SFD·T01", out var robot);
        var order = UnitTokenReplacementOrder(state, item);
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var legacy = item.TokenEntryPlan is { } plan
            && (count == plan.OriginalCount || count == plan.OriginalCount + plan.Applied.Count) ? plan : null;
        var sequence = 0;
        for (var i = 0; i < (legacy?.OriginalCount ?? count); i++)
        {
            var entryField = battlefieldId;
            if (allowLocationChoice && entryField is null && UnitTokenInstructionAllowsLocationChoice(item.CardNo, factory.TokenFamilyName))
                entryField = ChooseUnitTokenBattlefield(state, item.ControllerId, factory.TokenFamilyName,
                    zones, cards, locations, pools, state.UntilEndOfTurnEffects);
            var copies = 1 + (legacy?.Applied.Count(application => application.TokenIndex == i) ?? 0);
            for (var copy = 0; copy < copies; copy++)
            foreach (var useRobot in ChooseUnitTokenReplacementForms(state, item, order, i, zones, cards, locations, pools, events))
            {
                var selectedFactory = useRobot ? robot : factory;
                var id = NextTokenObjectId(zones, cards, visibleSource == "HIDDEN" ? item.ControllerId + "-PUBLIC" : visibleSource, ++sequence);
                var token = selectedFactory.CreateObject(id, item.ControllerId, item.ControllerId, isExhausted: !entersReady);
                // CN375: ready/exhausted entry and explicit keyword grants belong
                // to the instruction and survive changing the token's factory.
                token = token with {
                    Tags = token.Tags.Concat(grantedKeywords ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    GrantedTags = grantedKeywords is { Count: > 0 }
                        ? (token.GrantedTags ?? []).Concat(grantedKeywords).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                        : token.GrantedTags
                };
                token = token with { Tags = ApplyAzirSandSoldierTemperedTags(zones, cards, item.ControllerId, token.Tags) };
                var hastePaid = ChooseTokenHaste(state, item.ControllerId, item.SourceObjectId, token, zones, cards, pools, events);
                if (hastePaid) token = token with { IsExhausted = false };
                token = ApplyUnitTokenEntryStaticAbility(zones, cards, item.ControllerId, id, token,
                    out var ready, out var staticSourceId, out var staticSource, out var staticAbility, entryField);
                cards[id] = token;
                var destination = entryField is null ? "BASE" : "BATTLEFIELD";
                zones[item.ControllerId] = entryField is null
                    ? zones[item.ControllerId] with { Base = zones[item.ControllerId].Base.Append(id).ToArray() }
                    : zones[item.ControllerId] with { Battlefields = zones[item.ControllerId].Battlefields.Append(id).ToArray() };
                locations[id] = new(item.ControllerId, destination, entryField);
                var payload = new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = visibleSource,
                    ["abilityId"] = abilityId, ["tokenObjectId"] = id, ["tokenCardNo"] = token.CardNo,
                    ["tokenName"] = selectedFactory.TokenFamilyName, ["power"] = token.Power, ["destinationZone"] = destination, ["battlefieldId"] = entryField, ["battlefieldObjectId"] = entryField, ["tokenFactoryCardNo"] = token.TokenFactoryCardNo,
                    ["tokenTags"] = token.Tags.ToArray(), ["isExhausted"] = token.IsExhausted, ["azirTempered"] = token.Tags.Contains(CardEquipmentKeywordNames.Tempered) };
                AddEntryStaticAbilityPayload(payload, ready ? staticAbility : null, staticSourceId, staticSource.CardNo);
                AddTokenHastePayload(payload, hastePaid);
                events.Add(CaptureUnitEntry(new("UNIT_TOKEN_CREATED", $"{(visibleSource == "HIDDEN" ? "技能" : visibleSource)}打出{selectedFactory.TokenFamilyName}", payload), zones, cards, state.UntilEndOfTurnEffects));
            }
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
