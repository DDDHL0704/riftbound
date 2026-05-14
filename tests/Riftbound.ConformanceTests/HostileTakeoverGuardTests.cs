using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HostileTakeoverGuardTests
{
    private const string ControlBattlefieldObjectId = "BF-CONTROL";
    private const string ControlTargetObjectId = "P2-CONTROL-TARGET";
    private const string ControlRemainingOccupantObjectId = "P2-CONTROL-REMAINING";

    [Fact]
    public async Task HostileTakeoverGainsControlReadiesEnemyBattlefieldUnitAndSchedulesReturn()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHostileTakeoverState();

        var played = await PlayHostileTakeoverAsync(engine, state, "P2-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hostile-takeover-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hostile-takeover-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("P2-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("P2", p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].OwnerId);
        Assert.Equal("P1", p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].ControllerId);
        Assert.False(p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(2, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(5, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].Power);
        Assert.Contains(
            "RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2",
            p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnEffects);
        Assert.Equal(["P1-SPELL-HOSTILE-TAKEOVER"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-HOSTILE-TAKEOVER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("wasExhausted", out var wasExhausted)
            && wasExhausted is true);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONTROL_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-HOSTILE-TAKEOVER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["previousControllerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BATTLEFIELD", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-FRIENDLY-BATTLEFIELD-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-HAND-UNIT")]
    public async Task HostileTakeoverRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildHostileTakeoverState();

        var result = await PlayHostileTakeoverAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HOSTILE-TAKEOVER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-BATTLEFIELD-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-HAND-UNIT"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                "P2-BATTLEFIELD-UNIT",
                "P2-FACE-DOWN-STANDBY",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Equal("P2", result.State.CardObjects["P2-BATTLEFIELD-UNIT"].OwnerId);
        Assert.Equal("P2", result.State.CardObjects["P2-BATTLEFIELD-UNIT"].ControllerId);
        Assert.True(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Empty(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONTROL_GAINED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostileTakeoverPreservesPreciseBattlefieldAndStartsContestTasksWhenOpponentOccupantRemains()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHostileTakeoverPreciseBattlefieldState(includeRemainingOpponentOccupant: true);

        var played = await PlayHostileTakeoverAsync(engine, state, ControlTargetObjectId);
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hostile-takeover-precise-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hostile-takeover-precise-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal("P2", p2Pass.State.CardObjects[ControlTargetObjectId].OwnerId);
        Assert.Equal("P1", p2Pass.State.CardObjects[ControlTargetObjectId].ControllerId);
        Assert.Contains(ControlTargetObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(ControlTargetObjectId, p2Pass.State.PlayerZones["P2"].Battlefields);

        var targetLocation = p2Pass.State.ObjectLocations[ControlTargetObjectId];
        Assert.Equal("P1", targetLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", targetLocation.Zone);
        Assert.Equal(ControlBattlefieldObjectId, targetLocation.BattlefieldObjectId);

        var battlefield = p2Pass.State.BattlefieldStates[ControlBattlefieldObjectId];
        Assert.True(battlefield.Contested);
        Assert.Equal(["P1", "P2"], battlefield.OccupantControllerIds);
        Assert.Contains(ControlTargetObjectId, battlefield.OccupantObjectIds);
        Assert.Contains(ControlRemainingOccupantObjectId, battlefield.OccupantObjectIds);
        Assert.Equal(TimingStates.SpellDuelOpen, p2Pass.State.TimingState);
        Assert.Equal("P1", p2Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", p2Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{ControlBattlefieldObjectId}", p2Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            p2Pass.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, ControlBattlefieldObjectId, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, ControlBattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["taskId"] as string, $"task:start-spell-duel:{ControlBattlefieldObjectId}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostileTakeoverPreservesPreciseBattlefieldWithoutStartingContestWhenNoOpponentOccupantRemains()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHostileTakeoverPreciseBattlefieldState(includeRemainingOpponentOccupant: false);

        var played = await PlayHostileTakeoverAsync(engine, state, ControlTargetObjectId);
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hostile-takeover-no-contest-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hostile-takeover-no-contest-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var targetLocation = p2Pass.State.ObjectLocations[ControlTargetObjectId];
        Assert.Equal("P1", targetLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", targetLocation.Zone);
        Assert.Equal(ControlBattlefieldObjectId, targetLocation.BattlefieldObjectId);

        var battlefield = p2Pass.State.BattlefieldStates[ControlBattlefieldObjectId];
        Assert.False(battlefield.Contested);
        Assert.Equal(["P1"], battlefield.OccupantControllerIds);
        Assert.Equal("IDLE", p2Pass.State.PendingTaskQueue.Phase);
        Assert.Empty(p2Pass.State.PendingTaskQueue.Tasks);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayHostileTakeoverAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-hostile-takeover-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-HOSTILE-TAKEOVER",
                "SFD·202/221",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static MatchState BuildHostileTakeoverState()
    {
        return new MatchState(
            roomId: "hostile-takeover-guard-test",
            tick: 0,
            turnNumber: 1,
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
                ["P1"] = new(5, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HOSTILE-TAKEOVER"],
                    Battlefields = ["P1-FRIENDLY-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-UNIT"],
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-UNIT",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HOSTILE-TAKEOVER"] = new(
                    "P1-SPELL-HOSTILE-TAKEOVER",
                    cardNo: "SFD·202/221",
                    manaCost: 5,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-BATTLEFIELD-UNIT"] = new(
                    "P1-FRIENDLY-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT"] = new(
                    "P2-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    damage: 2,
                    power: 5,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BASE-UNIT"] = new(
                    "P2-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-STALE-UNIT"] = new(
                    "P2-STALE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FACE-DOWN-STANDBY"] = new(
                    "P2-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SPELL"] = new(
                    "P2-BATTLEFIELD-SPELL",
                    cardNo: "OGN·169/298",
                    power: 1,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-RUNE"] = new(
                    "P2-BATTLEFIELD-RUNE",
                    cardNo: "RUNES·001",
                    power: 1,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-HAND-UNIT"] = new(
                    "P2-HAND-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static MatchState BuildHostileTakeoverPreciseBattlefieldState(bool includeRemainingOpponentOccupant)
    {
        var p2Battlefields = includeRemainingOpponentOccupant
            ? new[] { ControlBattlefieldObjectId, ControlTargetObjectId, ControlRemainingOccupantObjectId }
            : [ControlBattlefieldObjectId, ControlTargetObjectId];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-HOSTILE-TAKEOVER"] = new(
                "P1-SPELL-HOSTILE-TAKEOVER",
                cardNo: "SFD·202/221",
                manaCost: 5,
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1"),
            [ControlBattlefieldObjectId] = new(
                ControlBattlefieldObjectId,
                cardNo: "SFD·214/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            [ControlTargetObjectId] = new(
                ControlTargetObjectId,
                cardNo: "SFD·125/221",
                damage: 2,
                power: 5,
                isExhausted: true,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2")
        };
        if (includeRemainingOpponentOccupant)
        {
            cardObjects[ControlRemainingOccupantObjectId] = new(
                ControlRemainingOccupantObjectId,
                cardNo: "SFD·126/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-HOSTILE-TAKEOVER"] = new("P1", "HAND"),
            [ControlBattlefieldObjectId] = new("P2", "BATTLEFIELD"),
            [ControlTargetObjectId] = new("P2", "BATTLEFIELD", ControlBattlefieldObjectId)
        };
        if (includeRemainingOpponentOccupant)
        {
            objectLocations[ControlRemainingOccupantObjectId] =
                new ObjectLocationState("P2", "BATTLEFIELD", ControlBattlefieldObjectId);
        }

        return new MatchState(
            roomId: "hostile-takeover-precise-battlefield-test",
            tick: 0,
            turnNumber: 1,
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
                ["P1"] = new(5, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HOSTILE-TAKEOVER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }
}
