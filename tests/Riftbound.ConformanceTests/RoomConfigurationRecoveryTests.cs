using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RoomConfigurationRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly MatchSessionOptions Standard = new(false, OfficialDeckFormat.ChinaStandard20260724);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdRestoreKeepsTheRecordedDeckFormatInsteadOfProcessDefaults(bool registry)
    {
        var journal = new Journal();
        var live = NewSession(journal, Standard);
        await live.EnsurePlayerAsync("alice", default);
        var frame = RoundTrip(Frame(journal.Entries));
        Assert.Equal(Standard, frame.AuthoritativeState!.SessionOptions);
        Assert.Equal(Standard, frame.ReplayInitialState!.SessionOptions);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));

        IMatchSession cold = registry
            ? await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new RecoveryStore(frame))
                .GetOrCreateAsync(frame.RoomId, default)
            : MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var ready = await cold.ReadyAsync("alice", "missing-deck", Raw(new ReadyCommand()), default);
        Assert.False(ready.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, ready.ErrorCode);
        var banned = await BannedDeck();
        var rejected = await cold.SubmitDeckAsync("alice", "banned-deck", banned, Raw(banned), default);
        Assert.False(rejected.Accepted);
        Assert.Contains("荣耀竞技场", rejected.ErrorMessage);
        Assert.Equal(Standard, rejected.State.SessionOptions);
    }

    [Fact]
    public async Task OriginalDefaultRoomSurvivesStricterProcessDefaultsWithoutChangingItsSerializedState()
    {
        var journal = new Journal();
        var live = NewSession(journal, MatchSessionOptions.Default);
        await live.EnsurePlayerAsync("alice", default);
        var frame = RoundTrip(Frame(journal.Entries));
        Assert.Null(frame.AuthoritativeState!.SessionOptions);
        Assert.DoesNotContain("sessionOptions", JsonSerializer.Serialize(frame.AuthoritativeState, Json));
        var cold = MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, Standard);
        var result = await cold.ReadyAsync("alice", "legacy-ready", Raw(new ReadyCommand()), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.SessionOptions);
    }

    [Fact]
    public async Task RejectedBannedDeckAndAcceptedOpeningReplayWithTheOriginalConfiguration()
    {
        var journal = new Journal();
        var live = NewSession(journal, Standard);
        await live.EnsurePlayerAsync("alice", default);
        await live.EnsurePlayerAsync("bob", default);
        var banned = await BannedDeck();
        var rejected = await live.SubmitDeckAsync("alice", "banned", banned, Raw(banned), default);
        Assert.False(rejected.Accepted);

        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog, Standard.DeckFormat)[0].Decklist;
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        foreach (var player in new[] { "alice", "bob" })
        {
            var submitted = await live.SubmitDeckAsync(player, "valid", command, Raw(command), default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
            Assert.Equal(Standard, submitted.State.SessionOptions);
        }
        Assert.True((await live.ReadyAsync("alice", "ready", Raw(new ReadyCommand()), default)).Accepted);
        var opened = await live.ReadyAsync("bob", "ready", Raw(new ReadyCommand()), default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        Assert.Equal(Standard, opened.State.SessionOptions);

        foreach (var count in Enumerable.Range(1, journal.Entries.Count))
        {
            var frame = RoundTrip(Frame(journal.Entries.Take(count).ToArray()));
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(frame.ReplayInitialState!, frame.Commands,
                frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
    }

    [Fact]
    public async Task PrivateConfigurationDoesNotAppearInViewsPromptsEventsOrSpectatorReplay()
    {
        var journal = new Journal();
        var session = NewSession(journal, Standard);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        var last = journal.Entries[^1];
        Assert.Contains("sessionOptions", JsonSerializer.Serialize(last.AuthoritativeState, Json));
        Assert.DoesNotContain("sessionOptions", JsonSerializer.Serialize(last.Snapshots, Json));
        Assert.DoesNotContain("sessionOptions", JsonSerializer.Serialize(last.Prompts, Json));
        Assert.DoesNotContain("sessionOptions", JsonSerializer.Serialize(last.Events, Json));
        Assert.DoesNotContain("sessionOptions", JsonSerializer.Serialize(MatchReplayRedactor.BuildSpectatorFrame(last), Json));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownOrChangedConfigurationCannotBeRestoredOrReplayed(bool unknown)
    {
        var journal = new Journal();
        var session = NewSession(journal, Standard);
        await session.EnsurePlayerAsync("alice", default);
        var frame = Frame(journal.Entries);
        var invalid = unknown ? Standard with { DeckFormat = (OfficialDeckFormat)999 } : MatchSessionOptions.Default;
        frame = frame with { AuthoritativeState = frame.AuthoritativeState! with { SessionOptions = invalid } };
        if (unknown)
        {
            frame = frame with { ReplayInitialState = frame.ReplayInitialState! with { SessionOptions = invalid } };
            var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands,
                frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick);
            Assert.Contains(errors, error => error.Contains("room deck format", StringComparison.Ordinal));
        }
        var exception = Assert.Throws<MatchSessionException>(() =>
            MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, exception.Code);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(frame.ReplayInitialState!, frame.Commands,
            frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch);
    }

    [Fact]
    public async Task DevelopmentScenarioKeepsTheRecordedRoomConfiguration()
    {
        var journal = new Journal();
        var session = NewSession(journal, Standard);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        var seeded = await session.SeedScenarioAsync("alice", "scenario", "basic-play", null, default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        Assert.Equal(Standard, seeded.State.SessionOptions);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(Frame(journal.Entries), new CoreRuleEngine(), default));
    }

    [Fact]
    public void NewRoomsRejectUnknownFormatsBeforeAnyJournalTransition() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewSession(new Journal(), new(false, (OfficialDeckFormat)999)));

    private static MatchSession NewSession(Journal journal, MatchSessionOptions options) =>
        new("room-configuration", new CoreRuleEngine(), journal, NoopMatchPlayerStore.Instance, options);

    private static async Task<SubmitDeckCommand> BannedDeck()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog, Standard.DeckFormat)[0].Decklist;
        var banned = catalog.Cards.First(card => card.CardName == "荣耀竞技场");
        return new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck,
            deck.Battlefields.Skip(1).Append(banned.CardNo).ToArray());
    }

    private static JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), Json);
    private static MatchRecoveryFrame RoundTrip(MatchRecoveryFrame frame) =>
        JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;

    private static MatchRecoveryFrame Frame(IReadOnlyList<MatchJournalEntry> entries)
    {
        var last = entries[^1];
        var commands = entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId, entry.CommandType,
            entry.RawCommand, entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence,
            entry.Accepted, entry.ErrorMessage) { TypedCommand = entry.TypedCommand, ErrorCode = entry.ErrorCode }).ToArray();
        var events = entries.SelectMany(entry => entry.Events.Select((item, i) =>
            new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, item))).ToArray();
        var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key, last.CompletedTick,
            last.CompletedEventSequence, pair.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[pair.Key]));
        var initial = MatchState.Create(last.RoomId) with
        {
            Seed = entries[0].OpeningSeed,
            SessionOptions = entries[0].AuthoritativeState.SessionOptions
        };
        return new(last.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState, initial)
            { OpeningSeed = entries[0].OpeningSeed };
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class RecoveryStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
