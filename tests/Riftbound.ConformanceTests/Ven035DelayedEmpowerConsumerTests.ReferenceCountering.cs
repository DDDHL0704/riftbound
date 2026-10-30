using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Theory]
    [InlineData("OGN·045/298")] [InlineData("SFD·045/221")]
    public async Task AHistoricalInverseIsNeitherASpellNorANewTargetForARestrictedCounter(string counter)
    {
        // CN355.10.d and359.3.f.3.a: the inverse refers to the already fixed incarnation;
        // it makes no new target choice. Its source being a spell does not make it one.
        var initial = Add(Position("P2"), "REFERENCE-COUNTER", counter, "P2");
        for (var i = 0; i < 2; i++) initial = Add(initial, "REFERENCE-COUNTER-RUNE-" + i, "UNL-R02", "P2", "BASE");
        var game = new Driver(initial);
        try
        {
            var unit = await PrepareTarget(game, "P2", true);
            await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            var generation = game.State.CardObjects[unit].ObjectGeneration; var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new PlayCardCommand("SPELL", No, [unit], Mode: Empower)); await game.Top();
            Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new EndTurnCommand());
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            var inverse = Assert.Single(game.State.StackItems); Assert.NotNull(inverse.DelayedEmpower);
            Assert.Equal("P1", inverse.ControllerId); Assert.Equal("P2", game.State.CardObjects[unit].ControllerId);
            if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.TapRunes("P2", Enumerable.Range(0, 2).Select(i => "REFERENCE-COUNTER-RUNE-" + i));
            await game.Act("P2", new RecycleRuneCommand("REFERENCE-COUNTER-RUNE-0"));
            Assert.Equal(2, game.State.RunePools["P2"].Mana); Assert.Equal(1, game.State.RunePools["P2"].PowerByTrait[RuneTrait.Green]);
            var before = MatchStateHasher.Hash(game.State);
            var command = new PlayCardCommand("REFERENCE-COUNTER", counter, [inverse.StackItemId]);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = game.PromptFor("P2").PromptId; raw["snapshotTick"] = game.State.Tick;
            await game.ActRetained(new("P2", "no35-reference-counter", JsonSerializer.SerializeToElement(raw, Json)), false);
            Assert.Equal(ErrorCodes.InvalidTarget, game.Commands()[^1].ErrorCode);
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
            Assert.Contains("REFERENCE-COUNTER", game.State.PlayerZones["P2"].Hand);
            Assert.Equal(inverse.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
            // P1 has already passed; immediate rune actions and a rejected counter
            // do not start a fresh two-pass round.
            for (var step = 0; game.State.TurnPlayerId == "P1"; step++)
            {
                Assert.InRange(step, 0, 5);
                Assert.NotNull(game.State.PriorityPlayerId);
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            }
            Assert.Equal("P2", game.State.TurnPlayerId); Assert.False(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
            await game.Act("P2", new EndTurnCommand()); await game.Act("P1", new EndTurnCommand());
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal("P2", game.State.TurnPlayerId);
            await game.Replay(); Restore(game.State);
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN035_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "historical-inverse-counter-" + counter.Replace('/', '-') + ".json"),
                    JsonSerializer.Serialize(new { scope = "Actual restricted-counter Session submission to a genuine earned inverse. Historical reference is not a new target choice. Returned observation may be accepted while driverState remains before its failing assertion; do not infer rejection from driverState alone.",
                        initial, driverState = game.State, commands = game.Commands(), events = game.Events(),
                        sessionObservation = game.LastRetainedResult }, Json));
            }
        }
    }
}
