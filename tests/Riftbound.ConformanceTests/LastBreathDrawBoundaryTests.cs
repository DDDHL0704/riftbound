using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    private const string Unit = "OGN·096/298";
    private const string Kill = "OGN·229/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachActuallyPaidSongInstanceEarnsItsOwnPrintedDrawSkillAndResponseWindow(int copies)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new PlayCardCommand("UNIT", Unit, []));
            Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                mana = game.State.RunePools["P1"].Mana;
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                Assert.Equal(Unit, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
                Assert.Equal(generation, game.State.CardObjects["SONG-" + i].AttachedRuleText!.Unit.Generation);
            }
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            var deck = game.State.PlayerZones["P1"].MainDeck.ToArray(); var prefix = game.Events().Length;
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Empty(game.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind == "CARD_DRAWN" && Equals(e.Event.Payload.GetValueOrDefault("playerId"), "P1"));
            var queue = game.State.TriggerQueue.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathDrawOne).ToArray();
            var stack = game.State.StackItems.Where(t => t.EffectKind == TriggerKinds.UnitLastBreathDrawOne).ToArray();
            Assert.Equal(1 + copies, queue.Length + stack.Length);
            if (queue.Length > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(1 + copies, game.State.StackItems.Count);
            for (var i = 0; i <= copies; i++)
            {
                Assert.Equal("P1", game.State.StackItems[^1].ControllerId); await game.Top();
                Assert.Equal(deck.Take(i + 1), game.State.PlayerZones["P1"].Hand);
                Assert.Equal(deck.Skip(i + 1), game.State.PlayerZones["P1"].MainDeck);
            }
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("actual-paid-draw-copies-" + copies, initial, game); }
    }

    [Theory]
    [InlineData("SFD·036/221", false, true)] [InlineData("SFD·036/221", true, false)]
    [InlineData("UNL-156/219", true, true)] [InlineData("UNL-156/219", false, false)]
    public async Task ConditionalNativeDrawIsEarnedAtDeathAndWaitsForItsOwnResponse(string no, bool hasAlly, bool earnsDraw)
    {
        var initial = Position(no);
        if (!hasAlly) initial = initial with {
            CardObjects = initial.CardObjects.Where(p => p.Key != "ALLY").ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "ALLY").ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Where(id => id != "ALLY").ToArray() } } };
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            var prefix = game.Events().Length; var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.Empty(game.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind == "CARD_DRAWN" && Equals(e.Event.Payload.GetValueOrDefault("playerId"), "P1"));
            Assert.Equal(earnsDraw ? 1 : 0, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (earnsDraw) { await game.Top(); Assert.Equal(deck.Take(1), game.State.PlayerZones["P1"].Hand); }
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("conditional-native-" + no.Replace('/', '-') + "-ally-" + hasAlly, initial, game); }
    }

    private static MatchState Position(string no = Unit)
    {
        var state = VenUnitEntryBasicTests.Position(no) with { RoomId = "LAST-BREATH-DRAW-COPIES",
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        state = Add(state, "KILL", Kill, "P2", "HAND");
        for (var i = 0; i < 6; i++) state = Add(state, "P1-RUNE-" + i, i < 2 ? "UNL-R02" : "UNL-R06", "P1", "BASE");
        for (var i = 0; i < 4; i++) state = Add(state, "P2-RUNE-" + i, "UNL-R06", "P2", "BASE");
        for (var i = 0; i < 2; i++) state = Add(state, "SONG-" + i, "SFD·059/221", "P1", "BASE");
        return state;
    }

    private static MatchState Add(MatchState state, string id, string no, string owner, string zone)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone switch {
                "BASE" => state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] },
                "MAIN_DECK" => state.PlayerZones[owner] with { MainDeck = [..state.PlayerZones[owner].MainDeck, id] },
                "GRAVEYARD" => state.PlayerZones[owner] with { Graveyard = [..state.PlayerZones[owner].Graveyard, id] },
                _ => state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } } };

    private static async Task Tap(Driver game, string player)
    { foreach (var id in game.State.PlayerZones[player].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !game.State.CardObjects[id].IsExhausted).ToArray()) await game.Act(player, new TapRuneCommand(id)); }

    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_LAST_BREATH_DRAW_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Controlled turn-five initial material, zero pools, actual physical payment/copies/destruction; per-command privacy, recovery, dedup and original action/event replay. No ordinary opening, SQL/network/restart or whole-card certification.",
            initial, final = game.State, commands = game.Commands(), events = game.Events()
        }, Json));
    }
}
