using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReliableSiegeDogSameLocationStaticPowerTests
{
    private const string SourceObjectId = "P1-RELIABLE-SIEGE-DOG";
    private const string SourceCardNo = "SFD·159/221";
    private const string FriendlyObjectId = "P1-FRIENDLY-SAME-LOCATION";
    private const string SecondFriendlyObjectId = "P1-FRIENDLY-SAME-LOCATION-2";

    [Fact]
    public void ReliableSiegeDogProjectsSameLocationOtherFriendlyStaticPowerAtBase()
    {
        var state = BuildBaseState(includeFriendlySameBase: true);

        var staticAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));

        Assert.Equal("STATIC_AURA:SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER:P1-RELIABLE-SIEGE-DOG", staticAura.EffectId);
        Assert.Equal("OBJECT", staticAura.Scope);
        Assert.Equal("WHILE_SOURCE_ON_PUBLIC_FIELD", staticAura.Duration);
        Assert.Equal(SourceObjectId, staticAura.TargetObjectId);
        Assert.Equal(SourceObjectId, staticAura.SourceObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(2, staticAura.BasePower);
        Assert.Equal(3, staticAura.EffectivePower);
        Assert.Equal(SourceCardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSourceSameLocationOtherFriendlyUnitPowerBonus", staticAura.SourcePath);
        Assert.True(staticAura.IsLayerEngineFoundationOnly);
        Assert.Equal("SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_LOCATION", staticAura.Condition);
        Assert.Equal("RECOMPUTED_FROM_CURRENT_SAME_LOCATION_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        Assert.Equal([FriendlyObjectId], staticAura.ParticipantObjectIds);
        Assert.Equal([SourceObjectId], staticAura.SourceDependencyObjectIds);
        Assert.Equal([SourceObjectId], staticAura.TargetDependencyObjectIds);
        Assert.Equal([FriendlyObjectId], staticAura.ParticipantDependencyObjectIds);
    }

    [Fact]
    public void ReliableSiegeDogOmitsSameLocationAuraWithoutOtherFriendlyUnit()
    {
        var state = BuildBaseState(includeFriendlySameBase: false);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReliableSiegeDogCountsSameBattlefieldOtherFriendlyUnitForBattlePower()
    {
        var state = BuildBattleState(includeFriendlySameBattlefield: true);

        var staticAura = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal([FriendlyObjectId, SecondFriendlyObjectId], staticAura.ParticipantObjectIds);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reliable-siege-dog-same-location-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD",
                [SourceObjectId],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);
    }

    [Fact]
    public async Task ReliableSiegeDogSkipsBattlePowerWhenOtherFriendlyUnitIsAtDifferentLocation()
    {
        var state = BuildBattleState(includeFriendlySameBattlefield: false);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reliable-siege-dog-no-same-location-static-power", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "P1-BATTLEFIELD",
                [SourceObjectId],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.False(attackerDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["damage"]);
    }

    private static MatchState BuildBaseState(bool includeFriendlySameBase)
    {
        var p1Base = includeFriendlySameBase
            ? new[] { SourceObjectId, FriendlyObjectId }
            : [SourceObjectId];

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [SourceObjectId] = new("P1", "BASE")
        };
        if (includeFriendlySameBase)
        {
            objectLocations[FriendlyObjectId] = new("P1", "BASE");
        }
        else
        {
            objectLocations["P1-FRIENDLY-DIFFERENT-LOCATION"] =
                new("P1", "BATTLEFIELD", "P1-OTHER-BATTLEFIELD");
        }

        return BuildNeutralState(
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = p1Base,
                    Battlefields = includeFriendlySameBase
                        ? []
                        : ["P1-OTHER-BATTLEFIELD", "P1-FRIENDLY-DIFFERENT-LOCATION"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-ENEMY-SAME-BASE"]
                }
            },
            cardObjects: BuildBaseCardObjects(includeFriendlySameBase),
            objectLocations: objectLocations);
    }

    private static MatchState BuildBattleState(bool includeFriendlySameBattlefield)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD"] = Battlefield("P1-BATTLEFIELD", "P1"),
            [SourceObjectId] = SourceUnit(),
            [FriendlyObjectId] = Unit(FriendlyObjectId, "P1", 1),
            ["P1-OTHER-BATTLEFIELD"] = Battlefield("P1-OTHER-BATTLEFIELD", "P1"),
            ["P2-DEFENDER"] = Unit("P2-DEFENDER", "P2", 10),
            ["P2-ENEMY-SAME-BATTLEFIELD"] = Unit("P2-ENEMY-SAME-BATTLEFIELD", "P2", 1)
        };
        if (includeFriendlySameBattlefield)
        {
            cardObjects[SecondFriendlyObjectId] = Unit(SecondFriendlyObjectId, "P1", 1);
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD"),
            [SourceObjectId] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD"),
            [FriendlyObjectId] = includeFriendlySameBattlefield
                ? new("P1", "BATTLEFIELD", "P1-BATTLEFIELD")
                : new("P1", "BATTLEFIELD", "P1-OTHER-BATTLEFIELD"),
            ["P1-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-OTHER-BATTLEFIELD"),
            ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD"),
            ["P2-ENEMY-SAME-BATTLEFIELD"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD")
        };
        if (includeFriendlySameBattlefield)
        {
            objectLocations[SecondFriendlyObjectId] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD");
        }

        return BuildNeutralState(
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = includeFriendlySameBattlefield
                        ? ["P1-BATTLEFIELD", SourceObjectId, FriendlyObjectId, SecondFriendlyObjectId, "P1-OTHER-BATTLEFIELD"]
                        : ["P1-BATTLEFIELD", SourceObjectId, "P1-OTHER-BATTLEFIELD", FriendlyObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DEFENDER", "P2-ENEMY-SAME-BATTLEFIELD"]
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations,
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD")]);
    }

    private static IReadOnlyDictionary<string, CardObjectState> BuildBaseCardObjects(bool includeFriendlySameBase)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [SourceObjectId] = SourceUnit(),
            ["P2-ENEMY-SAME-BASE"] = Unit("P2-ENEMY-SAME-BASE", "P2", 1)
        };
        if (includeFriendlySameBase)
        {
            cardObjects[FriendlyObjectId] = Unit(FriendlyObjectId, "P1", 1);
        }
        else
        {
            cardObjects["P1-OTHER-BATTLEFIELD"] = Battlefield("P1-OTHER-BATTLEFIELD", "P1");
            cardObjects["P1-FRIENDLY-DIFFERENT-LOCATION"] =
                Unit("P1-FRIENDLY-DIFFERENT-LOCATION", "P1", 1);
        }

        return cardObjects;
    }

    private static MatchState BuildNeutralState(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyList<string>? untilEndOfTurnEffects = null)
    {
        return new MatchState(
            "reliable-siege-dog-static-power-room",
            tick: 1,
            turnNumber: 7,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects,
            objectLocations: objectLocations,
            untilEndOfTurnEffects: untilEndOfTurnEffects);
    }

    private static CardObjectState SourceUnit()
    {
        return new CardObjectState(
            SourceObjectId,
            cardNo: SourceCardNo,
            power: 2,
            tags: [CardObjectTags.UnitCard, "精锐", "犬形"],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Unit(string objectId, string playerId, int power)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
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
