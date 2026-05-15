using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OrnnFriendlyEquipmentStaticPowerTests
{
    private const string OrnnObjectId = "P1-UNIT-ORNN-STATIC";
    private const string OrnnCardNo = "SFD·085/221";
    private const string OrnnAltCardNo = "SFD·085a/221";
    private const string FriendlyBaseEquipmentObjectId = "P1-EQUIPMENT-BASE";
    private const string SecondFriendlyBaseEquipmentObjectId = "P1-EQUIPMENT-BASE-2";
    private const string HandEquipmentObjectId = "P1-EQUIPMENT-HAND";
    private const string FaceDownEquipmentObjectId = "P1-EQUIPMENT-FACE-DOWN";
    private const string DirtyControllerEquipmentObjectId = "P1-EQUIPMENT-DIRTY-CONTROLLER";
    private const string EnemyEquipmentObjectId = "P2-EQUIPMENT-ENEMY";
    private const string FriendlyUnitObjectId = "P1-UNIT-FRIENDLY";

    [Theory]
    [InlineData(OrnnCardNo)]
    [InlineData(OrnnAltCardNo)]
    public async Task OrnnCountsOnlyFriendlyPublicFieldEquipmentWhenPlayed(string cardNo)
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnState(cardNo, includeFriendlyFieldEquipment: true);

        var played = await PlayOrnnAsync(engine, state, cardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Contains(OrnnObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(FriendlyBaseEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(SecondFriendlyBaseEquipmentObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(EnemyEquipmentObjectId, resolved.State.PlayerZones["P2"].Base);

        var ornn = resolved.State.CardObjects[OrnnObjectId];
        Assert.Equal(6, ornn.Power);
        Assert.Equal([CardObjectTags.UnitCard, "法盾2", CardEquipmentKeywordNames.Tempered], ornn.Tags);

        var unitPlayed = Assert.Single(resolved.Events, IsOrnnUnitPlayedEvent);
        Assert.Equal(6, Assert.IsType<int>(unitPlayed.Payload["power"]));
        Assert.Equal(2, Assert.IsType<int>(unitPlayed.Payload["friendlyEquipmentPowerBonus"]));
    }

    [Fact]
    public async Task OrnnKeepsBasePowerWhenNoFriendlyPublicFieldEquipmentExists()
    {
        var engine = new CoreRuleEngine();
        var state = BuildOrnnState(OrnnCardNo, includeFriendlyFieldEquipment: false);

        var played = await PlayOrnnAsync(engine, state, OrnnCardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(4, resolved.State.CardObjects[OrnnObjectId].Power);
        var unitPlayed = Assert.Single(resolved.Events, IsOrnnUnitPlayedEvent);
        Assert.Equal(4, Assert.IsType<int>(unitPlayed.Payload["power"]));
        Assert.False(unitPlayed.Payload.ContainsKey("friendlyEquipmentPowerBonus"));
    }

    private static async Task<ResolutionResult> PlayOrnnAsync(
        CoreRuleEngine engine,
        MatchState state,
        string cardNo)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(OrnnObjectId, cardNo, []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ornn-static-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-ornn-static-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static MatchState BuildOrnnState(
        string cardNo,
        bool includeFriendlyFieldEquipment)
    {
        var p1Base = new List<string>
        {
            FriendlyUnitObjectId,
            FaceDownEquipmentObjectId,
            DirtyControllerEquipmentObjectId
        };
        var p1Battlefields = new List<string>();
        if (includeFriendlyFieldEquipment)
        {
            p1Base.Insert(0, FriendlyBaseEquipmentObjectId);
            p1Base.Insert(1, SecondFriendlyBaseEquipmentObjectId);
        }

        var p1Hand = new[] { OrnnObjectId, HandEquipmentObjectId };
        var p2Base = new[] { EnemyEquipmentObjectId };
        var objectLocations = p1Hand
            .Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "HAND")))
            .Concat(p1Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "BASE"))))
            .Concat(p1Battlefields.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "BATTLEFIELD"))))
            .Concat(p2Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P2", "BASE"))))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return new MatchState(
            "ornn-friendly-equipment-static-power",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            }) with
        {
            Status = MatchStatuses.InProgress,
            ReadyPlayerIds = ["P1", "P2"],
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = p1Hand,
                    Base = p1Base,
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [OrnnObjectId] = Unit(OrnnObjectId, cardNo, "P1", "P1"),
                [FriendlyUnitObjectId] = Unit(FriendlyUnitObjectId, "SFD·125/221", "P1", "P1"),
                [FriendlyBaseEquipmentObjectId] = Equipment(FriendlyBaseEquipmentObjectId, "P1", "P1"),
                [SecondFriendlyBaseEquipmentObjectId] = Equipment(SecondFriendlyBaseEquipmentObjectId, "P1", "P1"),
                [HandEquipmentObjectId] = Equipment(HandEquipmentObjectId, "P1", "P1"),
                [FaceDownEquipmentObjectId] = Equipment(FaceDownEquipmentObjectId, "P1", "P1", isFaceDown: true),
                [DirtyControllerEquipmentObjectId] = Equipment(DirtyControllerEquipmentObjectId, "P1", "P2"),
                [EnemyEquipmentObjectId] = Equipment(EnemyEquipmentObjectId, "P2", "P2")
            },
            ObjectLocations = objectLocations
        };
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string ownerId,
        string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Equipment(
        string objectId,
        string ownerId,
        string controllerId,
        bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·022/221",
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static bool IsOrnnUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, OrnnObjectId, StringComparison.Ordinal);
    }
}
