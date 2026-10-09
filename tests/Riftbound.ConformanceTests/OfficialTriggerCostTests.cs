using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 204.3.a, 337.1.b, 383.3.e.2: cost -> response -> effect.
public sealed class OfficialTriggerCostTests
{
    internal static MatchState Position(string no) => no == "SFD·214/221"
        ? OfficialHoldSequenceTests.State(no)
        : OfficialHoldSequenceTests.AddLegend(OfficialHoldSequenceTests.State("OGN·294/298"), no);
    private static Task<ResolutionResult> Act(MatchState state, GameCommand command, string player = "P1")
        => OfficialGraveyardRecastTests.Act(state, player, command);
    internal static GameCommand Choose(MatchState state, bool accept) => state.PendingCardChoice is { } choice
        ? new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["LEGEND"] : [])
        : new PayCostCommand(state.PendingPayment!.PaymentId, state.PendingPayment.PaymentWindow, [accept ? "PAY" : "DECLINE"]);

    [Theory]
    [InlineData("UNL-193/219")]
    [InlineData("SFD·201/221")]
    [InlineData("SFD·214/221")]
    [InlineData("UNL-232/219")]
    [InlineData("UNL-232*/219")]
    [InlineData("SFD·249/221")]
    public async Task PaymentIsBeforeFirstResponseAndEffectWaitsForBothPlayers(string no)
    {
        var opened = await Act(Position(no), new PassPriorityCommand());
        OfficialGraveyardRecastTests.Restore(opened.State);
        Assert.Null(opened.State.PriorityPlayerId);
        var paid = await Act(opened.State, Choose(opened.State, true));
        OfficialGraveyardRecastTests.Restore(paid.State);
        var receipt = Assert.IsType<TriggerCostReceipt>(paid.State.StackItems.Single().TriggerCost);
        Assert.Empty(paid.State.PlayerZones["P1"].Hand);
        Assert.Equal(1, paid.State.PlayerScores["P1"]);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED");
        Assert.Equal("P1", paid.State.PriorityPlayerId);
        if (no == "SFD·214/221") { Assert.Equal(4, receipt.Power); Assert.Equal(5, paid.State.RunePools["P1"].TotalPower); }
        else Assert.True(paid.State.CardObjects["LEGEND"].IsExhausted);
        var passed = await Act(paid.State, new PassPriorityCommand());
        Assert.Equal("P2", passed.State.PriorityPlayerId);
        Assert.Single(passed.State.StackItems);
        Assert.Empty(passed.State.PlayerZones["P1"].Hand);
        var done = await Act(passed.State, new PassPriorityCommand(), "P2");
        Assert.Empty(done.State.StackItems);
        Assert.Equal(no.StartsWith("UNL-") ? 2 : 1, done.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(no == "SFD·214/221" ? 2 : 1, done.State.PlayerScores["P1"]);
        Assert.Equal(no is "SFD·201/221" or "SFD·249/221" ? 1 : 0, done.Events.Count(e => e.Kind == "EQUIPMENT_TOKEN_CREATED"));
        OfficialGraveyardRecastTests.Restore(done.State);
    }

    [Theory]
    [InlineData("UNL-193/219", false)]
    [InlineData("UNL-193/219", true)]
    [InlineData("SFD·201/221", false)]
    [InlineData("SFD·201/221", true)]
    public async Task CapturedPaidEffectSurvivesSourceChangeAndNeverExhaustsAgain(string no, bool newGeneration)
    {
        var opened = await Act(Position(no), new PassPriorityCommand());
        var paid = await Act(opened.State, Choose(opened.State, true));
        var cards = paid.State.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        // Model a response changing the source after its cost was committed.
        cards["LEGEND"] = cards["LEGEND"] with { IsExhausted = false, ControllerId = "P2",
            ObjectGeneration = newGeneration ? 1 : 0 };
        var changed = paid.State with { CardObjects = cards };
        OfficialGraveyardRecastTests.Restore(changed);
        var result = await OfficialGraveyardRecastTests.Top(changed);
        Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(no == "UNL-193/219" ? 2 : 1, result.State.PlayerZones["P1"].Hand.Count);
        if (no == "SFD·201/221") Assert.All(result.Events.Where(e => e.Kind == "EQUIPMENT_TOKEN_CREATED"),
            e => Assert.Equal("P1", result.State.CardObjects[(string)e.Payload["tokenObjectId"]!].ControllerId));
        Assert.DoesNotContain(result.Events, e => e.Kind == "LEGEND_EXHAUSTED");
    }

    [Theory]
    [InlineData("UNL-193/219")]
    [InlineData("SFD·201/221")]
    public async Task MultipleHeldFieldsCannotReuseOneExhaustCostBeforeResponses(string no)
    {
        var state = Position(no);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["OTHER"] = cards["OTHER"] with { ControllerId = "P1" };
        var result = await Act(state with { CardObjects = cards }, new PassPriorityCommand());
        Assert.Equal(2, result.State.TriggerQueue.Count);
        result = await Act(result.State, new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Null(result.State.PriorityPlayerId);
        result = await Act(result.State, Choose(result.State, true));
        Assert.Single(result.State.StackItems);
        Assert.NotNull(result.State.PriorityPlayerId);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Contains(result.Events, e => e.Kind == "TRIGGER_NOT_CONFIRMED");
        result = await OfficialGraveyardRecastTests.Top(result.State);
        Assert.Equal(no == "UNL-193/219" ? 2 : 1, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(no == "SFD·201/221" ? 1 : 0, result.Events.Count(e => e.Kind == "EQUIPMENT_TOKEN_CREATED"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedPowerAndExhaustCostsRespectPlayerTriggerOrder(bool powerFirst)
    {
        var state = OfficialHoldSequenceTests.AddLegend(Position("SFD·214/221"), "UNL-193/219");
        var opened = await Act(state, new PassPriorityCommand());
        // The command specifies resolution order; insertion/confirmation is the reverse.
        var ordered = opened.State.TriggerQueue.OrderBy(t => (t.HeldContext!.Kind == "PAY_POWER_SCORE") == powerFirst ? 1 : 0).Select(t => t.TriggerId).ToArray();
        var first = await Act(opened.State, new OrderTriggersCommand(OrderedTriggerIds: ordered));
        Assert.Equal(powerFirst, first.State.PendingPayment is not null);
        var next = await Act(first.State, Choose(first.State, true));
        Assert.Null(next.State.PriorityPlayerId);
        Assert.Equal(!powerFirst, next.State.PendingPayment is not null);
        Assert.Equal(1, next.State.PlayerScores["P1"]);
        Assert.Empty(next.State.PlayerZones["P1"].Hand);
        OfficialGraveyardRecastTests.Restore(next.State);
        next = await Act(next.State, Choose(next.State, true));
        Assert.All(next.State.StackItems, i => Assert.NotNull(i.TriggerCost));
        Assert.NotNull(next.State.PriorityPlayerId);
        var finished = await TurnSequenceTestDriver.Complete(next);
        Assert.Equal(2, finished.State.PlayerScores["P1"]);
        Assert.Equal(2, finished.State.PlayerZones["P1"].Hand.Count);
    }

    [Fact]
    public async Task PaymentCanUseRuneResourcesWithoutOpeningOpponentResponseOrEarlyScoring()
    {
        var state = Position("SFD·214/221");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones["P1"] = zones["P1"] with { RuneDeck = [], Base = ["R1", "R2", "R3"] };
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        foreach (var id in new[] { "R1", "R2", "R3" }) locations[id] = new("P1", "BASE");
        var opened = await Act(state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 3), ["P2"] = RunePool.Empty } }, new PassPriorityCommand());
        var recycled = await Act(opened.State, new RecycleRuneCommand("R1"));
        Assert.Equal(opened.State.PendingPayment!.PaymentId, recycled.State.PendingPayment!.PaymentId);
        Assert.Null(recycled.State.PriorityPlayerId);
        Assert.Equal(1, recycled.State.PlayerScores["P1"]);
        OfficialGraveyardRecastTests.Restore(recycled.State);
        var paid = await Act(recycled.State, Choose(recycled.State, true));
        Assert.Equal(0, paid.State.RunePools["P1"].TotalPower);
        var done = await OfficialGraveyardRecastTests.Top(paid.State);
        Assert.Equal(2, done.State.PlayerScores["P1"]);
    }

    [Theory]
    [InlineData("UNL-193/219")]
    [InlineData("SFD·201/221")]
    [InlineData("SFD·214/221")]
    public async Task ForgedCostReceiptOrMissingConfirmationCannotRecover(string no)
    {
        var opened = await Act(Position(no), new PassPriorityCommand());
        var paid = await Act(opened.State, Choose(opened.State, true));
        var item = paid.State.StackItems.Single();
        foreach (var receipt in new[] { item.TriggerCost! with { Power = 99 }, item.TriggerCost! with { Source = new(item.SourceObjectId, 99) } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [item with { TriggerCost = receipt }] }), e => e.Contains("cost receipt"));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [item with { TriggerCost = null }] }), e => e.Contains("unconfirmed trigger cost"));
        if (opened.State.PendingPayment is { } payment)
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with { PendingPayment = payment with { PowerCost = 0 } }), e => e.Contains("trigger cost payment"));
        else
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with { PendingCardChoice = opened.State.PendingCardChoice! with { LegalObjectIds = ["F"] } }), e => e.Contains("trigger cost choice"));
    }

    [Fact]
    public async Task WinningScoreWaitsForPaidEffectResolution()
    {
        var opened = await Act(Position("SFD·214/221") with { PlayerScores = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 0 } }, new PassPriorityCommand());
        Assert.Equal(7, opened.State.PlayerScores["P1"]);
        var paid = await Act(opened.State, Choose(opened.State, true));
        Assert.Equal(MatchStatuses.InProgress, paid.State.Status);
        Assert.Null(paid.State.WinnerPlayerId);
        var done = await OfficialGraveyardRecastTests.Top(paid.State);
        Assert.Equal(MatchStatuses.Finished, done.State.Status);
        Assert.Equal("P1", done.State.WinnerPlayerId);
    }
}
