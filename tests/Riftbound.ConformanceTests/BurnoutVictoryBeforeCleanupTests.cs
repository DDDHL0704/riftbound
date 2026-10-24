using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN core effective 2026-10-16: finish the parent (321), then normal
// victory (323.1) precedes lethal cleanup (323.4). These are controlled
// CoreRules positions, not certification of an ordinary opening or deck.
public sealed class BurnoutVictoryBeforeCleanupTests
{
    [Theory]
    [InlineData(false, 6)]
    [InlineData(false, 7)]
    [InlineData(true, 6)]
    [InlineData(true, 7)]
    public async Task FirstBurnoutFinishesDrawAndRuneCallBeforeVictoryOrLethalCleanup(
        bool woundedByRealSpell, int opponentScore)
    {
        var initial = Position(opponentScore);
        var driver = new Driver(initial);
        await DamageIfRequested(driver, woundedByRealSpell);
        Assert.Equal(woundedByRealSpell ? 10 : 9, Might(driver.State));
        Assert.True(Might(driver.State) > driver.State.CardObjects["MUNDO"].Damage);

        await driver.Act("P1", new PlayCardCommand("DRAW", "OGN·047/298", []));
        await FinishTop(driver);

        // Replay the actual accepted damage prefix and draw parent before
        // checking the independent source expectation, including the red case.
        await driver.Replay();
        Assert.Equal(woundedByRealSpell ? 6 : 3, driver.Commands().Length);
        var expectedWin = opponentScore == 7;
        var expectedAlive = !woundedByRealSpell || expectedWin;
        Assert.Equal(expectedAlive, driver.State.PlayerZones["P1"].Base.Contains("MUNDO"));
        Assert.Equal(!expectedAlive, driver.State.PlayerZones["P1"].Graveyard.Contains("MUNDO"));
        Assert.Equal(opponentScore + 1, driver.State.PlayerScores["P2"]);
        Assert.Equal(expectedWin ? "P2" : null, driver.State.WinnerPlayerId);
        Assert.Equal(expectedWin ? MatchStatuses.Finished : MatchStatuses.InProgress, driver.State.Status);
        Assert.Contains("RUNE", driver.State.PlayerZones["P1"].Base);
        Assert.True(driver.State.CardObjects["RUNE"].IsExhausted);
        Assert.Contains("DRAW", driver.State.PlayerZones["P1"].Graveyard);
        var events = driver.Events();
        var drawn = Assert.Single(events, e => e.Event.Kind == "CARD_DRAWN");
        var rune = Assert.Single(events, e => e.Event.Kind == "RUNES_CALLED");
        Assert.Single(events, e => e.Event.Kind == "BURNOUT_APPLIED");
        Assert.True(drawn.Sequence < rune.Sequence);
        Assert.Equal(expectedWin ? 1 : 0, events.Count(e => e.Event.Kind == "MATCH_WON"));
        var destroyed = events.Where(e => e.Event.Kind == "UNIT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "MUNDO")).ToArray();
        if (expectedAlive) Assert.Empty(destroyed);
        else Assert.True(rune.Sequence < Assert.Single(destroyed).Sequence);
        if (expectedWin)
            Assert.True(rune.Sequence < Assert.Single(events, e => e.Event.Kind == "MATCH_WON").Sequence);
    }

    // The existing preselected CARDS branch is used only to make an 8:8
    // tie. This does not certify ownership of Portalpalooza's printed mode.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TiedFirstBurnoutsStillPerformNormalLethalCleanupAfterBothDraws(bool woundedByRealSpell)
    {
        var initial = Position(7, portal: true);
        var driver = new Driver(initial);
        await DamageIfRequested(driver, woundedByRealSpell);
        await driver.Act("P1", new PlayCardCommand("DRAW", "OGN·071/298", [], "OTHER_PLAYERS_CHOOSE_CARDS"));
        await FinishTop(driver);
        await driver.Replay();
        Assert.Equal(8, driver.State.PlayerScores["P1"]);
        Assert.Equal(8, driver.State.PlayerScores["P2"]);
        Assert.Null(driver.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status);
        Assert.Equal(!woundedByRealSpell, driver.State.PlayerZones["P1"].Base.Contains("MUNDO"));
        Assert.Equal(woundedByRealSpell, driver.State.PlayerZones["P1"].Graveyard.Contains("MUNDO"));
        Assert.Single(driver.State.PlayerZones["P2"].Hand);
        var events = driver.Events();
        Assert.Equal(2, events.Count(e => e.Event.Kind == "BURNOUT_APPLIED"));
        Assert.Equal(2, events.Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.DoesNotContain(events, e => e.Event.Kind is "MATCH_WON" or "RUNES_CALLED");
        if (woundedByRealSpell)
        {
            var death = Assert.Single(events, e => e.Event.Kind == "UNIT_DESTROYED"
                && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "MUNDO"));
            Assert.All(events.Where(e => e.Event.Kind == "CARD_DRAWN"), draw => Assert.True(draw.Sequence < death.Sequence));
        }
    }

    private static async Task DamageIfRequested(Driver driver, bool wounded)
    {
        Assert.Equal(0, driver.State.CardObjects["MUNDO"].Damage);
        Assert.Equal(6, driver.State.CardObjects["MUNDO"].Power);
        Assert.Equal(9, Might(driver.State));
        if (!wounded) return;
        await driver.Act("P1", new PlayCardCommand("DAMAGE", "OGS·022/024", ["MUNDO"]));
        await FinishTop(driver);
        Assert.Equal(8, driver.State.CardObjects["MUNDO"].Damage);
        Assert.Contains("MUNDO", driver.State.PlayerZones["P1"].Base);
        Assert.Contains("DAMAGE", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(10, Might(driver.State));
    }

    private static async Task FinishTop(Driver driver)
    {
        var id = driver.State.StackItems.Last().StackItemId;
        for (var i = 0; i < 8 && driver.State.StackItems.Any(item => item.StackItemId == id); i++)
            await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.DoesNotContain(driver.State.StackItems, item => item.StackItemId == id);
    }

    private static int Might(MatchState state) => CurrentPowerTargetTests.Snapshot(state)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("MUNDO")
        .GetProperty("effectivePower").GetInt32();

    private static MatchState Position(int opponentScore, bool portal = false)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string zone, string owner = "P1", string? field = null)
        {
            var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
            object?[] args = [id, owner, no, null];
            Assert.True((bool)factory.GetMethod("TryCreate", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!);
            cards[id] = Assert.IsType<CardObjectState>(args[3]);
            locations[id] = new(owner, zone, field);
        }
        Put("MUNDO", "OGN·109/298", "BASE");
        Put("DAMAGE", "OGS·022/024", "HAND");
        Put("DRAW", portal ? "OGN·071/298" : "OGN·047/298", "HAND");
        Put("RUNE", "SFD·R03", "RUNE_DECK");
        Put("F", "OGN·278/298", "BATTLEFIELD", field: "F");
        Put("OTHER", "OGN·294/298", "BATTLEFIELD", "P2", "OTHER");
        foreach (var id in new[] { "G0", "G1", "G2" }) Put(id, "OGN·083/298", "GRAVEYARD");
        if (portal)
            foreach (var id in new[] { "P2G0", "P2G1", "P2G2" }) Put(id, "OGN·083/298", "GRAVEYARD", "P2");
        return new MatchState("CONTROLLED-MUNDO-CLEANUP-" + portal + "-" + opponentScore, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["MUNDO"], Hand = ["DAMAGE", "DRAW"], Graveyard = ["G0", "G1", "G2"], RuneDeck = ["RUNE"], Battlefields = ["F"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER"], Graveyard = portal ? ["P2G0", "P2G1", "P2G2"] : [] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = portal ? 7 : 0, ["P2"] = opponentScore },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) }, seed: 424242)
        { CardPlays = new(5, [], []) };
    }
    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private MatchSession session;
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        internal MatchState State { get; private set; }
        internal Driver(MatchState initial)
        {
            this.initial = initial;
            State = initial;
            session = new(initial, new CoreRuleEngine(), this);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            entries.Add(entry);
            return ValueTask.CompletedTask;
        }
        internal RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(
            e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick,
            e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted,
            e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        internal RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        internal async Task Act(string actor, GameCommand command)
        {
            var prompt = session.PromptFor(actor);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = prompt.PromptId;
            raw["snapshotTick"] = State.Tick;
            var result = await session.SubmitAsync(actor, "mundo-original-" + entries.Count,
                command, JsonSerializer.SerializeToElement(raw, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            State = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var sequence = entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(State);
            var prompts = ResolutionResult.BuildPrompts(State);
            var views = State.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(
                id, State.Tick, sequence, snapshots[id], State.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.Validate(State.RoomId, sequence, Commands(), Events(),
                views, State, State.Tick, spectator);
            Assert.Empty(errors);
            var frame = new MatchRecoveryFrame(State.RoomId, State.Tick, sequence, Commands(), Events(),
                views, errors, State, initial, spectator) { OpeningSeed = initial.Seed };
            session = MatchSession.Restore(frame, new CoreRuleEngine(), this);
        }
        internal async Task Replay()
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State,
                new CoreRuleEngine(), default, Events());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
    }

}
