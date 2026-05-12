using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EnemyBattlefieldUnitTargetScopeGuardTests
{
    [Fact]
    public async Task MegasharkCannonDamagesOnlyEnemyPublicBattlefieldUnitTarget()
    {
        var engine = new CoreRuleEngine();
        var state = BuildMegasharkState();

        var played = await PlayMegasharkAsync(engine, state, "P2-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal(["P2-BATTLEFIELD-UNIT"], stackItem.TargetObjectIds);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "MEGASHARK_CANNON_PLAY_UNIT_DAMAGE_6_ENEMY_BATTLEFIELD", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-megashark-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-megashark-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains("P1-UNIT-MEGASHARK-CANNON", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-UNIT-MEGASHARK-CANNON", p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(6, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P1-FRIENDLY-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BASE-UNIT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-EQUIPMENT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-SPELL"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-RUNE"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-FACE-DOWN-STANDBY"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-FACE-UP-STANDBY"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].Damage);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-FACE-UP-STANDBY")]
    [InlineData("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P1-FRIENDLY-BATTLEFIELD-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P2-HAND-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    public async Task MegasharkCannonRejectsNonPublicEnemyBattlefieldUnitTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildMegasharkState();

        var result = await PlayMegasharkAsync(new CoreRuleEngine(), state, targetObjectId);

        AssertRejectedWithoutMutation(state, result, expectedMana: 6, expectedHand: ["P1-UNIT-MEGASHARK-CANNON"]);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
    }

    [Fact]
    public async Task CrescentStrikeRejectsEnemyBattlefieldNonUnitWithoutTargetRequiredTag()
    {
        var state = BuildCrescentState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-crescent-equipment-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-CRESCENT-STRIKE",
                "UNL-072/219",
                ["P2-BATTLEFIELD-EQUIPMENT"]),
            CancellationToken.None);

        AssertRejectedWithoutMutation(state, result, expectedMana: 3, expectedHand: ["P1-SPELL-CRESCENT-STRIKE"]);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-EQUIPMENT"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
    }

    private static async Task<ResolutionResult> PlayMegasharkAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-megashark-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-UNIT-MEGASHARK-CANNON",
                "OGN·092/298",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static void AssertRejectedWithoutMutation(
        MatchState initialState,
        ResolutionResult result,
        int expectedMana,
        IReadOnlyList<string> expectedHand)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(expectedMana, 0), result.State.RunePools["P1"]);
        Assert.Equal(expectedHand, result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
    }

    private static MatchState BuildMegasharkState()
    {
        return new MatchState(
            roomId: "enemy-battlefield-unit-target-scope-guard-test",
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
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-MEGASHARK-CANNON"],
                    Battlefields = ["P1-FRIENDLY-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-UNIT"],
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-UNIT",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-FACE-UP-STANDBY",
                        "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MEGASHARK-CANNON"] = Unit(
                    "P1-UNIT-MEGASHARK-CANNON",
                    cardNo: "OGN·092/298",
                    power: 6,
                    isExhausted: false),
                ["P1-FRIENDLY-BATTLEFIELD-UNIT"] = Unit("P1-FRIENDLY-BATTLEFIELD-UNIT"),
                ["P2-HAND-UNIT"] = Unit("P2-HAND-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", power: 7, ownerId: "P2", controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit("P2-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P2"),
                ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
                ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2"),
                ["P2-FACE-DOWN-STANDBY"] = Unit(
                    "P2-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FACE-UP-STANDBY"] = Unit(
                    "P2-FACE-UP-STANDBY",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"),
                ["P2-STALE-UNIT"] = Unit("P2-STALE-UNIT", ownerId: "P2", controllerId: "P2")
            });
    }

    private static MatchState BuildCrescentState()
    {
        return new MatchState(
            roomId: "enemy-battlefield-unit-no-required-tag-guard-test",
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
                    Hand = ["P1-SPELL-CRESCENT-STRIKE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-EQUIPMENT", "P2-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-CRESCENT-STRIKE"] = NonUnit(
                    "P1-SPELL-CRESCENT-STRIKE",
                    "UNL-072/219",
                    CardObjectTags.SpellCard,
                    "P1",
                    manaCost: 3),
                ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit("P2-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P2"),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", ownerId: "P2", controllerId: "P2")
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
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
