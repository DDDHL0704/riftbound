using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Only the exact registrations, parsing and real play/Empower prerequisites.
// Targeted-action replacement conformance belongs to the separate shared C7.
public sealed class GangplankProfilePrerequisiteTests
{
    private const string TargetClause = "{{已强化>}} 如果一个将我选作目标的法术或技能将眩晕我、或给予我-{{S}}、或把我送回手牌，则改为给予我在本回合内{{S}}+3。";

    [Theory]
    [InlineData("VEN·086", 37984)] [InlineData("VEN·181", 37985)]
    public async Task EachExactCnFaceRetainsPrintedUnitCostsAndOneCompositeTargetProfile(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("普朗克", card.CardName); Assert.Equal("海上霸主", card.SubTitle);
        Assert.Equal(6, card.Energy); Assert.Equal(6, card.Power); Assert.Null(card.ReturnEnergy); Assert.Equal(["orange"], card.CardColorList);
        Assert.Contains("英雄单位", card.CardCategoryNameList); Assert.Contains(TargetClause, card.CardEffect, StringComparison.Ordinal);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(6, behavior.ManaCost); Assert.Equal(6, behavior.SourceUnitPower); Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
        var activation = Assert.Single(EmpowerParser.Parse(card.CardEffect)); var cost = Assert.Single(activation.Alternatives);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Equal(2, cost.TypedPower["orange"]); Assert.Single(cost.TypedPower);
        Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust);
        var profile = Assert.Single(EmpowerParser.TargetReplacements(card.CardEffect));
        Assert.Equal(["STUN", "NEGATIVE_MIGHT", "RETURN_TO_HAND"], profile.ActionKinds); Assert.Equal(3, profile.MightBonus);
        Assert.Equal(0, EmpowerParser.Self(card.CardEffect).Might); Assert.Empty(EmpowerParser.Self(card.CardEffect).Keywords);
        Assert.Empty(EmpowerParser.SpellEffects(card.CardEffect));
    }

    [Theory]
    [InlineData("VEN·086", "BASE")] [InlineData("VEN·181", "BASE")]
    [InlineData("VEN·086", "BATTLEFIELD:BF")] [InlineData("VEN·181", "BATTLEFIELD:BF")]
    public async Task RealPrintedPlayAndRealEmpowerPayTheirSeparateCostsAndColdRecover(string no, string destination)
    {
        var initial = Position(no); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.Equal(14, driver.State.RunePools["P1"].Mana); Assert.Equal(4, driver.State.RunePools["P1"].PowerByTrait["orange"]);
        var unit = driver.State.CardObjects["UNIT"]; Assert.Equal(no, unit.CardNo); Assert.Equal(6, unit.Power);
        Assert.Contains(CardObjectTags.UnitCard, unit.Tags); Assert.Contains("CARD_TYPE:HERO", unit.Tags); Assert.Contains("海盗", unit.Tags);
        Assert.True(unit.IsExhausted); Assert.False(unit.IsEmpowered);
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.Single(driver.State.StackItems); Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(14, driver.State.RunePools["P1"].Mana); Assert.Equal(2, driver.State.RunePools["P1"].PowerByTrait["orange"]);
        Assert.Equal(3, driver.State.RunePools["P1"].PowerByTrait["blue"]);
        await driver.Top(); Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        Assert.Empty(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        await driver.Replay(); Export(no, destination, initial, driver);
    }

    [Theory]
    [InlineData("VEN·086", "orange")] [InlineData("VEN·181", "orange")]
    [InlineData("VEN·086", "controller")] [InlineData("VEN·181", "controller")]
    public async Task InsufficientTypedEmpowerCostAndForeignControllerPreserveAllStateAndViews(string no, string branch)
    {
        var driver = new Driver(Position(no)); await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        var state = driver.State;
        if (branch == "orange") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(14, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 1, [RuneTrait.Blue] = 20 }) } };
        await OptionalEntryTargetBoundaryTests.Reject(state, branch == "controller" ? "P2" : "P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
    }

    [Theory]
    [InlineData("no-gate")] [InlineData("no-chosen")] [InlineData("enemy-only")]
    [InlineData("no-expiry")] [InlineData("negative-result")] [InlineData("zero-result")]
    [InlineData("partial-actions")] [InlineData("two-independent-clauses")]
    public void UnsupportedOrAmbiguousShapesDoNotInventTargetReplacementInstances(string branch)
    {
        var text = branch switch {
            "no-gate" => TargetClause.Replace("{{已强化>}}", "", StringComparison.Ordinal),
            "no-chosen" => TargetClause.Replace("将我选作目标", "影响我", StringComparison.Ordinal),
            "enemy-only" => TargetClause.Replace("如果一个", "如果一个敌方", StringComparison.Ordinal),
            "no-expiry" => TargetClause.Replace("在本回合内", "", StringComparison.Ordinal),
            "negative-result" => TargetClause.Replace("+3", "-3", StringComparison.Ordinal),
            "zero-result" => TargetClause.Replace("+3", "+0", StringComparison.Ordinal),
            "partial-actions" => TargetClause.Replace("、或给予我-{{S}}、或把我送回手牌", "", StringComparison.Ordinal),
            _ => TargetClause + "\n" + TargetClause
        };
        Assert.Empty(EmpowerParser.TargetReplacements(text));
    }

    [Fact]
    public void CasterSideExtraNegativeMightRemainsAnIndependentProfile()
    {
        const string text = "{{已强化>}} 你控制的法术或技能将给予其选作目标的一名单位-{{S}}时，额外给予{{S}}-1。";
        Assert.Empty(EmpowerParser.TargetReplacements(text));
        Assert.Equal(1, Assert.Single(EmpowerParser.SpellEffects(text)).AdditionalChosenNegativeMight);
    }

    private static MatchState Position(string no)
    {
        var initial = VenUnitEntryBasicTests.Position(no);
        return initial with { RoomId = "GANGPLANK-REGISTERED-PREREQUISITE", RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 4, [RuneTrait.Blue] = 3 }) } };
    }
    private static void Export(string no, string destination, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_GANGPLANK_PREREQUISITE_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, no.Replace('·', '-') + "-" + destination.Replace(':', '-') + ".json"),
            JsonSerializer.Serialize(new { scope = "Exact registered play/Empower prerequisites; no targeted replacement execution yet.",
                initial, final = driver.State, commands = driver.Commands(), events = driver.Events() }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
