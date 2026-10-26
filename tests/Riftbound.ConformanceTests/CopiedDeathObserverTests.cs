using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedDeathObserverTests
{
    public static IEnumerable<object[]> Cases()
        => from no in new[] { "UNL-068/219", "UNL-129/219", "OGN·118/298", "ARC-006/006" }
           from copies in new[] { 0, 1, 2 } select new object[] { no, copies };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ActualAssemblyGivesEachPrintedDeathTextInstanceItsOwnOrdinaryTrigger(string no, int copies)
    {
        var initial = Position(no, copies); var game = new Driver(initial);
        try
        {
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P2", new AssembleEquipmentCommand("GEAR" + i, "HOST", ["ASSEMBLE_GREEN"]));
                await game.Top();
                var bound = Assert.IsType<AttachedRuleTextState>(game.State.CardObjects["GEAR" + i].AttachedRuleText);
                Assert.Equal("HOST", bound.Unit.ObjectId); Assert.Equal(no, bound.CardNo);
            }
            Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            await game.Act("P2", new EndTurnCommand());
            Assert.Equal("P1", game.State.TurnPlayerId);
            await game.Act("P1", new TapRuneCommand("PAY0"));
            var before = game.State; var firstSequence = game.Events().Last().Sequence;
            await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
            await game.Top();
            Assert.Contains("D", game.State.PlayerZones["P2"].Graveyard);
            Assert.Equal(1, game.State.DeathLedger.Counts["P2"]);
            var earned = game.State.TriggerQueue.Where(t => t.DeathObserver is not null).Select(t => t.SourceObjectId)
                .Concat(game.State.StackItems.Where(t => t.DeathObserver is not null).Select(t => t.SourceObjectId)).ToArray();
            Assert.Equal(copies + 1, earned.Length); Assert.All(earned, id => Assert.Equal("HOST", id));
            Assert.Equal(copies + 1, game.Events().Count(e => e.Sequence > firstSequence && e.Event.Kind == "TRIGGER_QUEUED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")));
            Assert.Equal(before.PlayerExperience.GetValueOrDefault("P2"), game.State.PlayerExperience.GetValueOrDefault("P2"));
            Assert.Equal(before.CardObjects["HOST"].Power, game.State.CardObjects["HOST"].Power);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i <= copies; i++) await game.Top();
            var events = game.Events().Where(e => e.Sequence > firstSequence).ToArray();
            Assert.Equal(copies + 1, events.Count(e => e.Event.Kind == "TRIGGER_RESOLVED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")));
            if (no == "UNL-068/219") Assert.Equal(before.CardObjects["HOST"].Power + 2 * (copies + 1), game.State.CardObjects["HOST"].Power);
            if (no == "UNL-129/219") Assert.Equal(before.PlayerExperience.GetValueOrDefault("P2") + copies + 1, game.State.PlayerExperience.GetValueOrDefault("P2"));
            if (no == "OGN·118/298") Assert.Equal(before.PlayerZones["P2"].Hand.Count + copies + 1, game.State.PlayerZones["P2"].Hand.Count);
            if (no == "ARC-006/006") Assert.Equal(copies + 1, events.Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
            await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_DEATH_EVIDENCE") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, no.Replace('/', '_') + "-" + copies + ".json"),
                    JsonSerializer.Serialize(new { scope = "Explicit midgame; actual assembly, ordinary death, response and unchanged original replay", initial,
                        final = game.State, commands = game.Commands(), events = game.Events() }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            }
        }
    }

    internal static MatchState Position(string no, int copies, bool two = false)
    {
        var s = DeathObserverAuditTests.Source(DeathObserverAuditTests.NoReplacement(two), no, "HOST");
        var cards = s.CardObjects.ToDictionary(); var zones = s.PlayerZones.ToDictionary(); var locations = s.ObjectLocations.ToDictionary();
        cards["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P2");
        cards.Remove("LEGEND"); locations.Remove("LEGEND"); zones["P2"] = zones["P2"] with { LegendZone = [] };
        for (var i = 0; i < copies; i++)
        {
            var id = "GEAR" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "SFD·059/221", "P2");
            locations[id] = new("P2", "BASE"); zones["P2"] = zones["P2"] with { Base = [..zones["P2"].Base, id] };
        }
        for (var i = 0; i < 4; i++)
        {
            var id = "PAY" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·007/298", "P1");
            locations[id] = new("P1", "BASE"); zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, id] };
        }
        foreach (var player in new[] { "P1", "P2" })
            for (var i = 0; i < 6; i++)
            {
                var id = player + "-EXTRA-DRAW" + i;
                cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·012/298", player);
                locations[id] = new(player, "MAIN_DECK"); zones[player] = zones[player] with { MainDeck = [..zones[player].MainDeck, id] };
            }
        return s with { RoomId = "COPIED-DEATH-EXPLICIT-MIDGAME", ActivePlayerId = "P2", TurnPlayerId = "P2", CardObjects = cards,
            PlayerZones = zones, ObjectLocations = locations, RunePools = new Dictionary<string, RunePool>(s.RunePools) {
                ["P2"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = copies }) } };
    }
}
