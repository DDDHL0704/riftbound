using System.Text.Json;

namespace Riftbound.Engine;

internal static class RoomSeatRecoveryValidator
{
    public static void Validate(IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var registeredPlayers = new HashSet<string>(StringComparer.Ordinal);
        var registeredSeats = new HashSet<string>(StringComparer.Ordinal);
        var registrations = commands.Where(c => c.Accepted && c.CommandType == "ROOM_SEAT").ToArray();
        foreach (var command in registrations)
        {
            var label = "recorded room seat " + command.ClientIntentId;
            if (command.RawCommand is not { ValueKind: JsonValueKind.Object } raw
                || raw.EnumerateObject().Count() != 3
                || !raw.TryGetProperty("cmdType", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "ROOM_SEAT"
                || !raw.TryGetProperty("playerId", out var player) || player.ValueKind != JsonValueKind.String || player.GetString() != command.PlayerId
                || !raw.TryGetProperty("seat", out var seat) || seat.ValueKind != JsonValueKind.String || seat.GetString() is not ("P1" or "P2"))
            { errors.Add(label + " payload is invalid"); continue; }
            var slot = seat.GetString()!;
            if (commands[0].Accepted && commands[0].CommandType == "ROOM_SEAT" && slot != (registeredSeats.Count == 0 ? "P1" : "P2"))
                errors.Add(label + " assigned seat order is invalid");
            if (string.IsNullOrWhiteSpace(command.PlayerId) || command.PlayerId != command.PlayerId.Trim()
                || !registeredPlayers.Add(command.PlayerId) || !registeredSeats.Add(slot)
                || command.ClientIntentId != "room-seat:" + slot || !command.Accepted || command.ErrorMessage is not null
                || command.CompletedTick != command.StartedTick + 1 || command.CompletedEventSequence != command.StartedEventSequence + 1)
                errors.Add(label + " identity or transition is invalid");
            var span = events.Where(e => e.Sequence > command.StartedEventSequence && e.Sequence <= command.CompletedEventSequence).ToArray();
            if (span.Length != 1 || span[0].Sequence != command.CompletedEventSequence || span[0].Tick != command.CompletedTick
                || span[0].Order != 0 || span[0].Event.Kind != "PLAYER_SEATED"
                || span[0].Event.Payload.Count != 2
                || Text(span[0].Event.Payload.GetValueOrDefault("playerId")) != command.PlayerId
                || Text(span[0].Event.Payload.GetValueOrDefault("seat")) != slot)
                errors.Add(label + " event does not match the original input");
        }
        if (events.Count(e => e.Event.Kind == "PLAYER_SEATED") != registrations.Length)
            errors.Add("room seat event has no matching original registration input");
    }

    private static string? Text(object? value) => value is string text ? text
        : value is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : null;
}
