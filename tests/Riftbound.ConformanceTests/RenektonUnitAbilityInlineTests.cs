using System.Text.Json;
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
