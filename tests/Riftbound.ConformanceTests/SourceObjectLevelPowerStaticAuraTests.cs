using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SourceObjectLevelPowerStaticAuraTests
{
    private const string CrystalhandHunterObjectId = "P1-CRYSTALHAND-HUNTER";
    private const string CrystalhandHunterCardNo = "UNL-094/219";
    private const string BattlefieldObjectId = "P1-CRYSTALHAND-HUNTER-BATTLEFIELD";
    private const string DefenderObjectId = "P2-CRYSTALHAND-HUNTER-DEFENDER";
    private const string MossStepperObjectId = "P1-MOSS-STEPPER-MATERIALIZED";
    private const string MossStepperCardNo = "UNL-047/219";
    private const string MossStepperBattlefieldObjectId = "P1-MOSS-STEPPER-BATTLEFIELD";
    private const string MossStepperDefenderObjectId = "P2-MOSS-STEPPER-DEFENDER";

    [Fact]
    public void CrystalhandHunterLevelStaticPowerProjectsSourceObjectAuraAtRequiredExperience()
    {
        var state = BuildState(playerOneExperience: 6);

        var staticAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, CrystalhandHunterObjectId, StringComparison.Ordinal));

        Assert.Equal("STATIC_AURA:SOURCE_OBJECT_POWER:P1-CRYSTALHAND-HUNTER", staticAura.EffectId);
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", staticAura.Duration);
        Assert.Equal(CrystalhandHunterObjectId, staticAura.TargetObjectId);
        Assert.Equal(CrystalhandHunterObjectId, staticAura.SourceObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(2, staticAura.BasePower);
        Assert.Equal(3, staticAura.EffectivePower);
        Assert.Equal(StaticAuraKinds.SourceObjectPower, staticAura.EffectKind);
        Assert.Equal(CrystalhandHunterCardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSourceObjectPowerBonus", staticAura.SourcePath);
        Assert.True(staticAura.IsLayerEngineFoundationOnly);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_AND_CONTROLLER_EXPERIENCE", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_CONTROLLER_EXPERIENCE", staticAura.Lifecycle);
        Assert.Equal([CrystalhandHunterObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([CrystalhandHunterObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([CrystalhandHunterObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([CrystalhandHunterObjectId], staticAura.ParticipantDependencyObjectIds);
    }

    [Fact]
    public void CrystalhandHunterLevelStaticPowerDoesNotProjectBelowRequiredExperience()
    {
        var state = BuildState(playerOneExperience: 5);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, CrystalhandHunterObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrystalhandHunterLevelStaticPowerAppliesToBattleDamage()
    {
        var state = BuildBattleState(playerOneExperience: 6);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-crystalhand-hunter-level-source-static-power", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [CrystalhandHunterObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var damageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, CrystalhandHunterObjectId, StringComparison.Ordinal));
        Assert.Equal(2, damageEvent.Payload["basePower"]);
        Assert.Equal(1, damageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, damageEvent.Payload["combatPower"]);
        Assert.Equal(3, damageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task MaterializedLevelStaticPowerDoesNotDoubleCountBattleDamage()
    {
        var state = BuildMaterializedMossStepperBattleState();

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, MossStepperObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-moss-stepper-materialized-level-source-static-power", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                MossStepperBattlefieldObjectId,
                [MossStepperObjectId],
                [MossStepperDefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var damageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, MossStepperObjectId, StringComparison.Ordinal));
        Assert.Equal(4, damageEvent.Payload["basePower"]);
        Assert.False(damageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(4, damageEvent.Payload["combatPower"]);
        Assert.Equal(4, damageEvent.Payload["damage"]);
    }

    private static MatchState BuildState(int playerOneExperience)
    {
        return new MatchState(
            "source-object-level-power-static-aura-room",
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
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = playerOneExperience,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [CrystalhandHunterObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [CrystalhandHunterObjectId] = CrystalhandHunter(CrystalhandHunterObjectId)
            });
    }

    private static MatchState BuildBattleState(int playerOneExperience)
    {
        return new MatchState(
            "source-object-level-power-static-aura-battle-room",
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
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = playerOneExperience,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldObjectId, CrystalhandHunterObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BattlefieldObjectId] = Battlefield(BattlefieldObjectId, "P1"),
                [CrystalhandHunterObjectId] = CrystalhandHunter(CrystalhandHunterObjectId),
                [DefenderObjectId] = new(
                    DefenderObjectId,
                    cardNo: "SFD·125/221",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [CrystalhandHunterObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)]);
    }

    private static MatchState BuildMaterializedMossStepperBattleState()
    {
        return new MatchState(
            "source-object-level-power-materialized-battle-room",
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
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 3,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [MossStepperBattlefieldObjectId, MossStepperObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [MossStepperDefenderObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [MossStepperBattlefieldObjectId] = Battlefield(MossStepperBattlefieldObjectId, "P1"),
                [MossStepperObjectId] = new(
                    MossStepperObjectId,
                    cardNo: MossStepperCardNo,
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "犬形", "狩猎2", CardObjectTags.Spellshield],
                    ownerId: "P1",
                    controllerId: "P1"),
                [MossStepperDefenderObjectId] = new(
                    MossStepperDefenderObjectId,
                    cardNo: "SFD·125/221",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [MossStepperBattlefieldObjectId] = new("P1", "BATTLEFIELD", MossStepperBattlefieldObjectId),
                [MossStepperObjectId] = new("P1", "BATTLEFIELD", MossStepperBattlefieldObjectId),
                [MossStepperDefenderObjectId] = new("P2", "BATTLEFIELD", MossStepperBattlefieldObjectId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(MossStepperBattlefieldObjectId)]);
    }

    private static CardObjectState CrystalhandHunter(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: CrystalhandHunterCardNo,
            power: 2,
            tags: [CardObjectTags.UnitCard, "约德尔人", "狩猎"],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Battlefield(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·275/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
