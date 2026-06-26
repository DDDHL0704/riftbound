using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class WiseElderSourceObjectFilteredPowerTests
{
    private const string WiseElderObjectId = "P1-WISE-ELDER";
    private const string WiseElderCardNo = "OGN·065/298";
    private const string BattlefieldObjectId = "P1-WISE-ELDER-BATTLEFIELD";
    private const string DefenderObjectId = "P2-WISE-ELDER-DEFENDER";

    [Fact]
    public void WiseElderBoonStaticPowerProjectsSourceObjectFilteredAura()
    {
        var state = BuildState(hasBoon: true);

        var staticAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, WiseElderObjectId, StringComparison.Ordinal));

        Assert.Equal("STATIC_AURA:SOURCE_OBJECT_FILTERED_POWER:P1-WISE-ELDER", staticAura.EffectId);
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", staticAura.Duration);
        Assert.Equal(WiseElderObjectId, staticAura.TargetObjectId);
        Assert.Equal(WiseElderObjectId, staticAura.SourceObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(4, staticAura.BasePower);
        Assert.Equal(5, staticAura.EffectivePower);
        Assert.Equal(StaticAuraKinds.SourceObjectFilteredPower, staticAura.EffectKind);
        Assert.Equal(WiseElderCardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSourceObjectFilteredPowerBonus", staticAura.SourcePath);
        Assert.True(staticAura.IsLayerEngineFoundationOnly);
        Assert.Equal("SOURCE_PUBLIC_FIELD_UNIT_MATCHES_FILTER", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_SOURCE_OBJECT_TAGS", staticAura.Lifecycle);
        Assert.Equal([WiseElderObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([WiseElderObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([WiseElderObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([WiseElderObjectId], staticAura.ParticipantDependencyObjectIds);
    }

    [Fact]
    public void WiseElderWithoutBoonDoesNotProjectSourceObjectFilteredAura()
    {
        var state = BuildState(hasBoon: false);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceObjectFilteredPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, WiseElderObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WiseElderBoonStaticPowerAppliesToBattleDamage()
    {
        var state = BuildBattleState(hasBoon: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-wise-elder-boon-source-filtered-static-power", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [WiseElderObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var damageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, WiseElderObjectId, StringComparison.Ordinal));
        Assert.Equal(4, damageEvent.Payload["basePower"]);
        Assert.Equal(1, damageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(5, damageEvent.Payload["combatPower"]);
        Assert.Equal(5, damageEvent.Payload["damage"]);
    }

    private static MatchState BuildState(bool hasBoon)
    {
        string[] wiseTags = hasBoon
            ? [CardObjectTags.UnitCard, CardObjectTags.Boon]
            : [CardObjectTags.UnitCard];

        return new MatchState(
            "wise-elder-source-object-filtered-power-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [WiseElderObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [WiseElderObjectId] = new(
                    WiseElderObjectId,
                    cardNo: WiseElderCardNo,
                    power: 4,
                    tags: wiseTags,
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static MatchState BuildBattleState(bool hasBoon)
    {
        string[] wiseTags = hasBoon
            ? [CardObjectTags.UnitCard, CardObjectTags.Boon]
            : [CardObjectTags.UnitCard];

        return new MatchState(
            "wise-elder-source-object-filtered-power-battle-room",
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
                    Battlefields = [BattlefieldObjectId, WiseElderObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BattlefieldObjectId] = Battlefield(BattlefieldObjectId, "P1"),
                [WiseElderObjectId] = new(
                    WiseElderObjectId,
                    cardNo: WiseElderCardNo,
                    power: 4,
                    tags: wiseTags,
                    ownerId: "P1",
                    controllerId: "P1"),
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
                [WiseElderObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)]);
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
