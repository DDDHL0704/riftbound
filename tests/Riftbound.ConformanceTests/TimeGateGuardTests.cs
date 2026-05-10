using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TimeGateGuardTests
{
    [Fact]
    public async Task TimeGatePlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTimeGateState();

        var played = await PlayTimeGateAsync(engine, state, "P1-EQUIPMENT-TIME-GATE", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-TIME-GATE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "TIME_GATE_PLAY_EQUIPMENT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-time-gate-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-time-gate-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-TIME-GATE", "P1-FACE-DOWN-STANDBY-TIME-GATE", "P1-EQUIPMENT-TIME-GATE"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var equipment = p2Pass.State.CardObjects["P1-EQUIPMENT-TIME-GATE"];
        Assert.Equal("SFD·078/221", equipment.CardNo);
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal([CardObjectTags.EquipmentCard], equipment.Tags);
        Assert.False(equipment.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-TIME-GATE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-EQUIPMENT-TIME-GATE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentName"] as string, "预时之门", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-EQUIPMENT-TIME-GATE", "P1-TARGET-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-TIME-GATE", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P2-EQUIPMENT-TIME-GATE", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-TIME-GATE", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-EQUIPMENT-TIME-GATE", "", 2, ErrorCodes.InsufficientCost)]
    public async Task TimeGatePlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildTimeGateState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlayTimeGateAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-TIME-GATE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-TIME-GATE", "P1-FACE-DOWN-STANDBY-TIME-GATE"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-EQUIPMENT-TIME-GATE"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-EQUIPMENT-TIME-GATE"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-TIME-GATE"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-TIME-GATE"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-TIME-GATE"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayTimeGateAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-time-gate-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·078/221",
                targetObjectIds),
            CancellationToken.None);
    }

    private static MatchState BuildTimeGateState(int mana = 3)
    {
        return new MatchState(
            roomId: "time-gate-guard-test",
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
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-TIME-GATE"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-TIME-GATE",
                        "P1-FACE-DOWN-STANDBY-TIME-GATE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-EQUIPMENT-TIME-GATE"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-TIME-GATE"] = TimeGate("P1-EQUIPMENT-TIME-GATE"),
                ["P1-BASE-TIME-GATE"] = TimeGate("P1-BASE-TIME-GATE"),
                ["P1-FACE-DOWN-STANDBY-TIME-GATE"] = TimeGate(
                    "P1-FACE-DOWN-STANDBY-TIME-GATE",
                    isFaceDown: true,
                    tags: [CardObjectTags.EquipmentCard, CardObjectTags.Standby]),
                ["P2-EQUIPMENT-TIME-GATE"] = TimeGate(
                    "P2-EQUIPMENT-TIME-GATE",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-TARGET-UNIT"] = new(
                    "P1-TARGET-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static CardObjectState TimeGate(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·078/221",
            isFaceDown: isFaceDown,
            manaCost: 3,
            tags: tags ?? [CardObjectTags.EquipmentCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }
}
