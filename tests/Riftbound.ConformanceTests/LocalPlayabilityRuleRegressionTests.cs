using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LocalPlayabilityRuleRegressionTests
{
    [Fact]
    public async Task PlayCardUnitToPreciseBattlefieldStartsSpellDuelAfterStackResolution()
    {
        var engine = new CoreRuleEngine();
        var state = PlayUnitToContestedBattlefieldState();

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-play-unit-to-precise-battlefield", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-HAND-UNIT",
                "SFD·125/221",
                [],
                Destination: "BATTLEFIELD:BF-1"),
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, played.State.TimingState);
        Assert.Equal(new ObjectLocationState("P1", "STACK"), played.State.ObjectLocations["P1-HAND-UNIT"]);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-play-unit-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-play-unit-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, p2Pass.State.TimingState);
        Assert.Equal("P1", p2Pass.State.FocusPlayerId);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF-1"), p2Pass.State.ObjectLocations["P1-HAND-UNIT"]);
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            p2Pass.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
    }

    [Fact]
    public void PlayCardPromptUsesRealBattlefieldDestinationsForUnits()
    {
        var state = PlayUnitToContestedBattlefieldState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var requirement = Assert.Single(
            requirements,
            item => string.Equals(item["sourceObjectId"] as string, "P1-HAND-UNIT", StringComparison.Ordinal));
        var destinationChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            requirement["destinationChoices"]);

        Assert.Contains(destinationChoices, choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal));
        Assert.Contains(destinationChoices, choice => string.Equals(choice.Id, "BATTLEFIELD:BF-1", StringComparison.Ordinal));
        Assert.DoesNotContain(destinationChoices, choice => string.Equals(choice.Id, "BATTLEFIELD:P1-MAIN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeclareBattleConquestScoresAndTriggersConquestForNonHuntUnit()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BattleStateForConquest(),
            new PlayerIntent("intent-non-hunt-conquest-score", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BF-1",
                ["P1-ATTACKER"],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal("P1", result.State.CardObjects["BF-1"].ControllerId);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        var scoreEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P1", scoreEvent.Payload["playerId"]);
        Assert.Equal(1, scoreEvent.Payload["amount"]);
        Assert.Equal("BATTLEFIELD_CONQUERED_SCORE", scoreEvent.Payload["reason"]);
        Assert.Contains(
            BattlefieldTaskMarkers.ScoreGainedThisTurn("BF-1", "P1"),
            result.State.UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task TurnStartHeldBattlefieldScoresAndTriggersOgn275Minion()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            TurnStartHeldOgn275State(),
            new PlayerIntent("intent-end-turn-into-held-score", "P2", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_CREATE_MINION", StringComparison.Ordinal));
        var scoreEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD_HELD_SCORE", scoreEvent.Payload["reason"]);
        var tokenEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_HELD_CREATE_MINION", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CatalogCostSpecDoesNotExposePrintedUnitPowerAsPowerCost()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var unit = RuleTextParser.Parse(Card(catalog, "UNL-022/219"));
        Assert.Equal(4, unit.Cost.Mana);
        Assert.Equal(1, unit.Cost.ReturnEnergy);
        Assert.Null(unit.Cost.Power);

        var spell = RuleTextParser.Parse(Card(catalog, "OGN·004/298"));
        Assert.Equal(1, spell.Cost.Mana);
        Assert.Null(spell.Cost.Power);
    }

    private static MatchState PlayUnitToContestedBattlefieldState()
    {
        return new MatchState(
            roomId: "local-playability-play-unit-battlefield",
            tick: 0,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(10, 10),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P2-DEFENDER"]
                }
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P2", "OGN·275/298"),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT", "P1", power: 2),
                ["P2-DEFENDER"] = Unit("P2-DEFENDER", "P2", power: 2)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P2", "BATTLEFIELD", "BF-1"),
                ["P1-HAND-UNIT"] = new("P1", "HAND"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState BattleStateForConquest()
    {
        return new MatchState(
            roomId: "local-playability-conquest-score",
            tick: 0,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
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
                    Battlefields = ["P1-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P2-DEFENDER"]
                }
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P2", "OGN·276/298"),
                ["P1-ATTACKER"] = Unit("P1-ATTACKER", "P1", power: 4),
                ["P2-DEFENDER"] = Unit("P2-DEFENDER", "P2", power: 1)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P2", "BATTLEFIELD", "BF-1"),
                ["P1-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-1")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")]);
    }

    private static MatchState TurnStartHeldOgn275State()
    {
        return new MatchState(
            roomId: "local-playability-held-score-ogn275",
            tick: 0,
            turnNumber: 2,
            activePlayerId: "P2",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
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
                    MainDeck = ["P1-DRAW"],
                    Battlefields = ["BF-1", "P1-HOLDER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P1", "OGN·275/298"),
                ["P1-HOLDER"] = Unit("P1-HOLDER", "P1", power: 2, exhausted: true),
                ["P1-DRAW"] = new(
                    "P1-DRAW",
                    cardNo: "OGN·004/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-HOLDER"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-DRAW"] = new("P1", "MAIN_DECK")
            });
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static Dictionary<string, int> Scores()
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["P1"] = 0,
            ["P2"] = 0
        };
    }

    private static CardObjectState Battlefield(string objectId, string playerId, string cardNo)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        bool exhausted = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            power: power,
            isExhausted: exhausted,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static OfficialCard Card(OfficialCardCatalog catalog, string cardNo)
    {
        return catalog.Cards.Single(card => string.Equals(card.CardNo, cardNo, StringComparison.Ordinal));
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
