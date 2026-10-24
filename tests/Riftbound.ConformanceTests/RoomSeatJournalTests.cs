using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RoomSeatJournalTests
{
    [Fact]
    public async Task LaterJoinIsAnOriginalTransitionAndEveryFirstDeckHistoryReplaysFromItsActualEmptyRoom()
    {
        var journal = new Journal();
        var session = new MatchSession("late-seat-history", new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("alice", default);
        Assert.Single(journal.Entries);
        var firstSeat = journal.Entries[0];
        Assert.Equal("ROOM_SEAT", firstSeat.CommandType);
        Assert.Equal(1, firstSeat.CompletedTick);
        Assert.Equal(1, firstSeat.CompletedEventSequence);
        Assert.Equal("P1", Assert.Single(firstSeat.AuthoritativeState.Seats).Value);
        Assert.Equal("PLAYER_SEATED", Assert.Single(firstSeat.Events).Kind);
        Assert.Equal(3, firstSeat.RawCommand!.Value.EnumerateObject().Count());
        Assert.DoesNotContain("token", firstSeat.RawCommand.Value.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var raw = JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var first = await session.SubmitDeckAsync("alice", "deck-a", command, raw, default);
        Assert.True(first.Accepted, first.ErrorMessage);
        await session.EnsurePlayerAsync("bob", default);
        Assert.Equal(new[] { "ROOM_SEAT", "SUBMIT_DECK", "ROOM_SEAT" }, journal.Entries.Select(e => e.CommandType));
        Assert.Equal(new[] { 1, 1, 2 }, journal.Entries.Select(e => e.AuthoritativeState.Seats.Count));
        var second = await session.SubmitDeckAsync("bob", "deck-b", command, raw, default);
        Assert.True(second.Accepted, second.ErrorMessage);

        var initial = MatchState.Create(session.RoomId)
            with { Seed = firstSeat.OpeningSeed };
        for (var count = 1; count <= journal.Entries.Count; count++)
        {
            var prefix = journal.Entries.Take(count).ToArray();
            var last = prefix[^1];
            var commands = prefix.Select(Command).ToArray();
            var events = prefix.SelectMany(e => e.Events.Select((item, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, item))).ToArray();
            var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, last.CompletedTick, last.CompletedEventSequence,
                p.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[p.Key]));
            Assert.Empty(MatchRecoveryValidator.Validate(session.RoomId, last.CompletedEventSequence, commands, events, views, last.AuthoritativeState, last.CompletedTick));
            var frame = new MatchRecoveryFrame(session.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState, initial)
                { OpeningSeed = firstSeat.OpeningSeed };
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, last.AuthoritativeState, new CoreRuleEngine(), default, events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
    }

    [Fact]
    public async Task KnownLivePlayerDoesNotCreateAnotherSeatTransition()
    {
        var journal = new Journal(); var session = new MatchSession("seat-repeat", new CoreRuleEngine(), journal);
        var first = await session.EnsurePlayerAsync("alice", default);
        var again = await session.EnsurePlayerAsync("alice", default);
        Assert.Equal(first, again);
        Assert.Single(journal.Entries);
        Assert.Equal(1, session.SnapshotFor("alice").Tick);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("slot")]
    [InlineData("intent")]
    [InlineData("accepted")]
    [InlineData("tick")]
    [InlineData("sequence")]
    [InlineData("error")]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("event-actor")]
    [InlineData("event-slot")]
    [InlineData("event-missing")]
    public async Task IndependentRecoveryAndReplayRejectAlteredOriginalRoomSeatInputs(string branch)
    {
        var journal = new Journal(); var session = new MatchSession("seat-forgery", new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("alice", default);
        var original = Frame(journal.Entries);
        Assert.Empty(MatchRecoveryValidator.Validate(original.RoomId, original.LastEventSequence, original.Commands, original.Events,
            original.PlayerViews, original.AuthoritativeState, original.CurrentTick));
        var command = original.Commands[0]; var gameEvent = original.Events[0];
        var payload = gameEvent.Event.Payload.ToDictionary(p => p.Key, p => p.Value);
        switch (branch)
        {
            case "actor": command = command with { RawCommand = Raw("intruder", "P1") }; break;
            case "slot":
                command = command with { RawCommand = Raw("alice", "P2"), ClientIntentId = "room-seat:P2" };
                payload["seat"] = "P2"; break;
            case "intent": command = command with { ClientIntentId = "client-seat" }; break;
            case "accepted": command = command with { Accepted = false }; break;
            case "tick": command = command with { CompletedTick = 2 }; break;
            case "sequence": command = command with { CompletedEventSequence = 2 }; break;
            case "error": command = command with { ErrorMessage = "" }; break;
            case "extra": command = command with { RawCommand = JsonSerializer.SerializeToElement(new { cmdType = "ROOM_SEAT", playerId = "alice", seat = "P1", extra = true }) }; break;
            case "missing": command = command with { RawCommand = JsonSerializer.SerializeToElement(new { cmdType = "ROOM_SEAT", playerId = "alice" }) }; break;
            case "event-actor": payload["playerId"] = "intruder"; break;
            case "event-slot": payload["seat"] = "P2"; break;
        }
        gameEvent = gameEvent with { Event = gameEvent.Event with { Payload = payload } };
        var forged = original with { Commands = [command], Events = branch == "event-missing" ? [] : [gameEvent] };
        var errors = MatchRecoveryValidator.Validate(forged.RoomId, forged.LastEventSequence, forged.Commands, forged.Events,
            forged.PlayerViews, forged.AuthoritativeState, forged.CurrentTick);
        Assert.Contains(errors, e => e.Contains("room seat", StringComparison.Ordinal));
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default));
        Assert.Equal("P1", Assert.Single(original.AuthoritativeState!.Seats).Value);
    }

    [Fact]
    public async Task JsonbMemberOrderDoesNotChangeTheOriginalRoomSeatReplay()
    {
        var journal = new Journal(); var session = new MatchSession("seat-jsonb", new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("alice", default);
        var frame = Frame(journal.Entries);
        using var reordered = JsonDocument.Parse("{\"seat\":\"P1\",\"playerId\":\"alice\",\"cmdType\":\"ROOM_SEAT\"}");
        frame = frame with { Commands = [frame.Commands[0] with { RawCommand = reordered.RootElement.Clone() }] };
        var payload = new Dictionary<string, object?> { ["seat"] = "P1", ["playerId"] = "alice" };
        frame = frame with { Events = [frame.Events[0] with { Event = frame.Events[0].Event with { Payload = payload } }] };
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, json), json)!;
        Assert.Empty(MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
    }

    [Fact]
    public async Task ClientCannotInstallASeatAndItsRejectedInGameRawIntentStillReplays()
    {
        var journal = new Journal(); var session = new MatchSession("seat-client-guard", new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        foreach (var actor in new[] { "alice", "bob" })
        {
            var submitted = await session.SubmitDeckAsync(actor, "deck", submit,
                JsonSerializer.SerializeToElement(submit, submit.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
        }
        foreach (var actor in new[] { "alice", "bob" })
        {
            var ready = await session.ReadyAsync(actor, "ready", JsonSerializer.SerializeToElement(new { cmdType = "READY" }), default);
            Assert.True(ready.Accepted, ready.ErrorMessage);
        }
        var raw = Raw("intruder", "P2");
        var command = GameCommandJsonMapper.Map(raw);
        Assert.IsType<UnsupportedCommand>(command);
        var before = session.SnapshotFor("alice");
        var rejected = await session.SubmitAsync("alice", "client-forged-seat", command, raw, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(before.Tick, rejected.State.Tick);
        Assert.Equal(2, rejected.State.Seats.Count);
        Assert.DoesNotContain("intruder", rejected.State.Seats.Keys);
        Assert.Empty(rejected.Events);
        Assert.False(journal.Entries[^1].Accepted);
        Assert.Equal("ROOM_SEAT", journal.Entries[^1].CommandType);
        Assert.Equal(2, journal.Entries.Count(e => e.CommandType == "ROOM_SEAT" && e.Accepted));
        var frame = Frame(journal.Entries);
        Assert.Empty(MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
    }

    [Fact]
    public async Task PreGameClientSeatIntentCannotChangeTheRoomBeforeTheSecondPlayerJoins()
    {
        var journal = new Journal(); var session = new MatchSession("seat-before-game", new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("alice", default);
        var raw = Raw("intruder", "P2");
        var before = session.SnapshotFor("alice");
        await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("alice", "forged-before-game", GameCommandJsonMapper.Map(raw), raw, default));
        Assert.Equal(before.Tick, session.SnapshotFor("alice").Tick);
        Assert.Single(journal.Entries);
        await session.EnsurePlayerAsync("bob", default);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain("intruder", journal.Entries[^1].AuthoritativeState.Seats.Keys);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(Frame(journal.Entries), new CoreRuleEngine(), default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientCannotRetrieveAnAcceptedServerSeatThroughItsReservedIntent(bool cold)
    {
        var journal = new Journal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession("seat-intent-boundary", engine, journal);
        await session.EnsurePlayerAsync("alice", default); await session.EnsurePlayerAsync("bob", default);
        var deck = PreconstructedDeckCatalog.Build(await OfficialCardCatalog.LoadDefaultAsync()).Single(d => d.Id == "lillia-lowcurve").Decklist;
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        foreach (var actor in new[] { "alice", "bob" })
            Assert.True((await session.SubmitDeckAsync(actor, "deck", submit, JsonSerializer.SerializeToElement(submit, submit.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)), default)).Accepted);
        foreach (var actor in new[] { "alice", "bob" })
            Assert.True((await session.ReadyAsync(actor, "ready", JsonSerializer.SerializeToElement(new { cmdType = "READY" }), default)).Accepted);
        if (cold) session = MatchSession.Restore(Frame(journal.Entries), engine, NoopMatchJournal.Instance);
        var before = MatchStateHasher.HashValue(session.SnapshotFor("alice")); var count = journal.Entries.Count;
        var original = journal.Entries.First(e => e.PlayerId == "alice" && e.CommandType == "ROOM_SEAT");
        MatchSessionException? rejection = null; ResolutionResult? returned = null;
        try { returned = await session.SubmitAsync("alice", original.ClientIntentId, GameCommandJsonMapper.Map(original.RawCommand!.Value), original.RawCommand, default); }
        catch (MatchSessionException error) { rejection = error; }
        Assert.True(rejection is not null, $"Client obtained accepted={returned?.Accepted}, tick={returned?.State.Tick} from the original internal seat cache; cold={cold}.");
        Assert.Equal(ErrorCodes.ClientIntentConflict, rejection!.Code);
        Assert.Equal(count, journal.Entries.Count);
        Assert.Equal(before, MatchStateHasher.HashValue(session.SnapshotFor("alice")));
    }

    [Fact]
    public async Task ConcurrentJoinsWaitForTheOriginalDurableTransitionBeforePublishingTheirSeats()
    {
        var journal = new BlockingJournal(); var session = new MatchSession("seat-concurrent", new CoreRuleEngine(), journal);
        var first = session.EnsurePlayerAsync("alice", default).AsTask();
        await journal.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = session.EnsurePlayerAsync("bob", default).AsTask();
        Assert.False(second.IsCompleted);
        Assert.Throws<MatchSessionException>(() => session.SnapshotFor("alice"));
        Assert.Empty(journal.Entries);
        journal.Release.SetResult();
        var players = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "P1", "P2" }, players.Select(p => p.Seat));
        Assert.Equal(new[] { 1, 2 }, journal.Entries.Select(e => e.AuthoritativeState.Seats.Count));
        Assert.Equal(new long[] { 1, 2 }, journal.Entries.Select(e => e.CompletedTick));
        Assert.Equal(new long[] { 1, 2 }, journal.Entries.Select(e => e.CompletedEventSequence));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(Frame(journal.Entries), new CoreRuleEngine(), default));
    }

    [Fact]
    public async Task FailedJournalDoesNotInstallAnUnrecordedSeatOrConsumeTheFirstSlot()
    {
        var journal = new FailingJournal(); var session = new MatchSession("seat-failed-store", new CoreRuleEngine(), journal);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.EnsurePlayerAsync("alice", default));
        Assert.Throws<MatchSessionException>(() => session.SnapshotFor("alice"));
        Assert.Empty(journal.Entries);
        var player = await session.EnsurePlayerAsync("bob", default);
        Assert.Equal("P1", player.Seat);
        Assert.Single(journal.Entries);
        Assert.Equal("bob", Assert.Single(journal.Entries[0].AuthoritativeState.Seats).Key);
        Assert.Equal(1, journal.Entries[0].CompletedTick);
    }

    private static JsonElement Raw(string player, string seat) => JsonSerializer.SerializeToElement(new { cmdType = "ROOM_SEAT", playerId = player, seat });

    private static MatchRecoveryFrame Frame(IReadOnlyList<MatchJournalEntry> entries)
    {
        var last = entries[^1];
        var events = entries.SelectMany(e => e.Events.Select((item, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, item))).ToArray();
        var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, last.CompletedTick, last.CompletedEventSequence,
            p.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[p.Key]));
        var initial = MatchState.Create(last.RoomId) with { Seed = entries[0].OpeningSeed };
        return new(last.RoomId, last.CompletedTick, last.CompletedEventSequence, entries.Select(Command).ToArray(), events, views, [], last.AuthoritativeState, initial)
            { OpeningSeed = entries[0].OpeningSeed };
    }

    private static RecoveredCommand Command(MatchJournalEntry e) => new(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
        e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode };

    private class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public virtual ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class BlockingJournal : Journal
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            if (Entries.Count == 0) { Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
            await base.RecordAsync(entry, cancellationToken);
        }
    }

    private sealed class FailingJournal : Journal
    {
        private bool first = true;
        public override ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            if (first) { first = false; throw new InvalidOperationException("original journal failure"); }
            return base.RecordAsync(entry, cancellationToken);
        }
    }
}
