using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GiantArmKatoGuardTests
{
    [Fact]
    public async Task GiantArmKatoPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildGiantArmKatoState();

        var played = await PlayGiantArmKatoAsync(engine, state, "P1-UNIT-GIANT-ARM-KATO", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-GIANT-ARM-KATO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "GIANT_ARM_KATO_PLAY_KEYWORD_UNIT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-giant-arm-kato-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-giant-arm-kato-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-GIANT-ARM-KATO", "P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO", "P1-UNIT-GIANT-ARM-KATO"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var unit = p2Pass.State.CardObjects["P1-UNIT-GIANT-ARM-KATO"];
        Assert.Equal("SFD·112/221", unit.CardNo);
        Assert.Equal("P1", unit.OwnerId);
        Assert.Equal("P1", unit.ControllerId);
        Assert.Equal(3, unit.Power);
        Assert.Equal([CardObjectTags.UnitCard, "法盾"], unit.Tags);
        Assert.False(unit.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-UNIT-GIANT-ARM-KATO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, "P1-UNIT-GIANT-ARM-KATO", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "巨腕加藤", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["power"]) == 3);
    }

    [Theory]
    [InlineData("P1-UNIT-GIANT-ARM-KATO", "P1-TARGET-UNIT", 4, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-GIANT-ARM-KATO", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P2-UNIT-GIANT-ARM-KATO", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO", "", 4, ErrorCodes.CardNotInHand)]
    [InlineData("P1-UNIT-GIANT-ARM-KATO", "", 3, ErrorCodes.InsufficientCost)]
    public async Task GiantArmKatoPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildGiantArmKatoState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlayGiantArmKatoAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-GIANT-ARM-KATO"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-GIANT-ARM-KATO", "P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-UNIT-GIANT-ARM-KATO"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-UNIT-GIANT-ARM-KATO"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayGiantArmKatoAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-giant-arm-kato-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·112/221",
                targetObjectIds),
            CancellationToken.None);
    }

    private static MatchState BuildGiantArmKatoState(int mana = 4)
    {
        return new MatchState(
            roomId: "giant-arm-kato-guard-test",
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
                    Hand = ["P1-UNIT-GIANT-ARM-KATO"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-GIANT-ARM-KATO",
                        "P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-GIANT-ARM-KATO"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-GIANT-ARM-KATO"] = GiantArmKato("P1-UNIT-GIANT-ARM-KATO"),
                ["P1-BASE-GIANT-ARM-KATO"] = GiantArmKato("P1-BASE-GIANT-ARM-KATO"),
                ["P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO"] = GiantArmKato(
                    "P1-FACE-DOWN-STANDBY-GIANT-ARM-KATO",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P2-UNIT-GIANT-ARM-KATO"] = GiantArmKato(
                    "P2-UNIT-GIANT-ARM-KATO",
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

    private static CardObjectState GiantArmKato(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·112/221",
            isFaceDown: isFaceDown,
            manaCost: 4,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }
}
