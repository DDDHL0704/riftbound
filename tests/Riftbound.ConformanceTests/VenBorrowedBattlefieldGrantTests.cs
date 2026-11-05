using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class VenBorrowedBattlefieldGrantTests
{
    private static MatchState Garden(string printing = "OGN·111/298", bool here = true)
    {
        var state = Put(Create("UNL-213/219"), "BORROWER", printing, "P1", "BASE");
        state = Put(state, "DONOR", "OGN·001/298", "P1", here ? "BATTLEFIELD" : "BASE", here ? "BF" : null);
        return AttackDamageDistributionTests.Spell(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["DONOR"] = state.CardObjects["DONOR"] with { IsExhausted = true } } }, "RETURN", "OGN·104/298", "P1");
    }

    private static IEnumerable<string> Skills(MatchState state, string label)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.ActivateAbility && c.Metadata?.GetValueOrDefault("sourceRequirements")
                is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "BORROWER")
                && ((string)r["abilityLabel"]!).Contains(label, StringComparison.Ordinal))
            .Select(r => (string)r["abilityId"]!);

    [Theory]
    [InlineData("OGN·111/298", true)]
    [InlineData("ARC-003/006", true)]
    [InlineData("OGN·111/298", false)]
    [InlineData("ARC-003/006", false)]
    public async Task GardenGrantedSkillIsBorrowedFromAnotherUnitHereAndWorksAtBase(string printing, bool here)
    {
        var state = Garden(printing, here);
        if (!here) { Assert.Empty(Skills(state, "获得 1 经验")); return; }
        var id = Assert.Single(Skills(state, "获得 1 经验"));
        await Reject(state, "P2", new ActivateAbilityCommand("BORROWER", id, []));
        await Reject(state, "P1", new ActivateAbilityCommand("BORROWER", id, ["DONOR"]));
        var game = new Driver(state);
        var before = state.PlayerExperience.GetValueOrDefault("P1");
        await game.Act("P1", new ActivateAbilityCommand("BORROWER", id, []));
        Assert.True(game.State.CardObjects["BORROWER"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Equal(before, game.State.PlayerExperience.GetValueOrDefault("P1"));
        Assert.Equal(state.RunePools["P1"], game.State.RunePools["P1"]);
        var item = Assert.Single(game.State.StackItems);
        var context = item.BorrowedExhaust!;
        Assert.Equal("BF", context.BattlefieldSource!.ObjectId);
        foreach (var bad in new[] { context with { BattlefieldSource = null },
            context with { BattlefieldCardNo = "SFD·208/221" },
            context with { BattlefieldSource = new("BF", 999) } })
            Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { BorrowedExhaust = bad }] }));
        await game.Top();
        Assert.Equal(before + 1, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("BORROWER")]
    [InlineData("DONOR")]
    public async Task CapturedGardenSkillSurvivesItsBorrowerOrRecipientReturningInResponse(string returned)
    {
        var game = new Driver(Garden());
        await game.Act("P1", new ActivateAbilityCommand("BORROWER", Assert.Single(Skills(game.State, "获得 1 经验")), []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top();
        Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(1, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    private static MatchState Forge(string printing, bool attached, bool controlled = true)
    {
        var state = Put(Create("SFD·208/221"), "BORROWER", printing, "P1", "BASE");
        state = Put(state, "GEAR", "SFD·022/221", "P1", "BASE");
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["DONOR"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["DONOR"] = OfficialGarenConquestBattlefieldCountTests.Printed("DONOR", "OGN·265/298", "P1")
                    with { IsExhausted = true },
                ["BF"] = state.CardObjects["BF"] with { ControllerId = controlled ? "P1" : "P2" },
                ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = attached ? "ALLY" : null } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["DONOR"] = new("P1", "LEGEND") } };
    }

    [Theory]
    [InlineData("OGN·111/298", false)]
    [InlineData("OGN·111/298", true)]
    [InlineData("ARC-003/006", false)]
    [InlineData("ARC-003/006", true)]
    public async Task ControlledForgeGrantsBorrowableLegendSkillForAttachedAndUnattachedArmaments(string printing, bool attached)
    {
        Assert.Empty(Skills(Forge(printing, attached, false), "一件武装"));
        var state = Forge(printing, attached);
        var id = Assert.Single(Skills(state, "一件武装"));
        await Reject(state, "P1", new ActivateAbilityCommand("BORROWER", id, ["GEAR", "ALLY"]));
        await Reject(state, "P1", new ActivateAbilityCommand("BORROWER", id, ["ENEMY", "GEAR"]));
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("BORROWER", id, ["BORROWER", "GEAR"]));
        Assert.True(game.State.CardObjects["BORROWER"].IsExhausted);
        Assert.Equal(attached ? "ALLY" : null, game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(state.RunePools["P1"], game.State.RunePools["P1"]);
        var item = Assert.Single(game.State.StackItems);
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { BorrowedExhaust = item.BorrowedExhaust! with { BattlefieldSource = null } }] }));
        await game.Top();
        Assert.Equal("BORROWER", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(attached ? 1 : 0, game.Events().Count(e => e.Event.Kind == "EQUIPMENT_DETACHED"));
        Assert.Single(game.Events(), e => e.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.Replay();
    }
}
