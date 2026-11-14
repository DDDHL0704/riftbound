using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    private static readonly JsonSerializerOptions CityJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CityOpeningExplicitAndOmittedStandardKeepTheOriginalOpeningAndFirstTurn(bool explicitRule)
    {
        var run = await CityRun.Create(OpeningRules.Standard, false, legacyCreate: !explicitRule);
        var (deck, _) = await CityDeck();
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await run.Act("alice", command);
        await run.Act("alice", new ReadyCommand()); // STANDARD still allows ready before the opponent's submission.
        Assert.Null(run.State.CityOpening); Assert.Null(run.Live.SnapshotFor("alice").OpeningSetup);
        await run.Act(run.Other, command); await run.Act(run.Other, new ReadyCommand());
        var first = run.State.TurnPlayerId; var second = run.State.OpeningSecondActionPlayerId!;
        await run.Act(first, new MulliganCommand([])); await run.Act(second, new MulliganCommand([]));
        Assert.Equal(5, run.State.PlayerZones[first].Hand.Count); Assert.Equal(4, run.State.PlayerZones[second].Hand.Count);
        Assert.Equal(2, CityRunes(run.State, first));
        await run.Act(first, new EndTurnCommand());
        Assert.Equal(3, CityRunes(run.State, second)); Assert.Equal(5, run.State.PlayerZones[second].Hand.Count);
        Assert.Null(run.State.PendingCardChoice); Assert.All(run.State.PlayerScores.Values, n => Assert.Equal(0, n));
        await run.Act(second, new EndTurnCommand()); await run.Cold();
        Assert.Equal(OpeningRules.Standard, run.Live.OpeningRule);
        Assert.DoesNotContain(run.Journal.Entries.SelectMany(e => e.Events), e => e.Kind.StartsWith("S5_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(OpeningRules.S5CityMode1, false, false)]
    [InlineData(OpeningRules.S5CityMode1, true, false)]
    [InlineData(OpeningRules.S5CityMode2, false, false)]
    [InlineData(OpeningRules.S5CityMode2, false, true)]
    [InlineData(OpeningRules.S5CityMode2, true, false)]
    [InlineData(OpeningRules.S5CityMode2, true, true)]
    [InlineData(OpeningRules.S5CityMode3, false, false)]
    [InlineData(OpeningRules.S5CityMode3, true, false)]
    public async Task CityOpeningRoomAndPracticeKeepPrivateSideboardingAndFirstDrawCompensationAcrossColdReplay(
        string rule, bool practice, bool recycle)
    {
        var run = await CityRun.Create(rule, practice);
        var (deck, reserve) = await CityDeck();
        await run.Act("alice", CityCommand(deck, reserve));
        Assert.Null(run.Live.SnapshotFor("alice").OpeningSetup!.OwnBattlefieldCardNo);
        Assert.DoesNotContain("READY", run.Live.PromptFor("alice").EnabledActions());
        await run.Act(run.Other, CityCommand(deck, reserve));
        var setup = run.State.CityOpening!;
        var first = setup.FirstPlayerId!; var second = setup.SecondPlayerId!;
        foreach (var player in new[] { first, second })
        {
            var snapshot = run.Live.SnapshotFor(player);
            Assert.Equal(rule, snapshot.OpeningRule);
            Assert.Equal("SIDEBOARD", snapshot.OpeningSetup!.Stage);
            Assert.Equal(setup.Battlefields![player], snapshot.OpeningSetup.OwnBattlefieldCardNo);
            Assert.Equal(first, snapshot.OpeningSetup.FirstPlayerId);
            Assert.Equal(second, snapshot.OpeningSetup.SecondPlayerId);
            Assert.Equal(reserve, snapshot.OpeningSetup.Sideboard);
            Assert.Empty(run.State.PlayerZones[player].Hand);
            Assert.Empty(run.State.CardObjects);
            Assert.Contains("READY", run.Live.PromptFor(player).EnabledActions());
            AssertCitySetupPrivate(run, player);
        }
        Assert.Null(ResolutionResult.BuildSpectatorSnapshot(run.State).OpeningSetup!.OwnBattlefieldCardNo);
        Assert.Null(ResolutionResult.BuildSpectatorSnapshot(run.State).OpeningSetup!.Sideboard);
        await run.Cold();

        // Actual equal-size sideboard swaps after learning only the player's own field.
        var replacement = reserve[0]; var outgoing = deck.MainDeck[1];
        var changed = deck with { MainDeck = deck.MainDeck.Take(1).Concat([replacement]).Concat(deck.MainDeck.Skip(2)).ToArray() };
        var changedReserve = reserve.Skip(1).Append(outgoing).ToArray();
        foreach (var player in new[] { first, second })
            await run.Act(player, CityCommand(changed, changedReserve));
        await run.Act(first, new ReadyCommand());
        AssertCitySetupPrivate(run, second);
        Assert.Empty(run.State.CardObjects);
        await run.Cold();
        var opening = await run.Act(second, new ReadyCommand());
        Assert.Equal(MatchPhases.Mulligan, run.State.Phase);
        Assert.Equal(first, run.State.TurnPlayerId);
        Assert.Equal(second, run.State.OpeningSecondActionPlayerId);
        Assert.Equal(rule == OpeningRules.S5CityMode3 ? 1 : 0, run.State.PlayerScores[second]);
        Assert.Equal(0, run.State.PlayerScores[first]);
        Assert.Equal(2, run.State.PlayerZones.Values.Sum(z => z.Battlefields.Count));
        var kinds = opening.Events.Select(e => e.Kind).ToArray();
        Assert.True(Array.IndexOf(kinds, "S5_BATTLEFIELDS_REVEALED") < Array.IndexOf(kinds, "OPENING_HAND_DRAWN"));
        foreach (var player in new[] { first, second })
        {
            Assert.Equal(4, run.State.PlayerZones[player].Hand.Count);
            Assert.Empty(run.State.PlayerZones[player].Base);
        }
        await run.Cold();
        await run.Act(first, new MulliganCommand([]));
        await run.Act(second, new MulliganCommand([]));
        Assert.Equal(MatchPhases.Main, run.State.Phase);
        Assert.Equal(5, run.State.PlayerZones[first].Hand.Count);
        Assert.Equal(4, run.State.PlayerZones[second].Hand.Count);
        Assert.Equal(2, CityRunes(run.State, first));
        Assert.Equal(0, CityRunes(run.State, second));
        var beforeDraw = run.State.PlayerZones[second].MainDeck.ToArray();
        await run.Act(first, new EndTurnCommand());
        Assert.Equal(3, CityRunes(run.State, second));
        if (rule == OpeningRules.S5CityMode2)
        {
            var pending = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Equal("S5_OPENING_INSIGHT", pending.ChoiceWindow);
            Assert.Equal(second, pending.PlayerId);
            Assert.Equal([beforeDraw[0]], pending.LegalObjectIds);
            Assert.Equal(4, run.State.PlayerZones[second].Hand.Count);
            var opponentView = JsonSerializer.Serialize(run.Live.SnapshotFor(first), CityJson);
            var opponentPrompt = JsonSerializer.Serialize(run.Live.PromptFor(first), CityJson);
            Assert.DoesNotContain(beforeDraw[0], opponentView);
            Assert.DoesNotContain(beforeDraw[0], opponentPrompt);
            Assert.Contains(beforeDraw[0], JsonSerializer.Serialize(run.Live.PromptFor(second), CityJson));
            var unchanged = MatchStateHasher.Hash(run.State);
            var stolen = new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, []);
            var refused = await run.Live.SubmitAsync(first, "city-wrong-insight-player", stolen, CityRaw(stolen), default);
            Assert.False(refused.Accepted); Assert.Equal(unchanged, MatchStateHasher.Hash(refused.State));
            await run.Cold();
            await run.Act(second, new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow,
                recycle ? [beforeDraw[0]] : []));
            Assert.Equal(recycle ? beforeDraw[1] : beforeDraw[0], run.State.PlayerZones[second].Hand.Last());
            if (recycle) Assert.Equal(beforeDraw[0], run.State.PlayerZones[second].MainDeck.Last());
        }
        Assert.Equal(MatchPhases.Main, run.State.Phase);
        Assert.Equal(rule == OpeningRules.S5CityMode1 ? 6 : 5, run.State.PlayerZones[second].Hand.Count);
        Assert.Equal(rule == OpeningRules.S5CityMode3 ? 1 : 0, run.State.PlayerScores[second]);
        Assert.Null(run.State.PendingCardChoice);
        await run.Cold();
        await run.Act(second, new EndTurnCommand()); // Both players' first turns have ended.
        await run.Act(first, new EndTurnCommand());
        Assert.Null(run.State.PendingCardChoice);
        Assert.Equal(rule == OpeningRules.S5CityMode1 ? 7 : 6, run.State.PlayerZones[second].Hand.Count);
        await run.Cold();
        await Assert.ThrowsAsync<MatchSessionException>(async () => await run.Registry.GetOrCreateAsync(run.State.RoomId,
            MatchSessionOptions.ForOpeningRule(rule == OpeningRules.S5CityMode1 ? OpeningRules.S5CityMode3 : OpeningRules.S5CityMode1), default));
        Assert.Equal(rule, run.Live.OpeningRule);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("standard")]
    [InlineData("S5_CITY_MODE4")]
    [InlineData(" S5_CITY_MODE1 ")]
    public async Task CityOpeningInvalidRoomRuleIsRejectedBeforeAnySeatOrDirectoryMutation(string? rule)
    {
        var journal = new SealedSubmittedOpeningTests.Journal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal);
        var directory = new InMemoryPublicMatchDirectory(); var clients = new RecordingHubClients();
        var hub = CreateHub(clients, new RecordingGroupManager(), "city-invalid", registry,
            publicMatches: directory, authenticatedHandle: "alice");
        Assert.Null(await hub.CreatePublicMatchWithOptions("alice", new(OpeningRule: rule!)));
        Assert.Empty(journal.Entries); Assert.Empty(await directory.ListOpenAsync(default));
        Assert.Empty(clients.CallerClient.JoinedMessages);
    }

    [Fact]
    public async Task CityOpeningSealedAndSeriesRequestsAreRejectedAndQuickMatchRemainsStandard()
    {
        Assert.Throws<ArgumentException>(() => PublicMatchCreation.Normalize(new(MatchFormats.Sealed, "OGN", 1,
            OpeningRule: OpeningRules.S5CityMode1)));
        Assert.Throws<ArgumentException>(() => PublicMatchCreation.Normalize(new(BestOf: 3, OpeningRule: OpeningRules.S5CityMode2)));
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance);
        var queue = new InMemoryMatchmakingQueue(registry, () => "CITY-QUICK-STANDARD");
        await queue.EnqueueAsync("alice", default); var paired = await queue.EnqueueAsync("bob", default);
        var live = await registry.FindAsync(paired.RoomId!, default);
        Assert.Equal(OpeningRules.Standard, live!.OpeningRule);
        Assert.Equal(OpeningRules.Standard, live.SnapshotFor("alice").OpeningRule);
        Assert.Null(live.SnapshotFor("bob").OpeningSetup);
    }

    [Fact]
    public async Task CityOpeningSideboardCannotIntroduceCardsOrChangeLockedRunesLegendOrBattlefields()
    {
        var run = await CityRun.Create(OpeningRules.S5CityMode1, false);
        var (deck, reserve) = await CityDeck();
        await run.Act("alice", CityCommand(deck, reserve)); await run.Act(run.Other, CityCommand(deck, reserve));
        var invalid = new[]
        {
            CityCommand(deck with { MainDeck = deck.MainDeck.Skip(1).Append(reserve[0]).ToArray() }, reserve),
            CityCommand(deck with { RuneDeck = deck.RuneDeck.Skip(1).Append("UNKNOWN").ToArray() }, reserve),
            CityCommand(deck with { Battlefields = deck.Battlefields.Reverse().ToArray() }, reserve),
            CityCommand(deck with { LegendCardNo = "OGN·247/298" }, reserve),
            CityCommand(deck, reserve.Concat(reserve).Concat(reserve).ToArray())
        };
        foreach (var command in invalid)
        {
            var before = MatchStateHasher.Hash(run.State);
            var result = await run.Live.SubmitDeckAsync("alice", "invalid-" + Guid.NewGuid(), command, CityRaw(command), default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
        await run.Cold();
        var frame = run.Frame(); var authoritative = frame.AuthoritativeState!;
        var wrong = authoritative.CityOpening! with { FirstPlayerId = authoritative.CityOpening.SecondPlayerId };
        Assert.NotEmpty(CityOpeningRules.Validate(authoritative with { CityOpening = wrong }));
        Assert.NotEmpty(CityOpeningRules.Validate(authoritative with { SessionOptions = authoritative.SessionOptions! with { OpeningRule = "UNKNOWN" } }));
        var alteredInitial = frame.ReplayInitialState! with { SessionOptions = frame.ReplayInitialState!.SessionOptions! with { OpeningRule = OpeningRules.S5CityMode3 } };
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame with { ReplayInitialState = alteredInitial }, new CoreRuleEngine(), default));
    }

    [Fact]
    public async Task CityOpeningReserveRegistrationCanChangeBeforePlayOrderIsKnown()
    {
        var run = await CityRun.Create(OpeningRules.S5CityMode3, false);
        var (deck, reserve) = await CityDeck();
        await run.Act("alice", CityCommand(deck, []));
        await run.Act("alice", CityCommand(deck, reserve));
        Assert.Equal(reserve, run.Live.SnapshotFor("alice").OpeningSetup!.Sideboard);
        Assert.Null(run.Live.SnapshotFor("alice").OpeningSetup!.OwnBattlefieldCardNo);
        await run.Cold();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CityOpeningInsightUsesSharedObservedCardPermissionInsteadOfBypassingIt(bool banish)
    {
        var run = await CityRun.Create(OpeningRules.S5CityMode2, false);
        var (deck, reserve) = await CityDeck(nightmare: true);
        await run.Act("alice", CityCommand(deck, reserve)); await run.Act(run.Other, CityCommand(deck, reserve));
        var first = run.State.CityOpening!.FirstPlayerId!; var second = run.State.CityOpening.SecondPlayerId!;
        await run.Act(first, new ReadyCommand()); await run.Act(second, new ReadyCommand());
        await run.Act(first, new MulliganCommand([])); await run.Act(second, new MulliganCommand([]));
        // A focused instruction fixture places a real registered Nocturne on top;
        // the eight full room scenarios separately exercise the unmodified opening shuffle.
        var zones = run.State.PlayerZones.ToDictionary();
        var locations = run.State.ObjectLocations.ToDictionary();
        var top = zones[second].MainDeck.FirstOrDefault(id => run.State.CardObjects[id].CardNo == "OGN·194/298");
        if (top is null)
        {
            top = zones[second].Hand.First(id => run.State.CardObjects[id].CardNo == "OGN·194/298");
            var exchanged = zones[second].MainDeck[0];
            zones[second] = zones[second] with { Hand = zones[second].Hand.Select(id => id == top ? exchanged : id).ToArray(),
                MainDeck = zones[second].MainDeck.Skip(1).Prepend(top).ToArray() };
            locations[top] = new(second, "MAIN_DECK"); locations[exchanged] = new(second, "HAND");
        }
        zones[second] = zones[second] with { MainDeck = new[] { top }.Concat(zones[second].MainDeck.Where(id => id != top)).ToArray() };
        var session = new MatchSession(run.State with { PlayerZones = zones, ObjectLocations = locations }, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var end = new EndTurnCommand();
        var result = await session.SubmitAsync(first, "city-observation-end", end, CityRaw(end), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var request = result.State.PendingRuleChoice!.Request;
        Assert.Equal("DECK_OBSERVATION_BANISH", request.Kind);
        Assert.Equal(second, request.PlayerId);
        Assert.False(request.Preview!.DeckObservationView!.Public);
        Assert.DoesNotContain(top, JsonSerializer.Serialize(session.PromptFor(first), CityJson));
        var pay = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [banish ? "BANISH" : "KEEP"]);
        result = await session.SubmitAsync(second, "city-observation-choice", pay, CityRaw(pay), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        if (banish)
        {
            Assert.Contains(result.Events, e => e.Kind == "CARD_BANISHED");
            var observed = Assert.Single(result.State.ObservedDeckPlays!);
            Assert.True(CoreRuleEngine.ValidObservedDeckPlay(result.State, observed));
            Assert.Equal(4, result.State.PlayerZones[second].Hand.Count);
            var permission = Assert.IsType<PendingEffectPlayState>(result.State.PendingEffectPlay);
            var decline = new ChooseCardsCommand(permission.ChoiceId, "EFFECT_PLAY", []);
            result = await session.SubmitAsync(second, "city-observation-decline", decline, CityRaw(decline), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            Assert.Contains(top, result.State.PlayerZones[second].Banished);
            Assert.Equal(5, result.State.PlayerZones[second].Hand.Count);
        }
        else
        {
            Assert.Equal("S5_OPENING_INSIGHT", result.State.PendingCardChoice!.ChoiceWindow);
            Assert.Equal([top], result.State.PendingCardChoice.LegalObjectIds);
        }
    }

    [Fact]
    public async Task CityOpeningSpectatorDoesNotUsePrivateSetupOfAPlayerWithTheViewerSentinelName()
    {
        var journal = new SealedSubmittedOpeningTests.Journal();
        var session = new MatchSession("city-spectator-sentinel", new CoreRuleEngine(), journal,
            NoopMatchPlayerStore.Instance, MatchSessionOptions.ForOpeningRule(OpeningRules.S5CityMode1));
        const string owner = "__spectator__";
        await session.EnsurePlayerAsync(owner, default); await session.EnsurePlayerAsync("bob", default);
        var (deck, reserve) = await CityDeck(); var command = CityCommand(deck, reserve);
        Assert.True((await session.SubmitDeckAsync(owner, "sentinel-deck-1", command, CityRaw(command), default)).Accepted);
        Assert.True((await session.SubmitDeckAsync("bob", "sentinel-deck-2", command, CityRaw(command), default)).Accepted);
        Assert.NotNull(session.SnapshotFor(owner).OpeningSetup!.OwnBattlefieldCardNo);
        Assert.Equal(reserve, session.SnapshotFor(owner).OpeningSetup!.Sideboard);
        var spectator = ResolutionResult.BuildSpectatorSnapshot(journal.Entries[^1].AuthoritativeState);
        Assert.Null(spectator.OpeningSetup!.OwnBattlefieldCardNo); Assert.Null(spectator.OpeningSetup.Sideboard);
    }

    private static int CityRunes(MatchState state, string player)
        => state.PlayerZones[player].Base.Count(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));

    private static void AssertCitySetupPrivate(CityRun run, string viewer)
    {
        var snapshot = run.Live.SnapshotFor(viewer);
        Assert.Single(JsonSerializer.SerializeToElement(snapshot.OpeningSetup, CityJson).EnumerateObject(), x => x.Name == "ownBattlefieldCardNo");
        Assert.Equal(0, Convert.ToInt32(snapshot.Lanes["battlefieldCount"]));
        Assert.All(run.Journal.Entries.SelectMany(x => x.Events).Where(x => x.Kind == "S5_BATTLEFIELDS_SELECTED"),
            e => Assert.Equal(["count"], e.Payload.Keys));
        Assert.Null(ResolutionResult.BuildSpectatorSnapshot(run.State).OpeningSetup!.OwnBattlefieldCardNo);
    }

    private static SubmitDeckCommand CityCommand(OfficialDecklist d, IReadOnlyList<string> reserve)
        => new(d.LegendCardNo, d.ChampionCardNo, d.MainDeck, d.RuneDeck, d.Battlefields, d.AdditionalLegendCardNos, reserve);
    private static JsonElement CityRaw(GameCommand c) => JsonSerializer.SerializeToElement(c, c.GetType(), CityJson);

    private static async Task<(OfficialDecklist Deck, string[] Reserve)> CityDeck(bool nightmare = false)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var legendCard = nightmare ? catalog.Cards.First(c => c.CardCategoryName == "传奇" && c.CardColorList.Contains("purple") && c.Hero != "妮蔻")
            : catalog.Cards.Single(c => c.CardNo == "UNL-181/219");
        var championCard = nightmare ? catalog.Cards.First(c => c.CardCategoryName == "英雄单位" && c.Hero == legendCard.Hero
                && ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724))
            : catalog.Cards.Single(c => c.CardNo == "UNL-022/219");
        var legend = legendCard.CardNo; var champion = championCard.CardNo;
        var traits = legendCard.CardColorList.ToHashSet();
        var cards = catalog.Cards.Where(c => c.CardCategoryName is "单位" or "英雄单位" or "装备" or "法术")
            .Where(c => c.CardGroupLimit != 1 && !c.CardEffect.Contains("{{唯我}}") && c.CardName != championCard.CardName
                && (!nightmare || c.CardNo != "OGN·194/298")
                && c.CardColorList.All(t => t == "colorless" || traits.Contains(t))
                && ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).GroupBy(c => c.CardName).Select(g => g.First()).ToArray();
        var used = nightmare ? 12 : 13;
        var main = new[] { champion }.Concat(nightmare ? Enumerable.Repeat("OGN·194/298", 3) : [])
            .Concat(cards.Take(used).SelectMany(c => Enumerable.Repeat(c.CardNo, 3))).ToArray();
        var runes = catalog.Cards.Where(c => c.CardCategoryName == "符文" && c.CardColorList.All(t => t == "colorless" || traits.Contains(t)))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).Take(1).Select(c => c.CardNo).ToArray();
        var fields = catalog.Cards.Where(c => c.CardCategoryName == "战场" && ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).GroupBy(c => c.CardName).Take(3).Select(g => g.First().CardNo).ToArray();
        var deck = new OfficialDecklist(legend, champion, main, Enumerable.Repeat(runes[0], 12).ToArray(), fields);
        Assert.True(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724).IsValid);
        return (deck, cards.Skip(used).Take(4).Select(c => c.CardNo).ToArray());
    }

    private sealed class CityRun
    {
        public SealedSubmittedOpeningTests.Journal Journal { get; } = new();
        private readonly CityPlayers players = new(); private readonly Dictionary<string, string> tokens = [];
        public InMemoryMatchSessionRegistry Registry { get; private set; } = null!;
        public IMatchSession Live { get; private set; } = null!;
        public string Other { get; private set; } = "bob";
        public MatchState State => Journal.Entries[^1].AuthoritativeState;
        public static async Task<CityRun> Create(string rule, bool practice, bool legacyCreate = false)
        {
            var run = new CityRun { Other = practice ? "practice_bot" : "bob" };
            run.Registry = new(new CoreRuleEngine(), run.Journal, NoopMatchRecoveryStore.Instance, run.players);
            var directory = new InMemoryPublicMatchDirectory();
            var hub = CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "city-host", run.Registry,
                publicMatches: directory, authenticatedHandle: "alice");
            var created = Assert.IsType<CreatePublicMatchResultDto>(legacyCreate ? await hub.CreatePublicMatch("alice")
                : await hub.CreatePublicMatchWithOptions("alice", new(OpeningRule: rule)));
            Assert.Equal(rule, created.Match.OpeningRule);
            Assert.Equal(rule, Assert.Single(await directory.ListOpenAsync(default)).OpeningRule);
            run.tokens["alice"] = created.PlayerSession.ReconnectToken;
            var clients = new RecordingHubClients();
            await CreateHub(clients, new RecordingGroupManager(), "city-opponent", run.Registry, authenticatedHandle: run.Other)
                .JoinRoom(created.Match.RoomId, run.Other);
            run.tokens[run.Other] = Assert.IsType<PlayerSessionDto>(Assert.Single(clients.CallerClient.JoinedMessages).Payload).ReconnectToken;
            run.Live = (await run.Registry.FindAsync(created.Match.RoomId, default))!;
            Assert.All(run.Journal.Entries, x => Assert.Equal(rule, CityOpeningRules.Rule(x.AuthoritativeState)));
            return run;
        }
        public async Task<ResolutionResult> Act(string player, GameCommand command)
        {
            var id = "city-action-" + Journal.Entries.Count; var raw = CityRaw(command);
            var r = command switch
            {
                SubmitDeckCommand deck => await Live.SubmitDeckAsync(player, id, deck, raw, default),
                ReadyCommand => await Live.ReadyAsync(player, id, raw, default),
                _ => await Live.SubmitAsync(player, id, command, raw, default)
            };
            Assert.True(r.Accepted, r.ErrorMessage); return r;
        }
        public MatchRecoveryFrame Frame()
        {
            var last = Journal.Entries[^1];
            var commands = Journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
            var events = Journal.Entries.SelectMany(e => e.Events.Select((item, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
                e.CompletedTick, i, item))).ToArray();
            var views = last.Snapshots.ToDictionary(x => x.Key, x => new RecoveredPlayerView(x.Key, last.CompletedTick,
                last.CompletedEventSequence, x.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[x.Key]));
            var initial = MatchState.Create(last.RoomId) with { Seed = Journal.Entries[0].OpeningSeed, SessionOptions = State.SessionOptions };
            var frame = new MatchRecoveryFrame(last.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], State, initial)
                { OpeningSeed = initial.Seed };
            return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, CityJson), CityJson)!;
        }
        public async Task Cold()
        {
            var frame = Frame();
            Registry = new(new CoreRuleEngine(), Journal, new CityRecovery(frame), players);
            Live = await Registry.GetOrCreateAsync(frame.RoomId, default);
            foreach (var player in tokens.Keys.ToArray())
            {
                tokens[player] = (await Live.ReconnectPlayerAsync(player, tokens[player], default)).ReconnectToken;
                Assert.Equal(MatchStateHasher.HashValue(frame.PlayerViews[player].Snapshot), MatchStateHasher.HashValue(Live.SnapshotFor(player)));
                var expected = JsonSerializer.SerializeToElement(frame.PlayerViews[player].Prompt, CityJson);
                var actual = JsonSerializer.SerializeToElement(Live.PromptFor(player), CityJson);
                Assert.True(MatchStateHasher.HashValue(expected) == MatchStateHasher.HashValue(actual),
                    "Recovered prompt differs: " + string.Join("; ", CityJsonDifferences(expected, actual)));
            }
        }
    }
    private static IEnumerable<string> CityJsonDifferences(JsonElement expected, JsonElement actual, string path = "$")
    {
        if (expected.ValueKind != actual.ValueKind) return [path + ": kind " + expected.ValueKind + " != " + actual.ValueKind];
        if (expected.ValueKind == JsonValueKind.Object)
        {
            var left = expected.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
            var right = actual.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
            return left.Keys.Union(right.Keys).SelectMany(k => left.TryGetValue(k, out var l) && right.TryGetValue(k, out var r)
                ? CityJsonDifferences(l, r, path + "." + k) : [path + "." + k + ": missing field"]).Take(10).ToArray();
        }
        if (expected.ValueKind == JsonValueKind.Array)
            return expected.GetArrayLength() != actual.GetArrayLength() ? [path + ": different lengths"]
                : expected.EnumerateArray().Zip(actual.EnumerateArray()).SelectMany((x, i) => CityJsonDifferences(x.First, x.Second, path + "[" + i + "]")).Take(10).ToArray();
        return expected.GetRawText() == actual.GetRawText() ? [] : [path + ": " + expected.GetRawText() + " != " + actual.GetRawText()];
    }
    private sealed class CityRecovery(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
    private sealed class CityPlayers : IMatchPlayerStore
    {
        private readonly Dictionary<(string, string), string> hashes = [];
        public ValueTask SavePlayerSessionAsync(string roomId, string playerId, string seat, string reconnectTokenHash,
            CancellationToken cancellationToken, long openingSeed = 0)
        { hashes[(roomId, playerId)] = reconnectTokenHash; return ValueTask.CompletedTask; }
        public async ValueTask CommitRoomSeatAsync(MatchJournalEntry entry, string reconnectTokenHash, IMatchJournal journal, CancellationToken cancellationToken)
        { await journal.RecordAsync(entry, cancellationToken); hashes[(entry.RoomId, entry.PlayerId)] = reconnectTokenHash; }
        public ValueTask<bool> HasReconnectTokenHashAsync(string roomId, string playerId, string reconnectTokenHash, CancellationToken cancellationToken)
            => ValueTask.FromResult(hashes.GetValueOrDefault((roomId, playerId)) == reconnectTokenHash);
    }
}
