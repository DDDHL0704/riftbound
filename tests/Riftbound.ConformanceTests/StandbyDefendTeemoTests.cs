using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyDefendTeemoTests
{
    private const string BattlefieldObjectId = "P1-TEEMO-DEFEND-BATTLEFIELD";
    private const string AttackerObjectId = "P1-TEEMO-DEFEND-ATTACKER";
    private const string SecondEnemyTargetObjectId = "P1-TEEMO-DEFEND-SECOND-ENEMY";
    private const string TeemoObjectId = "P2-TEEMO-FACE-UP-DEFENDER";

    public static TheoryData<string> TeemoCards()
    {
        return new TheoryData<string>
        {
            "OGN·121/298",
            "OGN·121a/298",
            "SFD·230/221",
            "SFD·230*/221"
        };
    }

    [Fact]
    public void DeclareBattlePromptMetadataExposesTeemoDefendTriggerTargetChoices()
    {
        var session = new MatchSession(
            BuildState("OGN·121/298", includeSecondEnemyTarget: true),
            new CoreRuleEngine(),
            NoopMatchJournal.Instance);

        var prompt = session.PromptFor("P1");

        Assert.Contains(CommandTypes.DeclareBattle, prompt.Actions);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            requirement => string.Equals(requirement["sourceObjectId"] as string, AttackerObjectId, StringComparison.Ordinal));

        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minBattlefieldTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["maxBattlefieldTargetCount"]));
        Assert.Equal(CardTargetScopes.EnemyUnitAtSourceBattlefield, Assert.IsType<string>(sourceRequirement["battlefieldTargetScope"]));
        var battlefieldTargetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["battlefieldTargetChoicesByIndex"]);
        Assert.Equal(
            [AttackerObjectId, SecondEnemyTargetObjectId],
            battlefieldTargetChoicesByIndex["0"].Select(choice => choice.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [MemberData(nameof(TeemoCards))]
    public async Task FaceUpTeemoDefendTriggerCountsTopFiveStandbyCardsAndRecycles(string teemoCardNo)
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildState(teemoCardNo),
            new PlayerIntent("intent-teemo-face-up-defend-trigger", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [TeemoObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("P2-MAIN-BOTTOM", result.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Equal(
            TopFiveMainDeckCards().OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray(),
            result.State.PlayerZones["P2"].MainDeck.Skip(1).OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray());
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MAIN_DECK_CARDS_REVEALED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5)
            && Equals(gameEvent.Payload["damageAmount"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], AttackerObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5));
    }

    [Fact]
    public async Task FaceUpTeemoDefendTriggerUsesExplicitBattlefieldTargetWhenMultipleTargetsAreLegal()
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildState("OGN·121/298", includeSecondEnemyTarget: true),
            new PlayerIntent("intent-teemo-face-up-defend-trigger-explicit-target", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [TeemoObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"],
                BattlefieldTargetObjectIds: [SecondEnemyTargetObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("P2-MAIN-BOTTOM", result.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MAIN_DECK_CARDS_REVEALED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["damageAmount"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], SecondEnemyTargetObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5));
    }

    private static MatchState BuildState(string teemoCardNo, bool includeSecondEnemyTarget = false)
    {
        var topFive = TopFiveMainDeckCards();
        return new MatchState(
            "standby-defend-teemo-room",
            51,
            7,
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
                    Battlefields = includeSecondEnemyTarget
                        ? [BattlefieldObjectId, AttackerObjectId, SecondEnemyTargetObjectId]
                        : [BattlefieldObjectId, AttackerObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [TeemoObjectId],
                    MainDeck = [.. topFive, "P2-MAIN-BOTTOM"]
                }
            },
            cardObjects: BuildCardObjects(teemoCardNo, includeSecondEnemyTarget),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)],
            objectLocations: BuildObjectLocations(includeSecondEnemyTarget));
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects(string teemoCardNo, bool includeSecondEnemyTarget)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: "OGN·278/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1"),
            [AttackerObjectId] = Unit(AttackerObjectId, "P1", "UNL-057/219", 7),
            [TeemoObjectId] = Unit(TeemoObjectId, "P2", teemoCardNo, 2, ["约德尔人"]),
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
        if (includeSecondEnemyTarget)
        {
            cardObjects[SecondEnemyTargetObjectId] = Unit(SecondEnemyTargetObjectId, "P1", "UNL-092/219", 3);
        }

        return cardObjects;
    }

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations(bool includeSecondEnemyTarget)
    {
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [TeemoObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        if (includeSecondEnemyTarget)
        {
            objectLocations[SecondEnemyTargetObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId);
        }

        return objectLocations;
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
