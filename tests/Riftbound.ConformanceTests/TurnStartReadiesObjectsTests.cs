using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TurnStartReadiesObjectsTests
{
    private const string LegionRearguardObjectId = "P1-BASE-LEGION-REARGUARD";
    private const string ExhaustedRuneObjectId = "P1-BASE-RUNE-EXHAUSTED";

    [Fact]
    public async Task EndTurnReadiesNextTurnPlayersExhaustedActiveZoneObjectsAndReopensMove()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BuildP2EndTurnIntoP1State(),
            new PlayerIntent("intent-p2-end-turn-into-p1-ready", "P2", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.False(result.State.CardObjects[LegionRearguardObjectId].IsExhausted);
        Assert.False(result.State.CardObjects[ExhaustedRuneObjectId].IsExhausted);

        var readyEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "OBJECTS_READIED", StringComparison.Ordinal));
        Assert.Equal("P1", Assert.IsType<string>(readyEvent.Payload["playerId"]));
        Assert.Equal(2, Assert.IsType<int>(readyEvent.Payload["count"]));
        Assert.Equal(
            [LegionRearguardObjectId, ExhaustedRuneObjectId],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(readyEvent.Payload["objectIds"]));

        var p1Prompt = result.Prompts["P1"];
        Assert.True(p1Prompt.Actionable);
        Assert.Contains(CommandTypes.MoveUnit, p1Prompt.Actions);
        var moveCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.Contains(
            moveCandidate.Sources ?? [],
            source => string.Equals(source.Id, LegionRearguardObjectId, StringComparison.Ordinal));
    }

    private static MatchState BuildP2EndTurnIntoP1State()
    {
        return new MatchState(
            roomId: "turn-start-readies-local-2p-smoke",
            tick: 12,
            turnNumber: 2,
            activePlayerId: "P2",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 0)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001"],
                    RuneDeck = ["P1-RUNE-DECK-001"],
                    Base = [ExhaustedRuneObjectId, LegionRearguardObjectId],
                    Battlefields = ["P1-BATTLEFIELD-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-RUNE-001"],
                    Battlefields = ["P2-BATTLEFIELD-001"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [ExhaustedRuneObjectId] = Rune(ExhaustedRuneObjectId, "P1", isExhausted: true),
                [LegionRearguardObjectId] = Unit(LegionRearguardObjectId, "OGN·010/298", "P1", isExhausted: true),
                ["P1-DRAW-001"] = Unit("P1-DRAW-001", "OGN·001/298", "P1"),
                ["P1-RUNE-DECK-001"] = Rune("P1-RUNE-DECK-001", "P1"),
                ["P1-BATTLEFIELD-001"] = Battlefield("P1-BATTLEFIELD-001", "OGN·275/298", "P1"),
                ["P2-BASE-RUNE-001"] = Rune("P2-BASE-RUNE-001", "P2", isExhausted: true),
                ["P2-BATTLEFIELD-001"] = Battlefield("P2-BATTLEFIELD-001", "OGN·276/298", "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [ExhaustedRuneObjectId] = new("P1", "BASE"),
                [LegionRearguardObjectId] = new("P1", "BASE"),
                ["P1-DRAW-001"] = new("P1", "MAIN_DECK"),
                ["P1-RUNE-DECK-001"] = new("P1", "RUNE_DECK"),
                ["P1-BATTLEFIELD-001"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-001"),
                ["P2-BASE-RUNE-001"] = new("P2", "BASE"),
                ["P2-BATTLEFIELD-001"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-001")
            });
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string playerId,
        bool isExhausted = false)
    {
        return new(
            objectId,
            power: 2,
            isExhausted: isExhausted,
            tags: [CardObjectTags.UnitCard],
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Rune(string objectId, string playerId, bool isExhausted = false)
    {
        return new(
            objectId,
            isExhausted: isExhausted,
            tags: [CardObjectTags.RuneCard],
            cardNo: "SFD·R01",
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Battlefield(string objectId, string cardNo, string playerId)
    {
        return new(
            objectId,
            tags: ["CARD_TYPE:BATTLEFIELD"],
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
    }
}
