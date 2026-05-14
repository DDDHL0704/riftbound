using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattleDamageAssignmentLifecycleTests
{
    private const string BattlefieldObjectId = "BF-DAMAGE";
    private const string HiddenBattlefieldObjectId = "BF-HIDDEN";
    private const string AttackerObjectId = "P1-ATTACKER";
    private const string SecondAttackerObjectId = "P1-SECOND-ATTACKER";
    private const string BulwarkDefenderObjectId = "P2-A-BULWARK";
    private const string BackRowDefenderObjectId = "P2-Z-BACKROW";
    private const string ShadowObjectId = "P2-SHADOW";
    private const string HiddenStandbyObjectId = "P2-HIDDEN-STANDBY";
    private const string NextBattlefieldObjectId = "BF-NEXT";
    private const string NextAttackerObjectId = "P1-NEXT-CONTEST";
    private const string NextDefenderObjectId = "P2-NEXT-CONTEST";
    private const string OriginalHeldScoreBattlefieldObjectId = "BF-BRUSH-ORIGINAL-HELD-SCORE";
    private const string HeldScoreRecycleRuneObjectId = "P2-RUNE-HELD-SCORE-RESPONSE";
    private const string HeldScoreRecycleRuneDeckObjectId = "P2-RUNE-BOTTOM-HELD-SCORE-RESPONSE";
    private const string HeldScoreTemporaryResourceId = "MALZAHAR:TEMP-HELD-SCORE-RESPONSE";

    [Fact]
    public async Task NaturalStartBattleWithAssignmentOrderingDefenderOpensAssignCombatDamagePrompt()
    {
        var state = BuildNaturalStartBattleState();

        var result = await DeclareAssignmentBattleAsync(state);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(state.Tick + 1, result.State.Tick);
        Assert.True(result.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));

        var p1Prompt = result.Prompts["P1"];
        Assert.True(p1Prompt.Actionable);
        Assert.Equal(PromptTypes.AssignCombatDamage, p1Prompt.View?.Type);
        Assert.Equal($"battle:{BattlefieldObjectId}", p1Prompt.View?.RelatedBattleId);
        Assert.Equal(BattlefieldObjectId, p1Prompt.View?.RelatedBattlefieldId);
        Assert.Equal([CommandTypes.AssignCombatDamage, CommandTypes.Surrender], p1Prompt.Actions);
        var candidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.AssignCombatDamage, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        Assert.Equal($"battle:{BattlefieldObjectId}", Assert.IsType<string>(metadata["battleId"]));
        Assert.Equal(BattlefieldObjectId, Assert.IsType<string>(metadata["battlefieldId"]));
        Assert.Equal("P1", Assert.IsType<string>(metadata["assigningPlayerId"]));
        var damagePool = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["damagePool"]);
        Assert.Equal(5, damagePool[AttackerObjectId]);
        Assert.Equal(2, damagePool[BulwarkDefenderObjectId]);
        Assert.Equal(1, damagePool[BackRowDefenderObjectId]);
        var legalTargets = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<string>>>(metadata["legalTargets"]);
        Assert.Equal([BulwarkDefenderObjectId, BackRowDefenderObjectId], legalTargets[AttackerObjectId]);
        var lethalThreshold = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["lethalDamageThreshold"]);
        Assert.Equal(2, lethalThreshold[BulwarkDefenderObjectId]);
        Assert.Equal(1, lethalThreshold[BackRowDefenderObjectId]);
        Assert.NotEmpty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["requiredAssignments"]));
        Assert.NotEmpty(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["assignmentChoices"]));

        var p2Prompt = result.Prompts["P2"];
        Assert.False(p2Prompt.Actionable);
        Assert.Equal(PromptTypes.AssignCombatDamage, p2Prompt.View?.Type);
        Assert.Equal(["WAIT", CommandTypes.Surrender], p2Prompt.Actions);
    }

    [Fact]
    public async Task ReconnectDuringNaturalAssignCombatDamagePreservesBattleTaskMetadataAndRedaction()
    {
        var opened = await DeclareAssignmentBattleAsync(BuildNaturalStartBattleState(includeHiddenStandby: true));
        Assert.True(opened.Accepted, opened.ErrorMessage);
        var session = new MatchSession(opened.State, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var player = session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var reconnect = session.ReconnectPlayer("P1", player.ReconnectToken);
        var snapshot = session.SnapshotFor("P1");
        var prompt = session.PromptFor("P1");
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("P1", reconnect.PlayerId);
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", Assert.IsType<string>(queue["activeTaskId"]));
        Assert.Equal(PromptTypes.AssignCombatDamage, prompt.View?.Type);
        Assert.Equal(BattlefieldObjectId, prompt.View?.RelatedBattlefieldId);
        Assert.Equal($"battle:{BattlefieldObjectId}", prompt.View?.RelatedBattleId);
        var battle = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["battle"]);
        Assert.True(Assert.IsType<bool>(battle["isActive"]));
        Assert.Equal(BattlefieldObjectId, Assert.IsType<string>(battle["battlefieldObjectId"]));
        AssertOpponentHiddenStandbyRedacted(snapshot, HiddenStandbyObjectId);
    }

    [Fact]
    public async Task NaturalBattleResponsePassThenOpensAssignCombatDamageForAssignmentOrderingBattle()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-shadow-assignment-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.StackPriority, openedResponse.Prompts["P2"].View?.Type);
        var responseCandidate = Assert.Single(
            openedResponse.Prompts["P2"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(responseCandidate.Sources ?? [], source => string.Equals(source.Id, ShadowObjectId, StringComparison.Ordinal));

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(PromptTypes.StackPriority, p2Pass.Prompts["P1"].View?.Type);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.True(p1Pass.State.BattleState.IsActive);
        Assert.Equal("BATTLE_TASKS", p1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", p1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.Equal($"battle:{BattlefieldObjectId}", p1Pass.Prompts["P1"].View?.RelatedBattleId);
        Assert.Equal(BattlefieldObjectId, p1Pass.Prompts["P1"].View?.RelatedBattlefieldId);

        var assigned = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-natural-response-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, ShadowResponseLegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
        Assert.Contains(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_STEP_STARTED", StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NaturalBattleResponsePreservesDeclarationContextAfterPass()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[AttackerObjectId] = cardObjects[AttackerObjectId] with { CardNo = "UNL-065/219", Power = 5 };
        state = state with { CardObjects = cardObjects };
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-context-shadow-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"],
                BattlefieldTargetObjectIds: [AttackerObjectId]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.True(openedResponse.State.BattleState.IsActive);
        var openedDeclaration = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal([AttackerObjectId], StringList(openedDeclaration.Payload["battlefieldTargetObjectIds"]));
        Assert.True(Assert.IsType<bool>(openedDeclaration.Payload["declarationContextPreserved"]));
        var openedPriority = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Equal([AttackerObjectId], StringList(openedPriority.Payload["battlefieldTargetObjectIds"]));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.Contains(
            openedResponse.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        var responseSession = new MatchSession(openedResponse.State, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1Snapshot = responseSession.SnapshotFor("P1");
        var p2Snapshot = responseSession.SnapshotFor("P2");
        var spectatorSnapshot = ResolutionResult.BuildSpectatorSnapshot(openedResponse.State);
        var p2Prompt = responseSession.PromptFor("P2");
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p1Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(spectatorSnapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Prompt));
        Assert.Equal(PromptTypes.StackPriority, p2Prompt.View?.Type);
        Assert.Equal($"battle:{BattlefieldObjectId}", p2Prompt.View?.RelatedBattleId);
        Assert.Equal(BattlefieldObjectId, p2Prompt.View?.RelatedBattlefieldId);

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-context-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-context-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        var closedPriority = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Equal([AttackerObjectId], StringList(closedPriority.Payload["battlefieldTargetObjectIds"]));
        var resumedDeclaration = p1Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal))
            .Last();
        Assert.Equal([AttackerObjectId], StringList(resumedDeclaration.Payload["battlefieldTargetObjectIds"]));
        var paymentOpened = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        Assert.Equal("ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1", paymentOpened.Payload["trigger"]);
        Assert.Equal(AttackerObjectId, paymentOpened.Payload["sourceObjectId"]);
        Assert.Equal(AttackerObjectId, paymentOpened.Payload["targetObjectId"]);
        Assert.NotNull(p1Pass.State.PendingPayment);
        Assert.Equal("TRIGGER_PAYMENT", p1Pass.State.PendingPayment?.PaymentWindow);
        Assert.Equal("P1", p1Pass.State.PendingPayment?.PlayerId);
        Assert.DoesNotContain(
            p1Pass.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        Assert.False(p1Pass.State.BattleState.IsActive);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, p1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponsePreservesBrushReplacementContextAfterPass()
    {
        var brushChoice = $"BRUSH_USE_REPLACED_BATTLEFIELD:{OriginalHeldScoreBattlefieldObjectId}";
        var optionalCosts = new[] { "COMBAT_ASSIGNMENT", brushChoice };
        var state = BuildBrushReplacementNaturalStartBattleState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-brush-context-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: optionalCosts),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.True(openedResponse.State.BattleState.IsActive);
        var openedDeclaration = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedDeclaration.Payload["optionalCosts"]));
        var openedPriority = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedPriority.Payload["optionalCosts"]));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_REPLACEMENT_APPLIED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(
            openedResponse.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));

        var responseSession = new MatchSession(openedResponse.State, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1Snapshot = responseSession.SnapshotFor("P1");
        var p2Snapshot = responseSession.SnapshotFor("P2");
        var spectatorSnapshot = ResolutionResult.BuildSpectatorSnapshot(openedResponse.State);
        var p2Prompt = responseSession.PromptFor("P2");
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p1Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(spectatorSnapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Prompt));
        Assert.Equal(PromptTypes.StackPriority, p2Prompt.View?.Type);
        Assert.Equal($"battle:{BattlefieldObjectId}", p2Prompt.View?.RelatedBattleId);
        Assert.Equal(BattlefieldObjectId, p2Prompt.View?.RelatedBattlefieldId);

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-brush-context-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-brush-context-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        var closedPriority = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(closedPriority.Payload["optionalCosts"]));
        var resumedDeclaration = p1Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal))
            .Last();
        Assert.Equal(optionalCosts, StringList(resumedDeclaration.Payload["optionalCosts"]));

        var replacement = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_REPLACEMENT_APPLIED", StringComparison.Ordinal));
        Assert.Equal(brushChoice, replacement.Payload["replacementChoice"]);
        Assert.Equal(BattlefieldObjectId, replacement.Payload["brushBattlefieldObjectId"]);
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, replacement.Payload["replacementBattlefieldObjectId"]);
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, replacement.Payload["effectiveBattlefieldObjectId"]);
        Assert.Equal("BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", replacement.Payload["replacementReason"]);

        var heldScore = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, heldScore.Payload["battlefieldId"]);
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, heldScore.Payload["battlefieldObjectId"]);
        var costPaid = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, costPaid.Payload["sourceObjectId"]);
        var scoreGained = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal("P2", scoreGained.Payload["playerId"]);
        Assert.Equal(1, scoreGained.Payload["score"]);
        Assert.Equal(1, p1Pass.State.PlayerScores["P2"]);
        Assert.Equal(0, p1Pass.State.RunePools["P2"].Power);
        Assert.DoesNotContain(
            p1Pass.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        Assert.False(p1Pass.State.BattleState.IsActive);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, p1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponsePreservesHeldScorePaymentResourceContextAfterPass()
    {
        var recycleAction = $"RECYCLE_RUNE:{HeldScoreRecycleRuneObjectId}";
        var optionalCosts = new[] { "COMBAT_ASSIGNMENT", recycleAction };
        var state = BuildHeldScorePaymentResourceNaturalStartBattleState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-payment-context-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: optionalCosts),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.True(openedResponse.State.BattleState.IsActive);
        var openedDeclaration = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedDeclaration.Payload["optionalCosts"]));
        var openedPriority = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedPriority.Payload["optionalCosts"]));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(
            openedResponse.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));

        var responseSession = new MatchSession(openedResponse.State, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1Snapshot = responseSession.SnapshotFor("P1");
        var p2Snapshot = responseSession.SnapshotFor("P2");
        var spectatorSnapshot = ResolutionResult.BuildSpectatorSnapshot(openedResponse.State);
        var p2Prompt = responseSession.PromptFor("P2");
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p1Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(spectatorSnapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Prompt));
        Assert.Equal(PromptTypes.StackPriority, p2Prompt.View?.Type);
        Assert.Equal($"battle:{BattlefieldObjectId}", p2Prompt.View?.RelatedBattleId);
        Assert.Equal(BattlefieldObjectId, p2Prompt.View?.RelatedBattlefieldId);

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-payment-context-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-payment-context-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        var closedPriority = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(closedPriority.Payload["optionalCosts"]));
        var resumedDeclaration = p1Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal))
            .Last();
        Assert.Equal(optionalCosts, StringList(resumedDeclaration.Payload["optionalCosts"]));

        var recycleEvent = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal(HeldScoreRecycleRuneObjectId, recycleEvent.Payload["sourceObjectId"]);
        Assert.Equal("BATTLEFIELD_HELD", recycleEvent.Payload["paymentWindow"]);
        var powerGained = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(HeldScoreRecycleRuneObjectId, powerGained.Payload["sourceObjectId"]);
        Assert.Equal("BATTLEFIELD_HELD", powerGained.Payload["paymentWindow"]);
        var heldScore = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(BattlefieldObjectId, heldScore.Payload["battlefieldId"]);
        Assert.Equal(BattlefieldObjectId, heldScore.Payload["battlefieldObjectId"]);
        var costPaid = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(BattlefieldObjectId, costPaid.Payload["sourceObjectId"]);
        Assert.Equal([recycleAction], Assert.IsType<string[]>(costPaid.Payload["paymentResourceActions"]));
        Assert.Equal([HeldScoreRecycleRuneObjectId], Assert.IsType<string[]>(costPaid.Payload["recycledRuneObjectIds"]));
        var scoreGained = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal("P2", scoreGained.Payload["playerId"]);
        Assert.Equal(1, scoreGained.Payload["score"]);
        Assert.Equal(1, p1Pass.State.PlayerScores["P2"]);
        Assert.Equal(0, p1Pass.State.RunePools["P2"].Power);
        Assert.DoesNotContain(HeldScoreRecycleRuneObjectId, p1Pass.State.PlayerZones["P2"].Base);
        Assert.Contains(HeldScoreRecycleRuneObjectId, p1Pass.State.PlayerZones["P2"].RuneDeck);
        Assert.DoesNotContain(
            p1Pass.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        Assert.False(p1Pass.State.BattleState.IsActive);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, p1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponsePreservesHeldScoreTemporaryPaymentResourceContextAfterPass()
    {
        var temporaryAction = PaymentCostRules.TemporaryPaymentResourceActionId(HeldScoreTemporaryResourceId);
        var optionalCosts = new[] { "COMBAT_ASSIGNMENT", temporaryAction };
        var state = BuildHeldScoreTemporaryPaymentResourceNaturalStartBattleState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-temp-payment-context-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: optionalCosts),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.True(openedResponse.State.BattleState.IsActive);
        var openedDeclaration = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedDeclaration.Payload["optionalCosts"]));
        var openedPriority = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedPriority.Payload["optionalCosts"]));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(
            openedResponse.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));

        var responseSession = new MatchSession(openedResponse.State, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1Snapshot = responseSession.SnapshotFor("P1");
        var p2Snapshot = responseSession.SnapshotFor("P2");
        var spectatorSnapshot = ResolutionResult.BuildSpectatorSnapshot(openedResponse.State);
        var p2Prompt = responseSession.PromptFor("P2");
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p1Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Snapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(spectatorSnapshot));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(p2Prompt));
        Assert.Equal(PromptTypes.StackPriority, p2Prompt.View?.Type);
        Assert.Equal($"battle:{BattlefieldObjectId}", p2Prompt.View?.RelatedBattleId);
        Assert.Equal(BattlefieldObjectId, p2Prompt.View?.RelatedBattlefieldId);

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-temp-payment-context-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-temp-payment-context-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        var closedPriority = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(closedPriority.Payload["optionalCosts"]));
        var resumedDeclaration = p1Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal))
            .Last();
        Assert.Equal(optionalCosts, StringList(resumedDeclaration.Payload["optionalCosts"]));

        var spentEvent = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(HeldScoreTemporaryResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal("BATTLEFIELD_HELD", spentEvent.Payload["paymentWindow"]);
        Assert.Equal(1, spentEvent.Payload["consumedPower"]);
        var clearedEvent = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(HeldScoreTemporaryResourceId, clearedEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal("BATTLEFIELD_HELD", clearedEvent.Payload["paymentWindow"]);
        var heldScore = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(BattlefieldObjectId, heldScore.Payload["battlefieldId"]);
        Assert.Equal(BattlefieldObjectId, heldScore.Payload["battlefieldObjectId"]);
        var costPaid = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(BattlefieldObjectId, costPaid.Payload["sourceObjectId"]);
        Assert.Equal([temporaryAction], Assert.IsType<string[]>(costPaid.Payload["paymentResourceActions"]));
        Assert.Equal([HeldScoreTemporaryResourceId], Assert.IsType<string[]>(costPaid.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(1, costPaid.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(0, costPaid.Payload["remainingPower"]);
        Assert.Equal(costPaid.Payload["paymentId"], spentEvent.Payload["paymentId"]);
        Assert.Equal(costPaid.Payload["paymentId"], clearedEvent.Payload["paymentId"]);
        var scoreGained = Assert.Single(
            p1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal("P2", scoreGained.Payload["playerId"]);
        Assert.Equal(1, scoreGained.Payload["score"]);
        Assert.Equal(1, p1Pass.State.PlayerScores["P2"]);
        Assert.Equal(0, p1Pass.State.RunePools["P2"].Power);
        Assert.Empty(p1Pass.State.TemporaryPaymentResources);
        Assert.DoesNotContain(
            p1Pass.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        Assert.False(p1Pass.State.BattleState.IsActive);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, p1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationPreservesBrushReplacementContextAfterStackResolution()
    {
        var brushChoice = $"BRUSH_USE_REPLACED_BATTLEFIELD:{OriginalHeldScoreBattlefieldObjectId}";
        var optionalCosts = new[] { "COMBAT_ASSIGNMENT", brushChoice };
        var state = BuildBrushReplacementActivationNaturalStartBattleState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-brush-activation-context-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: optionalCosts),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        var openedDeclaration = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedDeclaration.Payload["optionalCosts"]));
        var openedPriority = Assert.Single(
            openedResponse.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(openedPriority.Payload["optionalCosts"]));
        var responseCandidate = Assert.Single(
            openedResponse.Prompts["P2"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(responseCandidate.Sources ?? [], source => string.Equals(source.Id, ShadowObjectId, StringComparison.Ordinal));

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-brush-activation-context-shadow", "P2", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                ShadowObjectId,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                [AttackerObjectId]),
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(
            ["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"],
            activated.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(activated.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Single(activated.State.StackItems);
        Assert.Equal("P2", activated.State.PriorityPlayerId);
        Assert.Contains(
            activated.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        AssertBattleResponseContextNotLeaked(activated.State, "P2");

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-brush-activation-context-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-brush-activation-context-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.Contains("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        Assert.Contains(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.Contains(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, AttackerObjectId, StringComparison.Ordinal));
        Assert.Contains(
            stackP1Pass.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        AssertBattleResponseContextNotLeaked(stackP1Pass.State, "P2");

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-brush-activation-context-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-brush-activation-context-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        var closedPriority = Assert.Single(
            responseP1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.Equal(optionalCosts, StringList(closedPriority.Payload["optionalCosts"]));
        var resumedDeclaration = responseP1Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal))
            .Last();
        Assert.Equal(optionalCosts, StringList(resumedDeclaration.Payload["optionalCosts"]));

        var replacement = Assert.Single(
            responseP1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_REPLACEMENT_APPLIED", StringComparison.Ordinal));
        Assert.Equal(brushChoice, replacement.Payload["replacementChoice"]);
        Assert.Equal(BattlefieldObjectId, replacement.Payload["brushBattlefieldObjectId"]);
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, replacement.Payload["effectiveBattlefieldObjectId"]);
        var heldScore = Assert.Single(
            responseP1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, heldScore.Payload["battlefieldId"]);
        var costPaid = Assert.Single(
            responseP1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal(OriginalHeldScoreBattlefieldObjectId, costPaid.Payload["sourceObjectId"]);
        var scoreGained = Assert.Single(
            responseP1Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE", StringComparison.Ordinal));
        Assert.Equal("P2", scoreGained.Payload["playerId"]);
        Assert.Equal(1, scoreGained.Payload["score"]);
        Assert.Contains(responseP1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Equal(1, responseP1Pass.State.PlayerScores["P2"]);
        Assert.Equal(0, responseP1Pass.State.RunePools["P2"].Mana);
        Assert.Equal(0, responseP1Pass.State.RunePools["P2"].Power);
        Assert.False(responseP1Pass.State.BattleState.IsActive);
        Assert.DoesNotContain(
            responseP1Pass.State.UntilEndOfTurnEffects,
            effectId => effectId.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, responseP1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalAssignCombatDamageRejectsWrongOrStaleCommandsWithoutMutation()
    {
        var opened = await DeclareAssignmentBattleAsync(BuildNaturalStartBattleState());
        Assert.True(opened.Accepted, opened.ErrorMessage);
        var state = opened.State;
        var legalAssignments = LegalAssignments();
        var engine = new CoreRuleEngine();

        var wrongPlayer = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-assign-wrong-player", "P2", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, legalAssignments),
            CancellationToken.None);
        var wrongBattle = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-assign-wrong-battle", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand("battle:WRONG", BattlefieldObjectId, legalAssignments),
            CancellationToken.None);
        var wrongBattlefield = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-assign-wrong-battlefield", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", "BF-WRONG", legalAssignments),
            CancellationToken.None);
        var invalidTarget = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-assign-invalid-target", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand(
                $"battle:{BattlefieldObjectId}",
                BattlefieldObjectId,
                [
                    new CombatDamageAssignmentDto(AttackerObjectId, "P2-NOT-IN-BATTLE", 5),
                    new CombatDamageAssignmentDto(BulwarkDefenderObjectId, AttackerObjectId, 2),
                    new CombatDamageAssignmentDto(BackRowDefenderObjectId, AttackerObjectId, 1)
                ]),
            CancellationToken.None);

        AssertRejectedNoMutation(state, wrongPlayer, ErrorCodes.PhaseNotAllowed);
        AssertRejectedNoMutation(state, wrongBattle, ErrorCodes.PhaseNotAllowed);
        AssertRejectedNoMutation(state, wrongBattlefield, ErrorCodes.PhaseNotAllowed);
        AssertRejectedNoMutation(state, invalidTarget, ErrorCodes.InvalidTarget);

        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var prompt = session.PromptFor("P1");
        var stale = await session.SubmitAsync(
            "P1",
            "intent-natural-assign-stale-prompt",
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, legalAssignments),
            JsonSerializer.SerializeToElement(new
            {
                cmdType = CommandTypes.AssignCombatDamage,
                battleId = $"battle:{BattlefieldObjectId}",
                battlefieldId = BattlefieldObjectId,
                assignments = legalAssignments.Select(assignment => new
                {
                    assignment.SourceObjectId,
                    assignment.TargetObjectId,
                    assignment.Damage
                }),
                promptId = $"{prompt.PromptId}:stale",
                snapshotTick = prompt.SnapshotTick
            }),
            CancellationToken.None);

        Assert.False(stale.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
        Assert.Empty(stale.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(stale.State));
    }

    [Fact]
    public async Task NaturalAssignCombatDamageCommitsSimultaneousDamageAndClosesBattle()
    {
        var opened = await DeclareAssignmentBattleAsync(BuildNaturalStartBattleState());
        Assert.True(opened.Accepted, opened.ErrorMessage);

        var assigned = await new CoreRuleEngine().ResolveAsync(
            opened.State,
            new PlayerIntent("intent-natural-assign-legal", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, LegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        Assert.Equal(opened.State.Tick + 1, assigned.State.Tick);
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
        Assert.Contains(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_STEP_STARTED", StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "COMBAT_DAMAGE_ASSIGNED", StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, BulwarkDefenderObjectId, StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, BackRowDefenderObjectId, StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Contains(AttackerObjectId, assigned.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(BulwarkDefenderObjectId, assigned.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(BackRowDefenderObjectId, assigned.State.PlayerZones["P2"].Graveyard);
        Assert.False(assigned.State.CardObjects[AttackerObjectId].IsAttacking);
        Assert.Equal(0, assigned.State.CardObjects[AttackerObjectId].Damage);
    }

    [Fact]
    public async Task NaturalAssignCombatDamageAdvancesNextContestedBattlefieldTask()
    {
        var opened = await DeclareAssignmentBattleAsync(BuildNaturalStartBattleState(includeNextContest: true));
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.True(opened.State.BattleState.IsActive);
        Assert.Equal(PromptTypes.AssignCombatDamage, opened.Prompts["P1"].View?.Type);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", opened.State.PendingTaskQueue.ActiveTaskId);

        var assigned = await new CoreRuleEngine().ResolveAsync(
            opened.State,
            new PlayerIntent("intent-natural-assign-advances-next-battlefield", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, LegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        Assert.Contains(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("P1", assigned.State.FocusPlayerId);
        Assert.Equal("P1", assigned.State.ActivePlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal($"spell-duel:{NextBattlefieldObjectId}", assigned.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalAssignCombatDamageEmitsNoResultWhenAllParticipantsDestroyed()
    {
        var state = BuildNoResultNaturalStartBattleState();
        var engine = new CoreRuleEngine();

        var opened = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-no-result-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId, SecondAttackerObjectId],
                [BulwarkDefenderObjectId, BackRowDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.True(opened.State.BattleState.IsActive);
        Assert.Equal(PromptTypes.AssignCombatDamage, opened.Prompts["P1"].View?.Type);

        var assigned = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-natural-no-result-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, NoResultAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        var noResultEvent = Assert.Single(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal));
        Assert.Equal(BattlefieldObjectId, noResultEvent.Payload["battlefieldId"]);
        Assert.Equal("P1", noResultEvent.Payload["attackingPlayerId"]);
        Assert.Equal("P2", noResultEvent.Payload["defendingPlayerId"]);
        Assert.Equal("ALL_PARTICIPANTS_DESTROYED", noResultEvent.Payload["reason"]);
        Assert.Equal(
            [AttackerObjectId, SecondAttackerObjectId],
            Assert.IsType<string[]>(noResultEvent.Payload["attackerObjectIds"]));
        Assert.Equal(
            [BulwarkDefenderObjectId, BackRowDefenderObjectId],
            Assert.IsType<string[]>(noResultEvent.Payload["defenderObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingAttackerObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingDefenderObjectIds"]));
        Assert.Contains(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(
            [AttackerObjectId, SecondAttackerObjectId],
            assigned.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(
            [BulwarkDefenderObjectId, BackRowDefenderObjectId],
            assigned.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(AttackerObjectId, assigned.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(SecondAttackerObjectId, assigned.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(BulwarkDefenderObjectId, assigned.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain(BackRowDefenderObjectId, assigned.State.PlayerZones["P2"].Battlefields);
        Assert.False(assigned.State.CardObjects.ContainsKey(AttackerObjectId));
        Assert.False(assigned.State.CardObjects.ContainsKey(SecondAttackerObjectId));
        Assert.False(assigned.State.CardObjects.ContainsKey(BulwarkDefenderObjectId));
        Assert.False(assigned.State.CardObjects.ContainsKey(BackRowDefenderObjectId));

        var battleResolution = Assert.Single(assigned.State.BattleResolutions);
        Assert.Equal("NO_RESULT", battleResolution.Kind);
        Assert.Equal("ALL_PARTICIPANTS_DESTROYED", battleResolution.Reason);
        Assert.Equal(BattlefieldObjectId, battleResolution.BattlefieldId);
        Assert.Equal("P1", battleResolution.AttackingPlayerId);
        Assert.Equal("P2", battleResolution.DefendingPlayerId);
        Assert.Null(battleResolution.WinnerPlayerId);
        Assert.Equal([AttackerObjectId, SecondAttackerObjectId], battleResolution.AttackerObjectIds);
        Assert.Equal([BulwarkDefenderObjectId, BackRowDefenderObjectId], battleResolution.DefenderObjectIds);
        Assert.Empty(battleResolution.SurvivingAttackerObjectIds);
        Assert.Empty(battleResolution.SurvivingDefenderObjectIds);
        Assert.Equal(
            [AttackerObjectId, SecondAttackerObjectId, BulwarkDefenderObjectId, BackRowDefenderObjectId],
            battleResolution.DestroyedObjectIds);
    }

    [Fact]
    public async Task NaturalStartBattleOneOnOneImmediateBattleRemainsStable()
    {
        var state = BuildNaturalStartBattleState(defenderObjectIds: [BulwarkDefenderObjectId]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-natural-one-on-one-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.False(result.State.BattleState.IsActive);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, result.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(BulwarkDefenderObjectId, result.State.PlayerZones["P2"].Graveyard);
    }

    private static async Task<ResolutionResult> DeclareAssignmentBattleAsync(
        MatchState state,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-natural-assignment-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, BackRowDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static IReadOnlyList<CombatDamageAssignmentDto> LegalAssignments()
    {
        return
        [
            new CombatDamageAssignmentDto(AttackerObjectId, BulwarkDefenderObjectId, 2),
            new CombatDamageAssignmentDto(AttackerObjectId, BackRowDefenderObjectId, 3),
            new CombatDamageAssignmentDto(BulwarkDefenderObjectId, AttackerObjectId, 2),
            new CombatDamageAssignmentDto(BackRowDefenderObjectId, AttackerObjectId, 1)
        ];
    }

    private static IReadOnlyList<CombatDamageAssignmentDto> ShadowResponseLegalAssignments()
    {
        return
        [
            new CombatDamageAssignmentDto(AttackerObjectId, BulwarkDefenderObjectId, 2),
            new CombatDamageAssignmentDto(AttackerObjectId, ShadowObjectId, 3),
            new CombatDamageAssignmentDto(BulwarkDefenderObjectId, AttackerObjectId, 2),
            new CombatDamageAssignmentDto(ShadowObjectId, AttackerObjectId, 1)
        ];
    }

    private static IReadOnlyList<CombatDamageAssignmentDto> NoResultAssignments()
    {
        return
        [
            new CombatDamageAssignmentDto(AttackerObjectId, BulwarkDefenderObjectId, 2),
            new CombatDamageAssignmentDto(AttackerObjectId, BackRowDefenderObjectId, 1),
            new CombatDamageAssignmentDto(SecondAttackerObjectId, BulwarkDefenderObjectId, 2),
            new CombatDamageAssignmentDto(SecondAttackerObjectId, BackRowDefenderObjectId, 3),
            new CombatDamageAssignmentDto(BulwarkDefenderObjectId, AttackerObjectId, 2),
            new CombatDamageAssignmentDto(BackRowDefenderObjectId, AttackerObjectId, 3),
            new CombatDamageAssignmentDto(BackRowDefenderObjectId, SecondAttackerObjectId, 1)
        ];
    }

    private static MatchState BuildNoResultNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(includeSecondAttacker: true);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[AttackerObjectId] = cardObjects[AttackerObjectId] with { Power = 3 };
        cardObjects[SecondAttackerObjectId] = cardObjects[SecondAttackerObjectId] with { Power = 5, Damage = 4 };
        cardObjects[BackRowDefenderObjectId] = cardObjects[BackRowDefenderObjectId] with { Power = 4 };
        return state with { CardObjects = cardObjects };
    }

    private static MatchState BuildBrushReplacementNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P1"] = state.PlayerZones["P1"] with
            {
                Battlefields = [AttackerObjectId]
            },
            ["P2"] = state.PlayerZones["P2"] with
            {
                Battlefields =
                [
                    BattlefieldObjectId,
                    OriginalHeldScoreBattlefieldObjectId,
                    BulwarkDefenderObjectId,
                    ShadowObjectId
                ]
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo,
                tags:
                [
                    P6TokenFactoryCatalog.BattlefieldCardTag,
                    "草丛",
                    $"REPLACES_BATTLEFIELD:{OriginalHeldScoreBattlefieldObjectId}"
                ],
                ownerId: "P2",
                controllerId: "P2"),
            [OriginalHeldScoreBattlefieldObjectId] = new(
                OriginalHeldScoreBattlefieldObjectId,
                cardNo: "SFD·214/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            [AttackerObjectId] = state.CardObjects[AttackerObjectId] with { Power = 1 }
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [OriginalHeldScoreBattlefieldObjectId] = new("P2", "BATTLEFIELD", OriginalHeldScoreBattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [BulwarkDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [ShadowObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        return state with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 4)
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildBrushReplacementActivationNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P1"] = state.PlayerZones["P1"] with
            {
                Battlefields = [AttackerObjectId]
            },
            ["P2"] = state.PlayerZones["P2"] with
            {
                Battlefields =
                [
                    BattlefieldObjectId,
                    OriginalHeldScoreBattlefieldObjectId,
                    BulwarkDefenderObjectId,
                    ShadowObjectId
                ]
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo,
                tags:
                [
                    P6TokenFactoryCatalog.BattlefieldCardTag,
                    "草丛",
                    $"REPLACES_BATTLEFIELD:{OriginalHeldScoreBattlefieldObjectId}"
                ],
                ownerId: "P2",
                controllerId: "P2"),
            [OriginalHeldScoreBattlefieldObjectId] = new(
                OriginalHeldScoreBattlefieldObjectId,
                cardNo: "SFD·214/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            [AttackerObjectId] = state.CardObjects[AttackerObjectId] with { Power = 1 }
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [OriginalHeldScoreBattlefieldObjectId] = new("P2", "BATTLEFIELD", OriginalHeldScoreBattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [BulwarkDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [ShadowObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        return state with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 5)
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildHeldScorePaymentResourceNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P1"] = state.PlayerZones["P1"] with
            {
                Battlefields = [AttackerObjectId]
            },
            ["P2"] = state.PlayerZones["P2"] with
            {
                Base = [HeldScoreRecycleRuneObjectId],
                RuneDeck = [HeldScoreRecycleRuneDeckObjectId],
                Battlefields =
                [
                    BattlefieldObjectId,
                    BulwarkDefenderObjectId,
                    ShadowObjectId
                ]
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: "SFD·214/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            [AttackerObjectId] = state.CardObjects[AttackerObjectId] with { Power = 1 },
            [HeldScoreRecycleRuneObjectId] = new(
                HeldScoreRecycleRuneObjectId,
                isExhausted: true,
                tags: [CardObjectTags.RuneCard, "COLOR:red"],
                cardNo: "UNL-R01",
                ownerId: "P2",
                controllerId: "P2"),
            [HeldScoreRecycleRuneDeckObjectId] = new(
                HeldScoreRecycleRuneDeckObjectId,
                isExhausted: true,
                tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                cardNo: "UNL-R02",
                ownerId: "P2",
                controllerId: "P2")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [BulwarkDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [ShadowObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [HeldScoreRecycleRuneObjectId] = new("P2", "BASE"),
            [HeldScoreRecycleRuneDeckObjectId] = new("P2", "RUNE_DECK")
        };
        return state with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 3)
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildHeldScoreTemporaryPaymentResourceNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P1"] = state.PlayerZones["P1"] with
            {
                Battlefields = [AttackerObjectId]
            },
            ["P2"] = state.PlayerZones["P2"] with
            {
                Battlefields =
                [
                    BattlefieldObjectId,
                    BulwarkDefenderObjectId,
                    ShadowObjectId
                ]
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: "SFD·214/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            [AttackerObjectId] = state.CardObjects[AttackerObjectId] with { Power = 1 }
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [BulwarkDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [ShadowObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        var temporaryResource = new TemporaryPaymentResourceState(
            HeldScoreTemporaryResourceId,
            ownerPlayerId: "P2",
            sourceObjectId: "P2-TEMP-RESOURCE-SOURCE",
            abilityId: P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            paymentWindow: "ACTIVATE_ABILITY",
            generatedPower: 1,
            remainingPower: 1,
            allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind],
            createdTick: 19);
        return state with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 3)
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations,
            TemporaryPaymentResources = [temporaryResource]
        };
    }

    private static MatchState BuildNaturalStartBattleState(
        bool includeHiddenStandby = false,
        bool includeShadowResponse = false,
        bool includeNextContest = false,
        bool includeSecondAttacker = false,
        IReadOnlyList<string>? defenderObjectIds = null)
    {
        var defenders = defenderObjectIds ?? [BulwarkDefenderObjectId, BackRowDefenderObjectId];
        var p2Battlefields = defenders
            .Concat(includeShadowResponse ? [ShadowObjectId] : [])
            .Concat(includeHiddenStandby ? [HiddenBattlefieldObjectId, HiddenStandbyObjectId] : [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new MatchState(
            roomId: "battle-damage-assignment-lifecycle-room",
            tick: 20,
            turnNumber: 4,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = includeShadowResponse ? new RunePool(1, 1) : RunePool.Empty
            },
            playerZones: BuildPlayerZones(p2Battlefields, includeNextContest, includeSecondAttacker),
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: BuildCardObjects(includeHiddenStandby, includeShadowResponse, includeNextContest, includeSecondAttacker),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)],
            objectLocations: BuildObjectLocations(includeHiddenStandby, includeShadowResponse, includeNextContest, includeSecondAttacker));
    }

    private static Dictionary<string, PlayerZones> BuildPlayerZones(
        IReadOnlyList<string> p2Battlefields,
        bool includeNextContest,
        bool includeSecondAttacker)
    {
        var p1Battlefields = new[] { BattlefieldObjectId, AttackerObjectId }
            .Concat(includeSecondAttacker ? [SecondAttackerObjectId] : [])
            .Concat(includeNextContest ? [NextBattlefieldObjectId, NextAttackerObjectId] : [])
            .ToArray();
        var p2BattlefieldObjects = includeNextContest
            ? p2Battlefields.Concat([NextDefenderObjectId]).ToArray()
            : p2Battlefields;
        return new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Battlefields = p1Battlefields
            },
            ["P2"] = PlayerZones.Empty with
            {
                Battlefields = p2BattlefieldObjects
            }
        };
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects(
        bool includeHiddenStandby,
        bool includeShadowResponse,
        bool includeNextContest,
        bool includeSecondAttacker)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = Battlefield(BattlefieldObjectId, "P1"),
            [AttackerObjectId] = Unit(AttackerObjectId, "P1", power: 5),
            [BulwarkDefenderObjectId] = Unit(
                BulwarkDefenderObjectId,
                "P2",
                power: 2,
                tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark]),
            [BackRowDefenderObjectId] = Unit(
                BackRowDefenderObjectId,
                "P2",
                power: 1,
                tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.BackRow])
        };

        if (includeSecondAttacker)
        {
            cardObjects[SecondAttackerObjectId] = Unit(SecondAttackerObjectId, "P1", power: 4);
        }

        if (includeShadowResponse)
        {
            cardObjects[ShadowObjectId] = Unit(
                ShadowObjectId,
                "P2",
                power: 1,
                cardNo: P4ActivatedAbilityCatalog.ShadowCardNo);
        }

        if (includeNextContest)
        {
            cardObjects[NextBattlefieldObjectId] = Battlefield(NextBattlefieldObjectId, "P1");
            cardObjects[NextAttackerObjectId] = Unit(NextAttackerObjectId, "P1", power: 2);
            cardObjects[NextDefenderObjectId] = Unit(NextDefenderObjectId, "P2", power: 2);
        }

        if (includeHiddenStandby)
        {
            cardObjects[HiddenBattlefieldObjectId] = Battlefield(HiddenBattlefieldObjectId, "P2");
            cardObjects[HiddenStandbyObjectId] = new CardObjectState(
                HiddenStandbyObjectId,
                cardNo: null,
                isFaceDown: true,
                power: 1,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: "P2",
                controllerId: "P2");
        }

        return cardObjects;
    }

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations(
        bool includeHiddenStandby,
        bool includeShadowResponse,
        bool includeNextContest,
        bool includeSecondAttacker)
    {
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [BulwarkDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [BackRowDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };

        if (includeSecondAttacker)
        {
            objectLocations[SecondAttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId);
        }

        if (includeShadowResponse)
        {
            objectLocations[ShadowObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId);
        }

        if (includeNextContest)
        {
            objectLocations[NextBattlefieldObjectId] = new("P1", "BATTLEFIELD", NextBattlefieldObjectId);
            objectLocations[NextAttackerObjectId] = new("P1", "BATTLEFIELD", NextBattlefieldObjectId);
            objectLocations[NextDefenderObjectId] = new("P2", "BATTLEFIELD", NextBattlefieldObjectId);
        }

        if (includeHiddenStandby)
        {
            objectLocations[HiddenBattlefieldObjectId] = new("P2", "BATTLEFIELD", HiddenBattlefieldObjectId);
            objectLocations[HiddenStandbyObjectId] = new("P2", "BATTLEFIELD", HiddenBattlefieldObjectId);
        }

        return objectLocations;
    }

    private static CardObjectState Battlefield(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·275/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        IReadOnlyList<string>? tags = null,
        string cardNo = "SFD·125/221")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static void AssertRejectedNoMutation(MatchState state, ResolutionResult result, string expectedErrorCode)
    {
        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static void AssertBattleResponseContextNotLeaked(MatchState state, string promptPlayerId)
    {
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(session.SnapshotFor("P1")));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(session.SnapshotFor("P2")));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(state)));
        Assert.DoesNotContain("BATTLE_RESPONSE_DECLARATION_CONTEXT", JsonSerializer.Serialize(session.PromptFor(promptPlayerId)));
    }

    private static void AssertOpponentHiddenStandbyRedacted(SnapshotDto snapshot, string hiddenObjectId)
    {
        var opponentZones = ZoneView(PlayerView(snapshot, "P2"));
        Assert.DoesNotContain(hiddenObjectId, StringList(opponentZones["battlefields"]));
        var opponentObjects = ObjectView(PlayerView(snapshot, "P2"));
        if (opponentObjects.TryGetValue(hiddenObjectId, out var hiddenObject))
        {
            var hiddenView = Assert.IsType<Dictionary<string, object?>>(hiddenObject);
            Assert.True(Assert.IsType<bool>(hiddenView["isFaceDown"]));
            Assert.DoesNotContain("power", hiddenView.Keys);
            Assert.DoesNotContain("tags", hiddenView.Keys);
            Assert.DoesNotContain("cardNo", hiddenView.Keys);
        }
    }

    private static Dictionary<string, object?> PlayerView(SnapshotDto snapshot, string playerId)
    {
        return Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
    }

    private static Dictionary<string, object?> ZoneView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["zones"]);
    }

    private static Dictionary<string, object?> ObjectView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["objects"]);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
    }
}
