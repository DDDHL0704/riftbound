using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial class MatchSession
{

    // Match the existing recovery envelope rules before any uncached request can be journaled.
    // Nullable-absent raw remains a separate typed-caller contract; do not fabricate wire JSON.
    private static bool HasCanonicalPresentRawEnvelope(JsonElement? rawCommand, string expectedCommandType)
    {
        if (rawCommand is null) return true;
        var raw = rawCommand.Value;
        if (raw.ValueKind != JsonValueKind.Object) return false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in raw.EnumerateObject())
            if (string.IsNullOrWhiteSpace(property.Name) || property.Name != property.Name.Trim()
                || !names.Add(property.Name)) return false;
        if (!raw.TryGetProperty("cmdType", out var type) || type.ValueKind != JsonValueKind.String)
            return false;
        var value = type.GetString();
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()
            || !string.Equals(value, expectedCommandType, StringComparison.Ordinal)) return false;
        if (raw.TryGetProperty("promptId", out var prompt) && prompt.ValueKind != JsonValueKind.Null)
        {
            if (prompt.ValueKind != JsonValueKind.String) return false;
            var text = prompt.GetString()!;
            if (text.Length > 0 && (string.IsNullOrWhiteSpace(text) || text != text.Trim())) return false;
        }
        return !raw.TryGetProperty("snapshotTick", out var tick)
            || tick.ValueKind == JsonValueKind.Number && tick.TryGetInt64(out var number) && number >= 0;
    }

    private static bool HasCanonicalRequiredText(JsonElement raw, string field)
        => raw.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text && !string.IsNullOrWhiteSpace(text) && text == text.Trim();

    private static bool HasCanonicalRequiredStringArray(JsonElement raw, string field)
    {
        return raw.TryGetProperty(field, out var value)
            && value.ValueKind == JsonValueKind.Array
            && HasCanonicalOptionalStringArray(raw, field);
    }

    private static bool HasCanonicalOptionalCosts(GameCommand command, JsonElement? rawCommand)
    {
        if (command is not (PlayCardCommand or RefusePlayPaymentCommand or ActivateAbilityCommand or LegendActCommand or HideCardCommand
                or RevealCardCommand or MoveUnitCommand or AssembleEquipmentCommand or DeclareBattleCommand)
            || rawCommand is not { ValueKind: JsonValueKind.Object } raw)
            return true;

        return HasCanonicalOptionalStringArray(raw, "optionalCosts");
    }

    private static bool HasCanonicalOptionalStringArray(JsonElement raw, string field)
    {
        var seen = false;
        foreach (var property in raw.EnumerateObject())
        {
            if (!string.Equals(property.Name.Trim(), field, StringComparison.Ordinal)) continue;
            if (seen || property.Name != field || property.Value.ValueKind != JsonValueKind.Array) return false;
            seen = true;
            foreach (var choice in property.Value.EnumerateArray())
            {
                if (choice.ValueKind != JsonValueKind.String) return false;
                var value = choice.GetString()!;
                if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) return false;
            }
        }
        return true;
    }

    private static bool IntentConflicts(CachedResolution cached, string commandType, string? rawCommandHash)
    {
        if (!string.Equals(cached.CommandType, commandType, StringComparison.Ordinal))
        {
            return true;
        }

        return !string.Equals(cached.InputHash, rawCommandHash, StringComparison.Ordinal);
    }

    private static string? InputFingerprint(JsonElement? rawCommand, TypedCommandInput? typedInput)
    {
        if (rawCommand is not { } command) return typedInput?.Fingerprint;
        if (command.ValueKind == JsonValueKind.Undefined) return "raw:undefined";
        var canonical = command.ValueKind == JsonValueKind.Object
            && command.TryGetProperty("cmdType", out var type) && type.ValueKind == JsonValueKind.String
            && MatchRecoveryValidator.HasCanonicalRawPayload(type.GetString()!, command);
        // Invalid numeric field forms must still conflict before ordinary payload
        // validation when an intent already belongs to a different valid input.
        return canonical ? "raw:canonical:" + CommandInputFingerprint.Hash(command)
            : "raw:malformed:" + MatchStateHasher.HashValue(command);
    }

    private static bool MatchesPresentInput(JsonElement? rawCommand, string commandType, TypedCommandInput typedInput)
        => !string.IsNullOrWhiteSpace(commandType) && commandType == commandType.Trim()
            && (rawCommand is null || MatchRecoveryValidator.HasCanonicalRawPayload(commandType, rawCommand.Value)
                && typedInput.MatchesWire(rawCommand.Value));

    private static bool TryRejectStalePrompt(
        MatchState state,
        string playerId,
        JsonElement? rawCommand,
        out ResolutionResult rejection)
    {
        rejection = default!;
        if (rawCommand is not { ValueKind: JsonValueKind.Object } command)
        {
            return false;
        }

        var hasPromptId = TryReadStringProperty(command, "promptId", out var submittedPromptId);
        var hasSnapshotTick = TryReadLongProperty(command, "snapshotTick", out var submittedSnapshotTick);
        if (!hasPromptId && !hasSnapshotTick)
        {
            return false;
        }

        var currentPrompts = BuildPromptFreshnessCandidates(state, playerId);
        if (hasPromptId
            && !currentPrompts.Any(currentPrompt => string.Equals(submittedPromptId, currentPrompt.PromptId, StringComparison.Ordinal)))
        {
            rejection = ResolutionResult.Rejected(
                state,
                "行动窗口已过期，请按最新提示重新提交。",
                ErrorCodes.PromptExpired);
            return true;
        }

        if (hasSnapshotTick
            && !currentPrompts.Any(currentPrompt => submittedSnapshotTick == currentPrompt.SnapshotTick))
        {
            rejection = ResolutionResult.Rejected(
                state,
                "行动快照已过期，请按最新状态重新提交。",
                ErrorCodes.PromptExpired);
            return true;
        }

        return false;
    }

    private static IReadOnlyList<ActionPromptDto> BuildPromptFreshnessCandidates(MatchState state, string playerId)
    {
        var currentPrompt = ResolutionResult.BuildPrompts(state)[playerId];
        if (!IsOrdinaryMainOpenPromptState(state, playerId))
        {
            return [currentPrompt];
        }

        var implementedPrompt = ActionPromptBuilder.Build(
            state,
            playerId,
            true,
            "当前玩家普通开环行动",
            WithSurrender(ImplementedMainOpenActions(state, playerId)));

        return string.Equals(currentPrompt.PromptId, implementedPrompt.PromptId, StringComparison.Ordinal)
            ? [currentPrompt]
            : [currentPrompt, implementedPrompt];
    }

    private static bool IsOrdinaryMainOpenPromptState(MatchState state, string playerId)
    {
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            && string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
            && string.Equals(state.TurnPlayerId, playerId, StringComparison.Ordinal)
            && state.PendingPayment is null
            && state.PendingHandChoice is null
            && state.PendingCardChoice is null
            && string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            && string.IsNullOrWhiteSpace(state.FocusPlayerId)
            && state.StackItems.Count == 0
            && !ResolutionResult.HasOpenOrderTriggersWindow(state)
            && !ResolutionResult.HasOpenBattleDamageAssignmentWindow(state)
            && ResolutionResult.ActiveStartBattleTask(state) is null
            && !ResolutionResult.HasBlockingPendingTaskQueue(state);
    }

    private static IReadOnlyList<string> ImplementedMainOpenActions(MatchState state, string playerId)
    {
        var reason = "当前玩家普通开环行动";
        var implementedSourceDrivenActions = new[]
        {
            "PLAY_CARD",
            "ACTIVATE_ABILITY",
            "ASSEMBLE_EQUIPMENT",
            "MOVE_UNIT",
            "DECLARE_BATTLE",
            "HIDE_CARD",
            "REVEAL_CARD",
            "TAP_RUNE",
            "RECYCLE_RUNE",
            "LEGEND_ACT"
        };
        var actions = implementedSourceDrivenActions
            .Where(action =>
            {
                var prompt = ActionPromptBuilder.Build(state, playerId, true, reason, [action]);
                var candidate = prompt.Candidates?.FirstOrDefault();
                return candidate?.Enabled == true;
            })
            .ToList();

        actions.Add("END_TURN");
        return actions;
    }

    private static IReadOnlyList<string> WithSurrender(IReadOnlyList<string> actions)
    {
        return actions.Contains(CommandTypes.Surrender, StringComparer.Ordinal)
            ? actions
            : actions.Concat([CommandTypes.Surrender]).ToArray();
    }

    private static bool TryReadStringProperty(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadLongProperty(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String
            && long.TryParse(property.GetString(), out value))
        {
            return true;
        }

        return false;
    }
}
