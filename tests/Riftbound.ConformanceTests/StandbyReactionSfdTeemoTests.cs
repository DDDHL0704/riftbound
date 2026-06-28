using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyReactionSfdTeemoTests
{
    private const string BattlefieldObjectId = "P1-SFD-TEEMO-BATTLEFIELD";
    private const string EnemyTargetObjectId = "P2-SFD-TEEMO-SAME-BATTLEFIELD-ENEMY";
    private const string OtherBattlefieldObjectId = "P1-SFD-TEEMO-OTHER-BATTLEFIELD";
    private const string OtherEnemyObjectId = "P2-SFD-TEEMO-OTHER-BATTLEFIELD-ENEMY";
    private const string PendingSpellObjectId = "P2-SFD-TEEMO-PENDING-SPELL";
    private const string TeemoObjectId = "P1-FACEDOWN-BATTLEFIELD-SFD-230-TEEMO";

    public static TheoryData<string, string> SfdTeemoCards()
    {
        return new TheoryData<string, string>
        {
            { "SFD·230/221", "SFD_230_TEEMO_STANDBY_DEFEND_REVEAL_PLAY_UNIT" },
            { "SFD·230*/221", "SFD_230_PROMO_TEEMO_STANDBY_DEFEND_REVEAL_PLAY_UNIT" }
        };
    }

    [Theory]
    [MemberData(nameof(SfdTeemoCards))]
    public void PromptMetadataExposesSfdTeemoStandbyReactionTargets(string cardNo, string _)
    {
        var prompt = ResolutionResult.BuildPrompts(BuildClosedPriorityState(cardNo))["P1"];

        Assert.Contains(CommandTypes.RevealCard, prompt.Actions);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.True(candidate.Enabled);
        Assert.Equal([TeemoObjectId], (candidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal([EnemyTargetObjectId], (candidate.Targets ?? []).Select(target => target.Id).ToArray());

        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        Assert.Equal(TeemoObjectId, Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal(cardNo, Assert.IsType<string>(sourceRequirement["cardNo"]));
        Assert.Equal("STANDBY_REACTION", Assert.IsType<string>(sourceRequirement["mode"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        Assert.Equal(CardTargetScopes.EnemyUnitAtSourceBattlefield, Assert.IsType<string>(sourceRequirement["targetScope"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Equal([EnemyTargetObjectId], targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());
    }

    [Theory]
    [MemberData(nameof(SfdTeemoCards))]
    public async Task BattlefieldStandbyReactionSfdTeemoDamagesAndRecyclesWithSharedResolver(string cardNo, string effectKind)
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildClosedPriorityState(cardNo),
            new PlayerIntent("intent-sfd-teemo-standby-reaction", "P1", CommandTypes.RevealCard),
            new RevealCardCommand(
                TeemoObjectId,
                cardNo,
                [EnemyTargetObjectId],
                Mode: "STANDBY_REACTION",
                OptionalCosts: ["STANDBY_REVEAL_0"],
                Destination: "STACK"),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["STACK-0-P2-PENDING-SPELL", "STACK-46-P1-FACEDOWN-BATTLEFIELD-SFD-230-TEEMO"], result.State.StackItems.Select(item => item.StackItemId));
        var standbyStackItem = result.State.StackItems[1];
        Assert.Equal(effectKind, standbyStackItem.EffectKind);
        Assert.Equal([EnemyTargetObjectId], standbyStackItem.TargetObjectIds);
        Assert.Equal($"BATTLEFIELD:{BattlefieldObjectId}", standbyStackItem.Destination);
        Assert.Equal(TimingStates.NeutralClosed, standbyStackItem.TimingContext);
        Assert.Equal("STACK", result.State.ObjectLocations[TeemoObjectId].Zone);

        var p1Pass = await engine.ResolveAsync(
            result.State,
            new PlayerIntent("intent-sfd-teemo-standby-reaction-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sfd-teemo-standby-reaction-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["STACK-0-P2-PENDING-SPELL"], p2Pass.State.StackItems.Select(item => item.StackItemId));
        Assert.Contains(TeemoObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", BattlefieldObjectId), p2Pass.State.ObjectLocations[TeemoObjectId]);
        Assert.False(p2Pass.State.CardObjects[TeemoObjectId].IsFaceDown);
        Assert.Equal(2, p2Pass.State.CardObjects[EnemyTargetObjectId].Damage);
        Assert.Equal("P1-MAIN-BOTTOM", p2Pass.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(
            TopFiveMainDeckCards().OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray(),
            p2Pass.State.PlayerZones["P1"].MainDeck.Skip(1).OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray());
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "MAIN_DECK_CARDS_REVEALED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], EnemyTargetObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5));
    }

    private static MatchState BuildClosedPriorityState(string cardNo)
    {
        var topFive = TopFiveMainDeckCards();
        return new MatchState(
            "standby-reaction-sfd-teemo-room",
            45,
            6,
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
            timingState: TimingStates.NeutralClosed,
            priorityPlayerId: "P1",
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldObjectId, TeemoObjectId, OtherBattlefieldObjectId],
                    MainDeck = [.. topFive, "P1-MAIN-BOTTOM"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [EnemyTargetObjectId, OtherEnemyObjectId]
                }
            },
            cardObjects: BuildCardObjects(cardNo),
            stackItems:
            [
                new StackItemState(
                    "STACK-0-P2-PENDING-SPELL",
                    "P2",
                    PendingSpellObjectId,
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ],
            objectLocations: BuildObjectLocations());
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects(string cardNo)
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = Battlefield(BattlefieldObjectId),
            [OtherBattlefieldObjectId] = Battlefield(OtherBattlefieldObjectId),
            [TeemoObjectId] = new(
                TeemoObjectId,
                isFaceDown: true,
                cardNo: cardNo,
                power: 2,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                ownerId: "P1",
                controllerId: "P1"),
            [EnemyTargetObjectId] = Unit(EnemyTargetObjectId, "P2", "UNL-057/219", 7),
            [OtherEnemyObjectId] = Unit(OtherEnemyObjectId, "P2", "UNL-092/219", 3),
            [PendingSpellObjectId] = new(
                PendingSpellObjectId,
                cardNo: "OGN·007/298",
                tags: [CardObjectTags.SpellCard],
                ownerId: "P2",
                controllerId: "P2"),
            ["P1-MAIN-STANDBY-001"] = Unit("P1-MAIN-STANDBY-001", "P1", "OGN·121/298", 2, [CardObjectTags.Standby]),
            ["P1-MAIN-STANDBY-002"] = Unit("P1-MAIN-STANDBY-002", "P1", "OGN·199/298", 2, [CardObjectTags.Standby]),
            ["P1-MAIN-NON-STANDBY-001"] = Unit("P1-MAIN-NON-STANDBY-001", "P1", "SFD·125/221", 3),
            ["P1-MAIN-NON-STANDBY-002"] = new(
                "P1-MAIN-NON-STANDBY-002",
                cardNo: "OGN·009/298",
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-MAIN-NON-STANDBY-003"] = Unit("P1-MAIN-NON-STANDBY-003", "P1", "SFD·125/221", 3),
            ["P1-MAIN-BOTTOM"] = Unit("P1-MAIN-BOTTOM", "P1", "SFD·125/221", 3)
        };
    }

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations()
    {
        return new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [OtherBattlefieldObjectId] = new("P1", "BATTLEFIELD", OtherBattlefieldObjectId),
            [TeemoObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [EnemyTargetObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [OtherEnemyObjectId] = new("P2", "BATTLEFIELD", OtherBattlefieldObjectId),
            [PendingSpellObjectId] = new("P2", "STACK")
        };
    }

    private static string[] TopFiveMainDeckCards()
    {
        return
        [
            "P1-MAIN-STANDBY-001",
            "P1-MAIN-NON-STANDBY-001",
            "P1-MAIN-STANDBY-002",
            "P1-MAIN-NON-STANDBY-002",
            "P1-MAIN-NON-STANDBY-003"
        ];
    }

    private static CardObjectState Battlefield(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·278/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P1",
            controllerId: "P1");
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
