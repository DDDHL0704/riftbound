using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MightyFaerieArrivalTests
{
    private const string Faerie = "SFD·125/221";
    private static MatchState Position(bool inField = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "FAERIE", Faerie,
            "P1", inField ? "BATTLEFIELD" : "BASE", inField ? "BF" : null);
        state = RadGearTestPosition.Put(state, "FOOTHOLD", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "PURPLE", "UNL-R05", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "BLUE", "UNL-R03", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "BLINK", "OGS·011/024", "P1", "HAND");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0) }, CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
    }
    private static Task Move(Driver game, bool toBase = false)
        => game.Act("P1", new MoveUnitCommand("FAERIE", toBase ? "BATTLEFIELD" : "BASE", toBase ? "BASE" : "BATTLEFIELD:BF"));
    private static async Task Confirm(Driver game, bool pay = true)
    {
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
        for (var i = 0; i < 3; i++)
        {
            if (game.State.PendingCardChoice is { } choice)
            {
                Assert.Contains("ALLY", choice.LegalObjectIds);
                Assert.DoesNotContain("FAERIE", choice.LegalObjectIds); Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
                Assert.DoesNotContain("FOOTHOLD", choice.LegalObjectIds);
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
            }
            else if (game.State.PendingPayment is { } payment)
            {
                Assert.Equal(1, payment.PowerCost); Assert.Equal(0, payment.ManaCost);
                if (pay) await game.Act("P1", new RecycleRuneCommand("PURPLE"));
                await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
            }
            else break;
        }
    }

    [Fact]
    public async Task ActualArrivalOffersOnePurplePaymentBeforeMovingAnotherExhaustedFriendlyUnit()
    {
        var game = new Driver(Position()); await Move(game);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); await Confirm(game);
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(Faerie, skill.CardNo);
        Assert.Equal("BF", skill.UnitEvent!.Here!.BattlefieldObjectId); Assert.NotNull(skill.TriggerCost);
        Assert.NotNull(game.State.PriorityPlayerId); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task DecliningThePurpleCostLeavesTheChosenUnitAndRuneUntouched()
    {
        var game = new Driver(Position()); await Move(game); await Confirm(game, false);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Contains("PURPLE", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Fact]
    public async Task MovementBackToBaseDoesNotEarnTheBattlefieldArrivalAbility()
    {
        var game = new Driver(Position(true)); await Move(game, true);
        Assert.Contains("FAERIE", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Null(game.State.PendingPayment);
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsActualMovementStillEarnsTheFaeriesControllersOptionalSkill()
    {
        var state = RadGearTestPosition.Put(Position(), "THEIR-MOVE", "OGN·043/298", "P2", "HAND");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("THEIR-MOVE", "OGN·043/298", ["FAERIE"], Destination: "BATTLEFIELD:BF"));
        await game.Top(); await Confirm(game); Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task TheSameBattlefieldRemainsTheArrivalFieldAfterTheSourceActuallyReturnsToHand()
    {
        var game = new Driver(Position()); await Move(game); await Confirm(game);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["FAERIE"])); await game.Top();
        Assert.Contains("FAERIE", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("FAERIE", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }

    [Theory]
    [InlineData("destination")] [InlineData("erase")] [InlineData("duplicate")]
    public async Task RecoveryRejectsAChangedArrivalFieldOrAnErasedOrRepeatedAcceptedSkill(string branch)
    {
        var game = new Driver(Position()); await Move(game); await Confirm(game); var item = Assert.Single(game.State.StackItems);
        var changed = item with { UnitEvent = item.UnitEvent! with { Here = new("P1", "BATTLEFIELD", "OTHER") } };
        var forged = game.State with { StackItems = branch switch {
            "erase" => [], "duplicate" => [item, item], _ => [changed] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }
}
