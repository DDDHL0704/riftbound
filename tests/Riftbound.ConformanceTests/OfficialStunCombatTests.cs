using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 423.1.b/c: stun removes damage contribution, never the unit's actual might.
public sealed class OfficialStunCombatTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void StunnedUnitKeepsItsLethalThresholdAndPublicMight(int existingDamage)
    {
        var state = Position(true, existingDamage);
        var candidate = ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.AssignCombatDamage);
        var metadata = candidate.Metadata!;
        var pool = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["damagePool"]);
        var lethal = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["lethalDamageThreshold"]);
        Assert.Equal(0, pool["A1"]);
        Assert.Equal(5 - existingDamage, lethal["A1"]);
        var participants = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["battleParticipants"]);
        Assert.Equal(5, participants.Single(p => Equals(p["objectId"], "A1"))["power"]);
    }

    [Fact]
    public async Task SubmittedDamageMustReachStunnedBulwarkBeforeAnotherUnit()
    {
        var engine = new CoreRuleEngine();
        var state = Position(true);
        var first = await engine.ResolveAsync(state, new("stun-attack", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand("battle:BF", "BF", [new("A2", "D", 3)]), default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var invalid = await engine.ResolveAsync(first.State, new("stun-skip-wall", "P2", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand("battle:BF", "BF", [new("D", "A2", 3)]), default);
        Assert.False(invalid.Accepted);
        Assert.Equal(MatchStateHasher.Hash(first.State), MatchStateHasher.Hash(invalid.State));
        var result = await engine.ResolveAsync(first.State, new("stun-defend", "P2", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand("battle:BF", "BF", [new("D", "A1", 3)]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        AssertSurvivors(result);
    }

    [Fact]
    public async Task AutomaticDamageDoesNotSkipStunnedBulwark()
    {
        var result = await new CoreRuleEngine().ResolveAsync(Position(false), new("stun-auto", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand("BF", ["A1", "A2"], ["D"], ["COMBAT_ASSIGNMENT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        AssertSurvivors(result);
    }

    private static void AssertSurvivors(ResolutionResult result)
    {
        Assert.Contains("A1", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("A2", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("D", result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(result.Events, e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload["sourceObjectId"], "A1"));
        Assert.Contains(result.Events, e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload["sourceObjectId"], "D") && Equals(e.Payload["targetObjectId"], "A1") && Equals(e.Payload["damage"], 3));
    }

    private static MatchState Position(bool ongoing, int damage = 0) => new("STUN-COMBAT", 12, 3, "P1",
        new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
        status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
        playerZones: new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Battlefields = ["A1", "A2"] },
            ["P2"] = PlayerZones.Empty with { Battlefields = ["BF", "D"] }
        }, cardObjects: new Dictionary<string, CardObjectState>
        {
            ["BF"] = new("BF", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2"),
            ["A1"] = new("A1", power: 5, damage: damage, cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1",
                isAttacking: ongoing, tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark], untilEndOfTurnEffects: ["STUNNED"]),
            ["A2"] = new("A2", power: 3, cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", isAttacking: ongoing, tags: [CardObjectTags.UnitCard]),
            ["D"] = new("D", power: 3, cardNo: "SFD·125/221", ownerId: "P2", controllerId: "P2", isDefending: ongoing, tags: [CardObjectTags.UnitCard])
        }, objectLocations: new Dictionary<string, ObjectLocationState>
        {
            ["BF"] = new("P2", "BATTLEFIELD", "BF"), ["A1"] = new("P1", "BATTLEFIELD", "BF"),
            ["A2"] = new("P1", "BATTLEFIELD", "BF"), ["D"] = new("P2", "BATTLEFIELD", "BF")
        }, untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF")]);
}
