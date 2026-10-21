using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.CurrentControllerDamageBonusTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SpecialTriggerDamageBonusTests
{
    public static IEnumerable<object[]> LocationBranches() => new[] { "UNL-118/219", "UNL-118a/219" }
        .SelectMany(printing => new[] { 0, 1, 2 }.SelectMany(copies => new[] { false, true }
            .Select(local => new object[] { printing, copies, local })));

    [Theory]
    [MemberData(nameof(LocationBranches))]
    public async Task PerLocationPlaySkillAddsCurrentControllerAndEachTargetsLocalBonus(string printing, int copies, bool local)
    {
        var state = LocationPosition(printing, local);
        for (var n = 0; n < copies; n++) state = Annie(state, "P1", id: "ANNIE" + n);
        var selected = await SelectLocation(state, printing);
        var done = await OfficialGraveyardRecastTests.Top(selected.State);
        AssertLocationDamage(done, 1 + copies + (local ? 1 : 0), 1 + copies);
        Assert.Contains("D2", done.State.PlayerZones["P2"].Battlefields);
        Restore(done.State);
    }

    public static IEnumerable<object[]> ConquestBranches() => new[] { "SFD·116/221", "SFD·233/221", "SFD·233*/221" }
        .SelectMany(printing => new[] { 0, 1, 2 }.SelectMany(copies => new[] { 0, 5 }
            .Select(power => new object[] { printing, copies, power })));

    [Theory]
    [MemberData(nameof(ConquestBranches))]
    public async Task ConquestSkillAddsBonusToItsCurrentPowerIncludingAZeroValuedDamageInstruction(string printing, int copies, int power)
    {
        var state = ConquestPosition(printing, power);
        for (var n = 0; n < copies; n++) state = Annie(state, "P1", id: "ANNIE" + n);
        var selected = await SelectConquest(state);
        var done = await OfficialGraveyardRecastTests.Top(selected.State);
        var expected = power + copies;
        Assert.Equal(expected, done.State.CardObjects["ENEMY"].Damage);
        if (expected == 0) Assert.DoesNotContain(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        else Assert.Equal(expected, Assert.Single(done.Events, e => e.Kind == "DAMAGE_APPLIED").Payload["damage"]);
        Assert.Contains("ENEMY", done.State.PlayerZones["P2"].Base);
        Restore(done.State);
    }

    public static IEnumerable<object[]> IneligibleSources() => new[] { false, true }
        .SelectMany(location => new[] { "hand", "graveyard", "opponent", "departed" }
            .Select(branch => new object[] { location, branch }));

    [Theory]
    [MemberData(nameof(IneligibleSources))]
    public async Task BothSkillPathsReadOnlyTheirControllersSourcesStillOnTheFieldAtResolution(bool location, string branch)
    {
        var state = location ? LocationPosition() : ConquestPosition();
        state = Annie(state, branch == "opponent" ? "P2" : "P1",
            branch == "hand" ? "HAND" : branch == "graveyard" ? "GRAVEYARD" : "BASE");
        var selected = location ? await SelectLocation(state) : await SelectConquest(state);
        if (branch == "departed") selected = selected with { State = Place(selected.State, "ANNIE", "P1", "GRAVEYARD") };
        Restore(selected.State);
        var done = await OfficialGraveyardRecastTests.Top(selected.State);
        if (location) AssertLocationDamage(done, 1, 1);
        else Assert.Equal(5, done.State.CardObjects["ENEMY"].Damage);
        Restore(done.State);
    }

    [Theory]
    [InlineData("global")]
    [InlineData("next")]
    [InlineData("immune")]
    public async Task PerLocationBonusesReachExistingPreventionBeforeElderLethality(string prevention)
    {
        var state = Annie(LocationPosition(local: true), "P1");
        var selected = await SelectLocation(state);
        var done = await OfficialGraveyardRecastTests.Top(Protect(selected.State, "D", prevention));
        AssertLocationDamage(done, 0, prevention == "global" ? 0 : 2);
        Assert.Contains("D", done.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, done.State.CardObjects["D"].Damage);
        Restore(done.State);
    }

    [Theory]
    [InlineData("global")]
    [InlineData("next")]
    [InlineData("immune")]
    public async Task ConquestBonusRetainsExistingSpellSkillAndUnitPrevention(string prevention)
    {
        var selected = await SelectConquest(Annie(ConquestPosition(), "P1"));
        var done = await OfficialGraveyardRecastTests.Top(Protect(selected.State, "ENEMY", prevention));
        var damage = Assert.Single(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Equal(0, damage.Payload["damage"]);
        Assert.Equal(6, damage.Payload["preventedDamage"]);
        Assert.Equal(0, done.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("ENEMY", done.State.PlayerZones["P2"].Base);
        Restore(done.State);
    }

    internal static MatchState LocationPosition(string printing = "UNL-118/219", bool local = false)
    {
        var state = ElderDragonTests.Position(printing);
        var field = state.ObjectLocations["D"].BattlefieldObjectId!;
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [field] = state.CardObjects[field] with { CardNo = local ? "OGN·296/298" : "OGN·278/298" } } };
    }

    internal static async Task<ResolutionResult> SelectLocation(MatchState state, string printing = "UNL-118/219")
    {
        var played = await Act(state, "P1", new PlayCardCommand("ELDER", printing, []));
        var selected = await ElderDragonTests.Select(played.State, "D", "E");
        Restore(selected.State);
        return selected;
    }

    internal static MatchState ConquestPosition(string printing = "SFD·116/221", int power = 5)
    {
        var state = UncontrolledConquestTests.Position(printing);
        // The target is in a base. A Void Gate at the source's battlefield
        // must not add a target-local bonus to that base target.
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { Power = power },
            ["BF"] = state.CardObjects["BF"] with { CardNo = "OGN·296/298" } } };
    }

    internal static async Task<ResolutionResult> SelectConquest(MatchState state)
    {
        var conquered = await ConquestLifecycleRegressionTests.Conquer(state);
        var selected = await UncontrolledConquestTests.Choose(conquered.State);
        Restore(selected.State);
        return selected;
    }

    private static MatchState Protect(MatchState state, string target, string prevention)
        => prevention == "global" ? state with { UntilEndOfTurnEffects = [..state.UntilEndOfTurnEffects, "PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN"] }
            : state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [target] = prevention == "next" ? state.CardObjects[target] with { UntilEndOfTurnEffects = ["PREVENT_NEXT_DAMAGE_THIS_TURN"] }
                    : state.CardObjects[target] with { CardNo = "OGN·189/298", MovementCountThisTurn = 2 } } };

    private static void AssertLocationDamage(ResolutionResult result, int fieldDamage, int baseDamage)
    {
        var events = result.Events.Where(e => e.Kind == "DAMAGE_APPLIED").ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal(fieldDamage, Assert.Single(events, e => Equals(e.Payload["targetObjectId"], "D")).Payload["damage"]);
        Assert.Equal(baseDamage, Assert.Single(events, e => Equals(e.Payload["targetObjectId"], "E")).Payload["damage"]);
        Assert.Equal(fieldDamage > 0, result.State.PlayerZones["P2"].Graveyard.Contains("D"));
        Assert.Equal(baseDamage > 0, result.State.PlayerZones["P2"].Graveyard.Contains("E"));
    }
}
