using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedPowerSourceCompositionTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AUnitHastePaymentCanUseTwoChosenWildcardSourcesWithoutTouchingEquipmentOnlyPower()
    {
        var state = RestrictedPowerPlayTests.Position("SFD·143/221", false);
        var pool = JsonSerializer.Deserialize<RunePool>(
            "{\"mana\":10,\"power\":1,\"equipmentOnlyPower\":1,\"unitOrEquipmentPlayOnlyPower\":1}", Web)!;
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
        var game = new Driver(state);
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [HasteOptionalCostNames.HasteReady,
            "POWER_SPEND:A:2", "POWER_SOURCE:UNIT_OR_EQUIPMENT_PLAY_ONLY:1,UNRESTRICTED:1"], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.AvailableRainbowPower);
        Assert.DoesNotContain("EQUIPMENT_ONLY", quote.Cost.AvailableWildcardSources!.Keys);
        Assert.Empty(quote.Cost.RemainingWildcardSources!);
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P1"].PromptId; raw["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new("P1", "unit-two-selected-sources", JsonSerializer.SerializeToElement(raw, Web)), true);
        var remaining = game.State.RunePools["P1"];
        Assert.Equal(0, remaining.Power); Assert.Equal(1, remaining.EquipmentOnlyPower);
        Assert.Equal(1, remaining.TotalStoredPower);
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(1, paid.Payload["unitOrEquipmentPlayOnlyPowerSpent"]);
        Assert.Equal(0, paid.Payload["remainingUnitOrEquipmentPlayOnlyPower"]);
        var eventSources = JsonSerializer.SerializeToElement(paid.Payload["selectedPowerSources"], Web);
        Assert.Equal(1, eventSources.GetProperty("UNRESTRICTED").GetInt32());
        Assert.Equal(1, eventSources.GetProperty("UNIT_OR_EQUIPMENT_PLAY_ONLY").GetInt32());
        await game.Replay();
    }
}
