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
        Assert.Equal(amount > 0 ? 1 : 0, PowerSpentTurnHistoryTests.Receipts(game).Count());
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
        Assert.Equal(new[] { 1, 2 }, PowerSpentTurnHistoryTests.Receipts(game)
            .Select(row => (int)PowerSpentTurnHistoryTests.Debit(row)["paidPowerTotal"]!));
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ActualPaymentsOnEitherPlayersTurnFollowTheCurrentControllerRatherThanTheOwner(bool opponentTurn)
    {
        var initial = Position();
        initial = initial with {
            TurnPlayerId = opponentTurn ? "P2" : "P1",
            ActivePlayerId = opponentTurn ? "P2" : "P1",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2" } } };
        if (opponentTurn)
            initial = RadGearTestPosition.Put(initial, "RESPONSE", "OGN·104/298", "P2", "HAND");
        var game = new Driver(initial);
        if (opponentTurn) {
            await game.Act("P2", new PlayCardCommand("RESPONSE", "OGN·104/298", ["ENEMY"]));
            await game.Act("P2", new PassPriorityCommand());
            Assert.Equal("P1", game.State.PriorityPlayerId);
        }
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
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
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

    [Fact]
    public async Task ObtainingUnspentPowerCannotEnableTheCondition()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "GAIN", "SFD·117/221", "P1", "BASE"));
        await game.Act("P1", new ActivateAbilityCommand("GAIN", P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
            [], [P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix + "2"]));
        Assert.Equal(32, game.State.RunePools["P1"].Power);
        Assert.Equal(0, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Empty(PowerSpentTurnHistoryTests.Receipts(game));
        Passive(game.State, false);
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnAcceptedConversionWhileAnInstructionIsPausedCountsOnceAndSurvivesResumeOrSurrender(bool surrender)
    {
        var initial = RadGearTestPosition.Put(Position(), "SPELL", "OGN·268/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·268/298", ["BF"]));
        await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        var selected = $"RESOURCE:CONVERT:{P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId}:"
            + P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "2";
        Assert.Contains(request.Options, option => option.Id == selected);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));
        Assert.Equal(2, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.True(game.State.CardObjects["CONVERT"].IsExhausted);
        Passive(game.State, true);
        Assert.Equal("RESOURCE_SKILL_RESOLVED", Assert.Single(PowerSpentTurnHistoryTests.Receipts(game)).Kind);
        if (surrender) await game.Act("P2", new SurrenderCommand());
        else {
            request = game.State.PendingRuleChoice!.Request;
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:1"]));
        }
        Assert.Equal(surrender ? 2 : 3, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(surrender ? 1 : 2, PowerSpentTurnHistoryTests.Receipts(game).Count());
        Assert.Equal(1, game.Events().Count(row => row.Event.Kind == "RESOURCE_SKILL_RESOLVED"));
        await game.Replay();
    }

    [Fact]
    public async Task ARealControlChangeUsesTheNewControllersPaymentHistory()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "HOST", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2" } } };
        initial = RadGearTestPosition.Put(initial, "CONTROL", "OGN·203/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("CONTROL", "OGN·203/298", ["HOST"]));
        Assert.DoesNotContain(CardCombatKeywordNames.Roam, game.State.CardObjects["HOST"].Tags);
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["HOST"].ControllerId);
        Assert.Equal("P2", game.State.CardObjects["HOST"].OwnerId);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Passive(game.State, true);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheUnitRemovesItsPassiveWithoutErasingThePlayersPayments()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await Convert(game, 2);
        Passive(game.State, true);
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
        Assert.True(game.State.RunePools["P1"].PaidPowerThisTurn >= 2);
        Assert.DoesNotContain(CardCombatKeywordNames.Roam, game.State.CardObjects["HOST"].Tags);
        await game.Act("P1", new PlayCardCommand("HOST", Sivir, []));
        Passive(game.State, true);
        await game.Replay();
    }

    [Fact]
    public async Task ACommittedResourceChildCapturesItsMightyCrossingOnceBeforeTheOuterInstructionResumes()
    {
        var initial = RadGearTestPosition.Put(Position(), "FIORA", "SFD·180/221", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "SPELL", "OGN·268/298", "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·268/298", ["BF"]));
        await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        var selected = $"RESOURCE:CONVERT:{P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId}:"
            + P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "2";
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [selected]));
        var queued = Assert.Single(game.State.TriggerQueue, item => item.MightySkill is not null);
        Assert.Equal(game.State.Tick, queued.EnqueuedAtTick);
        var captured = queued.MightySkill!;
        Assert.Equal(4, captured.BeforeMight); Assert.Equal(6, captured.AfterMight);
        Assert.Equal(2, captured.After.PaidPowerThisTurn["P1"]);
        request = game.State.PendingRuleChoice!.Request;
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:0"]));
        Assert.Single(game.State.StackItems, item => item.MightySkill is not null);
        Assert.Single(game.Events(), row => row.Event.Kind == "TRIGGER_QUEUED"
            && Equals(row.Event.Payload.GetValueOrDefault("effectKind"), CoreRuleEngine.MightyReadyEffect));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        await game.Replay();
    }

    [Theory, InlineData("payer"), InlineData("other-player"), InlineData("missing")]
    public async Task RebuildingViewsCannotAuthenticateAlteredMightyPaymentInputsAgainstOriginalRecords(string mutation)
    {
        var initial = RadGearTestPosition.Put(Position(), "FIORA", "SFD·180/221", "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        var game = new Driver(initial);
        await Convert(game, 2);
        var item = Assert.Single(game.State.StackItems, item => item.MightySkill is not null);
        var context = item.MightySkill!;
        var counts = new Dictionary<string, int>(context.After.PaidPowerThisTurn);
        if (mutation == "missing") counts.Clear();
        else counts[mutation == "payer" ? "P1" : "P2"]++;
        var altered = game.State with { StackItems = game.State.StackItems.Select(row => row == item
            ? row with { MightySkill = context with { After = context.After with { PaidPowerThisTurn = counts } } }
            : row).ToArray() };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, altered));
        await game.Replay();
    }
}
