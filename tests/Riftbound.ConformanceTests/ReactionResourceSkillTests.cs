using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReactionResourceSkillTests
{
    private const string DragonSoulSageObjectId = "P1-DRAGON-SOUL-SAGE";
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    [Fact]
    public void DragonSoulSageReactionPromptExposesServerFilteredNoTargetResourceSkill()
    {
        var state = BuildDragonSoulSagePriorityState();
        var prompts = ResolutionResult.BuildPrompts(state);

        var p1Prompt = prompts["P1"];
        Assert.True(p1Prompt.Actionable);
        Assert.Contains(CommandTypes.ActivateAbility, p1Prompt.Actions);
        var activateCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([DragonSoulSageObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId,
                StringComparison.Ordinal));
        Assert.Equal(DragonSoulSageObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]) is true);
        Assert.True(Assert.IsType<bool>(requirement["reactionSpeed"]) is true);
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]) is true);
        Assert.True(Assert.IsType<bool>(requirement["resolvesImmediately"]) is true);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageGeneratedMana, requirement["generatedMana"]);
        Assert.Equal("stack-priority-reaction-representative", requirement["timingPolicy"]);
        Assert.Equal("resolves-immediately-without-stack-item", requirement["reactionPolicy"]);
        Assert.Equal("rune-pool-mana-reset-at-turn-cleanup", requirement["resourceLifecycle"]);
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Empty(targetChoicesByIndex);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]));

        var p2Prompt = prompts["P2"];
        Assert.False(p2Prompt.Actionable);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, p2Prompt.Actions);
        Assert.DoesNotContain(
            p2Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
    }

    [Fact]
    public void DragonSoulSageOpenMainPromptDoesNotExposeReactionResourceSkill()
    {
        var state = BuildDragonSoulSagePriorityState() with
        {
            TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null,
            StackItems = []
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        AssertNoDragonSoulSageResourceSkill(prompt);
    }

    [Fact]
    public async Task DragonSoulSageReactionCommandExhaustsSourceGainsManaWithoutOpeningStackItem()
    {
        var state = BuildDragonSoulSagePriorityState();

        var result = await ResolveDragonSoulSageAsync(state);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.True(result.State.CardObjects[DragonSoulSageObjectId].IsExhausted);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageGeneratedMana, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);

        var activatedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal)
            && AssertPayloadBool(gameEvent, "resourceSkill")
            && AssertPayloadBool(gameEvent, "reactionSpeed")
            && string.Equals(gameEvent.Payload["timingContext"] as string, "STACK_PRIORITY_REACTION", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId, activatedEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageGeneratedMana, activatedEvent.Payload["generatedMana"]);

        var exhaustedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, DragonSoulSageObjectId, StringComparison.Ordinal));
        Assert.True(AssertPayloadBool(exhaustedEvent, "resourceSkill"));
        Assert.True(AssertPayloadBool(exhaustedEvent, "reactionSpeed"));

        var manaEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageGeneratedMana, manaEvent.Payload["mana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageGeneratedMana, manaEvent.Payload["manaAfter"]);
        Assert.True(AssertPayloadBool(manaEvent, "resourceSkill"));
        Assert.True(AssertPayloadBool(manaEvent, "reactionSpeed"));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DragonSoulSageReactionResourceStalePromptReplayAfterManaGainRejectsWithoutMutation()
    {
        var state = BuildDragonSoulSagePriorityState();
        var command = DragonSoulSageCommand();
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Contains(
            activateCandidate.Sources ?? [],
            source => string.Equals(source.Id, DragonSoulSageObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.ActivateAbility, prompt);

        var gained = await session.SubmitAsync(
            "P1",
            "intent-dragon-soul-sage-before-stale-prompt-replay",
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(gained.Accepted, gained.ErrorMessage);
        Assert.True(gained.State.CardObjects[DragonSoulSageObjectId].IsExhausted);
        Assert.Equal(P4ActivatedAbilityCatalog.DragonSoulSageGeneratedMana, gained.State.RunePools["P1"].Mana);
        Assert.Empty(gained.State.TemporaryPaymentResources);
        Assert.Equal([PendingStackItemId], gained.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Null(gained.State.PendingPayment);
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal));
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        AssertNoDragonSoulSageResourceSkill(gained.Prompts["P1"]);
        var postGainHash = MatchStateHasher.Hash(gained.State);

        var replay = await session.SubmitAsync(
            "P1",
            "intent-dragon-soul-sage-stale-prompt-replay",
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(postGainHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(gained.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.True(replay.State.CardObjects[DragonSoulSageObjectId].IsExhausted);
        Assert.Empty(replay.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.StackItems, replay.State.StackItems);
        Assert.Equal(gained.State.PendingTaskQueue.Phase, replay.State.PendingTaskQueue.Phase);
        Assert.Equal(gained.State.PendingTaskQueue.ActiveTaskId, replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(gained.State.PendingTaskQueue.Tasks, replay.State.PendingTaskQueue.Tasks);
        Assert.Null(replay.State.PendingPayment);
        AssertNoDragonSoulSageResourceSkill(replay.Prompts["P1"]);
    }

    [Fact]
    public async Task DragonSoulSageGeneratedManaUsesNormalRunePoolCleanupLifecycle()
    {
        var result = await ResolveDragonSoulSageAsync(BuildDragonSoulSagePriorityState());
        Assert.True(result.Accepted, result.ErrorMessage);

        var endTurnState = result.State with
        {
            TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            StackItems = []
        };
        var cleanupResult = await new CoreRuleEngine().ResolveAsync(
            endTurnState,
            new PlayerIntent("intent-dragon-soul-sage-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(cleanupResult.Accepted, cleanupResult.ErrorMessage);
        Assert.Equal(RunePool.Empty, cleanupResult.State.RunePools["P1"]);
        Assert.Contains(
            cleanupResult.Events,
            gameEvent => string.Equals(gameEvent.Kind, "RUNE_POOL_CLEARED", StringComparison.Ordinal)
                && Assert.IsAssignableFrom<IEnumerable<string>>(gameEvent.Payload["playerIds"]).Contains("P1"));
    }

    [Theory]
    [InlineData("wrong-priority")]
    [InlineData("open-main")]
    [InlineData("spell-duel-focus")]
    [InlineData("cleanup-blocking")]
    public async Task DragonSoulSageRejectsWrongTimingWithoutMutation(string caseName)
    {
        var state = BuildInvalidTimingState(caseName);

        await AssertRejectedNoMutationAsync(
            state,
            "P1",
            DragonSoulSageCommand(),
            ErrorCodes.PhaseNotAllowed);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("optional-cost")]
    [InlineData("exhausted")]
    [InlineData("face-down")]
    [InlineData("base-source")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    public async Task DragonSoulSageRejectsInvalidSourceOrPayloadWithoutMutation(string caseName)
    {
        var state = BuildInvalidCommandState(caseName);
        var command = caseName switch
        {
            "target" => DragonSoulSageCommand(["P2-ANY-TARGET"]),
            "optional-cost" => DragonSoulSageCommand(optionalCosts: ["SPEND_MANA:1"]),
            _ => DragonSoulSageCommand()
        };

        await AssertRejectedNoMutationAsync(
            state,
            "P1",
            command,
            caseName == "wrong-card" ? ErrorCodes.UnsupportedCardBehavior : ErrorCodes.InvalidTarget);
    }

    private static async Task<ResolutionResult> ResolveDragonSoulSageAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-soul-sage-resource-skill", "P1", CommandTypes.ActivateAbility),
            DragonSoulSageCommand(),
            CancellationToken.None);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        string playerId,
        ActivateAbilityCommand command,
        string expectedErrorCode)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-dragon-soul-sage-rejected-{expectedErrorCode}", playerId, CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static ActivateAbilityCommand DragonSoulSageCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            DragonSoulSageObjectId,
            P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId,
            targetObjectIds ?? [],
            optionalCosts);
    }

    private static JsonElement PromptScopedRawCommand(string cmdType, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
    }

    private static void AssertNoDragonSoulSageResourceSkill(ActionPromptDto prompt)
    {
        foreach (var candidate in prompt.Candidates ?? [])
        {
            if (!string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal)
                || candidate.Metadata is not IReadOnlyDictionary<string, object?> metadata
                || !metadata.TryGetValue("sourceRequirements", out var rawRequirements)
                || rawRequirements is not IEnumerable<IReadOnlyDictionary<string, object?>> sourceRequirements)
            {
                continue;
            }

            Assert.DoesNotContain(
                sourceRequirements,
                requirement => string.Equals(
                    requirement["abilityId"] as string,
                    P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId,
                    StringComparison.Ordinal));
        }
    }

    private static MatchState BuildInvalidTimingState(string caseName)
    {
        var state = BuildDragonSoulSagePriorityState();
        return caseName switch
        {
            "wrong-priority" => state with
            {
                PriorityPlayerId = "P2"
            },
            "open-main" => state with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                StackItems = []
            },
            "spell-duel-focus" => state with
            {
                TimingState = TimingStates.SpellDuelOpen,
                PriorityPlayerId = null,
                StackItems = [],
                FocusPlayerId = "P1"
            },
            "cleanup-blocking" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    DragonSoulSageObjectId,
                    state.CardObjects[DragonSoulSageObjectId] with
                    {
                        Damage = 1,
                        Power = 1
                    })
            },
            _ => state
        };
    }

    private static MatchState BuildInvalidCommandState(string caseName)
    {
        var state = BuildDragonSoulSagePriorityState();
        return caseName switch
        {
            "exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    DragonSoulSageObjectId,
                    state.CardObjects[DragonSoulSageObjectId] with { IsExhausted = true })
            },
            "face-down" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    DragonSoulSageObjectId,
                    state.CardObjects[DragonSoulSageObjectId] with { IsFaceDown = true })
            },
            "base-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = [DragonSoulSageObjectId],
                        Battlefields = []
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    DragonSoulSageObjectId,
                    new ObjectLocationState("P1", "BASE"))
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    DragonSoulSageObjectId,
                    state.CardObjects[DragonSoulSageObjectId] with { ControllerId = "P2" })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    DragonSoulSageObjectId,
                    state.CardObjects[DragonSoulSageObjectId] with { CardNo = "UNL-094/219" })
            },
            _ => state
        };
    }

    private static MatchState BuildDragonSoulSagePriorityState()
    {
        return new MatchState(
            "room-dragon-soul-sage-resource",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "Alice",
                ["P2"] = "Bob"
            },
            status: MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [DragonSoulSageObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [DragonSoulSageObjectId] = Unit(
                    DragonSoulSageObjectId,
                    P4ActivatedAbilityCatalog.DragonSoulSageCardNo,
                    "P1",
                    power: 2),
                [PendingSpellObjectId] = new(
                    PendingSpellObjectId,
                    tags: [CardObjectTags.SpellCard],
                    cardNo: "UNL-001/219",
                    ownerId: "P2",
                    controllerId: "P2")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    PendingStackItemId,
                    "P2",
                    PendingSpellObjectId,
                    "TEST_PENDING_REACTION_SPELL",
                    "UNL-001/219")
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [DragonSoulSageObjectId] = new("P1", "BATTLEFIELD", "P1-MAIN"),
                [PendingSpellObjectId] = new("P2", "STACK")
            });
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string playerId,
        int power = 0)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.UnitCard],
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId,
            power: power);
    }

    private static IReadOnlyDictionary<string, CardObjectState> ReplaceCardObject(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        CardObjectState replacement)
    {
        var next = cardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, PlayerZones> ReplacePlayerZones(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string playerId,
        PlayerZones replacement)
    {
        var next = playerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[playerId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocation(
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        string objectId,
        ObjectLocationState replacement)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static bool AssertPayloadBool(GameEvent gameEvent, string key)
    {
        return Assert.IsType<bool>(gameEvent.Payload[key]);
    }
}
