using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenPlainArmamentTests
{
    public static TheoryData<string, string, int, int> Cards => new()
    {
        { "VEN·011", RuneTrait.Red, 3, 1 },
        { "VEN·027", RuneTrait.Green, 2, 1 },
        { "VEN·073", RuneTrait.Orange, 3, 2 }
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task PaidPlayAndAssemblyUseThePrintedColorAndOnlyGrantMightAfterResponses(
        string no, string trait, int mana, int bonus)
    {
        var driver = new Driver(Position(no, trait));
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var source = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(mana, source.Energy);
        Assert.Equal(bonus, source.Power);
        Assert.Contains("装配", source.CardEffect);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards),
            OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards,
                CardBehaviorRegistry.GetAll().Select(b => new ImplementedCardBehavior(b.CardNo, b.EffectKind, b.DisplayName)).ToArray()));
        Assert.Equal(BehaviorImplementationStatuses.Implemented, Assert.Single(specs, s => s.CardNo == no).Status);

        var baseMight = Might(driver.State);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(20 - mana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(1, driver.State.RunePools["P1"].PowerByTrait[trait]);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Contains(CardEquipmentKeywordNames.Weapon, driver.State.CardObjects["UNIT"].Tags);
        Assert.Equal(baseMight, Might(driver.State));

        await driver.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", ["ASSEMBLE_" + trait.ToUpperInvariant()]));
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Null(driver.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Equal(baseMight, Might(driver.State));
        await driver.Top();
        Assert.Equal("ALLY", driver.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Equal(baseMight + bonus, Might(driver.State));
        Assert.Equal(baseMight, driver.State.CardObjects["ALLY"].Power);
        await driver.Replay();
    }

    public static IEnumerable<object[]> IllegalCases() => Cards.SelectMany(card =>
        new[] { "wrong-color", "no-power", "enemy", "equipment", "hand-source", "poor-play" }
            .Select(branch => new object[] { card[0], card[1], branch }));

    [Theory]
    [MemberData(nameof(IllegalCases))]
    public async Task IllegalPaymentAndTargetsNeverChangeState(string no, string trait, string branch)
    {
        var state = Position(no, trait);
        if (branch == "poor-play")
        {
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) } };
            await Reject(state, new PlayCardCommand("UNIT", no, []));
            return;
        }
        if (branch != "hand-source")
        {
            var driver = new Driver(state);
            await driver.Act("P1", new PlayCardCommand("UNIT", no, []));
            state = driver.State;
        }
        if (branch is "wrong-color" or "no-power")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [trait == RuneTrait.Blue ? RuneTrait.Red : RuneTrait.Blue] = branch == "no-power" ? 0 : 1 }) } };
        await Reject(state, new AssembleEquipmentCommand("UNIT",
            branch == "enemy" ? "ENEMY" : branch == "equipment" ? "GEAR" : "ALLY", ["ASSEMBLE_" + trait.ToUpperInvariant()]));
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task DestroyingTheArmamentRemovesItsBonusAndKeepsTheHostPrintedPower(
        string no, string trait, int mana, int bonus)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(no, trait), "DESTROY", "SFD·005/221");
        var driver = new Driver(initial);
        var power = Might(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(20 - mana, driver.State.RunePools["P1"].Mana);
        await driver.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", ["ASSEMBLE_" + trait.ToUpperInvariant()]));
        await driver.Top();
        Assert.Equal(power + bonus, Might(driver.State));
        await driver.Act("P1", new PlayCardCommand("DESTROY", "SFD·005/221", ["UNIT"]));
        await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Null(driver.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Equal(power, Might(driver.State));
        Assert.Equal(power, driver.State.CardObjects["ALLY"].Power);
        await driver.Replay();
    }

    private static MatchState Position(string no, string trait)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [trait] = 1 }) } };
    }

    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(
        ResolutionResult.BuildSnapshots(state)["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web))
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("ALLY")
        .GetProperty("effectivePower").GetInt32();

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-armament", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
