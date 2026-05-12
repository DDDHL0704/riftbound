using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldContestBattleTaskGuardTests
{
    [Fact]
    public void ActiveStartBattlePromptOnlyExposesCurrentBattlefieldUnitsForActivePlayer()
    {
        var state = BuildActiveStartBattleGuardState();

        Assert.Equal("BATTLE_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", state.PendingTaskQueue.ActiveTaskId);

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.True(prompts["P1"].Actionable);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], prompts["P1"].Actions);
        Assert.False(prompts["P2"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompts["P2"].Actions);

        var declareBattleCandidate = Assert.Single(
            prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(declareBattleCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(sourceRequirements);

        Assert.Equal("P1-ATTACKER-VALID", sourceRequirement["sourceObjectId"]);

        var attackerChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["attackerChoicesByIndex"]);
        var defenderChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        var battlefieldChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["battlefieldChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        var attackerChoiceIds = attackerChoicesByIndex
            .SelectMany(pair => pair.Value)
            .Select(choice => choice.Id)
            .ToArray();
        var defenderChoiceIds = defenderChoicesByIndex
            .SelectMany(pair => pair.Value)
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Equal(["BF-1"], battlefieldChoices);
        Assert.Equal(["P1-ATTACKER-VALID"], attackerChoiceIds);
        Assert.Equal(["P2-DEFENDER-VALID"], defenderChoiceIds);

        Assert.DoesNotContain("P1-OTHER-BATTLEFIELD-UNIT", attackerChoiceIds);
        Assert.DoesNotContain("P1-BASE-UNIT", attackerChoiceIds);
        Assert.DoesNotContain("P1-STALE-UNIT", attackerChoiceIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", attackerChoiceIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-EQUIPMENT", attackerChoiceIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-SPELL", attackerChoiceIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-RUNE", attackerChoiceIds);
        Assert.DoesNotContain("P2-OTHER-BATTLEFIELD-UNIT", defenderChoiceIds);
        Assert.DoesNotContain("P2-BASE-UNIT", defenderChoiceIds);
        Assert.DoesNotContain("P2-STALE-UNIT", defenderChoiceIds);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", defenderChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", defenderChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-SPELL", defenderChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-RUNE", defenderChoiceIds);
    }

    [Theory]
    [InlineData("BF-2", "P1-ATTACKER-VALID", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-OTHER-BATTLEFIELD-UNIT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BASE-UNIT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-STALE-UNIT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-FACE-DOWN-STANDBY", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BATTLEFIELD-EQUIPMENT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BATTLEFIELD-SPELL", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BATTLEFIELD-RUNE", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-OTHER-BATTLEFIELD-UNIT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BASE-UNIT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-STALE-UNIT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-FACE-DOWN-STANDBY")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BATTLEFIELD-SPELL")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BATTLEFIELD-RUNE")]
    public async Task ActiveStartBattleRejectsInvalidDeclareBattleWithoutMutation(
        string battlefieldId,
        string attackerObjectId,
        string defenderObjectId)
    {
        var state = BuildActiveStartBattleGuardState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-invalid-start-battle-{attackerObjectId}-{defenderObjectId}", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                battlefieldId,
                [attackerObjectId],
                [defenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        AssertRejectedWithoutMutation(state, result);
    }

    [Fact]
    public async Task ActiveStartBattleDeclareBattleClearsTaskAndPreservesRepresentativeEvents()
    {
        var state = BuildActiveStartBattleGuardState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-valid-start-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BF-1",
                ["P1-ATTACKER-VALID"],
                ["P2-DEFENDER-VALID"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(10, result.State.Tick);
        Assert.NotEqual("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.False(result.State.BattleState.IsActive);
        Assert.False(result.State.CardObjects["P1-ATTACKER-VALID"].IsAttacking);
        Assert.Equal("P1", result.State.CardObjects["BF-1"].ControllerId);
        Assert.DoesNotContain("P2-DEFENDER-VALID", result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-DEFENDER-VALID", result.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-DEFENDER-VALID", StringComparison.Ordinal));
    }

    private static void AssertRejectedWithoutMutation(MatchState state, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P1-ATTACKER-VALID", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("P2-DEFENDER-VALID", result.State.PlayerZones["P2"].Battlefields);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].CardNo);
    }

    private static MatchState BuildActiveStartBattleGuardState()
    {
        return new MatchState(
            roomId: "battlefield-contest-battle-task-guard-room",
            tick: 9,
            turnNumber: 3,
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
                    Base = ["P1-BASE-UNIT"],
                    Battlefields =
                    [
                        "BF-1",
                        "BF-2",
                        "P1-ATTACKER-VALID",
                        "P1-OTHER-BATTLEFIELD-UNIT",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-DEFENDER-VALID",
                        "BF-3",
                        "P2-OTHER-BATTLEFIELD-UNIT",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE"
                    ]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: BuildCardObjects(),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")],
            objectLocations: BuildObjectLocations());
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF-1"] = Battlefield("BF-1", "P1"),
            ["BF-2"] = Battlefield("BF-2", "P1"),
            ["BF-3"] = Battlefield("BF-3", "P2"),
            ["P1-ATTACKER-VALID"] = Unit("P1-ATTACKER-VALID", "P1", power: 4),
            ["P1-OTHER-BATTLEFIELD-UNIT"] = Unit("P1-OTHER-BATTLEFIELD-UNIT", "P1"),
            ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", "P1"),
            ["P1-FACE-DOWN-STANDBY"] = HiddenStandbyUnit("P1-FACE-DOWN-STANDBY", "P1"),
            ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit(
                "P1-BATTLEFIELD-EQUIPMENT",
                "SFD·139/221",
                CardObjectTags.EquipmentCard,
                "P1",
                attachedToObjectId: "P1-ATTACKER-VALID"),
            ["P1-BATTLEFIELD-SPELL"] = NonUnit("P1-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P1"),
            ["P1-BATTLEFIELD-RUNE"] = NonUnit("P1-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P1"),
            ["P2-DEFENDER-VALID"] = Unit("P2-DEFENDER-VALID", "P2", power: 2),
            ["P2-OTHER-BATTLEFIELD-UNIT"] = Unit("P2-OTHER-BATTLEFIELD-UNIT", "P2"),
            ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", "P2"),
            ["P2-FACE-DOWN-STANDBY"] = HiddenStandbyUnit("P2-FACE-DOWN-STANDBY", "P2"),
            ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit(
                "P2-BATTLEFIELD-EQUIPMENT",
                "SFD·139/221",
                CardObjectTags.EquipmentCard,
                "P2",
                attachedToObjectId: "P2-DEFENDER-VALID"),
            ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
            ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2")
        };
    }

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations()
    {
        return new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["BF-2"] = new("P1", "BATTLEFIELD", "BF-2"),
            ["BF-3"] = new("P2", "BATTLEFIELD", "BF-3"),
            ["P1-ATTACKER-VALID"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-OTHER-BATTLEFIELD-UNIT"] = new("P1", "BATTLEFIELD", "BF-2"),
            ["P1-BASE-UNIT"] = new("P1", "BASE"),
            ["P1-FACE-DOWN-STANDBY"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-BATTLEFIELD-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-BATTLEFIELD-SPELL"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-BATTLEFIELD-RUNE"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P2-DEFENDER-VALID"] = new("P2", "BATTLEFIELD", "BF-1"),
            ["P2-OTHER-BATTLEFIELD-UNIT"] = new("P2", "BATTLEFIELD", "BF-3"),
            ["P2-BASE-UNIT"] = new("P2", "BASE"),
            ["P2-FACE-DOWN-STANDBY"] = new("P2", "BATTLEFIELD", "BF-3"),
            ["P2-BATTLEFIELD-EQUIPMENT"] = new("P2", "BATTLEFIELD", "BF-1"),
            ["P2-BATTLEFIELD-SPELL"] = new("P2", "BATTLEFIELD", "BF-1"),
            ["P2-BATTLEFIELD-RUNE"] = new("P2", "BATTLEFIELD", "BF-1")
        };
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
        int power = 2)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState HiddenStandbyUnit(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: null,
            isFaceDown: true,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId,
        string attachedToObjectId = "")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId,
            attachedToObjectId: attachedToObjectId);
    }
}
