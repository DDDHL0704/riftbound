using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathRuneCallBoundaryTests
{
    private const string Unit = "OGN·216/298";
    private const string Kill = "OGN·229/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachActuallyPaidSongInstanceEarnsItsOwnPrintedDormantRuneCall(int copies)
    {
        var (initial, game, generation) = await Capture(copies);
        try
        {
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            for (var i = 0; i < copies; i++) Assert.Null(game.State.CardObjects["SONG-" + i].AttachedToObjectId);
            var queue = game.State.TriggerQueue.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCallRuneOne).ToArray();
            var stack = game.State.StackItems.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathCallRuneOne).ToArray();
            Assert.Equal(1 + copies, queue.Length + stack.Length);
            var before = game.State.PlayerZones["P1"].RuneDeck.ToArray(); var eventPrefix = game.Events().Length;
            if (queue.Length > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(1 + copies, game.State.StackItems.Count);
            for (var i = 0; i <= copies; i++)
            {
                Assert.Equal("P1", game.State.StackItems[^1].ControllerId); await game.Top();
                Assert.Equal(before.Length - i - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
                Assert.All(before.Take(i + 1), id => {
                    Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted);
                    Assert.Equal("P1", game.State.CardObjects[id].OwnerId); Assert.Equal("P1", game.State.CardObjects[id].ControllerId);
                });
            }
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            var calls = game.Events().Skip(eventPrefix).Where(e => e.Event.Kind == "RUNES_CALLED").ToArray();
            Assert.Equal(1 + copies, calls.Length); Assert.All(calls, e => Assert.Equal(1, Convert.ToInt32(e.Event.Payload["count"])));
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_LAST_BREATH_RUNE_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "actual-rune-last-breath-copies-" + copies + ".json"), JsonSerializer.Serialize(new {
                    scope = "Explicit turn-five initial hand/base/rune-deck material; zero resource pools, real printed unit play, paid green Song attachments and opponent destruction. Per-command recovery, accepted-intent dedup, hidden-zone checks and original command/event replay. No VEN078 gameplay, canonical opening, SQL, network or real restart claim.",
                    initial, final = game.State, commands = game.Commands(), events = game.Events()
                }, Json));
            }
        }
    }

    private static async Task<(MatchState Initial, Driver Game, long Generation)> Capture(int copies, bool privateDraw = false)
    {
        var initial = Position();
        if (privateDraw)
        {
            initial = Add(Add(Add(initial, "DRAW", "OGN·048/298", "P1", "HAND"), "DRAW-RUNE-A", "UNL-R06", "P1", "BASE"), "DRAW-RUNE-B", "UNL-R06", "P1", "BASE");
            var moved = initial.PlayerZones["P1"].MainDeck.Concat(initial.PlayerZones["P1"].Graveyard).ToArray();
            initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = [], Hand = [..initial.PlayerZones["P1"].Hand, ..moved] } },
                ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "HAND") : p.Value) };
        }
        var game = new Driver(initial);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P2"].Mana);
            await Tap(game, "P1", Enumerable.Range(0, 6).Select(i => "P1-RUNE-" + i));
            var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new PlayCardCommand("UNIT", Unit, []));
            Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.CardObjects["UNIT"].Power);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                mana = game.State.RunePools["P1"].Mana;
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
                Assert.Equal(Unit, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
                Assert.Equal(generation, game.State.CardObjects["SONG-" + i].AttachedRuleText!.Unit.Generation);
            }
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
        return (initial, game, generation);
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Unit) with { RoomId = "LAST-BREATH-RUNE-COPIES", RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        state = Add(state, "KILL", Kill, "P2", "HAND");
        for (var i = 0; i < 6; i++) state = Add(state, "P1-RUNE-" + i, i < 2 ? "UNL-R02" : "UNL-R06", "P1", "BASE");
        for (var i = 0; i < 4; i++) state = Add(state, "P2-RUNE-" + i, "UNL-R06", "P2", "BASE");
        for (var i = 0; i < 6; i++) state = Add(state, "DORMANT-RUNE-" + i, "UNL-R06", "P1", "RUNE_DECK");
        for (var i = 0; i < 2; i++) state = Add(state, "SONG-" + i, "SFD·059/221", "P1", "BASE");
        return state;
    }
    private static MatchState Add(MatchState state, string id, string no, string owner, string zone)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone switch {
                "BASE" => state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] },
                "RUNE_DECK" => state.PlayerZones[owner] with { RuneDeck = [..state.PlayerZones[owner].RuneDeck, id] },
                _ => state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } } };
    private static async Task Tap(Driver game, string player, IEnumerable<string>? selectedRunes = null)
    {
        var runes = (selectedRunes ?? game.State.PlayerZones[player].Base.Where(id =>
            game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !game.State.CardObjects[id].IsExhausted)).ToArray();
        // Prepare resources with the production batch command. The driver's
        // privacy, recovery, duplicate and original-history replay checks still
        // run after every command, including each actual last-breath resolution.
        foreach (var batch in runes.Chunk(12))
            await game.Act(player, new TapRuneCommand(batch[0], batch));
    }
}
