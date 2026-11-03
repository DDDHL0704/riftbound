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
        var initial = AttackDamageDistributionTests.Spell(EmpowerTests.Position(printing), "READY", "OGN·146/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "STATUS", "VEN·035", "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["JAYCE"] = new("JAYCE", cardNo: "VEN·149", ownerId: "P1", controllerId: "P1"),
                ["GEAR"] = initial.CardObjects["GEAR"] with { IsExhausted = true },
                ["GEAR2"] = new("GEAR2", cardNo: "SFD·022/221", ownerId: "P1", controllerId: "P1",
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
        await game.Replay();
    }
}
