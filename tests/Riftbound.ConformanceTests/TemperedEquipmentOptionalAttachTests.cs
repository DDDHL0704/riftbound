using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TemperedEquipmentOptionalAttachTests
{
    private const string SentinelObjectId = "P1-UNIT-SENTINEL-ADEPT";
    private const string SentinelCardNo = "SFD·008/221";
    private const string SpinningAxeObjectId = "P1-EQUIPMENT-SPINNING-AXE";
    private const string SpinningAxeCardNo = "SFD·186/221";

    [Fact]
    public void PromptExposesOnlyLegalTemperedAttachChoiceForSentinelAdept()
    {
        var state = BuildTemperedState();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, SentinelObjectId, StringComparison.Ordinal));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);

        Assert.Equal(
            [TemperedAttachCost(SpinningAxeObjectId)],
            optionalCostChoices.Select(choice => choice.Id).ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));
    }

    [Fact]
    public async Task LegalTemperedOptionalAttachAttachesAfterBothPlayersPass()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedState();
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId) };

        var played = await PlaySentinelAdeptAsync(engine, state, optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);
        Assert.DoesNotContain(SentinelObjectId, played.State.PlayerZones["P1"].Hand);

        var p1Pass = await PassPriorityAsync(engine, played.State, "P1", "intent-tempered-p1-pass");
        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-tempered-p2-pass");

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(SentinelObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(SentinelObjectId, p2Pass.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Tempered, p2Pass.State.CardObjects[SentinelObjectId].Tags);

        var attachedEvent = Assert.Single(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(SpinningAxeObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal(SentinelObjectId, attachedEvent.Payload["unitObjectId"]);
        Assert.Equal(SentinelObjectId, attachedEvent.Payload["attachedToObjectId"]);
        Assert.Equal(SpinningAxeCardNo, attachedEvent.Payload["equipmentCardNo"]);
        Assert.Equal(SentinelCardNo, attachedEvent.Payload["unitCardNo"]);
        Assert.Equal("TEMPERED_OPTIONAL_ATTACH", attachedEvent.Payload["reason"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(attachedEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task NoTemperedOptionalAttachStillPlaysSentinelAdeptWithoutAttachment()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedState();

        var played = await PlaySentinelAdeptAsync(engine, state);
        var p1Pass = await PassPriorityAsync(engine, played.State, "P1", "intent-tempered-no-optional-p1-pass");
        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-tempered-no-optional-p2-pass");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains(SentinelObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Null(p2Pass.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.DoesNotContain(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-EQUIPMENT-SPINNING-AXE")]
    [InlineData("P1-MISSING-SPINNING-AXE")]
    [InlineData("P1-NON-EQUIPMENT-SPINNING-AXE")]
    [InlineData("P1-HAND-SPINNING-AXE")]
    [InlineData("P1-FACE-DOWN-SPINNING-AXE")]
    [InlineData("P1-STALE-SPINNING-AXE")]
    [InlineData("P1-WRONG-CARD-EQUIPMENT")]
    [InlineData("P1-WRONG-CONTROLLER-SPINNING-AXE")]
    public async Task InvalidTemperedOptionalAttachChoiceRejectsWithoutMutation(string equipmentObjectId)
    {
        var state = BuildTemperedState();

        var result = await PlaySentinelAdeptAsync(
            new CoreRuleEngine(),
            state,
            [TemperedAttachCost(equipmentObjectId)]);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Contains(SentinelObjectId, result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(SentinelObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task ResolutionStaleTemperedAttachChoiceNoEffectsWithoutAttachEvent()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedState();
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId) };
        var played = await PlaySentinelAdeptAsync(engine, state, optionalCosts);
        var staleState = MoveSpinningAxeToGraveyard(played.State);

        var p1Pass = await PassPriorityAsync(engine, staleState, "P1", "intent-tempered-stale-p1-pass");
        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-tempered-stale-p2-pass");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains(SentinelObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(SpinningAxeObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Null(p2Pass.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.DoesNotContain(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySentinelAdeptAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-tempered-sentinel-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SentinelObjectId,
                SentinelCardNo,
                [],
                OptionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> PassPriorityAsync(
        CoreRuleEngine engine,
        MatchState state,
        string playerId,
        string intentId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent(intentId, playerId, CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static MatchState MoveSpinningAxeToGraveyard(MatchState state)
    {
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var p1Zones = playerZones["P1"];
        playerZones["P1"] = p1Zones with
        {
            Base = p1Zones.Base
                .Where(objectId => !string.Equals(objectId, SpinningAxeObjectId, StringComparison.Ordinal))
                .ToArray(),
            Graveyard = p1Zones.Graveyard
                .Concat([SpinningAxeObjectId])
                .ToArray()
        };

        return state with
        {
            PlayerZones = playerZones
        };
    }

    private static MatchState BuildTemperedState()
    {
        return new MatchState(
            roomId: "tempered-equipment-optional-attach-test",
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
                    Hand = [SentinelObjectId, "P1-HAND-SPINNING-AXE"],
                    Base =
                    [
                        SpinningAxeObjectId,
                        "P1-NON-EQUIPMENT-SPINNING-AXE",
                        "P1-FACE-DOWN-SPINNING-AXE",
                        "P1-WRONG-CARD-EQUIPMENT",
                        "P1-WRONG-CONTROLLER-SPINNING-AXE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-EQUIPMENT-SPINNING-AXE"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [SentinelObjectId] = UnitCard(SentinelObjectId, SentinelCardNo, ownerId: "P1", controllerId: "P1"),
                [SpinningAxeObjectId] = SpinningAxe(SpinningAxeObjectId, "P1", "P1"),
                ["P1-HAND-SPINNING-AXE"] = SpinningAxe("P1-HAND-SPINNING-AXE", "P1", "P1"),
                ["P1-STALE-SPINNING-AXE"] = SpinningAxe("P1-STALE-SPINNING-AXE", "P1", "P1"),
                ["P1-FACE-DOWN-SPINNING-AXE"] = SpinningAxe("P1-FACE-DOWN-SPINNING-AXE", "P1", "P1", isFaceDown: true),
                ["P1-NON-EQUIPMENT-SPINNING-AXE"] = new(
                    "P1-NON-EQUIPMENT-SPINNING-AXE",
                    cardNo: SpinningAxeCardNo,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-WRONG-CARD-EQUIPMENT"] = new(
                    "P1-WRONG-CARD-EQUIPMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-WRONG-CONTROLLER-SPINNING-AXE"] = SpinningAxe(
                    "P1-WRONG-CONTROLLER-SPINNING-AXE",
                    "P1",
                    "P2"),
                ["P2-EQUIPMENT-SPINNING-AXE"] = SpinningAxe("P2-EQUIPMENT-SPINNING-AXE", "P2", "P2")
            });
    }

    private static CardObjectState UnitCard(
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

    private static CardObjectState SpinningAxe(
        string objectId,
        string ownerId,
        string controllerId,
        bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: SpinningAxeCardNo,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon, CardEquipmentKeywordNames.Agile],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static string TemperedAttachCost(string equipmentObjectId)
    {
        return $"TEMPERED_ATTACH:{equipmentObjectId}";
    }
}
