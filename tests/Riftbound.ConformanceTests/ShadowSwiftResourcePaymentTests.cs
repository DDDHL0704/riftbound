using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ShadowSwiftResourcePaymentTests
{
    private const string LegendAbility = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
    private const string Resource = "LEGEND_RESOURCE:P2-LEGEND:" + LegendAbility + ":POWER_SPEND:A:2";
    private const string ShadowAbility = P4ActivatedAbilityCatalog.ShadowStunAbilityId;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DefenderCanUseLegendManaAndChosenPowerDuringSwiftSkillPayment()
    {
        var initial = Position(new RunePool(0, 3, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var game = await Focused(initial);
        var command = new ActivateAbilityCommand("SHADOW", ShadowAbility, ["HOST"],
            [Resource, "POWER_SPEND:A:1", "POWER_SOURCE:UNRESTRICTED:1", "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:1"]);
        var direct = await new CoreRuleEngine().ResolveAsync(game.State, new("shadow-inline", "P2", command.CmdType), command, default);
        Assert.True(direct.Accepted, direct.ErrorMessage);
        var requirement = ShadowRequirement(game.State);
        Assert.Contains(requirement.GetProperty("paymentResourceChoices").EnumerateArray(), c => c.GetProperty("id").GetString() == Resource);
        Assert.Contains(requirement.GetProperty("targetChoicesByIndex").GetProperty("0").EnumerateArray(), c => c.GetProperty("id").GetString() == "HOST");
        Assert.Equal("POWER_SPEND:", requirement.GetProperty("powerSpendSelection").GetProperty("choicePrefix").GetString());

        await game.Act("P2", command);
        Assert.True(game.State.CardObjects["P2-LEGEND"].IsExhausted);
        Assert.True(game.State.CardObjects["SHADOW"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P2"].Power);
        Assert.Equal(1, game.State.RunePools["P2"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, game.State.RunePools["P2"].UnitPlayOrUnitAbilityOnlyMana);
        var cost = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("abilityId"), ShadowAbility)).Event.Payload;
        Assert.Equal(1, Convert.ToInt32(cost["unitPlayOrUnitAbilityOnlyManaSpent"]));
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(cost["selectedPowerSources"])["UNRESTRICTED"]);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await game.Replay();

        var commands = game.Commands();
        var index = Array.FindIndex(commands, c => c.CommandType == CommandTypes.ActivateAbility);
        Assert.True(index >= 0);
        var original = commands[index];
        var changed = JsonNode.Parse(original.RawCommand!.Value.GetRawText())!.AsObject();
        changed["optionalCosts"] = JsonSerializer.SerializeToNode(new[] { Resource, "POWER_SPEND:red:1",
            "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:1" }, Web);
        commands[index] = original with { RawCommand = JsonSerializer.SerializeToElement(changed, Web) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, game.State,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
        Assert.NotEmpty(replay.Errors);
    }

    [Fact]
    public async Task DefenderCanChooseColoredInsteadOfWildcardPowerForTheSkill()
    {
        var game = await Focused(Position(new RunePool(1, 1, new Dictionary<string, int> { [RuneTrait.Red] = 1 })));
        var command = new ActivateAbilityCommand("SHADOW", ShadowAbility, ["HOST"], ["POWER_SPEND:red:1"]);
        var result = await new CoreRuleEngine().ResolveAsync(game.State, new("shadow-colored", "P2", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.RunePools["P2"].Power);
        Assert.Equal(0, result.State.RunePools["P2"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public async Task SpellshieldTargetRequiresItsTaxAfterTheLegendCost(int power, bool affordable)
    {
        var game = await Focused(Position(new RunePool(0, power), spellshield: true));
        var prompt = ResolutionResult.BuildPrompts(game.State)["P2"];
        var candidate = (prompt.Candidates ?? []).SingleOrDefault(c => c.Action == CommandTypes.ActivateAbility);
        Assert.Equal(affordable, candidate is not null && candidate.Enabled);
        var command = new ActivateAbilityCommand("SHADOW", ShadowAbility, ["HOST"], [Resource]);
        var before = MatchStateHasher.Hash(game.State);
        var result = await new CoreRuleEngine().ResolveAsync(game.State, new("shadow-tax", "P2", command.CmdType), command, default);
        Assert.Equal(affordable, result.Accepted);
        if (!affordable)
        {
            Assert.Empty(result.Events);
            Assert.Equal(before, MatchStateHasher.Hash(result.State));
            Assert.False(result.State.CardObjects["P2-LEGEND"].IsExhausted);
            Assert.False(result.State.CardObjects["SHADOW"].IsExhausted);
            return;
        }
        var cost = Assert.Single(result.Events, e => e.Kind == "COST_PAID"
            && Equals(e.Payload.GetValueOrDefault("abilityId"), ShadowAbility)).Payload;
        Assert.Equal(1, Convert.ToInt32(cost["spellshieldTaxPower"]));
        Assert.Equal(2, Convert.ToInt32(cost["totalPowerCost"]));
        Assert.Equal(0, result.State.RunePools["P2"].TotalStoredPower);
    }

    private static JsonElement ShadowRequirement(MatchState state)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P2"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        var requirements = JsonSerializer.SerializeToElement(candidate.Metadata, Web).GetProperty("sourceRequirements");
        return Assert.Single(requirements.EnumerateArray(), r => r.GetProperty("abilityId").GetString() == ShadowAbility);
    }

    private static async Task<Driver> Focused(MatchState initial)
    {
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        await game.Act("P1", new PassFocusCommand());
        Assert.Equal("P2", game.State.FocusPlayerId);
        Assert.True(game.State.BattleState.IsActive);
        return game;
    }

    private static MatchState Position(RunePool pool, bool spellshield = false)
    {
        var state = OfficialHoldSequenceTests.AddUnit(AttackDamageDistributionTests.Position("OGN·001/298"),
            "SHADOW", P4ActivatedAbilityCatalog.ShadowCardNo, "F", "P2") with { Tick = 0 };
        var host = state.CardObjects["HOST"];
        if (spellshield) host = host with { Tags = host.Tags.Concat([CardObjectTags.Spellshield]).ToArray() };
        var complete = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = host,
                ["P2-LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-LEGEND", "VEN·141", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { LegendZone = ["P2-LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["P2-LEGEND"] = new("P2", "LEGEND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = pool } };
        ArmamentMightTests.PublicRecovery(complete);
        return complete;
    }
}
