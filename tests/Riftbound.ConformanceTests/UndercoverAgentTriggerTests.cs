using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class UndercoverAgentTriggerTests
{
    [Fact]
    public async Task UndercoverAgentLastBreathOpensHandChoiceAndDiscardsChosenThenDrawsTwo()
    {
        var engine = new CoreRuleEngine();
        var pending = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));

        Assert.NotNull(pending.State.PendingHandChoice);
        Assert.Equal(PromptTypes.HandChoice, pending.Prompts["P1"].View?.Type);
        Assert.Equal(PromptTypes.HandChoice, pending.Prompts["P2"].View?.Type);
        Assert.True(pending.Prompts["P1"].Actionable);
        Assert.False(pending.Prompts["P2"].Actionable);
        Assert.Contains(pending.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "HAND_CHOICE_REQUESTED", StringComparison.Ordinal));
        Assert.DoesNotContain(pending.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));

        var ownCandidate = Assert.Single(
            pending.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ChooseHandCards, StringComparison.Ordinal));
        var ownMetadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(ownCandidate.Metadata);
        var ownHandChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(ownMetadata["handChoices"]).ToArray();
        Assert.Equal(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"], ownHandChoices.Select(choice => choice.Id).ToArray());
        Assert.Equal("UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", ownMetadata["effectKind"]);

        var opponentViewMetadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(pending.Prompts["P2"].View?.Metadata);
        Assert.False(opponentViewMetadata.ContainsKey("handChoices"));
        Assert.False(opponentViewMetadata.ContainsKey("legalObjectIds"));
        Assert.Equal("WAITING", opponentViewMetadata["serverHandChoiceState"]);

        var choice = pending.State.PendingHandChoice!;
        var accepted = await engine.ResolveAsync(
            pending.State,
            new PlayerIntent("intent-undercover-hand-choice-submit", "P1", CommandTypes.ChooseHandCards),
            new ChooseHandCardsCommand(
                choice.ChoiceId,
                choice.ChoiceWindow,
                ["P1-HAND-001", "P1-HAND-002"]),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.State.PendingHandChoice);
        Assert.Equal(["P1-HAND-003", "P1-DRAW-001", "P1-DRAW-002"], accepted.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-001", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-HAND-002", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(accepted.Events));
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "HAND_CHOICE_RESOLVED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StateBasedCleanupUndercoverAgentQueuesAndOpensHandChoiceThroughStack()
    {
        var engine = new CoreRuleEngine();
        var pending = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentCleanupDestroyedState(["P1-HAND-001", "P1-HAND-002"]));

        Assert.NotNull(pending.State.PendingHandChoice);
        Assert.Equal(PromptTypes.HandChoice, pending.Prompts["P1"].View?.Type);
        Assert.Contains(pending.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Empty(pending.State.StackItems);
        Assert.Empty(pending.State.TriggerQueue);
    }

    [Fact]
    public async Task UndercoverAgentLastBreathWithOneHandAutoDiscardsAndDrawsTwo()
    {
        var engine = new CoreRuleEngine();
        var resolved = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState(["P1-HAND-001"]));

        Assert.Null(resolved.State.PendingHandChoice);
        Assert.NotEqual(PromptTypes.HandChoice, resolved.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], resolved.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-001", resolved.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, resolved.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(resolved.Events));
    }

    [Fact]
    public async Task UndercoverAgentLastBreathWithNoHandDrawsTwoWithoutPrompt()
    {
        var engine = new CoreRuleEngine();
        var resolved = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState([]));

        Assert.Null(resolved.State.PendingHandChoice);
        Assert.NotEqual(PromptTypes.HandChoice, resolved.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], resolved.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));
        Assert.Equal(2, DrawnCardCount(resolved.Events));
        Assert.Contains(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "HAND_CHOICE_SKIPPED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UndercoverAgentHandChoiceRejectsInvalidCommandsWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var pending = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));
        var state = pending.State;
        var choice = state.PendingHandChoice!;

        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P2",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-HAND-002"]),
            ErrorCodes.PhaseNotAllowed);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand($"stale-{choice.ChoiceId}", choice.ChoiceWindow, ["P1-HAND-001", "P1-HAND-002"]),
            ErrorCodes.PromptExpired);

        using var staleSnapshot = JsonDocument.Parse(
            $$"""
              {
                "cmdType": "CHOOSE_HAND_CARDS",
                "choiceId": "{{choice.ChoiceId}}",
                "choiceWindow": "{{choice.ChoiceWindow}}",
                "chosenObjectIds": ["P1-HAND-001", "P1-HAND-002"],
                "snapshotTick": {{state.Tick - 1}}
              }
              """);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var staleSnapshotResult = await session.SubmitAsync(
            "P1",
            "intent-undercover-stale-snapshot",
            GameCommandJsonMapper.Map(staleSnapshot.RootElement),
            staleSnapshot.RootElement,
            CancellationToken.None);
        Assert.False(staleSnapshotResult.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, staleSnapshotResult.ErrorCode);
        AssertNoMutation(state, staleSnapshotResult.State);
        Assert.Empty(staleSnapshotResult.Events);

        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-HAND-001"]),
            ErrorCodes.InvalidPayload);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001"]),
            ErrorCodes.InvalidPayload);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-UNKNOWN"]),
            ErrorCodes.InvalidTarget);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-BASE-OTHER"]),
            ErrorCodes.InvalidTarget);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, null),
            ErrorCodes.InvalidPayload);

        using var malformed = JsonDocument.Parse(
            $$"""
              {
                "cmdType": "CHOOSE_HAND_CARDS",
                "choiceId": "{{choice.ChoiceId}}",
                "choiceWindow": "{{choice.ChoiceWindow}}",
                "chosenObjectIds": "P1-HAND-001"
              }
              """);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            GameCommandJsonMapper.Map(malformed.RootElement),
            ErrorCodes.InvalidPayload);
    }

    [Fact]
    public async Task HiddenAndStandbyUndercoverAgentsDoNotTriggerOrLeakHandChoice()
    {
        var engine = new CoreRuleEngine();
        var p1Pass = await engine.ResolveAsync(
            BuildHiddenUndercoverAgentsDestroyedState(),
            new PlayerIntent("intent-hidden-undercover-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hidden-undercover-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Null(p2Pass.State.PendingHandChoice);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "HAND_CHOICE_REQUESTED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.HandChoice, p2Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, p2Pass.Prompts["P2"].View?.Type);
    }

    private static async Task AssertRejectedWithoutMutationAsync(
        CoreRuleEngine engine,
        MatchState state,
        string playerId,
        GameCommand command,
        string expectedErrorCode)
    {
        var rejected = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-undercover-reject-{Guid.NewGuid():N}", playerId, command.CmdType),
            command,
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        AssertNoMutation(state, rejected.State);
        Assert.Empty(rejected.Events);
    }

    private static void AssertNoMutation(MatchState expected, MatchState actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.PendingHandChoice, actual.PendingHandChoice);
        Assert.Equal(expected.PlayerZones["P1"].Hand, actual.PlayerZones["P1"].Hand);
        Assert.Equal(expected.PlayerZones["P1"].Graveyard, actual.PlayerZones["P1"].Graveyard);
        Assert.Equal(expected.PlayerZones["P1"].MainDeck, actual.PlayerZones["P1"].MainDeck);
    }

    private static int DrawnCardCount(IEnumerable<GameEvent> events)
    {
        return events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal))
            .Sum(gameEvent => gameEvent.Payload.TryGetValue("count", out var count)
                && count is int typedCount
                    ? typedCount
                    : 0);
    }

    private static async Task<ResolutionResult> ResolveUndercoverAgentTriggerAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-undercover-spirit-fire-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-undercover-spirit-fire-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", p2Pass.State.StackItems[0].EffectKind);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));

        var triggerPass1 = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-undercover-trigger-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var triggerPass2 = await engine.ResolveAsync(
            triggerPass1.State,
            new PlayerIntent("intent-undercover-trigger-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(triggerPass2.Accepted, triggerPass2.ErrorMessage);
        Assert.Empty(triggerPass2.State.StackItems);
        Assert.Empty(triggerPass2.State.TriggerQueue);
        Assert.Contains(triggerPass2.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", StringComparison.Ordinal));
        return triggerPass2;
    }

    private static MatchState BuildUndercoverAgentDestroyedState(IReadOnlyList<string> handObjectIds)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                MainDeck = ["P1-DRAW-001", "P1-DRAW-002", "P1-DRAW-003"],
                Hand = handObjectIds,
                Base = ["P1-UNDERCOVER-AGENT", "P1-BASE-OTHER"]
            },
            ["P2"] = PlayerZones.Empty
        };
        var cardObjects = BaseCardObjects(handObjectIds);
        cardObjects["P1-UNDERCOVER-AGENT"] = new(
            "P1-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 2,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-BASE-OTHER"] = new(
            "P1-BASE-OTHER",
            cardNo: "OGN·033/298",
            power: 1,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-SPELL-SPIRIT-FIRE"] = new(
            "P1-SPELL-SPIRIT-FIRE",
            cardNo: "OGN·256/298",
            ownerId: "P1",
            controllerId: "P1");

        return BuildState(
            "undercover-agent-trigger-room",
            playerZones,
            cardObjects,
            ["P1-UNDERCOVER-AGENT"]);
    }

    private static MatchState BuildUndercoverAgentCleanupDestroyedState(IReadOnlyList<string> handObjectIds)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                MainDeck = ["P1-DRAW-001", "P1-DRAW-002", "P1-DRAW-003"],
                Hand = handObjectIds,
                Base = ["P1-UNDERCOVER-AGENT"]
            },
            ["P2"] = PlayerZones.Empty with
            {
                Base = ["P2-CLEANUP-DUMMY"]
            }
        };
        var cardObjects = BaseCardObjects(handObjectIds);
        cardObjects["P1-UNDERCOVER-AGENT"] = new(
            "P1-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-SPELL-STARFALL"] = new(
            "P1-SPELL-STARFALL",
            cardNo: "OGN·029/298",
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-CLEANUP-DUMMY"] = new(
            "P2-CLEANUP-DUMMY",
            cardNo: "OGN·033/298",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");

        return BuildState(
            "undercover-agent-cleanup-trigger-room",
            playerZones,
            cardObjects,
            ["P1-UNDERCOVER-AGENT", "P2-CLEANUP-DUMMY"],
            "STACK-STARFALL-UNDERCOVER-AGENT",
            "P1-SPELL-STARFALL",
            "STARFALL_DAMAGE_3_TWICE",
            "OGN·029/298");
    }

    private static MatchState BuildHiddenUndercoverAgentsDestroyedState()
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                MainDeck = ["P1-DRAW-001", "P1-DRAW-002"],
                Hand = ["P1-HAND-001", "P1-HAND-002"],
                Base = ["P1-HIDDEN-UNDERCOVER-AGENT"]
            },
            ["P2"] = PlayerZones.Empty with
            {
                Base = ["P2-STANDBY-UNDERCOVER-AGENT"]
            }
        };
        var cardObjects = BaseCardObjects(["P1-HAND-001", "P1-HAND-002"]);
        cardObjects["P1-HIDDEN-UNDERCOVER-AGENT"] = new(
            "P1-HIDDEN-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 2,
            isFaceDown: true,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-STANDBY-UNDERCOVER-AGENT"] = new(
            "P2-STANDBY-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 2,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
            ownerId: "P2",
            controllerId: "P2");
        cardObjects["P1-SPELL-SPIRIT-FIRE"] = new(
            "P1-SPELL-SPIRIT-FIRE",
            cardNo: "OGN·256/298",
            ownerId: "P1",
            controllerId: "P1");

        return BuildState(
            "hidden-undercover-agent-trigger-room",
            playerZones,
            cardObjects,
            ["P1-HIDDEN-UNDERCOVER-AGENT", "P2-STANDBY-UNDERCOVER-AGENT"]);
    }

    private static Dictionary<string, CardObjectState> BaseCardObjects(IReadOnlyList<string> handObjectIds)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-DRAW-001"] = new("P1-DRAW-001", cardNo: "OGN·033/298", ownerId: "P1", controllerId: "P1"),
            ["P1-DRAW-002"] = new("P1-DRAW-002", cardNo: "OGN·033/298", ownerId: "P1", controllerId: "P1"),
            ["P1-DRAW-003"] = new("P1-DRAW-003", cardNo: "OGN·033/298", ownerId: "P1", controllerId: "P1")
        };

        foreach (var handObjectId in handObjectIds)
        {
            cardObjects[handObjectId] = new(
                handObjectId,
                cardNo: "OGN·033/298",
                ownerId: "P1",
                controllerId: "P1");
        }

        return cardObjects;
    }

    private static MatchState BuildState(
        string roomId,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> targetObjectIds,
        string stackItemId = "STACK-SPIRIT-FIRE-UNDERCOVER-AGENT",
        string sourceObjectId = "P1-SPELL-SPIRIT-FIRE",
        string effectKind = "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
        string cardNo = "OGN·256/298")
    {
        return new MatchState(
            roomId,
            11,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: playerZones,
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    stackItemId,
                    "P1",
                    sourceObjectId,
                    effectKind,
                    cardNo,
                    targetObjectIds)
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }
}
