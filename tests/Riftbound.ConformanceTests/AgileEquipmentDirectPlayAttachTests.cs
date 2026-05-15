using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AgileEquipmentDirectPlayAttachTests
{
    [Theory]
    [InlineData("SFD·022/221", "P1-EQUIPMENT-LONG-SWORD", 2)]
    [InlineData("SFD·056/221", "P1-EQUIPMENT-STERAKS", 3)]
    [InlineData("SFD·064/221", "P1-EQUIPMENT-CLOTH-ARMOR", 1)]
    [InlineData("SFD·186/221", "P1-EQUIPMENT-SPINNING-AXE", 2)]
    public async Task AgileEquipmentDirectPlayAttachesToControlledUnit(
        string cardNo,
        string sourceObjectId,
        int manaCost)
    {
        var engine = new CoreRuleEngine();
        var state = BuildAgileEquipmentState(cardNo, sourceObjectId, manaCost);

        var played = await PlayAgileEquipmentAsync(engine, state, cardNo, sourceObjectId, "P1-BASE-UNIT");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(3 - manaCost, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal([sourceObjectId], played.State.ObjectLocations
            .Where(entry => string.Equals(entry.Value.Zone, "STACK", StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToArray());
        Assert.Equal(["P1-BASE-UNIT"], stackItem.TargetObjectIds);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-agile-equipment-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-agile-equipment-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(sourceObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal("P1-BASE-UNIT", p2Pass.State.CardObjects[sourceObjectId].AttachedToObjectId);
        Assert.Contains(CardObjectTags.EquipmentCard, p2Pass.State.CardObjects[sourceObjectId].Tags);
        Assert.Contains("灵便", p2Pass.State.CardObjects[sourceObjectId].Tags);
        Assert.Equal("P1", p2Pass.State.ObjectLocations[sourceObjectId].PlayerId);
        Assert.Equal("BASE", p2Pass.State.ObjectLocations[sourceObjectId].Zone);

        var attachedEvent = Assert.Single(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(sourceObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1-BASE-UNIT", attachedEvent.Payload["unitObjectId"]);
        Assert.Equal("P1-BASE-UNIT", attachedEvent.Payload["attachedToObjectId"]);
        Assert.Equal(cardNo, attachedEvent.Payload["equipmentCardNo"]);
        Assert.Equal("AGILE_DIRECT_PLAY_ATTACH", attachedEvent.Payload["reason"]);
    }

    [Fact]
    public void AgileEquipmentPromptExposesControlledUnitTarget()
    {
        var state = BuildAgileEquipmentState("SFD·022/221", "P1-EQUIPMENT-LONG-SWORD", 2);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-EQUIPMENT-LONG-SWORD", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P1-BASE-UNIT", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BASE-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P1-EQUIPMENT-TARGET", StringComparison.Ordinal));

        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]);
        var requirement = Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, "P1-EQUIPMENT-LONG-SWORD", StringComparison.Ordinal));
        Assert.Equal(1, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(requirement["maxTargetCount"]));
        Assert.Equal(CardTargetScopes.FriendlyUnit, Assert.IsType<string>(requirement["targetScope"]));
        Assert.Equal("友方单位", Assert.IsType<string>(requirement["targetScopeLabel"]));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(requirement["targetChoicesByIndex"]);
        var firstChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"]);
        Assert.Equal(
            ["P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT"],
            firstChoices.Select(choice => choice.Id).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P1-EQUIPMENT-TARGET")]
    [InlineData("P1-SPELL-TARGET")]
    [InlineData("P1-RUNE-TARGET")]
    [InlineData("P1-FACE-DOWN-UNIT")]
    [InlineData("P1-STALE-UNIT")]
    [InlineData("P1-NONCONTROLLED-UNIT")]
    public async Task AgileEquipmentDirectPlayRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildAgileEquipmentState("SFD·022/221", "P1-EQUIPMENT-LONG-SWORD", 2);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-agile-equipment-invalid", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "SFD·022/221",
                targetObjectIds),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-EQUIPMENT-LONG-SWORD", result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayAgileEquipmentAsync(
        CoreRuleEngine engine,
        MatchState state,
        string cardNo,
        string sourceObjectId,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-agile-equipment-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(sourceObjectId, cardNo, [targetObjectId]),
            CancellationToken.None);
    }

    private static MatchState BuildAgileEquipmentState(
        string cardNo,
        string sourceObjectId,
        int manaCost)
    {
        return new MatchState(
            roomId: "agile-equipment-direct-play-test",
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
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base =
                    [
                        "P1-BASE-UNIT",
                        "P1-EQUIPMENT-TARGET",
                        "P1-SPELL-TARGET",
                        "P1-RUNE-TARGET",
                        "P1-FACE-DOWN-UNIT",
                        "P1-NONCONTROLLED-UNIT"
                    ],
                    Battlefields = ["P1-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = Equipment(sourceObjectId, cardNo, manaCost),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", "P1", "P1"),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT", "P1", "P1"),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", "P2", "P2"),
                ["P1-NONCONTROLLED-UNIT"] = Unit("P1-NONCONTROLLED-UNIT", "P1", "P2"),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT", "P1", "P1"),
                ["P1-FACE-DOWN-UNIT"] = Unit("P1-FACE-DOWN-UNIT", "P1", "P1", isFaceDown: true),
                ["P1-EQUIPMENT-TARGET"] = NonUnit("P1-EQUIPMENT-TARGET", "SFD·139/221", CardObjectTags.EquipmentCard),
                ["P1-SPELL-TARGET"] = NonUnit("P1-SPELL-TARGET", "SFD·006/221", CardObjectTags.SpellCard),
                ["P1-RUNE-TARGET"] = NonUnit("P1-RUNE-TARGET", "FND·001/298", CardObjectTags.RuneCard)
            });
    }

    private static CardObjectState Equipment(string objectId, string cardNo, int manaCost)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            manaCost: manaCost,
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Unit(
        string objectId,
        string ownerId,
        string controllerId,
        bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·125/221",
            power: 3,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(string objectId, string cardNo, string tag)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [tag],
            ownerId: "P1",
            controllerId: "P1");
    }
}
