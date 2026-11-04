using System.Text;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SealedRoomPoolRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(null, 1, false)]
    [InlineData(null, 1, true)]
    [InlineData("OGN", 1, false)]
    [InlineData("OGN", 1, true)]
    [InlineData("SFD", 3, false)]
    [InlineData("SFD", 3, true)]
    public async Task ColdReconnectAfterJsonObjectKeyReorderingPreservesSeatSnapshots(
        string? setCode, int bestOf, bool bobSeatsFirst)
    {
        var engine = new CoreRuleEngine();
        var journal = new Journal();
        var players = new PlayerStore();
        var options = setCode is null ? new MatchSessionOptions(false, OfficialDeckFormat.ChinaStandard20260724)
            : MatchSessionOptions.ForSealed(setCode, bestOf);
        var live = new MatchSession("reordered-seat-snapshots", engine, journal, players, options);
        var firstId = bobSeatsFirst ? "bob" : "alice";
        var secondId = bobSeatsFirst ? "alice" : "bob";
        var first = await live.EnsurePlayerAsync(firstId, default);
        live = MatchSession.Restore(RoundTripWithReorderedObjectKeys(Frame(journal.Entries)), engine, journal, players);
        await live.ReconnectPlayerAsync(firstId, first.ReconnectToken, default);
        var second = await live.EnsurePlayerAsync(secondId, default);

        var original = Frame(journal.Entries);
        var frame = RoundTripWithReorderedObjectKeys(original);
        // PostgreSQL JSONB may enumerate "bob" before "alice", independently of their seats.
        Assert.Equal(["bob", "alice"], frame.AuthoritativeState!.Seats.Keys);
        Assert.Equal(MatchStateHasher.Hash(original.AuthoritativeState!), MatchStateHasher.Hash(frame.AuthoritativeState));
        Assert.Empty(Validate(frame));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default));

        var cold = MatchSession.Restore(frame, engine, NoopMatchJournal.Instance, players);
        await cold.ReconnectPlayerAsync(secondId, second.ReconnectToken, default);
        Assert.Equal([firstId, secondId], ResolutionResult.BuildSnapshots(frame.AuthoritativeState).Keys);
        foreach (var player in new[] { firstId, secondId })
        {
            Assert.Equal(JsonSerializer.Serialize(live.SnapshotFor(player), Json),
                JsonSerializer.Serialize(cold.SnapshotFor(player), Json));
            Assert.Equal(JsonSerializer.Serialize(live.PromptFor(player), Json),
                JsonSerializer.Serialize(cold.PromptFor(player), Json));
            Assert.Equal([firstId, secondId], cold.SnapshotFor(player).Players.Keys);
        }
        Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(original.AuthoritativeState!), Json),
            JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(frame.AuthoritativeState), Json));
    }

    [Theory]
    [InlineData("OGN", 1)]
    [InlineData("SFD", 3)]
    public async Task RealSeatJournalReplaysPrivatePoolsFromTheOriginalEmptyRoom(string setCode, int bestOf)
    {
        var journal = new Journal();
        var players = new PlayerStore();
        var live = new MatchSession("sealed-private-pools", new CoreRuleEngine(), journal, players,
            MatchSessionOptions.ForSealed(setCode, bestOf));
        var alice = await live.EnsurePlayerAsync("alice", default);
        var first = journal.Entries[0].AuthoritativeState.PlayerSealedPools!["alice"];
        await live.EnsurePlayerAsync("bob", default);
        var frame = RoundTrip(Frame(journal.Entries));
        Assert.Empty(frame.ReplayInitialState!.Seats);
        Assert.Null(frame.ReplayInitialState.PlayerSealedPools);
        Assert.Equal(2, frame.Commands.Count);
        Assert.True(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        var pools = frame.AuthoritativeState!.PlayerSealedPools!;
        Assert.Equal(JsonSerializer.Serialize(first, Json), JsonSerializer.Serialize(pools["alice"], Json));
        Assert.NotEqual(pools["alice"].Seed, pools["bob"].Seed);
        Assert.NotEqual(JsonSerializer.Serialize(pools["alice"].Packs, Json), JsonSerializer.Serialize(pools["bob"].Packs, Json));
        Assert.All(pools.Values, pool =>
        {
            Assert.Equal(6, pool.Packs.Count);
            Assert.Equal(78, pool.Pool.Values.Sum());
            Assert.Equal(SealedSetCatalog.Sha256, pool.ConfigurationSha256);
        });
        var cold = MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance, players);
        await cold.ReconnectPlayerAsync("alice", alice.ReconnectToken, default);
        Assert.Equal(JsonSerializer.Serialize(live.SnapshotFor("alice"), Json), JsonSerializer.Serialize(cold.SnapshotFor("alice"), Json));
        Assert.Equal(JsonSerializer.Serialize(live.SnapshotFor("bob"), Json), JsonSerializer.Serialize(cold.SnapshotFor("bob"), Json));
        var restored = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new RecoveryStore(frame))
            .GetOrCreateAsync(frame.RoomId, MatchSessionOptions.Default, default);
        Assert.Equal(JsonSerializer.Serialize(live.SnapshotFor("alice"), Json), JsonSerializer.Serialize(restored.SnapshotFor("alice"), Json));
    }

    [Fact]
    public async Task MixedPackPoolsRetainOrderAndCardCountsAcrossColdRecoveryAndReplay()
    {
        var choices = new[] { new SealedPackChoiceDto("OGN", 3), new SealedPackChoiceDto("SFD", 3) };
        var options = MatchSessionOptions.ForSealed(choices);
        var journal = new Journal();
        var players = new PlayerStore();
        var live = new MatchSession("mixed-sealed-pools", new CoreRuleEngine(), journal, players, options);
        var alice = await live.EnsurePlayerAsync("alice", default);
        await live.EnsurePlayerAsync("bob", default);
        var frame = RoundTripWithReorderedObjectKeys(Frame(journal.Entries));

        Assert.True(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        Assert.Equal(options, frame.ReplayInitialState!.SessionOptions);
        foreach (var pool in frame.AuthoritativeState!.PlayerSealedPools!.Values)
        {
            Assert.Equal(string.Empty, pool.SetCode);
            Assert.Equal(78, pool.Pool.Values.Sum());
            Assert.Equal(["OGN", "OGN", "OGN", "SFD", "SFD", "SFD"], pool.Packs.Select(pack => pack.SetCode));
            Assert.All(pool.Packs, pack => Assert.All(pack.Cards,
                card => Assert.StartsWith(pack.SetCode + "·", card.CardNo)));
            Assert.Equal(pool.Pool.OrderBy(entry => entry.Key), pool.Packs.SelectMany(pack => pack.PoolCardNos)
                .GroupBy(cardNo => cardNo, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal));
        }
        Assert.NotEqual(frame.AuthoritativeState.PlayerSealedPools["alice"].Seed,
            frame.AuthoritativeState.PlayerSealedPools["bob"].Seed);
        Assert.Equal(choices, live.SnapshotFor("alice").SealedPacks);
        Assert.Equal(string.Empty, live.SnapshotFor("alice").SealedPool!.SetCode);
        foreach (var player in new[] { "alice", "bob" })
        {
            var other = player == "alice" ? "bob" : "alice";
            var ownPool = live.SnapshotFor(player).SealedPool;
            Assert.Equal(JsonSerializer.Serialize(SealedRoomPools.ViewFor(frame.AuthoritativeState, player), Json),
                JsonSerializer.Serialize(ownPool, Json));
            Assert.NotEqual(JsonSerializer.Serialize(SealedRoomPools.ViewFor(frame.AuthoritativeState, other), Json),
                JsonSerializer.Serialize(ownPool, Json));
        }
        Assert.Null(ResolutionResult.BuildSpectatorSnapshot(frame.AuthoritativeState).SealedPool);

        var cold = MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance, players);
        await cold.ReconnectPlayerAsync("alice", alice.ReconnectToken, default);
        foreach (var player in new[] { "alice", "bob" })
            Assert.Equal(JsonSerializer.Serialize(live.SnapshotFor(player), Json),
                JsonSerializer.Serialize(cold.SnapshotFor(player), Json));
    }

    [Fact]
    public async Task OpponentSpectatorEventsAndPromptsNeverContainPrivatePoolMetadata()
    {
        var journal = new Journal();
        var live = Session(journal);
        await live.EnsurePlayerAsync("alice", default);
        await live.EnsurePlayerAsync("bob", default);
        var state = journal.Entries[^1].AuthoritativeState;
        foreach (var player in state.Seats.Keys)
        {
            var own = live.SnapshotFor(player);
            Assert.Equal(MatchFormats.Sealed, own.Format);
            Assert.Equal("OGN", own.SetCode);
            Assert.Equal(1, own.BestOf);
            var pool = Assert.IsType<SealedPoolDto>(own.SealedPool);
            Assert.Equal(JsonSerializer.Serialize(SealedRoomPools.ViewFor(state, player), Json), JsonSerializer.Serialize(pool, Json));
            var publicPart = JsonSerializer.Serialize(own with { SealedPool = null }, Json);
            Assert.DoesNotContain("sealedPool", publicPart);
            Assert.DoesNotContain("playerSealedPools", publicPart);
            Assert.DoesNotContain("seed", publicPart, StringComparison.OrdinalIgnoreCase);
        }
        var spectator = ResolutionResult.BuildSpectatorSnapshot(state);
        Assert.Null(spectator.SealedPool);
        Assert.Null(SealedRoomPools.ViewFor(state, "unseated-player"));
        var eventsAndPrompts = JsonSerializer.Serialize(new { events = journal.Entries.SelectMany(entry => entry.Events),
            prompts = journal.Entries.SelectMany(entry => entry.Prompts.Values), spectator }, Json);
        foreach (var pool in state.PlayerSealedPools!.Values)
        {
            Assert.DoesNotContain(pool.Seed, eventsAndPrompts);
            Assert.DoesNotContain(pool.ConfigurationSha256, eventsAndPrompts);
            Assert.DoesNotContain(pool.GeneratorVersion, eventsAndPrompts);
        }
        Assert.DoesNotContain("sealedPool", eventsAndPrompts);
        Assert.DoesNotContain("cardNo", eventsAndPrompts);
        var replay = MatchReplayRedactor.BuildSpectatorFrame(journal.Entries[^1]);
        Assert.Null(replay.SpectatorSnapshot.SealedPool);
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("configuration")]
    [InlineData("count")]
    [InlineData("missing-player")]
    [InlineData("missing-pack")]
    public async Task CorruptPrivatePoolsAreRejectedByRestoreAndReplay(string corruption)
    {
        var journal = new Journal();
        var live = Session(journal);
        await live.EnsurePlayerAsync("alice", default);
        var frame = RoundTrip(Frame(journal.Entries));
        var state = frame.AuthoritativeState!;
        var pools = state.PlayerSealedPools!.ToDictionary(entry => entry.Key, entry => entry.Value);
        var pool = pools["alice"];
        pools["alice"] = corruption switch
        {
            "seed" => pool with { Seed = "client-chosen-pool-seed" },
            "configuration" => pool with { ConfigurationSha256 = new string('0', 64) },
            "count" => pool with { Pool = pool.Pool.ToDictionary(entry => entry.Key, entry => entry.Value + 1) },
            "missing-pack" => pool with { Packs = [null!, .. pool.Packs.Skip(1)] },
            _ => pool
        };
        if (corruption == "missing-player") pools.Clear();
        frame = frame with { AuthoritativeState = state with { PlayerSealedPools = pools } };
        Assert.NotEmpty(Validate(frame));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
    }

    [Theory]
    [InlineData("RAD", 1)]
    [InlineData("OGN", 2)]
    [InlineData("", 1)]
    public void UnsupportedRoomOptionsCannotCreatePools(string setCode, int bestOf)
    {
        Assert.Throws<ArgumentException>(() => MatchSessionOptions.ForSealed(setCode, bestOf));
    }

    [Fact]
    public async Task FailedSeatCommitLeavesNoPlayerOrPoolAndRetryKeepsPrivateSeed()
    {
        var journal = new Journal { FailNext = true };
        var live = Session(journal);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await live.EnsurePlayerAsync("alice", default));
        Assert.Empty(journal.Entries);
        Assert.Throws<MatchSessionException>(() => live.SnapshotFor("alice"));
        var player = await live.EnsurePlayerAsync("alice", default);
        var original = JsonSerializer.Serialize(live.SnapshotFor("alice").SealedPool, Json);
        await live.EnsurePlayerAsync("alice", default);
        Assert.Single(journal.Entries);
        Assert.Equal(original, JsonSerializer.Serialize(live.SnapshotFor("alice").SealedPool, Json));
        Assert.Equal(player, live.ReconnectPlayer("alice", player.ReconnectToken));
    }

    [Fact]
    public async Task ProjectionCollectionsCannotMutateAuthoritativePacks()
    {
        var journal = new Journal();
        var live = Session(journal);
        await live.EnsurePlayerAsync("alice", default);
        var state = journal.Entries[0].AuthoritativeState;
        var hash = MatchStateHasher.Hash(state);
        var view = live.SnapshotFor("alice").SealedPool!;
        ((SealedPackCardDto[])view.Packs[0].Cards)[0] = new("FORGED", "common", false);
        ((SealedPoolCardDto[])view.Pool)[0] = new("FORGED", 999);
        Assert.Equal(hash, MatchStateHasher.Hash(state));
        Assert.DoesNotContain(live.SnapshotFor("alice").SealedPool!.Pool, card => card.CardNo == "FORGED");
    }

    [Fact]
    public async Task RestoringOneSeatedPlayerThenJoiningTheSecondRetainsTheOriginalPool()
    {
        var journal = new Journal();
        var live = Session(journal);
        await live.EnsurePlayerAsync("alice", default);
        var frame = RoundTrip(Frame(journal.Entries));
        var original = JsonSerializer.Serialize(live.SnapshotFor("alice").SealedPool, Json);
        var cold = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
        await cold.EnsurePlayerAsync("bob", default);
        Assert.Equal(original, JsonSerializer.Serialize(cold.SnapshotFor("alice").SealedPool, Json));
        var final = RoundTrip(Frame(journal.Entries));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, new CoreRuleEngine(), default));
        Assert.Empty(Validate(final));
    }

    [Fact]
    public async Task RecoveryRejectsOpponentPoolSubstitutionAndAnySpectatorPool()
    {
        var journal = new Journal();
        var live = Session(journal);
        await live.EnsurePlayerAsync("alice", default);
        await live.EnsurePlayerAsync("bob", default);
        var frame = RoundTrip(Frame(journal.Entries));
        var views = frame.PlayerViews.ToDictionary(entry => entry.Key, entry => entry.Value);
        views["alice"] = views["alice"] with { Snapshot = views["alice"].Snapshot with { SealedPool = live.SnapshotFor("bob").SealedPool } };
        Assert.Contains(Validate(frame with { PlayerViews = views }), error => error.Contains("private sealed pool", StringComparison.Ordinal));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(journal.Entries[^1]);
        spectator = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { SealedPool = live.SnapshotFor("alice").SealedPool } };
        Assert.Contains(Validate(frame with { SpectatorReplayFrame = spectator }), error => error.Contains("private sealed pool", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SpectatorPoolRedactionDoesNotDependOnAnUnseatedPlaceholderName()
    {
        var journal = new Journal();
        var live = Session(journal);
        await live.EnsurePlayerAsync("__spectator__", default);
        Assert.NotNull(live.SnapshotFor("__spectator__").SealedPool);
        Assert.Null(ResolutionResult.BuildSpectatorSnapshot(journal.Entries[0].AuthoritativeState).SealedPool);
    }

    [Fact]
    public async Task DevelopmentScenarioCannotReplaceThePrivatePoolOrItsSeed()
    {
        var journal = new Journal();
        var live = Session(journal);
        await live.EnsurePlayerAsync("alice", default);
        var before = JsonSerializer.Serialize(live.SnapshotFor("alice"), Json);
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await live.SeedScenarioAsync("alice", "reset-pool", "combat", null, default));
        Assert.Equal(ErrorCodes.UnsupportedCommand, error.Code);
        Assert.Equal(before, JsonSerializer.Serialize(live.SnapshotFor("alice"), Json));
        Assert.Single(journal.Entries);
    }

    private static IReadOnlyList<string> Validate(MatchRecoveryFrame frame) =>
        MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static MatchSession Session(Journal journal, string setCode = "OGN", int bestOf = 1) =>
        new("sealed-private-pools", new CoreRuleEngine(), journal, NoopMatchPlayerStore.Instance,
            MatchSessionOptions.ForSealed(setCode, bestOf));

    private static MatchRecoveryFrame RoundTrip(MatchRecoveryFrame frame) =>
        JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;

    private static MatchRecoveryFrame RoundTripWithReorderedObjectKeys(MatchRecoveryFrame frame)
    {
        // Reorder object properties recursively as JSONB does, while retaining every value and array order.
        var payload = Reorder(JsonSerializer.SerializeToElement(frame, Json));
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(payload, Json), Json)!;

        static object Reorder(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject()
                .OrderBy(property => Encoding.UTF8.GetByteCount(property.Name))
                .ThenBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(property => property.Name, property => Reorder(property.Value), StringComparer.Ordinal),
            JsonValueKind.Array => value.EnumerateArray().Select(Reorder).ToArray(),
            _ => value.Clone()
        };
    }

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
        var initial = MatchState.Create(last.RoomId) with { Seed = entries[0].OpeningSeed,
            SessionOptions = entries[0].AuthoritativeState.SessionOptions };
        return new(last.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState, initial)
            { OpeningSeed = entries[0].OpeningSeed };
    }

    private sealed class Journal : IMatchJournal
    {
        public bool FailNext { get; set; }
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            if (FailNext) { FailNext = false; throw new InvalidOperationException("seat commit failed"); }
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
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
