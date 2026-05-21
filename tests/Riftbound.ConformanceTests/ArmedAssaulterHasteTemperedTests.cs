using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ArmedAssaulterHasteTemperedTests
{
    private const string ArmedAssaulterObjectId = "P1-UNIT-ARMED-ASSAULTER";
    private const string ArmedAssaulterCardNo = "SFD·002/221";
    private const string SpinningAxeObjectId = "P1-EQUIPMENT-SPINNING-AXE";
    private const string SecondSpinningAxeObjectId = "P1-EQUIPMENT-SPINNING-AXE-2";
    private const string SpinningAxeCardNo = "SFD·186/221";
    private const string RedPower = "red";

    [Fact]
    public void PromptExposesHasteReadyAndLegalTemperedAttachChoicesForArmedAssaulter()
    {
        var state = BuildArmedAssaulterState(mana: 7, redPower: 1);

        var requirement = ArmedAssaulterSourceRequirement(state);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                requirement["optionalCostChoices"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Equal(
            [TemperedAttachCost(SpinningAxeObjectId), TemperedAttachCost(SecondSpinningAxeObjectId), HasteOptionalCostNames.HasteReady],
            optionalCostChoices);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(requirement["hasteReadyPowerCost"]));
        Assert.Equal(RuneTrait.Red, requirement["hasteReadyPowerTrait"]);

        var wrongTraitRequirement = ArmedAssaulterSourceRequirement(BuildArmedAssaulterState(mana: 7, greenPower: 1));
        var wrongTraitChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                wrongTraitRequirement["optionalCostChoices"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Equal(
            [TemperedAttachCost(SpinningAxeObjectId), TemperedAttachCost(SecondSpinningAxeObjectId)],
            wrongTraitChoices);
    }

    [Fact]
    public async Task LegalBothCostsPayHasteAndAttachAfterResolution()
    {
        var engine = new CoreRuleEngine();
        var state = BuildArmedAssaulterState(mana: 7, redPower: 1);
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId), HasteOptionalCostNames.HasteReady };

        var played = await PlayArmedAssaulterAsync(engine, state, optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RedPower));
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);
        var costEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(6, costEvent.Payload["baseManaCost"]);
        Assert.Equal(7, costEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, powerByTrait[RuneTrait.Red]);

        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(ArmedAssaulterObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[ArmedAssaulterObjectId].IsExhausted);
        Assert.Equal(ArmedAssaulterObjectId, resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);

        var unitEvent = Assert.Single(resolved.Events, IsArmedAssaulterUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(true, unitEvent.Payload["hasteReadyOptionalCostPaid"]);
        var attachEvent = Assert.Single(resolved.Events, IsTemperedAttachEvent);
        Assert.Equal(SpinningAxeObjectId, attachEvent.Payload["equipmentObjectId"]);
        Assert.Equal(ArmedAssaulterObjectId, attachEvent.Payload["attachedToObjectId"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(attachEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task ReplayingAcceptedHasteReadyTemperedPlayCardRejectsWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = BuildArmedAssaulterState(mana: 7, redPower: 1);
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId), HasteOptionalCostNames.HasteReady };
        var command = ArmedAssaulterCommand(optionalCosts);

        var played = await PlayArmedAssaulterAsync(engine, state, command);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], played.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var postAcceptanceHash = MatchStateHasher.Hash(played.State);
        var postAcceptanceRunePool = played.State.RunePools["P1"];
        var postAcceptanceHand = played.State.PlayerZones["P1"].Hand.ToArray();
        var postAcceptanceBase = played.State.PlayerZones["P1"].Base.ToArray();
        var postAcceptanceStack = played.State.StackItems.ToArray();
        var postAcceptanceEquipment = played.State.CardObjects[SpinningAxeObjectId];
        var postAcceptanceUnitLocation = played.State.ObjectLocations[ArmedAssaulterObjectId];

        var replay = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-armed-assaulter-replay", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.DoesNotContain(replay.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal(postAcceptanceHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postAcceptanceRunePool, replay.State.RunePools["P1"]);
        Assert.Equal(postAcceptanceHand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(postAcceptanceBase, replay.State.PlayerZones["P1"].Base);
        Assert.Equal(postAcceptanceStack, replay.State.StackItems);
        Assert.Equal(postAcceptanceEquipment, replay.State.CardObjects[SpinningAxeObjectId]);
        Assert.Equal(postAcceptanceUnitLocation, replay.State.ObjectLocations[ArmedAssaulterObjectId]);
        var stackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);
        Assert.Null(replay.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
    }

    [Fact]
    public async Task LegalTemperedOnlyAttachesButDoesNotSetHasteReadyFlag()
    {
        var engine = new CoreRuleEngine();
        var state = BuildArmedAssaulterState(mana: 6);
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId) };

        var played = await PlayArmedAssaulterAsync(engine, state, optionalCosts);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(ArmedAssaulterObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal(ArmedAssaulterObjectId, resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        var unitEvent = Assert.Single(resolved.Events, IsArmedAssaulterUnitPlayedEvent);
        Assert.Equal(resolved.State.CardObjects[ArmedAssaulterObjectId].IsExhausted, UnitEventIsExhausted(unitEvent));
        Assert.False(unitEvent.Payload.ContainsKey("hasteReadyOptionalCostPaid"));
        Assert.Contains(resolved.Events, IsTemperedAttachEvent);
    }

    [Fact]
    public async Task LegalHasteOnlyPreservesActiveEntryWithoutAttachment()
    {
        var engine = new CoreRuleEngine();
        var state = BuildArmedAssaulterState(mana: 7, redPower: 1);

        var played = await PlayArmedAssaulterAsync(engine, state, [HasteOptionalCostNames.HasteReady]);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(ArmedAssaulterObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[ArmedAssaulterObjectId].IsExhausted);
        Assert.Null(resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        var unitEvent = Assert.Single(resolved.Events, IsArmedAssaulterUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(true, unitEvent.Payload["hasteReadyOptionalCostPaid"]);
        Assert.DoesNotContain(resolved.Events, IsTemperedAttachEvent);
    }

    [Fact]
    public async Task NoOptionalCostsStillPlaysWithoutHasteFlagOrAttachment()
    {
        var engine = new CoreRuleEngine();
        var state = BuildArmedAssaulterState(mana: 6);

        var played = await PlayArmedAssaulterAsync(engine, state, []);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(ArmedAssaulterObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Null(resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        var unitEvent = Assert.Single(resolved.Events, IsArmedAssaulterUnitPlayedEvent);
        Assert.Equal(resolved.State.CardObjects[ArmedAssaulterObjectId].IsExhausted, UnitEventIsExhausted(unitEvent));
        Assert.False(unitEvent.Payload.ContainsKey("hasteReadyOptionalCostPaid"));
        Assert.DoesNotContain(resolved.Events, IsTemperedAttachEvent);
    }

    [Theory]
    [InlineData("duplicate-haste")]
    [InlineData("duplicate-tempered")]
    [InlineData("duplicate-tempered-with-haste")]
    [InlineData("conflicting-tempered")]
    [InlineData("missing-equipment")]
    [InlineData("enemy-equipment")]
    [InlineData("non-equipment")]
    [InlineData("wrong-card-equipment")]
    [InlineData("hand-equipment")]
    [InlineData("face-down-equipment")]
    [InlineData("stale-equipment")]
    [InlineData("wrong-controller-equipment")]
    [InlineData("malformed-tempered")]
    [InlineData("unrelated-optional")]
    [InlineData("insufficient-mana")]
    [InlineData("insufficient-red")]
    [InlineData("wrong-trait")]
    public async Task InvalidCombinationOrPaymentRejectsWithoutMutation(string scenario)
    {
        var state = scenario switch
        {
            "insufficient-mana" => BuildArmedAssaulterState(mana: 6, redPower: 1),
            "insufficient-red" => BuildArmedAssaulterState(mana: 7),
            "wrong-trait" => BuildArmedAssaulterState(mana: 7, greenPower: 1),
            _ => BuildArmedAssaulterState(mana: 7, redPower: 1)
        };
        var optionalCosts = scenario switch
        {
            "duplicate-haste" => new[] { HasteOptionalCostNames.HasteReady, HasteOptionalCostNames.HasteReady },
            "duplicate-tempered" => [TemperedAttachCost(SpinningAxeObjectId), TemperedAttachCost(SpinningAxeObjectId)],
            "duplicate-tempered-with-haste" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost(SpinningAxeObjectId), TemperedAttachCost(SpinningAxeObjectId)],
            "conflicting-tempered" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost(SpinningAxeObjectId), TemperedAttachCost(SecondSpinningAxeObjectId)],
            "missing-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P1-MISSING-SPINNING-AXE")],
            "enemy-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P2-EQUIPMENT-SPINNING-AXE")],
            "non-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P1-NON-EQUIPMENT-SPINNING-AXE")],
            "wrong-card-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P1-WRONG-CARD-EQUIPMENT")],
            "hand-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P1-HAND-SPINNING-AXE")],
            "face-down-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P1-FACE-DOWN-SPINNING-AXE")],
            "stale-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P1-STALE-SPINNING-AXE")],
            "wrong-controller-equipment" => [HasteOptionalCostNames.HasteReady, TemperedAttachCost("P1-WRONG-CONTROLLER-SPINNING-AXE")],
            "malformed-tempered" => [HasteOptionalCostNames.HasteReady, "TEMPERED_ATTACH:"],
            "unrelated-optional" => [HasteOptionalCostNames.HasteReady, "UNSUPPORTED_OPTIONAL_COST"],
            _ => [HasteOptionalCostNames.HasteReady, TemperedAttachCost(SpinningAxeObjectId)]
        };

        await AssertRejectedNoMutationAsync(state, optionalCosts);
    }

    [Fact]
    public async Task StaleSelectedEquipmentBeforeResolutionKeepsHasteButSkipsAttach()
    {
        var engine = new CoreRuleEngine();
        var state = BuildArmedAssaulterState(mana: 7, redPower: 1);
        var optionalCosts = new[] { HasteOptionalCostNames.HasteReady, TemperedAttachCost(SpinningAxeObjectId) };
        var played = await PlayArmedAssaulterAsync(engine, state, optionalCosts);
        var staleState = MoveSpinningAxeToGraveyard(played.State);

        var resolved = await ResolveTopStackAsync(engine, staleState);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(ArmedAssaulterObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[ArmedAssaulterObjectId].IsExhausted);
        Assert.Contains(SpinningAxeObjectId, resolved.State.PlayerZones["P1"].Graveyard);
        Assert.Null(resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        var unitEvent = Assert.Single(resolved.Events, IsArmedAssaulterUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["hasteReadyOptionalCostPaid"]);
        Assert.DoesNotContain(resolved.Events, IsTemperedAttachEvent);
    }

    private static IReadOnlyDictionary<string, object?> ArmedAssaulterSourceRequirement(MatchState state)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        return Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            requirement => string.Equals(requirement["sourceObjectId"] as string, ArmedAssaulterObjectId, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayArmedAssaulterAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> optionalCosts)
    {
        return await PlayArmedAssaulterAsync(engine, state, ArmedAssaulterCommand(optionalCosts));
    }

    private static async Task<ResolutionResult> PlayArmedAssaulterAsync(
        CoreRuleEngine engine,
        MatchState state,
        PlayCardCommand command)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-armed-assaulter-play", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);
    }

    private static PlayCardCommand ArmedAssaulterCommand(IReadOnlyList<string> optionalCosts)
    {
        return new PlayCardCommand(
            ArmedAssaulterObjectId,
            ArmedAssaulterCardNo,
            [],
            OptionalCosts: optionalCosts);
    }

    private static async Task<ResolutionResult> ResolveTopStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-armed-assaulter-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-armed-assaulter-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        IReadOnlyList<string> optionalCosts)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await PlayArmedAssaulterAsync(new CoreRuleEngine(), state, optionalCosts);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
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

        var objectLocations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        objectLocations[SpinningAxeObjectId] = new ObjectLocationState("P1", "GRAVEYARD");

        return state with
        {
            PlayerZones = playerZones,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BuildArmedAssaulterState(
        int mana,
        int redPower = 0,
        int greenPower = 0)
    {
        var powerByTrait = new Dictionary<string, int>(StringComparer.Ordinal);
        if (redPower > 0)
        {
            powerByTrait[RuneTrait.Red] = redPower;
        }

        if (greenPower > 0)
        {
            powerByTrait[RuneTrait.Green] = greenPower;
        }

        var p1Base = new[]
        {
            SpinningAxeObjectId,
            SecondSpinningAxeObjectId,
            "P1-NON-EQUIPMENT-SPINNING-AXE",
            "P1-FACE-DOWN-SPINNING-AXE",
            "P1-WRONG-CARD-EQUIPMENT",
            "P1-WRONG-CONTROLLER-SPINNING-AXE"
        };
        var p1Hand = new[] { ArmedAssaulterObjectId, "P1-HAND-SPINNING-AXE" };
        var p2Base = new[] { "P2-EQUIPMENT-SPINNING-AXE" };

        var objectLocations = p1Hand
            .Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "HAND")))
            .Concat(p1Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P1", "BASE"))))
            .Concat(p2Base.Select(objectId => new KeyValuePair<string, ObjectLocationState>(objectId, new ObjectLocationState("P2", "BASE"))))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        return new MatchState(
            "armed-assaulter-haste-tempered",
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
                ["P1"] = new RunePool(mana, 0, powerByTrait),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = p1Hand,
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [ArmedAssaulterObjectId] = UnitCard(ArmedAssaulterObjectId, ArmedAssaulterCardNo, "P1", "P1"),
                [SpinningAxeObjectId] = SpinningAxe(SpinningAxeObjectId, "P1", "P1"),
                [SecondSpinningAxeObjectId] = SpinningAxe(SecondSpinningAxeObjectId, "P1", "P1"),
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
            },
            ObjectLocations = objectLocations
        };
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

    private static bool IsArmedAssaulterUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, ArmedAssaulterObjectId, StringComparison.Ordinal);
    }

    private static bool IsTemperedAttachEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "TEMPERED_OPTIONAL_ATTACH", StringComparison.Ordinal);
    }

    private static bool UnitEventIsExhausted(GameEvent gameEvent)
    {
        return gameEvent.Payload.TryGetValue("isExhausted", out var isExhausted)
            && isExhausted is true;
    }

    private static string TemperedAttachCost(string equipmentObjectId)
    {
        return $"TEMPERED_ATTACH:{equipmentObjectId}";
    }
}
