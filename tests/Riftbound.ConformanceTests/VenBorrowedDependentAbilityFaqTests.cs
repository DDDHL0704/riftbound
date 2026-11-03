using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenBorrowedDependentAbilityFaqTests
{
    // CN VEN FAQ p. 15, core 727.1.b.1: the borrowed dependency
    // checks the borrowing source, even when the legend is not empowered.
    [Theory]
    [InlineData("OGN·111/298", false)]
    [InlineData("OGN·111/298", true)]
    [InlineData("ARC-003/006", false)]
    [InlineData("ARC-003/006", true)]
    public async Task BorrowedTwoEquipmentSkillDependsOnHeimerRatherThanJayce(string printing, bool empower)
    {
        var game = await Ready(printing, empower);
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(empower, game.State.CardObjects["HOST"].IsEmpowered);
        Assert.False(game.State.CardObjects["JAYCE"].IsEmpowered);
        var available = ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [];
        var borrowed = available.Where(candidate => candidate.Enabled && candidate.Action == CommandTypes.ActivateAbility
            && candidate.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(candidate => (IEnumerable<IReadOnlyDictionary<string, object?>>)candidate.Metadata!["sourceRequirements"]!)
            .Where(row => Equals(row.GetValueOrDefault("sourceObjectId"), "HOST")
                && Equals(row.GetValueOrDefault("minTargetCount"), 2)).ToArray();
        Assert.Equal(empower, borrowed.Length > 0);
        if (empower)
        {
            var ability = Assert.IsType<string>(borrowed[0]["abilityId"]);
            var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new ActivateAbilityCommand("HOST", ability, ["GEAR", "GEAR2"]));
            Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
            Assert.True(game.State.CardObjects["HOST"].IsExhausted);
            Assert.False(game.State.CardObjects["JAYCE"].IsExhausted);
            Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
            Assert.True(game.State.CardObjects["GEAR2"].IsExhausted);
            Assert.Single(game.State.StackItems);
            var item = game.State.StackItems[0];
            var captured = item.BorrowedExhaust!;
            foreach (var forged in new[] {
                item with { BorrowedExhaust = null },
                item with { BorrowedExhaust = captured with { BorrowerWasEmpowered = false } },
                item with { BorrowedExhaust = captured with { AbilityIndex = 99 } },
                item with { BorrowedExhaust = captured with { Donor = captured.Donor with { CardNo = "FORGED" } } },
                item with { CardNo = "VEN·149" },
                item with { TargetGenerations = new Dictionary<string, long> { ["GEAR"] = 999, ["GEAR2"] = 0 } } })
                Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
            await game.Top();
            Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
            Assert.False(game.State.CardObjects["GEAR2"].IsExhausted);
        }
        await game.Replay();
    }

    private static async Task<Driver> Ready(string printing, bool empower, string secondEquipment = "SFD·022/221")
    {
        var initial = AttackDamageDistributionTests.Spell(EmpowerTests.Position(printing), "READY", "OGN·146/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "STATUS", "VEN·035", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "STATUS2", "VEN·035", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "LEAVE", "OGN·104/298", "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["JAYCE"] = new("JAYCE", cardNo: "VEN·149", ownerId: "P1", controllerId: "P1"),
                ["GEAR"] = initial.CardObjects["GEAR"] with { IsExhausted = true },
                ["GEAR2"] = new("GEAR2", cardNo: secondEquipment, ownerId: "P1", controllerId: "P1",
                    isExhausted: true, tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["JAYCE"], Base = [..initial.PlayerZones["P1"].Base, "GEAR2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["JAYCE"] = new("P1", "LEGEND"), ["GEAR2"] = new("P1", "BASE") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", printing, []));
        if (empower) {
            await game.Act("P1", new PlayCardCommand("STATUS", "VEN·035", ["HOST"], Mode: "EMPOWER_THEN_REMOVE"));
            await game.Top();
        }
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["HOST"]));
        await game.Top();
        return game;
    }

    [Theory]
    [InlineData("OGN·111/298")]
    [InlineData("ARC-003/006")]
    public async Task BorrowedEquipmentSkillKeepsItsOwnCostAndMightFloor(string printing)
    {
        var game = await Ready(printing, false, "OGN·090/298");
        var row = (ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .First(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST")
                && r.GetValueOrDefault("abilityLabel") is string label && label.Contains("战力", StringComparison.Ordinal));
        var mana = game.State.RunePools["P1"].Mana;
        var might = ArmamentMightTests.Might(game.State);
        await game.Act("P1", new ActivateAbilityCommand("HOST", (string)row["abilityId"]!, ["HOST"]));
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["GEAR2"].IsExhausted);
        await game.Top();
        Assert.Equal(Math.Max(1, might - 1), ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unit-target")]
    [InlineData("unempowered")]
    [InlineData("no-mana")]
    [InlineData("donor-departed")]
    [InlineData("stale-donor")]
    public async Task InvalidBorrowedSkillCannotPayOrExhaustSource(string boundary)
    {
        var game = await Ready("OGN·111/298", true);
        var state = game.State;
        var id = BorrowedId(state, 2);
        IReadOnlyList<string> targets = boundary switch {
            "duplicate" => ["GEAR", "GEAR"], "unit-target" => ["HOST", "GEAR"], _ => ["GEAR", "GEAR2"] };
        if (boundary == "unempowered") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsEmpowered = false } } };
        if (boundary == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) } };
        if (boundary == "donor-departed") state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { LegendZone = [], Graveyard = [..state.PlayerZones["P1"].Graveyard, "JAYCE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["JAYCE"] = new("P1", "GRAVEYARD") } };
        if (boundary == "stale-donor") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["JAYCE"] = state.CardObjects["JAYCE"] with { ObjectGeneration = state.CardObjects["JAYCE"].ObjectGeneration + 1 } } };
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("borrowed-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, targets), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedDependentSkillSurvivesSourceLosingEmpowerOrLeaving(bool leave)
    {
        var game = await Ready("OGN·111/298", true);
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedId(game.State, 2), ["GEAR", "GEAR2"]));
        await game.Act("P1", leave
            ? new PlayCardCommand("LEAVE", "OGN·104/298", ["HOST"])
            : new PlayCardCommand("STATUS2", "VEN·035", ["HOST"], Mode: "REMOVE_THEN_EMPOWER"));
        await game.Top();
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        if (leave) Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.False(game.State.CardObjects["GEAR2"].IsExhausted);
        await game.Replay();
    }

    private static string BorrowedId(MatchState state, int targets)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST") && Equals(r.GetValueOrDefault("minTargetCount"), targets))
            .Select(r => (string)r["abilityId"]!).First();
}
