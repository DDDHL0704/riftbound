using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OpeningRandomizationTests
{
    private static readonly MethodInfo ShuffleMethod = typeof(MatchSession)
        .GetMethod("RandomizeStable", BindingFlags.NonPublic | BindingFlags.Static)!
        .MakeGenericMethod(typeof(string));

    [Fact]
    public void HistoricalNativeAcceptedHistoryRemainsByteExactBesideTheNewCurrentFixture()
    {
        const string originalHash = "f3c2a8495ee9dd9eef80badba6222c9f0d9ea49ec19eacb1d62ff4ddb9b12317";
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var original = File.ReadAllBytes(Path.Combine(folder, "card-play-history-actual-opening-339c.json"));
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant());
        using var current = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "card-play-history-current-opening-20261017.json")));
        Assert.Equal(originalHash, current.RootElement.GetProperty("originalHistoricalFixture").GetProperty("sha256").GetString());
        Assert.Equal(0, current.RootElement.GetProperty("openingSeed").GetInt64());
        Assert.Equal(9, current.RootElement.GetProperty("acceptedCommands").GetArrayLength());
    }

    // Public submitted blueprint from the independently measured counterexample;
    // each copy receives its original, distinct physical identity.
    private static string[] MainCards() => new[] {
        "SFD·198/221", "SFD·033/221", "OGN·044/298", "OGN·052/298", "OGN·210/298",
        "OGN·216/298", "OGN·217/298", "SFD·036/221", "SFD·041/221", "SFD·154/221",
        "SFD·159/221", "SFD·167/221", "UNL-036/219"
    }.SelectMany(card => Enumerable.Repeat(card, 3)).ToArray();

    [Theory]
    [InlineData("shuffle-probe-a", 1L)]
    [InlineData("shuffle-probe-b", 4L)]
    public void OpeningMainDeckDoesNotReproduceTheMeasuredPhysicalCopyClustering(string player, long cursor)
    {
        var cards = MainCards();
        var values = cards.Select((card, index) => player + "-MAIN-"
            + new string(card.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-')
            + "-" + (index + 1).ToString("00", CultureInfo.InvariantCulture)).ToArray();
        var copies = values.Take(3).ToHashSet(StringComparer.Ordinal);
        var random = new Random(20261017);
        const int samples = 20_000;
        var opening = 0; var topTen = 0; var contiguous = 0; var openingCopies = 0;
        for (var sample = 0; sample < samples; sample++)
        {
            var shuffled = Shuffle(values, random.NextInt64(long.MaxValue), cursor, player + ":MAIN_DECK");
            Assert.Equal(values.Order(StringComparer.Ordinal), shuffled.Order(StringComparer.Ordinal));
            var positions = shuffled.Select((value, index) => (value, index))
                .Where(x => copies.Contains(x.value)).Select(x => x.index).ToArray();
            Assert.Equal(3, positions.Length);
            if (positions[^1] < 4) opening++;
            if (positions[^1] < 10) topTen++;
            if (positions[^1] - positions[0] == 2) contiguous++;
            openingCopies += positions.Count(position => position < 4);
        }
        // Independent uniform-permutation references: 4/9139, 120/9139,
        // 37/9139 and 12/39. Wide bounds are regression guards, not a
        // claim that finite samples prove a perfectly uniform generator.
        Assert.InRange(opening, 0, 30);
        Assert.InRange(topTen, 150, 400);
        Assert.InRange(contiguous, 40, 140);
        Assert.InRange((double)openingCopies / samples, 0.28, 0.34);
    }

    [Fact]
    public async Task FreshProductionSessionsDoNotShareThePublicRoomDerivedSeed()
    {
        var seeds = new HashSet<long>();
        for (var index = 0; index < 8; index++)
        {
            var session = new MatchSession("same-public-opening-room", new CoreRuleEngine());
            session.EnsurePlayer("alice"); session.EnsurePlayer("bob");
            var ready = await session.ReadyAsync("alice", "ready", Raw(new ReadyCommand()), default);
            Assert.True(ready.Accepted, ready.ErrorMessage);
            Assert.NotEqual(0, ready.State.Seed);
            Assert.True(seeds.Add(ready.State.Seed), "Fresh production rooms must use independent server entropy.");
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(424242L)]
    [InlineData(-9223372036854775807L)]
    public async Task ExplicitReplaySeedRemainsAnExactOriginalInputIncludingZero(long seed)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).First().Decklist;
        var initial = MatchReplayInitialStateBuilder.FromSeats("explicit-opening-seed",
            new Dictionary<string, string> { ["alice"] = "P1", ["bob"] = "P2" }) with { Seed = seed };
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo,
            deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        foreach (var player in initial.Seats.Keys)
        {
            var submitted = await session.SubmitDeckAsync(player, "deck", command, Raw(command), default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
            Assert.Equal(seed, submitted.State.Seed);
        }
        await session.ReadyAsync("alice", "ready", Raw(new ReadyCommand()), default);
        var opened = await session.ReadyAsync("bob", "ready", Raw(new ReadyCommand()), default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        Assert.Equal(seed, opened.State.Seed);
        Assert.Equal(7, opened.State.RngCursor);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(opened);
    }

    [Theory]
    [InlineData("TURN_ORDER", 2)]
    [InlineData("alice:MAIN_DECK", 39)]
    [InlineData("alice:RUNE_DECK", 12)]
    [InlineData("alice:BATTLEFIELDS", 3)]
    public void OpeningPermutationDoesNotDependOnPhysicalCardNames(string scope, int count)
    {
        var values = Enumerable.Range(0, count).Select(i => "card-" + i).ToArray();
        var renamed = Enumerable.Range(0, count).Select(i => "unrelated-physical-identity-" + i).ToArray();
        var first = Shuffle(values, 424242, 7, scope).Select(value => Array.IndexOf(values, value));
        var second = Shuffle(renamed, 424242, 7, scope).Select(value => Array.IndexOf(renamed, value));
        Assert.Equal(first, second);
        Assert.Equal(values, Enumerable.Range(0, count).Select(i => "card-" + i));
    }

    public static IEnumerable<object[]> StableVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "opening-randomization-v1.json")));
        return document.RootElement.GetProperty("cases").EnumerateArray().Select(row => new object[] {
            row.GetProperty("seed").GetInt64(), row.GetProperty("cursor").GetInt64(),
            row.GetProperty("scope").GetString()!, row.GetProperty("count").GetInt32(),
            row.GetProperty("permutation").EnumerateArray().Select(x => x.GetInt32()).ToArray()
        }).ToArray();
    }

    [Theory]
    [MemberData(nameof(StableVectors))]
    public void ExplicitOpeningInputsMatchIndependentCrossPlatformVectors(long seed, long cursor,
        string scope, int count, int[] expected)
    {
        var values = Enumerable.Range(0, count).Select(i => "physical-" + i).ToArray();
        var original = values.ToArray();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(expected, Shuffle(values, seed, cursor, scope).Select(value => Array.IndexOf(values, value)));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(expected, Shuffle(values, seed, cursor, scope).Select(value => Array.IndexOf(values, value)));
            Assert.Equal(original, values);
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Theory]
    [InlineData("TURN_ORDER", 2)]
    [InlineData("alice:RUNE_DECK", 12)]
    [InlineData("alice:BATTLEFIELDS", 3)]
    public void OtherOpeningConsumersHaveNoFixedFirstObjectAcrossIndependentSeeds(string scope, int count)
    {
        var values = Enumerable.Range(0, count).Select(i => "physical-" + i).ToArray();
        var observed = new int[count]; var random = new Random(20261017);
        const int samples = 12_000;
        for (var sample = 0; sample < samples; sample++)
        {
            var result = Shuffle(values, random.NextInt64(long.MaxValue), 0, scope);
            observed[Array.IndexOf(values, result[0])]++;
        }
        var expected = (double)samples / count;
        foreach (var total in observed) Assert.InRange(total, (int)(expected * 0.75), (int)(expected * 1.25));
    }

    [Fact]
    public async Task AllFourActualOpeningConsumersUseTheirBoundSeedCursorAndScope()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).First().Decklist;
        var initial = MatchReplayInitialStateBuilder.FromSeats("four-opening-consumers",
            new Dictionary<string, string> { ["alice"] = "P1", ["bob"] = "P2" }) with { Seed = 424242 };
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo,
            deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        foreach (var player in initial.Seats.Keys)
            Assert.True((await session.SubmitDeckAsync(player, "deck", command, Raw(command), default)).Accepted);
        Assert.True((await session.ReadyAsync("alice", "ready", Raw(new ReadyCommand()), default)).Accepted);
        var result = await session.ReadyAsync("bob", "ready", Raw(new ReadyCommand()), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var state = result.State;
        int[] Expected(string scope) => (int[])StableVectors().Single(row => (long)row[0] == 424242 && (string)row[2] == scope)[4];
        var players = new[] { "alice", "bob" };
        Assert.Equal(players[Expected("TURN_ORDER")[0]], state.ActivePlayerId);
        Assert.Equal(players[Expected("TURN_ORDER")[1]], state.OpeningSecondActionPlayerId);
        foreach (var player in players)
        {
            string[] Physical(string zone) => state.CardObjects.Values
                .Where(card => card.OwnerId == player && card.ObjectId.StartsWith(player + "-" + zone + "-", StringComparison.Ordinal))
                .OrderBy(card => int.Parse(card.ObjectId[(card.ObjectId.LastIndexOf('-') + 1)..], CultureInfo.InvariantCulture))
                .Select(card => card.ObjectId).ToArray();
            var main = Physical("MAIN"); var runes = Physical("RUNE"); var zones = state.PlayerZones[player];
            Assert.Equal(39, main.Length); Assert.Equal(12, runes.Length);
            Assert.Equal(Expected(player + ":MAIN_DECK").Select(index => main[index]), zones.Hand.Concat(zones.MainDeck));
            Assert.Equal(Expected(player + ":RUNE_DECK").Select(index => runes[index]), zones.RuneDeck);
            Assert.Equal(deck.Battlefields[Expected(player + ":BATTLEFIELDS")[0]], state.CardObjects[zones.Battlefields.Single()].CardNo);
        }
        Assert.Equal(424242, state.Seed); Assert.Equal(7, state.RngCursor);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }

    [Fact]
    public async Task PrivateOriginalSeedSurvivesColdReplayAndIndependentTamperChecks()
    {
        var engine = new CoreRuleEngine(); var journal = new Journal(); var store = new PlayerStore();
        IMatchSession session = new MatchSession("private-opening-replay", engine, journal, store);
        await session.EnsurePlayerAsync("alice", default); await session.EnsurePlayerAsync("bob", default);
        var seed = Assert.Single(store.Seeds.Distinct());
        Assert.NotEqual(0, seed);
        // Durable room entries now precede both original deck submissions.
        var initial = MatchState.Create(session.RoomId) with { Seed = seed };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).First().Decklist;
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo,
            deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        foreach (var player in new[] { "alice", "bob" })
            Assert.True((await session.SubmitDeckAsync(player, "deck", command, Raw(command), default)).Accepted);
        Assert.True((await session.ReadyAsync("alice", "ready", Raw(new ReadyCommand()), default)).Accepted);
        var result = await session.ReadyAsync("bob", "ready", Raw(new ReadyCommand()), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.All(journal.Entries, entry => Assert.Equal(seed, entry.OpeningSeed));
        var frame = Frame(initial, journal, result.State);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default));
        session = await new InMemoryMatchSessionRegistry(engine, journal, new RecoveryStore(frame), store)
            .GetOrCreateAsync(session.RoomId, default);
        Assert.Equal(JsonSerializer.Serialize(result.Snapshots["alice"]), JsonSerializer.Serialize(session.SnapshotFor("alice")));
        var mulligan = new MulliganCommand([]);
        var continued = await session.SubmitAsync(result.State.ActivePlayerId, "mulligan", mulligan, Raw(mulligan), default);
        Assert.True(continued.Accepted, continued.ErrorMessage);
        Assert.Equal(seed, journal.Entries[^1].OpeningSeed);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(Frame(initial, journal, continued.State), engine, default));

        var changed = seed ^ 1;
        foreach (var forged in new[] {
            frame with { ReplayInitialState = initial with { Seed = changed } },
            frame with { OpeningSeed = changed },
            frame with { OpeningSeed = changed, ReplayInitialState = initial with { Seed = changed } }
        })
        {
            var before = MatchStateHasher.Hash(frame.AuthoritativeState!);
            var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, engine, default);
            Assert.NotEmpty(errors);
            Assert.DoesNotContain(seed.ToString(CultureInfo.InvariantCulture), string.Join("; ", errors), StringComparison.Ordinal);
            Assert.Throws<MatchSessionException>(() => MatchSession.Restore(forged, engine, NoopMatchJournal.Instance));
            await Assert.ThrowsAsync<MatchSessionException>(async () => await new InMemoryMatchSessionRegistry(
                engine, NoopMatchJournal.Instance, new RecoveryStore(forged), store).GetOrCreateAsync(frame.RoomId, default));
            Assert.Equal(before, MatchStateHasher.Hash(frame.AuthoritativeState!));
        }
        var publicPayload = JsonSerializer.Serialize(new { result.Snapshots, result.Prompts, result.Events,
            spectator = MatchReplayRedactor.BuildSpectatorFrame(journal.Entries[^1]) });
        Assert.DoesNotContain(seed.ToString(CultureInfo.InvariantCulture), publicPayload, StringComparison.Ordinal);
        using var publicJson = JsonDocument.Parse(publicPayload);
        CheckPrivateFields(publicJson.RootElement);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }

    private static void CheckPrivateFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                Assert.DoesNotContain("seed", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.NotEqual("rngCursor", property.Name);
                CheckPrivateFields(property.Value);
            }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckPrivateFields(item);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(9223372036854775807L)]
    public async Task EmptyWaitingRoomRecoveryBindsThePrivateSeedBeforeAnyCommand(long seed)
    {
        var initial = MatchReplayInitialStateBuilder.FromSeats("waiting-opening-input",
            new Dictionary<string, string> { ["alice"] = "P1", ["bob"] = "P2" }) with { Seed = seed };
        var frame = new MatchRecoveryFrame(initial.RoomId, 0, 0, [], [],
            new Dictionary<string, RecoveredPlayerView>(), [], initial) { OpeningSeed = seed };
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        var wrong = frame with { OpeningSeed = seed ^ 1 };
        Assert.Contains("waiting-room state seed differs from recorded opening seed",
            await MatchActionLogReplayer.ValidateRecoveryFrameAsync(wrong, new CoreRuleEngine(), default));
        await Assert.ThrowsAsync<MatchSessionException>(async () => await new InMemoryMatchSessionRegistry(
            new CoreRuleEngine(), NoopMatchJournal.Instance, new RecoveryStore(wrong)).GetOrCreateAsync(initial.RoomId, default));
    }

    [Theory]
    [InlineData(2, 0UL, 0, 1)]
    [InlineData(3, 0UL, 1, 2)]
    [InlineData(39, 0UL, 16, 2)]
    [InlineData(39, 15UL, 16, 2)]
    [InlineData(39, ulong.MaxValue, 15, 1)]
    public void OpeningBoundedDrawRejectsOnlyTheIncompleteModuloBucket(int bound, ulong first, int expected, int calls)
    {
        // 2^64 mod 2 = 0, mod 3 = 1, mod 39 = 16; independent integer references.
        var count = 0;
        ulong Next() { count++; return count == 1 ? first : (ulong)expected; }
        var method = typeof(MatchSession).GetMethod("NextOpeningIndex", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(expected, (int)method.Invoke(null, [bound, (Func<ulong>)Next])!);
        Assert.Equal(calls, count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializedRecoveryAndJournalCannotOmitTheRecordedOpeningSeed(bool journalPayload)
    {
        var journal = new Journal();
        var session = new MatchSession("required-private-opening-input", new CoreRuleEngine(), journal);
        session.EnsurePlayer("alice"); session.EnsurePlayer("bob");
        var result = await session.ReadyAsync("alice", "ready", Raw(new ReadyCommand()), default);
        Assert.True(result.Accepted);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var initial = MatchReplayInitialStateBuilder.FromSeats(session.RoomId, result.State.Seats) with { Seed = result.State.Seed };
        var node = journalPayload ? JsonSerializer.SerializeToNode(journal.Entries.Single(), options)!
            : JsonSerializer.SerializeToNode(Frame(initial, journal, result.State), options)!;
        Assert.True(node.AsObject().Remove("openingSeed"));
        if (journalPayload) Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MatchJournalEntry>(node.ToJsonString(), options));
        else Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MatchRecoveryFrame>(node.ToJsonString(), options));
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class PlayerStore : IMatchPlayerStore
    {
        internal List<long> Seeds { get; } = [];
        public ValueTask SavePlayerSessionAsync(string room, string player, string seat, string hash,
            CancellationToken token, long openingSeed = 0)
        { Seeds.Add(openingSeed); return ValueTask.CompletedTask; }
        public async ValueTask CommitRoomSeatAsync(MatchJournalEntry entry, string reconnectTokenHash,
            IMatchJournal journal, CancellationToken cancellationToken)
        {
            await SavePlayerSessionAsync(entry.RoomId, entry.PlayerId, entry.AuthoritativeState.Seats[entry.PlayerId],
                reconnectTokenHash, cancellationToken, entry.OpeningSeed);
            await journal.RecordAsync(entry, cancellationToken);
        }

        public ValueTask<bool> HasReconnectTokenHashAsync(string room, string player, string hash, CancellationToken token)
            => ValueTask.FromResult(false);
    }

    private sealed class RecoveryStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState state)
    {
        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
            entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage)
            { TypedCommand = entry.TypedCommand, RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((gameEvent, index) =>
            new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, gameEvent))).ToArray();
        var last = journal.Entries[^1];
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, last.CompletedEventSequence, last.Snapshots[player], state.Tick, last.CompletedEventSequence,
            last.Prompts[player]));
        return new(state.RoomId, state.Tick, last.CompletedEventSequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(last)) { OpeningSeed = initial.Seed };
    }

    private static IReadOnlyList<string> Shuffle(string[] values, long seed, long cursor, string scope)
        => (IReadOnlyList<string>)ShuffleMethod.Invoke(null, [values, seed, cursor, scope])!;

    private static JsonElement Raw(GameCommand command)
        => JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
