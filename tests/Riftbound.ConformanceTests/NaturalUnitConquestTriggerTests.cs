using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NaturalUnitConquestTriggerTests
{
    private const string BattlefieldId = "P1-NATURAL-UNIT-CONQUEST-BATTLEFIELD";
    private const string KaisaObjectId = "P1-NATURAL-CONQUEST-KAISA";
    private const string TreantObjectId = "P1-NATURAL-CONQUEST-TREANT";
    private const string YetiObjectId = "P1-NATURAL-CONQUEST-YETI";
    private const string DefenderObjectId = "P2-NATURAL-CONQUEST-DEFENDER";
    private const string DrawObjectId = "P1-NATURAL-CONQUEST-DRAW";

    [Fact]
    public async Task KaisaDrawsFromUnitConquestTriggerAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestState(),
            new PlayerIntent("intent-natural-unit-conquest-kaisa-draw", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [KaisaObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));

        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, KaisaObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestDrawOne, conquestTrigger.Payload["effectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var drawEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);
        Assert.Equal([DrawObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CrimsonSignetTreantRepeatsUnitConquestTriggerAfterNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestTreantState(),
            new PlayerIntent("intent-natural-unit-conquest-treant-repeat", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [TreantObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TreantObjectId, StringComparison.Ordinal));

        var conquestTriggers = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TreantObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["effectId"] as string, TriggerKinds.UnitConquestGrantFriendlyBoon, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, conquestTriggers.Length);

        var boonEvents = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, TreantObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["abilityId"] as string, TriggerKinds.UnitConquestGrantFriendlyBoon, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["targetObjectId"] as string, TreantObjectId, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, boonEvents.Length);
        Assert.False(Assert.IsType<bool>(boonEvents[0].Payload["alreadyHadBoon"]));
        Assert.True(Assert.IsType<bool>(boonEvents[1].Payload["alreadyHadBoon"]));

        var treant = result.State.CardObjects[TreantObjectId];
        Assert.Equal(5, treant.Power);
        Assert.Contains(CardObjectTags.Boon, treant.Tags);
    }

    [Fact]
    public async Task YetiBrawlerCreatesTwoDormantGoldAfterOverkillNaturalBattlefieldConquest()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildNaturalConquestYetiState(),
            new PlayerIntent("intent-natural-unit-conquest-yeti-overkill-gold", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldId,
                [YetiObjectId],
                [DefenderObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var conqueredEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, YetiObjectId, StringComparison.Ordinal));
        Assert.Equal(5, Assert.IsType<int>(conqueredEvent.Payload["assignedOverkillDamageToEnemyUnits"]));

        var conquestTrigger = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONQUEST_EFFECT_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, YetiObjectId, StringComparison.Ordinal));
        Assert.Equal(TriggerKinds.UnitConquestOverkillCreateDormantGold, conquestTrigger.Payload["effectId"]);
        Assert.Equal("BATTLEFIELD_CONQUERED", conquestTrigger.Payload["reason"]);
        Assert.Equal(BattlefieldId, conquestTrigger.Payload["battlefieldObjectId"]);

        var tokenEvents = result.Events
            .Where(gameEvent =>
                string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["sourceObjectId"] as string, YetiObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["abilityId"] as string, TriggerKinds.UnitConquestOverkillCreateDormantGold, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, tokenEvents.Length);

        var tokenObjectIds = tokenEvents
            .Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["tokenObjectId"]))
            .ToArray();
        Assert.Equal(tokenObjectIds, tokenObjectIds.Distinct(StringComparer.Ordinal).ToArray());
        Assert.All(tokenObjectIds, tokenObjectId =>
        {
            Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
            var tokenState = result.State.CardObjects[tokenObjectId];
            Assert.True(tokenState.IsExhausted);
            Assert.Contains(CardObjectTags.EquipmentCard, tokenState.Tags);
            Assert.Contains("金币", tokenState.Tags);
            Assert.Contains("反应", tokenState.Tags);
        });
    }

    private static MatchState BuildNaturalConquestState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [KaisaObjectId] = Unit(KaisaObjectId, "P1", 4, "OGN·039/298"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1),
            [DrawObjectId] = Unit(DrawObjectId, "P1", 2)
        };

        return new MatchState(
            "natural-unit-conquest-trigger-room",
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
                    Battlefields = [BattlefieldId, KaisaObjectId],
                    MainDeck = [DrawObjectId]
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
                [KaisaObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId),
                [DrawObjectId] = new("P1", "MAIN_DECK")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestTreantState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [TreantObjectId] = Unit(TreantObjectId, "P1", 4, "UNL-029/219"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1)
        };

        return new MatchState(
            "natural-unit-conquest-trigger-treant-room",
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
                    Battlefields = [BattlefieldId, TreantObjectId]
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
                [TreantObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static MatchState BuildNaturalConquestYetiState()
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldId] = new(
                BattlefieldId,
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [YetiObjectId] = Unit(YetiObjectId, "P1", 6, "UNL-018/219"),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", 1)
        };

        return new MatchState(
            "natural-unit-conquest-yeti-overkill-trigger-room",
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
                    Battlefields = [BattlefieldId, YetiObjectId]
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
                [YetiObjectId] = new("P1", "BATTLEFIELD", BattlefieldId),
                [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldId)
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldId)]);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        string cardNo = "SFD·125/221")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }
}
