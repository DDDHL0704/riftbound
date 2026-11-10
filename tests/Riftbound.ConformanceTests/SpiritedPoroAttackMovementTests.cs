using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SpiritedPoroAttackMovementTests
{
    private const string Poro = "UNL-137/219";
    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(AttackDamageDistributionTests.Position(Poro), "HOST", Poro, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "OUTSIDE", "OGN·219/298", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "OUR-RETURN", "OGN·104/298", "P1", "HAND");
        return RadGearTestPosition.Put(state, "THEIR-RETURN", "OGN·104/298", "P2", "HAND");
    }

    private static Task Attack(Driver game)
        => game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));

    private static async Task Confirm(Driver game, bool pay = true)
    {
        for (var i = 0; i < 4; i++)
        {
            if (game.State.PendingCardChoice is { } choice)
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
            else if (game.State.PendingPayment is { } payment) {
                Assert.Equal(1, payment.ManaCost); Assert.Equal(0, payment.PowerCost);
                await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
            }
            else break;
        }
    }

    [Fact]
    public async Task AnActualAttackPaysOneManaBeforeResponsesAndMovesOnlyTheConfirmedEnemyHereToItsBase()
    {
        var game = new Driver(Position()); var mana = game.State.RunePools["P1"].Mana;
        await Attack(game); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, choice.LegalObjectIds);
        await Confirm(game); var skill = Assert.Single(game.State.StackItems);
        Assert.NotNull(skill.TriggerCost); Assert.Equal("BASE:P2", skill.Destination);
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal("F", game.State.ObjectLocations["ENEMY1"].BattlefieldObjectId);
        var prefix = game.Events().Length; await game.Top();
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Base);
        Assert.Equal("F", game.State.ObjectLocations["ENEMY2"].BattlefieldObjectId);
        Assert.Contains(game.Events().Skip(prefix), entry => entry.Event.Kind == "UNIT_MOVED_TO_BASE"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY1"));
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Fact]
    public async Task DecliningTheLeadingPaymentLeavesTheEnemyAndManaUntouched()
    {
        var game = new Driver(Position()); var mana = game.State.RunePools["P1"].Mana;
        await Attack(game); await Confirm(game, false);
        Assert.Empty(game.State.StackItems); Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Equal("F", game.State.ObjectLocations["ENEMY1"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task ARealReturnOfThePoroRemovesItsCurrentHereWithoutRefundingThePaidMana()
    {
        var game = new Driver(Position()); await Attack(game); await Confirm(game);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("OUR-RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand); await game.Top();
        Assert.Equal("F", game.State.ObjectLocations["ENEMY1"].BattlefieldObjectId);
        Assert.True(game.State.RunePools["P1"].Mana < mana); await game.Replay();
    }

    [Fact]
    public async Task ARealEnemyReturnCannotRedirectThePaidSkillToAnotherLegalUnitOrItsNewGeneration()
    {
        var game = new Driver(Position()); await Attack(game); await Confirm(game);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("THEIR-RETURN", "OGN·104/298", ["ENEMY1"])); await game.Top();
        await game.Top(); Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Hand);
        Assert.Equal("F", game.State.ObjectLocations["ENEMY2"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task TargetProtectionAndTheCurrentHereRestrictionRemainPartOfConfirmation()
    {
        var state = RadGearTestPosition.Put(Position(), "ENEMY1", "UNL-147/219", "P2", "BATTLEFIELD", "F");
        var game = new Driver(state); await Attack(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        foreach (var id in new[] { "ENEMY1", "OUTSIDE", "HOST" }) {
            Assert.DoesNotContain(id, choice.LegalObjectIds);
            await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
        }
    }

    [Fact]
    public async Task AcquiringTheDefendingRoleDoesNotEarnTheAttackSkill()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "ALLY", Poro, "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["ALLY"].IsDefending);
        Assert.DoesNotContain(game.State.StackItems, item => item.CardNo == Poro); await game.Replay();
    }

    [Theory]
    [InlineData("target")] [InlineData("destination")] [InlineData("erase")]
    public async Task RecoveryBindsThePaidSkillToItsOriginalTargetAndFixedBaseDestination(string change)
    {
        var game = new Driver(Position()); await Attack(game); await Confirm(game); var item = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = change == "erase" ? [] : [change == "destination"
            ? item with { Destination = "BASE:P1" }
            : item with { TargetObjectIds = ["ENEMY2"],
                TargetGenerations = new Dictionary<string, long> { ["ENEMY2"] = game.State.CardObjects["ENEMY2"].ObjectGeneration } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }

    [Fact]
    public async Task ThePrintedManaCanBePaidForAnImmovableEnemyWithoutExecutingTheMove()
    {
        var state = Position(); state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY1"] = state.CardObjects["ENEMY1"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.AllPlayers] } } };
        var game = new Driver(state); var mana = game.State.RunePools["P1"].Mana;
        await Attack(game); Assert.Contains("ENEMY1", game.State.PendingCardChoice!.LegalObjectIds);
        await Confirm(game); Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal("BASE:P2", Assert.Single(game.State.StackItems).Destination);
        var prefix = game.Events().Length; await game.Top();
        Assert.Equal("F", game.State.ObjectLocations["ENEMY1"].BattlefieldObjectId);
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(game.Events().Skip(prefix), entry =>
            entry.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD"); await game.Replay();
    }

}
