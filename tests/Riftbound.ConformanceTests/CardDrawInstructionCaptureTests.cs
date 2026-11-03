using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class CardDrawInstructionCaptureTests
{
    private const string CaptureKey = "drawInstructionCapture";

    [Theory]
    [InlineData(0, 1)] [InlineData(8, 2)] [InlineData(9, 2)]
    public void ADrawBatchExcludesAllOfItsNewCardsFromOtherHandCount(int handCount, int drawCount)
    {
        var (state, events, _) = Draw(Position(handCount), drawCount);
        Assert.Equal(handCount + drawCount, state.PlayerZones["P1"].Hand.Count);
        AssertCapture(Assert.Single(events, e => e.Kind == "CARD_DRAWN"), handCount, drawCount);
    }

    [Fact]
    public void BurnoutWithinOneInstructionDoesNotSplitItsOtherHandCount()
    {
        var initial = Position(8); var own = initial.PlayerZones["P1"];
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = own with { MainDeck = own.MainDeck.Take(1).ToArray(), Graveyard = own.MainDeck.Skip(1).ToArray() } } };
        var (state, events, _) = Draw(initial, 2);
        Assert.Equal(10, state.PlayerZones["P1"].Hand.Count);
        Assert.Single(events, e => e.Kind == "BURNOUT_APPLIED");
        AssertCapture(Assert.Single(events, e => e.Kind == "CARD_DRAWN"), 8, 2);
    }

    [Fact]
    public void ASelectedDeckDrawCapturesTheHandBeforeAddingAllSelectedCards()
    {
        var state = Position(9); var zones = state.PlayerZones.ToDictionary(); var cards = state.CardObjects.ToDictionary();
        var result = Invoke("DrawSelectedMainDeckTargetsAndRecycleRest", state, zones, cards,
            "P1", "ALLY", zones["P1"].MainDeck.Take(2).ToArray(), 3, true, false);
        var events = (IReadOnlyList<GameEvent>)Property(result, "Events");
        Assert.Equal(11, zones["P1"].Hand.Count);
        AssertCapture(Assert.Single(events, e => e.Kind == "CARD_DRAWN"), 9, 2);
    }

    [Fact]
    public void TurnStartUsesTheDrawsOwnCountEvenWhenTheEventBuilderSeesALaterHand()
    {
        var initial = Position(9);
        var draw = Invoke("DrawCards", initial, initial.CardObjects.ToDictionary(), "P1", initial.PlayerZones["P1"], 1, false);
        var changed = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = [] } } };
        var events = (IReadOnlyList<GameEvent>)Invoke("BuildTurnStartEvents", changed, 0, draw, Array.Empty<GameEvent>());
        AssertCapture(Assert.Single(events, e => e.Kind == "CARD_DRAWN"), 9, 1);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void SecondDrawUsesTheInstructionSnapshotAndPublishesNoCapture(bool sourcePresentAtDraw)
    {
        var initial = Position(8);
        if (sourcePresentAtDraw) initial = Put(initial, "GEM", "UNL-074/219", "P1", "BASE");
        var (state, events, _) = Draw(initial, 2);
        state = sourcePresentAtDraw ? RadUnitEffectTestFixture.Place(state, "GEM", "P1", "GRAVEYARD")
            : Put(state, "GEM", "UNL-074/219", "P1", "BASE");
        var result = Collect(initial, state, events);
        Assert.Equal(2, result.State.DrawLedger.Counts["P1"]);
        Assert.Equal(sourcePresentAtDraw ? 1 : 0, result.State.TriggerQueue.Count);
        Assert.All(result.Events, e => Assert.DoesNotContain(CaptureKey, e.Payload.Keys));
        Assert.DoesNotContain("OtherHandCount", JsonSerializer.Serialize(result.Events));
    }

    [Fact]
    public void CaptureIsAlsoRemovedOutsideThePerTurnLedgerPhases()
    {
        var initial = Position(9); var (state, events, _) = Draw(initial, 1);
        var result = Collect(initial, state with { Phase = "SETUP" }, events);
        Assert.All(result.Events, e => Assert.DoesNotContain(CaptureKey, e.Payload.Keys));
    }

    [Fact]
    public void EventCountCannotOverrideTheInstructionThatActuallyDrewCards()
    {
        var initial = Position(8); var (state, events, _) = Draw(initial, 2);
        var forged = events.Select(e => e.Kind == "CARD_DRAWN" ? e with {
            Payload = new Dictionary<string, object?>(e.Payload) { ["count"] = 3 } } : e).ToArray();
        var error = Assert.Throws<TargetInvocationException>(() => Collect(initial, state, forged));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static MatchState Position(int handCount)
    {
        var state = Create();
        for (var i = 0; i < handCount; i++) state = Put(state, "H" + i, "OGN·219/298", "P1", "HAND");
        return state;
    }

    private static (MatchState State, IReadOnlyList<GameEvent> Events, object Result) Draw(MatchState state, int count)
    {
        var zones = state.PlayerZones.ToDictionary(); var cards = state.CardObjects.ToDictionary(); var events = new List<GameEvent>();
        var draw = Invoke("ApplyDrawToPlayer", state, zones, cards, state.PlayerScores, "P1", count, state.RngCursor, events, true);
        return (state with { PlayerZones = zones, CardObjects = cards }, events, draw);
    }

    private static ResolutionResult Collect(MatchState before, MatchState after, IReadOnlyList<GameEvent> events)
        => (ResolutionResult)Invoke("RecordDrawTriggers", before, new ResolutionResult(true, null, after, events,
            new Dictionary<string, SnapshotDto>(), new Dictionary<string, ActionPromptDto>()));

    private static void AssertCapture(GameEvent ev, int hand, int drawn)
    {
        var capture = Assert.IsAssignableFrom<object>(ev.Payload[CaptureKey]);
        Assert.Equal("P1", Property(capture, "PlayerId"));
        Assert.Equal(hand, Property(capture, "OtherHandCount"));
        Assert.Equal(drawn, Property(capture, "DrawnCount"));
    }

    private static object Property(object instance, string name)
        => instance.GetType().GetProperty(name)!.GetValue(instance)!;
    private static object Invoke(string name, params object?[] args)
        => typeof(CoreRuleEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;
}
