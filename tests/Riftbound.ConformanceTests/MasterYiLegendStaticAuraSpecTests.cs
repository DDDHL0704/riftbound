using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MasterYiLegendStaticAuraSpecTests
{
    private const string LegendObjectId = "P1-LEGEND-MASTER-YI-LEVEL";
    private const string FriendlyUnitObjectId = "P1-BASE-FRIENDLY-UNIT";
    private const string EnemyUnitObjectId = "P2-BASE-ENEMY-UNIT";

    [Fact]
    public void MasterYiLevelFriendlyUnitsPowerAuraProjectsFromLegendSourceAtSixExperience()
    {
        var state = BuildState(playerOneExperience: 6);

        var staticAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal));
        Assert.Equal(FriendlyUnitObjectId, staticAura.TargetObjectId);
        Assert.Equal(LegendObjectId, staticAura.SourceObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(2, staticAura.BasePower);
        Assert.Equal(3, staticAura.EffectivePower);
        Assert.Equal("CoreRuleEngine.ResolveFriendlyUnitsPowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_PUBLIC_STATIC_AURA_AND_FRIENDLY_PUBLIC_UNITS", staticAura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        Assert.Equal([FriendlyUnitObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([LegendObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([FriendlyUnitObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyUnitObjectId], staticAura.ParticipantDependencyObjectIds);
        Assert.True(staticAura.SourceOrder.HasValue);

        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1View = AssertSnapshotFriendlyUnitsPowerAura(session.SnapshotFor("P1"));
        var p2View = AssertSnapshotFriendlyUnitsPowerAura(session.SnapshotFor("P2"));
        Assert.Equal(p1View["effectId"], p2View["effectId"]);
        Assert.Equal(p1View["sourceDependencyObjectIds"], p2View["sourceDependencyObjectIds"]);
        Assert.Equal(p1View["participantObjectIds"], p2View["participantObjectIds"]);
    }

    [Fact]
    public void MasterYiLevelFriendlyUnitsPowerAuraDoesNotProjectBelowSixExperience()
    {
        var state = BuildState(playerOneExperience: 5);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal));

        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.DoesNotContain(
            SnapshotContinuousEffects(session.SnapshotFor("P1")),
            effect => string.Equals(effect["effectKind"] as string, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal));
        Assert.DoesNotContain(
            SnapshotContinuousEffects(session.SnapshotFor("P2")),
            effect => string.Equals(effect["effectKind"] as string, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal));
    }

    private static Dictionary<string, object?> AssertSnapshotFriendlyUnitsPowerAura(SnapshotDto snapshot)
    {
        var effect = Assert.Single(
            SnapshotContinuousEffects(snapshot),
            candidate => string.Equals(candidate["effectKind"] as string, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal));

        Assert.Equal(ContinuousEffectLayers.StaticAura, effect["layer"]);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", effect["duration"]);
        Assert.Equal(FriendlyUnitObjectId, effect["targetObjectId"]);
        Assert.Equal(LegendObjectId, effect["sourceObjectId"]);
        Assert.Equal("UNL-191/219", effect["sourceCardNo"]);
        Assert.Equal(1, Assert.IsType<int>(effect["powerDelta"]));
        Assert.Equal(2, Assert.IsType<int>(effect["basePower"]));
        Assert.Equal(3, Assert.IsType<int>(effect["effectivePower"]));
        Assert.Equal("CoreRuleEngine.ResolveFriendlyUnitsPowerBonus", effect["sourcePath"]);
        Assert.Equal("SOURCE_PUBLIC_STATIC_AURA_AND_FRIENDLY_PUBLIC_UNITS", effect["condition"]);
        Assert.Equal("DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS", effect["lifecycle"]);
        Assert.Equal([FriendlyUnitObjectId], Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["participantObjectIds"]));
        Assert.Equal([LegendObjectId], Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["sourceDependencyObjectIds"]));
        Assert.Equal([FriendlyUnitObjectId], Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["targetDependencyObjectIds"]));
        Assert.Equal([FriendlyUnitObjectId], Assert.IsAssignableFrom<IReadOnlyList<string>>(effect["participantDependencyObjectIds"]));
        Assert.IsType<int>(effect["sourceOrder"]);
        return effect;
    }

    private static IReadOnlyList<Dictionary<string, object?>> SnapshotContinuousEffects(SnapshotDto snapshot)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
    }

    private static MatchState BuildState(int playerOneExperience)
    {
        return new MatchState(
            "master-yi-level-static-aura-spec-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [FriendlyUnitObjectId],
                    LegendZone = [LegendObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [EnemyUnitObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = playerOneExperience,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [FriendlyUnitObjectId] = new(
                    FriendlyUnitObjectId,
                    cardNo: "SFD·125/221",
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                [LegendObjectId] = new(
                    LegendObjectId,
                    cardNo: "UNL-191/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                [EnemyUnitObjectId] = new(
                    EnemyUnitObjectId,
                    cardNo: "SFD·125/221",
                    ownerId: "P2",
                    controllerId: "P2",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            });
    }
}
