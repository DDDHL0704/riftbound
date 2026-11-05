using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VenBlastConeFaqTests
{
    private static MatchState Position(string printing, bool empowered)
    {
        var state = CopiedMovementObserverTests.Position(printing, 0);
        state = HandDiscardObserverTests.Put(state, "CONE", "UNL-133/219", "P1", "HAND");
        state = HandDiscardObserverTests.Put(state, "STUN", "UNL-042/219", "P1", "HAND");
        return state with { ActivePlayerId = "P1", TurnPlayerId = "P1",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { IsEmpowered = empowered } } };
    }

    // CN VEN FAQ p. 13: the entry move precedes the optional, non-targeted stun.
    // Keep the zero-target play command: the unit is chosen by the entry skill.
    [Theory]
    [InlineData("VEN·086", false)]
    [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)]
    [InlineData("VEN·181", true)]
    public async Task ActualBlastConePlayMovesAndStunsWithoutTargetingThenAnAlreadyStunnedGangplankDoesNotReplace(string printing, bool empowered)
    {
        var game = await Enter(printing, empowered);
        await ChooseDestination(game, "BATTLEFIELD:F2");
        var target = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", target.ChoiceWindow);
        Assert.Contains("HOST", target.LegalObjectIds);
        Assert.DoesNotContain("MOVER0", target.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["HOST"]));
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        await game.Top();
        Assert.Equal("F2", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        await ConfirmExhaust(game, true);
        Assert.True(game.State.CardObjects["CONE"].IsExhausted);
        var stun = Assert.Single(game.State.StackItems);
        Assert.Empty(stun.TargetObjectIds);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        var stuns = game.Events().Count(e => e.Event.Kind == "STATUS_EFFECT_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("statusEffectId"), "STUNNED"));
        await game.Act("P1", new PlayCardCommand("STUN", "UNL-042/219", ["HOST"]));
        await game.Top();
        Assert.Equal(stuns, game.Events().Count(e => e.Event.Kind == "STATUS_EFFECT_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("statusEffectId"), "STUNNED")));
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(empowered, game.State.CardObjects["HOST"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)]
    public async Task DecliningEntryMovementLeavesTheEquipmentReadyAndDoesNotStun(string printing, bool empowered)
    {
        var game = await Enter(printing, empowered);
        await ChooseDestination(game, "DECLINE");
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task DecliningExhaustKeepsTheRealMoveButDoesNotStun(string printing)
    {
        var game = await Enter(printing, true);
        await ChooseDestination(game, "BATTLEFIELD:F2");
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await game.Top();
        await ConfirmExhaust(game, false);
        Assert.Equal("F2", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task OtherSpellsEarnTheSameTriggerButAnExhaustedSourceCannotPayAgain(string printing)
    {
        var initial = HandDiscardObserverTests.Put(HostAtBattlefield(Position(printing, true)), "MOVE1", "OGN·168/298", "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "MOVE2", "OGN·168/298", "P1", "HAND");
        var game = await Enter(printing, true, initial);
        await ChooseDestination(game, "DECLINE");
        await game.Act("P1", new PlayCardCommand("MOVE1", "OGN·168/298", ["HOST"]));
        await game.Top();
        await ConfirmExhaust(game, true);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Act("P1", new PlayCardCommand("MOVE2", "OGN·168/298", ["P2-KEEPER"]));
        await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["P2-KEEPER"].Zone);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["P2-KEEPER"].UntilEndOfTurnEffects);
        Assert.True(game.State.CardObjects["CONE"].IsExhausted);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningAnOriannaConvertedSourceAfterPayingDoesNotEraseTheIndependentStun()
    {
        // Orianna makes the equipment a legal target for the reaction recall.
        // A Swift equipment-destruction spell cannot respond to a closed chain.
        var initial = HostAtBattlefield(Position("VEN·086", true));
        var own = initial.PlayerZones["P1"];
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = own with { Hand = own.Hand.Where(id => id != "CONE").ToArray(), Base = [..own.Base, "CONE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["CONE"] = new("P1", "BASE") } };
        initial = HandDiscardObserverTests.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "MOVE", "OGN·168/298", "P1", "HAND");
        for (var i = 0; i < 6; i++) initial = HandDiscardObserverTests.Put(initial, "RUNE" + i, "UNL-R03", "P1", "BASE");
        var legend = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["LEGEND"] = legend },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P2", new EndTurnCommand());
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["CONE"]));
        await game.TapRunes("P1", Enumerable.Range(0, 6).Select(i => "RUNE" + i).ToArray());
        var payment = game.State.PendingPayment!;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Top();
        Assert.NotNull(game.State.CardObjects["CONE"].LegendEquipmentUnit);
        await game.Act("P1", new PlayCardCommand("MOVE", "OGN·168/298", ["HOST"]));
        await game.Top();
        await ConfirmExhaust(game, true);
        var captured = Assert.Single(game.State.StackItems).MovementObserver!;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["CONE"]));
        await game.Top();
        Assert.Contains("CONE", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["CONE"].ObjectGeneration > captured.SourceGeneration);
        Assert.Null(game.State.CardObjects["CONE"].LegendEquipmentUnit);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        var stun = game.Events().Last(e => e.Event.Kind == "STATUS_EFFECT_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectId"), "STUNNED"));
        Assert.Equal("HIDDEN", stun.Event.Payload["sourceObjectId"]);
        var resolved = game.Events().Last(e => e.Event.Kind == "TRIGGER_RESOLVED");
        Assert.Equal("HIDDEN", resolved.Event.Payload["sourceObjectId"]);
        await game.Replay();
    }

    [Fact]
    public async Task MovingYourOwnUnitOrAnOpponentMovingTheirOwnUnitDoesNotTriggerTheCone()
    {
        var game = await Enter("VEN·086", true);
        await ChooseDestination(game, "DECLINE");
        await game.Act("P1", new MoveUnitCommand("MOVER0", Origin: "BASE", Destination: "BATTLEFIELD:F1"));
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new MoveUnitCommand("HOST", Origin: "BASE", Destination: "BATTLEFIELD:F2"));
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task InvalidMovementChoicesDoNotChangeStateAndDecliningTheUnitStillKeepsTheEquipment()
    {
        var game = await Enter("VEN·086", true);
        var rule = game.State.PendingRuleChoice!;
        await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", ["BASE:P1"]));
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:F2"]));
        await ChooseDestination(game, "BATTLEFIELD:F2");
        var choice = game.State.PendingCardChoice!;
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["MOVER0"]));
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Contains("CONE", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    private static MatchState HostAtBattlefield(MatchState state)
        => state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with {
                    Base = state.PlayerZones["P2"].Base.Where(id => id != "HOST").ToArray(),
                    Battlefields = [..state.PlayerZones["P2"].Battlefields, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["HOST"] = new("P2", "BATTLEFIELD", "F2") } };

    private static async Task<VenBasicFlowSpellTests.Driver> Enter(string printing, bool empowered, MatchState? initial = null)
    {
        var game = new VenBasicFlowSpellTests.Driver(initial ?? Position(printing, empowered));
        await game.Act("P1", new PlayCardCommand("CONE", "UNL-133/219", [], Destination: "BASE"));
        if (game.State.PendingRuleChoice is null && game.State.StackItems.Count > 0) await game.Top();
        await Order(game);
        Assert.Contains("CONE", (game.State.PendingRuleChoice?.Request.Preview ?? game.State).PlayerZones["P1"].Base);
        return game;
    }

    private static async Task Order(VenBasicFlowSpellTests.Driver game)
    {
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static async Task ChooseDestination(VenBasicFlowSpellTests.Driver game, string destination)
    {
        var rule = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("UNIT_EVENT_MOVEMENT_DESTINATION", rule.Request.Kind);
        Assert.DoesNotContain(rule.Request.Options, o => o.Id == "BASE:P1");
        Assert.Contains(rule.Request.Options, o => o.Id == destination);
        await game.Act("P1", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [destination]));
    }

    private static async Task ConfirmExhaust(VenBasicFlowSpellTests.Driver game, bool accept)
    {
        await Order(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(new[] { "CONE" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["CONE"] : []));
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task BlastConeStillRejectsAUnitTargetOnTheEquipmentPlayCommand(string printing)
    {
        var initial = Position(printing, true);
        var command = new PlayCardCommand("CONE", "UNL-133/219", ["HOST"], Destination: "BASE");
        await RadGearTestPosition.Reject(initial, "P1", command);
        Assert.Contains("CONE", initial.PlayerZones["P1"].Hand);
        Assert.True(initial.CardObjects["HOST"].IsEmpowered);
        Assert.Empty(initial.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
    }
}
