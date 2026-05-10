using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TreasureHunterMoveTriggerTests
{
    private const string TreasureHunterTrigger = "TREASURE_HUNTER_MOVE_CREATE_GOLD";

    [Fact]
    public async Task TreasureHunterMoveCreatesDormantGoldToken()
    {
        var result = await MoveTreasureHunterAsync(BuildTreasureHunterMoveState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenObjectId = Assert.Single(GoldTokenIds(result.State));
        Assert.Equal([tokenObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TREASURE-HUNTER"], result.State.PlayerZones["P1"].Battlefields);

        var tokenState = result.State.CardObjects[tokenObjectId];
        Assert.True(tokenState.IsExhausted);
        Assert.Equal("P1", tokenState.OwnerId);
        Assert.Equal("P1", tokenState.ControllerId);
        Assert.Contains(CardObjectTags.EquipmentCard, tokenState.Tags);
        Assert.Contains("金币", tokenState.Tags);
        Assert.Contains("反应", tokenState.Tags);

        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TreasureHunterTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-TREASURE-HUNTER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, TreasureHunterTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tokenObjectId"] as string, tokenObjectId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task TreasureHunterHiddenStandbyOrOpponentControlledDoesNotTrigger(
        bool faceDown,
        bool standby,
        bool opponentControlled)
    {
        var state = BuildTreasureHunterMoveState(
            faceDown: faceDown,
            standby: standby,
            opponentControlled: opponentControlled);

        var result = await MoveTreasureHunterAsync(state);

        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
        if (!result.Accepted)
        {
            Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
            Assert.Equal(state.PlayerZones["P1"].Battlefields, result.State.PlayerZones["P1"].Battlefields);
        }
    }

    [Fact]
    public async Task NonTreasureHunterMoveDoesNotTrigger()
    {
        var result = await MoveTreasureHunterAsync(BuildTreasureHunterMoveState(cardNo: "SFD·001/221"));

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
    }

    [Fact]
    public async Task FailedTreasureHunterMoveDoesNotCreateGold()
    {
        var state = BuildTreasureHunterMoveState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-invalid-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-TREASURE-HUNTER", "BATTLEFIELD", "BASE", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
        Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(state.PlayerZones["P1"].Battlefields, result.State.PlayerZones["P1"].Battlefields);
    }

    [Fact]
    public async Task TreasureHunterPreciseRoamMoveCreatesDormantGoldToken()
    {
        var result = await PreciseRoamTreasureHunterAsync(BuildTreasureHunterPreciseRoamState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenObjectId = Assert.Single(GoldTokenIds(result.State));
        Assert.Equal([tokenObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TREASURE-HUNTER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P1-BATTLEFIELD-B", result.State.ObjectLocations["P1-TREASURE-HUNTER"].BattlefieldObjectId);

        var tokenState = result.State.CardObjects[tokenObjectId];
        Assert.True(tokenState.IsExhausted);
        Assert.Equal("P1", tokenState.OwnerId);
        Assert.Equal("P1", tokenState.ControllerId);
        Assert.Contains(CardObjectTags.EquipmentCard, tokenState.Tags);
        Assert.Contains("金币", tokenState.Tags);

        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TreasureHunterTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["originZone"] as string, "BATTLEFIELD:P1-BATTLEFIELD-A", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BATTLEFIELD:P1-BATTLEFIELD-B", StringComparison.Ordinal));
        Assert.Contains(result.Events, IsTreasureHunterGoldTokenEvent);
    }

    [Fact]
    public async Task TreasureHunterPreciseRoamNoOpDoesNotCreateGold()
    {
        var state = BuildTreasureHunterPreciseRoamState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-roam-noop", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(
                "P1-TREASURE-HUNTER",
                "BATTLEFIELD:P1-BATTLEFIELD-A",
                "BATTLEFIELD:P1-BATTLEFIELD-A",
                ["ROAM"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
        Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(state.PlayerZones["P1"].Battlefields, result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P1-BATTLEFIELD-A", result.State.ObjectLocations["P1-TREASURE-HUNTER"].BattlefieldObjectId);
    }

    private static async Task<ResolutionResult> MoveTreasureHunterAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-TREASURE-HUNTER", "BASE", "BATTLEFIELD", []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> PreciseRoamTreasureHunterAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-roam", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(
                "P1-TREASURE-HUNTER",
                "BATTLEFIELD:P1-BATTLEFIELD-A",
                "BATTLEFIELD:P1-BATTLEFIELD-B",
                ["ROAM"]),
            CancellationToken.None);
    }

    private static bool IsTreasureHunterTriggerEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Payload.TryGetValue("trigger", out var trigger) ? trigger as string : null, TreasureHunterTrigger, StringComparison.Ordinal);
    }

    private static bool IsTreasureHunterGoldTokenEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("abilityId", out var abilityId) ? abilityId as string : null, TreasureHunterTrigger, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> GoldTokenIds(MatchState state)
    {
        return state.PlayerZones["P1"].Base
            .Concat(state.PlayerZones["P1"].Battlefields)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("金币", StringComparer.Ordinal)
                && cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static MatchState BuildTreasureHunterMoveState(
        string cardNo = "SFD·130/221",
        bool faceDown = false,
        bool standby = false,
        bool opponentControlled = false)
    {
        var treasureHunterTags = standby
            ? new[] { CardObjectTags.UnitCard, CardObjectTags.Standby }
            : [CardObjectTags.UnitCard];
        return new MatchState(
            roomId: "treasure-hunter-move-trigger-test",
            tick: 27,
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-TREASURE-HUNTER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new(
                    "P1-TREASURE-HUNTER",
                    isFaceDown: faceDown,
                    cardNo: cardNo,
                    power: 1,
                    tags: treasureHunterTags,
                    ownerId: "P1",
                    controllerId: opponentControlled ? "P2" : "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new("P1", "BASE")
            });
    }

    private static MatchState BuildTreasureHunterPreciseRoamState()
    {
        return new MatchState(
            roomId: "treasure-hunter-roam-trigger-test",
            tick: 27,
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-TREASURE-HUNTER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new(
                    "P1-TREASURE-HUNTER",
                    cardNo: "SFD·130/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-A")
            });
    }
}
