using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReflectionsSwapGuardTests
{
    [Fact]
    public async Task ReflectionsSwapsFriendlyBaseAndBattlefieldUnitsAndDrawsOne()
    {
        var engine = new CoreRuleEngine();
        var state = BuildReflectionsState();

        var played = await PlayReflectionsAsync(
            engine,
            state,
            ["P1-BASE-EPHEMERAL", "P1-BATTLEFIELD-UNIT"]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_1", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-reflections-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-reflections-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains("P1-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-BASE-EPHEMERAL", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-REFLECTIONS-DRAW-001"], p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SPELL-REFLECTIONS"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["firstTargetObjectId"] as string, "P1-BASE-EPHEMERAL", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["secondTargetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal)
            && Assert.IsType<int>(gameEvent.Payload["count"]) == 1);
    }

    [Theory]
    [InlineData("P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-EPHEMERAL", "P1-BASE-UNIT")]
    public async Task ReflectionsRejectsMissingEphemeralOrSamePositionPairsWithoutMutation(
        string firstTargetObjectId,
        string secondTargetObjectId)
    {
        var state = BuildReflectionsState();

        var result = await PlayReflectionsAsync(
            new CoreRuleEngine(),
            state,
            [firstTargetObjectId, secondTargetObjectId]);

        AssertRejectedWithoutMutation(result);
    }

    [Theory]
    [InlineData("P1-BASE-EQUIPMENT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-SPELL", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-RUNE", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-FACE-DOWN-STANDBY", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-STALE-UNIT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P2-ENEMY-UNIT", "P1-BATTLEFIELD-UNIT")]
    [InlineData("P1-DIRTY-P2-CONTROLLED-BASE-UNIT", "P1-BATTLEFIELD-UNIT")]
    public async Task ReflectionsRejectsInvalidTargetsWithoutMutation(
        string firstTargetObjectId,
        string secondTargetObjectId)
    {
        var state = BuildReflectionsState();

        var result = await PlayReflectionsAsync(
            new CoreRuleEngine(),
            state,
            [firstTargetObjectId, secondTargetObjectId]);

        AssertRejectedWithoutMutation(result);
    }

    [Fact]
    public void ReflectionsPromptLegalSelectionsRequireEphemeralAndDifferentPositions()
    {
        var state = BuildReflectionsState();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-REFLECTIONS", StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();
        var legalTargetSelections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
                sourceRequirement["legalTargetSelections"])
            .ToArray();

        Assert.Contains("P1-BASE-EPHEMERAL", firstTargetChoiceIds);
        Assert.Contains("P1-BATTLEFIELD-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-EQUIPMENT", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-SPELL", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-BASE-RUNE", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-DIRTY-P2-CONTROLLED-BASE-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-ENEMY-UNIT", firstTargetChoiceIds);

        Assert.Contains(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-EPHEMERAL", "P1-BATTLEFIELD-UNIT"]));
        Assert.Contains(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-UNIT", "P1-BATTLEFIELD-EPHEMERAL"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.SequenceEqual(["P1-BASE-EPHEMERAL", "P1-BASE-UNIT"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.Contains("P1-BASE-EQUIPMENT", StringComparer.Ordinal)
            || selection.Contains("P1-BASE-SPELL", StringComparer.Ordinal)
            || selection.Contains("P1-BASE-RUNE", StringComparer.Ordinal)
            || selection.Contains("P1-FACE-DOWN-STANDBY", StringComparer.Ordinal));
    }

    private static async Task<ResolutionResult> PlayReflectionsAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-reflections-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-REFLECTIONS",
                "UNL-083/219",
                targetObjectIds),
            CancellationToken.None);
    }

    private static void AssertRejectedWithoutMutation(ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-REFLECTIONS"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-REFLECTIONS-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(
            [
                "P1-BASE-EPHEMERAL",
                "P1-BASE-UNIT",
                "P1-BASE-EQUIPMENT",
                "P1-BASE-SPELL",
                "P1-BASE-RUNE",
                "P1-FACE-DOWN-STANDBY",
                "P1-DIRTY-P2-CONTROLLED-BASE-UNIT"
            ],
            result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-BATTLEFIELD-UNIT",
                "P1-BATTLEFIELD-EPHEMERAL"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-ENEMY-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    private static MatchState BuildReflectionsState()
    {
        return new MatchState(
            roomId: "reflections-swap-guard-test",
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
                    MainDeck = ["P1-REFLECTIONS-DRAW-001"],
                    Hand = ["P1-SPELL-REFLECTIONS"],
                    Base =
                    [
                        "P1-BASE-EPHEMERAL",
                        "P1-BASE-UNIT",
                        "P1-BASE-EQUIPMENT",
                        "P1-BASE-SPELL",
                        "P1-BASE-RUNE",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-DIRTY-P2-CONTROLLED-BASE-UNIT"
                    ],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNIT",
                        "P1-BATTLEFIELD-EPHEMERAL"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-REFLECTIONS"] = Reflections(),
                ["P1-REFLECTIONS-DRAW-001"] = Unit("P1-REFLECTIONS-DRAW-001"),
                ["P1-BASE-EPHEMERAL"] = Unit(
                    "P1-BASE-EPHEMERAL",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT"),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT"),
                ["P1-BATTLEFIELD-EPHEMERAL"] = Unit(
                    "P1-BATTLEFIELD-EPHEMERAL",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P1-BASE-EQUIPMENT"] = NonUnit("P1-BASE-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard),
                ["P1-BASE-SPELL"] = NonUnit("P1-BASE-SPELL", "OGN·169/298", CardObjectTags.SpellCard),
                ["P1-BASE-RUNE"] = NonUnit("P1-BASE-RUNE", "RUNES·001", CardObjectTags.RuneCard),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT"),
                ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"] = Unit(
                    "P1-DIRTY-P2-CONTROLLED-BASE-UNIT",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-ENEMY-UNIT"] = Unit(
                    "P2-ENEMY-UNIT",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static CardObjectState Reflections()
    {
        return new CardObjectState(
            "P1-SPELL-REFLECTIONS",
            cardNo: "UNL-083/219",
            manaCost: 2,
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
        string tag)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
            ownerId: "P1",
            controllerId: "P1");
    }
}
