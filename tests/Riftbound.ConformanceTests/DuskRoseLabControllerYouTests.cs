using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

// Current CN190.6.d and191.5: an uncontrolled battlefield has no "you".
public sealed class DuskRoseLabControllerYouTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("P1", "P1", null)]
    [InlineData("P1", "P2", null)]
    [InlineData("P2", "P1", null)]
    [InlineData("P2", "P2", null)]
    [InlineData("P1", "P1", "P2")]
    [InlineData("P1", "P2", "P2")]
    [InlineData("P2", "P1", "P1")]
    [InlineData("P2", "P2", "P1")]
    public async Task UncontrolledOrOtherControlledLabHasNoYourTurnTrigger(
        string turn, string owner, string? controller)
    {
        var initial = Position(turn, owner, controller, units: false);
        if (controller is not null) initial = OfficialHoldSequenceTests.AddUnit(initial, "OTHER-UNIT", "OGN\u00b7001/298", "F", controller);
        var original = MatchStateHasher.Hash(initial);
        var done = await Act(initial, turn, new PassPriorityCommand());
        Assert.Equal(original, MatchStateHasher.Hash(initial));
        Assert.Equal(MatchPhases.Main, done.State.Phase);
        Assert.Null(done.State.PendingCardChoice);
        Assert.Null(done.State.PendingPayment);
        Assert.Empty(done.State.TriggerQueue);
        Assert.Empty(done.State.StackItems);
        Assert.DoesNotContain(done.Events, e => e.Kind == "TRIGGER_QUEUED" && Equals(e.Payload["sourceObjectId"], "F"));
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_DESTROYED" or "BATTLEFIELD_TRIGGER_RESOLVED");
        Assert.Single(done.State.PlayerZones[turn].Hand);
        Assert.Empty(done.State.PlayerZones[Other(turn)].Hand);
        Assert.Equal(owner, done.State.CardObjects["F"].OwnerId);
        Assert.Equal(controller, done.State.CardObjects["F"].ControllerId);
        OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Theory]
    [InlineData("P1", "P1", false)]
    [InlineData("P1", "P2", false)]
    [InlineData("P2", "P1", false)]
    [InlineData("P2", "P2", false)]
    [InlineData("P1", "P1", true)]
    [InlineData("P1", "P2", true)]
    [InlineData("P2", "P1", true)]
    [InlineData("P2", "P2", true)]
    public async Task YourTurnUsesActualControllerForCostDecisionAndDraw(
        string turn, string owner, bool pay)
    {
        var opened = await Act(Position(turn, owner, turn, units: true), turn, new PassPriorityCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal(turn, choice.PlayerId);
        Assert.Equal(new[] { "A", "B" }, choice.LegalObjectIds);
        Assert.Empty(opened.State.PlayerZones[turn].Hand);
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["B"] : []);
        var rejected = await new CoreRuleEngine().ResolveAsync(opened.State, new("other-lab-choice", Other(turn), command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
        OfficialInsightAndSpellLockTests.Restore(opened.State);
        var result = await Act(opened.State, turn, command);
        if (pay)
        {
            Assert.Contains("B", result.State.PlayerZones[turn].Graveyard);
            Assert.Empty(result.State.PlayerZones[turn].Hand);
            var item = Assert.Single(result.State.StackItems);
            Assert.Equal(turn, item.ControllerId);
            Assert.Equal(turn, item.BattlefieldStart!.ControllerId);
            Assert.Equal("UNL-209/219", item.BattlefieldStart.CardNo);
            Assert.Equal("B", item.TriggerCost!.DestructionTarget!.ObjectId);
            OfficialInsightAndSpellLockTests.Restore(result.State);
            result = await Top(result.State);
            Assert.Contains(result.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED" && Equals(e.Payload["playerId"], turn));
        }
        Assert.Equal(pay ? 2 : 1, result.State.PlayerZones[turn].Hand.Count);
        Assert.Empty(result.State.PlayerZones[Other(turn)].Hand);
        Assert.Contains("A", result.State.PlayerZones[turn].Battlefields);
        Assert.Equal(pay, result.State.PlayerZones[turn].Graveyard.Contains("B"));
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Equal(owner, result.State.CardObjects["F"].OwnerId);
        OfficialInsightAndSpellLockTests.Restore(result.State);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("context")]
    [InlineData("both")]
    [InlineData("event")]
    [InlineData("event-and-both")]
    [InlineData("missing-controller")]
    [InlineData("missing-origin")]
    [InlineData("duplicate-origin")]
    [InlineData("no-history")]
    public async Task OriginalLabTriggerBirthAndStrictReplayRejectConsistentlyForgedControllers(string branch)
    {
        var original = await RecordedPaidLab();
        var originalHash = MatchStateHasher.HashValue(original);
        Assert.Equal("bob", Assert.Single(original.AuthoritativeState!.StackItems).ControllerId);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(original, new CoreRuleEngine(), default));
        var state = original.AuthoritativeState!;
        var item = Assert.Single(state.StackItems);
        var context = JsonSerializer.SerializeToNode(item.BattlefieldStart, Json)!;
        if (branch is "context" or "both" or "event-and-both") context["controllerId"] = "alice";
        if (branch == "missing-controller") context.AsObject().Remove("controllerId");
        item = item with {
            ControllerId = branch is "actor" or "both" or "event-and-both" ? "alice" : item.ControllerId,
            BattlefieldStart = context.Deserialize<BattlefieldStartContext>(Json) };
        state = state with { StackItems = [item] };
        var origin = Assert.Single(original.Events, e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload["effectKind"], "CAPTURED_BATTLEFIELD_START_DESTROY_DRAW"));
        var events = original.Events.ToArray();
        if (branch is "event" or "event-and-both") events = events.Select(e => e == origin
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) { ["controllerId"] = "alice" } } }
            : e).ToArray();
        if (branch == "missing-origin") events = events.Where(e => e != origin).ToArray();
        if (branch == "duplicate-origin") events = [..events, origin];
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick,
            original.LastEventSequence, snapshots[p], state.Tick, original.LastEventSequence, prompts[p]));
        var forged = original with { AuthoritativeState = state, Events = events, PlayerViews = views,
            SpectatorReplayFrame = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, original.LastEventSequence,
                events.Select(e => e.Event).ToArray(), state) };
        if (branch == "no-history") forged = forged with { Commands = [], Events = [], OpeningSeed = state.Seed };
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default));
        Assert.Equal(originalHash, MatchStateHasher.HashValue(original));
    }

    private static MatchState Position(string turn, string owner, string? controller, bool units)
    {
        var state = At(OfficialHoldSequenceTests.State("UNL-209/219"), "F", owner, "BATTLEFIELD", "F");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        cards["F"] = cards["F"] with { OwnerId = owner, ControllerId = controller };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = null };
        if (turn == "P2")
        {
            var originZones = zones["P1"];
            zones["P2"] = zones["P2"] with { MainDeck = originZones.MainDeck, RuneDeck = originZones.RuneDeck };
            zones["P1"] = originZones with { MainDeck = [], RuneDeck = [] };
            foreach (var id in originZones.MainDeck.Concat(originZones.RuneDeck))
            {
                cards[id] = cards[id] with { OwnerId = turn, ControllerId = turn };
                locations[id] = locations[id] with { PlayerId = turn };
            }
        }
        state = state with { TurnPlayerId = turn, ActivePlayerId = turn, CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        if (units)
        {
            state = OfficialHoldSequenceTests.AddUnit(state, "A", "OGN\u00b7001/298", "F", turn);
            state = OfficialHoldSequenceTests.AddUnit(state, "B", "OGN\u00b7001/298", "F", turn);
        }
        return state;
    }

    private static string Other(string player) => player == "P1" ? "P2" : "P1";
    private static async Task<MatchRecoveryFrame> RecordedPaidLab()
    {
        var journal = new Journal();
        var session = new MatchSession("recorded-lab-you-" + Guid.NewGuid().ToString("N"), new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        const string scenario = "battlefield-turn-start-destroy-draw";
        var seeded = await session.SeedScenarioAsync("alice", "lab-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        var opened = await session.SubmitAsync("alice", "begin-bob-lab", new EndTurnCommand(),
            JsonSerializer.SerializeToElement(new { cmdType = "END_TURN" }), default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.Equal("bob", opened.State.TurnPlayerId);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal("bob", choice.PlayerId);
        Assert.Contains("P2-BATTLEFIELD-ROSE-SACRIFICE", choice.LegalObjectIds);
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2-BATTLEFIELD-ROSE-SACRIFICE"]);
        var paid = await session.SubmitAsync("bob", "original-lab-cost", command,
            JsonSerializer.SerializeToElement(command, command.GetType(), Json), default);
        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal("bob", Assert.Single(paid.State.StackItems).ControllerId);
        Assert.Equal("P2-BATTLEFIELD-ROSE-SACRIFICE", paid.State.StackItems[0].TriggerCost!.DestructionTarget!.ObjectId);
        var last = journal.Entries[^1];
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((value, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, value))).ToArray();
        var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, last.CompletedTick,
            last.CompletedEventSequence, p.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[p.Key]));
        Assert.Empty(MatchRecoveryValidator.Validate(session.RoomId, last.CompletedEventSequence, commands, events,
            views, last.AuthoritativeState, last.CompletedTick));
        return new(session.RoomId, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState,
            MatchState.Create(session.RoomId) with { Seed = journal.Entries[0].OpeningSeed }) { OpeningSeed = journal.Entries[0].OpeningSeed };
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

}
