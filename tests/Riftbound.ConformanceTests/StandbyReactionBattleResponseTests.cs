using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyReactionBattleResponseTests
{
    private const string BattlefieldObjectId = "P1-TEEMO-DEFENSE-BATTLEFIELD";
    private const string AttackerObjectId = "P1-TEEMO-DEFENSE-ATTACKER";
    private const string DefenderObjectId = "P2-TEEMO-DEFENSE-DEFENDER";
    private const string TeemoObjectId = "P2-FACEDOWN-BASE-OGN-121-TEEMO";

    [Fact]
    public async Task DeclareBattleOpensStandbyReactionBattleResponseForDefender()
    {
        // Current battle responses occur in the initial duel after actual movement establishes roles.
        var state = await StandbyReactionSfdTeemoTests.BuildClosedPriorityState("OGN·121/298", twoContests: true);
        Assert.Equal(TimingStates.SpellDuelOpen, state.TimingState);
        Assert.Equal("P1", state.FocusPlayerId);
        Assert.True(state.CardObjects[StandbyReactionSfdTeemoTests.EnemyTargetObjectId].IsAttacking);
        Assert.True(state.CardObjects["HOST"].IsDefending);
        Assert.Contains(CommandTypes.RevealCard, ResolutionResult.BuildPrompts(state)["P1"].EnabledActions());
        Assert.True(state.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal("F", state.CardObjects["SHADOW"].StandbyPlacement!.Battlefield.ObjectId);
        Assert.Equal(0, state.CardObjects[StandbyReactionSfdTeemoTests.EnemyTargetObjectId].Damage);
        Assert.Equal(0, state.CardObjects["STANDBY-NEXT-OPPONENT"].Damage);
        Assert.False(state.CardObjects["STANDBY-NEXT-OPPONENT"].IsAttacking);
        Assert.False(state.CardObjects["STANDBY-NEXT-OPPONENT"].IsDefending);
        var played = await OfficialInsightAndSpellLockTests.Act(state, "P1", new RevealCardCommand("SHADOW", "OGN·121/298", [],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"));
        var pending = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal([StandbyReactionSfdTeemoTests.EnemyTargetObjectId], pending.LegalObjectIds);
        OfficialGraveyardRecastTests.Restore(played.State);
        ArmamentMightTests.PublicRecovery(played.State);
    }

    [Fact]
    public async Task BaseStandbyReactionTargetDamageUsesBattleResponseBattlefieldContext()
    {
        // A shadow's original field stays authoritative while a second contest waits.
        await StandbyTestDriver.AssertNaturalNestedBattleResponse();
    }

    private static ValueTask<ResolutionResult> DeclareBattleAsync(MatchState state, CoreRuleEngine? engine = null)
    {
        return (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-teemo-defense-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [DefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static MatchState BuildBattleResponseStandbyState()
    {
        var topFive = TopFiveMainDeckCards();
        return new MatchState(
            "standby-reaction-battle-response-room",
            17,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldObjectId, AttackerObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [TeemoObjectId],
                    Battlefields = [DefenderObjectId],
                    MainDeck = [.. topFive, "P2-MAIN-BOTTOM"]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: BuildCardObjects(),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)],
            objectLocations: BuildObjectLocations());
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: "OGN·278/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [AttackerObjectId] = Unit(AttackerObjectId, "P1", "UNL-057/219", 7),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", "UNL-092/219", 3),
            [TeemoObjectId] = new(
                TeemoObjectId,
                isFaceDown: true,
                cardNo: "OGN·121/298",
                power: 2,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-MAIN-STANDBY-001"] = Unit("P2-MAIN-STANDBY-001", "P2", "OGN·121/298", 2, [CardObjectTags.Standby]),
            ["P2-MAIN-STANDBY-002"] = Unit("P2-MAIN-STANDBY-002", "P2", "OGN·199/298", 2, [CardObjectTags.Standby]),
            ["P2-MAIN-NON-STANDBY-001"] = Unit("P2-MAIN-NON-STANDBY-001", "P2", "SFD·125/221", 3),
            ["P2-MAIN-NON-STANDBY-002"] = new(
                "P2-MAIN-NON-STANDBY-002",
                cardNo: "OGN·009/298",
                tags: [CardObjectTags.SpellCard],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-MAIN-NON-STANDBY-003"] = Unit("P2-MAIN-NON-STANDBY-003", "P2", "SFD·125/221", 3),
            ["P2-MAIN-BOTTOM"] = Unit("P2-MAIN-BOTTOM", "P2", "SFD·125/221", 3)
        };
    }

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations()
    {
        return new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [DefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [TeemoObjectId] = new("P2", "BASE")
        };
    }

    private static string[] TopFiveMainDeckCards()
    {
        return
        [
            "P2-MAIN-STANDBY-001",
            "P2-MAIN-NON-STANDBY-001",
            "P2-MAIN-STANDBY-002",
            "P2-MAIN-NON-STANDBY-002",
            "P2-MAIN-NON-STANDBY-003"
        ];
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        string cardNo,
        int power,
        IReadOnlyList<string>? extraTags = null)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [.. new[] { CardObjectTags.UnitCard }.Concat(extraTags ?? [])],
            ownerId: playerId,
            controllerId: playerId);
    }
}
