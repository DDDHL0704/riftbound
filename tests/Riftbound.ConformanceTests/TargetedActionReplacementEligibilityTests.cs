using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Real registered-card controls for the action seam. No Gangplank object or
// replacement profile is fabricated while the exact CN profiles are pending.
public sealed class TargetedActionReplacementEligibilityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("UNL-042/219", true)]
    [InlineData("OGN·050/298", false)]
    public async Task AlreadyStunnedTargetDoesNotTriggerVanguardAgainButSeparateDrawStillResolves(string stun, bool draws)
    {
        var run = new Driver(Position(stun));
        await run.Act("P1", new PlayCardCommand("VANGUARD", "OGN·059/298", [], Destination: "BASE"));
        Assert.Equal(7, run.State.CardObjects["VANGUARD"].Power);
        Assert.True(run.State.CardObjects["VANGUARD"].IsExhausted);
        await run.Act("P1", new PlayCardCommand("STUN1", stun, ["TARGET"])); await run.Top();
        Assert.Equal(8, run.State.CardObjects["VANGUARD"].Power);
        Assert.False(run.State.CardObjects["VANGUARD"].IsExhausted);
        Assert.Contains("STUNNED", run.State.CardObjects["TARGET"].UntilEndOfTurnEffects);
        await run.Cold();
        var before = run.Entries.Count;
        await run.Act("P1", new PlayCardCommand("STUN2", stun, ["TARGET"])); await run.Top();
        Assert.Equal(8, run.State.CardObjects["VANGUARD"].Power);
        Assert.Single(run.State.CardObjects["TARGET"].UntilEndOfTurnEffects, e => e == "STUNNED");
        Assert.DoesNotContain(run.Entries.Skip(before).SelectMany(e => e.Events), e => e.Kind == "STATUS_EFFECT_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("effectId"), "STUNNED"));
        Assert.DoesNotContain(run.Entries.Skip(before).SelectMany(e => e.Events), e => e.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Payload.GetValueOrDefault("trigger"), "ECLIPSE_VANGUARD_STUN_TRIGGER_READY_POWER_1"));
        Assert.Equal(draws, run.State.PlayerZones["P1"].Hand.Contains("DRAW1"));
        Assert.Equal(draws, run.State.PlayerZones["P1"].Hand.Contains("DRAW2"));
        await run.Cold();
    }

    [Fact]
    public async Task AlreadyStunnedConditionalReturnChoosesReturnBeforeStunNoOp()
    {
        var run = new Driver(Position("UNL-042/219", opponentTurn: true));
        await run.Act("P2", new MoveUnitCommand("TARGET", "BASE", "BATTLEFIELD:FIELD", []));
        Assert.True(run.State.CardObjects["TARGET"].IsAttacking);
        await run.Act("P2", new PassFocusCommand());
        await run.Act("P1", new PlayCardCommand("STUN1", "UNL-042/219", ["TARGET"])); await run.Top();
        Assert.Contains("STUNNED", run.State.CardObjects["TARGET"].UntilEndOfTurnEffects);
        if (run.State.FocusPlayerId != "P1") await run.Act(run.State.FocusPlayerId!, new PassFocusCommand());
        var before = run.Entries.Count;
        await run.Act("P1", new PlayCardCommand("RETURN", "UNL-134/219", ["TARGET"])); await run.Top();
        Assert.Contains("TARGET", run.State.PlayerZones["P2"].Hand);
        Assert.Equal(1, run.State.CardObjects["TARGET"].ObjectGeneration);
        Assert.DoesNotContain("STUNNED", run.State.CardObjects["TARGET"].UntilEndOfTurnEffects);
        Assert.Contains(run.Entries.Skip(before).SelectMany(e => e.Events), e => e.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.DoesNotContain(run.Entries.Skip(before).SelectMany(e => e.Events), e => e.Kind == "STATUS_EFFECT_APPLIED");
        await run.Cold();
    }

    [Fact]
    public async Task MinimumOneOnPrintedOneMightHasNoNegativeLedgerAndKeepsIndependentDraw()
    {
        var run = new Driver(Position("UNL-042/219"));
        Assert.Equal(1, run.State.CardObjects["TARGET"].Power);
        await run.Act("P1", new PlayCardCommand("NEGATIVE", "OGN·095/298", ["TARGET"])); await run.Top();
        Assert.Equal(1, run.State.CardObjects["TARGET"].Power);
        Assert.Empty(run.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifiers);
        Assert.Contains("DRAW1", run.State.PlayerZones["P1"].Hand);
        var change = Assert.Single(run.Entries.SelectMany(e => e.Events), e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(0, Convert.ToInt32(change.Payload["appliedPowerDelta"]));
        await run.Cold();
    }

    private static MatchState Position(string stun, bool opponentTurn = false)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string card, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, card, owner); locations[id] = new(owner, zone, field); }
        Put("VANGUARD", "OGN·059/298", "P1", "HAND"); Put("STUN1", stun, "P1", "HAND"); Put("STUN2", stun, "P1", "HAND");
        Put("RETURN", "UNL-134/219", "P1", "HAND"); Put("NEGATIVE", "OGN·095/298", "P1", "HAND");
        Put("TARGET", "OGN·096/298", "P2", "BASE"); Put("DEFENDER", "OGN·096/298", "P1", "BATTLEFIELD", "FIELD");
        Put("FIELD", "OGN·278/298", "P1", "BATTLEFIELD", "FIELD"); cards["FIELD"] = cards["FIELD"] with { ControllerId = "P1" };
        foreach (var id in new[] { "DRAW1", "DRAW2", "DRAW3", "PRIVATE-P1" }) Put(id, "OGN·096/298", "P1", "MAIN_DECK");
        Put("PRIVATE-P2", "OGN·096/298", "P2", "MAIN_DECK");
        var turn = opponentTurn ? "P2" : "P1";
        return new MatchState("TARGETED-ACTION-ELIGIBILITY", 0, 5, turn, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: turn, phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["VANGUARD", "STUN1", "STUN2", "RETURN", "NEGATIVE"],
                    Battlefields = ["FIELD", "DEFENDER"], MainDeck = ["DRAW1", "DRAW2", "DRAW3", "PRIVATE-P1"] },
                ["P2"] = PlayerZones.Empty with { Base = ["TARGET"], MainDeck = ["PRIVATE-P2"] } },
            cardObjects: cards, objectLocations: locations, runePools: new Dictionary<string, RunePool> {
                ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 4, [RuneTrait.Purple] = 4, [RuneTrait.Green] = 4 }),
                ["P2"] = new(30, 0) }, seed: 424242) with { CardPlays = new(5, [], []) };
    }

    private sealed class Driver : IMatchJournal
    {
        private readonly MatchState initial;
        internal Driver(MatchState initial) { this.initial = initial; State = initial; }
        internal MatchState State { get; private set; }
        internal List<MatchJournalEntry> Entries { get; } = [];
        private MatchSession? current;
        private MatchSession Session => current ??= new(initial, new CoreRuleEngine(), this);
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
        internal async Task Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = Session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            var payload = JsonSerializer.SerializeToElement(raw); var id = "atomic-action-" + Entries.Count;
            var result = await Session.SubmitAsync(actor, id, GameCommandJsonMapper.Map(payload), payload, default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State;
            var count = Entries.Count;
            Assert.True((await Session.SubmitAsync(actor, id, GameCommandJsonMapper.Map(payload), payload, default)).Accepted);
            Assert.Equal(count, Entries.Count);
        }
        internal async Task Top() { await Act(State.PriorityPlayerId!, new PassPriorityCommand()); await Act(State.PriorityPlayerId!, new PassPriorityCommand()); }
        internal async Task Cold()
        {
            var last = Entries[^1];
            var commands = Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var views = last.Snapshots.ToDictionary(v => v.Key, v => new RecoveredPlayerView(v.Key, last.CompletedTick, last.CompletedEventSequence,
                v.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[v.Key]));
            var frame = new MatchRecoveryFrame(initial.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], State) { OpeningSeed = initial.Seed };
            frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
            Assert.Empty(MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, State, new CoreRuleEngine(), default, frame.Events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            current = MatchSession.Restore(frame, new CoreRuleEngine(), this);
            foreach (var actor in State.Seats.Keys)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(current.SnapshotFor(actor), Json), JsonSerializer.SerializeToNode(last.Snapshots[actor], Json)));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(current.PromptFor(actor), Json), JsonSerializer.SerializeToNode(last.Prompts[actor], Json)));
                var visible = JsonSerializer.Serialize(new { snapshot = current.SnapshotFor(actor), prompt = current.PromptFor(actor) }, Json);
                Assert.DoesNotContain("PRIVATE-P1", visible); Assert.DoesNotContain("PRIVATE-P2", visible);
            }
        }
    }
}
