using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RideTheWindMoveGuardTests
{
    [Fact]
    public async Task RideTheWindReadiesAndMovesPublicFriendlyBattlefieldUnitToOwnerBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildRideTheWindState();

        var played = await PlayRideTheWindAsync(engine, state, "P1-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-ride-the-wind-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-ride-the-wind-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("P1-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(2, p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(4, p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].Power);
        Assert.False(p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("wasExhausted", out var wasExhausted)
            && wasExhausted is true);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerPlayerId"] as string, "P1", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-ENEMY-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-UNIT")]
    [InlineData("P1-STALE-UNIT")]
    [InlineData("P1-FACE-DOWN-STANDBY")]
    [InlineData("P1-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P1-BATTLEFIELD-SPELL")]
    [InlineData("P1-BATTLEFIELD-RUNE")]
    public async Task RideTheWindRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildRideTheWindState();

        var result = await PlayRideTheWindAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-RIDE-THE-WIND"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-BATTLEFIELD-UNIT",
                "P1-FACE-DOWN-STANDBY",
                "P1-BATTLEFIELD-EQUIPMENT",
                "P1-BATTLEFIELD-SPELL",
                "P1-BATTLEFIELD-RUNE"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-ENEMY-BATTLEFIELD-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(2, result.State.CardObjects["P1-BATTLEFIELD-UNIT"].Damage);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayRideTheWindAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ride-the-wind-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-RIDE-THE-WIND",
                "OGN·173/298",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static MatchState BuildRideTheWindState()
    {
        return new MatchState(
            roomId: "ride-the-wind-move-guard-test",
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
                    Hand = ["P1-SPELL-RIDE-THE-WIND"],
                    Base = ["P1-BASE-UNIT"],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNIT",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-RIDE-THE-WIND"] = new(
                    "P1-SPELL-RIDE-THE-WIND",
                    cardNo: "OGN·173/298",
                    manaCost: 2,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-UNIT"] = new(
                    "P1-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    damage: 2,
                    power: 4,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BASE-UNIT"] = new(
                    "P1-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STALE-UNIT"] = new(
                    "P1-STALE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FACE-DOWN-STANDBY"] = new(
                    "P1-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-EQUIPMENT"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-SPELL"] = new(
                    "P1-BATTLEFIELD-SPELL",
                    cardNo: "OGN·169/298",
                    power: 1,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-RUNE"] = new(
                    "P1-BATTLEFIELD-RUNE",
                    cardNo: "RUNES·001",
                    power: 1,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-ENEMY-BATTLEFIELD-UNIT"] = new(
                    "P2-ENEMY-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }
}
