using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ConformanceFixtureShapeTests
{
    [Fact]
    public async Task DuplicateClientIntentDoesNotAdvanceTickTwice()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        var first = await session.SubmitAsync("P1", "intent-1", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var duplicate = await session.SubmitAsync("P1", "intent-1", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, "PASS", StringComparison.Ordinal))
            .ToArray();

        Assert.True(first.Accepted);
        Assert.True(duplicate.Accepted);
        Assert.Equal(first.State.Tick, duplicate.State.Tick);
        Assert.Equal(first.Events, duplicate.Events);
        Assert.Single(gameplayEntries);
    }

    [Fact]
    public async Task JournalEntriesCarryMonotonicEventSequenceBounds()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-end-turn", new EndTurnCommand(), RawCommand("END_TURN"), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => !string.Equals(entry.CommandType, "READY", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, gameplayEntries.Length);
        Assert.Equal(3, gameplayEntries[0].StartedEventSequence);
        Assert.Equal(4, gameplayEntries[0].CompletedEventSequence);
        Assert.Equal(4, gameplayEntries[1].StartedEventSequence);
        Assert.Equal(9, gameplayEntries[1].CompletedEventSequence);
    }

    [Fact]
    public async Task JournalEntryKeepsOriginalCommandPayload()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);
        var raw = JsonDocument.Parse("""{"cmdType":"PASS","clientNote":"keep-me"}""").RootElement.Clone();

        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), raw, CancellationToken.None);

        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.CommandType, "PASS", StringComparison.Ordinal));
        Assert.NotNull(entry.RawCommand);
        Assert.Equal("keep-me", entry.RawCommand.Value.GetProperty("clientNote").GetString());
    }

    [Fact]
    public async Task SubmitRequiresPlayerToJoinRoomFirst()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.PlayerNotInRoom, error.Code);
        Assert.Equal("player is not in room", error.Message);
    }

    [Fact]
    public async Task SubmitRequiresMatchToStart()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.MatchNotStarted, error.Code);
        Assert.Equal("match has not started", error.Message);
    }

    [Fact]
    public void JoinAssignsStableP1P2SeatsAndSnapshotsExposeSeatStatus()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");
        session.EnsurePlayer("alice");

        var aliceSnapshot = session.SnapshotFor("alice");
        var bobSnapshot = session.SnapshotFor("bob");

        Assert.Equal("alice", aliceSnapshot.ActivePlayerId);
        Assert.Equal("alice", bobSnapshot.ActivePlayerId);
        Assert.Equal("P1", PlayerSeat(aliceSnapshot, "alice"));
        Assert.Equal("P2", PlayerSeat(aliceSnapshot, "bob"));
        Assert.Equal("P1", PlayerSeat(bobSnapshot, "alice"));
        Assert.Equal("P2", PlayerSeat(bobSnapshot, "bob"));
    }

    [Fact]
    public void JoinRejectsThirdPlayer()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");

        var error = Assert.Throws<MatchSessionException>(() => session.EnsurePlayer("charlie"));
        Assert.Equal(ErrorCodes.RoomFull, error.Code);
        Assert.Equal("room already has two players", error.Message);
    }

    [Fact]
    public void ReconnectTokenIsStableAndRequired()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        var join = session.EnsurePlayer("alice");
        var duplicateJoin = session.EnsurePlayer(" alice ");
        var reconnect = session.ReconnectPlayer("alice", join.ReconnectToken);

        Assert.Equal(join, duplicateJoin);
        Assert.Equal(join, reconnect);
        Assert.Throws<MatchSessionException>(() => session.ReconnectPlayer("alice", "bad-token"));
    }

    [Fact]
    public void ProtocolEnvelopeKeepsCurrentContractFields()
    {
        var message = new WsServerMessage(
            MessageType.SNAPSHOT,
            "room",
            "P1",
            7,
            new { tick = 7 });

        Assert.Equal(MessageType.SNAPSHOT, message.Type);
        Assert.Equal("room", message.RoomId);
        Assert.Equal("P1", message.PlayerId);
        Assert.Equal(7, message.ServerTick);
    }

    [Theory]
    [InlineData("READY", typeof(ReadyCommand))]
    [InlineData("PASS_PRIORITY", typeof(PassPriorityCommand))]
    [InlineData("PASS_FOCUS", typeof(PassFocusCommand))]
    [InlineData("PASS", typeof(PassCommand))]
    [InlineData("END_TURN", typeof(EndTurnCommand))]
    public void GameCommandMapperKeepsPassAndEndTurnSemanticsDistinct(string cmdType, Type expectedType)
    {
        var command = GameCommandJsonMapper.Map(JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement);

        Assert.IsType(expectedType, command);
        Assert.Equal(cmdType, command.CmdType);
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static async Task ReadyBothAsync(MatchSession session)
    {
        await session.ReadyAsync("P1", "ready-p1", RawCommand("READY"), CancellationToken.None);
        await session.ReadyAsync("P2", "ready-p2", RawCommand("READY"), CancellationToken.None);
    }

    private static string PlayerSeat(SnapshotDto snapshot, string playerId)
    {
        var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
        return Assert.IsType<string>(player["seat"]);
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement.Clone();
    }
}
