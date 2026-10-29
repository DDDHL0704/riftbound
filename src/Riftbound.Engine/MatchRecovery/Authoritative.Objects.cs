using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateAuthoritativeStateCardObjectIdentities(
        IReadOnlyDictionary<string, CardObjectState>? cardObjects,
        List<string> errors)
    {
        if (cardObjects is null)
        {
            return;
        }

        foreach (var (objectId, cardObject) in cardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(cardObject.ObjectId))
            {
                errors.Add($"authoritative state card object {objectId} object id is required");
                continue;
            }

            var normalizedMapObjectId = objectId.Trim();
            var normalizedCardObjectId = cardObject.ObjectId.Trim();
            if (!string.Equals(cardObject.ObjectId, normalizedCardObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state card object {objectId} object id {normalizedCardObjectId} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} object id",
                normalizedCardObjectId,
                errors);

            if (!string.Equals(normalizedMapObjectId, normalizedCardObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state card object {objectId} object id {normalizedCardObjectId} does not match map key");
            }
        }
    }

    private static void ValidateAuthoritativeStateCardObjectValues(
        IReadOnlyDictionary<string, CardObjectState>? cardObjects,
        List<string> errors)
    {
        if (cardObjects is null)
        {
            return;
        }

        foreach (var (objectId, cardObject) in cardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                continue;
            }

            var cardNo = ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} card no",
                cardObject.CardNo,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} card no",
                cardNo,
                errors);
            var attachedToObjectId = ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} attached object",
                cardObject.AttachedToObjectId,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} attached object",
                attachedToObjectId,
                errors);

            if (cardObject.UntilEndOfTurnEffects.Any(effect => !TemporaryCombatKeywordRules.IsValid(effect)))
                errors.Add($"authoritative state card object {objectId} has invalid temporary combat keyword grant");

            if (cardObject.MovementCountThisTurn < 0)
                errors.Add($"authoritative state card object {objectId} movement count cannot be negative");

            if (cardObject.Damage < 0)
            {
                errors.Add($"authoritative state card object {objectId} damage {cardObject.Damage} cannot be negative");
            }

            if (cardObject.ManaCost < 0)
            {
                errors.Add($"authoritative state card object {objectId} mana cost {cardObject.ManaCost} cannot be negative");
            }

            ValidateAuthoritativeStateStringListValues(
                $"card object {objectId} until end of turn effect",
                cardObject.UntilEndOfTurnEffects,
                errors,
                rejectDuplicates: true,
                rejectRedactionSentinel: true);
            ValidateAuthoritativeStateStringListValues(
                $"card object {objectId} tag",
                cardObject.Tags,
                errors,
                rejectDuplicates: true,
                rejectRedactionSentinel: true);
            ValidateAuthoritativeStateCardObjectPowerModifierValues(
                objectId,
                cardObject.UntilEndOfTurnPowerModifiers,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateCardObjectPowerModifierValues(
        string objectId,
        IReadOnlyList<PowerModifierLedgerEntry>? powerModifiers,
        List<string> errors)
    {
        if (powerModifiers is null)
        {
            errors.Add($"authoritative state card object {objectId} power modifier list is required");
            return;
        }

        var seenEffectIds = new HashSet<string>(StringComparer.Ordinal);
        var seenAppliedOrders = new HashSet<int>();
        foreach (var modifier in powerModifiers.OrderBy(
            entry => entry?.EffectId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (modifier is null)
            {
                errors.Add($"authoritative state card object {objectId} power modifier is required");
                continue;
            }

            var modifierId = ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier id",
                modifier.EffectId,
                errors);
            var modifierLabel = modifierId ?? "<unknown>";
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} power modifier id",
                modifierId,
                errors);
            if (modifierId is not null && !seenEffectIds.Add(modifierId))
            {
                errors.Add($"authoritative state card object {objectId} power modifier {modifierId} is duplicated");
            }

            var effectKind = ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} effect kind",
                modifier.EffectKind,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} power modifier {modifierLabel} effect kind",
                effectKind,
                errors);
            var duration = ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} duration",
                modifier.Duration,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} power modifier {modifierLabel} duration",
                duration,
                errors);
            var targetObjectId = ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} target object",
                modifier.TargetObjectId,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} power modifier {modifierLabel} target object",
                targetObjectId,
                errors);
            var sourcePath = ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} source path",
                modifier.SourcePath,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} power modifier {modifierLabel} source path",
                sourcePath,
                errors);
            var sourceObjectId = ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} power modifier {modifierLabel} source object",
                modifier.SourceObjectId,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} power modifier {modifierLabel} source object",
                sourceObjectId,
                errors);
            var sourceCardNo = ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} power modifier {modifierLabel} source card no",
                modifier.SourceCardNo,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"card object {objectId} power modifier {modifierLabel} source card no",
                sourceCardNo,
                errors);

            if (targetObjectId is not null && !string.Equals(targetObjectId, objectId.Trim(), StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state card object {objectId} power modifier {modifierLabel} target object {targetObjectId} does not match card object");
            }

            if (modifier.PowerDelta == 0)
            {
                errors.Add(
                    $"authoritative state card object {objectId} power modifier {modifierLabel} power delta 0 is invalid");
            }

            if (modifier.MinimumPower < 0)
            {
                errors.Add(
                    $"authoritative state card object {objectId} power modifier {modifierLabel} minimum power {modifier.MinimumPower} cannot be negative");
            }

            ValidateContinuousEffectPowerFloorConsistency(
                $"authoritative state card object {objectId} power modifier {modifierLabel}",
                modifier.MinimumPower,
                modifier.ResultingPower,
                modifier.RequestedPowerDelta,
                modifier.PowerDelta,
                modifier.SnapshotMight,
                errors, modifier.EffectKind, modifier.Duration, modifier.SourceCardNo, modifier.SourcePath, modifier.EffectId);

            if (modifier.AppliedOrder is { } appliedOrder)
            {
                if (appliedOrder < 1)
                {
                    errors.Add(
                        $"authoritative state card object {objectId} power modifier {modifierLabel} applied order {appliedOrder} is invalid");
                }
                else if (!seenAppliedOrders.Add(appliedOrder))
                {
                    errors.Add(
                        $"authoritative state card object {objectId} power modifier applied order {appliedOrder} is duplicated");
                }
            }
        }
    }

    private static void ValidateAuthoritativeStateObjectLocationPlayers(
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (objectLocations is null)
        {
            errors.Add("authoritative state object locations map is required");
            return;
        }

        foreach (var (objectId, location) in objectLocations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (location is null)
            {
                errors.Add($"authoritative state object location {objectId} is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"object location {objectId} player",
                location.PlayerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"object location {objectId} player",
                location.PlayerId,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateObjectLocationZones(
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        List<string> errors)
    {
        if (objectLocations is null)
        {
            return;
        }

        foreach (var (objectId, location) in objectLocations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (location is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(location.Zone))
            {
                errors.Add($"authoritative state object location {objectId} zone is required");
                continue;
            }

            var normalizedZone = location.Zone.Trim();
            if (!string.Equals(location.Zone, normalizedZone, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state object location {objectId} zone {normalizedZone} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel(
                $"object location {objectId} zone",
                normalizedZone,
                errors);

            if (!IsKnownObjectLocationZone(normalizedZone))
            {
                errors.Add($"authoritative state object location {objectId} zone {normalizedZone} is not supported");
            }

            var battlefieldObjectId = ValidateAuthoritativeStateNullableTextValue(
                $"object location {objectId} battlefield object",
                location.BattlefieldObjectId,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"object location {objectId} battlefield object",
                battlefieldObjectId,
                errors);
        }
    }

    private static bool IsKnownObjectLocationZone(string zone)
    {
        return zone switch
        {
            "MAIN_DECK"
                or "RUNE_DECK"
                or "HAND"
                or "BASE"
                or "BATTLEFIELD"
                or "GRAVEYARD"
                or "BANISHED"
                or "LEGEND"
                or "CHAMPION"
                or "STACK" => true,
            _ => false
        };
    }

    private static void ValidateAuthoritativeStateObjectLocationPlayerZones(
        IReadOnlyDictionary<string, PlayerZones>? playerZones,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        List<string> errors)
    {
        if (playerZones is null || objectLocations is null)
        {
            return;
        }

        var playerZoneLocations = BuildAuthoritativeStatePlayerZoneObjectIndex(
            playerZones,
            errors,
            reportZoneValueErrors: false);
        if (playerZoneLocations.Count == 0)
        {
            return;
        }

        foreach (var (objectId, location) in objectLocations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (location is null
                || string.IsNullOrWhiteSpace(objectId)
                || string.IsNullOrWhiteSpace(location.PlayerId)
                || string.IsNullOrWhiteSpace(location.Zone))
            {
                continue;
            }

            var normalizedZone = location.Zone.Trim();
            if (!IsKnownObjectLocationZone(normalizedZone)
                || string.Equals(normalizedZone, "STACK", StringComparison.Ordinal))
            {
                continue;
            }

            var normalizedObjectId = objectId.Trim();
            var normalizedPlayerId = location.PlayerId.Trim();
            if (!playerZoneLocations.TryGetValue(normalizedObjectId, out var playerZoneLocation))
            {
                errors.Add($"authoritative state object location {normalizedObjectId} is missing from player zones");
                continue;
            }

            if (!string.Equals(playerZoneLocation.PlayerId, normalizedPlayerId, StringComparison.Ordinal)
                || !string.Equals(playerZoneLocation.Zone, normalizedZone, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state object location {normalizedObjectId} {normalizedPlayerId}/{normalizedZone} disagrees with player zones {playerZoneLocation.PlayerId}/{playerZoneLocation.Zone}");
            }
        }
    }

    private static void ValidateAuthoritativeStatePlayerZoneValues(
        IReadOnlyDictionary<string, PlayerZones>? playerZones,
        List<string> errors)
    {
        if (playerZones is null)
        {
            return;
        }

        _ = BuildAuthoritativeStatePlayerZoneObjectIndex(
            playerZones,
            errors,
            reportZoneValueErrors: true);
    }

    private static Dictionary<string, AuthoritativeStateZoneLocation> BuildAuthoritativeStatePlayerZoneObjectIndex(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        List<string> errors,
        bool reportZoneValueErrors)
    {
        var playerZoneLocations = new Dictionary<string, AuthoritativeStateZoneLocation>(StringComparer.Ordinal);
        foreach (var (playerId, zones) in playerZones.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (zones is null)
            {
                if (reportZoneValueErrors)
                {
                    errors.Add($"authoritative state player zones for {normalizedPlayerId} are required");
                }

                continue;
            }

            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "MAIN_DECK",
                zones.MainDeck,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "RUNE_DECK",
                zones.RuneDeck,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "HAND",
                zones.Hand,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "BASE",
                zones.Base,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "BATTLEFIELD",
                zones.Battlefields,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "GRAVEYARD",
                zones.Graveyard,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "BANISHED",
                zones.Banished,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "LEGEND",
                zones.LegendZone,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "CHAMPION",
                zones.ChampionZone,
                errors,
                reportZoneValueErrors);
        }

        return playerZoneLocations;
    }

    private static void AddAuthoritativeStatePlayerZoneObjects(
        Dictionary<string, AuthoritativeStateZoneLocation> playerZoneLocations,
        string playerId,
        string zone,
        IReadOnlyList<string>? objectIds,
        List<string> errors,
        bool reportZoneValueErrors)
    {
        if (objectIds is null)
        {
            if (reportZoneValueErrors)
            {
                errors.Add($"authoritative state player zones {playerId}/{zone} list is required");
            }

            return;
        }

        foreach (var objectId in objectIds)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                if (reportZoneValueErrors)
                {
                    errors.Add($"authoritative state player zones {playerId}/{zone} object id is required");
                }

                continue;
            }

            var normalizedObjectId = objectId.Trim();
            if (reportZoneValueErrors
                && !string.Equals(objectId, normalizedObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state player zones {playerId}/{zone} object {normalizedObjectId} has surrounding whitespace");
            }

            if (reportZoneValueErrors)
            {
                RejectAuthoritativeStateRedactionSentinel(
                    $"player zones {playerId}/{zone} object",
                    normalizedObjectId,
                    errors);
            }

            var location = new AuthoritativeStateZoneLocation(playerId, zone);
            if (playerZoneLocations.TryGetValue(normalizedObjectId, out var existingLocation))
            {
                if (reportZoneValueErrors)
                {
                    errors.Add(
                        $"authoritative state player zones object {normalizedObjectId} is duplicated between {existingLocation.PlayerId}/{existingLocation.Zone} and {playerId}/{zone}");
                }

                continue;
            }

            playerZoneLocations[normalizedObjectId] = location;
        }
    }

    private readonly record struct AuthoritativeStateZoneLocation(string PlayerId, string Zone);

    private static void ValidateAuthoritativeStateStackPlayers(
        IReadOnlyList<StackItemState>? stackItems,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (stackItems is null)
        {
            errors.Add("authoritative state stack items list is required");
            return;
        }

        foreach (var stackItem in stackItems.OrderBy(item => item?.StackItemId ?? string.Empty, StringComparer.Ordinal))
        {
            if (stackItem is null)
            {
                errors.Add("authoritative state stack item is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"stack item {stackItem.StackItemId} controller player",
                stackItem.ControllerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateTriggerQueuePlayers(
        IReadOnlyList<TriggerQueueItemState>? triggerQueue,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (triggerQueue is null)
        {
            errors.Add("authoritative state trigger queue list is required");
            return;
        }

        foreach (var trigger in triggerQueue.OrderBy(item => item?.TriggerId ?? string.Empty, StringComparer.Ordinal))
        {
            if (trigger is null)
            {
                errors.Add("authoritative state trigger queue item is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"trigger queue item {trigger.TriggerId} controller player",
                trigger.ControllerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStatePendingPaymentPlayer(
        PendingPaymentState? pendingPayment,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (pendingPayment is null)
        {
            return;
        }

        ValidateAuthoritativeStateRequiredObjectPlayer(
            "pending payment player",
            pendingPayment.PlayerId,
            seatPlayerIds,
            errors);
        var paymentLabel = string.IsNullOrWhiteSpace(pendingPayment.PaymentId)
            ? "<unknown>"
            : pendingPayment.PaymentId.Trim();
        RejectAuthoritativeStateRedactionSentinel(
            $"pending payment {paymentLabel} player",
            pendingPayment.PlayerId,
            errors);
    }

    private static void ValidateAuthoritativeStatePendingHandChoicePlayer(
        PendingHandChoiceState? pendingHandChoice,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (pendingHandChoice is null)
        {
            return;
        }

        ValidateAuthoritativeStateRequiredObjectPlayer(
            "pending hand choice player",
            pendingHandChoice.PlayerId,
            seatPlayerIds,
            errors);
        var choiceLabel = string.IsNullOrWhiteSpace(pendingHandChoice.ChoiceId)
            ? "<unknown>"
            : pendingHandChoice.ChoiceId.Trim();
        RejectAuthoritativeStateRedactionSentinel(
            $"pending hand choice {choiceLabel} player",
            pendingHandChoice.PlayerId,
            errors);
    }

    private static IReadOnlySet<string> BuildAuthoritativeStateBattlefieldStateObjectIds(MatchState authoritativeState)
    {
        return authoritativeState.BattlefieldStates.Values
            .Select(battlefield => battlefield.BattlefieldObjectId)
            .Where(battlefieldObjectId => !string.IsNullOrWhiteSpace(battlefieldObjectId))
            .Select(battlefieldObjectId => battlefieldObjectId.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAuthoritativeStateBattlefieldStateOccupantObjectIds(
        MatchState authoritativeState)
    {
        return authoritativeState.BattlefieldStates.Values
            .Where(battlefield => !string.IsNullOrWhiteSpace(battlefield.BattlefieldObjectId))
            .ToDictionary(
                battlefield => battlefield.BattlefieldObjectId.Trim(),
                battlefield => (IReadOnlySet<string>)battlefield.OccupantObjectIds
                    .Where(occupantObjectId => !string.IsNullOrWhiteSpace(occupantObjectId))
                    .Select(occupantObjectId => occupantObjectId.Trim())
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAuthoritativeStateRequiredBattlefieldTaskKindsByBattlefield(
        IReadOnlyList<BattlefieldTaskState> battlefieldTasks)
    {
        var taskKindsByBattlefield = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var battlefieldTask in battlefieldTasks)
        {
            if (string.IsNullOrWhiteSpace(battlefieldTask.BattlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldTask.Kind)
                || !IsKnownBattlefieldTaskKind(battlefieldTask.Kind))
            {
                continue;
            }

            var battlefieldObjectId = battlefieldTask.BattlefieldObjectId.Trim();
            if (!taskKindsByBattlefield.TryGetValue(battlefieldObjectId, out var taskKinds))
            {
                taskKinds = new HashSet<string>(StringComparer.Ordinal);
                taskKindsByBattlefield[battlefieldObjectId] = taskKinds;
            }

            taskKinds.Add(battlefieldTask.Kind.Trim());
        }

        return taskKindsByBattlefield.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlySet<string>)entry.Value,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<(string BattlefieldObjectId, string Kind), BattlefieldTaskState>
        BuildAuthoritativeStateBattlefieldTasksByKey(IReadOnlyList<BattlefieldTaskState> battlefieldTasks)
    {
        var battlefieldTasksByKey =
            new Dictionary<(string BattlefieldObjectId, string Kind), BattlefieldTaskState>();
        foreach (var battlefieldTask in battlefieldTasks)
        {
            if (string.IsNullOrWhiteSpace(battlefieldTask.BattlefieldObjectId)
                || string.IsNullOrWhiteSpace(battlefieldTask.Kind)
                || !IsKnownBattlefieldTaskKind(battlefieldTask.Kind))
            {
                continue;
            }

            var key = (battlefieldTask.BattlefieldObjectId.Trim(), battlefieldTask.Kind.Trim());
            if (!battlefieldTasksByKey.ContainsKey(key))
            {
                battlefieldTasksByKey[key] = battlefieldTask;
            }
        }

        return battlefieldTasksByKey;
    }

    private static IReadOnlyDictionary<string, string> BuildAuthoritativeStateObjectControllerIndex(MatchState authoritativeState)
    {
        var objectControllers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (objectId, cardObject) in authoritativeState.CardObjects)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                continue;
            }

            var controllerId = EffectiveAuthoritativeObjectControllerId(authoritativeState, objectId, cardObject);
            if (!string.IsNullOrWhiteSpace(controllerId))
            {
                objectControllers[objectId.Trim()] = controllerId.Trim();
            }
        }

        return objectControllers;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAuthoritativeStateObjectTagIndex(MatchState authoritativeState)
    {
        var objectTags = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var (objectId, cardObject) in authoritativeState.CardObjects)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                continue;
            }

            objectTags[objectId.Trim()] = cardObject.Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return objectTags;
    }

    private static IReadOnlyDictionary<string, string?> BuildAuthoritativeStateObjectCardNoIndex(MatchState authoritativeState)
    {
        var objectCardNos = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (objectId, cardObject) in authoritativeState.CardObjects)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                continue;
            }

            objectCardNos[objectId.Trim()] = string.IsNullOrWhiteSpace(cardObject.CardNo)
                ? null
                : cardObject.CardNo.Trim();
        }

        return objectCardNos;
    }

    private static IReadOnlyDictionary<string, int> BuildAuthoritativeStateObjectPowerIndex(MatchState authoritativeState)
    {
        var objectPowers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (objectId, cardObject) in authoritativeState.CardObjects)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                continue;
            }

            objectPowers[objectId.Trim()] = cardObject.Power;
        }

        return objectPowers;
    }

    private static IReadOnlyDictionary<string, bool> BuildAuthoritativeStateObjectFaceDownIndex(MatchState authoritativeState)
    {
        var objectFaceDowns = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (objectId, cardObject) in authoritativeState.CardObjects)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                continue;
            }

            objectFaceDowns[objectId.Trim()] = cardObject.IsFaceDown;
        }

        return objectFaceDowns;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAuthoritativeStatePlayerBattlefieldObjectIdsByPlayer(
        IReadOnlyDictionary<string, PlayerZones>? playerZones)
    {
        var playerBattlefieldObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (playerZones is null)
        {
            return playerBattlefieldObjectIdsByPlayer;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || zones is null
                || zones.Battlefields is null)
            {
                continue;
            }

            playerBattlefieldObjectIdsByPlayer[playerId.Trim()] = zones.Battlefields
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerBattlefieldObjectIdsByPlayer;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAuthoritativeStatePlayerBaseObjectIdsByPlayer(
        IReadOnlyDictionary<string, PlayerZones>? playerZones)
    {
        var playerBaseObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (playerZones is null)
        {
            return playerBaseObjectIdsByPlayer;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || zones is null
                || zones.Base is null)
            {
                continue;
            }

            playerBaseObjectIdsByPlayer[playerId.Trim()] = zones.Base
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerBaseObjectIdsByPlayer;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAuthoritativeStatePlayerGraveyardObjectIdsByPlayer(
        IReadOnlyDictionary<string, PlayerZones>? playerZones)
    {
        var playerGraveyardObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (playerZones is null)
        {
            return playerGraveyardObjectIdsByPlayer;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || zones is null
                || zones.Graveyard is null)
            {
                continue;
            }

            playerGraveyardObjectIdsByPlayer[playerId.Trim()] = zones.Graveyard
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerGraveyardObjectIdsByPlayer;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildAuthoritativeStatePlayerFieldObjectIdsByPlayer(
        IReadOnlyDictionary<string, PlayerZones>? playerZones)
    {
        var playerFieldObjectIdsByPlayer =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        if (playerZones is null)
        {
            return playerFieldObjectIdsByPlayer;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.IsNullOrWhiteSpace(playerId) || zones is null)
            {
                continue;
            }

            playerFieldObjectIdsByPlayer[playerId.Trim()] = zones.Base
                .Concat(zones.Battlefields)
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Select(objectId => objectId.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }

        return playerFieldObjectIdsByPlayer;
    }

    private static string? EffectiveAuthoritativeObjectControllerId(
        MatchState authoritativeState,
        string objectId,
        CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return authoritativeState.ObjectLocations.TryGetValue(objectId, out var location)
            ? location.PlayerId
            : null;
    }

    private static void AddKnownObjectId(HashSet<string> knownObjectIds, string? objectId)
    {
        if (!string.IsNullOrWhiteSpace(objectId))
        {
            knownObjectIds.Add(objectId.Trim());
        }
    }

    private static void ValidateAuthoritativeStateObjectReferenceList(
        string objectLabel,
        IReadOnlyList<string>? objectIds,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (objectIds is null)
        {
            errors.Add($"authoritative state {objectLabel} list is required");
            return;
        }

        foreach (var objectId in objectIds)
        {
            ValidateAuthoritativeStateOptionalObjectReference(objectLabel, objectId, knownObjectIds, errors);
        }
    }

    private static void ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
        string objectLabel,
        IReadOnlyList<string>? objectIds,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (objectIds is null)
        {
            errors.Add($"authoritative state {objectLabel} list is required");
            return;
        }

        foreach (var objectId in objectIds)
        {
            ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                objectLabel,
                objectId,
                knownObjectIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateRequiredObjectReference(
        string objectLabel,
        string? objectId,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            errors.Add($"authoritative state {objectLabel} is required");
            return;
        }

        ValidateAuthoritativeStateOptionalObjectReference(objectLabel, objectId, knownObjectIds, errors);
    }

    private static void ValidateAuthoritativeStateRequiredObjectReferenceWithExpectedDetails(
        string objectLabel,
        string? objectId,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            errors.Add($"authoritative state {objectLabel} is required");
            return;
        }

        ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
            objectLabel,
            objectId,
            knownObjectIds,
            errors);
    }

    private static void ValidateAuthoritativeStateOptionalObjectReference(
        string objectLabel,
        string? objectId,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var normalizedObjectId = objectId.Trim();
        if (!string.Equals(objectId, normalizedObjectId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {objectLabel} {normalizedObjectId} has surrounding whitespace");
        }

        if (!knownObjectIds.Contains(normalizedObjectId))
        {
            errors.Add($"authoritative state {objectLabel} {normalizedObjectId} is missing from object registry");
        }
    }

    private static void ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
        string objectLabel,
        string? objectId,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var normalizedObjectId = objectId.Trim();
        if (!string.Equals(objectId, normalizedObjectId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {objectLabel} {normalizedObjectId} has surrounding whitespace");
        }

        if (!knownObjectIds.Contains(normalizedObjectId))
        {
            var expectedObjectIds = knownObjectIds
                .OrderBy(knownObjectId => knownObjectId, StringComparer.Ordinal)
                .ToArray();
            errors.Add(
                $"authoritative state {objectLabel} {normalizedObjectId} is missing from object registry; {FormatExpectedActualForRecovery(expectedObjectIds, normalizedObjectId)}");
        }
    }

    private static void ValidateAuthoritativeStateRequiredObjectPlayer(
        string playerLabel,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {playerLabel} is required");
            return;
        }

        ValidateAuthoritativeStateObjectPlayer(playerLabel, playerId, seatPlayerIds, errors);
    }

    private static void ValidateAuthoritativeStateOptionalObjectPlayer(
        string playerLabel,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (playerId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {playerLabel} is blank");
            return;
        }

        ValidateAuthoritativeStateObjectPlayer(playerLabel, playerId, seatPlayerIds, errors);
    }

    private static void ValidateAuthoritativeStateObjectPlayer(
        string playerLabel,
        string playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {playerLabel} {normalizedPlayerId} has surrounding whitespace");
        }

        if (!seatPlayerIds.Contains(normalizedPlayerId))
        {
            errors.Add(
                $"authoritative state {playerLabel} {normalizedPlayerId} is missing from seats; {FormatAuthoritativeStateExpectedPlayerDetail(seatPlayerIds, normalizedPlayerId)}");
        }
    }

    private static void ValidateAuthoritativeStateBattlefieldResolutionPlayers(
        IReadOnlyList<BattlefieldResolutionState>? battlefieldResolutions,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (battlefieldResolutions is null)
        {
            errors.Add("authoritative state battlefield resolutions list is required");
            return;
        }

        foreach (var resolution in battlefieldResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battlefield resolution is required");
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battlefield resolution {resolution.ResolutionId} player",
                resolution.PlayerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"battlefield resolution {resolution.ResolutionId} player",
                resolution.PlayerId,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battlefield resolution {resolution.ResolutionId} previous controller player",
                resolution.PreviousControllerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"battlefield resolution {resolution.ResolutionId} previous controller player",
                resolution.PreviousControllerId,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battlefield resolution {resolution.ResolutionId} controller player",
                resolution.ControllerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"battlefield resolution {resolution.ResolutionId} controller player",
                resolution.ControllerId,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateBattleResolutionPlayers(
        IReadOnlyList<BattleResolutionState>? battleResolutions,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (battleResolutions is null)
        {
            errors.Add("authoritative state battle resolutions list is required");
            return;
        }

        foreach (var resolution in battleResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battle resolution is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"battle resolution {resolution.ResolutionId} attacking player",
                resolution.AttackingPlayerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"battle resolution {resolution.ResolutionId} attacking player",
                resolution.AttackingPlayerId,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battle resolution {resolution.ResolutionId} defending player",
                resolution.DefendingPlayerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"battle resolution {resolution.ResolutionId} defending player",
                resolution.DefendingPlayerId,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battle resolution {resolution.ResolutionId} winner player",
                resolution.WinnerPlayerId,
                seatPlayerIds,
                errors);
            RejectAuthoritativeStateRedactionSentinel(
                $"battle resolution {resolution.ResolutionId} winner player",
                resolution.WinnerPlayerId,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateOptionalPlayerPointer(
        string pointerName,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (playerId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {pointerName} is blank");
            return;
        }

        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {pointerName} {normalizedPlayerId} has surrounding whitespace");
        }

        RejectAuthoritativeStateRedactionSentinel(pointerName, normalizedPlayerId, errors);

        if (!seatPlayerIds.Contains(normalizedPlayerId))
        {
            errors.Add(
                $"authoritative state {pointerName} {normalizedPlayerId} is missing from seats; {FormatAuthoritativeStateExpectedPlayerDetail(seatPlayerIds, normalizedPlayerId)}");
        }
    }

    private static string FormatAuthoritativeStateExpectedPlayerDetail(
        IReadOnlySet<string> seatPlayerIds,
        string actualPlayerId)
    {
        var expectedPlayerIds = seatPlayerIds
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        return FormatExpectedActualForRecovery(expectedPlayerIds, actualPlayerId);
    }

    private static void ValidateAuthoritativeStateSeats(
        MatchState authoritativeState,
        List<string> errors)
    {
        var seenSeats = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (playerId, seat) in authoritativeState.Seats.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var normalizedPlayerId = playerId.Trim();
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add("authoritative state seat player id is required");
            }
            else if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state seat player {normalizedPlayerId} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel("seat player id", normalizedPlayerId, errors);

            var playerLabel = string.IsNullOrWhiteSpace(normalizedPlayerId)
                ? "<blank>"
                : normalizedPlayerId;
            if (string.IsNullOrWhiteSpace(seat))
            {
                errors.Add($"authoritative state seat for {playerLabel} is required");
                continue;
            }

            var normalizedSeat = seat.Trim();
            if (!string.Equals(seat, normalizedSeat, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state seat {normalizedSeat} for {playerLabel} has surrounding whitespace");
            }

            RejectAuthoritativeStateRedactionSentinel($"seat for {playerLabel}", normalizedSeat, errors);

            if (!IsKnownSeat(normalizedSeat))
            {
                errors.Add($"authoritative state seat {normalizedSeat} for {playerLabel} is invalid");
            }

            if (!seenSeats.Add(normalizedSeat))
            {
                errors.Add($"authoritative state seat {normalizedSeat} is duplicated");
            }
        }
    }
}
