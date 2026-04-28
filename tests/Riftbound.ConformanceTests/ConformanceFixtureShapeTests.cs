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

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
