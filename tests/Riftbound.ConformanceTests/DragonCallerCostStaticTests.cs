using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DragonCallerCostStaticTests
{
    [Fact]
    public void DragonCallerPromptReducesDragonUnitManaCost()
    {
        var state = DragonCallerState(
            mana: 6,
            handObjectIds: ["P1-UNIT-RAGING-DRAKE", "P1-UNIT-STERN-SERGEANT"],
            dragonCallerObjectIds: ["P1-BASE-DRAGON-CALLER"]);

        var ragingDrakeRequirement = PlayCardRequirement(state, "P1-UNIT-RAGING-DRAKE");
        var sternSergeantRequirement = PlayCardRequirement(state, "P1-UNIT-STERN-SERGEANT");

        Assert.Equal(4, ragingDrakeRequirement["minimumManaCost"]);
        Assert.Equal(2, ragingDrakeRequirement["dragonUnitCostReductionMana"]);
        Assert.Equal(6, sternSergeantRequirement["minimumManaCost"]);
        Assert.Equal(0, sternSergeantRequirement["dragonUnitCostReductionMana"]);
    }

    [Fact]
    public async Task DragonCallerLetsControllerPayReducedCostForDragonUnit()
    {
        var state = DragonCallerState(
            mana: 4,
            handObjectIds: ["P1-UNIT-RAGING-DRAKE"],
            dragonCallerObjectIds: ["P1-BASE-DRAGON-CALLER"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-caller-reduced-dragon-unit", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-UNIT-RAGING-DRAKE", "OGN·031/298", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-UNIT-RAGING-DRAKE", stackItem.SourceObjectId);

        var costPaid = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(4, costPaid.Payload["mana"]);
        Assert.Equal(6, costPaid.Payload["baseMana"]);
        Assert.Equal(4, costPaid.Payload["totalManaCost"]);
        Assert.Equal(2, costPaid.Payload["dragonUnitCostReductionMana"]);
    }

    [Fact]
    public async Task DragonCallerCostReductionStacksButKeepsOneManaFloor()
    {
        var state = DragonCallerState(
            mana: 1,
            handObjectIds: ["P1-UNIT-BLAZING-DRAKE"],
            dragonCallerObjectIds: ["P1-BASE-DRAGON-CALLER-001", "P1-BASE-DRAGON-CALLER-002"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-caller-stacked-floor", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-UNIT-BLAZING-DRAKE", "OGN·001/298", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        var costPaid = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costPaid.Payload["mana"]);
        Assert.Equal(5, costPaid.Payload["baseMana"]);
        Assert.Equal(1, costPaid.Payload["totalManaCost"]);
        Assert.Equal(4, costPaid.Payload["dragonUnitCostReductionMana"]);
    }

    [Fact]
    public async Task DragonCallerDoesNotReduceNonDragonUnitManaCost()
    {
        var state = DragonCallerState(
            mana: 4,
            handObjectIds: ["P1-UNIT-STERN-SERGEANT"],
            dragonCallerObjectIds: ["P1-BASE-DRAGON-CALLER"]);
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-caller-non-dragon", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-UNIT-STERN-SERGEANT", "UNL-157/219", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(true, "P1", "P1", 6)]
    [InlineData(false, "P1", "P2", 6)]
    [InlineData(false, "P2", "P2", 6)]
    [InlineData(false, "P1", "P1", 4)]
    public void DragonCallerPromptRequiresPublicControlledStaticSource(
        bool sourceIsFaceDown,
        string sourceOwnerId,
        string sourceControllerId,
        int expectedMinimumManaCost)
    {
        var state = DragonCallerState(
            mana: 6,
            handObjectIds: ["P1-UNIT-RAGING-DRAKE"],
            dragonCallerObjectIds: ["P1-BASE-DRAGON-CALLER"],
            dragonCallerIsFaceDown: sourceIsFaceDown,
            dragonCallerOwnerId: sourceOwnerId,
            dragonCallerControllerId: sourceControllerId);

        var requirement = PlayCardRequirement(state, "P1-UNIT-RAGING-DRAKE");

        Assert.Equal(expectedMinimumManaCost, requirement["minimumManaCost"]);
        Assert.Equal(expectedMinimumManaCost == 4 ? 2 : 0, requirement["dragonUnitCostReductionMana"]);
    }

    [Fact]
    public void DragonCallerPromptRecomputesWhenStaticSourceLeavesPlay()
    {
        var withSource = DragonCallerState(
            mana: 6,
            handObjectIds: ["P1-UNIT-RAGING-DRAKE"],
            dragonCallerObjectIds: ["P1-BASE-DRAGON-CALLER"]);
        var withoutSource = DragonCallerState(
            mana: 6,
            handObjectIds: ["P1-UNIT-RAGING-DRAKE"],
            dragonCallerObjectIds: []);

        Assert.Equal(4, PlayCardRequirement(withSource, "P1-UNIT-RAGING-DRAKE")["minimumManaCost"]);
        Assert.Equal(6, PlayCardRequirement(withoutSource, "P1-UNIT-RAGING-DRAKE")["minimumManaCost"]);
    }

    private static IReadOnlyDictionary<string, object?> PlayCardRequirement(
        MatchState state,
        string sourceObjectId)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();

        return Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, sourceObjectId, StringComparison.Ordinal));
    }

    private static MatchState DragonCallerState(
        int mana,
        IReadOnlyList<string> handObjectIds,
        IReadOnlyList<string> dragonCallerObjectIds,
        bool dragonCallerIsFaceDown = false,
        string dragonCallerOwnerId = "P1",
        string dragonCallerControllerId = "P1")
    {
        var p1Base = dragonCallerObjectIds
            .Where(objectId => string.Equals(dragonCallerOwnerId, "P1", StringComparison.Ordinal))
            .ToArray();
        var p2Base = dragonCallerObjectIds
            .Where(objectId => string.Equals(dragonCallerOwnerId, "P2", StringComparison.Ordinal))
            .ToArray();
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);

        foreach (var objectId in handObjectIds)
        {
            cardObjects[objectId] = HandCard(objectId);
        }

        foreach (var objectId in dragonCallerObjectIds)
        {
            cardObjects[objectId] = DragonCaller(
                objectId,
                dragonCallerIsFaceDown,
                dragonCallerOwnerId,
                dragonCallerControllerId);
        }

        return new MatchState(
            roomId: "dragon-caller-cost-static-test",
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
                    Hand = handObjectIds,
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            cardObjects: cardObjects);
    }

    private static CardObjectState HandCard(string objectId)
    {
        return objectId switch
        {
            "P1-UNIT-RAGING-DRAKE" => new(
                objectId,
                cardNo: "OGN·031/298",
                manaCost: 6,
                power: 4,
                tags: [CardObjectTags.UnitCard, "龙"],
                ownerId: "P1",
                controllerId: "P1"),
            "P1-UNIT-BLAZING-DRAKE" => new(
                objectId,
                cardNo: "OGN·001/298",
                manaCost: 5,
                power: 5,
                tags: [CardObjectTags.UnitCard, "龙", "急速"],
                ownerId: "P1",
                controllerId: "P1"),
            "P1-UNIT-STERN-SERGEANT" => new(
                objectId,
                cardNo: "UNL-157/219",
                manaCost: 6,
                power: 6,
                tags: [CardObjectTags.UnitCard, "精锐"],
                ownerId: "P1",
                controllerId: "P1"),
            _ => throw new ArgumentOutOfRangeException(nameof(objectId), objectId, "Unknown hand card.")
        };
    }

    private static CardObjectState DragonCaller(
        string objectId,
        bool isFaceDown,
        string ownerId,
        string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "OGN·140/298",
            manaCost: 4,
            power: 3,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }
}
