using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StaticAuraStackingAndModifierTests
{
    private const string BattlefieldId = "BATTLEFIELD:P1-STATIC-AURA-STACK";
    private const string SourceObjectId = "P1-STATIC-AURA-STACK-SCARLET-PIGEON";
    private const string OtherFriendlyAuraSourceObjectId = "P1-STATIC-AURA-STACK-BARON-NASHOR";
    private const string DefenderObjectId = "P2-STATIC-AURA-STACK-DEFENDER";

    [Fact]
    public async Task SourceCombatAndOtherFriendlyStaticAurasStackWithUntilEndPowerModifier()
    {
        var projectionState = BuildStackingState(markBattleParticipants: true);

        var sourceCombatAura = Assert.Single(
            projectionState.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceAttackingWithAnotherUnitPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal(2, sourceCombatAura.PowerDelta);
        Assert.Equal(4, sourceCombatAura.BasePower);
        Assert.Equal(6, sourceCombatAura.EffectivePower);

        var otherFriendlyAura = Assert.Single(
            projectionState.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.OtherFriendlyUnitsPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, OtherFriendlyAuraSourceObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal(2, otherFriendlyAura.PowerDelta);
        Assert.Equal(4, otherFriendlyAura.BasePower);
        Assert.Equal(6, otherFriendlyAura.EffectivePower);

        var untilEndPower = Assert.Single(
            projectionState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal(1, untilEndPower.PowerDelta);
        Assert.Equal(3, untilEndPower.BasePower);
        Assert.Equal(4, untilEndPower.EffectivePower);

        var state = BuildStackingState(markBattleParticipants: false);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-static-aura-stacking-with-until-end", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                BattlefieldId,
                [SourceObjectId, OtherFriendlyAuraSourceObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var pigeonDamage = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal(DefenderObjectId, pigeonDamage.Payload["targetObjectId"]);
        Assert.Equal(4, pigeonDamage.Payload["basePower"]);
        Assert.Equal(0, pigeonDamage.Payload["keywordBonus"]);
        Assert.Equal(4, pigeonDamage.Payload["staticPowerBonus"]);
        Assert.Equal(8, pigeonDamage.Payload["combatPower"]);
        Assert.Equal(8, pigeonDamage.Payload["damage"]);
    }

    private static MatchState BuildStackingState(bool markBattleParticipants)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = Battlefield(BattlefieldId, "P1"),
            [SourceObjectId] = new(
                SourceObjectId,
                cardNo: "UNL-154/219",
                isAttacking: markBattleParticipants,
                power: 4,
                untilEndOfTurnPowerModifier: 1,
                tags: [CardObjectTags.UnitCard, "鸟类"],
                ownerId: "P1",
                controllerId: "P1"),
            [OtherFriendlyAuraSourceObjectId] = Unit(
                OtherFriendlyAuraSourceObjectId,
                "P1",
                12,
                "UNL-147/219",
                isAttacking: markBattleParticipants),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 10, isDefending: markBattleParticipants)
        };

        return new MatchState(
            "static-aura-stacking-with-until-end-room",
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
                    Battlefields = [BattlefieldId, SourceObjectId, OtherFriendlyAuraSourceObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [DefenderObjectId]
                }
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [BattlefieldId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [SourceObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [OtherFriendlyAuraSourceObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        string cardNo = "SFD·125/221",
        bool isAttacking = false,
        bool isDefending = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isAttacking: isAttacking,
            isDefending: isDefending,
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
