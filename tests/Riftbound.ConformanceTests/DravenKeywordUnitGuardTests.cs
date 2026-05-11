using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DravenKeywordUnitGuardTests
{
    [Theory]
    [InlineData("SFD·148/221", "SFD_DRAVEN_PLAY_KEYWORD_UNIT")]
    [InlineData("SFD·148a/221", "SFD_DRAVEN_ALT_A_PLAY_KEYWORD_UNIT")]
    public async Task DravenKeywordUnitPlayCardWithNoTargetsUsesStackAndResolvesToBase(
        string cardNo,
        string expectedEffectKind)
    {
        var engine = new CoreRuleEngine();
        var state = BuildDravenState(cardNo);

        var played = await PlayDravenAsync(engine, state, "P1-UNIT-DRAVEN", cardNo, []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-DRAVEN", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["mana"]) == 6);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-DRAVEN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, expectedEffectKind, StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-draven-keyword-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-draven-keyword-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), p2Pass.State.RunePools["P1"]);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-DRAVEN", "P1-FACE-DOWN-STANDBY-DRAVEN", "P1-UNIT-DRAVEN"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var unit = p2Pass.State.CardObjects["P1-UNIT-DRAVEN"];
        Assert.Equal(cardNo, unit.CardNo);
        Assert.Equal("P1", unit.OwnerId);
        Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(6, unit.Power);
        Assert.Equal([CardObjectTags.UnitCard, "法盾"], unit.Tags);
        Assert.False(unit.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-DRAVEN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, expectedEffectKind, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-DRAVEN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, "P1-UNIT-DRAVEN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "德莱文", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["power"]) == 6);
    }

    [Theory]
    [InlineData("SFD·148/221")]
    [InlineData("SFD·148a/221")]
    public async Task DravenKeywordUnitPlayCardWithExplicitTargetRejectsWithoutMutation(string cardNo)
    {
        var state = BuildDravenState(cardNo);

        var result = await PlayDravenAsync(
            new CoreRuleEngine(),
            state,
            "P1-UNIT-DRAVEN",
            cardNo,
            ["P1-TARGET-UNIT"]);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        AssertNoPlayMutation(result, cardNo, 6);
    }

    [Theory]
    [InlineData("P1-BASE-DRAVEN", 6, ErrorCodes.CardNotInHand)]
    [InlineData("P2-UNIT-DRAVEN", 6, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-DRAVEN", 6, ErrorCodes.CardNotInHand)]
    [InlineData("P1-UNIT-DRAVEN", 5, ErrorCodes.InsufficientCost)]
    public async Task DravenKeywordUnitPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        int mana,
        string expectedErrorCode)
    {
        const string cardNo = "SFD·148/221";
        var state = BuildDravenState(cardNo, mana);

        var result = await PlayDravenAsync(
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
        Assert.Equal(["P1-UNIT-DRAVEN"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-DRAVEN", "P1-FACE-DOWN-STANDBY-DRAVEN"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-UNIT-DRAVEN"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-UNIT-DRAVEN"].IsFaceDown);
        Assert.Equal(cardNo, result.State.CardObjects["P1-UNIT-DRAVEN"].CardNo);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-DRAVEN"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-DRAVEN"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-DRAVEN"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayDravenAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        string cardNo,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-draven-keyword-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                targetObjectIds),
            CancellationToken.None);
    }

    private static MatchState BuildDravenState(string cardNo, int mana = 6)
    {
        return new MatchState(
            roomId: "draven-keyword-unit-guard-test",
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
                    Hand = ["P1-UNIT-DRAVEN"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-DRAVEN",
                        "P1-FACE-DOWN-STANDBY-DRAVEN"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-DRAVEN"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-DRAVEN"] = Draven("P1-UNIT-DRAVEN", cardNo),
                ["P1-BASE-DRAVEN"] = Draven("P1-BASE-DRAVEN", cardNo),
                ["P1-FACE-DOWN-STANDBY-DRAVEN"] = Draven(
                    "P1-FACE-DOWN-STANDBY-DRAVEN",
                    cardNo,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-UNIT-DRAVEN"] = Draven(
                    "P2-UNIT-DRAVEN",
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

    private static CardObjectState Draven(
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
            manaCost: 6,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }
}
