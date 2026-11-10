using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SivirPaidPowerStaticTests
{
    private const string Sivir = "SFD·143/221";

    private static MatchState Position(string no = Sivir, string zone = "BASE")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", no, "P1", zone);
        state = RadGearTestPosition.Put(state, "CONVERT", "SFD·083/221", "P1", "BASE");
        return RadGearTestPosition.Put(state, "SECOND", "SFD·083/221", "P1", "BASE");
    }

    private static Task Convert(Driver game, int amount, string source = "CONVERT", string player = "P1")
        => game.Act(player, new ActivateAbilityCommand(source, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId,
            [], [P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + amount]));

    private static void Passive(MatchState state, bool active, int expected = 6)
    {
        Assert.Equal(4, state.CardObjects["HOST"].Power);
        Assert.Equal(active ? expected : 4, ArmamentMightTests.Might(state));
        Assert.Equal(active, state.CardObjects["HOST"].Tags.Contains(CardCombatKeywordNames.Roam));
        ArmamentMightTests.PublicRecovery(state);
    }

    // SFD143: generic/trait power and optional fees all count; CN445.1.b:
    // paying zero or merely obtaining a resource does not constitute a payment.
    [Theory]
    [InlineData(Sivir, false)] [InlineData(Sivir, true)]
    [InlineData("SFD·143a/221", false)] [InlineData("SFD·143a/221", true)]
    public async Task BothPrintingsIncludeTheirOwnActualHasteFeeAndExpireAtTheRealTurnSwitch(string no, bool haste)
    {
        var game = new Driver(Position(no, "HAND"));
        await game.Act("P1", new PlayCardCommand("HOST", no, [],
            OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : []));
        Passive(game.State, haste);
        Assert.Equal(!haste, game.State.CardObjects["HOST"].IsExhausted);
        var turn = game.State.TurnNumber;
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(turn + 1, game.State.TurnNumber);
        Assert.All(game.State.RunePools.Values, pool => Assert.Equal(0, pool.PaidPowerThisTurn));
        Passive(game.State, false);
        await game.Replay();
    }

    [Theory, InlineData(0), InlineData(1), InlineData(2), InlineData(3)]
    public async Task AVariableResourceConversionCountsItsActualDebitRatherThanItsZeroPrintedFee(int amount)
    {
        var game = new Driver(Position());
        var original = game.State.RunePools["P1"];
        Passive(game.State, false);
        await Convert(game, amount);
        Assert.Equal(amount, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(original.Mana + amount, game.State.RunePools["P1"].Mana);
        Assert.Equal(original.TotalPower - amount, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(amount > 0 ? 1 : 0, game.Events().Count(row => row.Event.Kind == CoreRuleEngine.PowerSpentEvent));
        Passive(game.State, amount >= 2);
        await game.Replay();
    }

    [Fact]
    public async Task SeparateRealPaymentsAccumulateWhilePromptsAndRejectedActionsCannotSpend()
    {
        var game = new Driver(Position());
        await Convert(game, 1);
        Passive(game.State, false);
        var before = MatchStateHasher.Hash(game.State);
        for (var i = 0; i < 3; i++) _ = game.PromptFor("P1");
        Assert.Equal(before, MatchStateHasher.Hash(game.State));
        await RadGearTestPosition.Reject(game.State, "P1", new ActivateAbilityCommand("SECOND",
            P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, [],
            [P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "999"]));
        Assert.Equal(1, game.State.RunePools["P1"].PaidPowerThisTurn);
        await Convert(game, 1, "SECOND");
        Assert.Equal(2, game.State.RunePools["P1"].PaidPowerThisTurn);
        Passive(game.State, true);
        Assert.Equal(new[] { 1, 2 }, game.Events().Where(row => row.Event.Kind == CoreRuleEngine.PowerSpentEvent)
            .Select(row => (int)row.Event.Payload["paidPowerTotal"]!));
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ActualPaymentsOnEitherPlayersTurnFollowTheCurrentControllerRatherThanTheOwner(bool opponentTurn)
    {
        var initial = Position();
        initial = initial with {
            TurnPlayerId = opponentTurn ? "P2" : "P1",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await Convert(game, 2);
        Assert.Equal(2, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(0, game.State.RunePools["P2"].PaidPowerThisTurn);
        Passive(game.State, true);
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsPaymentDoesNotEnableTheOwnersOrControllersOtherUnits()
    {
        var initial = RadGearTestPosition.Put(Position(), "OPPONENT", "SFD·083/221", "P2", "BASE")
            with { TurnPlayerId = "P2" };
        var game = new Driver(initial);
        await Convert(game, 2, "OPPONENT", "P2");
        Assert.Equal(2, game.State.RunePools["P2"].PaidPowerThisTurn);
        Assert.Equal(0, game.State.RunePools["P1"].PaidPowerThisTurn);
        Passive(game.State, false);
        await game.Replay();
    }

    [Fact]
    public async Task MeetingTheThresholdEnablesARealMoveBetweenBattlefieldsWithoutChangingIncarnation()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "HOST", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await RadGearTestPosition.Reject(game.State, "P1",
            new MoveUnitCommand("HOST", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        await Convert(game, 2);
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new MoveUnitCommand("HOST", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
        Passive(game.State, true);
        await game.Replay();
    }

    [Fact]
    public async Task ARealPaymentEnablesBothThePrintedTextAndAnActiveEquipmentCopy()
    {
        var initial = ArmamentMightTests.Position("SFD·059/221", Sivir, 4, true);
        initial = RadGearTestPosition.Put(initial, "CONVERT", "SFD·083/221", "P1", "BASE");
        var game = new Driver(initial);
        Passive(game.State, false);
        await Convert(game, 2);
        Passive(game.State, true, 8);
        Assert.Single(game.State.CardObjects["HOST"].RuleDerivedTags!, tag => tag == CardCombatKeywordNames.Roam);
        await game.Replay();
    }

    [Fact]
    public async Task ARealPaymentCapturesTheFourToSixMightyCrossingWithItsPublicDebitInputs()
    {
        var initial = RadGearTestPosition.Put(Position(), "FIORA", "SFD·180/221", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await Convert(game, 2);
        var context = Assert.Single(game.State.StackItems, item => item.MightySkill is not null).MightySkill!;
        Assert.Equal("HOST", context.Unit.ObjectId);
        Assert.Equal(4, context.BeforeMight); Assert.Equal(6, context.AfterMight);
        Assert.Equal(0, context.Before.PaidPowerThisTurn["P1"]);
        Assert.Equal(2, context.After.PaidPowerThisTurn["P1"]);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Passive(game.State, true);
        await game.Replay();
    }
}
