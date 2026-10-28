using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RenektonUnitAbilityInlineTests
{
    private const string Ability = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
    private const string Resource = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:2";
    private const string Mana = "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AUnitAbilityCanUseTheLegendResourceDuringItsOwnPayment()
    {
        var initial = Position(new(0, 3));
        var command = new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [],
            [Resource, Mana]);
        var direct = await new CoreRuleEngine().ResolveAsync(initial, new("unit-ability-inline", "P1", command.CmdType), command, default);
        Assert.True(direct.Accepted, direct.ErrorMessage);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var requirements = JsonSerializer.SerializeToElement(candidate.Metadata, Web).GetProperty("sourceRequirements");
        var vi = Assert.Single(requirements.EnumerateArray(), r => r.GetProperty("sourceObjectId").GetString() == "HOST"
            && r.GetProperty("abilityId").GetString() == P4ActivatedAbilityCatalog.ViDoublePowerAbilityId);
        Assert.Contains(vi.GetProperty("paymentResourceChoices").EnumerateArray(),
            c => c.GetProperty("id").GetString() == Resource);

        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "COST_PAID"));
        await game.Top();
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task UnitAndLegendFeesCannotUseTheSamePower()
    {
        var initial = Position(new(0, 2));
        var command = new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [],
            [Resource, Mana]);
        var before = MatchStateHasher.Hash(initial);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("unit-ability-unfunded", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.False(rejected.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task UnitAbilityAndLegendCanUsePowerFromRecycledRunes()
    {
        var initial = Position(RunePool.Empty);
        foreach (var id in new[] { "RED-1", "RED-2", "RED-3" })
            initial = VoluntaryPaymentResourceChoicesTests.WithRune(initial, RuneTrait.Red, id);
        var resource = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:red:2";
        var command = new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [],
            ["RECYCLE_RUNE:RED-1", "RECYCLE_RUNE:RED-2", "RECYCLE_RUNE:RED-3", resource, Mana]);
        var direct = await new CoreRuleEngine().ResolveAsync(initial, new("unit-ability-runes", "P1", command.CmdType), command, default);
        Assert.True(direct.Accepted, direct.ErrorMessage);
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana);
        Assert.Equal(3, game.Events().Count(e => e.Event.Kind == "RUNE_RECYCLED"));
        await game.Replay();
    }

    [Fact]
    public async Task AbilityPaymentCanRecycleAfterTheLegendFreesCapacity()
    {
        var initial = Position(new RunePool(0, 2, unitOrEquipmentPlayOnlyPower: int.MaxValue - 2));
        initial = VoluntaryPaymentResourceChoicesTests.WithRune(initial, RuneTrait.Red, "RED");
        var command = new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [],
            [Resource, "RECYCLE_RUNE:RED", Mana]);
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(int.MaxValue - 2, game.State.RunePools["P1"].UnitOrEquipmentPlayOnlyPower);
        var events = game.Events();
        var legend = Array.FindIndex(events, e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("abilityId"), Ability));
        var recycled = Array.FindIndex(events, e => e.Event.Kind == "RUNE_RECYCLED");
        Assert.True(legend >= 0 && recycled > legend);
        await game.Replay();

        var reversed = command with { OptionalCosts = ["RECYCLE_RUNE:RED", Resource, Mana] };
        var before = MatchStateHasher.Hash(initial);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("ability-rune-before-capacity", "P1", reversed.CmdType), reversed, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task AbilityReceiptReportsTheActualRestrictedAndOrdinaryManaSpent()
    {
        var game = new Driver(Position(new(0, 3)));
        await game.Act("P1", new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [], [Resource]));
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("abilityId"), P4ActivatedAbilityCatalog.ViDoublePowerAbilityId)).Event.Payload;
        Assert.Equal(0, Convert.ToInt32(paid["ordinaryManaSpent"]));
        Assert.Equal(2, Convert.ToInt32(paid["unitPlayOrUnitAbilityOnlyManaSpent"]));
        Assert.Contains(Resource, Assert.IsAssignableFrom<IEnumerable<string>>(paid["paymentResourceActions"]));
        await game.Replay();
    }

    [Fact]
    public async Task ChangedLegendWalletChoiceCannotReplayTheOriginalAbility()
    {
        var initial = Position(new RunePool(0, 2, new Dictionary<string, int> { ["red"] = 1, ["purple"] = 1 }));
        var selected = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:1,red:1";
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [],
            [selected, Mana]));
        var commands = game.Commands();
        var original = Assert.Single(commands);
        var changed = JsonNode.Parse(original.RawCommand!.Value.GetRawText())!.AsObject();
        changed["optionalCosts"] = JsonSerializer.SerializeToNode(new[] {
            "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:1,purple:1", Mana }, Web);
        commands[0] = original with { RawCommand = JsonSerializer.SerializeToElement(changed, Web) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, game.State,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task EquipmentAbilityCannotSpendTheManaButCanBankIt(int ordinaryMana, bool succeeds)
    {
        var state = ArmamentMightTests.Position("UNL-136/219") with { Tick = 0 };
        state = OfficialHoldSequenceTests.AddLegend(state, "VEN·141");
        var initial = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "UNL-136/219", "P1"),
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(ordinaryMana, 2) } };
        var command = new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, [], [Resource]);
        var before = MatchStateHasher.Hash(initial);
        var direct = await new CoreRuleEngine().ResolveAsync(initial, new("equipment-inline-" + ordinaryMana, "P1", command.CmdType), command, default);
        Assert.Equal(succeeds, direct.Accepted);
        if (!succeeds) { Assert.Empty(direct.Events); Assert.Equal(before, MatchStateHasher.Hash(direct.State)); return; }
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        await game.Replay();
    }

    private static MatchState Position(RunePool pool)
    {
        var state = ArmamentMightTests.Position(unit: P4ActivatedAbilityCatalog.ViCardNo, power: 3) with { Tick = 0 };
        state = OfficialHoldSequenceTests.AddLegend(state, "VEN·141");
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", P4ActivatedAbilityCatalog.ViCardNo, "P1"),
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }
}
