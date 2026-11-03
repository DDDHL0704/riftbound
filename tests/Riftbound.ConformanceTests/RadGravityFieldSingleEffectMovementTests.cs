using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Gravity Field is staged only as a public fee source. Its printing remains
// outside the runtime catalog until the remaining movement and Last Breath work.
public sealed class RadGravityFieldSingleEffectMovementTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public async Task SequentialSwapInstructionsPayIndependentlyAndEitherCanBeDeclined(
        bool zed, bool first, bool second)
    {
        var initial = SwapPosition(zed);
        initial = Tax(Tax(initial, "TAX1", "P2", "OTHER"), "TAX2", "P2", "F");
        var game = new Driver(initial);
        await game.Act("P1", Swap(zed)); await game.Top();
        Fee(game, "P1");
        Assert.Equal("F", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        await Decide(game, first ? "PAY" : "DECLINE");
        Fee(game, "P1");
        await Decide(game, second ? "PAY" : "DECLINE");
        await FinishAttachment(game);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(first ? "OTHER" : "F", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(second ? "F" : "OTHER", game.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal(second ? "F" : "OTHER", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        var paid = (first ? 1 : 0) + (second ? 1 : 0);
        Assert.Equal(10 - (zed ? 1 : 0) - paid, game.State.RunePools["P1"].Mana);
        Assert.Equal(2 - paid, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(paid, game.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "IMPOSED_MOVEMENT_COST")));
        Assert.Equal(paid, game.Events().Count(e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal("TARGET", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task RuneResourcesPersistWhenAzirPaysOrDeclinesAndTheSecondInstructionStillRuns(bool pay)
    {
        var initial = Tax(SwapPosition(false), "TAX", "P2", "OTHER");
        initial = WithPool(initial, "P1", new(0, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }));
        initial = RadGearTestPosition.Put(initial, "RUNE", "OGN·007/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", Swap(false)); await game.Top();
        Fee(game, "P1");
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "PAY");
        await Decide(game, "TAP_RUNE:RUNE");
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        await Decide(game, "RECYCLE_RUNE:RUNE");
        Assert.Equal(1, game.State.RunePools["P1"].TotalPower);
        await Decide(game, pay ? "PAY" : "DECLINE");
        await FinishAttachment(game);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal("RUNE_DECK", game.State.ObjectLocations["RUNE"].Zone);
        Assert.Equal(pay ? "OTHER" : "F", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", game.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Single(game.Events(), e => e.Event.Kind == "RUNE_TAPPED");
        Assert.Single(game.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task LanternsOpponentMakesTheMovementPaymentAndKeepsTheSourceControllersResources(bool pay)
    {
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT", "RAD·113/167", "P1", "BATTLEFIELD", "BF");
        initial = WithPool(Tax(initial, "TAX", "P1", "BF"), "P2", new(1, 1));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", P4ActivatedAbilityCatalog.LanternDragonAbilityId, ["P2"]));
        await game.Top();
        Assert.Equal("MOVEMENT_SELECTION", game.State.PendingRuleChoice!.Request.Kind);
        await Decide(game, "MOVE:ENEMY");
        Fee(game, "P2");
        await RadGearTestPosition.Reject(game.State, "P1",
            new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["PAY"]));
        await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P2"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P2"].TotalPower);
        Assert.Equal(30, game.State.RunePools["P1"].Mana);
        Assert.Equal(30, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(true, false)] [InlineData(false, false)]
    [InlineData(true, true)] [InlineData(false, true)]
    public async Task MadulisSharedMovementRetainsPaymentOrAcquiredResources(bool pay, bool acquire)
    {
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT",
            P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo, "P1", "BASE");
        initial = Tax(initial, "TAX", "P2", "OTHER");
        initial = WithPool(initial, "P1", new(acquire ? 0 : 1, acquire ? 0 : 1,
            new Dictionary<string, int> { [RuneTrait.Purple] = 1 }));
        if (acquire) initial = RadGearTestPosition.Put(initial, "RUNE", "OGN·007/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", P4ActivatedAbilityCatalog.GatekeeperMaduliMoveAbilityId, ["OTHER"]));
        await game.Top();
        Fee(game, "P1");
        if (acquire)
        {
            await Decide(game, "TAP_RUNE:RUNE");
            await Decide(game, "RECYCLE_RUNE:RUNE");
        }
        await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        if (pay)
        {
            var move = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD").Event;
            Assert.Equal("GATEKEEPER_MADULI_PURPLE_MOVE", move.Payload["movementPermission"]);
            Assert.Equal("OTHER", move.Payload["battlefieldObjectId"]);
        }
        else Assert.Contains(game.Events(), e => e.Event.Kind == "ABILITY_NO_EFFECT"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "MOVEMENT_COST_DECLINED"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AUnitAlreadyAtItsDestinationDoesNotPayForANonexistentMove(bool zed)
    {
        var initial = SwapPosition(zed);
        initial = RadUnitEffectTestFixture.Place(initial, "TARGET", "P1", "BATTLEFIELD", "F");
        initial = RadUnitEffectTestFixture.Place(initial, "GEAR", "P1", "BATTLEFIELD", "F");
        var game = new Driver(Tax(initial, "TAX", "P2", "F"));
        await game.Act("P1", Swap(zed)); await game.Top();
        await FinishAttachment(game);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "IMPOSED_MOVEMENT_COST"));
        Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    private static MatchState SwapPosition(bool zed)
    {
        var state = AzirMovementAuditTests.AzirPosition(false, zed ? "VEN·112" : "SFD·050/221");
        if (zed)
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T05", out var factory));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "VEN·112", "P1"),
                ["TARGET"] = factory.CreateObject("TARGET", "P1", "P1", isExhausted: false) } };
        }
        return WithPool(state, "P1", new(10, 2, new Dictionary<string, int> { [zed ? RuneTrait.Purple : RuneTrait.Green] = 1 }));
    }

    private static ActivateAbilityCommand Swap(bool zed) => new("HOST",
        zed ? "NAMED-UNIT-SWAP:0" : P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
        zed ? ["TARGET"] : ["TARGET", "GEAR"]);

    private static async Task FinishAttachment(Driver game)
    {
        if (game.State.PendingCardChoice is { ChoiceWindow: "AZIR_ARMAMENT_ATTACHMENT" } choice)
            await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
    }

    private static MatchState WithPool(MatchState state, string player, RunePool pool)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { [player] = pool } };

    private static MatchState Tax(MatchState state, string id, string player, string field)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: "RAD·062/167", ownerId: player, controllerId: player,
                tags: [CardObjectTags.EquipmentCard, "部署"]) } };
        return RadUnitEffectTestFixture.Place(state, id, player, "BATTLEFIELD", field);
    }

    private static void Fee(Driver game, string player)
    {
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", request.Kind);
        Assert.Equal(player, request.PlayerId);
        Assert.Equal(1, request.ManaCost); Assert.Equal(1, request.PowerCost);
    }

    private static Task Decide(Driver game, string option)
    {
        var request = game.State.PendingRuleChoice!.Request;
        return game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
    }
}
