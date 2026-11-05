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

    // Entry movement, optional decisions and target replacement are covered by
    // BlastConeMovementFaqTests. Keep this distinct source-recall/privacy boundary.
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

    private static MatchState HostAtBattlefield(MatchState state)
        => state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with {
                    Base = state.PlayerZones["P2"].Base.Where(id => id != "HOST").ToArray(),
                    Battlefields = [..state.PlayerZones["P2"].Battlefields, "HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["HOST"] = new("P2", "BATTLEFIELD", "F2") } };

    private static async Task Order(VenBasicFlowSpellTests.Driver game)
    {
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static async Task ConfirmExhaust(VenBasicFlowSpellTests.Driver game, bool accept)
    {
        await Order(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(new[] { "CONE" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["CONE"] : []));
    }

}
