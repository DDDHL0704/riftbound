using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven128EmpoweredLastBreathConsumerTests
{
    [Theory]
    [InlineData(0, "plain")] [InlineData(2, "plain")]
    [InlineData(0, "native")] [InlineData(2, "native")]
    [InlineData(1, "temporary")] [InlineData(2, "hourglass")]
    public async Task ActualResourceDestructionCostsCaptureTheConditionalTextInstancesBeforeLeavingPlay(int copies, string mode)
    {
        var initial = ResourceCostPosition(copies, mode); var game = new Driver(initial);
        MatchState? before = null; ResolutionResult? core = null; string? coreFailure = null, sessionFailure = null;
        try
        {
            Assert.Equal(0, initial.RunePools["P1"].Mana); Assert.Equal(0, initial.RunePools["P1"].TotalPower);
            await game.TapRunes("P1", Enumerable.Range(1, 6).Select(i => "P1-RUNE-" + i));
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            if (mode is "native" or "hourglass")
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-3"));
                await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
            }
            if (mode == "temporary")
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-1"));
                await game.Act("P1", new PlayCardCommand("TEMP-EMPOWER", "VEN·035", ["UNIT"], Mode: "EMPOWER_THEN_REMOVE"));
                await game.Top();
            }
            for (var i = 1; i <= copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + (mode == "temporary" ? 2 : i)));
                await game.Act("P1", new AssembleEquipmentCommand("COST-SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"]));
                await game.Top(); Assert.Equal("UNIT", game.State.CardObjects["COST-SONG-" + i].AttachedToObjectId);
            }
            before = game.State; var beforeHash = MatchStateHasher.Hash(before); var generation = before.CardObjects["UNIT"].ObjectGeneration;
            Assert.Equal(mode != "plain", before.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(0, before.RunePools["P1"].TotalPower); Assert.Empty(Minions(before));
            var command = new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["UNIT"]);
            try { core = await new CoreRuleEngine().ResolveAsync(before, new("actual-minion-resource-cost", "P1", command.CmdType), command, default); }
            catch (Exception e) { coreFailure = e.ToString(); }
            Assert.Equal(beforeHash, MatchStateHasher.Hash(before));
            try { await game.Act("P1", command); } catch (Exception e) { sessionFailure = e.ToString(); }
            Assert.Null(coreFailure); Assert.NotNull(core); Assert.True(core.Accepted, core.ErrorMessage); Assert.Null(sessionFailure);
            Assert.True(game.State.CardObjects["MALZ"].IsExhausted);
            Assert.Equal(before.RunePools["P1"].Mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
            Assert.Single(core.Events, e => e.Kind == "POWER_GAINED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "MALZ"));
            var expected = mode is "plain" or "hourglass" ? 0 : copies + 1;
            var queued = game.State.TriggerQueue.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCreateMinions).ToArray();
            var stacked = game.State.StackItems.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCreateMinions).ToArray();
            Assert.Equal(expected, core.State.TriggerQueue.Count(t => t.EffectKind == TriggerKinds.UnitLastBreathCreateMinions)
                + core.State.StackItems.Count(t => t.EffectKind == TriggerKinds.UnitLastBreathCreateMinions));
            Assert.Equal(expected, queued.Length + stacked.Length);
            if (mode == "hourglass")
            {
                Assert.Contains("HOURGLASS", game.State.PlayerZones["P1"].Graveyard);
                Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
                Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
                Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
                for (var i = 1; i <= copies; i++) Assert.Equal("UNIT", game.State.CardObjects["COST-SONG-" + i].AttachedToObjectId);
            }
            else
            {
                Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
                Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
                for (var i = 1; i <= copies; i++) Assert.Null(game.State.CardObjects["COST-SONG-" + i].AttachedToObjectId);
            }
            if (queued.Length > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queued.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i < expected; i++) await game.Top();
            Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems);
            Assert.Equal(expected * 2, Minions(game.State).Length);
            Assert.All(Minions(game.State), unit => {
                Assert.Equal("P1", unit.OwnerId); Assert.Equal("P1", unit.ControllerId); Assert.Equal(1, unit.Power);
                Assert.False(unit.IsEmpowered); Assert.True(unit.IsExhausted); Assert.False(string.IsNullOrWhiteSpace(unit.TokenFactoryCardNo));
                Assert.Contains(unit.ObjectId, game.State.PlayerZones["P1"].Base);
            });
            // The temporary No35 inverse is a separate later timing; this case
            // ends after all earned death skills rather than advancing that timing.
            if (mode != "temporary")
                for (var i = 0; i < 2; i++) { await game.Act(game.State.TurnPlayerId!, new EndTurnCommand()); Assert.Equal(expected * 2, Minions(game.State).Length); }
            await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN128_COST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, $"resource-cost-{copies}-{mode}.json"), JsonSerializer.Serialize(new {
                    scope = $"Explicit turn{initial.TurnNumber} local fixture with zero initial pool, physical runes, printed unit play, actual native/No35 Empower and paid attachments. Malzahar/Songs/hourglass are pre-existing public fixtures. Both actual Core and current prompt/tick Session resource destruction observed. Normal per-command recovery, dedup, privacy and original replay; temporary case stops before its later inverse. No ordinary opening or external acceptance.",
                    initial, before, core, coreFailure, sessionFailure, final = game.State, commands = game.Commands(), events = game.Events()
                }, Json));
            }
        }
    }

    private static MatchState ResourceCostPosition(int copies, string mode)
    {
        var state = Position("P1"); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var baseIds = state.PlayerZones["P1"].Base.ToList(); var hand = state.PlayerZones["P1"].Hand.ToList();
        void Put(string id, string no, bool inHand = false)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1");
            locations[id] = new("P1", inHand ? "HAND" : "BASE"); (inHand ? hand : baseIds).Add(id);
        }
        foreach (var i in new[] { 1, 2 }) cards["P1-RUNE-" + i] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-RUNE-" + i, "UNL-R02", "P1");
        foreach (var i in new[] { 5, 6 }) Put("P1-RUNE-" + i, "UNL-R06");
        for (var i = 1; i <= copies; i++) Put("COST-SONG-" + i, "SFD·059/221");
        Put("MALZ", P4ActivatedAbilityCatalog.MalzaharCardNo);
        if (mode == "temporary") Put("TEMP-EMPOWER", "VEN·035", true);
        if (mode == "hourglass") Put("HOURGLASS", "OGN·077/298");
        return state with { RoomId = $"VEN128-ACTUAL-RESOURCE-COST-{copies}-{mode}", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = baseIds, Hand = hand } } };
    }
}
