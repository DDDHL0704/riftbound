using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NaturalUnitConquestTriggerTests
{
    private const string BattlefieldId = "P1-NATURAL-UNIT-CONQUEST-BATTLEFIELD";
    private const string KaisaObjectId = "P1-NATURAL-CONQUEST-KAISA";
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
