using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedPermanentPowerNonPlayBoundaryTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AUnitAbilityCannotSpendPowerRestrictedToPlayingUnitOrEquipment()
    {
        var state = ArmamentMightTests.Position(unit: P4ActivatedAbilityCatalog.ViCardNo, power: 3) with { Tick = 0 };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", P4ActivatedAbilityCatalog.ViCardNo, "P1") } };
        state = WithPool(state, 2); var game = new Driver(state); var before = MatchStateHasher.Hash(state);
        var command = new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, []);
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(state)["P1"].PromptId; raw["snapshotTick"] = state.Tick;
        await game.ActRetained(new("P1", "restricted-unit-ability", JsonSerializer.SerializeToElement(raw, Web)), false);
        Assert.Equal(before, MatchStateHasher.Hash(game.State)); Assert.Equal(1, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Empty(game.Events()); await game.Replay();
    }

    [Fact]
    public async Task AnInstructionCannotSpendPowerRestrictedToPlayingUnitOrEquipment()
    {
        var initial = WithPool(InstructionPowerPaymentTimingTests.Position(), 1); var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"]));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", choice.Request.Kind);
        Assert.DoesNotContain(choice.Request.Options, option => option.Id == "SPEND_POWER:1");
        Assert.Equal(1, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·063/298", "SPELL_ONLY")]
    [InlineData("SFD·143/221", "EQUIPMENT_ONLY")]
    public async Task APresentButIneligibleSourceCannotBeSelectedForTheCard(string cardNo, string selected)
    {
        var initial = RestrictedPowerPlayTests.Position(cardNo, false);
        var pool = new RunePool(10, 1, spellOnlyPower: 1, equipmentOnlyPower: 1, unitOrEquipmentPlayOnlyPower: 1);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } };
        var game = new Driver(initial); var before = MatchStateHasher.Hash(initial);
        var command = new PlayCardCommand("CARD", cardNo, cardNo == "SFD·143/221" ? [] : ["HOST"],
            OptionalCosts: ["POWER_SPEND:A:1", "POWER_SOURCE:" + selected + ":1"],
            Destination: cardNo == "SFD·143/221" ? "BASE" : "");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.False(quote.CanPay);
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(initial)["P1"].PromptId; raw["snapshotTick"] = initial.Tick;
        await game.ActRetained(new("P1", "ineligible-power-source", JsonSerializer.SerializeToElement(raw, Web)), false);
        Assert.Equal(before, MatchStateHasher.Hash(game.State)); Assert.Equal(4, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Empty(game.Events()); await game.Replay();
    }

    private static MatchState WithPool(MatchState state, int mana)
    {
        var pool = JsonSerializer.Deserialize<RunePool>(
            $"{{\"mana\":{mana},\"power\":0,\"unitOrEquipmentPlayOnlyPower\":1}}", Web)!;
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }
}
