using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class UnitManaSelectionSecurityTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ManaAndPowerSourceSelectionsComposeAndTheOriginalHistoryRejectsTampering()
    {
        var initial = RestrictedPowerPlayTests.Position("SFD·143/221", false);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(4, 0, unitOrEquipmentPlayOnlyPower: 1, unitPlayOrUnitAbilityOnlyMana: 4) } };
        var mana = "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:2";
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts:
            [mana, "POWER_SPEND:A:1", "POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1"], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.RemainingOrdinaryMana);
        Assert.Equal(2, quote.Cost.RemainingUnitPlayOrUnitAbilityOnlyMana);
        var game = new Driver(initial);
        await game.Act("P1", command);
        var pool = game.State.RunePools["P1"];
        Assert.Equal(2, pool.Mana); Assert.Equal(2, pool.UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(0, pool.UnitOrEquipmentPlayOnlyPower);
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event.Payload;
        var sources = JsonSerializer.SerializeToElement(paid["selectedManaSources"], Web);
        Assert.Equal(2, sources.GetProperty("UNRESTRICTED").GetInt32());
        Assert.Equal(2, sources.GetProperty("UNIT_PLAY_OR_UNIT_ABILITY_ONLY").GetInt32());
        var power = JsonSerializer.SerializeToElement(paid["selectedPowerSources"], Web);
        Assert.Equal(1, power.GetProperty("UNIT_OR_EQUIPMENT_PLAY_ONLY").GetInt32());
        await game.Replay();

        var commands = game.Commands();
        var original = Assert.Single(commands);
        var changed = JsonNode.Parse(original.RawCommand!.Value.GetRawText())!.AsObject();
        changed["optionalCosts"] = JsonSerializer.SerializeToNode(new[] {
            "MANA_SOURCE:UNRESTRICTED:4", "POWER_SPEND:A:1", "POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1" }, Web);
        commands[0] = original with { RawCommand = JsonSerializer.SerializeToElement(changed, Web) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, game.State,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch);
        Assert.NotEmpty(replay.Errors);
    }

    [Fact]
    public async Task DerivedAbilitySelectionCannotOverrideTheRawCommand()
    {
        var state = ArmamentMightTests.Position(unit: P4ActivatedAbilityCatalog.ViCardNo, power: 3) with { Tick = 0 };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", P4ActivatedAbilityCatalog.ViCardNo, "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(2, 1, unitPlayOrUnitAbilityOnlyMana: 2) } };
        var command = new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [],
            ["MANA_SOURCE:UNRESTRICTED:2"]) {
                ServerManaSourceSelection = new Dictionary<string, int> { ["UNIT_PLAY_OR_UNIT_ABILITY_ONLY"] = 2 } };
        var wire = JsonSerializer.SerializeToElement(command, Web);
        Assert.False(wire.TryGetProperty("serverManaSourceSelection", out _));
        var mapped = Assert.IsType<ActivateAbilityCommand>(GameCommandJsonMapper.Map(wire));
        Assert.Null(mapped.ServerManaSourceSelection);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forged-derived-mana", "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(2, result.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
    }

    [Fact]
    public async Task MultipleManaSelectorsAreRejectedWithoutMutation()
    {
        var state = RestrictedPowerPlayTests.Position("SFD·143/221", false);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 1, unitPlayOrUnitAbilityOnlyMana: 4) } };
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts:
            ["MANA_SOURCE:UNRESTRICTED:4", "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:4"], Destination: "BASE");
        var before = MatchStateHasher.Hash(state);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)).CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("duplicate-mana-selection", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
