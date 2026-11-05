using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EncouragedGraveyardPlayTests
{
    [Theory]
    [InlineData("BASE", true)]
    [InlineData("BATTLEFIELD:BF", true)]
    [InlineData("BASE", false)]
    [InlineData("BATTLEFIELD:BF", false)]
    public async Task AnotherConfirmedCardEnablesThePrintedGraveyardCost(string destination, bool resolves)
    {
        var game = new Driver(Position(graveyard: true, red: true));
        await Reject(game.State, new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: destination));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        if (!resolves)
        {
            var first = Assert.Single(game.State.StackItems).StackItemId;
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [first]));
        }
        await game.Top();
        Assert.Empty(game.State.StackItems);
        var command = new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: destination);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait.GetValueOrDefault("red"));
        var beforeMana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", command);
        Assert.Equal(beforeMana - 3, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault("red"));
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.True(Assert.Single(game.State.CardPlays.Finalized, play => play.Source.ObjectId == "UNIT").OriginOutsideHands);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task GraveyardPermissionDoesNotRemoveTheRedPowerCost()
    {
        var game = new Driver(Position(graveyard: true, red: false));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        await game.Top();
        await Reject(game.State, new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BASE"));
        await game.Replay();
    }

    [Fact]
    public async Task HandPlayKeepsItsOrdinaryCostWithoutInspireOrRedPower()
    {
        var game = new Driver(Position(graveyard: false, red: false));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BASE"));
        Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HoardsNormalPermissionAndTheCardsOwnCostRemainSeparateChoices(bool normal)
    {
        var initial = HandDiscardObserverTests.Put(Position(graveyard: true, red: true), "HOARD", "VEN·022", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        await game.Top();
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!,
            choice => choice.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            candidate.Metadata!["sourceRequirements"]).Where(row => Equals(row["sourceObjectId"], "UNIT")).ToArray();
        Assert.Equal(2, requirements.Length);
        Assert.Single(requirements, row => Equals(row["normalGraveyardPlayChoiceId"], "NORMAL_GRAVEYARD_PLAY"));
        var alternate = Assert.Single(requirements, row => row["graveyardPlayCost"] is not null);
        var shownCost = JsonSerializer.SerializeToElement(alternate["graveyardPlayCost"]);
        Assert.Equal(3, shownCost.GetProperty("mana").GetInt32());
        Assert.Equal(1, shownCost.GetProperty("power").GetInt32());
        Assert.Equal("red", Assert.Single(shownCost.GetProperty("traits").EnumerateArray()).GetString());
        var command = new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BASE",
            OptionalCosts: normal ? ["NORMAL_GRAVEYARD_PLAY"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(normal ? 0 : 1, quote.Cost.PowerByTrait.GetValueOrDefault("red"));
        await game.Act("P1", command);
        Assert.Equal(normal ? 1 : 0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault("red"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task TheOtherCardsConfirmationExpiresWhenTheTurnEnds()
    {
        var game = new Driver(Position(graveyard: true, red: true));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        await game.Top();
        var command = new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BASE");
        Assert.True(new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command)).CanPay);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        var rejected = await Reject(game.State, command);
        Assert.Equal(ErrorCodes.CardNotInHand, rejected.ErrorCode);
        await game.Replay();
    }

    [Fact]
    public async Task ARealPlayAndDestructionOfTheSameCardCannotEnableItsOwnGraveyardPermission()
    {
        var initial = Position(graveyard: false, red: true);
        initial = initial with {
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Base = ["GEAR", "ALLY"], Battlefields = ["BF"] }
                : p.Value with { Base = [], Battlefields = ["OTHER", "ENEMY"] }),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["ALLY"] = new("P1", "BASE"), ["ENEMY"] = new("P2", "BATTLEFIELD", "OTHER") } };
        initial = AttackDamageDistributionTests.Spell(initial, "KILL", "OGN·033/298", "P2");
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BATTLEFIELD:BF"));
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER"));
        if (game.State.FocusPlayerId != "P2") await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P2", new PlayCardCommand("KILL", "OGN·033/298", ["UNIT"]));
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation);
        await SacredProtectorTests.FinishCombat(game);
        Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]);
        var command = new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BASE");
        var rejected = await Reject(game.State, command);
        Assert.Equal(ErrorCodes.CardNotInHand, rejected.ErrorCode);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        await game.Top();
        await game.Act("P1", command);
        Assert.Equal(3, game.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault("red"));
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    private static MatchState Position(bool graveyard, bool red)
    {
        var state = VenUnitEntryBasicTests.Position("UNL-025/219");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["GEAR"], Battlefields = ["BF", "ALLY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["ALLY"] = new("P1", "BATTLEFIELD", "BF") } };
        if (graveyard) state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], Graveyard = ["GRAVE", "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, red ? new Dictionary<string, int> { ["red"] = 1 } : new Dictionary<string, int>()) } };
        state = AttackDamageDistributionTests.Spell(state, "FIRST", "OGN·048/298", "P1");
        return AttackDamageDistributionTests.Spell(state, "COUNTER", "OGN·045/298", "P2");
    }

    private static async Task<ResolutionResult> Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-graveyard-play", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        return result;
    }
}
