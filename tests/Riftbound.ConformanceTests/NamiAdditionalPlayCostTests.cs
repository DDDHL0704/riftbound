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
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(30 - PrintedPowerCostRules.ForCard(Nami).Amount - 1, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceAfterConfirmationDoesNotCancelItsPaidIndependentStun()
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "SFD·135/221", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Nami, [], OptionalCosts: [Fee]));
        await ConfirmEnemy(game);
        var id = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["UNIT"]));
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
