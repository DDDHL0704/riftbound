using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("OGN·040/298", "EQUIPMENT", "red", false, 5)]
    [InlineData("OGN·081/298", "EQUIPMENT", "green", false, 5)]
    [InlineData("OGN·120/298", "EQUIPMENT", "blue", false, 5)]
    [InlineData("OGN·163/298", "EQUIPMENT", "orange", false, 5)]
    [InlineData("OGN·204/298", "EQUIPMENT", "purple", false, 5)]
    [InlineData("OGN·245/298", "EQUIPMENT", "yellow", false, 5)]
    [InlineData("UNL-093/219", "UNIT", "mana", false, 5)]
    [InlineData("UNL-049/219", "EQUIPMENT", "power", false, 5)]
    [InlineData("UNL-049/219", "EQUIPMENT", "level", false, 6)]
    [InlineData("VEN·075", "EQUIPMENT", "mana", false, 5)]
    [InlineData("VEN·075", "EQUIPMENT", "empowered", true, 5)]
    [InlineData("OGS·014/024", "UNIT", "spell-mana", false, 5)]
    [InlineData("OGN·247/298", "LEGEND", "spell-power", false, 5)]
    [InlineData("SFD·189/221", "LEGEND", "equipment-power", false, 5)]
    [InlineData("UNL-197/219", "LEGEND", "duel-mana", false, 5)]
    [InlineData("VEN·141", "LEGEND", "unit-mana", false, 5)]
    public async Task BorrowedResourcesResolveImmediatelyUsingTheirOwnCostsAndRestrictions(
        string donor, string category, string gain, bool empowered, int experience)
    {
        var game = await WithTemplate(donor, category, empower: empowered, experience: experience);
        var rows = ResourceRows(game.State);
        Assert.Equal(gain == "level" ? 2 : 1, rows.Length);
        var id = (string)rows[gain == "level" ? 1 : 0]["abilityId"]!;
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Empty(game.State.StackItems);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Equal(before.TimingState, game.State.TimingState);
        Assert.Equal(before.ActivePlayerId, game.State.ActivePlayerId);
        Assert.Equal(before.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Equal(before.FocusPlayerId, game.State.FocusPlayerId);
        Assert.Equal(before.PassedPriorityPlayerIds, game.State.PassedPriorityPlayerIds);
        var old = before.RunePools["P1"];
        var traits = old.PowerByTrait.ToDictionary();
        traits[gain] = traits.GetValueOrDefault(gain) + 1;
        var expected = gain switch {
            "mana" => old with { Mana = old.Mana + 1 },
            "empowered" => old with { Mana = old.Mana + 2 },
            "power" => old with { Power = old.Power + 1 },
            "level" => old with { Power = old.Power + 1, Mana = old.Mana + 1 },
            "spell-mana" => old with { SpellOnlyMana = old.SpellOnlyMana + 2 },
            "spell-power" => old with { SpellOnlyPower = old.SpellOnlyPower + 1 },
            "equipment-power" => old with { EquipmentOnlyPower = old.EquipmentOnlyPower + 1 },
            "duel-mana" => old with { DuelOnlyMana = old.DuelOnlyMana + 1 },
            "unit-mana" => old with { UnitPlayOrUnitAbilityOnlyMana = old.UnitPlayOrUnitAbilityOnlyMana + 2, Power = old.Power - 2 },
            _ => old with { PowerByTrait = traits } };
        Assert.Equal(expected, game.State.RunePools["P1"]);
        await RejectBorrowedResource(game.State, "HOST", id, []);
        var resolved = game.Events().Last(e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.Equal("HOST", resolved.Event.Payload.GetValueOrDefault("sourceObjectId"));
        Assert.Equal("no-ordinary-stack-item", resolved.Event.Payload.GetValueOrDefault("stackPolicy"));
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-093/219", "UNIT", "mana-overflow")]
    [InlineData("OGN·040/298", "EQUIPMENT", "power-overflow")]
    [InlineData("OGS·014/024", "UNIT", "mana-overflow")]
    [InlineData("VEN·141", "LEGEND", "no-power")]
    [InlineData("UNL-049/219", "EQUIPMENT", "level")]
    [InlineData("UNL-093/219", "UNIT", "target")]
    [InlineData("UNL-093/219", "UNIT", "opponent-priority")]
    public async Task BorrowedResourcesRejectIllegalPaymentsDependenciesAndWindows(string donor, string category, string boundary)
    {
        var game = await WithTemplate(donor, category, experience: 6);
        var rows = ResourceRows(game.State);
        var id = (string)rows[boundary == "level" ? 1 : 0]["abilityId"]!;
        var state = game.State;
        if (boundary.EndsWith("overflow", StringComparison.Ordinal) || boundary == "no-power")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = boundary == "mana-overflow" ? new(0, 0, spellOnlyMana: int.MaxValue)
                    : boundary == "power-overflow" ? new(0, 0, equipmentOnlyPower: int.MaxValue) : RunePool.Empty } };
        if (boundary == "level") state = state with { PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = 5 } };
        if (boundary == "opponent-priority") state = state with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P2" };
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("borrowed-resource-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, boundary == "target" ? ["ALLY"] : []), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        if (boundary.EndsWith("overflow", StringComparison.Ordinal)) Assert.Empty(ResourceRows(state));
    }

    [Fact]
    public async Task BorrowedReactionResourceKeepsThePendingSpellAndPriorityPasses()
    {
        var game = await WithTemplate("UNL-093/219", "UNIT");
        var id = (string)Assert.Single(ResourceRows(game.State))["abilityId"]!;
        await game.Act("P1", new PlayCardCommand("STATUS", "VEN·035", ["HOST"], Mode: "EMPOWER_THEN_REMOVE"));
        var before = game.State;
        Assert.Single(before.StackItems);
        Assert.Equal("P1", before.PriorityPlayerId);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(before.StackItems, game.State.StackItems);
        Assert.Equal(before.TimingState, game.State.TimingState);
        Assert.Equal(before.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.Equal(before.PassedPriorityPlayerIds, game.State.PassedPriorityPlayerIds);
        Assert.Equal(before.RunePools["P1"].Mana + 1, game.State.RunePools["P1"].Mana);
        await game.Top();
        await game.Replay();
    }

    private static IReadOnlyDictionary<string, object?>[] ResourceRows(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(c => c.Enabled && c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST")
                && r.GetValueOrDefault("abilityLabel") is string label && label.Contains("费用资源", StringComparison.Ordinal)).ToArray();

    private static async Task RejectBorrowedResource(MatchState state, string source, string ability, IReadOnlyList<string> targets)
    {
        var hash = MatchStateHasher.Hash(state);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("borrowed-exhausted-resource", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(source, ability, targets), default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedResourcePaysANestedReplacementOnceAndSurvivesColdReplay(bool conversion)
    {
        var initial = ReplacementResourceTests.AddResource(OfficialSettReplacementTests.Position(power: 0), conversion ? "SFD·117/221" : "OGN·040/298", exhausted: true);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BORROWER"] = new("BORROWER", cardNo: "OGN·111/298", power: 3, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, "BORROWER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["BORROWER"] = new("P2", "BASE") } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PassPriorityCommand());
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal(request.Options.Count, request.Options.Select(o => o.Id).Distinct().Count());
        var resource = Assert.Single(request.Options, o => o.Id.StartsWith("RESOURCE:BORROWER:BORROWED:", StringComparison.Ordinal)
            && (!conversion || o.Id.EndsWith(":BORROWED_CONVERSION:1", StringComparison.Ordinal)));
        await game.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [resource.Id]));
        Assert.NotNull(game.State.PendingRuleChoice);
        Assert.True(game.State.CardObjects["BORROWER"].IsExhausted);
        Assert.True(game.State.CardObjects["RESOURCE"].IsExhausted);
        Assert.Equal(1, conversion ? game.State.RunePools["P2"].Power : game.State.RunePools["P2"].PowerByTrait.GetValueOrDefault("red"));
        Assert.Equal(conversion ? 6 : 7, game.State.RunePools["P2"].Mana);
        Assert.Equal(0, game.State.PendingRuleChoice.Origin.RunePools["P2"].TotalPower);
        var next = game.State.PendingRuleChoice.Request;
        Assert.DoesNotContain(next.Options, o => o.Id.StartsWith("RESOURCE:BORROWER:", StringComparison.Ordinal));
        await game.Act("P2", new PayCostCommand(next.Id, "RULE_REPLACEMENT", [conversion ? "SETT:D:LEGEND:ANY" : "SETT:D:LEGEND:TRAIT:red"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("D", game.State.PlayerZones["P2"].Base);
        Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
        Assert.Single(game.Events(), e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        await game.Replay();
    }
}
