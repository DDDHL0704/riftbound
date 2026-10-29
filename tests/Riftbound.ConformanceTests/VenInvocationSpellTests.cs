using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenInvocationSpellTests
{
    public static TheoryData<string, string, int, string> Cards => new()
    {
        { "VEN·015", "OGN·049/298", 1, RuneTrait.Red },
        { "VEN·061", "OGN·142/298", 1, "" },
        { "VEN·107", "OGN·219/298", 1, RuneTrait.Purple },
        { "VEN·131", "OGN·175/298", 2, RuneTrait.Yellow }
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task EachInvocationPaysItsPrintedCostAndResolvesAgainstTheRequiredEnemyTrait(
        string no, string enemy, int mana, string trait)
    {
        var initial = Position(no, enemy);
        var driver = new Driver(initial);
        var command = new PlayCardCommand("UNIT", no, ["ENEMY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(trait.Length == 0 ? 0 : 1, quote.Cost.PowerByTrait.Values.Sum());
        if (trait.Length > 0) Assert.Equal(1, quote.Cost.PowerByTrait[trait]);
        await driver.Act("P1", command);
        Assert.Equal(20 - mana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(initial.CardObjects["ENEMY"].Power, driver.State.CardObjects["ENEMY"].Power);
        await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Graveyard);
        if (no == "VEN·015") Assert.Equal(4, driver.State.CardObjects["ENEMY"].Damage);
        if (no == "VEN·061")
        {
            Assert.Equal(5, driver.State.CardObjects["ENEMY"].Power);
            await driver.Act("P1", new EndTurnCommand());
            Assert.Equal(10, driver.State.CardObjects["ENEMY"].Power);
        }
        if (no == "VEN·107") Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Hand);
        if (no == "VEN·131") Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
        await driver.Replay();
    }

    public static IEnumerable<object[]> IllegalCases() => Cards.SelectMany(card =>
        new[] { "wrong-trait", "friendly", "graveyard", "equipment", "poor" }
            .Select(branch => new object[] { card[0], card[1], branch }));

    [Theory]
    [MemberData(nameof(IllegalCases))]
    public async Task WrongTraitsSourcesAndInsufficientManaLeaveTheStateUnchanged(string no, string enemy, string branch)
    {
        var state = Position(no, enemy);
        var target = branch == "friendly" ? "ALLY" : branch == "graveyard" ? "GRAVE" : branch == "equipment" ? "GEAR" : "ENEMY";
        if (branch == "wrong-trait") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·001/298", "P2") } };
        if (branch == "poor") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        await Reject(state, new PlayCardCommand("UNIT", no, [target]));
    }

    [Fact]
    public async Task FuryCannotBeCounteredButTheCounterCanStillBePlayedAndResolve()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·015", "OGN·049/298"), "COUNTER", "OGN·064/298", "P2");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·015", ["ENEMY"]));
        var spell = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell]));
        await driver.Top();
        Assert.Equal(spell, Assert.Single(driver.State.StackItems).StackItemId);
        Assert.Contains("COUNTER", driver.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        await driver.Top();
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Damage);
        await driver.Replay();
    }

    [Fact]
    public async Task InsightOffersQuotesAndPaysItsWardTargetWithNoPowerInTheWallet()
    {
        var state = Position("VEN·061", "OGN·142/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, CardObjectTags.Spellshield] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 0) } };
        var command = new PlayCardCommand("UNIT", "VEN·061", ["ENEMY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.GenericPower);
        Assert.Empty(quote.Cost.PowerByTrait);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.Sources!, source => source.Id == "UNIT");
        var driver = new Driver(state);
        await driver.Act("P1", command);
        await driver.Top();
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(5, driver.State.CardObjects["ENEMY"].Power);
        await driver.Replay();
    }

    [Fact]
    public async Task UnityAlsoDestroysAnEnemyChaosEquipment()
    {
        var state = Position("VEN·131", "OGN·175/298");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Base = ["ALLY"] },
            ["P2"] = state.PlayerZones["P2"] with { Base = ["ENEMY", "GEAR"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "UNL-138/219", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR"] = new("P2", "BASE") } };
        var command = new PlayCardCommand("UNIT", "VEN·131", ["GEAR"]);
        Assert.True(new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)).CanPay);
        var driver = new Driver(state);
        await driver.Act("P1", command);
        await driver.Top();
        Assert.Contains("GEAR", driver.State.PlayerZones["P2"].Graveyard);
        await driver.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task DiscordCanSelectZeroOrMultipleEnemiesUpToFiveTotalCurrentMight(int count)
    {
        var state = Position("VEN·107", "OGN·219/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "OGN·216/298", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Base = ["ENEMY", "SECOND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SECOND"] = new("P2", "BASE") } };
        await Reject(state, new PlayCardCommand("UNIT", "VEN·107", ["ENEMY", "ENEMY"]));
        var tooStrong = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECOND"] = state.CardObjects["SECOND"] with { Power = 2 } } };
        await Reject(tooStrong, new PlayCardCommand("UNIT", "VEN·107", ["ENEMY", "SECOND"]));
        var targets = new[] { "ENEMY", "SECOND" }.Take(count).ToArray();
        var driver = new Driver(state);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·107", targets));
        await driver.Top();
        Assert.Equal(count, driver.State.PlayerZones["P2"].Hand.Count);
        Assert.All(targets, id => Assert.Contains(id, driver.State.PlayerZones["P2"].Hand));
        await driver.Replay();
    }

    private static MatchState Position(string no, string enemy)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", enemy, "P2") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0,
                new Dictionary<string, int> { [RuneTrait.Red] = 2, [RuneTrait.Blue] = 2, [RuneTrait.Yellow] = 2, [RuneTrait.Purple] = 2 }) } };
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-invocation", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
