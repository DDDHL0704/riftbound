using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// CN 135.2.e.5.a/b, 204.4.b.1, 430.2-3 and 445.2.c.
public sealed class SharedSwiftAbilityPaymentTests
{
    private const string LegendAbility = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
    private const string Legend = "LEGEND_RESOURCE:P2-LEGEND:" + LegendAbility + ":POWER_SPEND:A:2";
    private const string Skill = P4ActivatedAbilityCatalog.ShadowStunAbilityId;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DefenderPaysTheSwiftFeeWithLegendResourcesAndAnIndependentPowerSelection()
    {
        var initial = Position(new(0, 3, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var game = await Focused(initial);
        var requirement = Requirement(game.State, "P2", Skill);
        Assert.Equal(2, requirement.GetProperty("availableManaWithPaymentResources").GetInt32());
        Assert.Equal("POWER_SPEND:", requirement.GetProperty("powerSpendSelection").GetProperty("choicePrefix").GetString());
        Assert.Contains(requirement.GetProperty("paymentResourceChoices").EnumerateArray(), c => c.GetProperty("id").GetString() == Legend);
        Assert.Contains(requirement.GetProperty("targetChoicesByIndex").GetProperty("0").EnumerateArray(), c => c.GetProperty("id").GetString() == "ALLY");

        await game.Act("P2", Command([Legend, "POWER_SPEND:A:1", "POWER_SOURCE:UNRESTRICTED:1",
            "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:1"]));
        Assert.True(game.State.CardObjects["P2-LEGEND"].IsExhausted);
        Assert.True(game.State.CardObjects["SHADOW"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P2"].Power);
        Assert.Equal(1, game.State.RunePools["P2"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, game.State.RunePools["P2"].UnitPlayOrUnitAbilityOnlyMana);
        var paid = Paid(game);
        Assert.Equal(1, Convert.ToInt32(paid["unitPlayOrUnitAbilityOnlyManaSpent"]));
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(paid["selectedPowerSources"])["UNRESTRICTED"]);
        Assert.Equal([Legend], Assert.IsAssignableFrom<IEnumerable<string>>(paid["paymentResourceActions"]));
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();

        foreach (var defect in new[] { "skill", "legend", "source" })
        {
            var commands = game.Commands();
            var index = Array.FindIndex(commands, c => c.CommandType == CommandTypes.ActivateAbility);
            var recorded = commands[index];
            var raw = JsonNode.Parse(recorded.RawCommand!.Value.GetRawText())!.AsObject();
            var costs = defect switch {
                "skill" => new[] { Legend, "POWER_SPEND:red:1", "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:1" },
                "legend" => [Legend.Replace("POWER_SPEND:A:2", "POWER_SPEND:A:1,red:1", StringComparison.Ordinal), "POWER_SPEND:A:1"],
                _ => [Legend, "POWER_SPEND:A:1", "POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1"] };
            raw["optionalCosts"] = JsonSerializer.SerializeToNode(costs, Web);
            commands[index] = recorded with { RawCommand = JsonSerializer.SerializeToElement(raw, Web) };
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, game.State,
                new CoreRuleEngine(), default, game.Events());
            Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheSkillMaySpendEitherEligibleColorWithoutChangingItsFee(bool wildcard)
    {
        var game = await Focused(Position(new(1, 1, new Dictionary<string, int> { [RuneTrait.Red] = 1 })));
        await game.Act("P2", Command([wildcard ? "POWER_SPEND:A:1" : "POWER_SPEND:red:1"]));
        Assert.Equal(wildcard ? 0 : 1, game.State.RunePools["P2"].Power);
        Assert.Equal(wildcard ? 1 : 0, game.State.RunePools["P2"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Equal(1, Convert.ToInt32(Paid(game)["totalPowerCost"]));
        Assert.Equal(0, Convert.ToInt32(Paid(game)["spellshieldTaxPower"]));
        await game.Top();
    }

    [Theory]
    [InlineData(3, false)] [InlineData(4, true)]
    public async Task PromptAndCommandIncludeTheWardTaxAfterPayingTheLegend(int power, bool affordable)
    {
        var game = await Focused(Position(new(0, power), ward: true));
        var candidate = (ResolutionResult.BuildPrompts(game.State)["P2"].Candidates ?? [])
            .SingleOrDefault(c => c.Action == CommandTypes.ActivateAbility);
        Assert.Equal(affordable, candidate is { Enabled: true });
        if (!affordable) { await Reject(game.State, "P2", Command([Legend])); return; }
        await game.Act("P2", Command([Legend, "POWER_SPEND:A:2"]));
        Assert.Equal(2, Convert.ToInt32(Paid(game)["totalPowerCost"]));
        Assert.Equal(1, Convert.ToInt32(Paid(game)["spellshieldTaxPower"]));
        Assert.Equal(0, game.State.RunePools["P2"].TotalStoredPower);
        await game.Top();
    }

    [Theory]
    [InlineData("duplicate-legend")] [InlineData("duplicate-power")] [InlineData("duplicate-source")]
    [InlineData("wrong-total")] [InlineData("play-only-source")] [InlineData("forged-legend")]
    [InlineData("unfunded")] [InlineData("wrong-target")]
    public async Task InvalidSharedPaymentsAreAtomic(string defect)
    {
        var game = await Focused(Position(new(0, defect == "unfunded" ? 2 : 3, unitOrEquipmentPlayOnlyPower: 1)));
        var costs = defect switch {
            "duplicate-legend" => new[] { Legend, Legend },
            "duplicate-power" => [Legend, "POWER_SPEND:A:1", "POWER_SPEND:A:1"],
            "duplicate-source" => [Legend, "POWER_SPEND:A:1", "POWER_SOURCE:UNRESTRICTED:1", "POWER_SOURCE:UNRESTRICTED:1"],
            "wrong-total" => [Legend, "POWER_SPEND:A:2"],
            "play-only-source" => [Legend, "POWER_SPEND:A:1", "POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1"],
            "forged-legend" => [Legend.Replace("P2-LEGEND", "SHADOW", StringComparison.Ordinal)],
            _ => [Legend] };
        var command = Command(costs);
        if (defect == "wrong-target") command = command with { TargetObjectIds = ["SHADOW"] };
        await Reject(game.State, "P2", command);
        Assert.False(game.State.CardObjects["P2-LEGEND"].IsExhausted);
        Assert.False(game.State.CardObjects["SHADOW"].IsExhausted);
    }

    [Fact]
    public async Task ResourcesDoNotOpenASwiftWindowOrTransferTheOpponentsFocus()
    {
        var initial = Position(new(0, 3));
        await Reject(initial, "P2", Command([Legend]));
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal("P1", game.State.FocusPlayerId);
        await Reject(game.State, "P2", Command([Legend]));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", Command([Legend]));
        await game.Top();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuneActionsRetainTheirOrderAroundTheLegendAndStorageCapacity(bool full)
    {
        var initial = Position(new(0, 2, unitOrEquipmentPlayOnlyPower: full ? int.MaxValue - 2 : 0));
        initial = Put(initial, "PAY-RUNE", "OGN·007/298", "P2", "BASE");
        var game = await Focused(initial);
        var resources = full ? new[] { Legend, "RECYCLE_RUNE:PAY-RUNE" }
            : ["TAP_RUNE:PAY-RUNE", "RECYCLE_RUNE:PAY-RUNE", Legend];
        var invalid = full ? new[] { "RECYCLE_RUNE:PAY-RUNE", Legend }
            : ["RECYCLE_RUNE:PAY-RUNE", "TAP_RUNE:PAY-RUNE", Legend];
        await Reject(game.State, "P2", Command(invalid));
        await game.Act("P2", Command(resources));
        Assert.Equal(full ? int.MaxValue - 2 : 0, game.State.RunePools["P2"].UnitOrEquipmentPlayOnlyPower);
        Assert.Contains("PAY-RUNE", game.State.PlayerZones["P2"].RuneDeck);
        var events = game.Events();
        var recycled = Array.FindIndex(events, e => e.Event.Kind == "RUNE_RECYCLED");
        var legend = Array.FindIndex(events, e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("abilityId"), LegendAbility));
        Assert.True(full ? legend < recycled : recycled < legend);
        await game.Top();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeSharedConsumersUseTheSameExplicitPowerSelector(bool equipment)
    {
        var state = Create("OGN·297/298");
        var ability = equipment ? P4ActivatedAbilityCatalog.NextSpellEchoAbilityId : P4ActivatedAbilityCatalog.ViDoublePowerAbilityId;
        state = Put(state, "NATIVE", equipment ? "SFD·078/221" : P4ActivatedAbilityCatalog.ViCardNo, "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(2, 1, new Dictionary<string, int> { [RuneTrait.Red] = 1 }) } };
        var game = new Driver(state);
        Assert.True(Requirement(state, "P1", ability).TryGetProperty("powerSpendSelection", out _));
        await game.Act("P1", new ActivateAbilityCommand("NATIVE", ability, [], ["POWER_SPEND:A:1", "POWER_SOURCE:UNRESTRICTED:1"]));
        Assert.Equal(0, game.State.RunePools["P1"].Power);
        Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        await game.Top();
    }

    [Fact]
    public async Task EnoughOrdinaryManaStillOffersALegendWalletThatFreesRecycleCapacity()
    {
        var state = OfficialHoldSequenceTests.AddLegend(RenataActivatedAbilityTests.AffordableBlueDrawPaymentState(), "VEN·141");
        state = VoluntaryPaymentResourceChoicesTests.WithRune(state, RuneTrait.Blue, "BLUE");
        state = state with { Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1"),
                ["P1-MAIN"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-MAIN", "OGN·297/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["P1-MAIN", "P1-RENATA"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["P1-MAIN"] = new("P1", "BATTLEFIELD", "P1-MAIN") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Red] = 2 }, unitOrEquipmentPlayOnlyPower: int.MaxValue - 2) } };
        var resource = "LEGEND_RESOURCE:LEGEND:" + LegendAbility + ":POWER_SPEND:red:2";
        var ability = P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId;
        var requirement = Requirement(state, "P1", ability);
        Assert.Contains(requirement.GetProperty("paymentResourceChoices").EnumerateArray(), c => c.GetProperty("id").GetString() == resource);
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("P1-RENATA", ability, [],
            [resource, "RECYCLE_RUNE:BLUE", "POWER_SPEND:blue:1", "MANA_SOURCE:UNRESTRICTED:1"]));
        Assert.Equal(2, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(int.MaxValue - 2, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        await game.Top();
    }

    [Fact]
    public async Task AnEquipmentDiscountToZeroDoesNotCreateAResourcePaymentWindow()
    {
        var state = Put(Create("VEN·161"), "BLOSSOM", "UNL-136/219", "P1", "BASE");
        state = OfficialHoldSequenceTests.AddLegend(state, "VEN·141");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 2) } };
        var ability = P4ActivatedAbilityCatalog.ScryingBlossomAbilityId;
        var requirement = Requirement(state, "P1", ability);
        Assert.Equal(0, requirement.GetProperty("manaCost").GetInt32());
        Assert.Equal(0, requirement.GetProperty("availableManaWithPaymentResources").GetInt32());
        Assert.Empty(requirement.GetProperty("paymentResourceChoices").EnumerateArray());
        await Reject(state, "P1", new ActivateAbilityCommand("BLOSSOM", ability, [],
            ["LEGEND_RESOURCE:LEGEND:" + LegendAbility + ":POWER_SPEND:A:2"]));
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("BLOSSOM", ability, []));
        Assert.Contains("BLOSSOM", game.State.PlayerZones["P1"].Graveyard);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(2, game.State.RunePools["P1"].Power);
        await game.Replay();
    }

    private static ActivateAbilityCommand Command(IReadOnlyList<string> costs) => new("SHADOW", Skill, ["ALLY"], costs);
    private static IReadOnlyDictionary<string, object?> Paid(Driver game)
        => Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("abilityId"), Skill)).Event.Payload;
    private static JsonElement Requirement(MatchState state, string player, string ability)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)[player].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        return Assert.Single(JsonSerializer.SerializeToElement(candidate.Metadata, Web).GetProperty("sourceRequirements").EnumerateArray(),
            r => r.GetProperty("abilityId").GetString() == ability);
    }
    private static async Task<Driver> Focused(MatchState state)
    {
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand());
        Assert.Equal("P2", game.State.FocusPlayerId);
        Assert.True(game.State.BattleState.IsActive);
        return game;
    }
    private static MatchState Position(RunePool pool, bool ward = false)
    {
        var state = Put(Create("OGN·297/298"), "OTHER", "OGN·297/298", "P2", "BATTLEFIELD", "OTHER");
        state = Put(state, "SHADOW", P4ActivatedAbilityCatalog.ShadowCardNo, "P2", "BATTLEFIELD", "OTHER");
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = ward ? state.CardObjects["ALLY"] with { Tags = [..state.CardObjects["ALLY"].Tags, CardObjectTags.Spellshield] } : state.CardObjects["ALLY"],
                ["P2-LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-LEGEND", "VEN·141", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { LegendZone = ["P2-LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["P2-LEGEND"] = new("P2", "LEGEND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = pool }
        };
    }
}
