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
    private const string BulwarkDefenderObjectId = "P2-A-BULWARK";
    private const string BackRowDefenderObjectId = "P2-Z-BACKROW";
    private const string HiddenStandbyObjectId = "P2-HIDDEN-STANDBY";

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

    private static async Task<ResolutionResult> DeclareAssignmentBattleAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
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

    private static MatchState BuildNaturalStartBattleState(
        bool includeHiddenStandby = false,
        IReadOnlyList<string>? defenderObjectIds = null)
    {
        var defenders = defenderObjectIds ?? [BulwarkDefenderObjectId, BackRowDefenderObjectId];
        var p2Battlefields = defenders
            .Concat(includeHiddenStandby ? [HiddenBattlefieldObjectId, HiddenStandbyObjectId] : [])
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
                    Battlefields = p2Battlefields
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: BuildCardObjects(includeHiddenStandby),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)],
            objectLocations: BuildObjectLocations(includeHiddenStandby));
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects(bool includeHiddenStandby)
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

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations(bool includeHiddenStandby)
    {
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [BulwarkDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId),
            [BackRowDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };

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
        IReadOnlyList<string>? tags = null)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
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
