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
        var state = BuildBattleResponseStandbyState();

        var result = await DeclareBattleAsync(state);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P2", result.State.PriorityPlayerId);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Contains(CommandTypes.RevealCard, result.Prompts["P2"].Actions);

        var candidate = Assert.Single(
            result.Prompts["P2"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.True(candidate.Enabled);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            Assert.IsType<Dictionary<string, object?>>(candidate.Metadata)["sourceRequirements"]));
        Assert.Equal(TeemoObjectId, sourceRequirement["sourceObjectId"]);
        Assert.Equal("STANDBY_REACTION", sourceRequirement["mode"]);
        Assert.Equal("ENEMY_UNIT_AT_SOURCE_BATTLEFIELD", sourceRequirement["targetScope"]);

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Equal([AttackerObjectId], targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public async Task BaseStandbyReactionTargetDamageUsesBattleResponseBattlefieldContext()
    {
        var engine = new CoreRuleEngine();
        var opened = await DeclareBattleAsync(BuildBattleResponseStandbyState(), engine);
        Assert.True(opened.Accepted, opened.ErrorMessage);

        var revealed = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-teemo-defense-standby-reaction", "P2", CommandTypes.RevealCard),
            new RevealCardCommand(
                TeemoObjectId,
                "OGN·121/298",
                [AttackerObjectId],
                Mode: "STANDBY_REACTION",
                OptionalCosts: ["STANDBY_REVEAL_0"],
                Destination: "STACK"),
            CancellationToken.None);

        Assert.True(revealed.Accepted, revealed.ErrorMessage);
        Assert.Empty(revealed.State.PlayerZones["P2"].Base);
        Assert.Single(revealed.State.StackItems);
        var stackItem = revealed.State.StackItems[0];
        Assert.Equal([AttackerObjectId], stackItem.TargetObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, stackItem.TimingContext);
        Assert.Equal(string.Empty, stackItem.Destination);

        var p2Pass = await engine.ResolveAsync(
            revealed.State,
            new PlayerIntent("intent-teemo-defense-standby-reaction-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-teemo-defense-standby-reaction-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.Empty(p1Pass.State.StackItems);
        Assert.True(p1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, p1Pass.State.TimingState);
        Assert.Equal("P2", p1Pass.State.PriorityPlayerId);
        Assert.Equal(2, p1Pass.State.CardObjects[AttackerObjectId].Damage);
        Assert.Equal([TeemoObjectId], p1Pass.State.PlayerZones["P2"].Base);
        Assert.Equal("P2-MAIN-BOTTOM", p1Pass.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Equal(
            TopFiveMainDeckCards().OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray(),
            p1Pass.State.PlayerZones["P2"].MainDeck.Skip(1).OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray());
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], AttackerObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5));
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
