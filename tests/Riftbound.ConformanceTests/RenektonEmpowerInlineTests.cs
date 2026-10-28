using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RenektonEmpowerInlineTests
{
    private const string Ability = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RecycledRunesDoNotRetroactivelyReduceTheEmpowerPrice()
    {
        // This controlled midgame becomes the journal's opening frame.
        var state = EmpowerTests.Runes(await EmpowerTests.Enter("VEN·001"), 5) with { Tick = 0 };
        state = OfficialHoldSequenceTests.AddLegend(state, "VEN·141");
        var initial = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 0) } };
        var resource = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:red:2";
        var command = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [],
            ["RECYCLE_RUNE:EMPOWER-RUNE-0", "RECYCLE_RUNE:EMPOWER-RUNE-1", resource,
             "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:3"]);
        var direct = await new CoreRuleEngine().ResolveAsync(initial, new("empower-inline", "P1", command.CmdType), command, default);
        Assert.True(direct.Accepted, direct.ErrorMessage);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, Web);
        var empower = Assert.Single(metadata.GetProperty("sourceRequirements").EnumerateArray(),
            r => r.GetProperty("sourceObjectId").GetString() == "HOST" && r.GetProperty("abilityId").GetString() == "EMPOWER:0:0");
        Assert.Equal(5, empower.GetProperty("manaCost").GetInt32());
        Assert.Contains(empower.GetProperty("paymentResourceChoices").EnumerateArray(),
            c => c.GetProperty("id").GetString() == resource);
        var game = new Driver(initial);
        await game.Act("P1", command);
        var activation = Assert.Single(game.State.StackItems).EmpowerActivation!;
        Assert.Equal(5, activation.ControlledRunes);
        Assert.Equal(5, activation.PaidMana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Equal(3, game.State.PlayerZones["P1"].Base.Count(id => id.StartsWith("EMPOWER-RUNE-")));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task MissingPowerCannotExhaustTheLegendOrStartEmpower()
    {
        var state = EmpowerTests.Runes(await EmpowerTests.Enter("VEN·001"), 5) with { Tick = 0 };
        state = OfficialHoldSequenceTests.AddLegend(state, "VEN·141");
        var initial = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(3, 1) } };
        var command = new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [],
            ["LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:2",
             "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:3"]);
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("empower-inline-unfunded", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
    }
}
