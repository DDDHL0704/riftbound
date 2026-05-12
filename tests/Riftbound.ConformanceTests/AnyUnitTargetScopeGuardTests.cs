using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AnyUnitTargetScopeGuardTests
{
    [Theory]
    [InlineData("P1-BASE-UNIT")]
    [InlineData("P2-BATTLEFIELD-UNIT")]
    public async Task FirstMateReadiesOnlyPublicFieldUnitTargets(string targetObjectId)
    {
        var engine = new CoreRuleEngine();
        var state = BuildFirstMateState();

        var played = await PlayFirstMateAsync(engine, state, targetObjectId);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal([targetObjectId], stackItem.TargetObjectIds);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "FIRST_MATE_PLAY_UNIT_READY_ANOTHER_UNIT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-first-mate-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-first-mate-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains("P1-UNIT-FIRST-MATE", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-UNIT-FIRST-MATE", p2Pass.State.PlayerZones["P1"].Hand);
        Assert.False(p2Pass.State.CardObjects[targetObjectId].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, targetObjectId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P1-BATTLEFIELD-SPELL")]
    [InlineData("P1-BATTLEFIELD-RUNE")]
    [InlineData("P1-FACE-DOWN-STANDBY")]
    [InlineData("P1-FACE-UP-STANDBY")]
    [InlineData("P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P1-HAND-UNIT")]
    [InlineData("P1-STALE-UNIT")]
    public async Task FirstMateRejectsNonPublicFieldUnitTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildFirstMateState();

        var result = await PlayFirstMateAsync(new CoreRuleEngine(), state, targetObjectId);

        AssertRejectedWithoutMutation(state, result);
        Assert.True(result.State.CardObjects["P1-BASE-UNIT"].IsExhausted);
        Assert.True(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
    }

    [Fact]
    public async Task AnyUnitScopeRejectsNonUnitWhenBehaviorDoesNotRequireUnitTag()
    {
        var state = BuildCurtainRisesState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-curtain-rises-equipment-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-CURTAIN-RISES",
                "UNL-009/219",
                ["P1-BATTLEFIELD-EQUIPMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CURTAIN-RISES"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
    }

    private static async Task<ResolutionResult> PlayFirstMateAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-first-mate-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-UNIT-FIRST-MATE",
                "OGN·132/298",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-FIRST-MATE", "P1-HAND-UNIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    private static MatchState BuildFirstMateState()
    {
        return new MatchState(
            roomId: "any-unit-target-scope-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-FIRST-MATE", "P1-HAND-UNIT"],
                    Base = ["P1-BASE-UNIT"],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-FACE-UP-STANDBY",
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
                ["P1-UNIT-FIRST-MATE"] = Unit(
                    "P1-UNIT-FIRST-MATE",
                    cardNo: "OGN·132/298",
                    power: 3,
                    isExhausted: false),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT"),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT"),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit("P1-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P1"),
                ["P1-BATTLEFIELD-SPELL"] = NonUnit("P1-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P1"),
                ["P1-BATTLEFIELD-RUNE"] = NonUnit("P1-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P1"),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-FACE-UP-STANDBY"] = Unit(
                    "P1-FACE-UP-STANDBY",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT")
            });
    }

    private static MatchState BuildCurtainRisesState()
    {
        return new MatchState(
            roomId: "any-unit-no-required-tag-scope-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CURTAIN-RISES"],
                    Battlefields = ["P1-BATTLEFIELD-EQUIPMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-CURTAIN-RISES"] = NonUnit("P1-SPELL-CURTAIN-RISES", "UNL-009/219", CardObjectTags.SpellCard, "P1", manaCost: 2),
                ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit("P1-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P1")
            });
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo = "SFD·125/221",
        int power = 2,
        bool isFaceDown = false,
        bool isExhausted = true,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isFaceDown: isFaceDown,
            isExhausted: isExhausted,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId,
        int manaCost = 0)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            manaCost: manaCost,
            power: 2,
            isExhausted: true,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
