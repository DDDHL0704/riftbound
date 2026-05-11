using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReksaiAttackRevealPlayUnitGuardTests
{
    [Theory]
    [InlineData("SFD·170/221", "SFD_170_REKSAI_ATTACK_REVEAL_PLAY_UNIT")]
    [InlineData("SFD·170a/221", "SFD_170A_REKSAI_ATTACK_REVEAL_PLAY_UNIT")]
    public async Task ReksaiPlayCardWithNoTargetsUsesStackAndResolvesToBase(
        string cardNo,
        string expectedEffectKind)
    {
        var engine = new CoreRuleEngine();
        var state = BuildReksaiState(cardNo);

        var played = await PlayReksaiAsync(engine, state, "P1-UNIT-REKSAI", cardNo, []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-REKSAI", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["mana"]) == 5);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-REKSAI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, expectedEffectKind, StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-reksai-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-reksai-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-REKSAI", "P1-FACE-DOWN-STANDBY-REKSAI", "P1-UNIT-REKSAI"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var unit = p2Pass.State.CardObjects["P1-UNIT-REKSAI"];
        Assert.Equal(cardNo, unit.CardNo);
        Assert.Equal("P1", unit.OwnerId);
        Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(5, unit.Power);
        Assert.Equal([CardObjectTags.UnitCard], unit.Tags);
        Assert.False(unit.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-REKSAI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, expectedEffectKind, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-REKSAI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, "P1-UNIT-REKSAI", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "雷克塞", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["power"]) == 5);
    }

    [Theory]
    [InlineData("SFD·170/221")]
    [InlineData("SFD·170a/221")]
    public async Task ReksaiPlayCardWithExplicitTargetRejectsWithoutMutation(string cardNo)
    {
        var state = BuildReksaiState(cardNo);

        var result = await PlayReksaiAsync(
            new CoreRuleEngine(),
            state,
            "P1-UNIT-REKSAI",
            cardNo,
            ["P1-TARGET-UNIT"]);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        AssertNoPlayMutation(result, cardNo, 5);
    }

    [Theory]
    [InlineData("P1-BASE-REKSAI", 5, ErrorCodes.CardNotInHand)]
    [InlineData("P2-UNIT-REKSAI", 5, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-REKSAI", 5, ErrorCodes.CardNotInHand)]
    [InlineData("P1-UNIT-REKSAI", 4, ErrorCodes.InsufficientCost)]
    public async Task ReksaiPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        int mana,
        string expectedErrorCode)
    {
        const string cardNo = "SFD·170/221";
        var state = BuildReksaiState(cardNo, mana);

        var result = await PlayReksaiAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            cardNo,
            []);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        AssertNoPlayMutation(result, cardNo, mana);
    }

    private static void AssertNoPlayMutation(
        ResolutionResult result,
        string cardNo,
        int mana)
    {
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-REKSAI"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-REKSAI", "P1-FACE-DOWN-STANDBY-REKSAI"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-UNIT-REKSAI"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-UNIT-REKSAI"].IsFaceDown);
        Assert.Equal(cardNo, result.State.CardObjects["P1-UNIT-REKSAI"].CardNo);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-REKSAI"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-REKSAI"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-REKSAI"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayReksaiAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        string cardNo,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-reksai-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                targetObjectIds),
            CancellationToken.None);
    }

    private static MatchState BuildReksaiState(string cardNo, int mana = 5)
    {
        return new MatchState(
            roomId: "reksai-attack-reveal-play-unit-guard-test",
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
                    Hand = ["P1-UNIT-REKSAI"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-REKSAI",
                        "P1-FACE-DOWN-STANDBY-REKSAI"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-REKSAI"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-REKSAI"] = Reksai("P1-UNIT-REKSAI", cardNo),
                ["P1-BASE-REKSAI"] = Reksai("P1-BASE-REKSAI", cardNo),
                ["P1-FACE-DOWN-STANDBY-REKSAI"] = Reksai(
                    "P1-FACE-DOWN-STANDBY-REKSAI",
                    cardNo,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-UNIT-REKSAI"] = Reksai(
                    "P2-UNIT-REKSAI",
                    cardNo,
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

    private static CardObjectState Reksai(
        string objectId,
        string cardNo,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : cardNo,
            isFaceDown: isFaceDown,
            manaCost: 5,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }
}
