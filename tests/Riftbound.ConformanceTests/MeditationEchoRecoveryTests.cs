using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MeditationEchoRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoRealGrantsRepeatBaseEffectTwiceButThePaidDrawOnlyOnce(bool reverse)
    {
        var initial = Position();
        var game = new StandbyDefendObserverTests.Driver(initial);
        await Grant(game, "GATE-1");
        await Grant(game, "GATE-2");
        var costs = new[] { "ECHO", "ECHO:GRANTED:2", "EXHAUST_FRIENDLY_UNIT:ALLY" };
        if (reverse) Array.Reverse(costs);
        var command = new PlayCardCommand("UNIT", "OGN·048/298", [], OptionalCosts: costs);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(6, quote.Cost!.Mana);
        var paid = await game.Act("P1", command);
        Assert.Equal(14, game.State.RunePools["P1"].Mana);
        Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Single(paid.Events, e => e.Kind == "UNIT_EXHAUSTED");
        Assert.Equal(3, Assert.Single(game.State.StackItems).EffectRepeatCount);
        var completed = await game.Top();
        Assert.Equal(3, completed.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2", "P1-DRAW-3", "P1-DRAW-4" }, game.State.PlayerZones["P1"].Hand);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Theory]
    [InlineData("remove-bonus")]
    [InlineData("add-bonus")]
    public async Task AlteredPendingCostCannotRewriteTheAcceptedPaymentHistory(string mutation)
    {
        var initial = Position();
        var game = new StandbyDefendObserverTests.Driver(initial);
        await Grant(game, "GATE-1");
        var bonus = mutation == "remove-bonus";
        var command = new PlayCardCommand("UNIT", "OGN·048/298", [], OptionalCosts:
            bonus ? ["ECHO", "EXHAUST_FRIENDLY_UNIT:ALLY"] : ["ECHO"]);
        await game.Act("P1", command);
        var real = game.State;
        var item = Assert.Single(real.StackItems);
        var changed = item with { OptionalCosts = bonus ? ["ECHO"] : ["ECHO", "EXHAUST_FRIENDLY_UNIT:ALLY"] };
        var forged = real with { StackItems = [changed] };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(forged));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), forged,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
        Assert.Contains(replay.Errors, error => error.Contains("replayed final state hash", StringComparison.Ordinal));
        Assert.Equal(bonus, game.State.CardObjects["ALLY"].IsExhausted);
        await game.Top();
        Assert.Equal(bonus ? 3 : 2, game.State.PlayerZones["P1"].Hand.Count);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public async Task CounteringTheSpellKeepsThePaidCostsWithoutDrawing()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·064/298", "P2");
        var game = new StandbyDefendObserverTests.Driver(initial);
        await Grant(game, "GATE-1");
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·048/298", [], OptionalCosts:
            ["ECHO", "EXHAUST_FRIENDLY_UNIT:ALLY"]));
        var spell = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell]));
        await game.Top();
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(16, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal(4, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Empty(game.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public async Task TwoDifferentUnitsCannotPayTheSingleOptionalCostTwice()
    {
        var state = Position();
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY-2"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY-2", "OGN·219/298", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["ALLY-2"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "ALLY-2"] } } };
        var command = new PlayCardCommand("UNIT", "OGN·048/298", [], OptionalCosts:
            ["EXHAUST_FRIENDLY_UNIT:ALLY", "EXHAUST_FRIENDLY_UNIT:ALLY-2"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        var result = await engine.ResolveAsync(state, new("two-costs", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(quote.CanPay);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static async Task Grant(StandbyDefendObserverTests.Driver game, string id)
    {
        await game.Act("P1", new ActivateAbilityCommand(id, P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []));
        await game.Top();
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position("OGN·048/298");
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GATE-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("GATE-1", "SFD·078/221", "P1"),
            ["GATE-2"] = OfficialGarenConquestBattlefieldCountTests.Printed("GATE-2", "SFD·078/221", "P1"),
            ["P1-DRAW-4"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-DRAW-4", "OGN·219/298", "P1") };
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            ["GATE-1"] = new("P1", "BASE"), ["GATE-2"] = new("P1", "BASE"), ["P1-DRAW-4"] = new("P1", "MAIN_DECK") };
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with {
                Base = ["ALLY", "GATE-1", "GATE-2"], MainDeck = [..state.PlayerZones["P1"].MainDeck, "P1-DRAW-4"] } };
        // Remove the unused base equipment so every object has one valid zone.
        cards.Remove("GEAR"); locations.Remove("GEAR");
        return state with { RoomId = "MEDITATION-ECHO-RECOVERY", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
