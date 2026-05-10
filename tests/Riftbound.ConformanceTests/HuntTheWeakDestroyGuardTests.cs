using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HuntTheWeakDestroyGuardTests
{
    [Fact]
    public async Task HuntTheWeakDestroysPublicSmallBattlefieldUnit()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHuntTheWeakState();

        var played = await PlayHuntTheWeakAsync(engine, state, "P2-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hunt-the-weak-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hunt-the-weak-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P2-BASE-UNIT", "P2-BATTLEFIELD-EQUIPMENT"], p2Pass.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                "P2-LARGE-BATTLEFIELD-UNIT",
                "P2-FACE-DOWN-STANDBY"
            ],
            p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT", p2Pass.State.CardObjects.Keys);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerPlayerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destroyedByPlayerId"] as string, "P1", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-LARGE-BATTLEFIELD-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    public async Task HuntTheWeakRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildHuntTheWeakState();

        var result = await PlayHuntTheWeakAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HUNT-THE-WEAK"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                "P2-BATTLEFIELD-UNIT",
                "P2-LARGE-BATTLEFIELD-UNIT",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-FACE-DOWN-STANDBY"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_DESTROYED", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayHuntTheWeakAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-hunt-the-weak-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-HUNT-THE-WEAK",
                "UNL-159/219",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static MatchState BuildHuntTheWeakState()
    {
        return new MatchState(
            roomId: "hunt-the-weak-destroy-guard-test",
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
                    Hand = ["P1-SPELL-HUNT-THE-WEAK"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-UNIT",
                        "P2-LARGE-BATTLEFIELD-UNIT",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-FACE-DOWN-STANDBY"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HUNT-THE-WEAK"] = new(
                    "P1-SPELL-HUNT-THE-WEAK",
                    cardNo: "UNL-159/219",
                    manaCost: 2,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT"] = new(
                    "P2-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    damage: 1,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LARGE-BATTLEFIELD-UNIT"] = new(
                    "P2-LARGE-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
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
                    controllerId: "P2")
            });
    }
}
