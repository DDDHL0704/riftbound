using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AkshanGuardTests
{
    [Fact]
    public async Task AkshanPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAkshanState();

        var played = await PlayAkshanAsync(engine, state, "P1-UNIT-AKSHAN", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-akshan-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-akshan-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-AKSHAN", "P1-FACE-DOWN-STANDBY-AKSHAN", "P1-UNIT-AKSHAN"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var unit = p2Pass.State.CardObjects["P1-UNIT-AKSHAN"];
        Assert.Equal("SFD·109/221", unit.CardNo);
        Assert.Equal("P1", unit.OwnerId);
        Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(4, unit.Power);
        Assert.Equal([CardObjectTags.UnitCard, "哨兵", "百炼"], unit.Tags);
        Assert.False(unit.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, "P1-UNIT-AKSHAN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "阿克尚", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["power"]) == 4);
    }

    [Theory]
    [InlineData("P1-UNIT-AKSHAN", "P1-TARGET-UNIT", 4, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P2-UNIT-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-AKSHAN", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-UNIT-AKSHAN", "", 3, ErrorCodes.InsufficientCost)]
    public async Task AkshanPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildAkshanState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlayAkshanAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-AKSHAN"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-AKSHAN", "P1-FACE-DOWN-STANDBY-AKSHAN"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-UNIT-AKSHAN"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-UNIT-AKSHAN"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-AKSHAN"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayAkshanAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-akshan-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·109/221",
                targetObjectIds),
            CancellationToken.None);
    }

    private static MatchState BuildAkshanState(int mana = 4)
    {
        return new MatchState(
            roomId: "akshan-guard-test",
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
                    Hand = ["P1-UNIT-AKSHAN"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-AKSHAN",
                        "P1-FACE-DOWN-STANDBY-AKSHAN"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-AKSHAN"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-AKSHAN"] = Akshan("P1-UNIT-AKSHAN"),
                ["P1-BASE-AKSHAN"] = Akshan("P1-BASE-AKSHAN"),
                ["P1-FACE-DOWN-STANDBY-AKSHAN"] = Akshan(
                    "P1-FACE-DOWN-STANDBY-AKSHAN",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-UNIT-AKSHAN"] = Akshan(
                    "P2-UNIT-AKSHAN",
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

    private static CardObjectState Akshan(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·109/221",
            isFaceDown: isFaceDown,
            manaCost: 4,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }
}
