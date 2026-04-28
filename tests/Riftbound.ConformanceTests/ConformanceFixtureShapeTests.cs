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

        var first = await session.SubmitAsync("P1", "intent-1", new PassCommand(), CancellationToken.None);
        var duplicate = await session.SubmitAsync("P1", "intent-1", new PassCommand(), CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.True(duplicate.Accepted);
        Assert.Equal(first.State.Tick, duplicate.State.Tick);
        Assert.Equal(first.Events, duplicate.Events);
        Assert.Single(journal.Entries);
    }

    [Fact]
    public async Task JournalEntriesCarryMonotonicEventSequenceBounds()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-end-turn", new EndTurnCommand(), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), CancellationToken.None);

        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(0, journal.Entries[0].StartedEventSequence);
        Assert.Equal(1, journal.Entries[0].CompletedEventSequence);
        Assert.Equal(1, journal.Entries[1].StartedEventSequence);
        Assert.Equal(6, journal.Entries[1].CompletedEventSequence);
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

    private static string PlayerSeat(SnapshotDto snapshot, string playerId)
    {
        var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
        return Assert.IsType<string>(player["seat"]);
    }
}
