using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SourceCombatStaticAuraProjectionTests
{
    private const string BattlefieldObjectId = "P1-COMBAT-AURA-BATTLEFIELD";
    private const string DefenderObjectId = "P2-COMBAT-AURA-DEFENDER";

    [Fact]
    public void ScarletPigeonAttackingWithAnotherUnitProjectsSourceCombatStaticAura()
    {
        const string sourceObjectId = "P1-SCARLET-PIGEON";
        const string allyObjectId = "P1-SCARLET-ALLY";
        var state = BuildBattleState(
            sourceObjectId,
            "UNL-154/219",
            sourcePower: 3,
            sourceControllerId: "P1",
            sourceIsAttacking: true,
            sourceIsDefending: false,
            extraP1BattlefieldUnits:
            [
                Unit(allyObjectId, "P1", 2, isAttacking: true)
            ]);

        var aura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceAttackingWithAnotherUnitPower, StringComparison.Ordinal));

        AssertSourceCombatAura(
            aura,
            "STATIC_AURA:SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER:P1-SCARLET-PIGEON",
            sourceObjectId,
            "UNL-154/219",
            "CoreRuleEngine.ResolveSourceAttackingWithAnotherUnitPowerBonus",
            "SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT",
            "RECOMPUTED_FROM_CURRENT_BATTLE_ATTACKER_LOCATIONS",
            basePower: 3,
            powerDelta: 2,
            effectivePower: 5,
            participantObjectIds: [allyObjectId, sourceObjectId]);
    }

    [Fact]
    public void ScarletPigeonSourceCombatStaticAuraIgnoresStandbyAttackerParticipants()
    {
        const string sourceObjectId = "P1-SCARLET-PIGEON";
        var state = BuildBattleState(
            sourceObjectId,
            "UNL-154/219",
            sourcePower: 3,
            sourceControllerId: "P1",
            sourceIsAttacking: true,
            sourceIsDefending: false,
            extraP1BattlefieldUnits:
            [
                Unit("P1-STANDBY-ATTACKER", "P1", 2, isAttacking: true, isStandby: true)
            ]);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceAttackingWithAnotherUnitPower, StringComparison.Ordinal));
    }

    [Fact]
    public void WaterbenderDefendingAloneProjectsSourceCombatStaticAura()
    {
        const string sourceObjectId = "P2-WATERBENDER";
        var state = BuildBattleState(
            sourceObjectId,
            "OGN·055/298",
            sourcePower: 2,
            sourceControllerId: "P2",
            sourceIsAttacking: false,
            sourceIsDefending: true,
            extraP1BattlefieldUnits:
            [
                Unit("P1-WATERBENDER-ATTACKER", "P1", 7, isAttacking: true)
            ],
            includeDefaultDefender: false);

        var aura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceLoneBattlePower, StringComparison.Ordinal));

        AssertSourceCombatAura(
            aura,
            "STATIC_AURA:SOURCE_LONE_BATTLE_POWER:P2-WATERBENDER",
            sourceObjectId,
            "OGN·055/298",
            "CoreRuleEngine.ResolveSourceLoneBattlePowerBonus",
            "SOURCE_ATTACKING_OR_DEFENDING_ALONE",
            "RECOMPUTED_FROM_CURRENT_BATTLE_PARTICIPANT_LOCATIONS",
            basePower: 2,
            powerDelta: 2,
            effectivePower: 4,
            participantObjectIds: ["P1-WATERBENDER-ATTACKER", sourceObjectId]);
    }

    [Fact]
    public void DuneDrakeAttackingReadyEnemyUnitProjectsSourceCombatStaticAura()
    {
        const string sourceObjectId = "P1-DUNE-DRAKE";
        var state = BuildBattleState(
            sourceObjectId,
            "OGN·131/298",
            sourcePower: 5,
            sourceControllerId: "P1",
            sourceIsAttacking: true,
            sourceIsDefending: false);

        var aura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceAttackingReadyEnemyUnitPower, StringComparison.Ordinal));

        AssertSourceCombatAura(
            aura,
            "STATIC_AURA:SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER:P1-DUNE-DRAKE",
            sourceObjectId,
            "OGN·131/298",
            "CoreRuleEngine.ResolveSourceAttackingReadyEnemyUnitPowerBonus",
            "SOURCE_ATTACKING_AND_READY_ENEMY_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
            "RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_READY_ENEMY_UNIT_LOCATIONS",
            basePower: 5,
            powerDelta: 2,
            effectivePower: 7,
            participantObjectIds: [DefenderObjectId]);
    }

    [Fact]
    public void DuneDrakeSourceCombatStaticAuraIgnoresStandbyReadyDefenderParticipants()
    {
        const string sourceObjectId = "P1-DUNE-DRAKE";
        var state = BuildBattleState(
            sourceObjectId,
            "OGN·131/298",
            sourcePower: 5,
            sourceControllerId: "P1",
            sourceIsAttacking: true,
            sourceIsDefending: false,
            defaultDefenderIsStandby: true);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceAttackingReadyEnemyUnitPower, StringComparison.Ordinal));
    }

    private static void AssertSourceCombatAura(
        ContinuousEffectState aura,
        string effectId,
        string sourceObjectId,
        string sourceCardNo,
        string sourcePath,
        string condition,
        string lifecycle,
        int basePower,
        int powerDelta,
        int effectivePower,
        IReadOnlyList<string> participantObjectIds)
    {
        Assert.Equal(effectId, aura.EffectId);
        Assert.Equal("OBJECT", aura.Scope);
        Assert.Equal(ContinuousEffectLayers.StaticAura, aura.Layer);
        Assert.Equal(sourceObjectId, aura.TargetObjectId);
        Assert.Equal(sourceObjectId, aura.SourceObjectId);
        Assert.Equal(powerDelta, aura.PowerDelta);
        Assert.Equal(basePower, aura.BasePower);
        Assert.Equal(effectivePower, aura.EffectivePower);
        Assert.Equal(sourceCardNo, aura.SourceCardNo);
        Assert.Equal(sourcePath, aura.SourcePath);
        Assert.Equal(condition, aura.Condition);
        Assert.Equal(lifecycle, aura.Lifecycle);
        Assert.Equal(participantObjectIds, aura.ParticipantObjectIds);
        Assert.Equal([sourceObjectId], aura.SourceDependencyObjectIds);
        Assert.Equal([sourceObjectId], aura.TargetDependencyObjectIds);
        Assert.Equal(participantObjectIds, aura.ParticipantDependencyObjectIds);
        Assert.True(aura.SourceOrder.HasValue);
    }

    private static MatchState BuildBattleState(
        string sourceObjectId,
        string sourceCardNo,
        int sourcePower,
        string sourceControllerId,
        bool sourceIsAttacking,
        bool sourceIsDefending,
        IReadOnlyList<CardObjectState>? extraP1BattlefieldUnits = null,
        bool includeDefaultDefender = true,
        bool defaultDefenderIsStandby = false)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [sourceObjectId] = Unit(
                sourceObjectId,
                sourceControllerId,
                sourcePower,
                sourceCardNo,
                sourceIsAttacking,
                sourceIsDefending)
        };

        if (includeDefaultDefender)
        {
            cardObjects[DefenderObjectId] = Unit(DefenderObjectId, "P2", 10, isDefending: true, isStandby: defaultDefenderIsStandby);
        }

        foreach (var unit in extraP1BattlefieldUnits ?? [])
        {
            cardObjects[unit.ObjectId] = unit;
        }

        var p1Battlefields = cardObjects
            .Where(entry => string.Equals(entry.Value.ControllerId, "P1", StringComparison.Ordinal)
                || string.Equals(entry.Key, BattlefieldObjectId, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        var p2Battlefields = cardObjects
            .Where(entry => string.Equals(entry.Value.ControllerId, "P2", StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        var objectLocations = cardObjects.ToDictionary(
            entry => entry.Key,
            entry => new ObjectLocationState(
                string.Equals(entry.Value.ControllerId, "P2", StringComparison.Ordinal) ? "P2" : "P1",
                "BATTLEFIELD",
                BattlefieldObjectId),
            StringComparer.Ordinal);

        return new MatchState(
            "source-combat-static-aura-projection-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        string cardNo = "SFD·125/221",
        bool isAttacking = false,
        bool isDefending = false,
        bool isStandby = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isAttacking: isAttacking,
            isDefending: isDefending,
            isExhausted: false,
            tags: isStandby ? [CardObjectTags.UnitCard, CardObjectTags.Standby] : [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }
}
