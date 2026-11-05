using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SkateboardProEntryTests
{
    private const string SkateboardPro = "SFD·072/221";

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task TwoEquipmentEarnAnIndependentReadyAbilityAfterTheUnitEntersExhausted(string destination)
    {
        var game = new Driver(Position(2));
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, [], Destination: destination));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(16, game.State.RunePools["P1"].Mana);
        Assert.Single(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_READIED");
        await game.Replay();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TheAdditionalTriggerConditionIsCheckedAgainstTheEquipmentThreshold(int count)
    {
        var game = new Driver(Position(count));
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, []));
        if (count >= 2)
        {
            Assert.Single(game.State.StackItems);
            await game.Top();
        }
        else Assert.Empty(game.State.StackItems);
        Assert.Equal(count < 2, game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("hand")]
    [InlineData("graveyard")]
    [InlineData("opponent")]
    [InlineData("unit")]
    public async Task OnlyControlledFieldEquipmentContributeToTheCondition(string excluded)
    {
        var initial = RadGearTestPosition.Put(Position(1), "EXTRA", excluded == "unit" ? "OGN·219/298" : "SFD·022/221",
            excluded == "opponent" ? "P2" : "P1", excluded switch { "hand" => "HAND", "graveyard" => "GRAVEYARD", _ => "BASE" });
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task EquipmentOwnedByTheOpponentStillCountsWhenControlledByThePlayer()
    {
        var initial = Position(2);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["EXTRA-1"] = initial.CardObjects["EXTRA-1"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, []));
        await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ARealFaceDownStandbyEquipmentDoesNotCountOrLeakItsFace()
    {
        var initial = RadGearTestPosition.Put(Position(1), "SECRET", "SFD·139/221", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new HideCardCommand("SECRET", "SFD·139/221", "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.True(game.State.CardObjects["SECRET"].IsFaceDown);
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task PlayingTheSecondEquipmentLaterDoesNotInventAnEarlierReadyTrigger()
    {
        var initial = RadGearTestPosition.Put(Position(1), "LATER", "SFD·022/221", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, []));
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("LATER", "SFD·022/221", ["ALLY"]));
        Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == "UNIT");
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task SpendingCountedGoldInResponseDoesNotRecheckTheTriggerCondition()
    {
        var initial = Position(2);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T05", out var factory));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["EXTRA-1"] = factory.CreateObject("EXTRA-1", "P1", "P1") with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, []));
        Assert.Single(game.State.StackItems);
        await game.Act("P1", new ActivateAbilityCommand("EXTRA-1", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        Assert.False(game.State.CardObjects.ContainsKey("EXTRA-1"));
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceDoesNotReadyItsNewGenerationInHand()
    {
        var initial = RadGearTestPosition.Put(Position(2), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", SkateboardPro, []));
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation);
        Assert.Single(game.State.StackItems);
        var events = game.Events().Length;
        await game.Top();
        Assert.DoesNotContain(game.Events().Skip(events), e => e.Event.Kind == "UNIT_READIED");
        await game.Replay();
    }

    [Fact]
    public async Task TheZeroEquipmentFixtureFinishesWithoutAnEmptyAbilityChain()
    {
        var fixture = await ConformanceFixture.LoadAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "p2-preflight-play-skateboard-pro-no-equipment-static.fixture.json"), default);
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), default);
        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    private static MatchState Position(int equipment)
    {
        var state = VenUnitEntryBasicTests.Position(SkateboardPro);
        if (equipment == 0) state = RadUnitEffectTestFixture.Place(state, "GEAR", "P1", "GRAVEYARD");
        for (var i = 1; i < equipment; i++)
            state = RadGearTestPosition.Put(state, "EXTRA-" + i, "SFD·022/221", "P1", "BASE");
        return state;
    }
}
