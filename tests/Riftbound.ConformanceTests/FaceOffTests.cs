using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.CurrentPowerTargetTests;
using static Riftbound.ConformanceTests.GroupedTargetTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class FaceOffTests
{
    internal const string Spell = "UNL-107/219";
    internal static MatchState Board(int count = 5, int power = 2)
    {
        var s = Group(count, power);
        return s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["AOE"] = s.CardObjects["AOE"] with { CardNo = Spell } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(2, 0) } };
    }
    private static Task<ResolutionResult> Resolve(MatchState s) => OfficialSettReplacementTests.Open(s, Spell, "A", "BF");

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task SelectsUnitAndBattlefieldThenMovesEveryStrictlyWeakerEnemy(int power)
    {
        var s = Board(power: power);
        Assert.True(Quote(s, "A", "BF").IsValid);
        var done = await Resolve(s);
        foreach (var id in Ids(5)) Assert.Equal(power < 4, done.State.PlayerZones["P2"].Base.Contains(id));
        Assert.Contains("A", done.State.PlayerZones["P1"].Base);
        Assert.Equal(1, done.State.PlayerExperience["P1"]);
        Assert.Equal(power < 4 ? 5 : 0, done.Events.Count(e => e.Kind == "UNIT_MOVED_TO_BASE"));
        var events = done.Events.ToList();
        Assert.True(events.FindLastIndex(e => e.Kind == "UNIT_MOVED_TO_BASE") < events.FindIndex(e => e.Kind == "EXPERIENCE_GAINED"));
        Restore(done.State);
    }

    [Theory]
    [InlineData("enemy")] [InlineData("three")] [InlineData("reverse")] [InlineData("missing")]
    public async Task RejectsForgedTargetShapeWithoutMutation(string branch)
    {
        var s = Board();
        string[] targets = branch switch { "enemy" => ["A", "D"], "three" => ["A", "D", "D2"], "reverse" => ["BF", "A"], _ => ["A", "ABSENT"] };
        Assert.False(Quote(s, targets).IsValid);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("bad", "P1", CommandTypes.PlayCard), new PlayCardCommand("AOE", Spell, targets), default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ExactBattlefieldExcludesOtherLocationAndFriendlyUnits()
    {
        var s = OtherField(Board(), "D2");
        s = At(At(s, "D3", "P2", "BASE"), "D4", "P1", "BASE");
        var done = await Resolve(s);
        Assert.All(new[]{"D", "D5"}, id => Assert.Contains(id, done.State.PlayerZones["P2"].Base));
        Assert.Equal("BF2", done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Contains("D4", done.State.PlayerZones["P1"].Base);
        Assert.Contains("A", done.State.PlayerZones["P1"].Base); Restore(done.State);
    }

    [Fact]
    public async Task EmptyBattlefieldStillGrantsExperience()
    {
        var done = await Resolve(At(Board(1), "D", "P2", "BASE"));
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        Assert.Equal(1, done.State.PlayerExperience["P1"]); Restore(done.State);
    }

    [Fact]
    public async Task AffectedWardUnitsAreNotTargetsOrAdditionalCosts()
    {
        var s = Board(2);
        s = s with { CardObjects = s.CardObjects.ToDictionary(p => p.Key, p => p.Key.StartsWith("D") ? p.Value with { Tags = [CardObjectTags.UnitCard, "法盾2"] } : p.Value) };
        var quote = Quote(s, "A", "BF"); Assert.True(quote.IsValid); Assert.True(quote.CanPay);
        var done = await Resolve(s);
        Assert.All(Ids(2), id => Assert.Contains(id, done.State.PlayerZones["P2"].Base));
        Assert.Equal(0, done.State.RunePools["P1"].Power); Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ComparisonIncludesCurrentLocalAuraAndSnapshotsWholeGroup(bool friendlyAura)
    {
        var s = Board(2, friendlyAura ? 4 : 3);
        s = DeathObserverAuditTests.Source(s, "OGS·013/024", "C-AURA", friendlyAura ? "P1" : "P2");
        s = At(s, "C-AURA", friendlyAura ? "P1" : "P2", friendlyAura ? "BASE" : "BATTLEFIELD", friendlyAura ? null : "BF");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["C-AURA"] = s.CardObjects["C-AURA"] with { Power = 1 } } };
        var done = await Resolve(s);
        foreach (var id in Ids(2)) Assert.Equal(friendlyAura, done.State.PlayerZones["P2"].Base.Contains(id));
        if (!friendlyAura) Assert.Contains("C-AURA", done.State.PlayerZones["P2"].Base);
        Restore(done.State);
    }

    [Theory]
    [InlineData("unit")] [InlineData("battlefield")]
    public async Task InvalidatedSelectedTargetSkipsMovementButIndependentExperienceResolves(string target)
    {
        var cast = await Act(Board(1), "P1", new PlayCardCommand("AOE", Spell, ["A", "BF"]));
        var id = target == "unit" ? "A" : "BF";
        // A generation change models leaving and returning; the original target no longer exists.
        var changed = cast.State with { CardObjects = new Dictionary<string, CardObjectState>(cast.State.CardObjects) {
            [id] = cast.State.CardObjects[id] with { ObjectGeneration = 1 } } };
        var done = await Top(changed);
        Assert.Contains("D", done.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, done.State.PlayerExperience["P1"]); Restore(done.State);
    }

    [Fact]
    public async Task RealPowerBindResponseChangesAffectedGroupAtResolution()
    {
        var s = Board(2, 3);
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["BUFF"] = new("BUFF", cardNo: "SFD·151/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Hand = [..s.PlayerZones["P2"].Hand, "BUFF"] } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P2"] = new(20, 20) } };
        var cast = await Act(s, "P1", new PlayCardCommand("AOE", Spell, ["A", "BF"]));
        var passed = await Act(cast.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("BUFF", "SFD·151/221", ["D", "D2"]));
        var buffed = await Top(response.State); var done = await Top(buffed.State);
        Assert.All(Ids(2), id => Assert.Contains(id, done.State.PlayerZones["P2"].Battlefields));
        Assert.Equal(1, done.State.PlayerExperience["P1"]); Restore(done.State);
    }

    [Fact]
    public async Task MoveUsesControllerBaseAndPreservesHostAndEquipmentState()
    {
        var s = Board(1);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { OwnerId = "P1", Damage = 1, IsExhausted = true, UntilEndOfTurnPowerModifier = 1 },
            ["GEAR"] = new("GEAR", ownerId: "P2", controllerId: "P2", attachedToObjectId: "D", tags: [CardObjectTags.EquipmentCard], isExhausted: true) } };
        s = At(s, "GEAR", "P2", "BATTLEFIELD", "BF");
        var done = await Resolve(s);
        Assert.All(new[]{"D", "GEAR"}, id => { Assert.Contains(id, done.State.PlayerZones["P2"].Base); Assert.Equal("BASE", done.State.ObjectLocations[id].Zone); Assert.Null(done.State.ObjectLocations[id].BattlefieldObjectId); Assert.True(done.State.CardObjects[id].IsExhausted); });
        Assert.Equal("P1", done.State.CardObjects["D"].OwnerId); Assert.Equal("P2", done.State.CardObjects["D"].ControllerId);
        Assert.Equal(1, done.State.CardObjects["D"].Damage); Assert.Equal(1, done.State.CardObjects["D"].UntilEndOfTurnPowerModifier);
        Assert.Equal("D", done.State.CardObjects["GEAR"].AttachedToObjectId); Restore(done.State);
    }

    [Fact]
    public async Task PerPlayerMovementRestrictionIsRespected()
    {
        var s = Board(2);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { UntilEndOfTurnEffects = ["MOVEMENT_PROHIBITED:P1"] } } };
        var done = await Resolve(s); Assert.Contains("D", done.State.PlayerZones["P2"].Battlefields); Assert.Contains("D2", done.State.PlayerZones["P2"].Base);
        Assert.Equal(1, done.State.PlayerExperience["P1"]); Restore(done.State);
    }
    [Fact]
    public async Task BattlefieldProhibitionPreventsMovesButNotExperience()
    {
        var s = Board(2);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["BF"] = s.CardObjects["BF"] with { CardNo = "OGN·295/298" } } };
        var done = await Resolve(s);
        Assert.All(Ids(2), id => Assert.Contains(id, done.State.PlayerZones["P2"].Battlefields));
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_MOVED_TO_BASE");
        Assert.Equal(1, done.State.PlayerExperience["P1"]); Restore(done.State);
    }

    [Theory]
    [InlineData(-2, 0)] [InlineData(0, -2)]
    public async Task NegativeMightIsReferencedAsZero(int reference, int enemy)
    {
        var s = Board(1, enemy);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["A"] = s.CardObjects["A"] with { Power = reference } } };
        var done = await Resolve(s);
        Assert.Contains("D", done.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, done.State.PlayerExperience["P1"]); Restore(done.State);
    }

    [Fact]
    public async Task AffectedGroupIsCollectedAtResolutionInsteadOfDeclaration()
    {
        var s = At(Board(2), "D2", "P2", "BASE");
        var cast = await Act(s, "P1", new PlayCardCommand("AOE", Spell, ["A", "BF"]));
        // Seed the result of a response moving a previously unaffected unit here.
        var done = await Top(At(cast.State, "D2", "P2", "BATTLEFIELD", "BF"));
        Assert.All(Ids(2), id => Assert.Contains(id, done.State.PlayerZones["P2"].Base));
        Assert.Equal(2, done.Events.Count(e => e.Kind == "UNIT_MOVED_TO_BASE")); Restore(done.State);
    }

}
