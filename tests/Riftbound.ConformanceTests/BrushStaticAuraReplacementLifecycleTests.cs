using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BrushStaticAuraReplacementLifecycleTests
{
    private const string BrushBattlefieldObjectId = "P2-BRUSH-LIFECYCLE-BATTLEFIELD";
    private const string OriginalBattlefieldObjectId = "P2-BRUSH-LIFECYCLE-ENERGY-HUB";
    private const string AttackerObjectId = "P1-BRUSH-LIFECYCLE-BIRD-ATTACKER";
    private const string DefenderObjectId = "P2-BRUSH-LIFECYCLE-CAT-DEFENDER";

    [Fact]
    public async Task BrushStaticAuraPersistsThroughScoreReplacementChoiceDoesNotTriggerOnDefensiveVictory()
    {
        var state = BuildState();
        var staticAuras = state.ContinuousEffects
            .Where(effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, BrushBattlefieldObjectId, StringComparison.Ordinal))
            .OrderBy(effect => effect.TargetObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, staticAuras.Length);
        Assert.Equal([AttackerObjectId, DefenderObjectId], staticAuras.Select(effect => Assert.IsType<string>(effect.TargetObjectId)).ToArray());
        Assert.All(
            staticAuras,
            effect =>
            {
                Assert.Equal("BATTLEFIELD", effect.Scope);
                Assert.Equal(1, effect.PowerDelta);
                Assert.Equal(StaticAuraKinds.BattlefieldFilteredUnitsPower, effect.EffectKind);
                Assert.Equal(P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo, effect.SourceCardNo);
                Assert.Equal("CoreRuleEngine.ResolveBattlefieldFilteredUnitsPowerBonus", effect.SourcePath);
                Assert.Equal("SOURCE_BATTLEFIELD_FILTERED_UNITS_POWER_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD", effect.Condition);
                Assert.Equal("DERIVED_FROM_CURRENT_BATTLEFIELD_FILTERED_OBJECT_LOCATIONS", effect.Lifecycle);
                Assert.Equal([AttackerObjectId, DefenderObjectId], effect.ParticipantObjectIds);
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-brush-static-aura-replacement-lifecycle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BrushBattlefieldObjectId,
                [AttackerObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    private static MatchState BuildState()
    {
        return new MatchState(
            "brush-static-aura-replacement-lifecycle-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(0, 4)
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [AttackerObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [BrushBattlefieldObjectId, OriginalBattlefieldObjectId, DefenderObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [AttackerObjectId] = new(
                    AttackerObjectId,
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "鸟类"],
                    ownerId: "P1",
                    controllerId: "P1"),
                [BrushBattlefieldObjectId] = new(
                    BrushBattlefieldObjectId,
                    cardNo: P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo,
                    tags:
                    [
                        P6TokenFactoryCatalog.BattlefieldCardTag,
                        "草丛",
                        $"REPLACES_BATTLEFIELD:{OriginalBattlefieldObjectId}"
                    ],
                    ownerId: "P2",
                    controllerId: "P2"),
                [OriginalBattlefieldObjectId] = new(
                    OriginalBattlefieldObjectId,
                    cardNo: "SFD·214/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                [DefenderObjectId] = new(
                    DefenderObjectId,
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "猫科"],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [AttackerObjectId] = new("P1", "BATTLEFIELD", BrushBattlefieldObjectId),
                [BrushBattlefieldObjectId] = new("P2", "BATTLEFIELD", BrushBattlefieldObjectId),
                [OriginalBattlefieldObjectId] = new("P2", "BATTLEFIELD", OriginalBattlefieldObjectId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BrushBattlefieldObjectId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BrushBattlefieldObjectId)]);
    }
}
