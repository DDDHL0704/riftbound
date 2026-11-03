using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private const string BorrowedDestroyPrefix = "DESTROY_FRIENDLY_PERMANENT:";

    [Theory]
    [InlineData("HOST", false)]
    [InlineData("HOST", true)]
    [InlineData("ALLY", false)]
    [InlineData("GEAR", false)]
    [InlineData("DONOR", false)]
    public async Task BorrowedSwiftResourcePaysUnitOrEquipmentWithoutRestoringASacrificedBorrower(string cost, bool token)
    {
        var initial = BorrowedConditionPosition("OGN·113/298");
        if (token)
        {
            var source = initial.CardObjects["HOST"];
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = source with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                    CopyFaceTransitions = [new(0, source.ObjectGeneration, P6TokenFactoryCatalog.ImageTokenCardNo, source.CardNo!)] } } };
        }
        var row = Assert.Single(ResourceRows(initial));
        var ability = (string)row["abilityId"]!;
        Assert.Contains("迅捷", (string)row["abilityLabel"]!);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]);
        Assert.Contains(choices, c => c.Id == BorrowedDestroyPrefix + cost);
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", ability, [], [BorrowedDestroyPrefix + cost]));
        Assert.Equal(initial.RunePools["P1"].Power + 2, game.State.RunePools["P1"].Power);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(initial.TimingState, game.State.TimingState);
        Assert.Equal(initial.FocusPlayerId, game.State.FocusPlayerId);
        if (token) Assert.False(game.State.CardObjects.ContainsKey("HOST"));
        else
        {
            Assert.Contains(cost, game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(initial.CardObjects[cost].ObjectGeneration + 1, game.State.CardObjects[cost].ObjectGeneration);
        }
        if (cost != "HOST") Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        if (cost != "DONOR") Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        var resolved = Assert.Single(game.Events(), e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.Equal(false, resolved.Event.Payload["reactionSpeed"]);
        Assert.Equal(true, resolved.Event.Payload["swiftSpeed"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TARGET_SELECTED");
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedSwiftResourceRejectsWrongCostsAndClosedOrOpponentsWindows()
    {
        var initial = BorrowedConditionPosition("OGN·113/298");
        var ability = (string)Assert.Single(ResourceRows(initial))["abilityId"]!;
        foreach (var costs in new[] { Array.Empty<string>(), ["MISSING"], ["RETURN"], ["HOST", "ALLY"], ["HOST", "HOST"] })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", ability, [], costs.Select(id => BorrowedDestroyPrefix + id).ToArray()));
        await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", ability, ["ALLY"], [BorrowedDestroyPrefix + "ALLY"]));
        await RadGearTestPosition.Reject(initial, "P2", new ActivateAbilityCommand("HOST", ability, [], [BorrowedDestroyPrefix + "ALLY"]));
        var closed = initial with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1" };
        await RadGearTestPosition.Reject(closed, "P1", new ActivateAbilityCommand("HOST", ability, [], [BorrowedDestroyPrefix + "ALLY"]));
        Assert.Empty(ResourceRows(closed));
        var otherTurn = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        await RadGearTestPosition.Reject(otherTurn, "P1", new ActivateAbilityCommand("HOST", ability, [], [BorrowedDestroyPrefix + "ALLY"]));
        Assert.Empty(ResourceRows(otherTurn));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task BorrowedSwiftResourceChecksExactOutputCapacityBeforeChargingItsDestructionCost(int capacity, bool typed)
    {
        var initial = BorrowedConditionPosition("OGN·113/298");
        var ability = (string)Assert.Single(ResourceRows(initial))["abilityId"]!;
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = typed ? new(0, 0, new Dictionary<string, int> { ["red"] = int.MaxValue - capacity }) : new(0, int.MaxValue - capacity) } };
        var command = new ActivateAbilityCommand("HOST", ability, [], [BorrowedDestroyPrefix + "HOST"]);
        if (capacity < 2)
        {
            Assert.Empty(ResourceRows(initial));
            await RadGearTestPosition.Reject(initial, "P1", command);
            return;
        }
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(int.MaxValue, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.RunePools["P1"].HasSupportedAmounts);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedSwiftResourceCanFreeOutputCapacityThroughSharedDestructionReplacements(bool convert)
    {
        var initial = ImposedMovementContinuationTests.AddBase(ImposedMovementContinuationTests.WithPool(
            ImposedMovementContinuationTests.Position("standard"),
            new(convert ? int.MaxValue - 2 : 0, convert ? int.MaxValue : int.MaxValue - 1)), "HOST", "OGN·111/298");
        initial = ImposedMovementContinuationTests.AddBase(initial, "DONOR", "OGN·113/298");
        initial = ImposedMovementContinuationTests.AddBase(initial, "OTHER", "OGN·111/298");
        initial = OfficialHoldSequenceTests.AddLegend(initial, "OGN·269/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["DONOR"] = initial.CardObjects["DONOR"] with { IsExhausted = true },
            ["A"] = initial.CardObjects["A"] with { Tags = [..initial.CardObjects["A"].Tags, CardObjectTags.Boon],
                Power = initial.CardObjects["A"].Power + 1 } } };
        if (convert) initial = ImposedMovementContinuationTests.AddBase(initial, "CONVERTER", P4ActivatedAbilityCatalog.HextechAnomalyCardNo);
        var ability = (string)Assert.Single(ResourceRows(initial), r => ((string)r["abilityLabel"]!).Contains("迅捷", StringComparison.Ordinal))["abilityId"]!;
        var otherAbility = (string)(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Single(r => Equals(r.GetValueOrDefault("sourceObjectId"), "OTHER")
                && ((string)r["abilityLabel"]!).Contains("迅捷", StringComparison.Ordinal))["abilityId"]!;
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", ability, [], [BorrowedDestroyPrefix + "A"]));
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("DESTRUCTION_REPLACEMENT", request.Kind);
        Assert.DoesNotContain(request.Options, o => o.Id.StartsWith("RESOURCE:OTHER:" + otherAbility, StringComparison.Ordinal));
        await RadGearTestPosition.Reject(game.State, "P1", new ActivateAbilityCommand("OTHER", otherAbility, [], [BorrowedDestroyPrefix + "A"]));
        if (convert)
        {
            var resource = Assert.Single(request.Options, o => o.Id.StartsWith("RESOURCE:CONVERTER:", StringComparison.Ordinal)
                && o.Id.EndsWith(P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix + "2", StringComparison.Ordinal));
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [resource.Id]));
            request = game.State.PendingRuleChoice!.Request;
        }
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [convert ? "DECLINE" : "SETT:A:LEGEND:ANY"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(int.MaxValue, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.False(game.State.CardObjects["OTHER"].IsExhausted);
        Assert.Contains("A", convert ? game.State.PlayerZones["P1"].Graveyard : game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedSwiftResourceUsesRealDuelFocusAndResolvesWithoutClosingIt()
    {
        var initial = AttackDamageDistributionTests.Spell(AttackDamageDistributionTests.Position("OGN·111/298"), "DUELREADY", "OGN·146/298", "P1");
        initial = ImposedMovementContinuationTests.AddBase(initial, "DONOR", "OGN·113/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["DONOR"] = initial.CardObjects["DONOR"] with { IsExhausted = true } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await game.Act("P1", new PlayCardCommand("DUELREADY", "OGN·146/298", ["HOST"]));
        await game.Top();
        if (game.State.FocusPlayerId == "P2") await game.Act("P2", new PassFocusCommand());
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        var before = game.State;
        var ability = (string)Assert.Single(ResourceRows(before))["abilityId"]!;
        await game.Act("P1", new ActivateAbilityCommand("HOST", ability, [], [BorrowedDestroyPrefix + "DONOR"]));
        Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
        Assert.Equal(before.FocusPlayerId, game.State.FocusPlayerId);
        Assert.Equal(before.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Equal(before.RunePools["P1"].Power + 2, game.State.RunePools["P1"].Power);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
