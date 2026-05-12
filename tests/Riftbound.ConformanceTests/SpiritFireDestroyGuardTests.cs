using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SpiritFireDestroyGuardTests
{
    [Fact]
    public async Task SpiritFireDestroysBattlefieldUnitsWithTotalPowerFour()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSpiritFireState();

        var played = await PlaySpiritFireAsync(
            engine,
            state,
            ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-spirit-fire-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-spirit-fire-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P2-SPIRIT-FIRE-UNIT-001", p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P2-SPIRIT-FIRE-UNIT-002", p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P2-SPIRIT-FIRE-KEEPER-001", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-001", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-002", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-SPIRIT-FIRE-UNIT-001", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-SPIRIT-FIRE-UNIT-002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SpiritFireRejectsTotalPowerAboveFourWithoutMutation()
    {
        var state = BuildSpiritFireState();

        var result = await PlaySpiritFireAsync(
            new CoreRuleEngine(),
            state,
            ["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-KEEPER-001"]);

        AssertRejectedWithoutMutation(state, result);
    }

    [Theory]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P1-HAND-UNIT")]
    public async Task SpiritFireRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildSpiritFireState();

        var result = await PlaySpiritFireAsync(
            new CoreRuleEngine(),
            state,
            [targetObjectId]);

        AssertRejectedWithoutMutation(state, result);
    }

    [Fact]
    public void SpiritFirePromptChoicesAndLegalSelectionsOnlyExposePublicBattlefieldUnits()
    {
        var state = BuildSpiritFireState();

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
            requirement => string.Equals(requirement["sourceObjectId"] as string, "P1-SPELL-SPIRIT-FIRE", StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();
        var legalTargetSelections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
                sourceRequirement["legalTargetSelections"])
            .ToArray();

        Assert.Contains("P2-SPIRIT-FIRE-UNIT-001", firstTargetChoiceIds);
        Assert.Contains("P2-SPIRIT-FIRE-UNIT-002", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-SPELL", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-RUNE", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P2-BASE-UNIT", firstTargetChoiceIds);
        Assert.DoesNotContain("P1-HAND-UNIT", firstTargetChoiceIds);

        Assert.Contains(legalTargetSelections, selection =>
            selection.SequenceEqual(["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"]));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.Contains("P2-SPIRIT-FIRE-KEEPER-001", StringComparer.Ordinal));
        Assert.DoesNotContain(legalTargetSelections, selection =>
            selection.Contains("P2-BATTLEFIELD-EQUIPMENT", StringComparer.Ordinal)
            || selection.Contains("P2-BATTLEFIELD-SPELL", StringComparer.Ordinal)
            || selection.Contains("P2-BATTLEFIELD-RUNE", StringComparer.Ordinal)
            || selection.Contains("P2-FACE-DOWN-STANDBY", StringComparer.Ordinal)
            || selection.Contains("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", StringComparer.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySpiritFireAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-spirit-fire-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-SPIRIT-FIRE",
                "OGN·256/298",
                targetObjectIds),
            CancellationToken.None);
    }

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SPIRIT-FIRE", "P1-HAND-UNIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    private static MatchState BuildSpiritFireState()
    {
        return new MatchState(
            roomId: "spirit-fire-target-guard-test",
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
                    Hand = ["P1-SPELL-SPIRIT-FIRE", "P1-HAND-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-SPIRIT-FIRE-UNIT-001",
                        "P2-SPIRIT-FIRE-UNIT-002",
                        "P2-SPIRIT-FIRE-KEEPER-001",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SPIRIT-FIRE"] = SpiritFire(),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT"),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P2-SPIRIT-FIRE-UNIT-001"] = Unit("P2-SPIRIT-FIRE-UNIT-001", ownerId: "P2", controllerId: "P2"),
                ["P2-SPIRIT-FIRE-UNIT-002"] = Unit("P2-SPIRIT-FIRE-UNIT-002", ownerId: "P2", controllerId: "P2"),
                ["P2-SPIRIT-FIRE-KEEPER-001"] = Unit("P2-SPIRIT-FIRE-KEEPER-001", power: 5, ownerId: "P2", controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit("P2-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P2"),
                ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
                ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2"),
                ["P2-FACE-DOWN-STANDBY"] = Unit(
                    "P2-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"),
                ["P2-STALE-UNIT"] = Unit("P2-STALE-UNIT", ownerId: "P2", controllerId: "P2")
            });
    }

    private static CardObjectState SpiritFire()
    {
        return new CardObjectState(
            "P1-SPELL-SPIRIT-FIRE",
            cardNo: "OGN·256/298",
            manaCost: 3,
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
        string tag,
        string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
