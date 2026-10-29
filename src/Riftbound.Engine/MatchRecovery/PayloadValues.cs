using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateSpectatorSnapshotStackKeyedOptionalStringValue(
        object? stackItem,
        string key,
        string fieldLabel,
        string? expected,
        string stackItemId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame snapshot stack item";
        var expectedText = expected ?? string.Empty;
        if (!TryReadObjectOptionalString(stackItem, key, out var actual))
        {
            if (!string.IsNullOrEmpty(expectedText))
            {
                errors.Add(
                    $"{payloadLabel} {fieldLabel} does not match authoritative state stack item {fieldLabel} {expectedText} for stack item id {stackItemId}");
            }

            return;
        }

        if (!string.Equals(actual, expectedText, StringComparison.Ordinal))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match authoritative state stack item {fieldLabel} {expectedText} for stack item id {stackItemId}");
        }
    }

    private static void ValidateSpectatorSnapshotStackKeyedRequiredStringListValue(
        object? stackItem,
        string key,
        string fieldLabel,
        IReadOnlyList<string> expected,
        string stackItemId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame snapshot stack item";
        if (!TryReadObjectStringList(stackItem, key, out var actual)
            || !StringListsEqual(actual, expected))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} do not match authoritative state stack item {fieldLabel} for stack item id {stackItemId}");
        }
    }

    private static void ValidateSpectatorSnapshotStackKeyedRequiredIntValue(
        object? stackItem,
        string key,
        string fieldLabel,
        int expected,
        string stackItemId,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame snapshot stack item";
        if (!TryReadObjectInt(stackItem, key, out var actual))
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} does not match authoritative state stack item {fieldLabel} {expected} for stack item id {stackItemId}");
            return;
        }

        if (actual != expected)
        {
            errors.Add(
                $"{payloadLabel} {fieldLabel} {actual} does not match authoritative state stack item {fieldLabel} {expected} for stack item id {stackItemId}");
        }
    }

    private static void ValidateSpectatorSnapshotStackAuthoritativeKeySet(
        IReadOnlyList<object?> stackItems,
        IReadOnlyList<StackItemState> authoritativeStackItems,
        List<string> errors)
    {
        var authoritativeStackItemIds = authoritativeStackItems
            .Select(item => item.StackItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var actualStackItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stackItem in stackItems)
        {
            if (!IsSnapshotPlayerPayloadObject(stackItem)
                || !TryReadObjectString(stackItem, "stackItemId", out var stackItemId)
                || string.IsNullOrWhiteSpace(stackItemId))
            {
                continue;
            }

            var normalizedStackItemId = stackItemId.Trim();
            actualStackItemIds.Add(normalizedStackItemId);
            if (!authoritativeStackItemIds.Contains(normalizedStackItemId))
            {
                errors.Add(
                    $"spectator replay frame snapshot stack item id {normalizedStackItemId} is not present in authoritative state stack items");
            }
        }

        foreach (var authoritativeStackItemId in authoritativeStackItemIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!actualStackItemIds.Contains(authoritativeStackItemId))
            {
                errors.Add(
                    $"spectator replay frame snapshot stack item id {authoritativeStackItemId} is required by authoritative state stack items");
            }
        }
    }

    private static string? ValidateSpectatorSnapshotStackItemPayloadValues(
        object? stackItem,
        string stackItemLabel,
        List<string> errors)
    {
        return ValidateStackItemPayloadValues(
            stackItem,
            $"spectator replay frame snapshot stack item {stackItemLabel}",
            errors);
    }

    private static IReadOnlyList<string> ExtractStackItemIds(SnapshotDto snapshot)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var stackItemIds = new List<string>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectString(item, "stackItemId", out var stackItemId)
                && !string.IsNullOrWhiteSpace(stackItemId))
            {
                stackItemIds.Add(stackItemId);
            }
        }

        return stackItemIds;
    }

    private static IReadOnlyList<string> ExtractStackItemStringValues(SnapshotDto snapshot, string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectString(item, key, out var value))
            {
                values.Add(value ?? string.Empty);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> ExtractStackItemOptionalStringValues(SnapshotDto snapshot, string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectOptionalString(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractStackItemStringListValues(
        SnapshotDto snapshot,
        string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<IReadOnlyList<string>>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectStringList(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<int> ExtractStackItemIntValues(SnapshotDto snapshot, string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<int>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectInt(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool TryReadSeat(object? player, out string seat)
    {
        if (player is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue("seat", out var readOnlySeat)
            && readOnlySeat is string readOnlySeatString)
        {
            seat = readOnlySeatString;
            return true;
        }

        if (player is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue("seat", out var dictionarySeat)
            && dictionarySeat is string dictionarySeatString)
        {
            seat = dictionarySeatString;
            return true;
        }

        if (player is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty("seat", out var jsonSeat)
            && jsonSeat.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(jsonSeat.GetString()))
        {
            seat = jsonSeat.GetString()!;
            return true;
        }

        seat = string.Empty;
        return false;
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out string? text)
    {
        if (!values.TryGetValue(key, out var value))
        {
            text = null;
            return false;
        }

        return TryReadStringValue(value, out text);
    }

    private static bool TryReadObjectList(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out IReadOnlyList<object?> items)
    {
        items = [];
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return TryReadObjectListValue(value, out items);
    }

    private static bool TryReadObjectList(
        object? value,
        string key,
        out IReadOnlyList<object?> items)
    {
        items = [];
        return TryReadObjectValue(value, key, out var nested)
            && nested is not null
            && TryReadObjectListValue(nested, out items);
    }

    private static bool TryReadObjectListValue(object? value, out IReadOnlyList<object?> items)
    {
        items = [];
        if (value is IReadOnlyList<object?> readOnlyList)
        {
            items = readOnlyList;
            return true;
        }

        if (value is IEnumerable<object?> enumerable)
        {
            items = enumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            items = jsonArray.EnumerateArray().Select(item => (object?)item).ToArray();
            return true;
        }

        return false;
    }

    private static bool TryReadObjectDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, object?> objects)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            objects = readOnlyDictionary;
            return true;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            objects = new Dictionary<string, object?>(dictionary, StringComparer.Ordinal);
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            var parsed = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in json.EnumerateObject())
            {
                parsed[property.Name] = property.Value;
            }

            objects = parsed;
            return true;
        }

        objects = new Dictionary<string, object?>(StringComparer.Ordinal);
        return false;
    }

    private static IReadOnlyList<string> ExtractObjectStringValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<string>();
        foreach (var item in items)
        {
            if (TryReadObjectString(item, key, out var value))
            {
                values.Add(value ?? string.Empty);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> ExtractObjectOptionalStringValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<string>();
        foreach (var item in items)
        {
            if (TryReadObjectOptionalString(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<long> ExtractObjectLongValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<long>();
        foreach (var item in items)
        {
            if (TryReadObjectLong(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<int> ExtractObjectIntValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<int>();
        foreach (var item in items)
        {
            if (TryReadObjectInt(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractObjectStringListValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<IReadOnlyList<string>>();
        foreach (var item in items)
        {
            if (TryReadObjectStringList(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool TryReadObjectString(object? value, string key, out string? text)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringValue(readOnlyValue, out text);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringValue(dictionaryValue, out text);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringValue(jsonValue, out text);
        }

        text = null;
        return false;
    }

    private static bool TryReadObjectOptionalString(object? value, string key, out string text)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            if (!readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
            {
                text = string.Empty;
                return true;
            }

            return TryReadOptionalStringValue(readOnlyValue, out text);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            if (!dictionary.TryGetValue(key, out var dictionaryValue))
            {
                text = string.Empty;
                return true;
            }

            return TryReadOptionalStringValue(dictionaryValue, out text);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            if (!json.TryGetProperty(key, out var jsonValue))
            {
                text = string.Empty;
                return true;
            }

            return TryReadOptionalStringValue(jsonValue, out text);
        }

        text = string.Empty;
        return false;
    }

    private static bool TryReadObjectStringList(object? value, string key, out IReadOnlyList<string> texts)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringListValue(readOnlyValue, out texts);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringListValue(dictionaryValue, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringListValue(jsonValue, out texts);
        }

        texts = [];
        return false;
    }

    private static bool TryReadObjectInt(object? value, string key, out int number)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadIntValue(readOnlyValue, out number);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadIntValue(dictionaryValue, out number);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadIntValue(jsonValue, out number);
        }

        number = 0;
        return false;
    }

    private static bool TryReadObjectLong(object? value, string key, out long number)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadLongValue(readOnlyValue, out number);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadLongValue(dictionaryValue, out number);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadLongValue(jsonValue, out number);
        }

        number = 0;
        return false;
    }

    private static bool TurnWindowMatches(object? value, TurnWindowState expected)
    {
        return TryReadObjectString(value, "state", out var state)
            && string.Equals(state, expected.State, StringComparison.Ordinal)
            && TryReadObjectBool(value, "isSpellDuel", out var isSpellDuel)
            && isSpellDuel == expected.IsSpellDuel
            && TryReadObjectBool(value, "isClosed", out var isClosed)
            && isClosed == expected.IsClosed
            && TryReadObjectBool(value, "hasStack", out var hasStack)
            && hasStack == expected.HasStack
            && TryReadObjectString(value, "actingPlayerId", out var actingPlayerId)
            && string.Equals(actingPlayerId, expected.ActingPlayerId, StringComparison.Ordinal);
    }

    private static bool SpellDuelMatches(object? value, SpellDuelState expected)
    {
        return TryReadObjectBool(value, "isActive", out var isActive)
            && isActive == expected.IsActive
            && TryReadObjectBool(value, "isClosed", out var isClosed)
            && isClosed == expected.IsClosed
            && TryReadObjectString(value, "spellDuelId", out var spellDuelId)
            && string.Equals(spellDuelId, expected.SpellDuelId, StringComparison.Ordinal)
            && TryReadObjectString(value, "battlefieldObjectId", out var battlefieldObjectId)
            && string.Equals(battlefieldObjectId, expected.BattlefieldObjectId, StringComparison.Ordinal)
            && TryReadObjectString(value, "focusPlayerId", out var focusPlayerId)
            && string.Equals(focusPlayerId, expected.FocusPlayerId, StringComparison.Ordinal)
            && TryReadObjectStringList(value, "passedFocusPlayerIds", out var passedFocusPlayerIds)
            && StringListsEqual(passedFocusPlayerIds, expected.PassedFocusPlayerIds)
            && TryReadObjectStringList(value, "stackItemIds", out var stackItemIds)
            && StringListsEqual(stackItemIds, expected.StackItemIds)
            && TryReadObjectStringList(value, "stackControllerIds", out var stackControllerIds)
            && StringListsEqual(stackControllerIds, expected.StackControllerIds);
    }

    private static bool BattleMatches(object? value, MatchState state, BattleState expected)
    {
        return TryReadObjectBool(value, "isActive", out var isActive)
            && isActive == expected.IsActive
            && TryReadObjectString(value, "battleId", out var battleId)
            && string.Equals(battleId, expected.BattleId, StringComparison.Ordinal)
            && TryReadObjectString(value, "battlefieldObjectId", out var battlefieldObjectId)
            && string.Equals(battlefieldObjectId, expected.BattlefieldObjectId, StringComparison.Ordinal)
            && TryReadObjectStringList(value, "attackerObjectIds", out var attackerObjectIds)
            && StringListsEqual(attackerObjectIds, expected.AttackerObjectIds)
            && TryReadObjectStringList(value, "defenderObjectIds", out var defenderObjectIds)
            && StringListsEqual(defenderObjectIds, expected.DefenderObjectIds)
            && TryReadObjectStringDictionary(value, "participantControllerIds", out var participantControllerIds)
            && StringDictionariesEqual(participantControllerIds, expected.ParticipantControllerIds)
            && TryReadObjectValue(value, "damageAssignment", out var damageAssignment)
            && BattleDamageAssignmentMatches(damageAssignment, state);
    }

    private static bool BattleDamageAssignmentMatches(object? value, MatchState state)
    {
        var expectedPending = ResolutionResult.HasOpenBattleDamageAssignmentWindow(state);
        if (!TryReadObjectBool(value, "isPending", out var isPending)
            || isPending != expectedPending)
        {
            return false;
        }

        if (!expectedPending)
        {
            return true;
        }

        return TryReadObjectString(value, "phase", out var phase)
            && string.Equals(phase, "DAMAGE_ASSIGNMENT", StringComparison.Ordinal)
            && TryReadObjectString(value, "battleId", out var battleId)
            && string.Equals(battleId, state.BattleState.BattleId, StringComparison.Ordinal)
            && TryReadObjectString(value, "battlefieldId", out var battlefieldId)
            && string.Equals(battlefieldId, state.BattleState.BattlefieldObjectId, StringComparison.Ordinal)
            && TryReadObjectString(value, "assigningPlayerId", out var assigningPlayerId)
            && string.Equals(assigningPlayerId, ResolutionResult.BattleDamageAssigningPlayerId(state), StringComparison.Ordinal)
            && TryReadObjectIntDictionary(value, "damagePool", out var damagePool)
            && IntDictionariesEqual(
                damagePool,
                ResolutionResult.BattleDamagePoolFor(state, state.BattleState))
            && TryReadObjectStringListDictionary(value, "legalTargets", out var legalTargets)
            && StringListDictionariesEqual(
                legalTargets,
                ResolutionResult.BattleDamageLegalTargetsFor(state, state.BattleState))
            && TryReadObjectIntDictionary(value, "existingDamage", out var existingDamage)
            && IntDictionariesEqual(
                existingDamage,
                ResolutionResult.BattleExistingDamageFor(state, state.BattleState))
            && TryReadObjectIntDictionary(value, "lethalDamageThreshold", out var lethalDamageThreshold)
            && IntDictionariesEqual(
                lethalDamageThreshold,
                ResolutionResult.BattleLethalDamageThresholdFor(state, state.BattleState))
            && TryReadObjectRequiredAssignments(value, "requiredAssignments", out var requiredAssignments)
            && RequiredAssignmentsEqual(
                requiredAssignments,
                ResolutionResult.BattleRequiredAssignmentsFor(state, state.BattleState));
    }

    private static bool TryReadObjectBool(object? value, string key, out bool flag)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadBoolValue(readOnlyValue, out flag);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadBoolValue(dictionaryValue, out flag);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadBoolValue(jsonValue, out flag);
        }

        flag = false;
        return false;
    }

    private static bool TryReadObjectValue(object? value, string key, out object? nested)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            nested = readOnlyValue;
            return true;
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            nested = dictionaryValue;
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            nested = jsonValue;
            return true;
        }

        nested = null;
        return false;
    }

    private static bool TryReadObjectStringDictionary(
        object? value,
        string key,
        out IReadOnlyDictionary<string, string> texts)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringDictionaryValue(readOnlyValue, out texts);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringDictionaryValue(dictionaryValue, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringDictionaryValue(jsonValue, out texts);
        }

        texts = new Dictionary<string, string>(StringComparer.Ordinal);
        return false;
    }

    private static bool TryReadObjectIntDictionary(
        object? value,
        string key,
        out IReadOnlyDictionary<string, int> numbers)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadIntDictionaryValue(readOnlyValue, out numbers);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadIntDictionaryValue(dictionaryValue, out numbers);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadIntDictionaryValue(jsonValue, out numbers);
        }

        numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        return false;
    }

    private static bool TryReadObjectStringListDictionary(
        object? value,
        string key,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> texts)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringListDictionaryValue(readOnlyValue, out texts);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringListDictionaryValue(dictionaryValue, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringListDictionaryValue(jsonValue, out texts);
        }

        texts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        return false;
    }

    private static bool TryReadObjectRequiredAssignments(
        object? value,
        string key,
        out IReadOnlyList<BattleRequiredAssignmentView> assignments)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadRequiredAssignmentsValue(readOnlyValue, out assignments);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadRequiredAssignmentsValue(dictionaryValue, out assignments);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadRequiredAssignmentsValue(jsonValue, out assignments);
        }

        assignments = [];
        return false;
    }

    private static bool TryReadStringValue(object? value, out string? text)
    {
        text = null;
        if (value is null)
        {
            return true;
        }

        if (value is string stringValue)
        {
            text = stringValue;
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.String } jsonString)
        {
            text = jsonString.GetString();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Null })
        {
            return true;
        }

        return false;
    }

    private static bool TryReadBoolValue(object? value, out bool flag)
    {
        switch (value)
        {
            case bool boolValue:
                flag = boolValue;
                return true;
            case JsonElement { ValueKind: JsonValueKind.True }:
                flag = true;
                return true;
            case JsonElement { ValueKind: JsonValueKind.False }:
                flag = false;
                return true;
            default:
                flag = false;
                return false;
        }
    }

    private static bool TryReadStringDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, string> texts)
    {
        texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyDictionary<string, string> stringDictionary)
        {
            texts = stringDictionary;
            return true;
        }

        if (value is IEnumerable<KeyValuePair<string, string>> stringPairs)
        {
            texts = stringPairs.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return TryConvertObjectDictionary(readOnlyDictionary, out texts);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return TryConvertObjectDictionary(dictionary, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } jsonObject)
        {
            var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in jsonObject.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                parsed[property.Name] = property.Value.GetString()!;
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool TryConvertObjectDictionary(
        IEnumerable<KeyValuePair<string, object?>> values,
        out IReadOnlyDictionary<string, string> texts)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (value is not string text)
            {
                texts = new Dictionary<string, string>(StringComparer.Ordinal);
                return false;
            }

            parsed[key] = text;
        }

        texts = parsed;
        return true;
    }

    private static bool TryReadIntDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, int> numbers)
    {
        numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyDictionary<string, int> intDictionary)
        {
            numbers = intDictionary;
            return true;
        }

        if (value is IEnumerable<KeyValuePair<string, int>> intPairs)
        {
            numbers = intPairs.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return TryConvertObjectIntDictionary(readOnlyDictionary, out numbers);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return TryConvertObjectIntDictionary(dictionary, out numbers);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } jsonObject)
        {
            var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in jsonObject.EnumerateObject())
            {
                if (!TryReadIntValue(property.Value, out var number))
                {
                    return false;
                }

                parsed[property.Name] = number;
            }

            numbers = parsed;
            return true;
        }

        return false;
    }

    private static bool TryConvertObjectIntDictionary(
        IEnumerable<KeyValuePair<string, object?>> values,
        out IReadOnlyDictionary<string, int> numbers)
    {
        var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!TryReadIntValue(value, out var number))
            {
                numbers = new Dictionary<string, int>(StringComparer.Ordinal);
                return false;
            }

            parsed[key] = number;
        }

        numbers = parsed;
        return true;
    }

    private static bool TryReadStringListDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> texts)
    {
        texts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyDictionary<string, IReadOnlyList<string>> stringListDictionary)
        {
            texts = stringListDictionary;
            return true;
        }

        if (value is IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> stringListPairs)
        {
            texts = stringListPairs.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return TryConvertObjectStringListDictionary(readOnlyDictionary, out texts);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return TryConvertObjectStringListDictionary(dictionary, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } jsonObject)
        {
            var parsed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var property in jsonObject.EnumerateObject())
            {
                if (!TryReadStringListValue(property.Value, out var values))
                {
                    return false;
                }

                parsed[property.Name] = values;
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool TryConvertObjectStringListDictionary(
        IEnumerable<KeyValuePair<string, object?>> values,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> texts)
    {
        var parsed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!TryReadStringListValue(value, out var strings))
            {
                texts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                return false;
            }

            parsed[key] = strings;
        }

        texts = parsed;
        return true;
    }

    private static bool TryReadRequiredAssignmentsValue(
        object? value,
        out IReadOnlyList<BattleRequiredAssignmentView> assignments)
    {
        assignments = [];
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyList<BattleRequiredAssignmentView> assignmentList)
        {
            assignments = assignmentList;
            return true;
        }

        if (value is IEnumerable<BattleRequiredAssignmentView> assignmentEnumerable)
        {
            assignments = assignmentEnumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            var parsed = new List<BattleRequiredAssignmentView>();
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (!TryReadRequiredAssignmentValue(item, out var assignment))
                {
                    return false;
                }

                parsed.Add(assignment);
            }

            assignments = parsed;
            return true;
        }

        if (value is IEnumerable<object?> objectValues)
        {
            return TryConvertRequiredAssignmentObjects(objectValues, out assignments);
        }

        return false;
    }

    private static bool TryConvertRequiredAssignmentObjects(
        IEnumerable<object?> values,
        out IReadOnlyList<BattleRequiredAssignmentView> assignments)
    {
        var parsed = new List<BattleRequiredAssignmentView>();
        foreach (var value in values)
        {
            if (!TryReadRequiredAssignmentValue(value, out var assignment))
            {
                assignments = [];
                return false;
            }

            parsed.Add(assignment);
        }

        assignments = parsed;
        return true;
    }

    private static bool TryReadRequiredAssignmentValue(
        object? value,
        out BattleRequiredAssignmentView assignment)
    {
        assignment = new BattleRequiredAssignmentView(string.Empty, 0, []);
        if (!TryReadObjectString(value, "sourceObjectId", out var sourceObjectId)
            || sourceObjectId is null
            || !TryReadObjectInt(value, "damage", out var damage)
            || !TryReadObjectStringList(value, "legalTargetObjectIds", out var legalTargetObjectIds))
        {
            return false;
        }

        assignment = new BattleRequiredAssignmentView(sourceObjectId, damage, legalTargetObjectIds);
        return true;
    }

    private static bool TryReadOptionalStringValue(object? value, out string text)
    {
        if (TryReadStringValue(value, out var maybeText))
        {
            text = maybeText ?? string.Empty;
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static bool TryReadIntValue(object? value, out int number)
    {
        switch (value)
        {
            case int intValue:
                number = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                number = (int)longValue;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } jsonNumber
                when jsonNumber.TryGetInt32(out var jsonInt):
                number = jsonInt;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryReadLongValue(object? value, out long number)
    {
        switch (value)
        {
            case int intValue:
                number = intValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } jsonNumber
                when jsonNumber.TryGetInt64(out var jsonLong):
                number = jsonLong;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryReadStringListValue(object? value, out IReadOnlyList<string> texts)
    {
        texts = [];
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyList<string> stringList)
        {
            texts = stringList;
            return true;
        }

        if (value is IEnumerable<string> stringEnumerable)
        {
            texts = stringEnumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            var parsed = new List<string>();
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                parsed.Add(item.GetString()!);
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool TryReadStringList(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out IReadOnlyList<string> texts)
    {
        texts = [];
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is IReadOnlyList<string> stringList)
        {
            texts = stringList;
            return true;
        }

        if (value is IEnumerable<string> stringEnumerable)
        {
            texts = stringEnumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            var parsed = new List<string>();
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                parsed.Add(item.GetString()!);
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool StringListsEqual(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        return left.Count == right.Count
            && left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool IntListsEqual(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        return left.Count == right.Count
            && left.SequenceEqual(right);
    }

    private static bool LongListsEqual(
        IReadOnlyList<long> left,
        IReadOnlyList<long> right)
    {
        return left.Count == right.Count
            && left.SequenceEqual(right);
    }

    private static bool StringListCollectionsEqual(
        IReadOnlyList<IReadOnlyList<string>> left,
        IReadOnlyList<IReadOnlyList<string>> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!StringListsEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<IReadOnlyList<string>> VisibleStackTargetObjectIdsForSpectator(MatchState state)
    {
        return state.StackItems
            .Select(item => VisibleObjectIdsForSpectator(state, item.TargetObjectIds))
            .ToArray();
    }

    private static bool IntDictionariesEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var value)
                && value == entry.Value);
    }

    private static bool StringListDictionariesEqual(
        IReadOnlyDictionary<string, IReadOnlyList<string>> left,
        IReadOnlyDictionary<string, IReadOnlyList<string>> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var values)
                && StringListsEqual(entry.Value, values));
    }

    private static bool RequiredAssignmentsEqual(
        IReadOnlyList<BattleRequiredAssignmentView> left,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> right)
    {
        return TryConvertRequiredAssignmentObjects(right, out var parsedRight)
            && RequiredAssignmentsEqual(left, parsedRight);
    }

    private static bool RequiredAssignmentsEqual(
        IReadOnlyList<BattleRequiredAssignmentView> left,
        IReadOnlyList<BattleRequiredAssignmentView> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!RequiredAssignmentEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RequiredAssignmentEqual(
        BattleRequiredAssignmentView left,
        BattleRequiredAssignmentView right)
    {
        return string.Equals(left.SourceObjectId, right.SourceObjectId, StringComparison.Ordinal)
            && left.Damage == right.Damage
            && StringListsEqual(left.LegalTargetObjectIds, right.LegalTargetObjectIds);
    }

    private static bool SeatsEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var seat)
                && string.Equals(seat, entry.Value, StringComparison.Ordinal));
    }
}
