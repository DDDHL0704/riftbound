using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalDeckRoleOpeningTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task AbsentRolesLeaveEmptyZonesAndDrawOnceOnEachPlayersFirstTurn(bool legend, bool champion)
    {
        var run = new Run(Initial(legend, champion));
        await run.Act("alice", new ReadyCommand());
        var opened = await run.Act("bob", new ReadyCommand());
        var missing = !legend || !champion;
        foreach (var zones in opened.State.PlayerZones.Values)
        {
            Assert.Equal(legend ? 1 : 0, zones.LegendZone.Count);
            Assert.Equal(champion ? 1 : 0, zones.ChampionZone.Count);
            Assert.Equal(4, zones.Hand.Count);
            Assert.Equal(champion ? 24 : 25, zones.MainDeck.Count + zones.Hand.Count);
        }
        Assert.Equal(missing ? 2 : 0, opened.State.OpeningRoleDrawPendingPlayerIds?.Count ?? 0);
        Assert.Equal(50 + 24 + 2 + (legend ? 2 : 0), opened.State.CardObjects.Count);
        if (!missing)
            Assert.DoesNotContain("openingRoleDrawPendingPlayerIds", JsonSerializer.Serialize(opened.State, Json));
        await run.Cold();

        var first = opened.State.TurnPlayerId;
        var second = opened.State.Seats.Keys.Single(player => player != first);
        await run.Act(first, new MulliganCommand([]));
        var firstTurn = await run.Act(second, new MulliganCommand([]));
        Assert.Equal(MatchPhases.Main, firstTurn.State.Phase);
        Assert.Equal(missing ? 6 : 5, firstTurn.State.PlayerZones[first].Hand.Count);
        Assert.Equal(4, firstTurn.State.PlayerZones[second].Hand.Count);
        Assert.Equal(missing ? 2 : 1, firstTurn.State.DrawLedger.Counts.GetValueOrDefault(first));
        Assert.Equal(missing ? 1 : 0, firstTurn.State.OpeningRoleDrawPendingPlayerIds?.Count ?? 0);
        if (missing)
        {
            var kinds = firstTurn.Events.Select(item => item.Kind).ToList();
            Assert.True(kinds.IndexOf("CARD_DRAWN") < kinds.IndexOf("RUNES_CALLED"));
        }
        if (!legend)
        {
            Assert.DoesNotContain(firstTurn.Prompts[first].Candidates ?? [], candidate => candidate.Action == CommandTypes.LegendAct && candidate.Enabled);
            Assert.All((firstTurn.Prompts[first].Candidates ?? []).Where(candidate => candidate.Action == CommandTypes.LegendAct),
                candidate => Assert.Empty(candidate.Sources ?? []));
            var rejected = await new CoreRuleEngine().ResolveAsync(firstTurn.State,
                new("absent-legend", first, CommandTypes.LegendAct), new LegendActCommand("absent", "absent", []), default);
            Assert.False(rejected.Accepted);
            Assert.Equal(MatchStateHasher.Hash(firstTurn.State), MatchStateHasher.Hash(rejected.State));
        }
        await run.Cold();
        var secondTurn = await run.Act(first, new EndTurnCommand());
        Assert.Equal(second, secondTurn.State.TurnPlayerId);
        Assert.Equal(missing ? 6 : 5, secondTurn.State.PlayerZones[second].Hand.Count);
        Assert.Equal(missing ? 2 : 1, secondTurn.State.DrawLedger.Counts.GetValueOrDefault(second));
        Assert.Null(secondTurn.State.OpeningRoleDrawPendingPlayerIds);
        await run.Cold();
        var later = await run.Act(second, new EndTurnCommand());
        Assert.Equal(first, later.State.TurnPlayerId);
        Assert.Equal(missing ? 7 : 6, later.State.PlayerZones[first].Hand.Count);
        Assert.Equal(1, later.State.DrawLedger.Counts.GetValueOrDefault(first));
        await run.Cold();
    }

    [Fact]
    public async Task SkippingTheDrawPhaseDoesNotSkipTheOpeningRoleDraw()
    {
        var run = new Run(Initial(false, false));
        await run.Act("alice", new ReadyCommand());
        var opened = await run.Act("bob", new ReadyCommand());
        var first = opened.State.TurnPlayerId;
        var second = opened.State.Seats.Keys.Single(player => player != first);
        await run.Act(first, new MulliganCommand([]));
        var firstTurn = await run.Act(second, new MulliganCommand([]));
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        const string equipmentId = "skip-draw-phase";
        var before = firstTurn.State with
        {
            CardObjects = new Dictionary<string, CardObjectState>(firstTurn.State.CardObjects)
            {
                [equipmentId] = PrintedCardFactory.Create(equipmentId, second, catalog.Cards.Single(card => card.CardNo == "VEN·022"))
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(firstTurn.State.ObjectLocations)
            {
                [equipmentId] = new(second, "BASE")
            },
            PlayerZones = firstTurn.State.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == second
                ? pair.Value with { Base = pair.Value.Base.Append(equipmentId).ToArray() } : pair.Value)
        };
        var result = await new CoreRuleEngine().ResolveAsync(before, new("skip-turn", first, CommandTypes.EndTurn), new EndTurnCommand(), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(5, result.State.PlayerZones[second].Hand.Count);
        Assert.Contains(result.Events, item => item.Kind == "DRAW_PHASE_SKIPPED");
        Assert.Single(result.Events, item => item.Kind == "CARD_DRAWN");
        Assert.Equal(1, result.State.DrawLedger.Counts.GetValueOrDefault(second));
        Assert.Null(result.State.OpeningRoleDrawPendingPlayerIds);
        OfficialGraveyardRecastTests.Restore(result.State);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }

    [Fact]
    public async Task AnExtraTurnCannotConsumeTheOtherPlayersOpeningRoleDraw()
    {
        var run = new Run(Initial(false, false));
        await run.Act("alice", new ReadyCommand());
        var opened = await run.Act("bob", new ReadyCommand());
        var first = opened.State.TurnPlayerId;
        var second = opened.State.Seats.Keys.Single(player => player != first);
        await run.Act(first, new MulliganCommand([]));
        var firstTurn = await run.Act(second, new MulliganCommand([]));
        var engine = new CoreRuleEngine();
        var extra = await engine.ResolveAsync(firstTurn.State with { ExtraTurnPlayerId = first },
            new("extra-turn", first, CommandTypes.EndTurn), new EndTurnCommand(), default);
        Assert.True(extra.Accepted, extra.ErrorMessage);
        Assert.Equal(first, extra.State.TurnPlayerId);
        Assert.Equal(7, extra.State.PlayerZones[first].Hand.Count);
        Assert.Equal([second], extra.State.OpeningRoleDrawPendingPlayerIds);
        var other = await engine.ResolveAsync(extra.State, new("other-turn", first, CommandTypes.EndTurn), new EndTurnCommand(), default);
        Assert.True(other.Accepted, other.ErrorMessage);
        Assert.Equal(second, other.State.TurnPlayerId);
        Assert.Equal(6, other.State.PlayerZones[second].Hand.Count);
        Assert.Null(other.State.OpeningRoleDrawPendingPlayerIds);
        OfficialGraveyardRecastTests.Restore(other.State);
    }

    [Fact]
    public async Task NoChosenChampionCannotReturnOtherHeroCardsAsTheChosenChampion()
    {
        var state = OfficialChosenChampionReturnTests.Position();
        state = state with
        {
            PlayerDecklists = new Dictionary<string, OfficialDecklist>(state.PlayerDecklists)
            {
                ["P1"] = state.PlayerDecklists["P1"] with { ChampionCardNo = "" }
            }
        };
        var result = await OfficialGraveyardRecastTests.Act(state, "P1", new PassPriorityCommand());
        Assert.Null(result.State.PendingCardChoice);
        Assert.Empty(result.State.PlayerZones["P1"].ChampionZone);
        Assert.Equal(4, result.State.PlayerZones["P1"].Graveyard.Count);
        OfficialGraveyardRecastTests.Restore(result.State);
    }

    [Theory]
    [InlineData("outsider")]
    [InlineData("duplicate")]
    [InlineData("has-both-roles")]
    public async Task RecoveryRejectsInvalidOpeningRoleDrawPlayers(string attack)
    {
        var run = new Run(Initial(attack == "has-both-roles", attack == "has-both-roles"));
        await run.Act("alice", new ReadyCommand());
        await run.Act("bob", new ReadyCommand());
        var frame = run.Frame();
        var forged = frame.AuthoritativeState! with
        {
            OpeningRoleDrawPendingPlayerIds = attack == "outsider" ? ["mallory"]
                : attack == "duplicate" ? ["alice", "alice"] : ["alice"]
        };
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands,
            frame.Events, frame.PlayerViews, forged, frame.CurrentTick);
        Assert.Contains(errors, error => error.Contains("opening role draw", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ConstructedDeckValidationStillRequiresBothRoles(bool legend, bool champion)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var original = PreconstructedDeckCatalog.Build(catalog)[0].Decklist;
        Assert.True(OfficialDeckValidator.Validate(original, catalog).IsValid);
        var deck = original with { LegendCardNo = legend ? original.LegendCardNo : "", ChampionCardNo = champion ? original.ChampionCardNo : "" };
        Assert.False(OfficialDeckValidator.Validate(deck, catalog).IsValid);
        Assert.False(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724).IsValid);
    }

    // An explicit already-admitted blueprint isolates the shared core opening.
    // Wire admission and server-owned sealed pools are covered by their own steps.
    private static MatchState Initial(bool legend, bool champion)
    {
        var main = Enumerable.Repeat("OGN·012/298", 25).ToArray();
        if (champion) main[0] = "UNL-022/219";
        var deck = new OfficialDecklist(legend ? "UNL-181/219" : "", champion ? "UNL-022/219" : "", main,
            Enumerable.Repeat("UNL-R01", 12).ToArray(), ["OGN·277/298", "OGN·277/298", "OGN·277/298"]);
        return MatchReplayInitialStateBuilder.FromSeats("optional-role-opening", new Dictionary<string, string>
        {
            ["alice"] = "P1", ["bob"] = "P2"
        }) with { Seed = 424242, PlayerDecklists = new Dictionary<string, OfficialDecklist> { ["alice"] = deck, ["bob"] = deck } };
    }

    private sealed class Run(MatchState initial)
    {
        private readonly Journal journal = new();
        private IMatchSession? session;
        internal async Task<ResolutionResult> Act(string player, GameCommand command)
        {
            session ??= new MatchSession(initial, new CoreRuleEngine(), journal);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = session.SnapshotFor(player).Tick;
            var wire = JsonSerializer.SerializeToElement(raw, Json);
            var intent = "optional-role-" + journal.Entries.Count;
            async ValueTask<ResolutionResult> Send() => command is ReadyCommand
                ? await session.ReadyAsync(player, intent, wire, default)
                : await session.SubmitAsync(player, intent, command, wire, default);
            var result = await Send();
            Assert.True(result.Accepted, result.ErrorMessage);
            var count = journal.Entries.Count;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash((await Send()).State));
            Assert.Equal(count, journal.Entries.Count);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            Assert.DoesNotContain("openingRoleDrawPendingPlayerIds", JsonSerializer.Serialize(result.Snapshots, Json));
            Assert.DoesNotContain("openingRoleDrawPendingPlayerIds", JsonSerializer.Serialize(result.Prompts, Json));
            Assert.DoesNotContain("openingRoleDrawPendingPlayerIds", JsonSerializer.Serialize(result.Events, Json));
            Assert.DoesNotContain("openingRoleDrawPendingPlayerIds", JsonSerializer.Serialize(MatchReplayRedactor.BuildSpectatorFrame(journal.Entries[^1]), Json));
            return result;
        }

        internal async Task Cold()
        {
            var frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(Frame(), Json), Json)!;
            Assert.Empty(frame.ValidationErrors);
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands,
                frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
            foreach (var player in frame.PlayerViews.Keys)
            {
                Assert.Equal(JsonSerializer.Serialize(frame.PlayerViews[player].Snapshot, Json), JsonSerializer.Serialize(session.SnapshotFor(player), Json));
                Assert.Equal(JsonSerializer.Serialize(frame.PlayerViews[player].Prompt, Json), JsonSerializer.Serialize(session.PromptFor(player), Json));
            }
        }

        internal MatchRecoveryFrame Frame()
        {
            var last = journal.Entries[^1];
            var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId, entry.CommandType,
                entry.RawCommand, entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence,
                entry.Accepted, entry.ErrorMessage) { ErrorCode = entry.ErrorCode, TypedCommand = entry.TypedCommand }).ToArray();
            var events = journal.Entries.SelectMany(entry => entry.Events.Select((item, index) =>
                new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, item))).ToArray();
            var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key, last.CompletedTick,
                last.CompletedEventSequence, pair.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[pair.Key]));
            var errors = MatchRecoveryValidator.Validate(last.RoomId, last.CompletedEventSequence, commands, events, views,
                last.AuthoritativeState, last.CompletedTick);
            return new(last.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, errors,
                last.AuthoritativeState, initial) { OpeningSeed = initial.Seed };
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
