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
        var choice = Assert.IsType<PendingRuleChoiceState>(game.State.PendingRuleChoice);
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", choice.Request.Kind);
        Assert.DoesNotContain(choice.Request.Options, option => option.Id == "SPEND_POWER:1");
        Assert.Equal(1, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
        await game.Replay();
    }

    private static MatchState WithPool(MatchState state, int mana)
    {
        var pool = JsonSerializer.Deserialize<RunePool>(
            $"{{\"mana\":{mana},\"power\":0,\"unitOrEquipmentPlayOnlyPower\":1}}", Web)!;
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }
}
