using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateEvents(
        long? currentTick,
        long lastEventSequence,
        IReadOnlyList<RecoveredEvent> events,
        List<string> errors)
    {
        var orderedEvents = events.OrderBy(gameEvent => gameEvent.Sequence).ToArray();
        if (orderedEvents.Length == 0)
        {
            if (lastEventSequence != 0)
            {
                errors.Add($"event stream is empty but match last event sequence is {lastEventSequence}");
            }

            return;
        }

        if (orderedEvents[^1].Sequence != lastEventSequence)
        {
            errors.Add(
                $"event stream ends at {orderedEvents[^1].Sequence} but match last event sequence is {lastEventSequence}");
        }

        var previousFrameSequence = 0L;
        var previousFrameTick = 0L;
        foreach (var gameEvent in events)
        {
            if (gameEvent.Sequence <= 0)
            {
                errors.Add($"event sequence value {gameEvent.Sequence} must be positive");
            }

            if (string.IsNullOrWhiteSpace(gameEvent.Event.Kind))
            {
                errors.Add($"event sequence {gameEvent.Sequence} kind is required");
            }

            if (gameEvent.Event.Payload is null)
            {
                errors.Add($"event sequence {gameEvent.Sequence} payload is required");
            }

            if (gameEvent.Sequence <= previousFrameSequence)
            {
                errors.Add(
                    $"event stream is not ordered by sequence: {gameEvent.Sequence} after {previousFrameSequence}");
            }

            if (gameEvent.Tick < 0)
            {
                errors.Add($"event sequence {gameEvent.Sequence} has negative tick {gameEvent.Tick}");
            }

            if (gameEvent.Order < 0)
            {
                errors.Add($"event sequence {gameEvent.Sequence} has negative order {gameEvent.Order}");
            }

            if (currentTick is { } recoveryTick && gameEvent.Tick > recoveryTick)
            {
                errors.Add(
                    $"event sequence {gameEvent.Sequence} has tick {gameEvent.Tick} after recovery tick {recoveryTick}");
            }

            if (gameEvent.Tick < previousFrameTick)
            {
                errors.Add(
                    $"event sequence {gameEvent.Sequence} tick {gameEvent.Tick} is before previous event tick {previousFrameTick}");
            }

            previousFrameSequence = gameEvent.Sequence;
            previousFrameTick = gameEvent.Tick;
        }

        var previous = 0L;
        var seen = new HashSet<long>();
        foreach (var gameEvent in orderedEvents)
        {
            if (!seen.Add(gameEvent.Sequence))
            {
                errors.Add($"duplicate event sequence {gameEvent.Sequence}");
            }

            if (gameEvent.Sequence != previous + 1)
            {
                errors.Add($"event sequence gap before {gameEvent.Sequence}; expected {previous + 1}");
            }

            previous = gameEvent.Sequence;
        }
    }

    private static void ValidateCommands(
        long? currentTick,
        long lastEventSequence,
        IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events,
        IEnumerable<string> recoveredPlayerIds,
        string recoveredPlayerScopeLabel,
        List<string> errors)
    {
        var acceptedEventOwners = new Dictionary<long, string>();
        var seenCommandIntents = new HashSet<(string PlayerId, string ClientIntentId)>();
        var knownRecoveredPlayerIds = recoveredPlayerIds.ToHashSet(StringComparer.Ordinal);
        var previousFrameStartedEventSequence = 0L;
        var previousFrameCompletedEventSequence = 0L;
        // A recovery frame may begin from a pre-existing authoritative state,
        // whose first journal command can legitimately start after tick zero.
        var previousFrameCompletedTick = commands.Count == 0 ? 0L : commands[0].StartedTick;
        foreach (var command in commands)
        {
            if (command.StartedEventSequence < previousFrameStartedEventSequence
                || (command.StartedEventSequence == previousFrameStartedEventSequence
                    && command.CompletedEventSequence < previousFrameCompletedEventSequence))
            {
                errors.Add(
                    $"command stream is not ordered by event span: {command.ClientIntentId} {command.StartedEventSequence}->{command.CompletedEventSequence} after {previousFrameStartedEventSequence}->{previousFrameCompletedEventSequence}");
            }

            if (command.StartedEventSequence != previousFrameCompletedEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at event sequence {command.StartedEventSequence} but previous command completed at {previousFrameCompletedEventSequence}; command event spans must be contiguous");
            }

            previousFrameStartedEventSequence = command.StartedEventSequence;
            previousFrameCompletedEventSequence = command.CompletedEventSequence;

            var normalizedPlayerId = ValidateCommandRequiredNormalizedString(
                command.PlayerId,
                "player id",
                errors);
            var normalizedClientIntentId = ValidateCommandRequiredNormalizedString(
                command.ClientIntentId,
                "client intent id",
                errors);
            var normalizedCommandType = ValidateCommandRequiredNormalizedString(
                command.CommandType,
                "type",
                errors);

            if (command.RawCommand is null
                && command.TypedCommand is null
                && (CommandRequiresRawPayload(normalizedCommandType)
                    || TypedCommandInput.Parameterless(normalizedCommandType) is null))
            {
                errors.Add(
                    $"command {normalizedClientIntentId} raw command is required for {normalizedCommandType}");
            }

            if (command.TypedCommand is { } typedInput
                && (command.RawCommand is not null || !typedInput.MatchesRecordedType(normalizedCommandType)))
                errors.Add($"command {normalizedClientIntentId} typed command input is invalid or conflicts with raw input");

            if (normalizedPlayerId.Length > 0
                && knownRecoveredPlayerIds.Count > 0
                && !knownRecoveredPlayerIds.Contains(normalizedPlayerId))
            {
                errors.Add(
                    $"command {normalizedClientIntentId} player {normalizedPlayerId} is missing from {recoveredPlayerScopeLabel}");
            }

            if (!seenCommandIntents.Add((normalizedPlayerId, normalizedClientIntentId)))
            {
                errors.Add(
                    $"command {normalizedClientIntentId} for player {normalizedPlayerId} appears more than once in recovery frame");
            }

            if (command.StartedEventSequence < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative started event sequence {command.StartedEventSequence}");
            }

            if (command.CompletedEventSequence < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative completed event sequence {command.CompletedEventSequence}");
            }

            ValidateRawCommandShape(command, errors);
            if (!string.IsNullOrWhiteSpace(normalizedCommandType)
                && TryReadRawCommandType(command.RawCommand, out var rawCommandType)
                && !string.Equals(rawCommandType, ExpectedRawCommandType(normalizedCommandType), StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw cmdType {rawCommandType} does not match recovered command type {command.CommandType}");
            }

            ValidateRawCommandPayloadShape(command, normalizedCommandType, errors);

            if (command.StartedTick < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative started tick {command.StartedTick}");
            }

            if (command.CompletedTick < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative completed tick {command.CompletedTick}");
            }

            if (command.CompletedTick < command.StartedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes before tick start: {command.StartedTick}->{command.CompletedTick}");
            }

            if (command.StartedTick < previousFrameCompletedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at tick {command.StartedTick} before previous command completed tick {previousFrameCompletedTick}");
            }

            if (command.StartedTick != previousFrameCompletedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at tick {command.StartedTick} but previous command completed at tick {previousFrameCompletedTick}; command ticks must be contiguous");
            }

            previousFrameCompletedTick = Math.Max(previousFrameCompletedTick, command.CompletedTick);

            if (currentTick is { } recoveryTick)
            {
                if (command.StartedTick > recoveryTick)
                {
                    errors.Add(
                        $"command {command.ClientIntentId} starts at tick {command.StartedTick} after recovery tick {recoveryTick}");
                }

                if (command.CompletedTick > recoveryTick)
                {
                    errors.Add(
                        $"command {command.ClientIntentId} completes at tick {command.CompletedTick} after recovery tick {recoveryTick}");
                }
            }

            if (command.CompletedEventSequence < command.StartedEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes before it starts: {command.StartedEventSequence}->{command.CompletedEventSequence}");
            }

            if (command.StartedEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at {command.StartedEventSequence} after match sequence {lastEventSequence}");
            }

            if (command.CompletedEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes at {command.CompletedEventSequence} after match sequence {lastEventSequence}");
            }

            var expectedEventCount = command.CompletedEventSequence - command.StartedEventSequence;
            var actualEventCount = events.Count(gameEvent =>
                gameEvent.Sequence > command.StartedEventSequence
                && gameEvent.Sequence <= command.CompletedEventSequence);
            if (command.Accepted && command.ErrorMessage is not null)
            {
                errors.Add($"accepted command {command.ClientIntentId} has error message");
            }

            if (!command.Accepted)
            {
                if (string.IsNullOrWhiteSpace(command.ErrorMessage))
                {
                    errors.Add($"rejected command {command.ClientIntentId} is missing error message");
                }
                else if (!string.Equals(command.ErrorMessage, command.ErrorMessage.Trim(), StringComparison.Ordinal))
                {
                    errors.Add($"rejected command {command.ClientIntentId} error message has surrounding whitespace");
                }

                if (expectedEventCount > 0)
                {
                    errors.Add(
                        $"rejected command {command.ClientIntentId} covers {expectedEventCount} event(s); rejected commands must not record events");
                }

                if (command.CompletedTick != command.StartedTick)
                {
                    errors.Add(
                        $"rejected command {command.ClientIntentId} advances tick {command.StartedTick}->{command.CompletedTick}");
                }
            }
            else if (actualEventCount != expectedEventCount)
            {
                errors.Add(
                    $"command {command.ClientIntentId} covers {expectedEventCount} event(s) but {actualEventCount} were loaded");
            }
            else if (expectedEventCount == 0 && command.CompletedTick != command.StartedTick)
            {
                errors.Add(
                    $"accepted command {command.ClientIntentId} advances tick {command.StartedTick}->{command.CompletedTick} without events");
            }

            if (command.Accepted)
            {
                var commandEvents = events
                    .Where(gameEvent =>
                        gameEvent.Sequence > command.StartedEventSequence
                        && gameEvent.Sequence <= command.CompletedEventSequence)
                    .OrderBy(gameEvent => gameEvent.Sequence)
                    .ToArray();
                for (var eventIndex = 0; eventIndex < commandEvents.Length; eventIndex++)
                {
                    var gameEvent = commandEvents[eventIndex];
                    if (gameEvent.Order != eventIndex)
                    {
                        errors.Add(
                            $"event sequence {gameEvent.Sequence} has order {gameEvent.Order} but command {command.ClientIntentId} expects order {eventIndex}");
                    }

                    if (gameEvent.Tick < command.StartedTick || gameEvent.Tick > command.CompletedTick)
                    {
                        errors.Add(
                            $"event sequence {gameEvent.Sequence} has tick {gameEvent.Tick} outside command {command.ClientIntentId} tick span {command.StartedTick}->{command.CompletedTick}");
                    }

                    if (gameEvent.Tick != command.CompletedTick)
                    {
                        errors.Add(
                            $"event sequence {gameEvent.Sequence} has tick {gameEvent.Tick} but command {command.ClientIntentId} completed tick is {command.CompletedTick}");
                    }
                }

                if (commandEvents.Length > 0)
                {
                    var lastCoveredEventTick = commandEvents.Max(gameEvent => gameEvent.Tick);
                    if (lastCoveredEventTick != command.CompletedTick)
                    {
                        errors.Add(
                            $"command {command.ClientIntentId} completes at tick {command.CompletedTick} but covered event tick tail is {lastCoveredEventTick}");
                    }
                }

                foreach (var gameEvent in commandEvents)
                {
                    if (acceptedEventOwners.TryGetValue(gameEvent.Sequence, out var owner))
                    {
                        if (!string.Equals(owner, command.ClientIntentId, StringComparison.Ordinal))
                        {
                            errors.Add(
                                $"event sequence {gameEvent.Sequence} is covered by multiple accepted commands: {owner} and {command.ClientIntentId}");
                        }
                    }
                    else
                    {
                        acceptedEventOwners.Add(gameEvent.Sequence, command.ClientIntentId);
                    }
                }
            }
        }

        if (commands.Count == 0)
        {
            return;
        }

        if (currentTick is { } frameCurrentTick && previousFrameCompletedTick != frameCurrentTick)
        {
            errors.Add(
                $"command tick tail {previousFrameCompletedTick} does not match recovery tick {frameCurrentTick}");
        }

        foreach (var gameEvent in events.OrderBy(gameEvent => gameEvent.Sequence))
        {
            if (!acceptedEventOwners.ContainsKey(gameEvent.Sequence))
            {
                errors.Add(
                    $"event sequence {gameEvent.Sequence} is not covered by an accepted command");
            }
        }
    }

    private static string ValidateCommandRequiredNormalizedString(
        string value,
        string label,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"command {label} is required");
            return string.Empty;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"command {label} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    internal static bool HasCanonicalRawPayload(string commandType, JsonElement raw)
    {
        // Validation context only; this row is never recorded or replayed.
        var context = new RecoveredCommand("", "input", commandType, raw, 0, 0, 0, 0, false, null);
        var errors = new List<string>();
        ValidateRawCommandShape(context, errors);
        ValidateRawCommandPayloadShape(context, commandType, errors);
        return errors.Count == 0;
    }

    private static void ValidateRawCommandShape(RecoveredCommand command, List<string> errors)
    {
        if (command.RawCommand is not { } rawCommand)
        {
            return;
        }

        if (rawCommand.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"command {command.ClientIntentId} raw command must be a JSON object");
            return;
        }

        ValidateRawCommandObjectPropertyNames(command, "raw command", rawCommand, errors);

        if (!rawCommand.TryGetProperty("cmdType", out var rawCommandType))
        {
            errors.Add($"command {command.ClientIntentId} raw command is missing cmdType");
            return;
        }

        if (rawCommandType.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(rawCommandType.GetString()))
        {
            errors.Add($"command {command.ClientIntentId} raw cmdType is required");
            return;
        }

        var rawCommandTypeValue = rawCommandType.GetString()!;
        var normalizedRawCommandType = rawCommandTypeValue.Trim();
        if (!string.Equals(rawCommandTypeValue, normalizedRawCommandType, StringComparison.Ordinal))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw cmdType {normalizedRawCommandType} has surrounding whitespace");
        }

        ValidateRawCommandOptionalString(command, rawCommand, "promptId", errors);
        ValidateRawCommandOptionalNonNegativeInteger(command, rawCommand, "snapshotTick", errors);
    }

    private static void ValidateRawCommandObjectPropertyNames(
        RecoveredCommand command,
        string scopeLabel,
        JsonElement rawCommand,
        List<string> errors)
    {
        var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in rawCommand.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name))
            {
                errors.Add($"command {command.ClientIntentId} {scopeLabel} property name is required");
                continue;
            }

            var normalizedName = property.Name.Trim();
            if (!string.Equals(property.Name, normalizedName, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} {scopeLabel} property {normalizedName} has surrounding whitespace");
            }

            if (!seenPropertyNames.Add(normalizedName))
            {
                errors.Add(
                    $"command {command.ClientIntentId} {scopeLabel} property {normalizedName} appears more than once");
            }
        }
    }

    private static void ValidateRawCommandPayloadShape(
        RecoveredCommand command,
        string normalizedCommandType,
        List<string> errors)
    {
        if (command.RawCommand is not { ValueKind: JsonValueKind.Object } rawCommand)
        {
            return;
        }

        if (normalizedCommandType.StartsWith(DevSeedScenarioPrefix, StringComparison.Ordinal))
        {
            ValidateRawDevSeedScenarioPayload(command, rawCommand, normalizedCommandType, errors);
            return;
        }

        switch (normalizedCommandType)
        {
            case CommandTypes.SubmitDeck:
                ValidateRawCommandRequiredString(command, rawCommand, "legendCardNo", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "championCardNo", errors);
                ValidateRawCommandStringArray(command, rawCommand, "mainDeck", errors);
                ValidateRawCommandStringArray(command, rawCommand, "runeDeck", errors);
                ValidateRawCommandStringArray(command, rawCommand, "battlefields", errors);
                break;
            case CommandTypes.Mulligan:
                ValidateRawCommandStringArray(command, rawCommand, "handObjectIds", errors);
                break;
            case CommandTypes.PlayCard:
            case CommandTypes.RefusePlayPayment:
                ValidateRawCommandRequiredString(command, rawCommand, "sourceObjectId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "cardNo", errors);
                ValidateRawCommandStringArray(command, rawCommand, "targetObjectIds", errors);
                ValidateRawCommandOptionalString(command, rawCommand, "mode", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "optionalCosts", errors);
                ValidateRawCommandOptionalString(command, rawCommand, "destination", errors);
                break;
            case CommandTypes.ActivateAbility:
            case CommandTypes.LegendAct:
                ValidateRawCommandRequiredString(command, rawCommand, "sourceObjectId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "abilityId", errors);
                ValidateRawCommandStringArray(command, rawCommand, "targetObjectIds", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "optionalCosts", errors);
                break;
            case CommandTypes.TapRune:
            case CommandTypes.RecycleRune:
                ValidateRawCommandRequiredString(command, rawCommand, "sourceObjectId", errors);
                break;
            case CommandTypes.HideCard:
                ValidateRawCommandRequiredString(command, rawCommand, "sourceObjectId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "cardNo", errors);
                ValidateRawCommandOptionalString(command, rawCommand, "destination", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "optionalCosts", errors);
                break;
            case CommandTypes.RevealCard:
                ValidateRawCommandRequiredString(command, rawCommand, "sourceObjectId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "cardNo", errors);
                ValidateRawCommandStringArray(command, rawCommand, "targetObjectIds", errors);
                ValidateRawCommandOptionalString(command, rawCommand, "mode", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "optionalCosts", errors);
                ValidateRawCommandOptionalString(command, rawCommand, "destination", errors);
                break;
            case CommandTypes.MoveUnit:
                ValidateRawCommandRequiredString(command, rawCommand, "sourceObjectId", errors);
                // The runtime derives an omitted origin from authoritative locations.
                // Older native table commands omitted it, including accepted group moves.
                ValidateRawCommandOptionalString(command, rawCommand, "origin", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "destination", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "optionalCosts", errors);
                break;
            case CommandTypes.AssembleEquipment:
                ValidateRawCommandRequiredString(command, rawCommand, "sourceObjectId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "targetObjectId", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "optionalCosts", errors);
                break;
            case CommandTypes.DeclareBattle:
                ValidateRawCommandRequiredString(command, rawCommand, "battlefieldId", errors);
                ValidateRawCommandStringArray(command, rawCommand, "attackerObjectIds", errors);
                ValidateRawCommandStringArray(command, rawCommand, "defenderObjectIds", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "optionalCosts", errors);
                ValidateRawCommandOptionalStringArray(command, rawCommand, "battlefieldTargetObjectIds", errors);
                break;
            case CommandTypes.PayCost:
                ValidateRawCommandRequiredString(command, rawCommand, "paymentId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "paymentWindow", errors);
                ValidateRawCommandStringArray(command, rawCommand, "paymentChoiceIds", errors);
                break;
            case CommandTypes.AssignCombatDamage:
                ValidateRawCommandRequiredString(command, rawCommand, "battleId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "battlefieldId", errors);
                ValidateRawCommandCombatAssignments(command, rawCommand, errors);
                break;
            case CommandTypes.OrderTriggers:
                ValidateRawCommandOrderedTriggerIds(command, rawCommand, errors);
                break;
            case CommandTypes.ChooseHandCards:
                ValidateRawCommandRequiredString(command, rawCommand, "choiceId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "choiceWindow", errors);
                ValidateRawCommandStringArray(command, rawCommand, "chosenObjectIds", errors);
                break;
            case CommandTypes.ChooseCards:
                ValidateRawCommandRequiredString(command, rawCommand, "choiceId", errors);
                ValidateRawCommandRequiredString(command, rawCommand, "choiceWindow", errors);
                ValidateRawCommandStringArray(command, rawCommand, "chosenObjectIds", errors);
                break;
        }
    }

    private static void ValidateRawDevSeedScenarioPayload(
        RecoveredCommand command,
        JsonElement rawCommand,
        string normalizedCommandType,
        List<string> errors)
    {
        ValidateRawCommandRequiredString(command, rawCommand, "scenarioId", errors);
        if (!rawCommand.TryGetProperty("scenarioId", out var scenarioIdProperty)
            || scenarioIdProperty.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(scenarioIdProperty.GetString()))
        {
            return;
        }

        var scenarioId = scenarioIdProperty.GetString()!.Trim();
        var expectedScenarioId = normalizedCommandType[DevSeedScenarioPrefix.Length..];
        if (!string.Equals(scenarioId, expectedScenarioId, StringComparison.Ordinal))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} scenarioId {scenarioId} does not match recovered scenario {expectedScenarioId}");
        }
    }

    private static void ValidateRawCommandRequiredString(
        RecoveredCommand command,
        JsonElement rawCommand,
        string propertyName,
        List<string> errors)
    {
        if (!rawCommand.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} {propertyName} is required");
            return;
        }

        var value = property.GetString()!;
        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} {propertyName} {normalizedValue} has surrounding whitespace");
        }
    }

    private static void ValidateRawCommandStringArray(
        RecoveredCommand command,
        JsonElement rawCommand,
        string propertyName,
        List<string> errors)
    {
        if (!rawCommand.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} {propertyName} must be an array");
            return;
        }

        var index = 0;
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(item.GetString()))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw {command.CommandType} {propertyName}[{index}] is required");
                index++;
                continue;
            }

            var value = item.GetString()!;
            var normalizedValue = value.Trim();
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw {command.CommandType} {propertyName}[{index}] {normalizedValue} has surrounding whitespace");
            }

            index++;
        }
    }

    private static void ValidateRawCommandOptionalString(
        RecoveredCommand command,
        JsonElement rawCommand,
        string propertyName,
        List<string> errors)
    {
        if (!rawCommand.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        // The live mapper accepts absent/null/empty optional scalars. Preserve that
        // contract for journals written by native clients, without accepting wrong types.
        if (property.ValueKind == JsonValueKind.Null
            || (property.ValueKind == JsonValueKind.String && property.GetString() == string.Empty)) return;

        if (property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} {propertyName} is required");
            return;
        }

        var value = property.GetString()!;
        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} {propertyName} {normalizedValue} has surrounding whitespace");
        }
    }

    private static void ValidateRawCommandOptionalNonNegativeInteger(
        RecoveredCommand command,
        JsonElement rawCommand,
        string propertyName,
        List<string> errors)
    {
        if (!rawCommand.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt64(out var value)
            || value < 0)
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} {propertyName} must be a non-negative integer");
        }
    }

    private static void ValidateRawCommandOptionalStringArray(
        RecoveredCommand command,
        JsonElement rawCommand,
        string propertyName,
        List<string> errors)
    {
        if (!rawCommand.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} {propertyName} must be an array");
            return;
        }

        var index = 0;
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(item.GetString()))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw {command.CommandType} {propertyName}[{index}] is required");
                index++;
                continue;
            }

            var value = item.GetString()!;
            var normalizedValue = value.Trim();
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw {command.CommandType} {propertyName}[{index}] {normalizedValue} has surrounding whitespace");
            }

            index++;
        }
    }

    private static void ValidateRawCommandCombatAssignments(
        RecoveredCommand command,
        JsonElement rawCommand,
        List<string> errors)
    {
        if (!rawCommand.TryGetProperty("assignments", out var assignments)
            || assignments.ValueKind != JsonValueKind.Array)
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} assignments must be an array");
            return;
        }

        var index = 0;
        foreach (var assignment in assignments.EnumerateArray())
        {
            if (assignment.ValueKind != JsonValueKind.Object)
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw {command.CommandType} assignments[{index}] must be an object");
                index++;
                continue;
            }

            ValidateRawCommandObjectPropertyNames(
                command,
                $"raw {command.CommandType} assignments[{index}]",
                assignment,
                errors);
            ValidateRawCommandAssignmentString(command, assignment, index, "sourceObjectId", errors);
            ValidateRawCommandAssignmentString(command, assignment, index, "targetObjectId", errors);
            if (!assignment.TryGetProperty("damage", out var damage)
                || damage.ValueKind != JsonValueKind.Number
                || !damage.TryGetInt32(out _))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw {command.CommandType} assignments[{index}].damage is required");
            }

            index++;
        }
    }

    private static void ValidateRawCommandAssignmentString(
        RecoveredCommand command,
        JsonElement assignment,
        int index,
        string propertyName,
        List<string> errors)
    {
        if (!assignment.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} assignments[{index}].{propertyName} is required");
            return;
        }

        var value = property.GetString()!;
        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw {command.CommandType} assignments[{index}].{propertyName} {normalizedValue} has surrounding whitespace");
        }
    }

    private static void ValidateRawCommandOrderedTriggerIds(
        RecoveredCommand command,
        JsonElement rawCommand,
        List<string> errors)
    {
        if (rawCommand.TryGetProperty("orderedTriggerIds", out _))
        {
            ValidateRawCommandStringArray(command, rawCommand, "orderedTriggerIds", errors);
            return;
        }

        if (rawCommand.TryGetProperty("triggerIds", out _))
        {
            ValidateRawCommandStringArray(command, rawCommand, "triggerIds", errors);
            return;
        }

        errors.Add(
            $"command {command.ClientIntentId} raw {command.CommandType} orderedTriggerIds or triggerIds must be an array");
    }

    private static bool CommandRequiresRawPayload(string recoveredCommandType)
    {
        if (recoveredCommandType.StartsWith(DevSeedScenarioPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        return recoveredCommandType switch
        {
            CommandTypes.SubmitDeck
                or CommandTypes.Mulligan
                or CommandTypes.PlayCard
                or CommandTypes.RefusePlayPayment
                or CommandTypes.ActivateAbility
                or CommandTypes.LegendAct
                or CommandTypes.HideCard
                or CommandTypes.TapRune
                or CommandTypes.RecycleRune
                or CommandTypes.RevealCard
                or CommandTypes.MoveUnit
                or CommandTypes.AssembleEquipment
                or CommandTypes.DeclareBattle
                or CommandTypes.PayCost
                or CommandTypes.AssignCombatDamage
                or CommandTypes.OrderTriggers
                or CommandTypes.ChooseHandCards
                or CommandTypes.ChooseCards => true,
            _ => false
        };
    }

    private static bool TryReadRawCommandType(JsonElement? rawCommand, out string commandType)
    {
        commandType = string.Empty;
        if (rawCommand is not { ValueKind: JsonValueKind.Object } raw
            || !raw.TryGetProperty("cmdType", out var rawCommandType)
            || rawCommandType.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = rawCommandType.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        commandType = value.Trim();
        return true;
    }

    private static string ExpectedRawCommandType(string recoveredCommandType)
    {
        return recoveredCommandType.StartsWith(DevSeedScenarioPrefix, StringComparison.Ordinal)
            ? DevSeedScenarioCommandType
            : recoveredCommandType;
    }
}
