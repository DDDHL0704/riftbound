using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RenektonRenataInlinePaymentTests
{
    private const string LegendAbility = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
    private const string LegendResource = "LEGEND_RESOURCE:LEGEND:" + LegendAbility + ":POWER_SPEND:A:2";
    private const string UnitMana = "UNIT_PLAY_OR_UNIT_ABILITY_ONLY";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachRenataSkillCanPayWithLegendManaDuringItsOwnPayment(bool score)
    {
        var ability = score ? P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId : P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId;
        var initial = Position(new RunePool(score ? 2 : 0, 2,
            new Dictionary<string, int> { [RuneTrait.Blue] = score ? 4 : 1 }));
        var manaSelection = score ? $"MANA_SOURCE:{UnitMana}:2,UNRESTRICTED:2" : $"MANA_SOURCE:{UnitMana}:1";
        var command = new ActivateAbilityCommand("P1-RENATA", ability, [], [LegendResource, manaSelection]);
        var direct = await new CoreRuleEngine().ResolveAsync(initial, new("renata-inline", "P1", command.CmdType), command, default);
        Assert.True(direct.Accepted, direct.ErrorMessage);

        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var requirements = JsonSerializer.SerializeToElement(candidate.Metadata, Web).GetProperty("sourceRequirements");
        var requirement = Assert.Single(requirements.EnumerateArray(), r => r.GetProperty("sourceObjectId").GetString() == "P1-RENATA"
            && r.GetProperty("abilityId").GetString() == ability);
        Assert.Contains(requirement.GetProperty("paymentResourceChoices").EnumerateArray(),
            c => c.GetProperty("id").GetString() == LegendResource);

        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(score, game.State.CardObjects["P1-RENATA"].IsExhausted);
        Assert.Equal(score ? 0 : 1, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("abilityId"), ability)).Event.Payload;
        Assert.Equal(score ? 2 : 0, Convert.ToInt32(paid["ordinaryManaSpent"]));
        Assert.Equal(score ? 2 : 1, Convert.ToInt32(paid["unitPlayOrUnitAbilityOnlyManaSpent"]));
        Assert.Equal([LegendResource], Assert.IsAssignableFrom<IEnumerable<string>>(paid["paymentResourceActions"]).ToArray());
        await game.Top();
        if (score) Assert.Equal(1, game.State.PlayerScores["P1"]);
        else Assert.Contains("P1-DRAW-001", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task LegendAndRenataCannotSpendTheSameBluePower()
    {
        var initial = Position(new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }));
        var resource = "LEGEND_RESOURCE:LEGEND:" + LegendAbility + ":POWER_SPEND:blue:2";
        var command = new ActivateAbilityCommand("P1-RENATA", P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, [], [resource]);
        var before = MatchStateHasher.Hash(initial);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("renata-same-power", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.False(rejected.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("other-controller")]
    [InlineData("wrong-legend")]
    public async Task InlinePaymentRequiresThePlayersReadyRenekton(string defect)
    {
        var initial = Position(new RunePool(0, 2, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }));
        var legend = initial.CardObjects["LEGEND"];
        legend = defect switch {
            "exhausted" => legend with { IsExhausted = true },
            "other-controller" => legend with { ControllerId = "P2" },
            _ => legend with { CardNo = "UNL-193/219" }
        };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["LEGEND"] = legend } };
        var command = new ActivateAbilityCommand("P1-RENATA", P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, [], [LegendResource]);
        var before = MatchStateHasher.Hash(initial);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("renata-invalid-" + defect, "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task RenataCanRecycleAfterLegendFreesCapacityButNotBefore()
    {
        var initial = Position(new RunePool(0, 2, unitOrEquipmentPlayOnlyPower: int.MaxValue - 2));
        initial = VoluntaryPaymentResourceChoicesTests.WithRune(initial, RuneTrait.Blue, "BLUE");
        var command = new ActivateAbilityCommand("P1-RENATA", P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, [],
            [LegendResource, "RECYCLE_RUNE:BLUE"]);
        var game = new Driver(initial);
        await game.Act("P1", command);
        var events = game.Events();
        var legend = Array.FindIndex(events, e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("abilityId"), LegendAbility));
        var recycled = Array.FindIndex(events, e => e.Event.Kind == "RUNE_RECYCLED");
        Assert.True(legend >= 0 && recycled > legend);
        Assert.Equal(int.MaxValue - 2, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        await game.Replay();

        var reversed = command with { OptionalCosts = ["RECYCLE_RUNE:BLUE", LegendResource] };
        var before = MatchStateHasher.Hash(initial);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("renata-reversed", "P1", reversed.CmdType), reversed, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task ChangingTheLegendPaymentInRecordedRenataCommandDoesNotReplay()
    {
        var initial = Position(new RunePool(0, 2, new Dictionary<string, int> { [RuneTrait.Blue] = 1, [RuneTrait.Red] = 1 }));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("P1-RENATA", P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, [], [LegendResource]));
        var commands = game.Commands();
        var original = Assert.Single(commands);
        var changed = JsonNode.Parse(original.RawCommand!.Value.GetRawText())!.AsObject();
        changed["optionalCosts"] = JsonSerializer.SerializeToNode(new[] {
            "LEGEND_RESOURCE:LEGEND:" + LegendAbility + ":POWER_SPEND:A:1,red:1" }, Web);
        commands[0] = original with { RawCommand = JsonSerializer.SerializeToElement(changed, Web) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, game.State,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
        Assert.NotEmpty(replay.Errors);
    }

    private static MatchState Position(RunePool pool)
    {
        var state = OfficialHoldSequenceTests.AddLegend(RenataActivatedAbilityTests.AffordableBlueDrawPaymentState(), "VEN·141");
        return state with {
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }
}
