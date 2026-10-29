using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static string? ValidateSnapshotPayloadOptionalStringValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors,
        Func<string, bool>? isKnownValue = null,
        bool rejectEmptyString = false)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            return null;
        }

        if (!TryReadStringValue(rawValue, out var value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
            return null;
        }

        if (value is null)
        {
            return null;
        }

        if (value.Length == 0)
        {
            if (rejectEmptyString)
            {
                errors.Add($"{payloadLabel} {itemLabel} is required");
                return null;
            }

            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
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
            errors.Add($"{payloadLabel} {itemLabel} {normalizedValue} is invalid");
        }

        return normalizedValue;
    }

    private static string? ValidateSnapshotPayloadRequiredNullableStringValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue))
        {
            errors.Add($"{payloadLabel} {itemLabel} field is required");
            return null;
        }

        if (IsNullSnapshotPayloadValue(rawValue))
        {
            return null;
        }

        if (!TryReadStringValue(rawValue, out var value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
            return null;
        }

        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is required");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"{payloadLabel} {itemLabel} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static int? ValidateSnapshotPayloadOptionalIntValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        if (!TryReadObjectValue(payload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            return null;
        }

        if (!TryReadIntValue(rawValue, out var value))
        {
            errors.Add($"{payloadLabel} {itemLabel} is invalid");
            return null;
        }

        return value;
    }

    private static int? ValidateSnapshotPayloadOptionalNonNegativeIntValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        var value = ValidateSnapshotPayloadOptionalIntValue(payload, key, payloadLabel, itemLabel, errors);
        if (value is < 0)
        {
            errors.Add($"{payloadLabel} {itemLabel} {value} cannot be negative");
        }

        return value;
    }

    private static int? ValidateSnapshotPayloadOptionalPositiveIntValue(
        object? payload,
        string key,
        string payloadLabel,
        string itemLabel,
        List<string> errors)
    {
        var value = ValidateSnapshotPayloadOptionalIntValue(payload, key, payloadLabel, itemLabel, errors);
        if (value is <= 0)
        {
            errors.Add($"{payloadLabel} {itemLabel} {value} must be positive");
        }

        return value;
    }

    private static IEnumerable<string> EnumerateSnapshotPayloadObjectPropertyNames(object? payload)
    {
        return payload switch
        {
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.Keys,
            IDictionary<string, object?> dictionary => dictionary.Keys,
            IReadOnlyDictionary<string, string> stringDictionary => stringDictionary.Keys,
            IEnumerable<KeyValuePair<string, string>> stringPairs => stringPairs.Select(entry => entry.Key),
            IReadOnlyDictionary<string, int> intDictionary => intDictionary.Keys,
            IEnumerable<KeyValuePair<string, int>> intPairs => intPairs.Select(entry => entry.Key),
            IReadOnlyDictionary<string, IReadOnlyList<string>> stringListDictionary => stringListDictionary.Keys,
            IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> stringListPairs => stringListPairs.Select(entry => entry.Key),
            JsonElement { ValueKind: JsonValueKind.Object } json => json.EnumerateObject().Select(property => property.Name),
            _ => []
        };
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumerateSnapshotPayloadObjectValues(object? payload)
    {
        return payload switch
        {
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary,
            IDictionary<string, object?> dictionary => dictionary,
            IReadOnlyDictionary<string, string> stringDictionary => stringDictionary.Select(entry =>
                new KeyValuePair<string, object?>(entry.Key, entry.Value)),
            IEnumerable<KeyValuePair<string, string>> stringPairs => stringPairs.Select(entry =>
                new KeyValuePair<string, object?>(entry.Key, entry.Value)),
            IReadOnlyDictionary<string, int> intDictionary => intDictionary.Select(entry =>
                new KeyValuePair<string, object?>(entry.Key, entry.Value)),
            IEnumerable<KeyValuePair<string, int>> intPairs => intPairs.Select(entry =>
                new KeyValuePair<string, object?>(entry.Key, entry.Value)),
            IReadOnlyDictionary<string, IReadOnlyList<string>> stringListDictionary => stringListDictionary.Select(entry =>
                new KeyValuePair<string, object?>(entry.Key, entry.Value)),
            IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> stringListPairs => stringListPairs.Select(entry =>
                new KeyValuePair<string, object?>(entry.Key, entry.Value)),
            JsonElement { ValueKind: JsonValueKind.Object } json => json.EnumerateObject().Select(property =>
                new KeyValuePair<string, object?>(property.Name, property.Value)),
            _ => []
        };
    }

    private static bool IsNullSnapshotPayloadValue(object? payload)
    {
        return payload is null
            || payload is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };
    }

    private static void ValidateSpectatorSnapshotPlayerPayloads(
        SnapshotDto snapshot,
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateSnapshotPayloadObjectPropertyNames(
            snapshot.Players,
            "spectator replay frame snapshot players",
            errors);

        var expectedPlayerIds = authoritativeState.Seats.Keys
            .OrderBy(expectedPlayerId => expectedPlayerId, StringComparer.Ordinal)
            .ToArray();
        var actualPlayerIds = snapshot.Players.Keys
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        foreach (var expectedPlayerId in expectedPlayerIds)
        {
            if (!snapshot.Players.ContainsKey(expectedPlayerId))
            {
                errors.Add(
                    $"spectator replay frame snapshot is missing player {expectedPlayerId}; {FormatExpectedActualForRecovery(expectedPlayerIds, actualPlayerIds)}");
            }
        }

        foreach (var (playerId, playerPayload) in snapshot.Players)
        {
            if (!authoritativeState.Seats.ContainsKey(playerId))
            {
                errors.Add(
                    $"spectator replay frame snapshot player {playerId} is not in authoritative seats; {FormatExpectedActualForRecovery(expectedPlayerIds, playerId)}");
            }

            if (!IsSnapshotPlayerPayloadObject(playerPayload))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} payload is required");
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                playerPayload,
                $"spectator replay frame snapshot player {playerId}",
                errors);

            var zones = authoritativeState.PlayerZones.TryGetValue(playerId, out var playerZones)
                ? playerZones
                : PlayerZones.Empty;

            ValidateSpectatorSnapshotPlayerScalarValues(playerId, playerPayload, zones, authoritativeState, errors);
            ValidateSpectatorSnapshotPlayerRunePoolPayload(playerId, playerPayload, authoritativeState, errors);
            ValidateSpectatorSnapshotPlayerZonePayloads(playerId, playerPayload, zones, authoritativeState, errors);
            ValidateSpectatorSnapshotPlayerObjectPayloads(playerId, playerPayload, authoritativeState, errors);
        }
    }

    private static void ValidateSpectatorSnapshotPlayerScalarValues(
        string playerId,
        object? playerPayload,
        PlayerZones zones,
        MatchState authoritativeState,
        List<string> errors)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId}";
        var payloadId = ValidateSnapshotPayloadRequiredStringValue(
            playerPayload,
            "id",
            payloadLabel,
            "payload id",
            errors);
        if (payloadId is null || !string.Equals(payloadId, playerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} payload id does not match player key; {FormatExpectedActualForRecovery(playerId, payloadId ?? FormatUnreadableObjectValue(playerPayload, "id"))}");
        }

        var name = ValidateSnapshotPayloadRequiredStringValue(
            playerPayload,
            "name",
            payloadLabel,
            "name",
            errors);
        if (name is null || !string.Equals(name, playerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} name does not match player id; {FormatExpectedActualForRecovery(playerId, name ?? FormatUnreadableObjectValue(playerPayload, "name"))}");
        }

        var expectedReady = authoritativeState.ReadyPlayerIds.Contains(playerId, StringComparer.Ordinal);
        ValidateSnapshotPayloadRequiredBoolValue(playerPayload, "ready", payloadLabel, "ready", errors);
        var hasReady = TryReadObjectBool(playerPayload, "ready", out var ready);
        if (!hasReady || ready != expectedReady)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} ready does not match authoritative state ready; {FormatExpectedActualForRecovery(expectedReady, FormatReadableBoolValue(hasReady, ready, playerPayload, "ready"))}");
        }

        var handSize = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            playerPayload,
            "handSize",
            payloadLabel,
            "hand size",
            errors);
        if (handSize is null || handSize != zones.Hand.Count)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} hand size does not match authoritative state hand size; {FormatExpectedActualForRecovery(zones.Hand.Count, FormatReadableIntValue(handSize, playerPayload, "handSize"))}");
        }

        var expectedScore = authoritativeState.PlayerScores.TryGetValue(playerId, out var score)
            ? score
            : 0;
        var payloadScore = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            playerPayload,
            "score",
            payloadLabel,
            "score",
            errors);
        if (payloadScore is null || payloadScore != expectedScore)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} score does not match authoritative state score; {FormatExpectedActualForRecovery(expectedScore, FormatReadableIntValue(payloadScore, playerPayload, "score"))}");
        }

        var expectedExperience = authoritativeState.PlayerExperience.TryGetValue(playerId, out var experience)
            ? experience
            : 0;
        var payloadExperience = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            playerPayload,
            "experience",
            payloadLabel,
            "experience",
            errors);
        if (payloadExperience is null || payloadExperience != expectedExperience)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} experience does not match authoritative state experience; {FormatExpectedActualForRecovery(expectedExperience, FormatReadableIntValue(payloadExperience, playerPayload, "experience"))}");
        }

        var expectedCardsPlayedThisTurn = authoritativeState.PlayerCardsPlayedThisTurn.TryGetValue(
            playerId,
            out var cardsPlayedThisTurn)
            ? cardsPlayedThisTurn
            : 0;
        var payloadCardsPlayedThisTurn = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            playerPayload,
            "cardsPlayedThisTurn",
            payloadLabel,
            "cards played this turn",
            errors);
        if (payloadCardsPlayedThisTurn is null || payloadCardsPlayedThisTurn != expectedCardsPlayedThisTurn)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} cards played this turn does not match authoritative state cards played this turn; {FormatExpectedActualForRecovery(expectedCardsPlayedThisTurn, FormatReadableIntValue(payloadCardsPlayedThisTurn, playerPayload, "cardsPlayedThisTurn"))}");
        }

        var expectedDeckSubmitted = authoritativeState.PlayerDecklists.ContainsKey(playerId);
        ValidateSnapshotPayloadRequiredBoolValue(
            playerPayload,
            "deckSubmitted",
            payloadLabel,
            "deck submitted",
            errors);
        var hasDeckSubmitted = TryReadObjectBool(playerPayload, "deckSubmitted", out var deckSubmitted);
        if (!hasDeckSubmitted || deckSubmitted != expectedDeckSubmitted)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} deck submitted does not match authoritative state deck submitted; {FormatExpectedActualForRecovery(expectedDeckSubmitted, FormatReadableBoolValue(hasDeckSubmitted, deckSubmitted, playerPayload, "deckSubmitted"))}");
        }

        var expectedMulliganCompleted = authoritativeState.MulliganCompletedPlayerIds.Contains(
            playerId,
            StringComparer.Ordinal);
        ValidateSnapshotPayloadRequiredBoolValue(
            playerPayload,
            "mulliganCompleted",
            payloadLabel,
            "mulligan completed",
            errors);
        var hasMulliganCompleted = TryReadObjectBool(playerPayload, "mulliganCompleted", out var mulliganCompleted);
        if (!hasMulliganCompleted || mulliganCompleted != expectedMulliganCompleted)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} mulligan completed does not match authoritative state mulligan completed; {FormatExpectedActualForRecovery(expectedMulliganCompleted, FormatReadableBoolValue(hasMulliganCompleted, mulliganCompleted, playerPayload, "mulliganCompleted"))}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerRunePoolPayload(
        string playerId,
        object? playerPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "runePool", out var runePoolPayload)
            || IsNullSnapshotPayloadValue(runePoolPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune pool is required");
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(runePoolPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune pool payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            runePoolPayload,
            $"spectator replay frame snapshot player {playerId} rune pool",
            errors);
        ValidateSpectatorSnapshotPlayerRunePoolPowerTraitPayloadPropertyNames(playerId, runePoolPayload, errors);
        ValidateSpectatorSnapshotPlayerRunePoolPayloadValues(playerId, runePoolPayload, errors);

        var expectedRunePool = authoritativeState.RunePools.TryGetValue(playerId, out var runePool)
            ? runePool
            : RunePool.Empty;
        var hasMana = TryReadObjectInt(runePoolPayload, "mana", out var mana);
        if (!hasMana || mana != expectedRunePool.Mana)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} rune pool mana does not match authoritative rune pool mana; {FormatExpectedActualForRecovery(expectedRunePool.Mana, FormatReadableIntValue(hasMana ? mana : null, runePoolPayload, "mana"))}");
        }

        foreach (var (property, expectedAmount, name) in new[] {
            ("spellOnlyMana", expectedRunePool.SpellOnlyMana, "spell-only mana"),
            ("duelOnlyMana", expectedRunePool.DuelOnlyMana, "duel-only mana"),
            ("unitPlayOrUnitAbilityOnlyMana", expectedRunePool.UnitPlayOrUnitAbilityOnlyMana, "unit-play-or-ability-only mana"),
            ("spellOnlyPower", expectedRunePool.SpellOnlyPower, "spell-only power"),
            ("equipmentOnlyPower", expectedRunePool.EquipmentOnlyPower, "equipment-only power"),
            ("unitOrEquipmentPlayOnlyPower", expectedRunePool.UnitOrEquipmentPlayOnlyPower, "unit-or-equipment-play-only power") })
        {
            var present = TryReadObjectValue(runePoolPayload, property, out _);
            var numeric = TryReadObjectInt(runePoolPayload, property, out var restrictedAmount);
            if (present ? !numeric || restrictedAmount != expectedAmount : expectedAmount != 0)
                errors.Add($"spectator replay frame snapshot player {playerId} {name} does not match authoritative rune pool");
        }

        var hasPower = TryReadObjectInt(runePoolPayload, "power", out var power);
        if (!hasPower || power != expectedRunePool.TotalPower)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} rune pool total power does not match authoritative rune pool total power; {FormatExpectedActualForRecovery(expectedRunePool.TotalPower, FormatReadableIntValue(hasPower ? power : null, runePoolPayload, "power"))}");
        }

        var hasUntypedPower = TryReadObjectInt(runePoolPayload, "untypedPower", out var untypedPower);
        if (!hasUntypedPower || untypedPower != expectedRunePool.Power)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} rune pool untyped power does not match authoritative rune pool untyped power; {FormatExpectedActualForRecovery(expectedRunePool.Power, FormatReadableIntValue(hasUntypedPower ? untypedPower : null, runePoolPayload, "untypedPower"))}");
        }

        var hasPowerByTrait = TryReadObjectIntDictionary(runePoolPayload, "powerByTrait", out var powerByTrait);
        if (!hasPowerByTrait || !IntDictionariesEqual(powerByTrait, expectedRunePool.PowerByTrait))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} rune pool power by trait does not match authoritative rune pool power by trait; {FormatExpectedActualForRecovery(expectedRunePool.PowerByTrait, FormatReadableIntDictionaryValue(hasPowerByTrait, powerByTrait, runePoolPayload, "powerByTrait"))}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerRunePoolPayloadValues(
        string playerId,
        object? runePoolPayload,
        List<string> errors)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId} rune pool";
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
        ValidateSpectatorRequiredIntMapPayloadShape(
            runePoolPayload,
            payloadLabel,
            "powerByTrait",
            "power trait",
            errors);
        ValidateSnapshotPayloadRequiredPositiveIntMapValues(
            runePoolPayload,
            "powerByTrait",
            payloadLabel,
            "power trait",
            errors);
    }

    private static void ValidateSpectatorSnapshotPlayerRunePoolPowerTraitPayloadPropertyNames(
        string playerId,
        object? runePoolPayload,
        List<string> errors)
    {
        if (!TryReadObjectValue(runePoolPayload, "powerByTrait", out var powerByTraitPayload))
        {
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            powerByTraitPayload,
            $"spectator replay frame snapshot player {playerId} rune pool power by trait",
            errors);
    }

    private static void ValidateSpectatorSnapshotPlayerZonePayloads(
        string playerId,
        object? playerPayload,
        PlayerZones zones,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "zones", out var zonePayload)
            || IsNullSnapshotPayloadValue(zonePayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} zones are required");
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(zonePayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} zones payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            zonePayload,
            $"spectator replay frame snapshot player {playerId} zones",
            errors);
        ValidateSpectatorSnapshotPlayerZonePayloadValues(playerId, zonePayload, errors);

        if (!TryReadObjectInt(zonePayload, "mainDeckCount", out var mainDeckCount)
            || mainDeckCount != zones.MainDeck.Count)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} main deck count does not match authoritative state main deck count; {FormatExpectedActualForRecovery(zones.MainDeck.Count, FormatReadableIntValue(mainDeckCount, zonePayload, "mainDeckCount"))}");
        }

        if (!TryReadObjectInt(zonePayload, "runeDeckCount", out var runeDeckCount)
            || runeDeckCount != zones.RuneDeck.Count)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} rune deck count does not match authoritative state rune deck count; {FormatExpectedActualForRecovery(zones.RuneDeck.Count, FormatReadableIntValue(runeDeckCount, zonePayload, "runeDeckCount"))}");
        }

        var publicHand = zones.Hand.Where(id => CoreRuleEngine.IsPubliclyRevealedHandCard(authoritativeState, id)).Order(StringComparer.Ordinal).ToArray();
        var hasHandObjects = TryReadObjectStringList(zonePayload, "hand", out var handObjects);
        if (!hasHandObjects || !handObjects.SequenceEqual(publicHand))
            errors.Add($"spectator replay frame snapshot player {playerId} hand objects must be redacted for spectator view; {FormatExpectedActualForRecovery(publicHand, FormatReadableStringListValue(hasHandObjects, handObjects, zonePayload, "hand"))}");
        if (!TryReadObjectInt(zonePayload, "handHidden", out var handHidden) || handHidden != zones.Hand.Count - publicHand.Length)
            errors.Add($"spectator replay frame snapshot player {playerId} hidden hand count does not match authoritative state hand count; {FormatExpectedActualForRecovery(zones.Hand.Count - publicHand.Length, FormatReadableIntValue(handHidden, zonePayload, "handHidden"))}");

        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "base",
            zones.Base,
            "base objects",
            errors);
        ValidateSpectatorSnapshotPlayerOptionalZoneStringList(
            playerId,
            zonePayload,
            "baseCards",
            BaseCardObjectIds(authoritativeState, zones),
            "base card objects",
            errors);
        ValidateSpectatorSnapshotPlayerOptionalZoneStringList(
            playerId,
            zonePayload,
            "baseRunes",
            BaseRuneObjectIds(authoritativeState, zones),
            "base rune objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "battlefields",
            zones.Battlefields
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId))
                .ToArray(),
            "battlefield objects",
            errors);

        var expectedHiddenStandbyCount = zones.Battlefields.Count(objectId =>
            IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId));
        if (!TryReadObjectInt(zonePayload, "battlefieldHiddenStandbyCount", out var hiddenStandbyCount)
            || hiddenStandbyCount != expectedHiddenStandbyCount)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} hidden battlefield standby count does not match authoritative state hidden battlefield standby count; {FormatExpectedActualForRecovery(expectedHiddenStandbyCount, FormatReadableIntValue(hiddenStandbyCount, zonePayload, "battlefieldHiddenStandbyCount"))}");
        }

        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "graveyard",
            zones.Graveyard,
            "graveyard objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "banished",
            zones.Banished,
            "banished objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "legendZone",
            zones.LegendZone,
            "legend zone objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "championZone",
            zones.ChampionZone,
            "champion zone objects",
            errors);
    }

    private static void ValidateSpectatorSnapshotPlayerZonePayloadValues(
        string playerId,
        object? zonePayload,
        List<string> errors)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId} zones";
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonePayload,
            "mainDeckCount",
            payloadLabel,
            "main deck count",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonePayload,
            "runeDeckCount",
            payloadLabel,
            "rune deck count",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            zonePayload,
            payloadLabel,
            "hand",
            "hand object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonePayload,
            "hand",
            payloadLabel,
            "hand object",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonePayload,
            "handHidden",
            payloadLabel,
            "hidden hand count",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            zonePayload,
            payloadLabel,
            "base",
            "base object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonePayload,
            "base",
            payloadLabel,
            "base object",
            errors);
        ValidateSnapshotPayloadOptionalStringListPayloadShape(
            zonePayload,
            "baseCards",
            payloadLabel,
            "base card object",
            errors);
        ValidateSnapshotPayloadOptionalStringListValues(
            zonePayload,
            "baseCards",
            payloadLabel,
            "base card object",
            errors);
        ValidateSnapshotPayloadOptionalStringListPayloadShape(
            zonePayload,
            "baseRunes",
            payloadLabel,
            "base rune object",
            errors);
        ValidateSnapshotPayloadOptionalStringListValues(
            zonePayload,
            "baseRunes",
            payloadLabel,
            "base rune object",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            zonePayload,
            payloadLabel,
            "battlefields",
            "battlefield object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonePayload,
            "battlefields",
            payloadLabel,
            "battlefield object",
            errors);
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            zonePayload,
            "battlefieldHiddenStandbyCount",
            payloadLabel,
            "hidden battlefield standby count",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            zonePayload,
            payloadLabel,
            "graveyard",
            "graveyard object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonePayload,
            "graveyard",
            payloadLabel,
            "graveyard object",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            zonePayload,
            payloadLabel,
            "banished",
            "banished object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonePayload,
            "banished",
            payloadLabel,
            "banished object",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            zonePayload,
            payloadLabel,
            "legendZone",
            "legend zone object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonePayload,
            "legendZone",
            payloadLabel,
            "legend zone object",
            errors);
        ValidateSpectatorRequiredStringListPayloadShape(
            zonePayload,
            payloadLabel,
            "championZone",
            "champion zone object",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            zonePayload,
            "championZone",
            payloadLabel,
            "champion zone object",
            errors);
    }

    private static void ValidateSpectatorSnapshotPlayerZoneStringList(
        string playerId,
        object? zonePayload,
        string key,
        IReadOnlyList<string> expected,
        string description,
        List<string> errors)
    {
        var hasValue = TryReadObjectStringList(zonePayload, key, out var value);
        if (!hasValue
            || !StringListsEqual(value, expected))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} {description} do not match authoritative state {description}; {FormatExpectedActualForRecovery(expected, FormatReadableStringListValue(hasValue, value, zonePayload, key))}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerOptionalZoneStringList(
        string playerId,
        object? zonePayload,
        string key,
        IReadOnlyList<string> expected,
        string description,
        List<string> errors)
    {
        if (!TryReadObjectValue(zonePayload, key, out var rawValue)
            || IsNullSnapshotPayloadValue(rawValue))
        {
            return;
        }

        var hasValue = TryReadObjectStringList(zonePayload, key, out var value);
        if (!hasValue
            || !StringListsEqual(value, expected))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} {description} do not match authoritative state {description}; {FormatExpectedActualForRecovery(expected, FormatReadableStringListValue(hasValue, value, zonePayload, key))}");
        }
    }

    private static IReadOnlyList<string> BaseRuneObjectIds(MatchState state, PlayerZones zones)
    {
        return zones.Base
            .Where(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal))
            .ToArray();
    }

    private static IReadOnlyList<string> BaseCardObjectIds(MatchState state, PlayerZones zones)
    {
        var runeSet = BaseRuneObjectIds(state, zones).ToHashSet(StringComparer.Ordinal);
        return zones.Base
            .Where(objectId => !runeSet.Contains(objectId))
            .ToArray();
    }

    private static void ValidateSpectatorSnapshotPlayerObjectPayloads(
        string playerId,
        object? playerPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "objects", out var objectsPayload)
            || IsNullSnapshotPayloadValue(objectsPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} objects are required");
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(objectsPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} objects payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            objectsPayload,
            $"spectator replay frame snapshot player {playerId} objects",
            errors);

        if (!TryReadObjectDictionaryValue(objectsPayload, out var objectPayloads))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} objects payload is required");
            return;
        }

        var expectedObjectIds = ExpectedSpectatorPlayerObjectIds(authoritativeState, playerId);
        var expectedObjectIdSet = expectedObjectIds.ToHashSet(StringComparer.Ordinal);
        foreach (var expectedObjectId in expectedObjectIds)
        {
            if (!objectPayloads.ContainsKey(expectedObjectId))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} is missing visible object {expectedObjectId}");
            }
        }

        foreach (var objectId in objectPayloads.Keys)
        {
            if (!expectedObjectIdSet.Contains(objectId))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} is not visible in authoritative spectator view");
            }
        }

        ValidateSpectatorSnapshotExtraPlayerObjectPayloadShapes(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectPayloadPropertyNames(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectPayloadValues(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectStringScalarPayloadValues(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectNumericScalarPayloadValues(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectBooleanScalarPayloadValues(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectListScalarPayloadValues(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectLocationPayloadValues(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            authoritativeState,
            errors);
        ValidateSpectatorSnapshotExtraPlayerObjectRedaction(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            authoritativeState,
            errors);
        ValidateSpectatorSnapshotExtraVisiblePlayerObjectScalarParity(
            playerId,
            objectPayloads,
            expectedObjectIdSet,
            authoritativeState,
            errors);

        foreach (var expectedObjectId in expectedObjectIds)
        {
            if (!objectPayloads.TryGetValue(expectedObjectId, out var objectPayload))
            {
                continue;
            }

            ValidateSpectatorSnapshotPlayerObjectPayload(
                playerId,
                expectedObjectId,
                objectPayload,
                authoritativeState,
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotExtraPlayerObjectPayloadShapes(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId))
            {
                continue;
            }

            if (!IsSnapshotPlayerPayloadObject(objectPayload))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} payload is required");
            }
        }
    }

    private static void ValidateSpectatorSnapshotExtraPlayerObjectPayloadPropertyNames(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId) || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                objectPayload,
                $"spectator replay frame snapshot player {playerId} object {objectId}",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotExtraPlayerObjectPayloadValues(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId) || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
            var payloadObjectId = ValidateSnapshotPayloadRequiredStringValue(
                objectPayload,
                "objectId",
                payloadLabel,
                "object id",
                errors);
            if (payloadObjectId is null)
            {
                errors.Add(
                    $"spectator replay frame snapshot player {playerId} object {objectId} payload object id does not match object key");
            }
            else if (!string.Equals(payloadObjectId, objectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot player {playerId} object {objectId} payload object id {payloadObjectId} does not match object key");
            }

            ValidateSnapshotPayloadRequiredBoolValue(
                objectPayload,
                "isFaceDown",
                payloadLabel,
                "face-down flag",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotExtraPlayerObjectStringScalarPayloadValues(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId) || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
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

    private static void ValidateSpectatorSnapshotExtraPlayerObjectNumericScalarPayloadValues(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId) || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
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

    private static void ValidateSpectatorSnapshotExtraPlayerObjectBooleanScalarPayloadValues(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId) || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
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

    private static void ValidateSpectatorSnapshotExtraPlayerObjectListScalarPayloadValues(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId) || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
            ValidateSpectatorRequiredStringListPayloadShape(
                objectPayload,
                payloadLabel,
                "tags",
                "tag",
                errors);
            ValidateSnapshotPayloadStringListValues(
                objectPayload,
                "tags",
                payloadLabel,
                "tag",
                errors);
            ValidateSpectatorRequiredStringListPayloadShape(
                objectPayload,
                payloadLabel,
                "untilEndOfTurnEffects",
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

    private static void ValidateSpectatorSnapshotExtraPlayerObjectLocationPayloadValues(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        MatchState authoritativeState,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId) || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId} location";
            var expectedLocation = ExpectedSpectatorObjectLocation(authoritativeState, objectId);
            var hasAuthoritativeObject = authoritativeState.CardObjects.ContainsKey(objectId);
            if (!TryReadObjectValue(objectPayload, "location", out var locationPayload)
                || IsNullSnapshotPayloadValue(locationPayload))
            {
                if (expectedLocation is not null)
                {
                    errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location is required");
                    errors.Add(
                        $"spectator replay frame snapshot player {playerId} object {objectId} location does not match authoritative object location; {FormatExpectedActualForRecovery(FormatObjectLocationValue(expectedLocation), FormatUnreadableObjectValue(objectPayload, "location"))}");
                }

                continue;
            }

            if (!IsSnapshotPlayerPayloadObject(locationPayload))
            {
                errors.Add($"{payloadLabel} payload is required");
                if (expectedLocation is not null)
                {
                    errors.Add(
                        $"spectator replay frame snapshot player {playerId} object {objectId} location does not match authoritative object location; {FormatExpectedActualForRecovery(FormatObjectLocationValue(expectedLocation), FormatUnreadableObjectValue(objectPayload, "location"))}");
                }

                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(locationPayload, payloadLabel, errors);
            var locationPlayerId = ValidateSnapshotPayloadRequiredStringValue(
                locationPayload,
                "playerId",
                payloadLabel,
                "player id",
                errors);
            if (expectedLocation is not null)
            {
                if (locationPlayerId is null)
                {
                    errors.Add(
                        $"spectator replay frame snapshot player {playerId} object {objectId} location player id does not match authoritative object location player id; {FormatExpectedActualForRecovery(expectedLocation.PlayerId, FormatReadableStringValue(false, locationPlayerId, locationPayload, "playerId"))}");
                }
                else if (!string.Equals(locationPlayerId, expectedLocation.PlayerId, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"spectator replay frame snapshot player {playerId} object {objectId} location player id does not match authoritative object location player id; {FormatExpectedActualForRecovery(expectedLocation.PlayerId, FormatReadableStringValue(true, locationPlayerId, locationPayload, "playerId"))}");
                }
            }

            var zone = ValidateSnapshotPayloadRequiredStringValue(
                locationPayload,
                "zone",
                payloadLabel,
                "zone",
                errors,
                IsKnownObjectLocationZone);
            if (expectedLocation is not null)
            {
                if (zone is null)
                {
                    errors.Add(
                        $"spectator replay frame snapshot player {playerId} object {objectId} location zone does not match authoritative object location zone; {FormatExpectedActualForRecovery(expectedLocation.Zone, FormatReadableStringValue(false, zone, locationPayload, "zone"))}");
                }
                else if (!string.Equals(zone, expectedLocation.Zone, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"spectator replay frame snapshot player {playerId} object {objectId} location zone does not match authoritative object location zone; {FormatExpectedActualForRecovery(expectedLocation.Zone, FormatReadableStringValue(true, zone, locationPayload, "zone"))}");
                }
            }

            var hasBattlefieldObjectId = TryReadObjectValue(locationPayload, "battlefieldObjectId", out var battlefieldObjectIdPayload)
                && !IsNullSnapshotPayloadValue(battlefieldObjectIdPayload);
            var battlefieldObjectId = ValidateSnapshotPayloadOptionalStringValue(
                locationPayload,
                "battlefieldObjectId",
                payloadLabel,
                "battlefield object id",
                errors);
            if (!hasBattlefieldObjectId)
            {
                battlefieldObjectId = string.Empty;
            }

            if (expectedLocation is not null)
            {
                ValidateSpectatorSnapshotObjectLocationBattlefieldObjectIdParity(
                    playerId,
                    objectId,
                    hasBattlefieldObjectId,
                    battlefieldObjectId,
                    FormatReadableStringValue(hasBattlefieldObjectId, battlefieldObjectId, locationPayload, "battlefieldObjectId"),
                    expectedLocation,
                    errors);
                ValidateSpectatorSnapshotObjectLocationZoneHintParity(
                    playerId,
                    objectId,
                    locationPayload,
                    authoritativeState,
                    expectedLocation,
                    errors);
            }

            if (expectedLocation is null && hasAuthoritativeObject)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location must be absent without authoritative object location");
            }
        }
    }

    private static void ValidateSpectatorSnapshotExtraPlayerObjectRedaction(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        MatchState authoritativeState,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId)
                || !IsSnapshotPlayerPayloadObject(objectPayload))
            {
                continue;
            }

            var requiresFaceDownRedaction = IsHiddenPlayerObjectForSpectator(authoritativeState, objectId);
            var hasAuthoritativeObject = authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject is not null;
            var expectedFaceDown = requiresFaceDownRedaction || cardObject?.IsFaceDown == true;
            var hasValidFaceDownFlag = ValidateSpectatorSnapshotPlayerObjectFaceDownRedactionParity(
                playerId,
                objectId,
                objectPayload,
                shouldValidateParity: requiresFaceDownRedaction || hasAuthoritativeObject,
                expectedFaceDown,
                errors,
                out var isFaceDown);

            if ((expectedFaceDown || (hasValidFaceDownFlag && isFaceDown))
                && SpectatorFaceDownObjectExposesPrivateMetadata(objectPayload))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} hidden face-down object {objectId} exposes private metadata");
            }
        }
    }

    private static bool SpectatorFaceDownObjectExposesPrivateMetadata(object? objectPayload)
    {
        return TryReadObjectValue(objectPayload, "damage", out _)
            || TryReadObjectValue(objectPayload, "power", out _)
            || TryReadObjectValue(objectPayload, "basePower", out _)
            || TryReadObjectValue(objectPayload, "effectivePower", out _)
            || TryReadObjectValue(objectPayload, "untilEndOfTurnPowerModifier", out _)
            || TryReadObjectValue(objectPayload, "isExhausted", out _)
            || TryReadObjectValue(objectPayload, "isAttacking", out _)
            || TryReadObjectValue(objectPayload, "isDefending", out _)
            || TryReadObjectValue(objectPayload, "tags", out _)
            || TryReadObjectValue(objectPayload, "untilEndOfTurnEffects", out _)
            || TryReadObjectValue(objectPayload, "manaCost", out _)
            || TryReadObjectValue(objectPayload, "attachedToObjectId", out _)
            || TryReadObjectValue(objectPayload, "cardNo", out _)
            || TryReadObjectValue(objectPayload, "ownerId", out _)
            || TryReadObjectValue(objectPayload, "controllerId", out _);
    }

    private static void ValidateSpectatorSnapshotExtraVisiblePlayerObjectScalarParity(
        string playerId,
        IReadOnlyDictionary<string, object?> objectPayloads,
        IReadOnlySet<string> expectedObjectIds,
        MatchState authoritativeState,
        List<string> errors)
    {
        foreach (var (objectId, objectPayload) in objectPayloads)
        {
            if (expectedObjectIds.Contains(objectId)
                || !IsSnapshotPlayerPayloadObject(objectPayload)
                || !authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject)
                || cardObject.IsFaceDown
                || IsHiddenPlayerObjectForSpectator(authoritativeState, objectId))
            {
                continue;
            }

            ValidateSpectatorSnapshotVisiblePlayerObjectScalars(
                playerId,
                objectId,
                objectPayload,
                cardObject,
                authoritativeState,
                errors);
        }
    }

    private static bool IsHiddenPlayerObjectForSpectator(MatchState authoritativeState, string objectId)
    {
        var expectedLocation = ExpectedSpectatorObjectLocation(authoritativeState, objectId);
        return expectedLocation is not null
            && (string.Equals(expectedLocation.Zone, "MAIN_DECK", StringComparison.Ordinal)
                || string.Equals(expectedLocation.Zone, "RUNE_DECK", StringComparison.Ordinal)
                || string.Equals(expectedLocation.Zone, "HAND", StringComparison.Ordinal)
                || IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId));
    }

    private static IReadOnlyList<string> ExpectedSpectatorPlayerObjectIds(
        MatchState authoritativeState,
        string playerId)
    {
        var zones = authoritativeState.PlayerZones.TryGetValue(playerId, out var playerZones)
            ? playerZones
            : PlayerZones.Empty;
        return zones.Hand.Where(id => CoreRuleEngine.IsPubliclyRevealedHandCard(authoritativeState, id)).Order(StringComparer.Ordinal)
            .Concat(zones.MainDeck.Where(id => CoreRuleEngine.IsPubliclyRevealedDeckCard(authoritativeState, id))).Concat(zones.Base)
            .Concat(zones.Battlefields.Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId)))
            .Concat(zones.Graveyard)
            .Concat(zones.Banished)
            .Concat(zones.LegendZone)
            .Concat(zones.ChampionZone)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => authoritativeState.CardObjects.ContainsKey(objectId))
            .ToArray();
    }

    private static void ValidateSpectatorSnapshotPlayerObjectPayload(
        string playerId,
        string objectId,
        object? objectPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!IsSnapshotPlayerPayloadObject(objectPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} payload is required");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            objectPayload,
            $"spectator replay frame snapshot player {playerId} object {objectId}",
            errors);

        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
        var payloadObjectId = ValidateSnapshotPayloadRequiredStringValue(
            objectPayload,
            "objectId",
            payloadLabel,
            "object id",
            errors);
        if (payloadObjectId is null)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} object id does not match authoritative object id");
        }
        else if (!string.Equals(payloadObjectId, objectId, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} object id does not match authoritative object id");
        }

        var hasAuthoritativeObject = authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject);
        var expectedFaceDown = hasAuthoritativeObject && cardObject is not null && cardObject.IsFaceDown;
        ValidateSnapshotPayloadRequiredBoolValue(
            objectPayload,
            "isFaceDown",
            payloadLabel,
            "face-down flag",
            errors);
        var hasValidFaceDownFlag = ValidateSpectatorSnapshotPlayerObjectFaceDownRedactionParity(
            playerId,
            objectId,
            objectPayload,
            shouldValidateParity: true,
            expectedFaceDown,
            errors,
            out var isFaceDown);

        if (!hasAuthoritativeObject || cardObject is null)
        {
            return;
        }

        ValidateSpectatorSnapshotPlayerObjectLocation(playerId, objectId, objectPayload, authoritativeState, errors);

        if (!expectedFaceDown)
        {
            ValidateSpectatorSnapshotVisiblePlayerObjectScalars(playerId, objectId, objectPayload, cardObject, authoritativeState, errors);
            if (hasValidFaceDownFlag
                && isFaceDown
                && SpectatorFaceDownObjectExposesPrivateMetadata(objectPayload))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} hidden face-down object {objectId} exposes private metadata");
            }

            return;
        }

        if (SpectatorFaceDownObjectExposesPrivateMetadata(objectPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} hidden face-down object {objectId} exposes private metadata");
        }
    }

    private static bool ValidateSpectatorSnapshotPlayerObjectFaceDownRedactionParity(
        string playerId,
        string objectId,
        object? objectPayload,
        bool shouldValidateParity,
        bool expectedFaceDown,
        List<string> errors,
        out bool isFaceDown)
    {
        var hasValidFaceDownFlag = TryReadObjectBool(objectPayload, "isFaceDown", out isFaceDown);
        if (shouldValidateParity && (!hasValidFaceDownFlag || isFaceDown != expectedFaceDown))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} face-down flag does not match authoritative spectator redaction");
        }

        return hasValidFaceDownFlag;
    }

    private static void ValidateSpectatorSnapshotPlayerObjectLocation(
        string playerId,
        string objectId,
        object? objectPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        var expectedLocation = ExpectedSpectatorObjectLocation(authoritativeState, objectId);
        if (expectedLocation is null)
        {
            if (TryReadObjectValue(objectPayload, "location", out _))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location must be absent without authoritative object location");
            }

            return;
        }

        if (!TryReadObjectValue(objectPayload, "location", out var locationPayload)
            || IsNullSnapshotPayloadValue(locationPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location is required");
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} location does not match authoritative object location; {FormatExpectedActualForRecovery(FormatObjectLocationValue(expectedLocation), FormatUnreadableObjectValue(objectPayload, "location"))}");
            return;
        }

        if (!IsSnapshotPlayerPayloadObject(locationPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location payload is required");
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} location does not match authoritative object location; {FormatExpectedActualForRecovery(FormatObjectLocationValue(expectedLocation), FormatUnreadableObjectValue(objectPayload, "location"))}");
            return;
        }

        ValidateSnapshotPayloadObjectPropertyNames(
            locationPayload,
            $"spectator replay frame snapshot player {playerId} object {objectId} location",
            errors);

        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId} location";
        var locationPlayerId = ValidateSnapshotPayloadRequiredStringValue(
            locationPayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);
        if (locationPlayerId is null)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} location player id does not match authoritative object location player id; {FormatExpectedActualForRecovery(expectedLocation.PlayerId, FormatReadableStringValue(false, locationPlayerId, locationPayload, "playerId"))}");
        }
        else if (!string.Equals(locationPlayerId, expectedLocation.PlayerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} location player id does not match authoritative object location player id; {FormatExpectedActualForRecovery(expectedLocation.PlayerId, FormatReadableStringValue(true, locationPlayerId, locationPayload, "playerId"))}");
        }

        var zone = ValidateSnapshotPayloadRequiredStringValue(
            locationPayload,
            "zone",
            payloadLabel,
            "zone",
            errors,
            IsKnownObjectLocationZone);
        if (zone is null)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} location zone does not match authoritative object location zone; {FormatExpectedActualForRecovery(expectedLocation.Zone, FormatReadableStringValue(false, zone, locationPayload, "zone"))}");
        }
        else if (!string.Equals(zone, expectedLocation.Zone, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} location zone does not match authoritative object location zone; {FormatExpectedActualForRecovery(expectedLocation.Zone, FormatReadableStringValue(true, zone, locationPayload, "zone"))}");
        }

        var hasBattlefieldObjectId = TryReadObjectValue(locationPayload, "battlefieldObjectId", out var battlefieldObjectIdPayload)
            && !IsNullSnapshotPayloadValue(battlefieldObjectIdPayload);
        var battlefieldObjectId = ValidateSnapshotPayloadOptionalStringValue(
            locationPayload,
            "battlefieldObjectId",
            payloadLabel,
            "battlefield object id",
            errors);
        if (!hasBattlefieldObjectId)
        {
            battlefieldObjectId = string.Empty;
        }

        ValidateSpectatorSnapshotObjectLocationBattlefieldObjectIdParity(
            playerId,
            objectId,
            hasBattlefieldObjectId,
            battlefieldObjectId,
            FormatReadableStringValue(hasBattlefieldObjectId, battlefieldObjectId, locationPayload, "battlefieldObjectId"),
            expectedLocation,
            errors);
        ValidateSpectatorSnapshotObjectLocationZoneHintParity(
            playerId,
            objectId,
            locationPayload,
            authoritativeState,
            expectedLocation,
            errors);
    }

    private static void ValidateSpectatorSnapshotObjectLocationBattlefieldObjectIdParity(
        string playerId,
        string objectId,
        bool hasBattlefieldObjectId,
        string? battlefieldObjectId,
        object actualBattlefieldObjectId,
        ObjectLocationState expectedLocation,
        List<string> errors)
    {
        var expectedBattlefieldObjectId = expectedLocation.BattlefieldObjectId ?? string.Empty;
        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId} location battlefield object id";
        if (string.IsNullOrEmpty(expectedBattlefieldObjectId))
        {
            if (battlefieldObjectId is not null
                && !string.Equals(battlefieldObjectId, expectedBattlefieldObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"{payloadLabel} does not match authoritative object location battlefield object id; {FormatExpectedActualForRecovery(expectedBattlefieldObjectId, actualBattlefieldObjectId)}");
            }

            return;
        }

        if (!hasBattlefieldObjectId || battlefieldObjectId is { Length: 0 })
        {
            errors.Add($"{payloadLabel} is required");
            errors.Add(
                $"{payloadLabel} does not match authoritative object location battlefield object id; {FormatExpectedActualForRecovery(expectedBattlefieldObjectId, actualBattlefieldObjectId)}");
            return;
        }

        if (battlefieldObjectId is null)
        {
            errors.Add(
                $"{payloadLabel} does not match authoritative object location battlefield object id; {FormatExpectedActualForRecovery(expectedBattlefieldObjectId, actualBattlefieldObjectId)}");
            return;
        }

        if (!string.Equals(battlefieldObjectId, expectedBattlefieldObjectId, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} does not match authoritative object location battlefield object id; {FormatExpectedActualForRecovery(expectedBattlefieldObjectId, actualBattlefieldObjectId)}");
        }
    }

    private static void ValidateSpectatorSnapshotObjectLocationZoneHintParity(
        string playerId,
        string objectId,
        object? locationPayload,
        MatchState authoritativeState,
        ObjectLocationState expectedLocation,
        List<string> errors)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId} location";
        var hasZoneKind = TryReadObjectValue(locationPayload, "zoneKind", out var zoneKindPayload)
            && !IsNullSnapshotPayloadValue(zoneKindPayload);
        var zoneKind = ValidateSnapshotPayloadOptionalStringValue(
            locationPayload,
            "zoneKind",
            payloadLabel,
            "zone kind",
            errors,
            ObjectLocationSnapshotZones.IsKnownZoneKind,
            rejectEmptyString: true);

        var hasZoneLabel = TryReadObjectValue(locationPayload, "zoneLabel", out var zoneLabelPayload)
            && !IsNullSnapshotPayloadValue(zoneLabelPayload);
        var zoneLabel = ValidateSnapshotPayloadOptionalStringValue(
            locationPayload,
            "zoneLabel",
            payloadLabel,
            "zone label",
            errors,
            rejectEmptyString: true);

        if (!hasZoneKind && !hasZoneLabel)
        {
            return;
        }

        var expectedHint = ExpectedSpectatorObjectLocationZoneHint(
            authoritativeState,
            objectId,
            expectedLocation);
        if (hasZoneKind
            && zoneKind is not null
            && !string.Equals(zoneKind, expectedHint.ZoneKind, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} zone kind does not match authoritative object location zone kind; {FormatExpectedActualForRecovery(expectedHint.ZoneKind, FormatReadableStringValue(true, zoneKind, locationPayload, "zoneKind"))}");
        }

        if (hasZoneLabel
            && zoneLabel is not null
            && !string.Equals(zoneLabel, expectedHint.ZoneLabel, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} zone label does not match authoritative object location zone label; {FormatExpectedActualForRecovery(expectedHint.ZoneLabel, FormatReadableStringValue(true, zoneLabel, locationPayload, "zoneLabel"))}");
        }
    }

    private static ObjectLocationState? ExpectedSpectatorObjectLocation(
        MatchState authoritativeState,
        string objectId)
    {
        if (authoritativeState.ObjectLocations.TryGetValue(objectId, out var location))
        {
            return location;
        }

        foreach (var (playerId, zones) in authoritativeState.PlayerZones)
        {
            if (zones.MainDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "MAIN_DECK");
            }

            if (zones.RuneDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "RUNE_DECK");
            }

            if (zones.Hand.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "HAND");
            }

            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BASE");
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BATTLEFIELD");
            }

            if (zones.Graveyard.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "GRAVEYARD");
            }

            if (zones.Banished.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BANISHED");
            }

            if (zones.LegendZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "LEGEND");
            }

            if (zones.ChampionZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "CHAMPION");
            }
        }

        return null;
    }

    private static (string ZoneKind, string ZoneLabel) ExpectedSpectatorObjectLocationZoneHint(
        MatchState authoritativeState,
        string objectId,
        ObjectLocationState location)
    {
        var includeObjectClassHints = !IsHiddenPlayerObjectForSpectator(authoritativeState, objectId);
        var cardObject = authoritativeState.CardObjects.TryGetValue(objectId, out var candidate)
            ? candidate
            : null;
        return ObjectLocationSnapshotZones.Describe(location, cardObject, includeObjectClassHints);
    }

    private static void ValidateSpectatorSnapshotPlayerObjectOptionalStringScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        string? expected,
        string description,
        List<string> errors)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
        var hasScalar = TryReadObjectValue(objectPayload, key, out var rawValue)
            && !IsNullSnapshotPayloadValue(rawValue);
        var value = ValidateSnapshotPayloadOptionalStringValue(
            objectPayload,
            key,
            payloadLabel,
            description,
            errors);
        var expectedText = expected ?? string.Empty;
        if (!string.IsNullOrEmpty(expectedText)
            && (!hasScalar || value is { Length: 0 }))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} is required");
        }

        if (!hasScalar)
        {
            value = string.Empty;
        }

        if (value is null)
        {
            if (!string.IsNullOrEmpty(expectedText))
            {
                errors.Add(
                    $"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}; {FormatExpectedActualForRecovery(expectedText, FormatReadableStringValue(hasScalar, value, objectPayload, key))}");
            }

            return;
        }

        if (!string.Equals(value, expectedText, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}; {FormatExpectedActualForRecovery(expectedText, FormatReadableStringValue(hasScalar, value, objectPayload, key))}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectIntScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        int expected,
        string description,
        List<string> errors,
        bool mustBeNonNegative = false)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
        var hasScalar = TryReadObjectValue(objectPayload, key, out var rawValue)
            && !IsNullSnapshotPayloadValue(rawValue);
        var value = mustBeNonNegative
            ? ValidateSnapshotPayloadOptionalNonNegativeIntValue(
                objectPayload,
                key,
                payloadLabel,
                description,
                errors)
            : ValidateSnapshotPayloadOptionalIntValue(
                objectPayload,
                key,
                payloadLabel,
                description,
                errors);
        if (!hasScalar)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} is required");
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}; {FormatExpectedActualForRecovery(expected, FormatReadableIntValue(value, objectPayload, key))}");
            return;
        }

        if (value is null || value != expected)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}; {FormatExpectedActualForRecovery(expected, FormatReadableIntValue(value, objectPayload, key))}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectBoolScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        bool expected,
        string description,
        List<string> errors)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
        var hasScalar = TryReadObjectValue(objectPayload, key, out var rawValue)
            && !IsNullSnapshotPayloadValue(rawValue);
        ValidateSnapshotPayloadOptionalBoolValue(
            objectPayload,
            key,
            payloadLabel,
            description,
            errors);
        var hasReadableBool = TryReadObjectBool(objectPayload, key, out var value);
        if (!hasScalar)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} is required");
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}; {FormatExpectedActualForRecovery(expected, FormatReadableBoolValue(hasReadableBool, value, objectPayload, key))}");
            return;
        }

        if (!hasReadableBool || value != expected)
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}; {FormatExpectedActualForRecovery(expected, FormatReadableBoolValue(hasReadableBool, value, objectPayload, key))}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectStringListScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        IReadOnlyList<string> expected,
        string description,
        string listItemDescription,
        List<string> errors)
    {
        var payloadLabel = $"spectator replay frame snapshot player {playerId} object {objectId}";
        var hasScalar = TryReadObjectValue(objectPayload, key, out var rawValue)
            && !IsNullSnapshotPayloadValue(rawValue);
        ValidateSpectatorRequiredStringListPayloadShape(
            objectPayload,
            payloadLabel,
            key,
            listItemDescription,
            errors);
        ValidateSnapshotPayloadStringListValues(
            objectPayload,
            key,
            payloadLabel,
            listItemDescription,
            errors);
        if (!hasScalar)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} are required");
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} {description} do not match authoritative object {description}; {FormatExpectedActualForRecovery(expected, FormatReadableStringListValue(false, [], objectPayload, key))}");
            return;
        }

        var hasReadableList = TryReadObjectStringList(objectPayload, key, out var value);
        if (!hasReadableList || !StringListsEqual(value, expected))
        {
            errors.Add(
                $"spectator replay frame snapshot player {playerId} object {objectId} {description} do not match authoritative object {description}; {FormatExpectedActualForRecovery(expected, FormatReadableStringListValue(hasReadableList, value, objectPayload, key))}");
        }
    }

    private static void ValidateSpectatorSnapshotLanePayloads(
        SnapshotDto snapshot,
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateSnapshotPayloadObjectPropertyNames(
            snapshot.Lanes,
            "spectator replay frame snapshot lanes",
            errors);

        var expectedBattlefieldObjectPairs = ExpectedSpectatorLaneBattlefieldObjectPairs(authoritativeState);
        var battlefieldCount = ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            snapshot.Lanes,
            "battlefieldCount",
            "spectator replay frame snapshot lanes",
            "battlefield count",
            errors);
        if (battlefieldCount is null || battlefieldCount != expectedBattlefieldObjectPairs.Count)
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield count {battlefieldCount.GetValueOrDefault()} does not match authoritative state battlefield object count {expectedBattlefieldObjectPairs.Count}; {FormatExpectedActualForRecovery(expectedBattlefieldObjectPairs.Count, battlefieldCount.GetValueOrDefault())}");
        }

        if (!TryReadObjectList(snapshot.Lanes, "battlefieldObjectIds", out var battlefieldObjectItems))
        {
            errors.Add("spectator replay frame snapshot lane battlefield object ids are required");
        }
        else
        {
            var knownPlayerIds = BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys);
            var expectedBattlefieldObjectIds = expectedBattlefieldObjectPairs
                .Select(pair => pair.ObjectId)
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .ToHashSet(StringComparer.Ordinal);
            var spectatorBattlefieldObjectPairs = new List<(string PlayerId, string ObjectId)>();
            var malformedPair = false;
            for (var index = 0; index < battlefieldObjectItems.Count; index++)
            {
                var item = battlefieldObjectItems[index];
                var itemLabel = $"spectator replay frame snapshot lane battlefield object id item {index + 1}";
                if (!IsSnapshotPlayerPayloadObject(item))
                {
                    errors.Add($"{itemLabel} payload is required");
                    malformedPair = true;
                    continue;
                }

                ValidateSnapshotPayloadObjectPropertyNames(
                    item,
                    itemLabel,
                    errors);

                var playerId = ValidateSnapshotPayloadRequiredStringValue(
                    item,
                    "playerId",
                    itemLabel,
                    "player id",
                    errors);
                var objectId = ValidateSnapshotPayloadRequiredStringValue(
                    item,
                    "objectId",
                    itemLabel,
                    "object id",
                    errors);
                if (playerId is null || objectId is null)
                {
                    malformedPair = true;
                    continue;
                }

                ValidateTimingPlayerReference(
                    $"{itemLabel} player id",
                    playerId,
                    knownPlayerIds,
                    "seats",
                    errors);
                ValidateTimingObjectReferenceWithExpectedDetails(
                    $"{itemLabel} object id",
                    objectId,
                    expectedBattlefieldObjectIds,
                    "authoritative state battlefield object ids",
                    errors);
                spectatorBattlefieldObjectPairs.Add((playerId, objectId));
            }

            if (malformedPair)
            {
                errors.Add("spectator replay frame snapshot lane battlefield object ids require player id and object id");
            }

            if (!BattlefieldObjectPairsEqual(spectatorBattlefieldObjectPairs, expectedBattlefieldObjectPairs))
            {
                errors.Add(
                    "spectator replay frame snapshot lane battlefield object ids disagree with authoritative state battlefield object ids"
                    + $"; {FormatExpectedActualForRecovery(
                        FormatBattlefieldObjectPairsForRecovery(expectedBattlefieldObjectPairs),
                        FormatBattlefieldObjectPairsForRecovery(spectatorBattlefieldObjectPairs))}");
            }
        }

        if (!TryReadObjectList(snapshot.Lanes, "battlefields", out var battlefieldItems))
        {
            errors.Add("spectator replay frame snapshot lane battlefields are required");
            return;
        }

        ValidateSpectatorSnapshotExtraBattlefieldPayloadShapes(battlefieldItems, authoritativeState, errors);

        var spectatorBattlefieldObjectIds = ExtractObjectStringValues(battlefieldItems, "battlefieldObjectId");
        var authoritativeBattlefieldObjectIds = authoritativeState.BattlefieldStates.Values
            .Select(battlefield => battlefield.BattlefieldObjectId)
            .ToArray();
        if (!StringListsEqual(spectatorBattlefieldObjectIds, authoritativeBattlefieldObjectIds))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefields disagree with authoritative state battlefields; {FormatExpectedActualForRecovery(authoritativeBattlefieldObjectIds, spectatorBattlefieldObjectIds)}");
        }

        ValidateSpectatorSnapshotBattlefieldPayloadPropertyNames(battlefieldItems, authoritativeState, errors);
        ValidateSpectatorSnapshotBattlefieldScalarPayloads(battlefieldItems, authoritativeState, errors);
        ValidateSpectatorSnapshotBattlefieldListPayloads(battlefieldItems, authoritativeState, errors);
        ValidateSpectatorSnapshotBattlefieldPlayerReferences(
            battlefieldItems,
            authoritativeState,
            errors);
        ValidateSpectatorSnapshotBattlefieldObjectReferences(
            battlefieldItems,
            authoritativeState,
            errors);
        ValidateSpectatorSnapshotStandbySlotPayloads(battlefieldItems, authoritativeState, errors);
    }

    private static void ValidateSpectatorSnapshotExtraBattlefieldPayloadShapes(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefieldCount = authoritativeState.BattlefieldStates.Count;
        for (var index = authoritativeBattlefieldCount; index < spectatorBattlefields.Count; index++)
        {
            if (!IsSnapshotPlayerPayloadObject(spectatorBattlefields[index]))
            {
                errors.Add($"spectator replay frame snapshot lane battlefield item {index + 1} payload is required");
            }
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldPayloadPropertyNames(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            if (!IsSnapshotPlayerPayloadObject(spectatorBattlefield))
            {
                continue;
            }

            var battlefieldObjectId = authoritativeBattlefields[index].BattlefieldObjectId;
            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorBattlefield,
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId}",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldScalarPayloads(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            var authoritativeBattlefield = authoritativeBattlefields[index];
            var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;
            var payloadLabel = $"spectator replay frame snapshot lane battlefield {battlefieldObjectId}";

            if (!IsSnapshotPlayerPayloadObject(spectatorBattlefield))
            {
                errors.Add($"{payloadLabel} payload is required");
                continue;
            }

            ValidateSpectatorSnapshotBattlefieldRequiredStringScalar(
                spectatorBattlefield,
                "battlefieldObjectId",
                authoritativeBattlefield.BattlefieldObjectId,
                payloadLabel,
                "battlefield object id",
                "authoritative state battlefield object id",
                errors);
            ValidateSpectatorSnapshotBattlefieldRequiredStringScalar(
                spectatorBattlefield,
                "zonePlayerId",
                authoritativeBattlefield.ZonePlayerId,
                payloadLabel,
                "zone player",
                "authoritative state zone player",
                errors);
            ValidateSpectatorSnapshotBattlefieldOptionalStringScalar(
                spectatorBattlefield,
                "cardNo",
                authoritativeBattlefield.CardNo,
                payloadLabel,
                "card number",
                "authoritative state card number",
                errors);
            ValidateSpectatorSnapshotBattlefieldOptionalStringScalar(
                spectatorBattlefield,
                "controllerId",
                authoritativeBattlefield.ControllerId,
                payloadLabel,
                "controller",
                "authoritative state controller",
                errors);
            ValidateSpectatorSnapshotBattlefieldRequiredStringScalar(
                spectatorBattlefield,
                "status",
                authoritativeBattlefield.Status,
                payloadLabel,
                "status",
                "authoritative state status",
                errors,
                IsKnownBattlefieldStatus);
            ValidateSpectatorSnapshotBattlefieldBoolScalar(
                spectatorBattlefield,
                "contested",
                authoritativeBattlefield.Contested,
                payloadLabel,
                "contested",
                "authoritative state contested",
                errors);
            ValidateSpectatorSnapshotBattlefieldNonNegativeIntScalar(
                spectatorBattlefield,
                "standbySlotCount",
                authoritativeBattlefield.StandbyObjectIds.Count,
                payloadLabel,
                "standby slot count",
                "authoritative state standby slot count",
                errors);
            ValidateSpectatorSnapshotBattlefieldNonNegativeIntScalar(
                spectatorBattlefield,
                "faceDownStandbyCount",
                authoritativeBattlefield.FaceDownStandbyCount,
                payloadLabel,
                "face-down standby count",
                "authoritative state face-down standby count",
                errors);

            var hiddenStandbyCount = authoritativeBattlefield.StandbyObjectIds.Count(objectId =>
                IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId));
            ValidateSpectatorSnapshotBattlefieldNonNegativeIntScalar(
                spectatorBattlefield,
                "hiddenStandbyCount",
                hiddenStandbyCount,
                payloadLabel,
                "hidden standby count",
                "authoritative state hidden standby count",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldRequiredStringScalar(
        object? battlefieldPayload,
        string key,
        string expected,
        string payloadLabel,
        string description,
        string mismatchDescription,
        List<string> errors,
        Func<string, bool>? isKnownValue = null)
    {
        var value = ValidateSnapshotPayloadRequiredStringValue(
            battlefieldPayload,
            key,
            payloadLabel,
            description,
            errors,
            isKnownValue);
        if (value is null || !string.Equals(value, expected, StringComparison.Ordinal))
        {
            errors.Add($"{payloadLabel} {description} does not match {mismatchDescription}");
        }
    }
}
