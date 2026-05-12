using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FirestormEnemyBattlefieldDamageGuardTests
{
    [Fact]
    public async Task FirestormDamagesOnlyEnemyPublicBattlefieldUnits()
    {
        var engine = new CoreRuleEngine();
        var state = BuildFirestormState();

        var played = await PlayFirestormAsync(engine, state, []);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "FIRESTORM_DAMAGE_ALL_ENEMY_BATTLEFIELD_UNITS_3", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-firestorm-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-firestorm-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-FIRESTORM"], p2Pass.State.PlayerZones["P1"].Graveyard);

        Assert.Equal(0, p2Pass.State.CardObjects["P1-FRIENDLY-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BASE-UNIT"].Damage);
        Assert.Equal(3, p2Pass.State.CardObjects["P2-FIRESTORM-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(3, p2Pass.State.CardObjects["P2-FIRESTORM-BATTLEFIELD-UNIT-002"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-EQUIPMENT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-SPELL"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-RUNE"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-FACE-DOWN-STANDBY"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].Damage);

        var damageTargetIds = p2Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal))
            .Select(gameEvent => gameEvent.Payload["targetObjectId"] as string)
            .ToArray();
        Assert.Equal(2, damageTargetIds.Length);
        Assert.Contains("P2-FIRESTORM-BATTLEFIELD-UNIT-001", damageTargetIds);
        Assert.Contains("P2-FIRESTORM-BATTLEFIELD-UNIT-002", damageTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", damageTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-SPELL", damageTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-RUNE", damageTargetIds);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", damageTargetIds);
        Assert.DoesNotContain("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", damageTargetIds);
    }

    [Theory]
    [InlineData("P2-FIRESTORM-BATTLEFIELD-UNIT-001")]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P1-FRIENDLY-BATTLEFIELD-UNIT")]
    public async Task FirestormRejectsExplicitTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildFirestormState();

        var result = await PlayFirestormAsync(new CoreRuleEngine(), state, [targetObjectId]);

        AssertRejectedWithoutMutation(state, result);
    }

    private static async Task<ResolutionResult> PlayFirestormAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-firestorm-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-FIRESTORM",
                "OGS·002/024",
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
        Assert.Equal(new RunePool(6, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FIRESTORM"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
    }

    private static MatchState BuildFirestormState()
    {
        return new MatchState(
            roomId: "firestorm-enemy-battlefield-damage-guard-test",
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
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FIRESTORM"],
                    Battlefields = ["P1-FRIENDLY-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-FIRESTORM-BATTLEFIELD-UNIT-001",
                        "P2-FIRESTORM-BATTLEFIELD-UNIT-002",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-FIRESTORM"] = Firestorm(),
                ["P1-FRIENDLY-BATTLEFIELD-UNIT"] = Unit("P1-FRIENDLY-BATTLEFIELD-UNIT", power: 8),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", power: 8, ownerId: "P2", controllerId: "P2"),
                ["P2-FIRESTORM-BATTLEFIELD-UNIT-001"] = Unit(
                    "P2-FIRESTORM-BATTLEFIELD-UNIT-001",
                    power: 8,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FIRESTORM-BATTLEFIELD-UNIT-002"] = Unit(
                    "P2-FIRESTORM-BATTLEFIELD-UNIT-002",
                    power: 8,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit("P2-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P2"),
                ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
                ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2"),
                ["P2-FACE-DOWN-STANDBY"] = Unit(
                    "P2-FACE-DOWN-STANDBY",
                    cardNo: null,
                    power: 8,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    power: 8),
                ["P2-STALE-UNIT"] = Unit("P2-STALE-UNIT", power: 8, ownerId: "P2", controllerId: "P2")
            });
    }

    private static CardObjectState Firestorm()
    {
        return new CardObjectState(
            "P1-SPELL-FIRESTORM",
            cardNo: "OGS·002/024",
            manaCost: 6,
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
            power: 8,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
