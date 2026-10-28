using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ShiranaPaidBoonReceiptCannotBeReboundToAnotherUnit(string player)
    {
        var (initial, game, choice) = await OpenShiranaBoonChoice(player);
        await game.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var item = Assert.Single(game.State.StackItems, s => s.BattlefieldConquestConsumeBoon is not null);
        var receipt = Assert.IsType<TriggerCostReceipt>(item.TriggerCost);
        Assert.Equal("ALLY", receipt.ConsumedBoon!.ObjectId);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);

        var forgedItem = item with { TriggerCost = receipt with {
            ConsumedBoon = new("UNIT", game.State.CardObjects["UNIT"].ObjectGeneration) } };
        var forged = game.State with { StackItems = game.State.StackItems
            .Select(s => s.StackItemId == item.StackItemId ? forgedItem : s).ToArray() };
        await AssertShiranaPaymentHistoryRejects(initial, game, forged);
    }

    [Fact]
    public async Task ShiranaForgedPaidReceiptWithoutBoonConsumptionCannotRestore()
    {
        var (initial, game, choice) = await OpenShiranaBoonChoice("P1");
        var simulated = await new CoreRuleEngine().ResolveAsync(game.State,
            new("shirana-not-journalled-payment", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]), default);
        Assert.True(simulated.Accepted, simulated.ErrorMessage);
        Assert.Equal("ALLY", Assert.Single(simulated.State.StackItems,
            s => s.BattlefieldConquestConsumeBoon is not null).TriggerCost!.ConsumedBoon!.ObjectId);

        // Keep the original journal and boon; forge the paid continuation and all views.
        var forged = simulated.State with { Tick = game.State.Tick, CardObjects = game.State.CardObjects,
            PlayerZones = game.State.PlayerZones, ObjectLocations = game.State.ObjectLocations };
        Assert.Contains(CardObjectTags.Boon, forged.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BOON_CONSUMED");
        await AssertShiranaPaymentHistoryRejects(initial, game, forged);
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Game, PendingCardChoiceState Choice)>
        OpenShiranaBoonChoice(string player)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·282/298", boon: true);
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act(player, new PassFocusCommand());
        await game.Act(ThreeBattlefieldTimingProbe.Other(player), new PassFocusCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(player, choice.PlayerId);
        Assert.Contains("ALLY", choice.LegalObjectIds);
        Assert.Contains("UNIT", choice.LegalObjectIds);
        Assert.DoesNotContain("ENEMY-BOON", choice.LegalObjectIds);
        return (initial, game, choice);
    }

    private static async Task AssertShiranaPaymentHistoryRejects(MatchState initial,
        StandbyDefendObserverTests.Driver game, MatchState forged)
    {
        var frame = VayneRecoveryFrame(initial, game, forged);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, forged,
            new CoreRuleEngine(), default, JournalRoundTrip(frame.Events));
        Assert.False(replay.IsMatch);
        Assert.DoesNotContain(frame.ValidationErrors, e => e.Contains("invalid trigger cost receipt", StringComparison.Ordinal));
        Assert.Contains(frame.ValidationErrors, e => e.Contains("boon cost", StringComparison.OrdinalIgnoreCase)
            && e.Contains("payment history", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance));
    }
}
