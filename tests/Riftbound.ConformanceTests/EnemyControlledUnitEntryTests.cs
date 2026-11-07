using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EnemyControlledUnitEntryTests
{
    private const string Predator = "OGN·161/298", Vanguard = "SFD·093/221";
    private static MatchState Position(string no)
        => RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(no), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
    private static PlayCardCommand Play(string no, string destination = "BATTLEFIELD:OTHER") => new("UNIT", no, [], Destination: destination);
    private static string[] Destinations(MatchState state, string no)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        return CoreRuleEngine.UnitPlayDestinationChoices(state, "P1", behavior).Select(choice => choice.Id).Order().ToArray();
    }

    [Theory]
    [InlineData(Predator, 8, 2)] [InlineData(Vanguard, 4, 1)]
    public async Task EachPrintedUnitPaysItsNormalCostAndEntersTheOpponentsControlledBattlefield(string no, int mana, int power)
    {
        var initial = Position(no);
        Assert.Equal(new[] { "BASE", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER" }, Destinations(initial, no));
        var game = new Driver(initial);
        var prompt = JsonSerializer.SerializeToElement(game.PromptFor("P1"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var play = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == CommandTypes.PlayCard);
        var source = play.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
            .Single(c => c.GetProperty("sourceObjectId").GetString() == "UNIT");
        Assert.Contains(source.GetProperty("destinationChoices").EnumerateArray(), c => c.GetProperty("id").GetString() == "BATTLEFIELD:OTHER");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, Play(no)));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana); Assert.Equal(power, quote.Cost.PowerByTrait[RuneTrait.Orange]);
        await game.Act("P1", Play(no));
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "OTHER"), game.State.ObjectLocations["UNIT"]);
        Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(20 - power, game.State.RunePools["P1"].TotalPower);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_PLAYED_TO_BATTLEFIELD");
        await game.Replay();
    }

    [Theory]
    [InlineData(Predator, "BASE")] [InlineData(Predator, "BATTLEFIELD:BF")]
    [InlineData(Vanguard, "BASE")] [InlineData(Vanguard, "BATTLEFIELD:BF")]
    public async Task TheOptionalEnemyLocationRetainsBaseAndFriendlyBattlefieldEntry(string no, string destination)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", Play(no, destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(destination == "BASE" ? null : "BF", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        await game.Replay();
    }

    [Theory]
    [InlineData(Predator, "neutral")] [InlineData(Vanguard, "neutral")]
    [InlineData(Predator, "hidden")] [InlineData(Vanguard, "hidden")]
    [InlineData(Predator, "non-battlefield")] [InlineData(Vanguard, "non-battlefield")]
    [InlineData(Predator, "missing")] [InlineData(Vanguard, "missing")]
    public async Task EnemyOccupationAloneAndInvalidFieldObjectsCannotGrantThePermission(string no, string invalid)
    {
        var state = Position(no);
        if (invalid != "missing") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with {
                ControllerId = invalid == "neutral" ? null : "P2", IsFaceDown = invalid == "hidden",
                Tags = invalid == "non-battlefield" ? [CardObjectTags.UnitCard] : state.CardObjects["OTHER"].Tags } } };
        var command = Play(no, invalid == "missing" ? "BATTLEFIELD:ABSENT" : "BATTLEFIELD:OTHER");
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.IsValid);
        await RadGearTestPosition.Reject(state, "P1", command);
    }

    [Theory]
    [InlineData(Predator)] [InlineData(Vanguard)]
    public async Task BattlefieldControlRatherThanItsOwnerDeterminesEnemyEntry(string no)
    {
        var initial = Position(no) with { CardObjects = new Dictionary<string, CardObjectState>(Position(no).CardObjects) {
            ["OTHER"] = Position(no).CardObjects["OTHER"] with { OwnerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P1", Play(no));
        Assert.Equal("P1", game.State.CardObjects["OTHER"].OwnerId);
        Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        await game.Replay();
    }

    [Theory]
    [InlineData(Predator)] [InlineData(Vanguard)]
    public async Task LocationPermissionDoesNotGrantReactionTiming(string no)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(no), "SPELL", "OGN·048/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        Assert.Empty(Destinations(game.State, no));
        await RadGearTestPosition.Reject(game.State, "P1", Play(no));
        await game.Top();
        await game.Act("P1", Play(no));
        await game.Replay();
    }

    [Theory]
    [InlineData(Predator)] [InlineData(Vanguard)]
    public async Task OrdinaryGraveyardEffectPlayKeepsThePrintedEnemyLocationAndItsPowerCost(string no)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(no), "UNIT", "P1", "GRAVEYARD");
        initial = RadGearTestPosition.Put(initial, "PARENT", "OGN·198/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PARENT", "OGN·198/298", ["UNIT"]));
        await game.Top();
        Assert.Equal("ANY", game.State.PendingEffectPlay!.DestinationPolicy);
        Assert.Contains("BATTLEFIELD:OTHER", CoreRuleEngine.EffectPlayDestinations(game.State, "P1"));
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", Play(no));
        Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.Equal(pool.Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - (no == Predator ? 2 : 1), game.State.RunePools["P1"].TotalPower);
        Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Theory]
    [InlineData(Predator)] [InlineData(Vanguard)]
    public async Task AnEffectRequiringAControlledBattlefieldCannotUseTheOptionalEnemyLocation(string no)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(no), "PARENT", "SFD·111/221", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("PARENT", "SFD·111/221", []));
        await game.Top();
        Assert.Equal("CONTROLLED_BATTLEFIELD", game.State.PendingEffectPlay!.DestinationPolicy);
        Assert.Equal(new[] { "BATTLEFIELD:BF" }, CoreRuleEngine.EffectPlayDestinations(game.State, "P1"));
        await RadGearTestPosition.Reject(game.State, "P1", Play(no));
        await game.Act("P1", Play(no, "BATTLEFIELD:BF"));
        await game.Replay();
    }

    [Fact]
    public async Task AnOrdinaryUnitCannotBorrowAnotherCardsEnemyLocationPermission()
    {
        var initial = RadGearTestPosition.Put(Position(Predator), "ORDINARY", "OGN·219/298", "P1", "HAND");
        await RadGearTestPosition.Reject(initial, "P1", new PlayCardCommand("ORDINARY", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
    }
}
