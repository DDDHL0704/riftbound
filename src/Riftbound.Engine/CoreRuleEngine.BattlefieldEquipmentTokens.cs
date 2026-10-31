using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // All effects that put an equipment token directly onto a battlefield use
    // this path.  In particular, bomb sources must not each create their own
    // token identity, location, static-entry, and observer bookkeeping.
    private static IReadOnlyDictionary<string, ObjectLocationState> CreateBattlefieldEquipmentTokenBatch(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string sourceObjectId,
        string sourceCardNo,
        string displayName,
        string tokenName,
        IReadOnlyList<string> tokenTags,
        int tokenCount,
        bool isExhausted,
        string battlefieldId,
        List<GameEvent> events,
        IReadOnlyDictionary<string, ObjectLocationState> priorLocations)
    {
        if (tokenCount <= 0) return priorLocations;

        var hasTokenDefinition = TryGetEquipmentTokenDefinition(tokenName, tokenTags, sourceCardNo,
            out var tokenDefinition);
        var resolvedTokenTags = ResolveEquipmentTokenTags(tokenName, tokenTags,
            hasTokenDefinition ? tokenDefinition : null);
        var locations = priorLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var created = new List<string>(tokenCount);
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, sourceObjectId, tokenIndex + 1);
            var tokenState = hasTokenDefinition
                ? tokenDefinition.CreateObject(tokenObjectId, controllerId, controllerId, isExhausted) with
                {
                    Tags = resolvedTokenTags
                }
                : new CardObjectState(tokenObjectId,
                    isExhausted: isExhausted,
                    tags: resolvedTokenTags,
                    ownerId: controllerId,
                    controllerId: controllerId);
            tokenState = ApplyTokenEntryStaticAbility(
                playerZones,
                cardObjects,
                controllerId,
                tokenObjectId,
                tokenState,
                out var entersReady,
                out var entrySourceId,
                out var entrySource,
                out var entryAbility,
                battlefieldId);
            cardObjects[tokenObjectId] = tokenState;
            created.Add(tokenObjectId);
            locations[tokenObjectId] = new(controllerId, "BATTLEFIELD", battlefieldId);

            var payload = new Dictionary<string, object?>
            {
                ["playerId"] = controllerId,
                ["sourceObjectId"] = sourceObjectId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenName"] = tokenName,
                ["destinationZone"] = "BATTLEFIELD",
                ["battlefieldObjectId"] = battlefieldId,
                ["isExhausted"] = tokenState.IsExhausted
            };
            if (hasTokenDefinition) payload["tokenCardNo"] = tokenDefinition.CardNo;
            if (tokenState.Tags.Count > 0) payload["tokenTags"] = tokenState.Tags.ToArray();
            AddEntryStaticAbilityPayload(payload, entersReady ? entryAbility : null, entrySourceId, entrySource.CardNo);
            events.Add(CaptureEquipmentPlayed(new GameEvent(
                "EQUIPMENT_TOKEN_CREATED",
                $"{displayName}打出装备指示物到战场",
                payload), playerZones, cardObjects, tokenState, controllerId));
        }

        if (created.Count > 0 && playerZones.TryGetValue(controllerId, out var zones))
            playerZones[controllerId] = zones with
            {
                Battlefields = zones.Battlefields.Concat(created).ToArray()
            };
        return locations;
    }
}
