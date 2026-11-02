using System.Reflection;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadSingularityDrawThresholdTests
{
    private const string Card = "RAD·069/167";
    private const string Effect = TriggerKinds.ControllerDrawHandThresholdDiscardDestroyScore;

    [Theory]
    [InlineData(8, 1, 0)] [InlineData(8, 2, 0)] [InlineData(9, 1, 1)]
    [InlineData(9, 2, 1)] [InlineData(9, 0, 0)] [InlineData(10, 3, 1)]
    public void ADrawBatchExcludesAllCardsInThatInstructionFromTheOtherHandThreshold(int hand, int draw, int expected)
    {
        var result = Draw(Position(hand), draw);
        Assert.Equal(expected, result.State.TriggerQueue.Count(i => i.EffectKind == Effect));
        foreach (var trigger in result.State.TriggerQueue.Where(i => i.EffectKind == Effect))
        {
            Assert.Equal(hand, trigger.DrawHandThreshold!.OtherHandCount);
            Assert.Equal(draw, trigger.DrawHandThreshold.DrawnCount);
        }
        Assert.All(result.Events, e => Assert.DoesNotContain("drawInstructionCapture", e.Payload.Keys));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void OnlySourcesPresentDuringTheInstructionEarnTheSkill(bool presentDuringDraw)
    {
        var before = Position(9);
        if (!presentDuringDraw) before = RadUnitEffectTestFixture.Place(before, "SOURCE", "P1", "GRAVEYARD");
        var zones = before.PlayerZones.ToDictionary(); var cards = before.CardObjects.ToDictionary(); var events = new List<GameEvent>();
        Invoke("ApplyDrawToPlayer", before, zones, cards, before.PlayerScores, "P1", 1, before.RngCursor, events, true);
        var after = before with { Tick = 1, PlayerZones = zones, CardObjects = cards };
        after = RadUnitEffectTestFixture.Place(after, "SOURCE", "P1", presentDuringDraw ? "GRAVEYARD" : "BASE");
        var result = Collect(before, after, events);
        Assert.Equal(presentDuringDraw ? 1 : 0, result.State.TriggerQueue.Count(i => i.EffectKind == Effect));
    }

    [Fact]
    public async Task ConfirmationPrecedesResponsesAndResolutionDiscardsThenDestroysThenScores()
    {
        var game = await Earn();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("DRAW_HAND_THRESHOLD_OPPONENT", choice.ChoiceWindow);
        Assert.Equal(new[] { "P2" }, choice.LegalObjectIds);
        Assert.Null(game.State.PriorityPlayerId);
        Assert.Equal(10, game.State.PlayerZones["P1"].Hand.Count);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2"]));
        Assert.NotNull(game.State.PriorityPlayerId);
        Assert.Equal(0, game.State.PlayerScores["P2"]);
        var prefix = game.Events().Length;
        await game.Top();
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, game.State.PlayerScores["P2"]);
        var events = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
        var discard = Array.FindIndex(events, e => e.Kind == "CARDS_DISCARDED");
        var leave = Array.FindIndex(events, e => e.Kind == "OBJECT_LEFT_FIELD");
        var score = Array.FindIndex(events, e => e.Kind == "SCORE_GAINED");
        Assert.True(discard >= 0 && leave > discard && score > leave);
        await game.Top(); // Already-earned play draw survives the equipment's destruction.
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task MultipleSourcesKeepIndependentSkillsAfterTheFirstEmptiesTheHand()
    {
        var game = await Earn(Source(Position(9), "SECOND"));
        Assert.Equal(2, game.State.StackItems.Count(i => i.EffectKind == Effect));
        await ConfirmAll(game);
        await game.Top(); await game.Top();
        Assert.Equal(2, game.State.PlayerScores["P2"]);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Graveyard);
        await game.Top(); await game.Top();
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData("GRAVEYARD")] [InlineData("CONTROLLER")] [InlineData("REENTERED")]
    public async Task DerivedSourceChangesDoNotCancelDiscardOrTheCapturedOpponentsPoint(string branch)
    {
        var game = await Earn(); await ConfirmAll(game);
        var state = branch == "GRAVEYARD" ? RadUnitEffectTestFixture.Place(game.State, "SOURCE", "P1", "GRAVEYARD")
            : branch == "CONTROLLER" ? RadUnitEffectTestFixture.Place(game.State, "SOURCE", "P2", "BASE")
            : game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                ["SOURCE"] = game.State.CardObjects["SOURCE"] with { ObjectGeneration = 2 } } };
        OfficialInsightAndSpellLockTests.Restore(state);
        var result = await OfficialGraveyardRecastTests.Top(state);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(1, result.State.PlayerScores["P2"]);
        if (branch == "REENTERED") Assert.Contains("SOURCE", result.State.PlayerZones["P1"].Base);
        else Assert.Contains("SOURCE", result.State.PlayerZones["P1"].Graveyard);
        OfficialInsightAndSpellLockTests.Restore(result.State);
    }

    [Theory]
    [InlineData("hand")] [InlineData("draw")] [InlineData("future")]
    [InlineData("generation")] [InlineData("instance")] [InlineData("opponent")]
    public async Task RecoveryRejectsForgedInstructionAndSourceWitnesses(string branch)
    {
        var game = await Earn(); await ConfirmAll(game);
        var item = Assert.Single(game.State.StackItems, i => i.EffectKind == Effect); var c = item.DrawHandThreshold!;
        var forged = branch switch {
            "hand" => c with { OtherHandCount = 8 }, "draw" => c with { DrawnCount = 0 },
            "future" => c with { CapturedTick = game.State.Tick + 1 },
            "generation" => c with { Source = c.Source with { Generation = 200 } },
            "instance" => c with { RuleTextInstanceId = "PRINTED:OTHER:0" },
            _ => c with { OpponentId = "P1" }
        };
        Assert.Contains("invalid stacked draw hand-threshold skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = game.State.StackItems.Select(i => i == item ? i with { DrawHandThreshold = forged } : i).ToArray() }));
    }

    [Fact]
    public async Task ChoosingTheControllerIsRejectedWithoutChangingState()
    {
        var game = await Earn(); var choice = game.State.PendingCardChoice!;
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1"]));
    }

    [Fact]
    public async Task ThePointCanWinAfterDiscardAndDestructionComplete()
    {
        var initial = Position(9) with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var game = await Earn(initial); await ConfirmAll(game); await game.Top();
        Assert.Equal("P2", game.State.WinnerPlayerId);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("plausible-hand")] [InlineData("missing-birth")]
    [InlineData("draw-count")] [InlineData("missing-choice")] [InlineData("rejected-command")]
    public async Task RecoveryAuthenticatesCountsAndChoicesAgainstTheOriginalAcceptedHistory(string branch)
    {
        var game = await Earn(); await ConfirmAll(game);
        var state = game.State; var events = game.Events(); var commands = game.Commands();
        var initial = Put(Position(9), "DRAW", "OGN·048/298", "P1", "HAND");
        MatchRecoveryFrame Frame() => new(state.RoomId, state.Tick, game.Events()[^1].Sequence, commands, events,
            new Dictionary<string, RecoveredPlayerView>(), [], state, initial) { OpeningSeed = initial.Seed };
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(Frame(), new CoreRuleEngine(), default));
        var item = Assert.Single(state.StackItems, i => i.EffectKind == Effect);
        if (branch == "plausible-hand")
        {
            state = state with { StackItems = state.StackItems.Select(i => i == item ? i with {
                DrawHandThreshold = item.DrawHandThreshold! with { OtherHandCount = 10 } } : i).ToArray() };
            Assert.DoesNotContain("invalid stacked draw hand-threshold skill", OfficialInsightAndSpellLockTests.Errors(state));
        }
        else if (branch == "missing-birth") events = events.Where(e => !(e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), Effect))).ToArray();
        else if (branch == "draw-count") events = events.Select(e => e.Event.Kind == "CARD_DRAWN" ? e with {
            Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) { ["count"] = 2 } } } : e).ToArray();
        else if (branch == "missing-choice") events = events.Where(e => e.Event.Kind != "DRAW_HAND_OPPONENT_CONFIRMED").ToArray();
        else commands = commands.Select(c => c.CompletedTick == item.DrawHandThreshold!.CapturedTick
            ? c with { Accepted = false } : c).ToArray();
        Assert.Contains(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(Frame(), new CoreRuleEngine(), default),
            error => error.Contains("draw hand-threshold", StringComparison.Ordinal));
    }

    [Fact]
    public void ADrawContinuedThroughBurnoutStillEarnsOnlyOneThresholdSkill()
    {
        var state = Put(Put(Position(9), "RECYCLE1", "OGN·219/298", "P1", "GRAVEYARD"),
            "RECYCLE2", "OGN·219/298", "P1", "GRAVEYARD");
        var result = Draw(state, 8);
        Assert.Single(result.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(8, Assert.Single(result.State.TriggerQueue, i => i.EffectKind == Effect).DrawHandThreshold!.DrawnCount);
    }

    private static async Task<Driver> Earn(MatchState? initial = null)
    {
        var game = new Driver(Put(initial ?? Position(9), "DRAW", "OGN·048/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", []));
        await game.Top();
        Assert.Contains(game.State.TriggerQueue, i => i.EffectKind == Effect);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue
            .OrderByDescending(i => i.EffectKind == Effect).Select(i => i.TriggerId).ToArray()));
        return game;
    }

    private static async Task ConfirmAll(Driver game)
    {
        for (var i = 0; game.State.PendingCardChoice is { } choice; i++)
        {
            Assert.InRange(i, 0, 3);
            await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2"]));
        }
    }

    private static MatchState Position(int hand)
    {
        var state = Source(Create(), "SOURCE");
        for (var i = 0; i < hand; i++) state = Put(state, "H" + i, "OGN·219/298", "P1", "HAND");
        return state;
    }

    private static MatchState Source(MatchState state, string id)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: Card, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) } };
        return RadUnitEffectTestFixture.Place(state, id, "P1", "BASE");
    }

    private static ResolutionResult Draw(MatchState before, int count)
    {
        var zones = before.PlayerZones.ToDictionary(); var cards = before.CardObjects.ToDictionary(); var events = new List<GameEvent>();
        Invoke("ApplyDrawToPlayer", before, zones, cards, before.PlayerScores, "P1", count, before.RngCursor, events, true);
        return Collect(before, before with { Tick = 1, PlayerZones = zones, CardObjects = cards }, events);
    }
    private static ResolutionResult Collect(MatchState before, MatchState after, IReadOnlyList<GameEvent> events)
        => (ResolutionResult)Invoke("RecordDrawTriggers", before, new ResolutionResult(true, null, after, events,
            new Dictionary<string, SnapshotDto>(), new Dictionary<string, ActionPromptDto>()));
    private static object Invoke(string method, params object?[] args)
        => typeof(CoreRuleEngine).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;
}
