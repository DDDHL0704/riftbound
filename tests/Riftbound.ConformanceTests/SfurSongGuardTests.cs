using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SfurSongGuardTests
{
    [Fact]
    public async Task SfurSongPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSfurSongState();

        var played = await PlaySfurSongAsync(engine, state, "P1-EQUIPMENT-SFUR-SONG", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-SFUR-SONG", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SFUR_SONG_PLAY_EQUIPMENT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-sfur-song-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sfur-song-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-SFUR-SONG", "P1-FACE-DOWN-STANDBY-SFUR-SONG", "P1-EQUIPMENT-SFUR-SONG"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var equipment = p2Pass.State.CardObjects["P1-EQUIPMENT-SFUR-SONG"];
        Assert.Equal("SFD·059/221", equipment.CardNo);
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal([CardObjectTags.EquipmentCard], equipment.Tags);
        Assert.False(equipment.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-SFUR-SONG", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-EQUIPMENT-SFUR-SONG", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentName"] as string, "斯弗尔尚歌", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-EQUIPMENT-SFUR-SONG", "P1-TARGET-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-SFUR-SONG", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P2-EQUIPMENT-SFUR-SONG", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-SFUR-SONG", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-EQUIPMENT-SFUR-SONG", "", 2, ErrorCodes.InsufficientCost)]
    public async Task SfurSongPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSfurSongState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlaySfurSongAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SFUR-SONG"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-SFUR-SONG", "P1-FACE-DOWN-STANDBY-SFUR-SONG"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-EQUIPMENT-SFUR-SONG"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-EQUIPMENT-SFUR-SONG"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-SFUR-SONG"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-SFUR-SONG"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-SFUR-SONG"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySfurSongAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sfur-song-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·059/221",
                targetObjectIds),
            CancellationToken.None);
    }

    private static MatchState BuildSfurSongState(int mana = 3)
    {
        return new MatchState(
            roomId: "sfur-song-guard-test",
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
                    Hand = ["P1-EQUIPMENT-SFUR-SONG"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-SFUR-SONG",
                        "P1-FACE-DOWN-STANDBY-SFUR-SONG"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-EQUIPMENT-SFUR-SONG"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SFUR-SONG"] = SfurSong("P1-EQUIPMENT-SFUR-SONG"),
                ["P1-BASE-SFUR-SONG"] = SfurSong("P1-BASE-SFUR-SONG"),
                ["P1-FACE-DOWN-STANDBY-SFUR-SONG"] = SfurSong(
                    "P1-FACE-DOWN-STANDBY-SFUR-SONG",
                    isFaceDown: true,
                    tags: [CardObjectTags.EquipmentCard, CardObjectTags.Standby]),
                ["P2-EQUIPMENT-SFUR-SONG"] = SfurSong(
                    "P2-EQUIPMENT-SFUR-SONG",
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

    private static CardObjectState SfurSong(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·059/221",
            isFaceDown: isFaceDown,
            manaCost: 3,
            tags: tags ?? [CardObjectTags.EquipmentCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }
}
