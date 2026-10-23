using Riftbound.Engine;

internal static class LoggedQaRoomBirthAssertions
{
    internal static void Check(MatchRecoveryFrame frame, string actor, string peer, string scenario, string setupIntent)
    {
        Require(frame.ReplayInitialState is { Tick: 0, Seats.Count: 0 } initial && initial.Seed == frame.OpeningSeed,
            "Replay must start from the original empty room and recorded private opening seed, before either seat exists.");
        Require(frame.Commands.Count > 0, "A durable QA checkpoint needs its actual first ROOM_SEAT input.");
        var seatCount = Math.Min(2, frame.Commands.Count);
        for (var index = 0; index < seatCount; index++) {
            var command = frame.Commands[index]; var slot = index == 0 ? "P1" : "P2"; var player = index == 0 ? actor : peer;
            Require(command.Accepted && command.CommandType == MatchSession.RoomSeatCommandType && command.PlayerId == player
                && command.ClientIntentId == "room-seat:" + slot && command.StartedTick == index && command.CompletedTick == index + 1
                && command.RawCommand is { } raw && raw.GetProperty("playerId").GetString() == player && raw.GetProperty("seat").GetString() == slot,
                "The first two original commands must be actual EnsurePlayerAsync ROOM_SEAT transitions for these exact identities.");
        }
        if (frame.Commands.Count < 3) {
            Require(frame.AuthoritativeState!.Seats.Count == seatCount && frame.AuthoritativeState.Status == MatchStatuses.Seating,
                "Each cold seat checkpoint must retain exactly the seats recorded so far."); return;
        }
        var setup = frame.Commands[2];
        Require(setup.Accepted && setup.ClientIntentId == setupIntent
            && setup.PlayerId == actor && setup.StartedTick == 2 && setup.CompletedTick == 3 && setup.CommandType == "DEV_SEED_SCENARIO:" + scenario,
            "The real DEV setup must follow both accepted seat registrations, never replace the empty-room replay origin.");
        Require(frame.Commands.Count(c => c.CommandType == MatchSession.RoomSeatCommandType) == 2,
            "Cold replay/reconnect must not add duplicate seat registrations.");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
