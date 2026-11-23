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
    private PromptFreshnessCache? promptFreshnessCache;

    private static readonly IReadOnlyList<string> MainOpenSourceDrivenActions =
    [
        CommandTypes.PlayCard,
        CommandTypes.ActivateAbility,
        CommandTypes.AssembleEquipment,
        CommandTypes.MoveUnit,
        CommandTypes.DeclareBattle,
        CommandTypes.HideCard,
        CommandTypes.RevealCard,
        CommandTypes.TapRune,
        CommandTypes.RecycleRune,
        CommandTypes.LegendAct
    ];
    private static readonly IReadOnlyList<string> MainOpenActions =
        [.. MainOpenSourceDrivenActions, CommandTypes.EndTurn, CommandTypes.Surrender];

    private sealed record PromptFreshnessCache(MatchState State, RewindFrame? Rewind)
    {
        public Dictionary<string, IReadOnlyList<ActionPromptDto>> ByPlayer { get; } = new(StringComparer.Ordinal);
    }

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

    private string? InputFingerprint(JsonElement? rawCommand, TypedCommandInput? typedInput)
    {
        if (rawCommand is not { } command) return typedInput?.Fingerprint;
        if (command.ValueKind == JsonValueKind.Undefined) return "raw:undefined";
        var canonical = command.ValueKind == JsonValueKind.Object
            && command.TryGetProperty("cmdType", out var type) && type.ValueKind == JsonValueKind.String
            && MatchRecoveryValidator.HasCanonicalRawPayload(type.GetString()!, command,
                allowAbsentDeckRoles: options.DeckFormat == OfficialDeckFormat.Sealed);
        // Invalid numeric field forms must still conflict before ordinary payload
        // validation when an intent already belongs to a different valid input.
        return canonical ? "raw:canonical:" + CommandInputFingerprint.Hash(command)
            : "raw:malformed:" + MatchStateHasher.HashValue(command);
    }

    private bool MatchesPresentInput(JsonElement? rawCommand, string commandType, TypedCommandInput typedInput)
        => !string.IsNullOrWhiteSpace(commandType) && commandType == commandType.Trim()
            && (rawCommand is null || MatchRecoveryValidator.HasCanonicalRawPayload(commandType, rawCommand.Value,
                    allowAbsentDeckRoles: options.DeckFormat == OfficialDeckFormat.Sealed)
                && typedInput.MatchesWire(rawCommand.Value));

    private bool TryRejectStalePrompt(
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

    private bool TryRejectUnavailablePromptAction(
        MatchState state,
        string playerId,
        GameCommand command,
        JsonElement? rawCommand,
        out ResolutionResult rejection)
    {
        rejection = default!;
        if (rawCommand is not { ValueKind: JsonValueKind.Object } raw)
        {
            return false;
        }

        var hasPromptId = TryReadStringProperty(raw, "promptId", out var submittedPromptId);
        var hasSnapshotTick = TryReadLongProperty(raw, "snapshotTick", out var submittedSnapshotTick);
        if (!hasPromptId && !hasSnapshotTick)
        {
            return false;
        }

        // TryRejectStalePrompt has already accepted each supplied freshness
        // component. Match the components together here so a prompt id and a
        // snapshot tick that belong to different current prompt variants do
        // not authorize an action accidentally.
        var currentPrompts = BuildPromptFreshnessCandidates(state, playerId)
            .Where(currentPrompt =>
                (!hasPromptId || string.Equals(submittedPromptId, currentPrompt.PromptId, StringComparison.Ordinal))
                && (!hasSnapshotTick || submittedSnapshotTick == currentPrompt.SnapshotTick))
            .ToArray();
        // Snapshot-only envelopes are also used by legacy native callers for
        // phase-level validation. A prompt id is the client-side commitment
        // that identifies the exact candidate list; without it, retain the
        // old wait-prompt behavior while still protecting an actionable prompt.
        if (!hasPromptId && !currentPrompts.Any(currentPrompt => currentPrompt.Actionable))
        {
            return false;
        }
        if (currentPrompts.Any(currentPrompt =>
                currentPrompt.Candidates?.Any(candidate =>
                    string.Equals(candidate.Action, command.CmdType, StringComparison.Ordinal)
                    && candidate.Enabled) == true))
        {
            return false;
        }

        rejection = ResolutionResult.Rejected(
            state,
            "当前操作未在行动提示中提供，请按最新提示重新提交。",
            ErrorCodes.PromptExpired);
        return true;
    }

    private IReadOnlyList<ActionPromptDto> BuildPromptFreshnessCandidates(MatchState state, string playerId)
    {
        // The serial gate covers validation and state changes. Rewind can
        // return to the same state with a new timeline position.
        var frame = rewindFrame;
        var cache = promptFreshnessCache;
        if (cache is null || !ReferenceEquals(cache.State, state) || !ReferenceEquals(cache.Rewind, frame))
        {
            cache = new PromptFreshnessCache(state, frame);
            promptFreshnessCache = cache;
        }

        if (!cache.ByPlayer.TryGetValue(playerId, out var prompts))
        {
            prompts = BuildPromptFreshnessCandidates(state, playerId, frame);
            cache.ByPlayer.Add(playerId, prompts);
        }
        return prompts;
    }

    private static IReadOnlyList<ActionPromptDto> BuildPromptFreshnessCandidates(
        MatchState state, string playerId, RewindFrame? frame)
    {
        var unprojectedPrompt = ResolutionResult.BuildPrompts(state)[playerId];
        var currentPrompt = ProjectRewindPrompt(unprojectedPrompt, frame);
        if (!IsOrdinaryMainOpenPromptState(state, playerId))
        {
            return [currentPrompt];
        }

        const string mainOpenReason = "当前玩家普通开环行动";
        ActionPromptDto implementedPrompt;
        if (unprojectedPrompt.Actionable && unprojectedPrompt.Candidates is { } candidates
            && candidates.Select(candidate => candidate.Action).SequenceEqual(MainOpenActions, StringComparer.Ordinal))
        {
            // The older window ID contains only enabled actions. Candidate
            // availability depends on the action and state, not its siblings.
            var enabled = candidates.Where(candidate => candidate.Enabled).ToArray();
            var actions = enabled.Select(candidate => candidate.Action).ToArray();
            implementedPrompt = unprojectedPrompt with
            {
                PromptId = ActionPromptBuilder.PromptIdFor(state, playerId, actions),
                Actions = actions,
                Candidates = enabled
            };
        }
        else
        {
            implementedPrompt = ActionPromptBuilder.Build(state, playerId, true, mainOpenReason,
                ImplementedMainOpenActions(state, playerId, mainOpenReason));
        }

        implementedPrompt = ProjectRewindPrompt(implementedPrompt, frame);
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

    private static IReadOnlyList<string> ImplementedMainOpenActions(MatchState state, string playerId, string reason)
    {
        var actions = MainOpenSourceDrivenActions
            .Where(action =>
            {
                var prompt = ActionPromptBuilder.Build(state, playerId, true, reason, [action]);
                var candidate = prompt.Candidates?.FirstOrDefault();
                return candidate?.Enabled == true;
            })
            .ToList();

        actions.Add(CommandTypes.EndTurn);
        actions.Add(CommandTypes.Surrender);
        return actions;
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
