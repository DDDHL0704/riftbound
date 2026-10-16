using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CardPlayHistoryRecoveryGuardTests
{
    private const string Actor = "paired-339c-b";
    private const string Opponent = "paired-339c-a";
    private const string PlayedObject = "paired-339c-b-MAIN-SFD-065-221-32";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProductionRegistryRestoresTheActualOpeningWithBothHistoryStagesIntact()
    {
        var (initial, journal, session) = await ActualOpening();
        var final = journal.Entries[^1].AuthoritativeState;
        var frame = Frame(initial, journal, final);
        Assert.Empty(StructuralErrors(frame));
        var restored = await Registry(new Store(frame), new Journal()).GetOrCreateAsync(initial.RoomId, default);
        Assert.Equal(JsonSerializer.Serialize(session.SnapshotFor(Actor)), JsonSerializer.Serialize(restored.SnapshotFor(Actor)));
        Assert.Equal(JsonSerializer.Serialize(session.SnapshotFor(Opponent)), JsonSerializer.Serialize(restored.SnapshotFor(Opponent)));

        // A real continuation proves that the restored private state retains the
        // independently asserted history, rather than only matching a projection.
        var next = await restored.SubmitAsync(Actor, "recovered-pass", new PassPriorityCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "PASS_PRIORITY" }), default);
        Assert.True(next.Accepted, next.ErrorMessage);
        AssertHistory(next.State.CardPlays);
    }

    [Theory]
    [InlineData("FINALIZER_VALID_OPPONENT")]
    [InlineData("RESOLVER_VALID_OPPONENT")]
    [InlineData("BOTH_VALID_OPPONENT")]
    [InlineData("DELETE_FINALIZED")]
    [InlineData("DELETE_RESOLVED")]
    [InlineData("DELETE_BOTH")]
    [InlineData("FABRICATED_SECOND_PLAY")]
    [InlineData("EARLIER_TICK_AND_PLAY_ID")]
    public async Task ProductionRegistryRejectsStructurallyValidCausalHistoryForgery(string mutation)
    {
        var (initial, journal, _) = await ActualOpening();
        var original = journal.Entries[^1].AuthoritativeState;
        var history = original.CardPlays;
        var entry = Assert.Single(history.Resolved);
        var forged = mutation switch
        {
            "FINALIZER_VALID_OPPONENT" => history with { Finalized = [entry with { PlayerId = Opponent }] },
            "RESOLVER_VALID_OPPONENT" => history with { Resolved = [entry with { PlayerId = Opponent }] },
            "BOTH_VALID_OPPONENT" => history with {
                Finalized = [entry with { PlayerId = Opponent }], Resolved = [entry with { PlayerId = Opponent }] },
            "DELETE_FINALIZED" => history with { Finalized = [] },
            "DELETE_RESOLVED" => history with { Resolved = [] },
            "DELETE_BOTH" => history with { Finalized = [], Resolved = [] },
            "FABRICATED_SECOND_PLAY" => history with {
                Finalized = [entry, entry with { PlayId = "STACK-1-" + PlayedObject }],
                Resolved = [entry, entry with { PlayId = "STACK-1-" + PlayedObject }] },
            "EARLIER_TICK_AND_PLAY_ID" => history with {
                Finalized = [entry with { Tick = 7, PlayId = "STACK-7-" + PlayedObject }],
                Resolved = [entry with { Tick = 7, PlayId = "STACK-7-" + PlayedObject }] },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        await AssertCausalRejection(Frame(initial, journal, original), original with { CardPlays = forged });
    }

    [Fact]
    public async Task ProductionRegistryRejectsOldTurnHistoryInjectedIntoTheCurrentTurn()
    {
        var (initial, journal, session) = await ActualOpening();
        var oldHistory = journal.Entries[^1].AuthoritativeState.CardPlays;
        var state = journal.Entries[^1].AuthoritativeState;
        for (var i = 0; i < 2; i++)
            state = (await Submit(session, state.PriorityPlayerId!, new PassPriorityCommand(), "predict-pass-" + i)).State;
        var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        state = (await Submit(session, Actor, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []), "keep-top")).State;
        state = (await Submit(session, Actor, new EndTurnCommand(), "next-turn")).State;
        Assert.Equal(2, state.TurnNumber);
        Assert.Empty(state.CardPlays.Finalized);
        Assert.Empty(state.CardPlays.Resolved);
        var forged = state with { CardPlays = oldHistory with { TurnNumber = state.TurnNumber } };
        await AssertCausalRejection(Frame(initial, journal, state), forged);
    }

    private static async Task AssertCausalRejection(MatchRecoveryFrame original, MatchState forged)
    {
        Assert.NotEqual(MatchStateHasher.Hash(original.AuthoritativeState!), MatchStateHasher.Hash(forged));
        var bad = original with { AuthoritativeState = forged };
        // These mutations deliberately pass the independent structural checks.
        // The real registry must reject them using the unchanged accepted log.
        Assert.Empty(StructuralErrors(bad));
        var logBefore = JsonSerializer.Serialize(new { original.Commands, original.Events });
        var writes = new Journal();
        var store = new Store(bad);
        var registry = Registry(store, writes);
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync(original.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("replayed final state hash", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(original.RoomId, default));
        Assert.Empty(writes.Entries);
        Assert.Equal(logBefore, JsonSerializer.Serialize(new { bad.Commands, bad.Events }));

        // A failed attempt must not cache the forged session or prevent recovery
        // after the exact original state is supplied again.
        store.Frame = original;
        var restored = await registry.GetOrCreateAsync(original.RoomId, default);
        Assert.Equal(original.CurrentTick, restored.SnapshotFor(Actor).Tick);
        Assert.Empty(writes.Entries);
    }

    private static async Task<(MatchState Initial, Journal Journal, MatchSession Session)> ActualOpening()
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "card-play-history-current-opening-20261017.json")));
        var fixture = doc.RootElement;
        var initial = MatchReplayInitialStateBuilder.FromSeats(fixture.GetProperty("sourceRoomId").GetString()!,
            fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        foreach (var recorded in fixture.GetProperty("acceptedCommands").EnumerateArray())
        {
            var player = recorded.GetProperty("playerId").GetString()!;
            var intent = recorded.GetProperty("clientIntentId").GetString()!;
            var raw = recorded.GetProperty("rawCommand").Clone();
            var type = recorded.GetProperty("commandType").GetString();
            var result = type switch
            {
                "SUBMIT_DECK" => await session.SubmitDeckAsync(player, intent, raw.Deserialize<SubmitDeckCommand>(WebJson)!, raw, default),
                "READY" => await session.ReadyAsync(player, intent, raw, default),
                "MULLIGAN" => await session.SubmitAsync(player, intent, raw.Deserialize<MulliganCommand>(WebJson)!, raw, default),
                "TAP_RUNE" => await session.SubmitAsync(player, intent, raw.Deserialize<TapRuneCommand>(WebJson)!, raw, default),
                "PLAY_CARD" => await session.SubmitAsync(player, intent, raw.Deserialize<PlayCardCommand>(WebJson)!, raw, default),
                _ => throw new InvalidOperationException("Unexpected actual opening command: " + type)
            };
            Assert.True(result.Accepted, $"{type}: {result.ErrorMessage}");
        }
        Assert.Equal(9, journal.Entries.Count);
        Assert.All(journal.Entries, entry => Assert.True(entry.Accepted));
        AssertHistory(journal.Entries[^1].AuthoritativeState.CardPlays);
        return (initial, journal, session);
    }

    private static void AssertHistory(TurnCardPlayHistory history)
    {
        Assert.Equal(1, history.TurnNumber);
        foreach (var stage in new[] { history.Finalized, history.Resolved })
        {
            var entry = Assert.Single(stage);
            Assert.Equal(Actor, entry.PlayerId);
            Assert.Equal(PlayedObject, entry.Source.ObjectId);
            Assert.Equal(1, entry.Source.Generation);
            Assert.Equal("SFD·065/221", entry.CardNo);
            Assert.Equal(1, entry.Types);
            Assert.Equal(8, entry.Tick);
            Assert.Equal("STACK-8-" + PlayedObject, entry.PlayId);
            Assert.Equal(0, entry.FaceRevision);
        }
    }

    private static async Task<ResolutionResult> Submit(IMatchSession session, string player, GameCommand command, string intent)
    {
        var result = await session.SubmitAsync(player, intent, command,
            JsonSerializer.SerializeToElement(command, command.GetType(), WebJson), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState final) => new(initial.RoomId,
        final.Tick, journal.Entries[^1].CompletedEventSequence,
        journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
            { RuleInputs = e.RuleInputs }).ToArray(),
        journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray(),
        new Dictionary<string, RecoveredPlayerView>(), [], final, initial);

    private static IReadOnlyList<string> StructuralErrors(MatchRecoveryFrame f) => MatchRecoveryValidator.Validate(f.RoomId,
        f.LastEventSequence, f.Commands, f.Events, f.PlayerViews, f.AuthoritativeState, f.CurrentTick, f.SpectatorReplayFrame);

    private static InMemoryMatchSessionRegistry Registry(Store store, Journal journal) => new(new CoreRuleEngine(), journal, store);

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        internal MatchRecoveryFrame Frame { get; set; } = frame;
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(Frame);
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
