using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class MatchSession
{
    public const string RoomSeatCommandType = "ROOM_SEAT";

    private static string RoomSeatIntentId(string seat) => "room-seat:" + seat;

    private async ValueTask<(PlayerSessionDto Player, ResolutionResult? Transition)> RegisterPlayerAsync(
        string playerId, CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizePlayerId(playerId);
        await serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PlayerSessionDto player;
            MatchState? nextState = null;
            lock (seatGate)
            {
                if (seats.ContainsKey(normalizedPlayerId))
                {
                    if (!reconnectTokens.TryGetValue(normalizedPlayerId, out var token) || string.IsNullOrWhiteSpace(token))
                        throw new MatchSessionException(ErrorCodes.InvalidReconnectToken, "已有玩家重连需要提供重连令牌。");
                    player = PlayerSessionFor(normalizedPlayerId);
                }
                else
                {
                    if (seats.Count >= 2) throw new MatchSessionException(ErrorCodes.RoomFull, "房间已有两名玩家。");
                    if (state.Status is not (MatchStatuses.Empty or MatchStatuses.Seating))
                        throw new MatchSessionException(ErrorCodes.PhaseNotAllowed, "对局开始后不能新增玩家席位。");
                    var seat = NextOpenSeat();
                    player = new PlayerSessionDto(normalizedPlayerId, seat, NewReconnectToken());
                    var nextSeats = new Dictionary<string, string>(seats, StringComparer.Ordinal) { [normalizedPlayerId] = seat };
                    nextState = StateWithRoomSeats(state, nextSeats, normalizedPlayerId) with { Tick = state.Tick + 1 };
                }
            }

            if (nextState is null)
            {
                await PersistPlayerSessionAsync(player, cancellationToken).ConfigureAwait(false);
                return (player, null);
            }

            var raw = JsonSerializer.SerializeToElement(new { cmdType = RoomSeatCommandType, playerId = player.PlayerId, seat = player.Seat });
            var events = new[] { new GameEvent("PLAYER_SEATED", "玩家加入房间", new Dictionary<string, object?> { ["playerId"] = player.PlayerId, ["seat"] = player.Seat }) };
            var result = new ResolutionResult(true, null, nextState, events, ResolutionResult.BuildSnapshots(nextState), ResolutionResult.BuildPrompts(nextState));
            var intentId = RoomSeatIntentId(player.Seat);
            var entry = new MatchJournalEntry(RoomId, player.PlayerId, intentId, RoomSeatCommandType, raw,
                state.Tick, nextState.Tick, lastEventSequence, lastEventSequence + 1, true, null,
                nextState, events, result.Snapshots, result.Prompts, DateTimeOffset.UtcNow) { OpeningSeed = openingSeed, ErrorCode = result.ErrorCode };
            await playerStore.CommitRoomSeatAsync(entry, ReconnectTokenHasher.Hash(player.ReconnectToken),
                journal, cancellationToken).ConfigureAwait(false);

            lock (seatGate)
            {
                seats.Add(player.PlayerId, player.Seat);
                reconnectTokens.Add(player.PlayerId, player.ReconnectToken);
                state = nextState;
                lastEventSequence++;
                intentCache[$"{player.PlayerId}:{intentId}"] = new CachedResolution(RoomSeatCommandType, InputFingerprint(raw, null), result);
            }
            return (player, result);
        }
        finally { serialGate.Release(); }
    }

    internal async ValueTask<ResolutionResult> ReplayRoomSeatAsync(RecoveredCommand command, CancellationToken cancellationToken)
    {
        if (command.RawCommand is not { ValueKind: JsonValueKind.Object } raw
            || raw.EnumerateObject().Count() != 3
            || !raw.TryGetProperty("cmdType", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != RoomSeatCommandType
            || !raw.TryGetProperty("playerId", out var player) || player.ValueKind != JsonValueKind.String || player.GetString() != command.PlayerId
            || !raw.TryGetProperty("seat", out var seat) || seat.ValueKind != JsonValueKind.String
            || seat.GetString() != NextOpenSeat() || command.ClientIntentId != RoomSeatIntentId(seat.GetString()!)
            || seats.ContainsKey(command.PlayerId) || !command.Accepted || command.ErrorMessage is not null
            || command.CompletedTick != command.StartedTick + 1 || command.CompletedEventSequence != command.StartedEventSequence + 1)
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "recorded room seat input is invalid");
        var registered = await RegisterPlayerAsync(command.PlayerId, cancellationToken).ConfigureAwait(false);
        return registered.Transition ?? throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "recorded room seat was already installed");
    }

    private static MatchState StateWithRoomSeats(MatchState current, IReadOnlyDictionary<string, string> roomSeats, string addedPlayerId)
    {
        var active = roomSeats.ContainsKey(current.ActivePlayerId) ? current.ActivePlayerId : addedPlayerId;
        return current with
        {
            ActivePlayerId = active,
            TurnPlayerId = roomSeats.ContainsKey(current.TurnPlayerId) ? current.TurnPlayerId : addedPlayerId,
            Seats = new Dictionary<string, string>(roomSeats, StringComparer.Ordinal),
            Status = current.Status == MatchStatuses.InProgress ? MatchStatuses.InProgress : MatchStatuses.Seating,
            Phase = current.Status == MatchStatuses.InProgress ? current.Phase : MatchPhases.Room,
            TimingState = current.Status == MatchStatuses.InProgress ? current.TimingState : TimingStates.Room,
            ReadyPlayerIds = current.ReadyPlayerIds.Where(roomSeats.ContainsKey).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            RunePools = RunePoolsForSeats(current.RunePools, roomSeats.Keys),
            PlayerZones = PlayerZonesForSeats(current.PlayerZones, roomSeats.Keys),
            PlayerScores = PlayerScoresForSeats(current.PlayerScores, roomSeats.Keys),
            PlayerExperience = PlayerExperienceForSeats(current.PlayerExperience, roomSeats.Keys),
            PlayerCardsPlayedThisTurn = PlayerCardsPlayedThisTurnForSeats(current.PlayerCardsPlayedThisTurn, roomSeats.Keys),
            PlayerDecklists = PlayerDecklistsForSeats(current.PlayerDecklists, roomSeats.Keys),
            MulliganCompletedPlayerIds = current.MulliganCompletedPlayerIds.Where(roomSeats.ContainsKey).OrderBy(id => id, StringComparer.Ordinal).ToArray()
        };
    }
}
