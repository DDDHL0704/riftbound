using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class IsolateMoveToBaseGuardTests
{
    [Fact]
    public async Task IsolateMovesPublicEnemyBattlefieldUnitToOwnerBaseWithoutDrawing()
    {
        var engine = new CoreRuleEngine();
        var state = BuildIsolateState();

        var played = await PlayIsolateAsync(engine, state, "P2-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-isolate-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-isolate-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("P2-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(2, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(4, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].Power);
        Assert.True(p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerPlayerId"] as string, "P2", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-FRIENDLY-BATTLEFIELD-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    public async Task IsolateRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildIsolateState();

        var result = await PlayIsolateAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ISOLATE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-BATTLEFIELD-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                "P2-BATTLEFIELD-UNIT",
                "P2-FACE-DOWN-STANDBY",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(2, result.State.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
        Assert.True(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_MOVED_TO_BASE", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayIsolateAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-isolate-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-ISOLATE",
                "UNL-124/219",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static MatchState BuildIsolateState()
    {
        return new MatchState(
            roomId: "isolate-move-to-base-guard-test",
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
                ["P1"] = new(2, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ISOLATE"],
                    Battlefields = ["P1-FRIENDLY-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-UNIT",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-ISOLATE"] = new(
                    "P1-SPELL-ISOLATE",
                    cardNo: "UNL-124/219",
                    manaCost: 2,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-BATTLEFIELD-UNIT"] = new(
                    "P1-FRIENDLY-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT"] = new(
                    "P2-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    damage: 2,
                    power: 4,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BASE-UNIT"] = new(
                    "P2-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-STALE-UNIT"] = new(
                    "P2-STALE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FACE-DOWN-STANDBY"] = new(
                    "P2-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SPELL"] = new(
                    "P2-BATTLEFIELD-SPELL",
                    cardNo: "OGN·169/298",
                    power: 1,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-RUNE"] = new(
                    "P2-BATTLEFIELD-RUNE",
                    cardNo: "RUNES·001",
                    power: 1,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }
}
