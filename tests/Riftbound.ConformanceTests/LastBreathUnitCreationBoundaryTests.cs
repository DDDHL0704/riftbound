using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathUnitCreationBoundaryTests
{
    private const string No = "VEN·128";
    private const string Kill = "OGN·229/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CombatLethalDamageUsesThePreRemovalEmpowerFlagAndKeepsTheBaseDestination(bool empowered)
    {
        // Explicit face-up battle fixture; actual battle damage, not a direct death-state edit.
        var initial = Field(Seed(empowered), "UNIT", "P1");
        initial = Field(initial, "ENEMY", "P2") with { UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF")] };
        var game = new Driver(initial);
        try
        {
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new DeclareBattleCommand("BF", ["UNIT"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
            for (var step = 0; !game.State.PlayerZones["P1"].Graveyard.Contains("UNIT"); step++)
            {
                Assert.InRange(step, 0, 12);
                var damagePrompt = ResolutionResult.BuildPrompts(game.State).Values.FirstOrDefault(p => p.Actionable && p.Actions.Contains(CommandTypes.AssignCombatDamage));
                if (damagePrompt is not null)
                {
                    var assignment = damagePrompt.PlayerId == "P1" ? new CombatDamageAssignmentDto("UNIT", "ENEMY", empowered ? 3 : 2)
                        : new CombatDamageAssignmentDto("ENEMY", "UNIT", 4);
                    await game.Act(damagePrompt.PlayerId, new AssignCombatDamageCommand(game.State.BattleState.BattleId!, "BF", [assignment]));
                }
                else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
                else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
                else Assert.Fail("Battle damage must expose its next ordinary continuation.");
            }
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var earned = Creations(game.State); Assert.Equal(empowered ? 1 : 0, earned.Length);
            foreach (var receipt in earned) { Assert.True(receipt.SourceWasEmpowered); Assert.Equal(generation, receipt.Source.Generation); }
            await FinishCreations(game); Assert.Equal(empowered ? 2 : 0, Minions(game.State).Length);
            Assert.All(Minions(game.State), c => Assert.Contains(c.ObjectId, game.State.PlayerZones["P1"].Base));
            await game.Replay();
        }
        finally { await Save("combat-pre-removal-empowered-" + empowered, initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualSameGenerationNo35StatusBeforeDestructionControlsBirthAndItsOldInverseCannotTouchTheMinions(bool remove)
    {
        var initial = Add(Seed(remove) with { TurnPlayerId = "P2", ActivePlayerId = "P2" }, "STATUS", "VEN·035", "P1", "HAND");
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P2"); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"]));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await Tap(game, "P1"); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-0"));
            var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new PlayCardCommand("STATUS", "VEN·035", ["UNIT"], Mode: remove ? "REMOVE_THEN_EMPOWER" : "EMPOWER_THEN_REMOVE"));
            Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.Equal(!remove, game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(remove ? 0 : 1, Creations(game.State).Length); await FinishCreations(game);
            Assert.Equal(remove ? 0 : 2, Minions(game.State).Length);
            await game.Act("P2", new EndTurnCommand()); await FinishEnd(game, "P2");
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(remove ? 0 : 2, Minions(game.State).Length); await game.Replay();
        }
        finally { await Save("same-generation-status-before-death-remove-" + remove, initial, game); }
    }

    [Fact]
    public async Task ActualControlRecallAndDestructionGiveNativeAndCopiedSkillsToTheDeathController()
    {
        var initial = Field(Seed(true), "UNIT", "P1");
        initial = Add(Add(Add(initial, "SONG-A", "SFD·059/221", "P1", "BASE"), "SONG-B", "SFD·059/221", "P1", "BASE"), "CONTROL", "OGN·203/298", "P2", "HAND");
        // Two real paid equip operations; the printed source and songs are controlled initial field material.
        for (var i = 0; i < 8; i++) initial = Add(initial, "CONTROL-RUNE-" + i, i < 3 ? "UNL-R05" : "UNL-R06", "P2", "BASE");
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-0")); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-1"));
            foreach (var gear in new[] { "SONG-A", "SONG-B" }) { await game.Act("P1", new AssembleEquipmentCommand(gear, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top(); }
            Assert.All(new[] { "SONG-A", "SONG-B" }, id => Assert.NotNull(game.State.CardObjects[id].AttachedRuleText));
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            for (var i = 0; i < 3; i++) await game.Act("P2", new RecycleRuneCommand("CONTROL-RUNE-" + i));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P2", new PlayCardCommand("CONTROL", "OGN·203/298", ["UNIT"])); await game.Top();
            Assert.Equal("P1", game.State.CardObjects["UNIT"].OwnerId); Assert.Equal("P2", game.State.CardObjects["UNIT"].ControllerId);
            Assert.Contains("UNIT", game.State.PlayerZones["P2"].Base); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            var earned = Creations(game.State); Assert.Equal(3, earned.Length);
            Assert.All(earned, c => { Assert.Equal("P2", c.ControllerId); Assert.True(c.SourceWasEmpowered); Assert.Equal(generation, c.Source.Generation); });
            Assert.Equal(3, earned.Select(c => c.InstanceId).Distinct().Count());
            await FinishCreations(game); Assert.Equal(6, Minions(game.State).Length);
            Assert.All(Minions(game.State), c => { Assert.Equal("P2", c.OwnerId); Assert.Equal("P2", c.ControllerId); Assert.Contains(c.ObjectId, game.State.PlayerZones["P2"].Base); });
            await game.Replay();
        }
        finally { await Save("actual-control-recall-copied-death-controller", initial, game); }
    }

    private static MatchState Seed(bool empowered)
    {
        var state = VenUnitEntryBasicTests.Position(No);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = empowered } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [], Base = [..state.PlayerZones["P1"].Base, "UNIT"] } },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        state = Add(state, "KILL", Kill, "P2", "HAND");
        for (var i = 0; i < 4; i++) { state = Add(state, "P1-RUNE-" + i, "UNL-R02", "P1", "BASE"); state = Add(state, "P2-RUNE-" + i, "UNL-R06", "P2", "BASE"); }
        return state;
    }
    private static MatchState Add(MatchState state, string id, string no, string owner, string zone)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone == "BASE" ? state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] } : state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static MatchState Field(MatchState state, string id, string player)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["BF"] = state.CardObjects["BF"] with { ControllerId = player } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with { Base = state.PlayerZones[player].Base.Where(x => x != id).ToArray(), Battlefields = [..state.PlayerZones[player].Battlefields, id] } } };
    private static async Task Tap(Driver game, string player)
    { foreach (var id in game.State.PlayerZones[player].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !game.State.CardObjects[id].IsExhausted).ToArray()) await game.Act(player, new TapRuneCommand(id)); }
    private static LastBreathUnitCreationContext[] Creations(MatchState state)
        => state.StackItems.Select(i => i.LastBreathUnitCreation).Concat(state.TriggerQueue.Select(i => i.LastBreathUnitCreation)).OfType<LastBreathUnitCreationContext>().ToArray();
    private static CardObjectState[] Minions(MatchState state) => state.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.MinionTokenFamily)).ToArray();
    private static async Task FinishCreations(Driver game)
    {
        for (var step = 0; Creations(game.State).Length > 0; step++)
        {
            Assert.InRange(step, 0, 12);
            if (game.State.TriggerQueue.Count > 0) await game.Act(game.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else await game.Top();
        }
    }
    private static async Task FinishEnd(Driver game, string player)
    {
        for (var i = 0; game.State.TurnPlayerId == player; i++)
        {
            Assert.InRange(i, 0, 12);
            if (game.State.TriggerQueue.Count > 0) await game.Act(game.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (game.State.StackItems.Count > 0) await game.Top();
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else Assert.Fail("End step must expose its next ordinary continuation.");
        }
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_LAST_BREATH_UNIT_CREATION_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new { scope = "Controlled initial material, actual battle/status/control/equip/destruction commands; not a canonical opening or L3.", initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
