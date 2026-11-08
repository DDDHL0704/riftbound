using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class NamiAdditionalPlayCostTests
{
    private const string Nami = "UNL-052/219", Fee = "SPEND_POWER:1:GREEN";

    private static MatchState Position(string no = Nami, bool enemyOnField = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "UNIT", no, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "PAYMENT-RUNE", "UNL-R03", "P1", "BASE");
        if (enemyOnField)
            state = RadGearTestPosition.Put(state, "ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        return state;
    }

    private static async Task ConfirmEnemy(Driver game)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal("UNIT", choice.SourceObjectId);
        Assert.Equal(1, choice.RequiredCount);
        Assert.Contains("ENEMY", choice.LegalObjectIds);
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
    }

    [Theory]
    [InlineData(Nami, "BASE", false)] [InlineData(Nami, "BASE", true)]
    [InlineData(Nami, "BATTLEFIELD:BF", false)] [InlineData(Nami, "BATTLEFIELD:BF", true)]
    public async Task TheExtraGreenIsPaidBeforeEntryAndTheIndependentStunChoosesItsTargetAfterEntry(
        string no, string destination, bool enemyOnField)
    {
        var game = new Driver(Position(no, enemyOnField));
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: [Fee], Destination: destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(before.Mana - 3, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - PrintedPowerCostRules.ForCard(no).Amount - 1, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        var paid = MatchStateHasher.HashValue(game.State.RunePools);
        await ConfirmEnemy(game);
        Assert.Equal(paid, MatchStateHasher.HashValue(game.State.RunePools));
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory, InlineData("BASE"), InlineData("BATTLEFIELD:BF")]
    public async Task DecliningTheAdditionalCostPlaysTheUnitWithoutCreatingAStunSkill(string destination)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], Destination: destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(30 - PrintedPowerCostRules.ForCard(Nami).Amount, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task TheCostDoesNotRequireAnEnemyOrATargetIdBeforeTheUnitEnters()
    {
        var initial = RadGearTestPosition.Put(Position(), "ENEMY", "OGN·219/298", "P2", "HAND");
        var game = new Driver(initial);
        Assert.Contains($"SPEND_POWER:{RuneTrait.Green}:1", JsonSerializer.Serialize(game.PromptFor("P1")));
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(30 - PrintedPowerCostRules.ForCard(Nami).Amount - 1, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task WardPaymentIsSeparateFromTheAlreadyPaidExtraGreenAndKeepsItsOriginalReceipt(bool pay)
    {
        var initial = RadGearTestPosition.Put(Position(), "ENEMY", "UNL-171/219", "P2", "BASE");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(30, PrintedPowerCostRules.ForCard(Nami).Amount + 1) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await ConfirmEnemy(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(CoreRuleEngine.TriggerTargetCostWindow, payment.PaymentWindow);
        Assert.Equal(1, payment.PowerCost);
        if (pay) await game.Act("P1", new RecycleRuneCommand("PAYMENT-RUNE"));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(pay, game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task AnActualLinkedEquipmentPlayWaivesBaseCostsButStillPaysTheAdditionalGreen()
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR", "SFD·090/221", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "KILL", "OGN·229/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, []));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "UNIT", ["ASSEMBLE_BLUE"]));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"]));
        await game.Top(); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Banished);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", "BANISH_SELF_PLAY_ALL_LINKED_UNITS", []));
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        Assert.Equal(pool.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        await ConfirmEnemy(game);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        for (var remaining = 0; game.State.StackItems.Count > 0; remaining++)
        {
            Assert.True(remaining < 2);
            await game.Top();
        }
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Fact]
    public async Task ARealImageCopiesNamiAfterEntryWithoutRetroactivelyPayingOrTriggeringHerPlayAbility()
    {
        var initial = RadGearTestPosition.Put(Position(), "ENEMY", Nami, "P2", "BATTLEFIELD", "OTHER");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["ENEMY"]));
        var pool = MatchStateHasher.HashValue(game.State.RunePools);
        await game.Top();
        Assert.NotNull(Assert.Single(game.State.StackItems).ReflexiveCopy);
        await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(Nami, image.CardNo);
        Assert.False(image.IsExhausted);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == CoreRuleEngine.PaidPermanentPlayCaptured);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceAfterConfirmationDoesNotCancelItsPaidIndependentStun()
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        await ConfirmEnemy(game);
        var id = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(id, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("SPEND_POWER:1:BLUE")] [InlineData("SPEND_POWER:2:GREEN")]
    [InlineData("SPEND_POWER:0:GREEN")] [InlineData("SPEND_POWER:1")]
    public async Task ADeclarationWithAnotherColorOrAmountCannotPayTheExtraGreen(string fee)
        => await RadGearTestPosition.Reject(Position(), "P1",
            new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [fee]));

    [Theory, InlineData("erase"), InlineData("wrong-color"), InlineData("duplicate")]
    public async Task RecoveryCannotReplaceThePaidDeclarationWithOtherSnapshotChoices(string mutation)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        await ConfirmEnemy(game);
        var skill = Assert.Single(game.State.StackItems);
        var forged = skill with { OptionalCosts = mutation switch {
            "erase" => [], "wrong-color" => ["SPEND_POWER:1:BLUE"], _ => [Fee, Fee] } };
        var history = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.Empty(ValidateOriginalHistory(game, game.State));
        Assert.NotEmpty(ValidateOriginalHistory(game, game.State with { StackItems = [forged] }));
        Assert.Equal(history, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
    }

    [Fact]
    public async Task RecoveryCannotSilentlyEraseTheStillPendingPaidSkill()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        await ConfirmEnemy(game);
        var forged = game.State with { StackItems = [], PriorityPlayerId = null,
            PassedPriorityPlayerIds = [], TimingState = TimingStates.NeutralOpen };
        Assert.Contains(ValidateOriginalHistory(game, forged),
            error => error == "pending paid permanent play skills differ from original accepted capture and retirement history");
    }

    [Fact]
    public async Task AnUnpaidOriginalPlayCannotBorrowAnotherGamesValidPaidSkillSnapshot()
    {
        var paid = new Driver(Position());
        await paid.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        await ConfirmEnemy(paid);
        var skill = Assert.Single(paid.State.StackItems);
        var unpaid = new Driver(Position());
        await unpaid.Act("P1", new PlayCardCommand("UNIT", Nami, []));
        await unpaid.Act("P1", new TapRuneCommand("PAYMENT-RUNE"));
        Assert.Equal(paid.State.Tick, unpaid.State.Tick);
        Assert.Empty(unpaid.State.StackItems);
        Assert.Equal(skill.StackItemId, Assert.Single(unpaid.State.CardPlays.Resolved).PlayId);
        var forged = unpaid.State with { StackItems = [skill], PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [], TimingState = TimingStates.NeutralClosed };
        Assert.Empty(ValidateOriginalHistory(unpaid, unpaid.State));
        Assert.NotEmpty(ValidateOriginalHistory(unpaid, forged));
    }

    private static IReadOnlyList<string> ValidateOriginalHistory(Driver game, MatchState state)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, options), options)!;
        var sequence = game.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
    }
}
