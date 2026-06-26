using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SameBattlefieldOtherFriendlyStaticPowerCardRowTests
{
    private const string BattlefieldObjectId = "P1-SAME-BATTLEFIELD-POWER-BATTLEFIELD";
    private const string SourceObjectId = "P1-SAME-BATTLEFIELD-POWER-SOURCE";
    private const string AllyObjectId = "P1-SAME-BATTLEFIELD-POWER-ALLY";
    private const string OtherBattlefieldObjectId = "P1-SAME-BATTLEFIELD-POWER-OTHER-BATTLEFIELD";
    private const string OtherBattlefieldAllyObjectId = "P1-SAME-BATTLEFIELD-POWER-OTHER-ALLY";
    private const string DefenderObjectId = "P2-SAME-BATTLEFIELD-POWER-DEFENDER";

    [Theory]
    [InlineData("OGS·013/024")]
    [InlineData("SFD·236/221")]
    [InlineData("SFD·236*/221")]
    [InlineData("OGN·243/298")]
    [InlineData("OGN·243a/298")]
    public async Task SameBattlefieldOtherFriendlyPowerAppliesForOfficialCardRows(string sourceCardNo)
    {
        var state = BuildState(sourceCardNo);

        var staticAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD", staticAura.Scope);
        Assert.Equal(AllyObjectId, staticAura.TargetObjectId);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne, staticAura.EffectKind);
        Assert.Equal(sourceCardNo, staticAura.SourceCardNo);
        Assert.Equal("CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus", staticAura.SourcePath);
        Assert.Equal("SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_BATTLEFIELD", staticAura.Condition);
        Assert.Equal("DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FRIENDLY_UNIT_LOCATIONS", staticAura.Lifecycle);
        Assert.Equal([AllyObjectId], staticAura.ParticipantObjectIds);
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, DefenderObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, OtherBattlefieldAllyObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-{sourceCardNo}-same-battlefield-other-friendly-power", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AllyObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, AllyObjectId, StringComparison.Ordinal));
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);

        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, DefenderObjectId, StringComparison.Ordinal));
        Assert.Equal(5, defenderDamageEvent.Payload["basePower"]);
        Assert.False(defenderDamageEvent.Payload.ContainsKey("staticPowerBonus"));
        Assert.Equal(5, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(5, defenderDamageEvent.Payload["damage"]);
    }

    private static MatchState BuildState(string sourceCardNo)
    {
        return new MatchState(
            "same-battlefield-other-friendly-static-power-card-row-room",
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
                    Battlefields =
                    [
                        BattlefieldObjectId,
                        SourceObjectId,
                        AllyObjectId,
                        OtherBattlefieldObjectId,
                        OtherBattlefieldAllyObjectId
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [BattlefieldObjectId] = Battlefield(BattlefieldObjectId, "P1"),
                [SourceObjectId] = new(
                    SourceObjectId,
                    cardNo: sourceCardNo,
                    power: 5,
                    tags: SourceUnitTags(sourceCardNo),
                    ownerId: "P1",
                    controllerId: "P1"),
                [AllyObjectId] = new(
                    AllyObjectId,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [OtherBattlefieldObjectId] = Battlefield(OtherBattlefieldObjectId, "P1"),
                [OtherBattlefieldAllyObjectId] = new(
                    OtherBattlefieldAllyObjectId,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [DefenderObjectId] = new(
                    DefenderObjectId,
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [SourceObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [AllyObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
                [OtherBattlefieldObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId),
                [OtherBattlefieldAllyObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)]);
    }

    private static string[] SourceUnitTags(string sourceCardNo)
    {
        return sourceCardNo.StartsWith("OGS", StringComparison.Ordinal)
            ? [CardObjectTags.UnitCard, "精锐"]
            : [CardObjectTags.UnitCard, "崔法利"];
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
