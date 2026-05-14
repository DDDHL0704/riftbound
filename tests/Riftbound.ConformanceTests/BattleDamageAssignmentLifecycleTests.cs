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
    private const string SecondShadowObjectId = "P2-SECOND-SHADOW";
    private const string HiddenStandbyObjectId = "P2-HIDDEN-STANDBY";
    private const string StandbyReactionObjectId = "P1-FACEDOWN-STANDBY-REACTION";
    private const string NextBattlefieldObjectId = "BF-NEXT";
    private const string NextAttackerObjectId = "P1-NEXT-CONTEST";
    private const string NextDefenderObjectId = "P2-NEXT-CONTEST";
    private const string OriginalHeldScoreBattlefieldObjectId = "BF-BRUSH-ORIGINAL-HELD-SCORE";
    private const string HeldScoreRecycleRuneObjectId = "P2-RUNE-HELD-SCORE-RESPONSE";
    private const string HeldScoreRecycleRuneDeckObjectId = "P2-RUNE-BOTTOM-HELD-SCORE-RESPONSE";
    private const string HeldScoreTemporaryResourceId = "MALZAHAR:TEMP-HELD-SCORE-RESPONSE";
    private const string IcevaleTrigger = "ICEVALE_ARCHER_ATTACK_PAY_1_POWER_MINUS_1";
    private const string TriggerPaymentWindow = "TRIGGER_PAYMENT";
    private const string PayOneMana = "SPEND_MANA:1";
    private const string Decline = "DECLINE";

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
    public async Task NaturalBattleResponseAssignmentAdvancesNextContestedBattlefieldTask()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-assignment-advancement-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", openedResponse.State.PendingTaskQueue.ActiveTaskId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-assignment-advancement-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(PromptTypes.StackPriority, p2Pass.Prompts["P1"].View?.Type);
        AssertNextContestedBattlefieldNotAdvanced(p2Pass);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-response-assignment-advancement-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p1Pass.State.BattleState.IsActive);
        Assert.Equal("BATTLE_TASKS", p1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", p1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(BattlefieldObjectId, p1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        var responseClosedIndex = EventIndex(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var assignmentOpenedIndex = EventIndex(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.True(responseClosedIndex < assignmentOpenedIndex);
        AssertNextContestedBattlefieldNotAdvanced(p1Pass);

        var assigned = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-natural-response-assignment-advancement-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, ShadowResponseLegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        var nextContestIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("P1", assigned.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponsePassAssignmentConquerResultOrdersBeforeNextAdvancement()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [AttackerObjectId] = state.CardObjects[AttackerObjectId] with
            {
                CardNo = "UNL-059/219",
                Power = 5,
                Tags = [CardObjectTags.UnitCard, "狩猎2"]
            }
        };
        state = state with { CardObjects = cardObjects };
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-conquer-ordering-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-conquer-ordering-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(PromptTypes.StackPriority, p2Pass.Prompts["P1"].View?.Type);
        AssertNextContestedBattlefieldNotAdvanced(p2Pass);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-response-conquer-ordering-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p1Pass.State.BattleState.IsActive);
        Assert.Equal(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(BattlefieldObjectId, p1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        var responseClosedIndex = EventIndex(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var assignmentOpenedIndex = EventIndex(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.True(responseClosedIndex < assignmentOpenedIndex);
        AssertNextContestedBattlefieldNotAdvanced(p1Pass);

        var assigned = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-natural-response-conquer-ordering-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, ShadowResponseLegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.Equal(2, assigned.State.PlayerExperience["P1"]);
        Assert.DoesNotContain(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));

        var conquerIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, AttackerObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["huntAmount"], 2));
        var experienceIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, AttackerObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["amount"], 2)
            && Equals(gameEvent.Payload["totalExperience"], 2));
        var damageRemovedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_REMOVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        var nextContestIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(conquerIndex < experienceIndex);
        Assert.True(experienceIndex < damageRemovedIndex);
        Assert.True(damageRemovedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("P1", assigned.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponsePassAssignmentHeldResultOrdersBeforeNextAdvancement()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId, BackRowDefenderObjectId]);
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [AttackerObjectId] = state.CardObjects[AttackerObjectId] with
            {
                Power = 1,
                Tags = [CardObjectTags.UnitCard]
            },
            [BulwarkDefenderObjectId] = state.CardObjects[BulwarkDefenderObjectId] with
            {
                CardNo = "UNL-059/219",
                Power = 2,
                Tags = [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark, "狩猎2"]
            }
        };
        state = state with { CardObjects = cardObjects };
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-held-ordering-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, BackRowDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var p2Pass = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-held-ordering-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(PromptTypes.StackPriority, p2Pass.Prompts["P1"].View?.Type);
        AssertNextContestedBattlefieldNotAdvanced(p2Pass);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-natural-response-held-ordering-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p1Pass.State.BattleState.IsActive);
        Assert.Equal(PromptTypes.AssignCombatDamage, p1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(BattlefieldObjectId, p1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        var responseClosedIndex = EventIndex(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var assignmentOpenedIndex = EventIndex(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.True(responseClosedIndex < assignmentOpenedIndex);
        AssertNextContestedBattlefieldNotAdvanced(p1Pass);

        var assigned = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-natural-response-held-ordering-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand(
                $"battle:{BattlefieldObjectId}",
                BattlefieldObjectId,
                [
                    new CombatDamageAssignmentDto(AttackerObjectId, BulwarkDefenderObjectId, 1),
                    new CombatDamageAssignmentDto(BulwarkDefenderObjectId, AttackerObjectId, 2),
                    new CombatDamageAssignmentDto(BackRowDefenderObjectId, AttackerObjectId, 1)
                ]),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.Equal(2, assigned.State.PlayerExperience["P2"]);
        Assert.DoesNotContain(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));

        var heldIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, AttackerObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["huntAmount"], 2));
        var heldEvent = assigned.Events[heldIndex];
        Assert.Equal([BulwarkDefenderObjectId, BackRowDefenderObjectId], Assert.IsType<string[]>(heldEvent.Payload["defenderObjectIds"]));
        Assert.Equal([BulwarkDefenderObjectId], Assert.IsType<string[]>(heldEvent.Payload["huntSourceObjectIds"]));
        var huntAmountsBySource = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(heldEvent.Payload["huntAmountsBySource"]);
        Assert.Equal(2, huntAmountsBySource[BulwarkDefenderObjectId]);
        var experienceIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EXPERIENCE_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, BulwarkDefenderObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["amount"], 2)
            && Equals(gameEvent.Payload["totalExperience"], 2));
        var damageRemovedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_REMOVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P2", StringComparison.Ordinal));
        var nextContestIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(heldIndex < experienceIndex);
        Assert.True(experienceIndex < damageRemovedIndex);
        Assert.True(damageRemovedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("P1", assigned.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationAssignmentAdvancesNextContestedBattlefieldTask()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-activation-assignment-advancement-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-activation-assignment-advancement-shadow", "P2", CommandTypes.ActivateAbility),
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
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-activation-assignment-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.Contains("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        Assert.Contains(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.True(responseP1Pass.State.BattleState.IsActive);
        Assert.Equal("BATTLE_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var assignmentOpenedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.True(responseClosedIndex < assignmentOpenedIndex);
        AssertNextContestedBattlefieldNotAdvanced(responseP1Pass);

        var assigned = await engine.ResolveAsync(
            responseP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-advancement-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, ShadowResponseLegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        var nextContestIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("P1", assigned.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationAssignmentNoResultAdvancesNextContestedBattlefieldTask()
    {
        var state = BuildNoResultShadowActivationNaturalStartBattleState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-activation-assignment-no-result-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId, SecondAttackerObjectId],
                [BulwarkDefenderObjectId, BackRowDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-activation-assignment-no-result-shadow", "P2", CommandTypes.ActivateAbility),
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
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-activation-assignment-no-result-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-no-result-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.Contains("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        var stackResolvedIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var abilityResolvedIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.True(stackResolvedIndex < abilityResolvedIndex);
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-no-result-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-no-result-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.True(responseP1Pass.State.BattleState.IsActive);
        Assert.Equal("BATTLE_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var assignmentOpenedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.True(responseClosedIndex < assignmentOpenedIndex);
        AssertNextContestedBattlefieldNotAdvanced(responseP1Pass);

        var assigned = await engine.ResolveAsync(
            responseP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-assignment-no-result-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, NoResultAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        var noResultEvent = Assert.Single(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal));
        Assert.Equal(BattlefieldObjectId, noResultEvent.Payload["battlefieldId"]);
        Assert.Equal("ALL_PARTICIPANTS_DESTROYED", noResultEvent.Payload["reason"]);
        Assert.Equal(
            [AttackerObjectId, SecondAttackerObjectId],
            Assert.IsType<string[]>(noResultEvent.Payload["attackerObjectIds"]));
        Assert.Equal(
            [BulwarkDefenderObjectId, BackRowDefenderObjectId],
            Assert.IsType<string[]>(noResultEvent.Payload["defenderObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingAttackerObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(noResultEvent.Payload["survivingDefenderObjectIds"]));
        Assert.DoesNotContain(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(assigned.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));

        var battleResolution = Assert.Single(assigned.State.BattleResolutions);
        Assert.Equal("NO_RESULT", battleResolution.Kind);
        Assert.Equal("ALL_PARTICIPANTS_DESTROYED", battleResolution.Reason);
        Assert.Equal(BattlefieldObjectId, battleResolution.BattlefieldId);
        Assert.Null(battleResolution.WinnerPlayerId);
        Assert.Empty(battleResolution.SurvivingAttackerObjectIds);
        Assert.Empty(battleResolution.SurvivingDefenderObjectIds);
        Assert.Equal(
            [AttackerObjectId, SecondAttackerObjectId, BulwarkDefenderObjectId, BackRowDefenderObjectId],
            battleResolution.DestroyedObjectIds);

        var noResultIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var nextContestIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(noResultIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("P1", assigned.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationAssignmentCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask()
    {
        var state = BuildShadowActivationStandbyCleanupNaturalStartBattleState();
        AssertOpponentHiddenStandbyRedacted(ResolutionResult.BuildSnapshots(state)["P1"], HiddenStandbyObjectId);
        AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(state, HiddenStandbyObjectId);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-activation-cleanup-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId, ShadowObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(openedResponse.State, HiddenStandbyObjectId);
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-activation-cleanup-shadow", "P2", CommandTypes.ActivateAbility),
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
        AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(activated.State, HiddenStandbyObjectId);
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-activation-cleanup-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-cleanup-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.Contains("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        var stackResolvedIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var abilityResolvedIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.True(stackResolvedIndex < abilityResolvedIndex);
        AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(stackP1Pass.State, HiddenStandbyObjectId);
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-cleanup-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-cleanup-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.True(responseP1Pass.State.BattleState.IsActive);
        Assert.Equal("BATTLE_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var assignmentOpenedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.True(responseClosedIndex < assignmentOpenedIndex);
        AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(responseP1Pass.State, HiddenStandbyObjectId);
        AssertNextContestedBattlefieldNotAdvanced(responseP1Pass);

        var assigned = await engine.ResolveAsync(
            responseP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-cleanup-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, ShadowResponseLegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        var battleClosedIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["previousControllerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal)
            && gameEvent.Payload["changed"] is true);
        var standbyRemovedEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_STANDBY_REMOVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONTROL_CLEANUP", StringComparison.Ordinal));
        var nextContestEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelStartedEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        Assert.True(battleClosedIndex < controlEventIndex);
        Assert.True(controlEventIndex < standbyRemovedEventIndex);
        Assert.True(standbyRemovedEventIndex < nextContestEventIndex);
        Assert.True(nextContestEventIndex < nextSpellDuelStartedEventIndex);

        var standbyRemovedEvent = assigned.Events[standbyRemovedEventIndex];
        Assert.Equal(
            [HiddenStandbyObjectId],
            Assert.IsAssignableFrom<IEnumerable<object?>>(standbyRemovedEvent.Payload["removedObjectIds"])
                .Cast<string>()
                .ToArray());
        Assert.Contains(HiddenStandbyObjectId, assigned.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(HiddenStandbyObjectId, assigned.State.PlayerZones["P2"].Battlefields);
        Assert.False(assigned.State.CardObjects[HiddenStandbyObjectId].IsFaceDown);
        Assert.Equal("P2", assigned.State.CardObjects[HiddenStandbyObjectId].ControllerId);
        var standbyLocation = assigned.State.ObjectLocations[HiddenStandbyObjectId];
        Assert.Equal("P2", standbyLocation.PlayerId);
        Assert.Equal("GRAVEYARD", standbyLocation.Zone);
        Assert.Null(standbyLocation.BattlefieldObjectId);

        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
                || string.Equals(task.ObjectId, HiddenStandbyObjectId, StringComparison.Ordinal));
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("P1", assigned.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, assigned.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, assigned.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationImmediateBattleAdvancesNextContestedBattlefieldTask()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-activation-immediate-advancement-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-activation-immediate-advancement-shadow", "P2", CommandTypes.ActivateAbility),
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
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-activation-immediate-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-immediate-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.Contains("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        var stackResolvedIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var abilityResolvedIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.True(stackResolvedIndex < abilityResolvedIndex);
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-immediate-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-immediate-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.False(responseP1Pass.State.BattleState.IsActive);
        Assert.Empty(responseP1Pass.State.StackItems);
        Assert.DoesNotContain(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(
            responseP1Pass.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));
        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var nextContestIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(responseClosedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, responseP1Pass.State.TimingState);
        Assert.Equal("P1", responseP1Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, responseP1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, responseP1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationAllowsNestedStandbyReactionStackBeforeReturningToResponse()
    {
        var state = BuildNestedStandbyReactionBattleResponseState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-nested-standby-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-nested-standby-shadow", "P2", CommandTypes.ActivateAbility),
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
        var shadowStackItem = Assert.Single(activated.State.StackItems);
        Assert.Equal(ShadowObjectId, shadowStackItem.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.ShadowStunAbilityEffectKind, shadowStackItem.EffectKind);
        Assert.Equal("P2", activated.State.PriorityPlayerId);
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-nested-standby-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        Assert.Equal("P1", stackP2Pass.State.PriorityPlayerId);
        Assert.Equal([shadowStackItem.StackItemId], stackP2Pass.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal(PromptTypes.StackPriority, stackP2Pass.Prompts["P1"].View?.Type);
        Assert.Contains(CommandTypes.RevealCard, stackP2Pass.Prompts["P1"].Actions);
        var revealCandidate = Assert.Single(
            stackP2Pass.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.RevealCard, StringComparison.Ordinal));
        Assert.True(revealCandidate.Enabled);
        Assert.Equal([StandbyReactionObjectId], (revealCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["STANDBY_REACTION"], (revealCandidate.Modes ?? []).Select(mode => mode.Id).ToArray());
        Assert.Equal(["STACK"], (revealCandidate.Destinations ?? []).Select(destination => destination.Id).ToArray());
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var revealed = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-nested-standby-reveal", "P1", CommandTypes.RevealCard),
            new RevealCardCommand(
                StandbyReactionObjectId,
                "OGN·121/298",
                [],
                Mode: "STANDBY_REACTION",
                OptionalCosts: ["STANDBY_REVEAL_0"],
                Destination: "STACK"),
            CancellationToken.None);

        Assert.True(revealed.Accepted, revealed.ErrorMessage);
        Assert.Equal(
            ["CARD_REVEALED", "CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"],
            revealed.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(2, revealed.State.StackItems.Count);
        Assert.Equal(shadowStackItem.StackItemId, revealed.State.StackItems[0].StackItemId);
        var standbyStackItem = revealed.State.StackItems[1];
        Assert.Equal(StandbyReactionObjectId, standbyStackItem.SourceObjectId);
        Assert.Equal("P1", standbyStackItem.ControllerId);
        Assert.Equal("OGN_TEEMO_STANDBY_DEFEND_REVEAL_PLAY_UNIT", standbyStackItem.EffectKind);
        Assert.Equal("OGN·121/298", standbyStackItem.CardNo);
        Assert.Equal(["STANDBY_REVEAL_0"], standbyStackItem.OptionalCosts);
        Assert.Equal("STACK", revealed.State.ObjectLocations[StandbyReactionObjectId].Zone);
        Assert.False(revealed.State.CardObjects[StandbyReactionObjectId].IsFaceDown);
        Assert.Equal("P1", revealed.State.PriorityPlayerId);
        Assert.DoesNotContain(revealed.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(revealed.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(revealed);

        var standbyP1Pass = await engine.ResolveAsync(
            revealed.State,
            new PlayerIntent("intent-natural-response-nested-standby-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(standbyP1Pass.Accepted, standbyP1Pass.ErrorMessage);
        Assert.Equal("P2", standbyP1Pass.State.PriorityPlayerId);
        Assert.Equal([shadowStackItem.StackItemId, standbyStackItem.StackItemId], standbyP1Pass.State.StackItems.Select(item => item.StackItemId).ToArray());
        AssertNextContestedBattlefieldNotAdvanced(standbyP1Pass);

        var standbyP2Pass = await engine.ResolveAsync(
            standbyP1Pass.State,
            new PlayerIntent("intent-natural-response-nested-standby-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(standbyP2Pass.Accepted, standbyP2Pass.ErrorMessage);
        Assert.Equal(["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_PLAYED_TO_BASE"], standbyP2Pass.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal([shadowStackItem.StackItemId], standbyP2Pass.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P2", standbyP2Pass.State.PriorityPlayerId);
        Assert.Contains(StandbyReactionObjectId, standbyP2Pass.State.PlayerZones["P1"].Base);
        Assert.False(standbyP2Pass.State.CardObjects[StandbyReactionObjectId].IsFaceDown);
        Assert.Equal("BASE", standbyP2Pass.State.ObjectLocations[StandbyReactionObjectId].Zone);
        Assert.DoesNotContain("STUNNED", standbyP2Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        AssertNextContestedBattlefieldNotAdvanced(standbyP2Pass);

        var shadowP2Pass = await engine.ResolveAsync(
            standbyP2Pass.State,
            new PlayerIntent("intent-natural-response-nested-shadow-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(shadowP2Pass.Accepted, shadowP2Pass.ErrorMessage);
        Assert.Equal("P1", shadowP2Pass.State.PriorityPlayerId);
        Assert.Equal([shadowStackItem.StackItemId], shadowP2Pass.State.StackItems.Select(item => item.StackItemId).ToArray());
        AssertNextContestedBattlefieldNotAdvanced(shadowP2Pass);

        var shadowP1Pass = await engine.ResolveAsync(
            shadowP2Pass.State,
            new PlayerIntent("intent-natural-response-nested-shadow-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(shadowP1Pass.Accepted, shadowP1Pass.ErrorMessage);
        Assert.Empty(shadowP1Pass.State.StackItems);
        Assert.True(shadowP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, shadowP1Pass.State.TimingState);
        Assert.Equal("P2", shadowP1Pass.State.PriorityPlayerId);
        Assert.Contains("STUNNED", shadowP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        var shadowResolvedIndex = EventIndex(shadowP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var abilityResolvedIndex = EventIndex(shadowP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        Assert.True(shadowResolvedIndex < abilityResolvedIndex);
        AssertNextContestedBattlefieldNotAdvanced(shadowP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            shadowP1Pass.State,
            new PlayerIntent("intent-natural-response-nested-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-nested-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.False(responseP1Pass.State.BattleState.IsActive);
        Assert.Empty(responseP1Pass.State.StackItems);
        Assert.DoesNotContain(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var nextContestIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(responseClosedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, responseP1Pass.State.TimingState);
        Assert.Equal("P1", responseP1Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, responseP1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, responseP1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseAllowsMultipleLegalSourcesSequentiallyBeforeAdvancement()
    {
        var state = BuildMultipleShadowBattleResponseState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-multiple-shadow-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.DoesNotContain(ShadowObjectId, openedResponse.State.BattleState.DefenderObjectIds);
        Assert.DoesNotContain(SecondShadowObjectId, openedResponse.State.BattleState.DefenderObjectIds);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.Equal(
            [SecondShadowObjectId, ShadowObjectId],
            EnabledActivateAbilitySourceIds(openedResponse.Prompts["P2"])
                .Where(sourceId => sourceId is ShadowObjectId or SecondShadowObjectId)
                .Order(StringComparer.Ordinal)
                .ToArray());
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activatedA = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-a", "P2", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                ShadowObjectId,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                [AttackerObjectId]),
            CancellationToken.None);

        Assert.True(activatedA.Accepted, activatedA.ErrorMessage);
        Assert.Equal(
            ["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"],
            activatedA.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(activatedA.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.False(activatedA.State.CardObjects[SecondShadowObjectId].IsExhausted);
        var shadowAStackItem = Assert.Single(activatedA.State.StackItems);
        Assert.Equal(ShadowObjectId, shadowAStackItem.SourceObjectId);
        Assert.Equal(new RunePool(1, 1), activatedA.State.RunePools["P2"]);
        AssertNextContestedBattlefieldNotAdvanced(activatedA);

        var shadowAStackP2Pass = await engine.ResolveAsync(
            activatedA.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-a-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(shadowAStackP2Pass.Accepted, shadowAStackP2Pass.ErrorMessage);
        Assert.Equal("P1", shadowAStackP2Pass.State.PriorityPlayerId);
        AssertNextContestedBattlefieldNotAdvanced(shadowAStackP2Pass);

        var shadowAStackP1Pass = await engine.ResolveAsync(
            shadowAStackP2Pass.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-a-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(shadowAStackP1Pass.Accepted, shadowAStackP1Pass.ErrorMessage);
        Assert.Empty(shadowAStackP1Pass.State.StackItems);
        Assert.True(shadowAStackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, shadowAStackP1Pass.State.TimingState);
        Assert.Equal("P2", shadowAStackP1Pass.State.PriorityPlayerId);
        Assert.True(shadowAStackP1Pass.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.False(shadowAStackP1Pass.State.CardObjects[SecondShadowObjectId].IsExhausted);
        Assert.Contains("STUNNED", shadowAStackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        Assert.Equal(
            [SecondShadowObjectId],
            EnabledActivateAbilitySourceIds(shadowAStackP1Pass.Prompts["P2"])
                .Where(sourceId => sourceId is ShadowObjectId or SecondShadowObjectId)
                .ToArray());
        AssertNextContestedBattlefieldNotAdvanced(shadowAStackP1Pass);

        var activatedB = await engine.ResolveAsync(
            shadowAStackP1Pass.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-b", "P2", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                SecondShadowObjectId,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                [AttackerObjectId]),
            CancellationToken.None);

        Assert.True(activatedB.Accepted, activatedB.ErrorMessage);
        Assert.Equal(
            ["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"],
            activatedB.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(activatedB.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.True(activatedB.State.CardObjects[SecondShadowObjectId].IsExhausted);
        var shadowBStackItem = Assert.Single(activatedB.State.StackItems);
        Assert.Equal(SecondShadowObjectId, shadowBStackItem.SourceObjectId);
        Assert.Equal(new RunePool(0, 0), activatedB.State.RunePools["P2"]);
        AssertNextContestedBattlefieldNotAdvanced(activatedB);

        var shadowBStackP2Pass = await engine.ResolveAsync(
            activatedB.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-b-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(shadowBStackP2Pass.Accepted, shadowBStackP2Pass.ErrorMessage);
        Assert.Equal("P1", shadowBStackP2Pass.State.PriorityPlayerId);
        AssertNextContestedBattlefieldNotAdvanced(shadowBStackP2Pass);

        var shadowBStackP1Pass = await engine.ResolveAsync(
            shadowBStackP2Pass.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-b-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(shadowBStackP1Pass.Accepted, shadowBStackP1Pass.ErrorMessage);
        Assert.Empty(shadowBStackP1Pass.State.StackItems);
        Assert.True(shadowBStackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, shadowBStackP1Pass.State.TimingState);
        Assert.Equal("P2", shadowBStackP1Pass.State.PriorityPlayerId);
        Assert.True(shadowBStackP1Pass.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.True(shadowBStackP1Pass.State.CardObjects[SecondShadowObjectId].IsExhausted);
        Assert.Empty(
            EnabledActivateAbilitySourceIds(shadowBStackP1Pass.Prompts["P2"])
                .Where(sourceId => sourceId is ShadowObjectId or SecondShadowObjectId)
                .ToArray());
        AssertNextContestedBattlefieldNotAdvanced(shadowBStackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            shadowBStackP1Pass.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-multiple-shadow-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.False(responseP1Pass.State.BattleState.IsActive);
        Assert.Empty(responseP1Pass.State.StackItems);
        Assert.DoesNotContain(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var nextContestIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(responseClosedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, responseP1Pass.State.TimingState);
        Assert.Equal("P1", responseP1Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, responseP1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, responseP1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationNoEffectForStaleTargetReturnsToResponseBeforeAdvancement()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [ShadowObjectId]);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-stale-target-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [ShadowObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-stale-target-shadow", "P2", CommandTypes.ActivateAbility),
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
        Assert.True(activated.State.CardObjects[AttackerObjectId].IsAttacking);
        Assert.Single(activated.State.StackItems);
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var staleCardObjects = new Dictionary<string, CardObjectState>(activated.State.CardObjects, StringComparer.Ordinal)
        {
            [AttackerObjectId] = activated.State.CardObjects[AttackerObjectId] with { IsAttacking = false }
        };
        var staleState = activated.State with { CardObjects = staleCardObjects };

        var stackP2Pass = await engine.ResolveAsync(
            staleState,
            new PlayerIntent("intent-natural-response-stale-target-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        Assert.Equal("P1", stackP2Pass.State.PriorityPlayerId);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-stale-target-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.False(stackP1Pass.State.CardObjects[AttackerObjectId].IsAttacking);
        Assert.DoesNotContain("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        var abilityResolvedIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        var noEffectIndex = EventIndex(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_NO_EFFECT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "TARGET_NO_LONGER_LEGAL", StringComparison.Ordinal));
        Assert.True(abilityResolvedIndex < noEffectIndex);
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-response-stale-target-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-stale-target-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.False(responseP1Pass.State.BattleState.IsActive);
        Assert.Empty(responseP1Pass.State.StackItems);
        Assert.DoesNotContain(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var nextContestIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(responseClosedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, responseP1Pass.State.TimingState);
        Assert.Equal("P1", responseP1Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, responseP1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, responseP1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationImmediateBattleSkipsCompletedCurrentBattlefieldBeforeAdvancingNextTask()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        Assert.Contains(
            BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId),
            state.UntilEndOfTurnEffects);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-activation-completed-current-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.DoesNotContain(ShadowObjectId, openedResponse.State.BattleState.AttackerObjectIds);
        Assert.DoesNotContain(ShadowObjectId, openedResponse.State.BattleState.DefenderObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-activation-completed-current-shadow", "P2", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                ShadowObjectId,
                P4ActivatedAbilityCatalog.ShadowStunAbilityId,
                [AttackerObjectId]),
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.True(activated.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Contains(ShadowObjectId, activated.State.PlayerZones["P2"].Battlefields);
        var activatedShadowLocation = activated.State.ObjectLocations[ShadowObjectId];
        Assert.Equal("P2", activatedShadowLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", activatedShadowLocation.Zone);
        Assert.Equal(BattlefieldObjectId, activatedShadowLocation.BattlefieldObjectId);
        Assert.Single(activated.State.StackItems);
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-activation-completed-current-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-completed-current-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.DoesNotContain(ShadowObjectId, stackP1Pass.State.BattleState.AttackerObjectIds);
        Assert.DoesNotContain(ShadowObjectId, stackP1Pass.State.BattleState.DefenderObjectIds);
        Assert.True(stackP1Pass.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Contains(ShadowObjectId, stackP1Pass.State.PlayerZones["P2"].Battlefields);
        var resolvedShadowLocation = stackP1Pass.State.ObjectLocations[ShadowObjectId];
        Assert.Equal("P2", resolvedShadowLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", resolvedShadowLocation.Zone);
        Assert.Equal(BattlefieldObjectId, resolvedShadowLocation.BattlefieldObjectId);
        Assert.Contains("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-completed-current-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var responseP1Pass = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-completed-current-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP1Pass.Accepted, responseP1Pass.ErrorMessage);
        Assert.False(responseP1Pass.State.BattleState.IsActive);
        Assert.Empty(responseP1Pass.State.StackItems);
        Assert.Contains(
            BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId),
            responseP1Pass.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Payload.TryGetValue("battlefieldObjectId", out var value) ? value as string : null, BattlefieldObjectId, StringComparison.Ordinal)
            && (string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
                || string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            responseP1Pass.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));

        var finalShadowLocation = responseP1Pass.State.ObjectLocations[ShadowObjectId];
        Assert.Contains(ShadowObjectId, responseP1Pass.State.PlayerZones["P2"].Battlefields);
        Assert.True(responseP1Pass.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Equal("P2", finalShadowLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", finalShadowLocation.Zone);
        Assert.Equal(BattlefieldObjectId, finalShadowLocation.BattlefieldObjectId);
        if (responseP1Pass.State.BattlefieldStates.TryGetValue(BattlefieldObjectId, out var completedBattlefield)
            && completedBattlefield.Contested)
        {
            Assert.Contains(ShadowObjectId, completedBattlefield.OccupantObjectIds);
            Assert.Contains("P2", completedBattlefield.OccupantControllerIds);
        }

        var responseClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var nextContestIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(responseP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(responseClosedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
        Assert.Equal(TimingStates.SpellDuelOpen, responseP1Pass.State.TimingState);
        Assert.Equal("P1", responseP1Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", responseP1Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", responseP1Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, responseP1Pass.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, responseP1Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, responseP1Pass.Prompts["P1"].View?.Type);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationPreservesNonParticipantSourceBattlefieldLocationAfterStackResolution()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        var initialShadowLocation = state.ObjectLocations[ShadowObjectId];
        Assert.Equal("P2", initialShadowLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", initialShadowLocation.Zone);
        Assert.Equal(BattlefieldObjectId, initialShadowLocation.BattlefieldObjectId);
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-nonparticipant-location-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal([BulwarkDefenderObjectId], openedResponse.State.BattleState.DefenderObjectIds);
        Assert.DoesNotContain(ShadowObjectId, openedResponse.State.BattleState.AttackerObjectIds);
        Assert.DoesNotContain(ShadowObjectId, openedResponse.State.BattleState.DefenderObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-nonparticipant-location-shadow", "P2", CommandTypes.ActivateAbility),
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
        Assert.Contains(ShadowObjectId, activated.State.PlayerZones["P2"].Battlefields);
        var activatedShadowLocation = activated.State.ObjectLocations[ShadowObjectId];
        Assert.Equal("P2", activatedShadowLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", activatedShadowLocation.Zone);
        Assert.Equal(BattlefieldObjectId, activatedShadowLocation.BattlefieldObjectId);
        Assert.Single(activated.State.StackItems);
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-nonparticipant-location-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-nonparticipant-location-stack-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP1Pass.Accepted, stackP1Pass.ErrorMessage);
        Assert.Empty(stackP1Pass.State.StackItems);
        Assert.True(stackP1Pass.State.BattleState.IsActive);
        Assert.Equal([BulwarkDefenderObjectId], stackP1Pass.State.BattleState.DefenderObjectIds);
        Assert.DoesNotContain(ShadowObjectId, stackP1Pass.State.BattleState.AttackerObjectIds);
        Assert.DoesNotContain(ShadowObjectId, stackP1Pass.State.BattleState.DefenderObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, stackP1Pass.State.TimingState);
        Assert.Equal("P2", stackP1Pass.State.PriorityPlayerId);
        Assert.True(stackP1Pass.State.CardObjects[ShadowObjectId].IsExhausted);
        Assert.Contains(ShadowObjectId, stackP1Pass.State.PlayerZones["P2"].Battlefields);
        var resolvedShadowLocation = stackP1Pass.State.ObjectLocations[ShadowObjectId];
        Assert.Equal("P2", resolvedShadowLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", resolvedShadowLocation.Zone);
        Assert.Equal(BattlefieldObjectId, resolvedShadowLocation.BattlefieldObjectId);
        Assert.Contains("STUNNED", stackP1Pass.State.CardObjects[AttackerObjectId].UntilEndOfTurnEffects);
        Assert.Contains(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(stackP1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.ShadowStunAbilityId, StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationPostPaymentBlocksNextContestedBattlefieldUntilAccepted()
    {
        var (engine, openedPayment, payment) = await OpenIcevaleShadowActivationPostPaymentAsync();

        var paid = await engine.ResolveAsync(
            openedPayment.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-pay", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, IcevaleTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["paymentWindow"] as string, TriggerPaymentWindow, StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, IcevaleTrigger, StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, BulwarkDefenderObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, IcevaleTrigger, StringComparison.Ordinal));
        Assert.Equal(1, paid.State.CardObjects[BulwarkDefenderObjectId].Power);
        Assert.Equal(-1, paid.State.CardObjects[BulwarkDefenderObjectId].UntilEndOfTurnPowerModifier);
        AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(paid, declined: false);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationPostPaymentDeclineAdvancesNextContestedBattlefield()
    {
        var (engine, openedPayment, payment) = await OpenIcevaleShadowActivationPostPaymentAsync();

        var declined = await engine.ResolveAsync(
            openedPayment.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Equal(1, declined.State.RunePools["P1"].Mana);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, IcevaleTrigger, StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, IcevaleTrigger, StringComparison.Ordinal));
        Assert.Equal(2, declined.State.CardObjects[BulwarkDefenderObjectId].Power);
        Assert.Equal(0, declined.State.CardObjects[BulwarkDefenderObjectId].UntilEndOfTurnPowerModifier);
        AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(declined, declined: true);
    }

    [Fact]
    public async Task NaturalBattleResponseActivationPostPaymentRejectKeepsNextContestedBattlefieldBlocked()
    {
        var (engine, openedPayment, payment) = await OpenIcevaleShadowActivationPostPaymentAsync();

        var rejected = await engine.ResolveAsync(
            openedPayment.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-reject", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:2"]),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(openedPayment.State), MatchStateHasher.Hash(rejected.State));
        Assert.NotNull(rejected.State.PendingPayment);
        AssertNextContestedBattlefieldNotAdvanced(rejected);
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
    public async Task NaturalBattlefieldControlCleanupRemovesIllegalStandbyBeforeAdvancingNextContestedTask()
    {
        var state = BuildControlChangeStandbyCleanupNaturalStartBattleState();
        AssertOpponentHiddenStandbyRedacted(ResolutionResult.BuildSnapshots(state)["P1"], HiddenStandbyObjectId);
        AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(state, HiddenStandbyObjectId);

        var opened = await DeclareAssignmentBattleAsync(state);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.True(opened.State.BattleState.IsActive);
        Assert.Equal(PromptTypes.AssignCombatDamage, opened.Prompts["P1"].View?.Type);
        Assert.Equal($"task:start-battle:{BattlefieldObjectId}", opened.State.PendingTaskQueue.ActiveTaskId);
        AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(opened.State, HiddenStandbyObjectId);

        var assigned = await new CoreRuleEngine().ResolveAsync(
            opened.State,
            new PlayerIntent("intent-natural-control-cleanup-assign-damage", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, LegalAssignments()),
            CancellationToken.None);

        Assert.True(assigned.Accepted, assigned.ErrorMessage);
        var controlEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["previousControllerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal)
            && gameEvent.Payload["changed"] is true);
        var standbyRemovedEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_STANDBY_REMOVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONTROL_CLEANUP", StringComparison.Ordinal));
        var nextContestEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelStartedEventIndex = EventIndex(assigned.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        Assert.True(controlEventIndex < standbyRemovedEventIndex);
        Assert.True(standbyRemovedEventIndex < nextContestEventIndex);
        Assert.True(nextContestEventIndex < nextSpellDuelStartedEventIndex);

        var standbyRemovedEvent = assigned.Events[standbyRemovedEventIndex];
        Assert.Equal(
            [HiddenStandbyObjectId],
            Assert.IsAssignableFrom<IEnumerable<object?>>(standbyRemovedEvent.Payload["removedObjectIds"])
                .Cast<string>()
                .ToArray());
        Assert.Contains(HiddenStandbyObjectId, assigned.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(HiddenStandbyObjectId, assigned.State.PlayerZones["P2"].Battlefields);
        Assert.False(assigned.State.CardObjects[HiddenStandbyObjectId].IsFaceDown);
        Assert.Equal("P2", assigned.State.CardObjects[HiddenStandbyObjectId].ControllerId);
        var standbyLocation = assigned.State.ObjectLocations[HiddenStandbyObjectId];
        Assert.Equal("P2", standbyLocation.PlayerId);
        Assert.Equal("GRAVEYARD", standbyLocation.Zone);
        Assert.Null(standbyLocation.BattlefieldObjectId);

        Assert.False(assigned.State.BattleState.IsActive);
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(
            assigned.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
                || string.Equals(task.ObjectId, HiddenStandbyObjectId, StringComparison.Ordinal));
        Assert.Equal(TimingStates.SpellDuelOpen, assigned.State.TimingState);
        Assert.Equal("SPELL_DUEL_TASKS", assigned.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", assigned.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, assigned.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, assigned.Prompts["P1"].View?.RelatedBattlefieldId);
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

    private static async Task<(CoreRuleEngine Engine, ResolutionResult OpenedPayment, PendingPaymentState Payment)> OpenIcevaleShadowActivationPostPaymentAsync()
    {
        var state = BuildIcevaleShadowActivationPostPaymentState();
        var engine = new CoreRuleEngine();

        var openedResponse = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-natural-response-activation-post-payment-declare-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BulwarkDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"],
                BattlefieldTargetObjectIds: [BulwarkDefenderObjectId]),
            CancellationToken.None);

        Assert.True(openedResponse.Accepted, openedResponse.ErrorMessage);
        Assert.True(openedResponse.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralClosed, openedResponse.State.TimingState);
        Assert.Equal("P2", openedResponse.State.PriorityPlayerId);
        Assert.Contains(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        Assert.DoesNotContain(openedResponse.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedResponse);

        var activated = await engine.ResolveAsync(
            openedResponse.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-shadow", "P2", CommandTypes.ActivateAbility),
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
        AssertNextContestedBattlefieldNotAdvanced(activated);

        var stackP2Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-stack-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(stackP2Pass.Accepted, stackP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(stackP2Pass);

        var stackP1Pass = await engine.ResolveAsync(
            stackP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-stack-p1-pass", "P1", CommandTypes.PassPriority),
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
        AssertNextContestedBattlefieldNotAdvanced(stackP1Pass);

        var responseP2Pass = await engine.ResolveAsync(
            stackP1Pass.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-response-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(responseP2Pass.Accepted, responseP2Pass.ErrorMessage);
        AssertNextContestedBattlefieldNotAdvanced(responseP2Pass);

        var openedPayment = await engine.ResolveAsync(
            responseP2Pass.State,
            new PlayerIntent("intent-natural-response-activation-post-payment-response-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(openedPayment.Accepted, openedPayment.ErrorMessage);
        Assert.False(openedPayment.State.BattleState.IsActive);
        Assert.Empty(openedPayment.State.StackItems);
        Assert.NotNull(openedPayment.State.PendingPayment);
        Assert.Equal(TriggerPaymentWindow, openedPayment.State.PendingPayment?.PaymentWindow);
        Assert.Equal("P1", openedPayment.State.PendingPayment?.PlayerId);
        Assert.DoesNotContain(openedPayment.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DAMAGE_ASSIGNMENT_OPENED", StringComparison.Ordinal));
        AssertNextContestedBattlefieldNotAdvanced(openedPayment);

        var responseClosedIndex = EventIndex(openedPayment.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_RESPONSE_PRIORITY_CLOSED", StringComparison.Ordinal));
        var battleClosedIndex = EventIndex(openedPayment.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var controlResolvedIndex = EventIndex(openedPayment.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, BattlefieldObjectId, StringComparison.Ordinal));
        var paymentOpenedIndex = EventIndex(openedPayment.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, IcevaleTrigger, StringComparison.Ordinal));

        Assert.True(responseClosedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlResolvedIndex);
        Assert.True(controlResolvedIndex < paymentOpenedIndex);
        Assert.DoesNotContain(
            openedPayment.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, BattlefieldObjectId, StringComparison.Ordinal));

        var payment = openedPayment.State.PendingPayment;
        Assert.NotNull(payment);
        return (engine, openedPayment, payment);
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

    private static MatchState BuildNoResultShadowActivationNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            includeSecondAttacker: true,
            defenderObjectIds: [BulwarkDefenderObjectId, BackRowDefenderObjectId]);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[AttackerObjectId] = cardObjects[AttackerObjectId] with { Power = 3 };
        cardObjects[SecondAttackerObjectId] = cardObjects[SecondAttackerObjectId] with { Power = 5, Damage = 4 };
        cardObjects[BackRowDefenderObjectId] = cardObjects[BackRowDefenderObjectId] with { Power = 4 };
        return state with { CardObjects = cardObjects };
    }

    private static MatchState BuildControlChangeStandbyCleanupNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(includeNextContest: true);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P2"] = state.PlayerZones["P2"] with
            {
                Battlefields = state.PlayerZones["P2"].Battlefields
                    .Concat([HiddenStandbyObjectId])
                    .ToArray()
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = state.CardObjects[BattlefieldObjectId] with
            {
                OwnerId = "P2",
                ControllerId = "P2"
            },
            [HiddenStandbyObjectId] = new CardObjectState(
                HiddenStandbyObjectId,
                cardNo: null,
                isFaceDown: true,
                power: 1,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: "P2",
                controllerId: "P2")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [HiddenStandbyObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildShadowActivationStandbyCleanupNaturalStartBattleState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId, ShadowObjectId]);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P2"] = state.PlayerZones["P2"] with
            {
                Battlefields = state.PlayerZones["P2"].Battlefields
                    .Concat([HiddenStandbyObjectId])
                    .ToArray()
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = state.CardObjects[BattlefieldObjectId] with
            {
                OwnerId = "P2",
                ControllerId = "P2"
            },
            [HiddenStandbyObjectId] = new CardObjectState(
                HiddenStandbyObjectId,
                cardNo: null,
                isFaceDown: true,
                power: 1,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: "P2",
                controllerId: "P2")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [HiddenStandbyObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildIcevaleShadowActivationPostPaymentState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [AttackerObjectId] = state.CardObjects[AttackerObjectId] with
            {
                CardNo = "UNL-065/219",
                Power = 5
            }
        };
        return state with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = new(1, 1)
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState BuildNestedStandbyReactionBattleResponseState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P1"] = state.PlayerZones["P1"] with
            {
                Base = state.PlayerZones["P1"].Base
                    .Concat([StandbyReactionObjectId])
                    .ToArray()
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [StandbyReactionObjectId] = new CardObjectState(
                StandbyReactionObjectId,
                cardNo: "OGN·121/298",
                isFaceDown: true,
                power: 2,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                manaCost: 2,
                ownerId: "P1",
                controllerId: "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [StandbyReactionObjectId] = new("P1", "BASE")
        };
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildMultipleShadowBattleResponseState()
    {
        var state = BuildNaturalStartBattleState(
            includeShadowResponse: true,
            includeNextContest: true,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            ["P2"] = state.PlayerZones["P2"] with
            {
                Battlefields = state.PlayerZones["P2"].Battlefields
                    .Concat([SecondShadowObjectId])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            }
        };
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [SecondShadowObjectId] = Unit(
                SecondShadowObjectId,
                "P2",
                power: 1,
                cardNo: P4ActivatedAbilityCatalog.ShadowCardNo)
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            [SecondShadowObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        return state with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 2)
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
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

    private static void AssertNextContestedBattlefieldNotAdvanced(ResolutionResult result)
    {
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        Assert.NotEqual(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.NotEqual("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.NotEqual($"task:start-spell-duel:{NextBattlefieldObjectId}", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.NotEqual(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
    }

    private static IReadOnlyList<string> EnabledActivateAbilitySourceIds(ActionPromptDto prompt)
    {
        return (prompt.Candidates ?? [])
            .Where(candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal)
                && candidate.Enabled)
            .SelectMany(candidate => candidate.Sources ?? [])
            .Select(source => source.Id)
            .ToArray();
    }

    private static void AssertNextContestedBattlefieldAdvancedAfterPaymentClosed(
        ResolutionResult result,
        bool declined)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{NextBattlefieldObjectId}", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
        Assert.Equal(NextBattlefieldObjectId, result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.AssignCombatDamage, result.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, result.Prompts["P1"].View?.Type);

        var paymentClosedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["declined"], declined));
        var nextContestIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));
        var nextSpellDuelIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, NextBattlefieldObjectId, StringComparison.Ordinal));

        Assert.True(paymentClosedIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
    }

    private static void AssertHiddenStandbyIdentityRedactedFromUnauthorizedProjection(
        MatchState state,
        string hiddenObjectId)
    {
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.DoesNotContain(hiddenObjectId, JsonSerializer.Serialize(session.SnapshotFor("P1")), StringComparison.Ordinal);
        Assert.DoesNotContain(hiddenObjectId, JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(state)), StringComparison.Ordinal);
        Assert.DoesNotContain(hiddenObjectId, JsonSerializer.Serialize(session.PromptFor("P1")), StringComparison.Ordinal);
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

    private static int EventIndex(IReadOnlyList<GameEvent> events, Predicate<GameEvent> predicate)
    {
        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            if (predicate(events[eventIndex]))
            {
                return eventIndex;
            }
        }

        Assert.Fail("Expected event was not emitted.");
        return -1;
    }
}
