using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static bool IsKnownContinuousEffectLayer(string value)
    {
        return string.Equals(value, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
            || string.Equals(value, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            || string.Equals(value, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal);
    }

    private static bool IsKnownContinuousEffectDuration(string value)
    {
        return string.Equals(value, "UNTIL_END_OF_TURN", StringComparison.Ordinal)
            || string.Equals(value, "UNTIL_END_OF_BATTLE", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_ARMAMENT_ATTACHED", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_SOURCE_ON_PUBLIC_FIELD", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_SOURCE_ATTACKING_OR_DEFENDING_ALONE", StringComparison.Ordinal)
            || string.Equals(value, "WHILE_SOURCE_ATTACKING_READY_ENEMY_UNIT_BATTLEFIELD", StringComparison.Ordinal);
    }

    private static bool IsKnownContinuousEffectLayerEngineStatus(string value)
    {
        return string.Equals(value, "FOUNDATION_ONLY", StringComparison.Ordinal);
    }

    private static bool IsKnownStandbySlotState(string value)
    {
        return string.Equals(value, "VISIBLE", StringComparison.Ordinal)
            || string.Equals(value, "HIDDEN", StringComparison.Ordinal);
    }

    private static bool IsKnownMatchPhase(string value)
    {
        return string.Equals(value, MatchPhases.Room, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.Mulligan, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.TurnStart, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.Main, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.TurnEnd, StringComparison.Ordinal);
    }

    private static bool IsKnownTimingState(string value)
    {
        return string.Equals(value, TimingStates.Room, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.Mulligan, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.NeutralClosed, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.SpellDuelClosed, StringComparison.Ordinal);
    }

    private static bool IsKnownMatchStatus(string value)
    {
        return string.Equals(value, MatchStatuses.Empty, StringComparison.Ordinal)
            || string.Equals(value, MatchStatuses.Seating, StringComparison.Ordinal)
            || string.Equals(value, MatchStatuses.InProgress, StringComparison.Ordinal)
            || string.Equals(value, MatchStatuses.Finished, StringComparison.Ordinal);
    }

    private static bool IsKnownBattlefieldStatus(string value)
    {
        return string.Equals(value, "CONTROLLED", StringComparison.Ordinal)
            || string.Equals(value, "UNCONTROLLED", StringComparison.Ordinal)
            || string.Equals(value, "CONTESTED", StringComparison.Ordinal);
    }

    private static bool IsKnownBattlefieldTaskKind(string value)
    {
        return string.Equals(value, "START_SPELL_DUEL", StringComparison.Ordinal)
            || string.Equals(value, "START_BATTLE", StringComparison.Ordinal);
    }

    private static bool IsKnownBattlefieldTaskStatus(string value)
    {
        return string.Equals(value, "PENDING", StringComparison.Ordinal)
            || string.Equals(value, "ACTIVE", StringComparison.Ordinal)
            || string.Equals(value, "COMPLETED", StringComparison.Ordinal)
            || string.Equals(value, "WAITING_FOR_SPELL_DUEL", StringComparison.Ordinal);
    }

    private static bool IsKnownBattlefieldTaskReason(string value)
    {
        return string.Equals(value, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            || string.Equals(value, "SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", StringComparison.Ordinal);
    }

    private static bool IsKnownSeat(string value)
    {
        return string.Equals(value, "P1", StringComparison.Ordinal)
            || string.Equals(value, "P2", StringComparison.Ordinal);
    }

    private static IReadOnlySet<string> BuildNormalizedPlayerIdSet(IEnumerable<string> playerIds)
    {
        return playerIds
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Select(playerId => playerId.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> BuildKnownStackItemIds(IEnumerable<string?> stackItemIds)
    {
        return stackItemIds
            .Where(stackItemId => !string.IsNullOrWhiteSpace(stackItemId))
            .Select(stackItemId => stackItemId!.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlySet<string> BuildSnapshotKnownObjectIds(SnapshotDto snapshot)
    {
        var knownObjectIds = new HashSet<string>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return knownObjectIds;
        }

        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
                || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
            {
                continue;
            }

            foreach (var (objectId, objectPayload) in objectPayloads)
            {
                AddKnownObjectId(knownObjectIds, objectId);
            }
        }

        return knownObjectIds;
    }

    private static IReadOnlySet<string>? BuildSnapshotBattlefieldStateObjectIds(SnapshotDto snapshot)
    {
        if (snapshot.Lanes is null
            || !TryReadObjectList(snapshot.Lanes, "battlefields", out var battlefieldPayloads))
        {
            return null;
        }

        var battlefieldObjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var battlefieldPayload in battlefieldPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(battlefieldPayload)
                || !TryReadObjectString(battlefieldPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId))
            {
                continue;
            }

            battlefieldObjectIds.Add(battlefieldObjectId.Trim());
        }

        return battlefieldObjectIds;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>>? BuildSnapshotBattlefieldStateOccupantObjectIds(SnapshotDto snapshot)
    {
        if (snapshot.Lanes is null
            || !TryReadObjectList(snapshot.Lanes, "battlefields", out var battlefieldPayloads))
        {
            return null;
        }

        var occupantObjectIdsByBattlefield = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var battlefieldPayload in battlefieldPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(battlefieldPayload)
                || !TryReadObjectString(battlefieldPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId)
                || !TryReadObjectStringList(battlefieldPayload, "occupantObjectIds", out var occupantObjectIds))
            {
                continue;
            }

            occupantObjectIdsByBattlefield[battlefieldObjectId.Trim()] = occupantObjectIds
                .Where(occupantObjectId => !string.IsNullOrWhiteSpace(occupantObjectId))
                .Select(occupantObjectId => occupantObjectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return occupantObjectIdsByBattlefield;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>>? BuildSnapshotRequiredBattlefieldTaskKindsByBattlefield(
        SnapshotDto snapshot)
    {
        if (snapshot.Lanes is null
            || !TryReadObjectList(snapshot.Lanes, "battlefields", out var battlefieldPayloads))
        {
            return null;
        }

        var taskKindsByBattlefield = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var battlefieldPayload in battlefieldPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(battlefieldPayload)
                || !TryReadObjectString(battlefieldPayload, "battlefieldObjectId", out var battlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldObjectId)
                || !TryReadObjectBool(battlefieldPayload, "contested", out var contested)
                || !contested)
            {
                continue;
            }

            var normalizedBattlefieldObjectId = battlefieldObjectId.Trim();
            if (!taskKindsByBattlefield.TryGetValue(normalizedBattlefieldObjectId, out var taskKinds))
            {
                taskKinds = new HashSet<string>(StringComparer.Ordinal);
                taskKindsByBattlefield[normalizedBattlefieldObjectId] = taskKinds;
            }

            taskKinds.Add("START_SPELL_DUEL");
            // Taking an undefended enemy-controlled battlefield still opens a
            // spell duel, but there is no battle task with only one occupying side.
            // Missing participant evidence remains subject to the strict check.
            var singleOccupyingSide = TryReadObjectStringList(battlefieldPayload, "occupantControllerIds", out var occupants)
                && occupants.Count > 0
                && occupants.All(id => !string.IsNullOrWhiteSpace(id))
                && occupants.Select(id => id.Trim()).Distinct(StringComparer.Ordinal).Count() == 1;
            if (!singleOccupyingSide) taskKinds.Add("START_BATTLE");
        }

        return taskKindsByBattlefield.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlySet<string>)entry.Value,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> BuildSnapshotObjectLocationIndex(SnapshotDto snapshot)
    {
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return objectLocations;
        }

        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
                || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
            {
                continue;
            }

            foreach (var (objectId, objectPayload) in objectPayloads)
            {
                if (string.IsNullOrWhiteSpace(objectId)
                    || !IsSnapshotPlayerPayloadObject(objectPayload)
                    || !TryReadObjectValue(objectPayload, "location", out var locationPayload)
                    || !IsSnapshotPlayerPayloadObject(locationPayload)
                    || !TryReadObjectString(locationPayload, "playerId", out var playerId)
                    || string.IsNullOrWhiteSpace(playerId)
                    || !TryReadObjectString(locationPayload, "zone", out var zone)
                    || string.IsNullOrWhiteSpace(zone)
                    || !TryReadObjectOptionalString(locationPayload, "battlefieldObjectId", out var battlefieldObjectId))
                {
                    continue;
                }

                objectLocations[objectId.Trim()] = new ObjectLocationState(
                    playerId.Trim(),
                    zone.Trim(),
                    string.IsNullOrWhiteSpace(battlefieldObjectId)
                        ? null
                        : battlefieldObjectId.Trim());
            }
        }

        return objectLocations;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSnapshotPlayerBattlefieldObjectIdsByPlayer(SnapshotDto snapshot)
    {
        var playerBattlefieldObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return playerBattlefieldObjectIdsByPlayer;
        }

        foreach (var (playerId, playerPayload) in snapshot.Players)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || !IsSnapshotPlayerPayloadObject(playerPayload)
                || !TryReadObjectValue(playerPayload, "zones", out var zonesPayload)
                || !IsSnapshotPlayerPayloadObject(zonesPayload)
                || !TryReadObjectStringList(zonesPayload, "battlefields", out var battlefieldObjectIds))
            {
                continue;
            }

            playerBattlefieldObjectIdsByPlayer[playerId.Trim()] = battlefieldObjectIds
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerBattlefieldObjectIdsByPlayer;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSnapshotPlayerBaseObjectIdsByPlayer(SnapshotDto snapshot)
    {
        var playerBaseObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return playerBaseObjectIdsByPlayer;
        }

        foreach (var (playerId, playerPayload) in snapshot.Players)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || !IsSnapshotPlayerPayloadObject(playerPayload)
                || !TryReadObjectValue(playerPayload, "zones", out var zonesPayload)
                || !IsSnapshotPlayerPayloadObject(zonesPayload)
                || !TryReadObjectStringList(zonesPayload, "base", out var baseObjectIds))
            {
                continue;
            }

            playerBaseObjectIdsByPlayer[playerId.Trim()] = baseObjectIds
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerBaseObjectIdsByPlayer;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSnapshotPlayerGraveyardObjectIdsByPlayer(SnapshotDto snapshot)
    {
        var playerGraveyardObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return playerGraveyardObjectIdsByPlayer;
        }

        foreach (var (playerId, playerPayload) in snapshot.Players)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || !IsSnapshotPlayerPayloadObject(playerPayload)
                || !TryReadObjectValue(playerPayload, "zones", out var zonesPayload)
                || !IsSnapshotPlayerPayloadObject(zonesPayload)
                || !TryReadObjectStringList(zonesPayload, "graveyard", out var graveyardObjectIds))
            {
                continue;
            }

            playerGraveyardObjectIdsByPlayer[playerId.Trim()] = graveyardObjectIds
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerGraveyardObjectIdsByPlayer;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSnapshotPlayerFieldObjectIdsByPlayer(SnapshotDto snapshot)
    {
        var playerFieldObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return playerFieldObjectIdsByPlayer;
        }

        foreach (var (playerId, playerPayload) in snapshot.Players)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || !IsSnapshotPlayerPayloadObject(playerPayload)
                || !TryReadObjectValue(playerPayload, "zones", out var zonesPayload)
                || !IsSnapshotPlayerPayloadObject(zonesPayload)
                || !TryReadObjectStringList(zonesPayload, "base", out var baseObjectIds)
                || !TryReadObjectStringList(zonesPayload, "battlefields", out var battlefieldObjectIds))
            {
                continue;
            }

            playerFieldObjectIdsByPlayer[playerId.Trim()] = baseObjectIds
                .Concat(battlefieldObjectIds)
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerFieldObjectIdsByPlayer;
    }

    private static IReadOnlyDictionary<string, string> BuildSnapshotObjectControllerIndex(SnapshotDto snapshot)
    {
        var objectControllers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return objectControllers;
        }

        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
                || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
            {
                continue;
            }

            foreach (var (objectId, objectPayload) in objectPayloads)
            {
                if (string.IsNullOrWhiteSpace(objectId)
                    || !IsSnapshotPlayerPayloadObject(objectPayload))
                {
                    continue;
                }

                var controllerId = ReadSnapshotObjectEffectiveControllerId(objectPayload);
                if (!string.IsNullOrWhiteSpace(controllerId))
                {
                    objectControllers[objectId.Trim()] = controllerId.Trim();
                }
            }
        }

        return objectControllers;
    }

    private static IReadOnlyDictionary<string, string?> BuildSnapshotObjectCardNoIndex(SnapshotDto snapshot)
    {
        var objectCardNos = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return objectCardNos;
        }

        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
                || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
            {
                continue;
            }

            foreach (var (objectId, objectPayload) in objectPayloads)
            {
                if (string.IsNullOrWhiteSpace(objectId)
                    || !IsSnapshotPlayerPayloadObject(objectPayload)
                    || !TryReadObjectValue(objectPayload, "cardNo", out var rawCardNo)
                    || IsNullSnapshotPayloadValue(rawCardNo)
                    || !TryReadStringValue(rawCardNo, out var cardNo)
                    || string.IsNullOrWhiteSpace(cardNo))
                {
                    continue;
                }

                objectCardNos[objectId.Trim()] = cardNo.Trim();
            }
        }

        return objectCardNos;
    }

    private static IReadOnlyDictionary<string, int> BuildSnapshotObjectPowerIndex(SnapshotDto snapshot)
    {
        var objectPowers = new Dictionary<string, int>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return objectPowers;
        }

        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
                || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
            {
                continue;
            }

            foreach (var (objectId, objectPayload) in objectPayloads)
            {
                if (string.IsNullOrWhiteSpace(objectId)
                    || !IsSnapshotPlayerPayloadObject(objectPayload)
                    || !TryReadObjectInt(objectPayload, "power", out var power))
                {
                    continue;
                }

                objectPowers[objectId.Trim()] = power;
            }
        }

        return objectPowers;
    }

    private static IReadOnlyDictionary<string, bool> BuildSnapshotObjectFaceDownIndex(SnapshotDto snapshot)
    {
        var objectFaceDowns = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return objectFaceDowns;
        }

        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
                || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
            {
                continue;
            }

            foreach (var (objectId, objectPayload) in objectPayloads)
            {
                if (string.IsNullOrWhiteSpace(objectId)
                    || !IsSnapshotPlayerPayloadObject(objectPayload)
                    || !TryReadObjectBool(objectPayload, "isFaceDown", out var isFaceDown))
                {
                    continue;
                }

                objectFaceDowns[objectId.Trim()] = isFaceDown;
            }
        }

        return objectFaceDowns;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSnapshotObjectTagIndex(SnapshotDto snapshot)
    {
        var objectTags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return objectTags;
        }

        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
                || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
            {
                continue;
            }

            foreach (var (objectId, objectPayload) in objectPayloads)
            {
                if (string.IsNullOrWhiteSpace(objectId)
                    || !IsSnapshotPlayerPayloadObject(objectPayload)
                    || !TryReadObjectStringList(objectPayload, "tags", out var tags))
                {
                    continue;
                }

                objectTags[objectId.Trim()] = tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .ToHashSet(StringComparer.Ordinal);
            }
        }

        return objectTags;
    }

    private static string? ReadSnapshotObjectEffectiveControllerId(object? objectPayload)
    {
        if (TryReadObjectOptionalString(objectPayload, "controllerId", out var controllerId)
            && !string.IsNullOrWhiteSpace(controllerId))
        {
            return controllerId;
        }

        if (TryReadObjectOptionalString(objectPayload, "ownerId", out var ownerId)
            && !string.IsNullOrWhiteSpace(ownerId))
        {
            return ownerId;
        }

        if (TryReadObjectValue(objectPayload, "location", out var locationPayload)
            && IsSnapshotPlayerPayloadObject(locationPayload)
            && TryReadObjectString(locationPayload, "playerId", out var locationPlayerId)
            && !string.IsNullOrWhiteSpace(locationPlayerId))
        {
            return locationPlayerId;
        }

        return null;
    }

    private static void ValidateSnapshotTimingPlayerMembership(
        RecoveredPlayerView view,
        string key,
        string label,
        List<string> errors,
        bool optional = false)
    {
        if (view.Snapshot.Timing is null
            || !view.Snapshot.Timing.TryGetValue(key, out var rawPlayerId)
            || rawPlayerId is null
            || rawPlayerId is JsonElement { ValueKind: JsonValueKind.Null })
        {
            return;
        }

        if (!TryReadString(view.Snapshot.Timing, key, out var playerId))
        {
            if (optional)
            {
                errors.Add($"snapshot for {view.PlayerId} timing {label} id is invalid");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            if (optional)
            {
                errors.Add($"snapshot for {view.PlayerId} timing {label} id is required");
            }

            return;
        }

        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} has surrounding whitespace");
        }

        if (!view.Snapshot.Players.ContainsKey(normalizedPlayerId))
        {
            var expectedPlayerIds = view.Snapshot.Players.Keys
                .OrderBy(knownPlayerId => knownPlayerId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} is missing from players; {FormatExpectedActualForRecovery(expectedPlayerIds, normalizedPlayerId)}");
        }
    }

    private static void ValidateSnapshotTimingPlayerListMembership(
        RecoveredPlayerView view,
        string key,
        string label,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !view.Snapshot.Timing.TryGetValue(key, out var rawPlayerIds)
            || rawPlayerIds is null
            || rawPlayerIds is JsonElement { ValueKind: JsonValueKind.Null })
        {
            return;
        }

        if (!TryReadStringList(view.Snapshot.Timing, key, out var playerIds))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} list is invalid");
            return;
        }

        var seenPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var playerId in playerIds)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add($"snapshot for {view.PlayerId} timing {label} id is required");
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} has surrounding whitespace");
            }

            if (!seenPlayerIds.Add(normalizedPlayerId))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} is duplicated");
                continue;
            }

            if (!view.Snapshot.Players.ContainsKey(normalizedPlayerId))
            {
                var expectedPlayerIds = view.Snapshot.Players.Keys
                    .OrderBy(knownPlayerId => knownPlayerId, StringComparer.Ordinal)
                    .ToArray();
                errors.Add(
                    $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} is missing from players; {FormatExpectedActualForRecovery(expectedPlayerIds, normalizedPlayerId)}");
            }
        }
    }

    private static void ValidateSnapshotPlayerPayloads(
        RecoveredPlayerView view,
        List<string> errors)
    {
        ValidateSnapshotPayloadObjectPropertyNames(
            view.Snapshot.Players,
            $"snapshot for {view.PlayerId} players",
            errors);

        var seenSeats = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (snapshotPlayerId, playerPayload) in view.Snapshot.Players)
        {
            if (!IsSnapshotPlayerPayloadObject(playerPayload))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} payload is required");
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                playerPayload,
                $"snapshot for {view.PlayerId} player {snapshotPlayerId}",
                errors);
            ValidateSnapshotPlayerRunePoolPayloadShape(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerRunePoolPayloadPropertyNames(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerRunePoolPowerTraitPayloadPropertyNames(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerRunePoolPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerZonesPayloadShape(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerZonesPayloadPropertyNames(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerZonesPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectsPayloadShape(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectsPayloadPropertyNames(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectPayloadShapes(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectPayloadPropertyNames(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectStringScalarPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectNumericScalarPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectBooleanScalarPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectListScalarPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectLocationPayloadShapes(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectLocationPayloadPropertyNames(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);
            ValidateSnapshotPlayerObjectLocationPayloadValues(
                view,
                snapshotPlayerId,
                playerPayload,
                errors);

            if (!TryReadObjectString(playerPayload, "id", out var payloadId)
                || string.IsNullOrWhiteSpace(payloadId))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} id is required");
            }
            else if (!string.Equals(payloadId, snapshotPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} player {snapshotPlayerId} payload id {payloadId} does not match player key");
            }

            if (!TryReadObjectString(playerPayload, "seat", out var seat)
                || string.IsNullOrWhiteSpace(seat))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} seat is required");
                continue;
            }

            var normalizedSeat = seat.Trim();
            if (!string.Equals(seat, normalizedSeat, StringComparison.Ordinal))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} player {snapshotPlayerId} seat {normalizedSeat} has surrounding whitespace");
            }

            if (!IsKnownSeat(normalizedSeat))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} seat {normalizedSeat} is invalid");
                continue;
            }

            if (!seenSeats.Add(normalizedSeat))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} player {snapshotPlayerId} seat {normalizedSeat} is duplicated");
            }
        }
    }

    private static void ValidateSnapshotPlayerRunePoolPayloadShape(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "runePool", out var runePoolPayload)
            || IsNullSnapshotPayloadValue(runePoolPayload))
        {
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(runePoolPayload))
        {
            errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} rune pool payload is required");
        }
    }

    private static void ValidateSnapshotPlayerRunePoolPayloadPropertyNames(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "runePool", out var runePoolPayload)
            || !IsSnapshotPlayerPayloadObject(runePoolPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            runePoolPayload,
            $"snapshot for {view.PlayerId} player {snapshotPlayerId} rune pool",
            errors);
    }

    private static void ValidateSnapshotPlayerRunePoolPowerTraitPayloadPropertyNames(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "runePool", out var runePoolPayload)
            || !IsSnapshotPlayerPayloadObject(runePoolPayload)
            || !TryReadObjectValue(runePoolPayload, "powerByTrait", out var powerByTraitPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            powerByTraitPayload,
            $"snapshot for {view.PlayerId} player {snapshotPlayerId} rune pool power by trait",
            errors);
    }

    private static void ValidateSnapshotPlayerRunePoolPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "runePool", out var runePoolPayload)
            || !IsSnapshotPlayerPayloadObject(runePoolPayload))
        {
            return;
        }

        var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} rune pool";
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(runePoolPayload, "mana", payloadLabel, "mana", errors);
        if (TryReadObjectValue(runePoolPayload, "spellOnlyMana", out _))
            ValidateSnapshotPayloadRequiredNonNegativeIntValue(runePoolPayload, "spellOnlyMana", payloadLabel, "spell-only mana", errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(runePoolPayload, "power", payloadLabel, "power", errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            runePoolPayload,
            "untypedPower",
            payloadLabel,
            "untyped power",
            errors);
        ValidateSnapshotPayloadRequiredIntMapPayloadShape(
            runePoolPayload,
            "powerByTrait",
            payloadLabel,
            "power trait",
            errors);
        ValidateSnapshotPayloadRequiredPositiveIntMapValues(
            runePoolPayload,
            "powerByTrait",
            payloadLabel,
            "power trait",
            errors);
    }

    private static void ValidateSnapshotPlayerZonesPayloadShape(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "zones", out var zonesPayload)
            || IsNullSnapshotPayloadValue(zonesPayload))
        {
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(zonesPayload))
        {
            errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} zones payload is required");
        }
    }

    private static void ValidateSnapshotPlayerZonesPayloadPropertyNames(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "zones", out var zonesPayload)
            || !IsSnapshotPlayerPayloadObject(zonesPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            zonesPayload,
            $"snapshot for {view.PlayerId} player {snapshotPlayerId} zones",
            errors);
    }

    private static void ValidateSnapshotPlayerZonesPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "zones", out var zonesPayload)
            || !IsSnapshotPlayerPayloadObject(zonesPayload))
        {
            return;
        }

        var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} zones";
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonesPayload,
            "mainDeckCount",
            payloadLabel,
            "main deck count",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonesPayload,
            "runeDeckCount",
            payloadLabel,
            "rune deck count",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            zonesPayload,
            "hand",
            payloadLabel,
            "hand object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonesPayload,
            "hand",
            payloadLabel,
            "hand object",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonesPayload,
            "handHidden",
            payloadLabel,
            "hidden hand count",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            zonesPayload,
            "base",
            payloadLabel,
            "base object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonesPayload,
            "base",
            payloadLabel,
            "base object",
            errors);
        ValidateSnapshotPayloadOptionalStringListPayloadShape(
            zonesPayload,
            "baseCards",
            payloadLabel,
            "base card object",
            errors);
        ValidateSnapshotPayloadOptionalStringListValues(
            zonesPayload,
            "baseCards",
            payloadLabel,
            "base card object",
            errors);
        ValidateSnapshotPayloadOptionalStringListPayloadShape(
            zonesPayload,
            "baseRunes",
            payloadLabel,
            "base rune object",
            errors);
        ValidateSnapshotPayloadOptionalStringListValues(
            zonesPayload,
            "baseRunes",
            payloadLabel,
            "base rune object",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            zonesPayload,
            "battlefields",
            payloadLabel,
            "battlefield object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonesPayload,
            "battlefields",
            payloadLabel,
            "battlefield object",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonesPayload,
            "battlefieldHiddenStandbyCount",
            payloadLabel,
            "hidden battlefield standby count",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            zonesPayload,
            "graveyard",
            payloadLabel,
            "graveyard object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonesPayload,
            "graveyard",
            payloadLabel,
            "graveyard object",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            zonesPayload,
            "banished",
            payloadLabel,
            "banished object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonesPayload,
            "banished",
            payloadLabel,
            "banished object",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            zonesPayload,
            "legendZone",
            payloadLabel,
            "legend zone object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonesPayload,
            "legendZone",
            payloadLabel,
            "legend zone object",
            errors);
        ValidateSnapshotPayloadRequiredStringListPayloadShape(
            zonesPayload,
            "championZone",
            payloadLabel,
            "champion zone object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonesPayload,
            "championZone",
            payloadLabel,
            "champion zone object",
            errors);
    }

    private static void ValidateSnapshotPlayerObjectsPayloadShape(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || IsNullSnapshotPayloadValue(objectsPayload))
        {
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(objectsPayload))
        {
            errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} objects payload is required");
        }
    }

    private static void ValidateSnapshotPlayerObjectsPayloadPropertyNames(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !IsSnapshotPlayerPayloadObject(objectsPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            objectsPayload,
            $"snapshot for {view.PlayerId} player {snapshotPlayerId} objects",
            errors);
    }

    private static void ValidateSnapshotPlayerObjectPayloadShapes(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId} payload is required");
            }
        }
    }

    private static void ValidateSnapshotPlayerObjectPayloadPropertyNames(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                objectPayload,
                $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId}",
                errors);
        }
    }

    private static void ValidateSnapshotPlayerObjectPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId}";
            var payloadObjectId = ValidateSnapshotPayloadRequiredStringValue(
                objectPayload,
                "objectId",
                payloadLabel,
                "object id",
                errors);
            if (payloadObjectId is not null
                && !string.Equals(payloadObjectId, objectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId} payload object id {payloadObjectId} does not match object key");
            }

            ValidateSnapshotPayloadRequiredBoolValue(
                objectPayload,
                "isFaceDown",
                payloadLabel,
                "face-down flag",
                errors);
        }
    }

    private static void ValidateSnapshotPlayerObjectStringScalarPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId}";
            ValidateSnapshotPayloadOptionalStringValue(
                objectPayload,
                "cardNo",
                payloadLabel,
                "card number",
                errors);
            ValidateSnapshotPayloadOptionalStringValue(
                objectPayload,
                "ownerId",
                payloadLabel,
                "owner id",
                errors);
            ValidateSnapshotPayloadOptionalStringValue(
                objectPayload,
                "controllerId",
                payloadLabel,
                "controller id",
                errors);
            ValidateSnapshotPayloadOptionalStringValue(
                objectPayload,
                "attachedToObjectId",
                payloadLabel,
                "attached object id",
                errors);
        }
    }

    private static void ValidateSnapshotPlayerObjectNumericScalarPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId}";
            ValidateSnapshotPayloadOptionalNonNegativeIntValue(
                objectPayload,
                "damage",
                payloadLabel,
                "damage",
                errors);
            ValidateSnapshotPayloadOptionalIntValue(
                objectPayload,
                "power",
                payloadLabel,
                "power",
                errors);
            ValidateSnapshotPayloadOptionalIntValue(
                objectPayload,
                "basePower",
                payloadLabel,
                "base power",
                errors);
            ValidateSnapshotPayloadOptionalIntValue(
                objectPayload,
                "effectivePower",
                payloadLabel,
                "effective power",
                errors);
            ValidateSnapshotPayloadOptionalIntValue(
                objectPayload,
                "untilEndOfTurnPowerModifier",
                payloadLabel,
                "until-end-of-turn power modifier",
                errors);
            ValidateSnapshotPayloadOptionalNonNegativeIntValue(
                objectPayload,
                "manaCost",
                payloadLabel,
                "mana cost",
                errors);
        }
    }

    private static void ValidateSnapshotPlayerObjectBooleanScalarPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId}";
            ValidateSnapshotPayloadOptionalBoolValue(
                objectPayload,
                "isExhausted",
                payloadLabel,
                "exhausted state",
                errors);
            ValidateSnapshotPayloadOptionalBoolValue(
                objectPayload,
                "isAttacking",
                payloadLabel,
                "attacking state",
                errors);
            ValidateSnapshotPayloadOptionalBoolValue(
                objectPayload,
                "isDefending",
                payloadLabel,
                "defending state",
                errors);
        }
    }

    private static void ValidateSnapshotPlayerObjectListScalarPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId}";
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                objectPayload,
                "tags",
                payloadLabel,
                "tag",
                errors);
            ValidateSnapshotPayloadStringListValues(
                objectPayload,
                "tags",
                payloadLabel,
                "tag",
                errors);
            ValidateSnapshotPayloadRequiredStringListPayloadShape(
                objectPayload,
                "untilEndOfTurnEffects",
                payloadLabel,
                "until-end-of-turn effect",
                errors);
            ValidateSnapshotPayloadStringListValues(
                objectPayload,
                "untilEndOfTurnEffects",
                payloadLabel,
                "until-end-of-turn effect",
                errors);
        }
    }

    private static void ValidateSnapshotPlayerObjectLocationPayloadShapes(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload)
                || !TryReadObjectValue(objectPayload, "location", out var locationPayload)
                || IsNullSnapshotPayloadValue(locationPayload))
            {
                continue;
            }

            if (!IsSnapshotPlayerPayloadObject(locationPayload))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId} location payload is required");
            }
        }
    }

    private static void ValidateSnapshotPlayerObjectLocationPayloadPropertyNames(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload)
                || !TryReadObjectValue(objectPayload, "location", out var locationPayload)
                || !IsSnapshotPlayerPayloadObject(locationPayload))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                locationPayload,
                $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId} location",
                errors);
        }
    }

    private static void ValidateSnapshotPlayerObjectLocationPayloadValues(
        RecoveredPlayerView view,
        string snapshotPlayerId,
        object? playerPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || !TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            return;
        }

        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(objectPayload)
                || !TryReadObjectValue(objectPayload, "location", out var locationPayload)
                || !IsSnapshotPlayerPayloadObject(locationPayload))
            {
                continue;
            }

            var payloadLabel = $"snapshot for {view.PlayerId} player {snapshotPlayerId} object {objectId} location";
            ValidateSnapshotPayloadRequiredStringValue(
                locationPayload,
                "playerId",
                payloadLabel,
                "player id",
                errors);
            ValidateSnapshotPayloadRequiredStringValue(
                locationPayload,
                "zone",
                payloadLabel,
                "zone",
                errors,
                IsKnownObjectLocationZone);
            ValidateSnapshotPayloadOptionalStringValue(
                locationPayload,
                "zoneKind",
                payloadLabel,
                "zone kind",
                errors,
                ObjectLocationSnapshotZones.IsKnownZoneKind,
                rejectEmptyString: true);
            ValidateSnapshotPayloadOptionalStringValue(
                locationPayload,
                "zoneLabel",
                payloadLabel,
                "zone label",
                errors,
                rejectEmptyString: true);
            ValidateSnapshotPayloadOptionalStringValue(
                locationPayload,
                "battlefieldObjectId",
                payloadLabel,
                "battlefield object id",
                errors);
        }
    }

    private static bool IsSnapshotPlayerPayloadObject(object? playerPayload)
    {
        return playerPayload switch
        {
            IReadOnlyDictionary<string, object?> => true,
            IDictionary<string, object?> => true,
            JsonElement { ValueKind: JsonValueKind.Object } => true,
            _ => false
        };
    }

    private static bool IsSnapshotIntMapPayloadObject(object? payload)
    {
        return payload switch
        {
            IReadOnlyDictionary<string, object?> => true,
            IDictionary<string, object?> => true,
            IReadOnlyDictionary<string, int> => true,
            IEnumerable<KeyValuePair<string, int>> => true,
            JsonElement { ValueKind: JsonValueKind.Object } => true,
            _ => false
        };
    }

    private static bool IsSnapshotStringMapPayloadObject(object? payload)
    {
        return payload switch
        {
            IReadOnlyDictionary<string, string> => true,
            IEnumerable<KeyValuePair<string, string>> => true,
            IReadOnlyDictionary<string, object?> => true,
            IDictionary<string, object?> => true,
            JsonElement { ValueKind: JsonValueKind.Object } => true,
            _ => false
        };
    }

    private static bool IsSnapshotStringListMapPayloadObject(object? payload)
    {
        return payload switch
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> => true,
            IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> => true,
            IReadOnlyDictionary<string, object?> => true,
            IDictionary<string, object?> => true,
            JsonElement { ValueKind: JsonValueKind.Object } => true,
            _ => false
        };
    }

    private static void ValidateSnapshotPayloadObjectPropertyNames(
        object? payload,
        string payloadLabel,
        List<string> errors)
    {
        var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var propertyName in EnumerateSnapshotPayloadObjectPropertyNames(payload))
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                errors.Add($"{payloadLabel} property name is required");
                continue;
            }

            var normalizedName = propertyName.Trim();
            if (!string.Equals(propertyName, normalizedName, StringComparison.Ordinal))
            {
                errors.Add($"{payloadLabel} property {normalizedName} has surrounding whitespace");
            }

            if (!seenPropertyNames.Add(normalizedName))
            {
                errors.Add($"{payloadLabel} property {normalizedName} appears more than once");
            }
        }
    }

    private static IReadOnlyList<string>? ValidateSnapshotPayloadStringListValues(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors,
        Func<string, bool>? isKnownValue = null)
    {
        if (!TryReadObjectValue(payload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload))
        {
            return null;
        }

        if (!TryReadStringListValue(listPayload, out var values))
        {
            errors.Add($"{payloadLabel} {itemLabel} list is invalid");
            return null;
        }

        return ValidateSnapshotStringListValues(values, payloadLabel, itemLabel, errors, isKnownValue);
    }

    private static IReadOnlyList<string>? ValidateSnapshotPayloadRequiredStringListValues(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors,
        Func<string, bool>? isKnownValue = null,
        bool allowRepeatedHiddenTargets = false,
        bool allowRepeatedValues = false)
    {
        if (!TryReadObjectValue(payload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} list is required");
            return null;
        }

        if (!TryReadStringListValue(listPayload, out var values))
        {
            errors.Add($"{payloadLabel} {itemLabel} list is invalid");
            return null;
        }

        return ValidateSnapshotStringListValues(values, payloadLabel, itemLabel, errors, isKnownValue, allowRepeatedHiddenTargets, allowRepeatedValues);
    }

    private static IReadOnlyList<string>? ValidateSnapshotPayloadOptionalStringListValues(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors,
        Func<string, bool>? isKnownValue = null)
    {
        return ValidateSnapshotPayloadStringListValues(payload, key, payloadLabel, itemLabel, errors, isKnownValue);
    }

    private static void ValidateSnapshotPayloadOptionalStringListPayloadShape(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        ValidateSnapshotPayloadRequiredStringListPayloadShape(payload, key, payloadLabel, itemLabel, errors);
    }

    private static void ValidateSnapshotPayloadRequiredStringListPayloadShape(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload))
        {
            return;
        }

        if (!TryReadStringListValue(listPayload, out _))
        {
            errors.Add($"{payloadLabel} {itemLabel} list payload is required");
        }
    }

    private static void ValidateSnapshotPayloadPresentStringListNonEmpty(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload)
            || !TryReadStringListValue(listPayload, out var values)
            || values.Count > 0)
        {
            return;
        }

        errors.Add($"{payloadLabel} {itemLabel} list must not be empty");
    }

    private static void ValidateSnapshotPayloadRequiredObjectListPayloadShape(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload))
        {
            return;
        }

        if (!TryReadObjectListValue(listPayload, out _))
        {
            errors.Add($"{payloadLabel} {itemLabel} list payload is required");
        }
    }

    private static void ValidateSnapshotPayloadObjectListItemPayloadShapes(
        object? payload,
        string key,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload)
            || !TryReadObjectListValue(listPayload, out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (!IsSnapshotPlayerPayloadObject(items[index]))
            {
                errors.Add($"{payloadLabel} item {index + 1} payload is required");
            }
        }
    }

    private static void ValidateSnapshotPayloadObjectListItemPayloadPropertyNames(
        object? payload,
        string key,
        string payloadLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var listPayload)
            || IsNullSnapshotPayloadValue(listPayload)
            || !TryReadObjectListValue(listPayload, out var items))
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (!IsSnapshotPlayerPayloadObject(items[index]))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                items[index],
                $"{payloadLabel} item {index + 1}",
                errors);
        }
    }

    private static void ValidateSnapshotPayloadRequiredStringDictionaryValues(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is required");
            return;
        }

        if (!IsSnapshotStringMapPayloadObject(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is invalid");
            return;
        }

        foreach (var (mapKey, rawValue) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            if (string.IsNullOrWhiteSpace(mapKey))
            {
                continue;
            }

            var normalizedKey = mapKey.Trim();
            if (!TryReadStringValue(rawValue, out var value))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} value is invalid");
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} value is required");
                continue;
            }

            var normalizedValue = value.Trim();
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} value {normalizedValue} has surrounding whitespace");
            }
        }
    }

    private static void ValidateSnapshotPayloadRequiredStringListDictionaryValues(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        string listItemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is required");
            return;
        }

        if (!IsSnapshotStringListMapPayloadObject(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is invalid");
            return;
        }

        foreach (var (mapKey, rawValue) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            if (string.IsNullOrWhiteSpace(mapKey))
            {
                continue;
            }

            var normalizedKey = mapKey.Trim();
            if (!TryReadStringListValue(rawValue, out var values))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} list is invalid");
                continue;
            }

            ValidateSnapshotStringListValues(
                values,
                $"{payloadLabel} {itemLabel} {normalizedKey}",
                listItemLabel,
                errors);
        }
    }

    private static IReadOnlyList<string> ValidateSnapshotStringListValues(
        IEnumerable<string> values,
        string payloadLabel,
        string itemLabel,
        List<string> errors,
        Func<string, bool>? isKnownValue = null,
        bool allowRepeatedHiddenTargets = false,
        bool allowRepeatedValues = false)
    {
        var seenValues = new HashSet<string>(StringComparer.Ordinal);
        var normalizedValues = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{payloadLabel} {itemLabel} is required");
                continue;
            }

            var normalizedValue = value.Trim();
            normalizedValues.Add(normalizedValue);
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedValue} has surrounding whitespace");
            }

            if (isKnownValue is not null && !isKnownValue(normalizedValue))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedValue} is invalid");
            }

            if (!seenValues.Add(normalizedValue) && !allowRepeatedValues && !(allowRepeatedHiddenTargets && normalizedValue == "HIDDEN"))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedValue} is duplicated");
            }
        }

        return normalizedValues;
    }

    private static string? ReadNormalizedPayloadString(object? payload, string key)
    {
        return TryReadObjectString(payload, key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static string? ValidateSnapshotPayloadRequiredStringValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors,
        Func<string, bool>? isKnownValue = null,
        IReadOnlyList<string>? knownValues = null)
    {
        if (!TryReadObjectString(payload, key, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is required");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"{payloadLabel} {itemLabel} {normalizedValue} has surrounding whitespace");
        }

        if (isKnownValue is not null && !isKnownValue(normalizedValue))
        {
            var detail = knownValues is null
                ? string.Empty
                : $"; {FormatExpectedActualForRecovery(knownValues, normalizedValue)}";
            errors.Add($"{payloadLabel} {itemLabel} {normalizedValue} is invalid{detail}");
        }

        return normalizedValue;
    }

    private static int? ValidateSnapshotPayloadRequiredNonNegativeIntValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            errors.Add($"{payloadLabel} {itemLabel} is required");
            return null;
        }

        if (!TryReadIntValue(rawValue, out var value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
            return null;
        }

        if (value < 0)
        {
            errors.Add($"{payloadLabel} {itemLabel} {value} cannot be negative");
        }

        return value;
    }

    private static int? ValidateSnapshotPayloadRequiredIntValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            errors.Add($"{payloadLabel} {itemLabel} is required");
            return null;
        }

        if (!TryReadIntValue(rawValue, out var value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
            return null;
        }

        return value;
    }

    private static int? ValidateSnapshotPayloadRequiredPositiveIntValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        var value = ValidateSnapshotPayloadRequiredIntValue(
            payload,
            key,
            payloadLabel,
            itemLabel,
            errors);
        if (value is null)
        {
            return null;
        }

        if (value <= 0)
        {
            errors.Add($"{payloadLabel} {itemLabel} {value} must be positive");
        }

        return value;
    }

    private static long? ValidateSnapshotPayloadRequiredNonNegativeLongValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            errors.Add($"{payloadLabel} {itemLabel} is required");
            return null;
        }

        if (!TryReadLongValue(rawValue, out var value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
            return null;
        }

        if (value < 0)
        {
            errors.Add($"{payloadLabel} {itemLabel} {value} cannot be negative");
        }

        return value;
    }

    private static void ValidateSnapshotPayloadRequiredTrueBoolValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            errors.Add($"{payloadLabel} {itemLabel} is required");
            return;
        }

        if (!TryReadBoolValue(rawValue, out var value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
            return;
        }

        if (!value)
        {
            errors.Add($"{payloadLabel} {itemLabel} must be true");
        }
    }

    private static void ValidateSnapshotPayloadRequiredBoolValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            errors.Add($"{payloadLabel} {itemLabel} is required");
            return;
        }

        if (!TryReadBoolValue(rawValue, out _))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
        }
    }

    private static void ValidateSnapshotPayloadOptionalBoolValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            return;
        }

        if (!TryReadBoolValue(rawValue, out _))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
        }
    }

    private static void ValidateSnapshotPayloadRequiredPositiveIntMapValues(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is required");
            return;
        }

        if (!IsSnapshotIntMapPayloadObject(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is invalid");
            return;
        }

        foreach (var (mapKey, rawValue) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            if (string.IsNullOrWhiteSpace(mapKey))
            {
                continue;
            }

            var normalizedKey = mapKey.Trim();
            if (!TryReadIntValue(rawValue, out var value))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} value is invalid");
                continue;
            }

            if (value <= 0)
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} value {value} must be positive");
            }
        }
    }

    private static void ValidateSnapshotPayloadRequiredIntMapPayloadShape(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload))
        {
            return;
        }

        if (!IsSnapshotIntMapPayloadObject(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map payload is required");
        }
    }

    private static void ValidateSnapshotPayloadRequiredNonNegativeIntMapValues(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var mapPayload)
            || IsNullSnapshotPayloadValue(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is required");
            return;
        }

        if (!IsSnapshotIntMapPayloadObject(mapPayload))
        {
            errors.Add($"{payloadLabel} {itemLabel} map is invalid");
            return;
        }

        foreach (var (mapKey, rawValue) in EnumerateSnapshotPayloadObjectValues(mapPayload))
        {
            if (string.IsNullOrWhiteSpace(mapKey))
            {
                continue;
            }

            var normalizedKey = mapKey.Trim();
            if (!TryReadIntValue(rawValue, out var value))
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} value is invalid");
                continue;
            }

            if (value < 0)
            {
                errors.Add($"{payloadLabel} {itemLabel} {normalizedKey} value {value} cannot be negative");
            }
        }
    }
}
