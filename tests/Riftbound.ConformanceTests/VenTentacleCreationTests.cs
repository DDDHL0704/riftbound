using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenTentacleCreationTests
{
    [Theory]
    [InlineData("VEN·T04", 37924, "随从")]
    [InlineData("VEN·T06", 37926, "比尔吉沃特")]
    public async Task OfficialTokenPrintingsHaveTheirCoreDefinedCharacteristicsAndCannotEnterADeck(
        string no, long id, string tag)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id);
        Assert.Equal("指示物单位", card.CardCategoryName);
        Assert.True(OfficialRuleDomainBehaviorCatalog.IsTokenCard(card));
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no, out _));
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory));
        var token = factory.CreateObject("TOKEN", "P1", "P1", isExhausted: true);
        Assert.Equal(1, token.Power);
        Assert.Equal(no, token.TokenFactoryCardNo);
        Assert.Contains(tag, token.Tags);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.True(token.IsExhausted);
        var validation = OfficialDeckValidator.Validate(new("", "", [no], [], []), catalog);
        Assert.False(validation.IsValid);
        Assert.Contains($"主牌堆中不能使用指示物：{no}。", validation.Errors);
        var state = VenUnitEntryBasicTests.Position("VEN·100");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = factory.CreateObject("UNIT", "P1", "P1") } };
        await Reject(state, new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HandAndFlowPlaysPayThreeManaAndCreateTwoDormantBilgewaterTentacles(bool flow)
    {
        var initial = Position(flow);
        var command = new PlayCardCommand("UNIT", "VEN·100", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Empty(quote.Cost.PowerByTrait);
        Assert.Equal(0, quote.Cost.GenericPower);
        var driver = new Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(17, driver.State.RunePools["P1"].Mana);
        Assert.Empty(Tokens(driver.State));
        await driver.Top();
        var tokens = Tokens(driver.State);
        Assert.Equal(2, tokens.Length);
        Assert.All(tokens, token => {
            Assert.Equal("VEN·T06", token.CardNo);
            Assert.Equal("VEN·T06", token.TokenFactoryCardNo);
            Assert.Equal(1, token.Power);
            Assert.True(token.IsExhausted);
            Assert.Equal("P1", token.OwnerId);
            Assert.Equal("P1", token.ControllerId);
            Assert.Contains("比尔吉沃特", token.Tags);
            Assert.Contains(CardObjectTags.UnitCard, token.Tags);
            Assert.DoesNotContain(token.Tags, tag => tag.StartsWith("COLOR:"));
            Assert.Contains(token.ObjectId, driver.State.PlayerZones["P1"].Base);
        });
        Assert.Contains("UNIT", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        await driver.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnpayableOrForgedTargetsCannotCreateTokensOrSpendResources(bool flow)
    {
        var state = Position(flow);
        await Reject(state, new PlayCardCommand("UNIT", "VEN·100", ["ALLY"]));
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 20) } };
        await Reject(state, new PlayCardCommand("UNIT", "VEN·100", []));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CounteringTheSpellCreatesNoTokensAndPreservesItsSourceDestination(bool flow)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(flow), "COUNTER", "OGN·045/298", "P2");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·100", []));
        var item = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item]));
        await driver.Top();
        Assert.Empty(driver.State.StackItems);
        Assert.Empty(Tokens(driver.State));
        Assert.Contains("UNIT", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·T04")] [InlineData("VEN·T06")]
    public async Task ReturningEitherTokenToHandMakesItCeaseToExist(string no)
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory));
        var initial = OfficialCopyIdentityTests.AddSpell(Position(false), "RETURN", "OGN·104/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = factory.CreateObject("ALLY", "P1", "P1") } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await driver.Top();
        Assert.DoesNotContain("ALLY", driver.State.CardObjects.Keys);
        Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Graveyard);
        await driver.Replay();
    }

    private static MatchState Position(bool flow)
    {
        var state = VenUnitEntryBasicTests.Position("VEN·100");
        return flow ? state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [], Graveyard = ["GRAVE", "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } } : state;
    }

    private static CardObjectState[] Tokens(MatchState state) => state.CardObjects.Values.Where(card => card.TokenFactoryCardNo is not null).ToArray();

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-token-play", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
