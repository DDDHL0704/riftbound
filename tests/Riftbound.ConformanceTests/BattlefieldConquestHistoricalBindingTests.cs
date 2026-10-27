using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldConquestHistoricalBindingTests
{
    [Fact]
    public async Task DefeatedUnitsRecycledByTheirEarnedDrawsKeepTheBattleIncarnationInConquestHistory()
    {
        // Controlled core-state isolation of the already reproduced legal
        // opening. No resource spending, deck validation or session is claimed.
        var initial = ArmamentMightTests.Position(unit: "OGN·041/298", power: 9);
        foreach (var id in new[] { "ENEMY1", "ENEMY2" })
            initial = OfficialHoldSequenceTests.AddUnit(initial, id, "OGN·096/298", "F", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" },
            ["ENEMY1"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY1", "OGN·096/298", "P2"),
            ["ENEMY2"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY2", "OGN·096/298", "P2") },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(0, 0) } };
        var originalGeneration = initial.CardObjects["ENEMY1"].ObjectGeneration;
        var attack = await AttackDamageDistributionTests.Attack(initial);
        var confirmed = await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1", "ENEMY2");
        var allocation = await AttackDamageDistributionTests.Top(confirmed.State);
        var result = await Act(allocation.State, "P1", new AssignEffectDamageCommand(
            allocation.State.PendingCardChoice!.ResolvingStackItemId!, [
                new CombatDamageAssignmentDto("HOST", "ENEMY1", 3), new CombatDamageAssignmentDto("HOST", "ENEMY2", 2)]));
        if (result.State.TriggerQueue.Count > 0)
            result = await Act(result.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; result.State.StackItems.Count > 0 || result.State.FocusPlayerId is not null; i++)
        { Assert.InRange(i, 0, 7); result = await AttackDamageDistributionTests.Top(result.State); }
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN_CANONICAL_EVIDENCE") is { Length: > 0 } folder)
        {
            var path = Path.Combine(folder, "core-isolation"); Directory.CreateDirectory(path);
            await File.WriteAllTextAsync(Path.Combine(path, "conquest-burnout-binding.json"), JsonSerializer.Serialize(new {
                scope = "Controlled direct core-state isolation, separate from canonical accepted session histories.", initial, result },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.Contains("ENEMY1", result.State.PlayerZones["P2"].Hand); Assert.Contains("ENEMY2", result.State.PlayerZones["P2"].Hand);
        Assert.True(result.State.CardObjects["ENEMY1"].ObjectGeneration > originalGeneration);
        var history = Assert.Single(result.State.BattlefieldResolutions, h => h.Kind == "CONQUERED" && h.BattlefieldObjectId == "F");
        var reference = Assert.Single(history.PublicObjectBindings!, b => b.ObjectId == "ENEMY1");
        Assert.Equal(originalGeneration, reference.Generation);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }
}
