using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HuntReadyGuardTests
{
    [Fact]
    public async Task HuntReadiesOnlyFriendlyPublicFieldUnits()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHuntState();

        var played = await PlayHuntAsync(engine, state, []);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "HUNT_READY_ALL_FRIENDLY_UNITS", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hunt-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hunt-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-HUNT"], p2Pass.State.PlayerZones["P1"].Graveyard);

        Assert.False(p2Pass.State.CardObjects["P1-BASE-UNIT"].IsExhausted);
        Assert.False(p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-BATTLEFIELD-EQUIPMENT"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-BATTLEFIELD-SPELL"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-BATTLEFIELD-RUNE"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);

        var readiedTargetIds = p2Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal))
            .Select(gameEvent => gameEvent.Payload["targetObjectId"] as string)
            .ToArray();
        Assert.Equal(2, readiedTargetIds.Length);
        Assert.Contains("P1-BASE-UNIT", readiedTargetIds);
        Assert.Contains("P1-BATTLEFIELD-UNIT", readiedTargetIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-EQUIPMENT", readiedTargetIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-SPELL", readiedTargetIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-RUNE", readiedTargetIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", readiedTargetIds);
        Assert.DoesNotContain("P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT", readiedTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT", readiedTargetIds);
    }

    [Theory]
    [InlineData("P1-BASE-UNIT")]
    [InlineData("P1-BATTLEFIELD-UNIT")]
    [InlineData("P2-BATTLEFIELD-UNIT")]
    [InlineData("P1-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P1-BATTLEFIELD-SPELL")]
    [InlineData("P1-BATTLEFIELD-RUNE")]
    [InlineData("P1-FACE-DOWN-STANDBY")]
    [InlineData("P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT")]
    public async Task HuntRejectsExplicitTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildHuntState();

        var result = await PlayHuntAsync(new CoreRuleEngine(), state, [targetObjectId]);

        AssertRejectedWithoutMutation(state, result);
    }

    private static async Task<ResolutionResult> PlayHuntAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-hunt-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-HUNT",
                "SFD·204/221",
                targetObjectIds),
            CancellationToken.None);
    }

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HUNT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    private static MatchState BuildHuntState()
    {
        return new MatchState(
            roomId: "hunt-ready-guard-test",
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
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HUNT"],
                    Base = ["P1-BASE-UNIT"],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNIT",
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HUNT"] = Hunt(),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT"),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT"),
                ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit("P1-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P1"),
                ["P1-BATTLEFIELD-SPELL"] = NonUnit("P1-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P1"),
                ["P1-BATTLEFIELD-RUNE"] = NonUnit("P1-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P1"),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", ownerId: "P2", controllerId: "P2")
            });
    }

    private static CardObjectState Hunt()
    {
        return new CardObjectState(
            "P1-SPELL-HUNT",
            cardNo: "SFD·204/221",
            manaCost: 1,
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo = "SFD·125/221",
        int power = 2,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isFaceDown: isFaceDown,
            isExhausted: true,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 2,
            isExhausted: true,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
