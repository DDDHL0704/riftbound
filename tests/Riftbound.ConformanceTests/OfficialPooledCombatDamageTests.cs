using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 465.2.a-c: the player distributes the side's total damage, not separate
// independent pools that each need to satisfy lethal assignment on their own.
public sealed class OfficialPooledCombatDamageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourcesCanTogetherSatisfyOneTargetsLethalThreshold(bool bulwark)
    {
        var result = await Assign(Position(bulwark: bulwark), [new("A1", "D1", 2), new("A2", "D1", 1), new("A2", "D2", 1)]);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.All(result.State.CardObjects.Values, c => Assert.Equal(0, c.Damage));
        Assert.DoesNotContain(result.Events, e => e.Kind == "DAMAGE_APPLIED");
    }

    [Fact]
    public async Task SourcesCannotOverfillOneTargetWhileAnotherHasNoLethalDamage()
    {
        var state = Position();
        var result = await Assign(state, [new("A1", "D1", 2), new("A2", "D1", 2)]);
        AssertRejectedUnchanged(state, result);
    }

    [Fact]
    public async Task SamePriorityTargetsMayBeChosenInEitherOrder()
    {
        var result = await Assign(Position(singleAttacker: true, defenderTwoPower: 3), [new("A1", "D2", 2)]);
        Assert.True(result.Accepted, result.ErrorMessage);
    }

    [Fact]
    public async Task TwoPartiallyDamagedTargetsAreNotACompleteLegalDistribution()
    {
        var state = Position(defenderTwoPower: 3);
        var result = await Assign(state, [new("A1", "D1", 1), new("A1", "D2", 1), new("A2", "D1", 1), new("A2", "D2", 1)]);
        AssertRejectedUnchanged(state, result);
    }

    [Fact]
    public async Task HigherPriorityTargetMustReceiveLethalBeforeLowerPriority()
    {
        var state = Position(singleAttacker: true, defenderTwoPower: 3, bulwark: true);
        var result = await Assign(state, [new("A1", "D2", 2)]);
        AssertRejectedUnchanged(state, result);
    }

    [Fact]
    public async Task ExcessDamageMayGoToFirstListedTargetAfterOtherTargetsHaveLethal()
    {
        var result = await Assign(Position(defenderOnePower: 1),
            [new("A1", "D1", 2), new("A2", "D1", 1), new("A2", "D2", 1)]);
        Assert.True(result.Accepted, result.ErrorMessage);
    }

    [Fact]
    public async Task AlreadyMarkedDamageReducesSharedLethalRequirement()
    {
        var state = Position(defenderTwoPower: 3);
        state = state with { CardObjects = state.CardObjects.ToDictionary(e => e.Key,
            e => e.Key == "D1" ? e.Value with { Damage = 1 } : e.Value) };
        var result = await Assign(state, [new("A1", "D1", 2), new("A2", "D2", 2)]);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.CardObjects["D1"].Damage);
    }

    [Fact]
    public async Task BothSidesSubmitBeforePooledDamageIsApplied()
    {
        var first = await Assign(Position(), [new("A1", "D1", 2), new("A2", "D1", 1), new("A2", "D2", 1)]);
        Assert.True(first.Accepted, first.ErrorMessage);
        var metadata = first.Prompts["P2"].View!.Metadata!;
        Assert.Equal("SIDE_POOL_LETHAL_PRIORITY", metadata["assignmentRule"]);
        Assert.Equal(4L, metadata["totalAssignableDamage"]);
        var result = await new CoreRuleEngine().ResolveAsync(first.State,
            new("defender-pooled", "P2", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand("battle:BF", "BF", [new("D1", "A1", 2), new("D1", "A2", 1), new("D2", "A2", 1)]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.False(result.State.BattleState.IsActive);
        foreach (var (target, expected) in new[] { ("A1", 2), ("A2", 2), ("D1", 3), ("D2", 1) })
            Assert.Equal(expected, result.Events.Where(e => e.Kind == "DAMAGE_APPLIED"
                && Equals(e.Payload.GetValueOrDefault("targetObjectId"), target))
                .Sum(e => Convert.ToInt32(e.Payload["damage"])));
    }

    [Fact]
    public async Task ExcessCannotReturnToBulwarkAfterLowerPriorityTarget()
    {
        var state = Position(defenderOnePower: 1, bulwark: true);
        AssertRejectedUnchanged(state, await Assign(state, [new("A1", "D1", 2), new("A2", "D1", 1), new("A2", "D2", 1)]));
    }

    [Fact]
    public async Task ExcessCannotBeSplitBetweenTwoTargets()
    {
        var state = Position(defenderOnePower: 1);
        AssertRejectedUnchanged(state, await Assign(state, [new("A1", "D1", 2), new("A2", "D2", 2)]));
    }

    [Fact]
    public async Task RepeatedPayloadCannotOverflowSourceTotal()
    {
        var state = Position(singleAttacker: true);
        AssertRejectedUnchanged(state, await Assign(state,
            [new("A1", "D1", int.MaxValue), new("A1", "D1", int.MaxValue), new("A1", "D1", 4)]));
    }

    [Fact]
    public void ServerPublishesPooledSuggestionWithoutChangingState()
    {
        var state = Position(bulwark: true);
        var before = MatchStateHasher.Hash(state);
        var metadata = ResolutionResult.BuildPrompts(state)["P1"].View!.Metadata!;
        Assert.Equal(4L, metadata["totalAssignableDamage"]);
        var priorities = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(metadata["targetPriority"]);
        Assert.Equal(0, priorities["D1"]);
        Assert.Equal(1, priorities["D2"]);
        var suggested = Assert.IsAssignableFrom<IReadOnlyDictionary<string, long>>(metadata["suggestedDamageByTarget"]);
        Assert.Equal(3L, suggested["D1"]);
        Assert.Equal(1L, suggested["D2"]);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    private static ValueTask<ResolutionResult> Assign(MatchState state, IReadOnlyList<CombatDamageAssignmentDto> assignments)
        => new CoreRuleEngine().ResolveAsync(state, new("pooled-" + Guid.NewGuid().ToString("N"), "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand("battle:BF", "BF", assignments), default);

    private static void AssertRejectedUnchanged(MatchState state, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    internal static MatchState Position(bool singleAttacker = false, int defenderOnePower = 3, int defenderTwoPower = 1, bool bulwark = false)
    {
        var cards = new Dictionary<string, CardObjectState>
        {
            ["BF"] = new("BF", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2"),
            ["A1"] = Unit("A1", "P1", 2), ["D1"] = Unit("D1", "P2", defenderOnePower), ["D2"] = Unit("D2", "P2", defenderTwoPower)
        };
        if (!singleAttacker) cards["A2"] = Unit("A2", "P1", 2);
        if (bulwark) cards["D1"] = cards["D1"] with { Tags = [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark] };
        return new("POOLED-COMBAT", 12, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Battlefields = singleAttacker ? ["A1"] : ["A1", "A2"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["BF", "D1", "D2"] }
            }, cardObjects: cards, objectLocations: cards.ToDictionary(e => e.Key, e => new ObjectLocationState(e.Value.ControllerId!, "BATTLEFIELD", "BF")),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF")]);
    }

    private static CardObjectState Unit(string id, string player, int power) => new(id, power: power, cardNo: "SFD·125/221",
        ownerId: player, controllerId: player, isAttacking: player == "P1", isDefending: player == "P2", tags: [CardObjectTags.UnitCard]);
}
