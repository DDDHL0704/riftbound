using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathRuneCallBoundaryTests
{
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(4)]
    public async Task ActualDeathControllerKeepsAllCopiedRuneCallsAfterSourceReturnsToOwnerEvenWithTooFewRunes(int availableRunes)
    {
        var initial = Add(Position(), "CONTROL", "OGN·203/298", "P2", "HAND");
        // Controlled face-up field material; control, paid copies, death and the private return use commands.
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray(),
                Battlefields = [..initial.PlayerZones["P1"].Battlefields, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "BF") } };
        for (var i = 0; i < 8; i++) initial = Add(initial, "CONTROL-RUNE-" + i, i < 3 ? "UNL-R05" : "UNL-R06", "P2", "BASE");
        // The next natural turn channels two before this test's last-breath skills are earned.
        for (var i = 0; i < availableRunes + 2; i++) initial = Add(initial, "P2-DORMANT-" + i, "UNL-R06", "P2", "RUNE_DECK");
        // Recycled runes would replenish the deck and defeat the scarcity boundary.
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, out var gold));
        var goldIds = Enumerable.Range(0, 5).Select(i => "P2-GOLD-" + i).ToArray();
        initial = initial with { CardObjects = initial.CardObjects.Concat(goldIds.Select(id => new KeyValuePair<string, CardObjectState>(id, gold.CreateObject(id, "P2", "P2")))).ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Concat(goldIds.Select(id => new KeyValuePair<string, ObjectLocationState>(id, new("P2", "BASE")))).ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, ..goldIds] } } };
        initial = Add(Add(Add(initial, "DRAW", "OGN·048/298", "P1", "HAND"), "DRAW-RUNE-A", "UNL-R06", "P1", "BASE"), "DRAW-RUNE-B", "UNL-R06", "P1", "BASE");
        var moved = initial.PlayerZones["P1"].MainDeck.Concat(initial.PlayerZones["P1"].Graveyard).ToArray();
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = [], Hand = [..initial.PlayerZones["P1"].Hand, ..moved] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "HAND") : p.Value) };
        var game = new Driver(initial);
        // The setup creates the copied sources and the controlled death queue.
        // Keep command-level duplicate/privacy checks, then cold-restore once
        // before exercising the real private draw and rune scarcity boundary.
        game.RecoverAfterEveryCommand = false;
        try
        {
            await Tap(game, "P1", Enumerable.Range(0, 6).Select(i => "P1-RUNE-" + i));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < 2; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(Unit, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            Assert.Equal(availableRunes, game.State.PlayerZones["P2"].RuneDeck.Count);
            for (var i = 0; i < 3; i++) await game.Act("P2", new ActivateAbilityCommand(goldIds[i], P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
            Assert.Equal(3, game.State.RunePools["P2"].Power);
            await game.Act("P2", new PlayCardCommand("CONTROL", "OGN·203/298", ["UNIT"])); await game.Top();
            Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            Assert.Equal("P2", game.State.CardObjects["UNIT"].ControllerId); Assert.Contains("UNIT", game.State.PlayerZones["P2"].Base);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            foreach (var id in goldIds.Skip(3)) await game.Act("P2", new ActivateAbilityCommand(id, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
            Assert.Equal(2, game.State.RunePools["P2"].Power);
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
            game.RecoverAfterEveryCommand = true;
            await game.ColdCheckpoint();
            Assert.Equal(0, game.State.RunePools["P2"].TotalPower); Assert.All(goldIds, id => Assert.False(game.State.CardObjects.ContainsKey(id)));
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.All(game.State.TriggerQueue, item => {
                Assert.Equal("P2", item.ControllerId); Assert.Equal("P2", item.LastBreathRuneCall!.ControllerId);
                Assert.Equal(generation, item.LastBreathRuneCall.Source.Generation);
            });
            Assert.Equal(3, game.State.TriggerQueue.Select(item => item.LastBreathRuneCall!.InstanceId).Distinct().Count());
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("DRAW-RUNE-A", ["DRAW-RUNE-A", "DRAW-RUNE-B"]));
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation + 1);
            Assert.Equal(3, game.State.StackItems.Count); Assert.All(game.State.StackItems, item => Assert.Equal("P2", item.ControllerId));
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var stack = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().ToArray();
                Assert.Equal(3, stack.Length);
                Assert.All(stack, item => {
                    Assert.Equal("HIDDEN", item.GetProperty("sourceObjectId").GetString());
                    Assert.Equal(TriggerKinds.UnitLastBreathCallRuneOne, item.GetProperty("effectKind").GetString());
                    Assert.False(item.TryGetProperty("lastBreathRuneCall", out _));
                });
            }
            var ownerRunes = game.State.PlayerZones["P1"].RuneDeck.ToArray();
            var runes = game.State.PlayerZones["P2"].RuneDeck.ToArray(); Assert.Equal(availableRunes, runes.Length);
            var eventPrefix = game.Events().Length;
            for (var i = 0; i < 3; i++)
            {
                await game.Top(); Assert.Equal(Math.Max(0, availableRunes - i - 1), game.State.PlayerZones["P2"].RuneDeck.Count);
                Assert.Equal(ownerRunes, game.State.PlayerZones["P1"].RuneDeck);
                Assert.All(runes.Take(Math.Min(i + 1, availableRunes)), id => {
                    Assert.Contains(id, game.State.PlayerZones["P2"].Base); Assert.True(game.State.CardObjects[id].IsExhausted);
                    Assert.Equal("P2", game.State.CardObjects[id].OwnerId); Assert.Equal("P2", game.State.CardObjects[id].ControllerId);
                });
            }
            var calls = game.Events().Skip(eventPrefix).Where(e => e.Event.Kind == "RUNES_CALLED").ToArray();
            Assert.Equal(3, calls.Length); Assert.All(calls, e => Assert.Equal("P2", e.Event.Payload["playerId"]));
            Assert.Equal(Enumerable.Range(0, 3).Select(i => i < availableRunes ? 1 : 0), calls.Select(e => Convert.ToInt32(e.Event.Payload["count"])));
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await SaveRuneRecovery("actual-death-controller-private-source-runes-" + availableRunes, initial, game); }
    }
}
