using System.Text.Json;
using Riftbound.Api.Hubs;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PrivateDeckSubmissionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly MatchSessionOptions Private = new(false)
    {
        HideSubmittedDeckIdentities = true
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmissionEventsUseTheRecordedRoomVisibilityPolicy(bool hide)
    {
        var journal = new Journal();
        var options = new MatchSessionOptions(false) { HideSubmittedDeckIdentities = hide };
        var session = NewSession(journal, options);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        var command = await Deck();
        var raw = Raw(command);
        var result = await session.SubmitDeckAsync("alice", "deck", command, raw, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var submitted = Assert.Single(result.Events);
        Assert.Equal("DECK_SUBMITTED", submitted.Kind);
        Assert.Equal("alice", submitted.Payload["playerId"]);
        Assert.Equal(command.MainDeck.Count, submitted.Payload["mainDeckCount"]);
        Assert.Equal(command.RuneDeck.Count, submitted.Payload["runeDeckCount"]);
        Assert.Equal(command.Battlefields.Count, submitted.Payload["battlefieldCount"]);
        Assert.Equal(!hide, submitted.Payload.ContainsKey("legendCardNo"));
        Assert.Equal(!hide, submitted.Payload.ContainsKey("championCardNo"));
        Assert.Equal(command.LegendCardNo, result.State.PlayerDecklists["alice"].LegendCardNo);
        Assert.Equal(command.ChampionCardNo, result.State.PlayerDecklists["alice"].ChampionCardNo);
        Assert.Equal(command.MainDeck, result.State.PlayerDecklists["alice"].MainDeck);
        Assert.Equal(raw.GetRawText(), journal.Entries[^1].RawCommand!.Value.GetRawText());

        if (hide)
        {
            Assert.Equal(4, submitted.Payload.Count);
            AssertPrivateViews(journal.Entries[^1], command);
        }
        else
        {
            Assert.Equal(6, submitted.Payload.Count);
            Assert.Equal(command.LegendCardNo, submitted.Payload["legendCardNo"]);
            Assert.Equal(command.ChampionCardNo, submitted.Payload["championCardNo"]);
            var projected = Assert.Single(GameEventObjectRefProjector.ProjectEvents(result.Events, result.State));
            Assert.Equal(command.LegendCardNo, projected.Payload["legendCardNo"]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdReconnectAndLaterSubmissionKeepTheOriginalPrivacyDespitePublicProcessDefaults(bool registry)
    {
        var journal = new Journal();
        var players = new PlayerStore();
        var live = NewSession(journal, Private, players);
        await live.EnsurePlayerAsync("alice", default);
        var bob = await live.EnsurePlayerAsync("bob", default);
        var command = await Deck();
        Assert.True((await live.SubmitDeckAsync("alice", "deck", command, Raw(command), default)).Accepted);
        var frame = RoundTrip(Frame(journal.Entries));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        var resumedJournal = new Journal();
        resumedJournal.Entries.AddRange(journal.Entries);
        IMatchSession cold = registry
            ? await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), resumedJournal, new RecoveryStore(frame),
                players, MatchSessionOptions.Default).GetOrCreateAsync(frame.RoomId, MatchSessionOptions.Default, default)
            : MatchSession.Restore(frame, new CoreRuleEngine(), resumedJournal, players, MatchSessionOptions.Default);
        var resumed = await cold.ReconnectPlayerAsync("bob", bob.ReconnectToken, default);
        Assert.Equal(bob.PlayerId, resumed.PlayerId);
        Assert.Equal(JsonSerializer.Serialize(live.SnapshotFor("bob"), Json),
            JsonSerializer.Serialize(cold.SnapshotFor("bob"), Json));
        AssertNoCardIdentities(cold.SnapshotFor("bob"), command);
        AssertNoCardIdentities(cold.PromptFor("bob"), command);

        var duplicate = await cold.SubmitDeckAsync("alice", "deck", command, Raw(command), default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Empty(duplicate.Events);
        Assert.Equal(3, resumedJournal.Entries.Count);
        var submitted = await cold.SubmitDeckAsync("bob", "deck", command, Raw(command), default);
        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        Assert.Equal(Private, submitted.State.SessionOptions);
        AssertPrivateViews(resumedJournal.Entries[^1], command);
        var resumedFrame = RoundTrip(Frame(resumedJournal.Entries));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(resumedFrame, new CoreRuleEngine(), default));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(resumedFrame.ReplayInitialState!, resumedFrame.Commands,
            resumedFrame.AuthoritativeState!, new CoreRuleEngine(), default, resumedFrame.Events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task PrivateSubmissionsStayHiddenUntilTheCoreOpeningPublishesThePublicRoleObjects()
    {
        var journal = new Journal();
        var session = NewSession(journal, Private);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        var command = await Deck();
        foreach (var player in new[] { "alice", "bob" })
        {
            Assert.True((await session.SubmitDeckAsync(player, "deck", command, Raw(command), default)).Accepted);
            AssertPrivateViews(journal.Entries[^1], command);
        }
        Assert.True((await session.ReadyAsync("alice", "ready", Raw(new ReadyCommand()), default)).Accepted);
        AssertPrivateViews(journal.Entries[^1], command);
        var opened = await session.ReadyAsync("bob", "ready", Raw(new ReadyCommand()), default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        foreach (var snapshot in opened.Snapshots.Values)
        {
            var strings = Strings(JsonSerializer.SerializeToElement(snapshot, Json)).ToArray();
            Assert.Contains(command.LegendCardNo, strings);
            Assert.Contains(command.ChampionCardNo, strings);
        }
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(opened);
        // Earlier spectator frames remain private even after the roles are public.
        foreach (var entry in journal.Entries.Where(entry => entry.AuthoritativeState.Status != MatchStatuses.InProgress))
            AssertPrivateViews(entry, command);
        var frame = RoundTrip(Frame(journal.Entries));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
    }

    [Fact]
    public async Task RejectedPrivateDeckDoesNotPublishIdentityOrChangeTheAcceptedBlueprint()
    {
        var journal = new Journal();
        var session = NewSession(journal, Private);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        var command = await Deck();
        var accepted = await session.SubmitDeckAsync("alice", "valid", command, Raw(command), default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var hash = MatchStateHasher.Hash(accepted.State);
        var invalid = command with { MainDeck = [] };
        var rejected = await session.SubmitDeckAsync("alice", "invalid", invalid, Raw(invalid), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, rejected.ErrorCode);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Empty(rejected.Events);
        AssertPrivateViews(journal.Entries[^1], command);
        var frame = RoundTrip(Frame(journal.Entries));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
    }

    [Fact]
    public async Task ChangingThePrivacyPolicyCannotRewriteTheOriginalHistoryOrItsEvents()
    {
        var journal = new Journal();
        var session = NewSession(journal, Private);
        await session.EnsurePlayerAsync("alice", default);
        var command = await Deck();
        Assert.True((await session.SubmitDeckAsync("alice", "deck", command, Raw(command), default)).Accepted);
        var frame = RoundTrip(Frame(journal.Entries));
        var changed = Private with { HideSubmittedDeckIdentities = false };
        var final = frame.AuthoritativeState! with { SessionOptions = changed };
        var exception = Assert.Throws<MatchSessionException>(() => MatchSession.Restore(
            frame with { AuthoritativeState = final }, new CoreRuleEngine(), NoopMatchJournal.Instance));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, exception.Code);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            frame.ReplayInitialState! with { SessionOptions = changed }, frame.Commands,
            final, new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch);
        AssertPrivateViews(journal.Entries[^1], command);
    }

    [Fact]
    public void DefaultPolicyPreservesTheExistingSerializedOptions()
    {
        Assert.False(MatchSessionOptions.Default.HideSubmittedDeckIdentities);
        Assert.Equal(JsonSerializer.Serialize(new { allowLegacyReadyWithoutDeck = true, deckFormat = OfficialDeckFormat.CoreRules }, Json),
            JsonSerializer.Serialize(MatchSessionOptions.Default, Json));
        Assert.DoesNotContain("hideSubmittedDeckIdentities", JsonSerializer.Serialize(MatchSessionOptions.Default, Json));
        Assert.Contains("hideSubmittedDeckIdentities", JsonSerializer.Serialize(Private, Json));
    }

    private static MatchSession NewSession(Journal journal, MatchSessionOptions options, IMatchPlayerStore? players = null) =>
        new("private-submission", new CoreRuleEngine(), journal, players ?? NoopMatchPlayerStore.Instance, options);

    private static async Task<SubmitDeckCommand> Deck()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog)[0].Decklist;
        return new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
    }

    private static void AssertPrivateViews(MatchJournalEntry entry, SubmitDeckCommand command)
    {
        AssertNoCardIdentities(entry.Events, command);
        AssertNoCardIdentities(entry.Snapshots, command);
        AssertNoCardIdentities(entry.Prompts, command);
        AssertNoCardIdentities(GameEventObjectRefProjector.ProjectEvents(entry.Events, entry.AuthoritativeState), command);
        AssertNoCardIdentities(MatchReplayRedactor.BuildSpectatorFrame(entry), command);
    }

    private static void AssertNoCardIdentities(object view, SubmitDeckCommand command)
    {
        var cardNos = command.MainDeck.Concat(command.RuneDeck).Concat(command.Battlefields)
            .Append(command.LegendCardNo).Append(command.ChampionCardNo).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var text in Strings(JsonSerializer.SerializeToElement(view, Json)))
        {
            Assert.NotEqual("sessionOptions", text);
            Assert.NotEqual("hideSubmittedDeckIdentities", text);
            Assert.DoesNotContain(cardNos, card => text.Contains(card, StringComparison.Ordinal));
        }
    }

    private static IEnumerable<string> Strings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) yield return value.GetString()!;
        else if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                yield return property.Name;
                foreach (var text in Strings(property.Value)) yield return text;
            }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                foreach (var text in Strings(item)) yield return text;
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
        var events = entries.SelectMany(entry => entry.Events.Select((item, index) =>
            new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, item))).ToArray();
        var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key, last.CompletedTick,
            last.CompletedEventSequence, pair.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[pair.Key]));
        var initial = MatchState.Create(last.RoomId) with
        {
            Seed = entries[0].OpeningSeed,
            SessionOptions = entries[0].AuthoritativeState.SessionOptions
        };
        return new(last.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState, initial)
        {
            OpeningSeed = entries[0].OpeningSeed,
            SpectatorReplayFrame = MatchReplayRedactor.BuildSpectatorFrame(last)
        };
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

    private sealed class PlayerStore : IMatchPlayerStore
    {
        private readonly Dictionary<(string Room, string Player), string> hashes = [];
        public ValueTask SavePlayerSessionAsync(string roomId, string playerId, string seat, string reconnectTokenHash,
            CancellationToken cancellationToken, long openingSeed = 0)
        { hashes[(roomId, playerId)] = reconnectTokenHash; return ValueTask.CompletedTask; }
        public async ValueTask CommitRoomSeatAsync(MatchJournalEntry entry, string reconnectTokenHash,
            IMatchJournal journal, CancellationToken cancellationToken)
        {
            await journal.RecordAsync(entry, cancellationToken);
            hashes[(entry.RoomId, entry.PlayerId)] = reconnectTokenHash;
        }
        public ValueTask<bool> HasReconnectTokenHashAsync(string roomId, string playerId, string reconnectTokenHash,
            CancellationToken cancellationToken) => ValueTask.FromResult(hashes.GetValueOrDefault((roomId, playerId)) == reconnectTokenHash);
    }
}
