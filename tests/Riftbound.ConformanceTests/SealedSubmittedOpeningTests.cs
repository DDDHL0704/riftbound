using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RealPoolSubmissionOpensOptionalRolesAndBlankBattlefieldsAndReplaysFromEmpty(bool legend, bool champion)
    {
        var run = new Run();
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        foreach (var player in new[] { "alice", "bob" })
        {
            var deck = await Deck(run.State.PlayerSealedPools![player], legend, champion);
            var command = Command(deck);
            var submitted = await run.Act(player, command, NullableRoles(command));
            Assert.DoesNotContain(submitted.Events, item => item.Payload.ContainsKey("legendCardNo") || item.Payload.ContainsKey("championCardNo"));
            await run.Cold();
        }
        await run.Act("alice", new ReadyCommand());
        var opened = await run.Act("bob", new ReadyCommand());
        Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        foreach (var zones in opened.State.PlayerZones.Values)
        {
            Assert.Equal(legend ? 1 : 0, zones.LegendZone.Count);
            Assert.Equal(champion ? 1 : 0, zones.ChampionZone.Count);
            var battlefield = opened.State.CardObjects[Assert.Single(zones.Battlefields)];
            Assert.Equal(SealedDeckRules.BlankBattlefieldCardNo, battlefield.CardNo);
            Assert.Contains("CARD_TYPE:BATTLEFIELD", battlefield.Tags);
        }
        await run.Cold();
        var first = opened.State.TurnPlayerId;
        var second = opened.State.Seats.Keys.Single(player => player != first);
        await run.Act(first, new MulliganCommand([]));
        var firstTurn = await run.Act(second, new MulliganCommand([]));
        var missing = !legend || !champion;
        Assert.Equal(missing ? 6 : 5, firstTurn.State.PlayerZones[first].Hand.Count);
        Assert.Equal(missing ? 2 : 1, firstTurn.State.DrawLedger.Counts.GetValueOrDefault(first));
        await run.Cold();
        var secondTurn = await run.Act(first, new EndTurnCommand());
        Assert.Equal(missing ? 6 : 5, secondTurn.State.PlayerZones[second].Hand.Count);
        Assert.Null(secondTurn.State.OpeningRoleDrawPendingPlayerIds);
        await run.Cold();
        var later = await run.Act(second, new EndTurnCommand());
        Assert.Equal(missing ? 7 : 6, later.State.PlayerZones[first].Hand.Count);
        Assert.Equal(1, later.State.DrawLedger.Counts.GetValueOrDefault(first));
        await run.Cold();
    }

    [Fact]
    public async Task ForgedClientPoolCannotAuthorizeMoreCopiesAndRejectionIsAtomicAndReplayable()
    {
        var run = new Run();
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        var pool = run.State.PlayerSealedPools!["alice"];
        var legal = await Deck(pool, false, false);
        var extra = legal.MainDeck[0];
        var forged = Command(legal with { MainDeck = legal.MainDeck.Concat(Enumerable.Repeat(extra, pool.Pool[extra] + 1)).ToArray() });
        var node = JsonSerializer.SerializeToNode(forged, Json)!.AsObject();
        node["sealedPool"] = JsonSerializer.SerializeToNode(new { pool = new[] { new { cardNo = extra, count = 9999 } } }, Json);
        var raw = JsonSerializer.SerializeToElement(node, Json);
        var before = MatchStateHasher.Hash(run.State);
        var result = await run.Live.SubmitDeckAsync("alice", "forged-pool", forged, raw, default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, result.ErrorCode);
        Assert.Contains("超过服务端卡池持有", result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        var repeat = await run.Live.SubmitDeckAsync("alice", "forged-pool", forged, raw, default);
        Assert.Same(result, repeat);
        await run.Cold();
        var coldRepeat = await run.Live.SubmitDeckAsync("alice", "forged-pool", forged, raw, default);
        Assert.False(coldRepeat.Accepted);
        Assert.Equal(result.ErrorMessage, coldRepeat.ErrorMessage);
        Assert.Equal(before, MatchStateHasher.Hash(coldRepeat.State));
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    public async Task RoomBestOfSuppliesTheAuthoritativeBattlefieldCount(int bestOf, int count)
    {
        var run = new Run(bestOf);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        var deck = await Deck(run.State.PlayerSealedPools!["alice"], false, false, count);
        await run.Act("alice", Command(deck));
        var invalid = Command(deck with { Battlefields = Enumerable.Repeat(SealedDeckRules.BlankBattlefieldCardNo, count == 3 ? 1 : 3).ToArray() });
        var rejected = await run.Live.SubmitDeckAsync("alice", "wrong-battlefields", invalid, Raw(invalid), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, rejected.ErrorCode);
        Assert.Contains($"必须提交 {count} 张战场牌", rejected.ErrorMessage);
        await run.Cold();
    }

    [Theory]
    [InlineData("37")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("\" \"")]
    public async Task NullableRoleSyntaxStillRejectsWrongTypesAndWhitespace(string value)
    {
        var run = new Run();
        await run.Join("alice");
        var command = Command(await Deck(run.State.PlayerSealedPools!["alice"], false, false));
        var node = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
        node["legendCardNo"] = JsonNode.Parse(value);
        var result = await run.Live.SubmitDeckAsync("alice", "malformed-role", command, JsonSerializer.SerializeToElement(node, Json), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Single(run.Journal.Entries);
    }

    [Fact]
    public async Task TypedSubmissionWithAbsentRolesKeepsOriginalTypedInputThroughRecovery()
    {
        var run = new Run();
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        var deck = Command(await Deck(run.State.PlayerSealedPools!["alice"], false, false));
        var submitted = await run.Live.SubmitDeckAsync("alice", "typed-deck", deck, null, default);
        Assert.True(submitted.Accepted, submitted.ErrorMessage);
        Assert.Null(run.Journal.Entries[^1].RawCommand);
        Assert.NotNull(run.Journal.Entries[^1].TypedCommand);
        await run.Cold();
    }

    [Fact]
    public async Task ConstructedRawDeckStillRejectsNullableRolesBeforeAdmission()
    {
        var session = new MatchSession("constructed-role-envelope", new CoreRuleEngine());
        await session.EnsurePlayerAsync("alice", default);
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var deck = Command(PreconstructedDeckCatalog.Build(catalog)[0].Decklist with { LegendCardNo = "", ChampionCardNo = "" });
        var result = await session.SubmitDeckAsync("alice", "null-role", deck, NullableRoles(deck), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
    }

    private static SubmitDeckCommand Command(OfficialDecklist deck) =>
        new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
    private static JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), Json);
    private static JsonElement NullableRoles(SubmitDeckCommand command)
    {
        var node = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
        if (command.LegendCardNo.Length == 0) node["legendCardNo"] = null;
        if (command.ChampionCardNo.Length == 0) node["championCardNo"] = null;
        return JsonSerializer.SerializeToElement(node, Json);
    }

    // Build solely from the actual six server packs; never install a replacement pool.
    internal static async Task<OfficialDecklist> Deck(SealedPoolGeneration pool, bool withLegend, bool withChampion, int battlefieldCount = 3)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var cards = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        var owned = pool.Pool.Keys.Select(no => cards[no]).OrderBy(card => card.CardNo, StringComparer.Ordinal).ToArray();
        var traits = new[] { RuneTrait.Red, RuneTrait.Green, RuneTrait.Blue, RuneTrait.Orange, RuneTrait.Purple, RuneTrait.Yellow };
        for (var a = 0; a < traits.Length; a++)
        for (var b = a + 1; b < traits.Length; b++)
        for (var c = b + 1; c < traits.Length; c++)
        {
            var allowed = new[] { traits[a], traits[b], traits[c] };
            bool Fits(OfficialCard card) => card.CardColorList.Select(RuneTrait.Normalize).All(trait => trait == "colorless" || allowed.Contains(trait));
            var legend = withLegend ? owned.FirstOrDefault(card => card.CardCategoryName == "传奇" && Fits(card)) : null;
            var champion = withChampion ? owned.FirstOrDefault(card => card.CardCategoryName == "英雄单位" && Fits(card)) : null;
            if (withLegend && legend is null || withChampion && champion is null) continue;
            var main = owned.Where(card => Fits(card) && card.CardCategoryName is "单位" or "英雄单位" or "法术" or "专属法术" or "装备")
                .SelectMany(card => Enumerable.Repeat(card.CardNo, pool.Pool[card.CardNo])).ToList();
            if (main.Count < 25) continue;
            if (champion is not null) { main.Remove(champion.CardNo); main.Insert(0, champion.CardNo); }
            var rune = catalog.Cards.First(card => SealedDeckRules.IsBasicRune(card) && Fits(card));
            var deck = new OfficialDecklist(legend?.CardNo ?? "", champion?.CardNo ?? "", main.Take(25).ToArray(),
                Enumerable.Repeat(rune.CardNo, 12).ToArray(), Enumerable.Repeat(SealedDeckRules.BlankBattlefieldCardNo, battlefieldCount).ToArray());
            Assert.True(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.Sealed,
                new(pool.Pool, battlefieldCount == 1 ? 3 : 1)).IsValid);
            return deck;
        }
        throw new Xunit.Sdk.XunitException("The controlled server seed must yield a legal 25-card deck for this role selection.");
    }

    internal sealed class Run
    {
        public Journal Journal { get; } = new();
        private readonly Players players = new();
        private readonly Dictionary<string, string> tokens = [];
        private readonly IRuleEngine engine;
        private readonly bool reorderRecoveryObjects;
        public IMatchSession Live { get; private set; }
        public MatchState State => Journal.Entries[^1].AuthoritativeState;
        public Run(int bestOf = 1, IRuleEngine? engine = null, long seed = 424242, string setCode = "OGN",
            bool reorderRecoveryObjects = false)
        {
            this.engine = engine ?? new CoreRuleEngine();
            this.reorderRecoveryObjects = reorderRecoveryObjects;
            var initial = MatchState.Create("sealed-submitted-opening") with { Seed = seed,
                SessionOptions = MatchSessionOptions.ForSealed(setCode, bestOf) };
            Live = new MatchSession(initial, this.engine, Journal, players);
        }
        public async Task Join(string player) => tokens[player] = (await Live.EnsurePlayerAsync(player, default)).ReconnectToken;
        public Task<ResolutionResult> Choose(bool goFirst = true) => Act(
            Live.SnapshotFor("alice").Series!.PlayOrderChooserPlayerId!, new ChoosePlayOrderCommand(goFirst));
        public async Task<ResolutionResult> Act(string player, GameCommand command, JsonElement? raw = null)
        {
            raw ??= Raw(command);
            var id = "action-" + Journal.Entries.Count;
            var result = command switch
            {
                SubmitDeckCommand deck => await Live.SubmitDeckAsync(player, id, deck, raw, default),
                ReadyCommand => await Live.ReadyAsync(player, id, raw, default),
                _ => await Live.SubmitAsync(player, id, command, raw, default)
            };
            Assert.True(result.Accepted, result.ErrorMessage);
            return result;
        }
        public MatchRecoveryFrame Frame()
        {
            var last = Journal.Entries[^1];
            var commands = Journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId, entry.CommandType,
                entry.RawCommand, entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence,
                entry.Accepted, entry.ErrorMessage) { TypedCommand = entry.TypedCommand, ErrorCode = entry.ErrorCode }).ToArray();
            var events = Journal.Entries.SelectMany(entry => entry.Events.Select((item, i) => new RecoveredEvent(
                entry.StartedEventSequence + i + 1, entry.CompletedTick, i, item))).ToArray();
            var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key, last.CompletedTick,
                last.CompletedEventSequence, pair.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[pair.Key]));
            var initial = MatchState.Create(last.RoomId) with { Seed = Journal.Entries[0].OpeningSeed,
                SessionOptions = Journal.Entries[0].AuthoritativeState.SessionOptions };
            var frame = new MatchRecoveryFrame(last.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [],
                last.AuthoritativeState, initial) { OpeningSeed = initial.Seed };
            return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        }
        public async Task Cold(bool verifyReplay = true)
        {
            var original = Frame();
            var frame = reorderRecoveryObjects ? ReorderRecoveryFrameObjects(original) : original;
            if (reorderRecoveryObjects)
            {
                Assert.Equal(["bob", "alice"], frame.AuthoritativeState!.Seats.Keys);
                Assert.Equal(MatchStateHasher.Hash(original.AuthoritativeState!), MatchStateHasher.Hash(frame.AuthoritativeState));
            }
            Assert.Empty(MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
                frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick));
            if (verifyReplay)
                Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default));
            Live = MatchSession.Restore(frame, engine, Journal, players);
            foreach (var player in tokens.Keys.ToArray())
            {
                tokens[player] = (await Live.ReconnectPlayerAsync(player, tokens[player], default)).ReconnectToken;
                Assert.Equal(MatchStateHasher.HashValue(frame.PlayerViews[player].Snapshot), MatchStateHasher.HashValue(Live.SnapshotFor(player)));
                if (reorderRecoveryObjects)
                {
                    Assert.Equal(JsonSerializer.Serialize(original.PlayerViews[player].Snapshot, Json),
                        JsonSerializer.Serialize(Live.SnapshotFor(player), Json));
                    Assert.Equal(JsonSerializer.Serialize(original.PlayerViews[player].Prompt, Json),
                        JsonSerializer.Serialize(Live.PromptFor(player), Json));
                }
            }
            if (reorderRecoveryObjects)
                Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(original.AuthoritativeState!), Json),
                    JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(frame.AuthoritativeState!), Json));
        }
    }

    internal sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public bool FailNext { get; set; }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            if (FailNext) { FailNext = false; throw new IOException("journal unavailable"); }
            Entries.Add(entry); return ValueTask.CompletedTask;
        }
    }
    private sealed class Players : IMatchPlayerStore
    {
        private readonly Dictionary<(string Room, string Player), string> hashes = [];
        public ValueTask SavePlayerSessionAsync(string roomId, string playerId, string seat, string reconnectTokenHash,
            CancellationToken cancellationToken, long openingSeed = 0)
        { hashes[(roomId, playerId)] = reconnectTokenHash; return ValueTask.CompletedTask; }
        public async ValueTask CommitRoomSeatAsync(MatchJournalEntry entry, string reconnectTokenHash, IMatchJournal journal, CancellationToken cancellationToken)
        { await journal.RecordAsync(entry, cancellationToken); hashes[(entry.RoomId, entry.PlayerId)] = reconnectTokenHash; }
        public ValueTask<bool> HasReconnectTokenHashAsync(string roomId, string playerId, string reconnectTokenHash, CancellationToken cancellationToken) =>
            ValueTask.FromResult(hashes.GetValueOrDefault((roomId, playerId)) == reconnectTokenHash);
    }
}
