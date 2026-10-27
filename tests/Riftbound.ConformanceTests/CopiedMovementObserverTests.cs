using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedMovementObserverTests
{
    public static IEnumerable<object[]> SelfCases()
        => from no in new[] { "SFD·130/221", "SFD·048/221", "SFD·137/221", "UNL-115/219", "UNL-127/219",
            "OGN·185/298", "UNL-080/219", "VEN·033", "OGN·222/298", "SFD·179/221", "UNL-082/219", "OGN·205/298", "UNL-022/219" }
           from copies in new[] { 0, 1, 2 } select new object[] { no, copies };

    [Theory]
    [MemberData(nameof(SelfCases))]
    public async Task RealAssemblyAndOneActualMoveUseEveryIndependentTextInstance(string no, int copies)
    {
        var initial = Position(no, copies); var game = new Driver(initial);
        try
        {
            await Assemble(game, copies);
            var before = game.State; var sequence = game.Events().LastOrDefault()?.Sequence ?? 0;
            await game.Act("P2", new MoveUnitCommand("HOST", no == "SFD·137/221" ? "BATTLEFIELD" : "BASE",
                no == "SFD·137/221" ? "BASE" : "BATTLEFIELD:F2", []));
            Assert.Equal(no == "OGN·205/298" ? 3 : 1, game.State.CardObjects["HOST"].MovementCountThisTurn);
            if (no == "UNL-022/219")
            {
                Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems);
                Assert.Equal(before.RunePools["P2"].Mana + copies + 1, game.State.RunePools["P2"].Mana);
                Assert.Equal(before.RunePools["P2"].Power + copies + 1, game.State.RunePools["P2"].Power);
            }
            else
            {
                var earned = game.State.TriggerQueue.Select(t => t.MovementObserver)
                    .Concat(game.State.StackItems.Select(t => t.MovementObserver)).OfType<MovementObserverContext>().ToArray();
                Assert.Equal(copies + 1, earned.Length);
                Assert.All(earned, c => { Assert.Equal(no, c.CardNo); Assert.Equal("HOST", c.Unit.ObjectId); });
                await Finish(game);
                if (no == "SFD·130/221") Assert.Equal(copies + 1, game.State.CardObjects.Values.Count(c => c.Tags.Contains("金币")));
                if (no == "SFD·048/221") Assert.Equal(before.PlayerZones["P2"].Hand.Count + copies + 1, game.State.PlayerZones["P2"].Hand.Count);
                if (no == "SFD·137/221") Assert.Equal(before.CardObjects["HOST"].Power + 2 * (copies + 1), game.State.CardObjects["HOST"].Power);
                if (no is "UNL-115/219" or "UNL-127/219") Assert.Equal((copies + 1) * (no == "UNL-115/219" ? 1 : 2), game.State.PlayerExperience["P2"]);
                if (no == "OGN·185/298") Assert.Equal(copies + 1, game.State.PlayerZones["P2"].Graveyard.Count(id => id.StartsWith("FOOD")));
                if (no == "UNL-080/219") Assert.Equal(before.CardObjects["HOST"].Power + 3 * (copies + 1), game.State.CardObjects["HOST"].Power);
                if (no == "VEN·033") Assert.Equal(before.CardObjects["HOST"].Power + 2 * (copies + 1), game.State.CardObjects["HOST"].Power);
                if (no is "OGN·222/298" or "SFD·179/221" or "UNL-082/219")
                    Assert.Equal((copies + 1) * (no == "SFD·179/221" ? 3 : 1), game.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo is not null));
                if (no == "OGN·205/298") Assert.Equal(copies + 1, game.State.PlayerScores["P2"] - before.PlayerScores.GetValueOrDefault("P2"));
            }
            Assert.Equal(copies + 1, game.Events().Count(e => e.Sequence > sequence && e.Event.Kind == "TRIGGER_QUEUED"
                && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")
                && !Equals(e.Event.Payload.GetValueOrDefault("triggeredByEventKind"), "CARD_DISCARDED")));
            await game.Replay();
        }
        finally { Save(game, initial, "self-" + no.Replace('/', '_') + "-" + copies); }
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 1)] [InlineData(2, 1)]
    [InlineData(0, 2)] [InlineData(1, 2)] [InlineData(2, 2)]
    public async Task OneOpponentGroupMovementEarnsOneDrawPerTextRatherThanPerMovedUnit(int copies, int units)
    {
        var initial = Position("OGN·158/298", copies); var game = new Driver(initial);
        try
        {
            await Assemble(game, copies); await game.Act("P2", new EndTurnCommand());
            var hand = game.State.PlayerZones["P2"].Hand.Count;
            var ids = Enumerable.Range(0, units).Select(i => "MOVER" + i).ToArray();
            await game.Act("P1", new MoveUnitCommand(ids[0], Destination: "BATTLEFIELD:F1", SourceObjectIds: ids));
            var earned = game.State.TriggerQueue.Select(t => t.MovementObserver)
                .Concat(game.State.StackItems.Select(t => t.MovementObserver)).OfType<MovementObserverContext>().ToArray();
            Assert.Equal(copies + 1, earned.Length);
            Assert.All(earned, c => { Assert.Equal(units, c.MovementBatch!.Count); Assert.Equal("P1", c.Unit.MovingPlayerId); });
            await Finish(game); Assert.Equal(hand + copies + 1, game.State.PlayerZones["P2"].Hand.Count); await game.Replay();
        }
        finally { Save(game, initial, $"opponent-{copies}-{units}"); }
    }

    internal static MatchState Position(string no, int copies)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("HOST", no, "P2", no == "SFD·137/221" ? "BATTLEFIELD" : "BASE", no == "SFD·137/221" ? "F2" : null);
        if (no == "OGN·205/298") cards["HOST"] = cards["HOST"] with { MovementCountThisTurn = 2 };
        for (var i = 0; i < copies; i++) Put("GEAR" + i, "SFD·059/221", "P2", "BASE");
        for (var i = 0; i < 4; i++) Put("FOOD" + i, "OGN·219/298", "P2", "HAND");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" })
        {
            var field = player == "P1" ? "F1" : "F2";
            Put(field, "OGN·278/298", player, "BATTLEFIELD", field);
            cards[field] = cards[field] with { ControllerId = player };
            Put(player + "-KEEPER", "OGN·219/298", player, "BATTLEFIELD", field);
            var deck = Enumerable.Range(0, 10).Select(i => player + "-DRAW" + i).ToArray();
            foreach (var id in deck) Put(id, no == "VEN·033" ? "OGN·133/298" : "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { Battlefields = [field, player + "-KEEPER"], MainDeck = deck };
        }
        for (var i = 0; i < 2; i++) Put("MOVER" + i, "OGN·219/298", "P1", "BASE");
        zones["P1"] = zones["P1"] with { Base = ["MOVER0", "MOVER1"] };
        zones["P2"] = zones["P2"] with { Base = [..Enumerable.Range(0, copies).Select(i => "GEAR" + i), ..no == "SFD·137/221" ? Array.Empty<string>() : ["HOST"]],
            Battlefields = [..zones["P2"].Battlefields, ..no == "SFD·137/221" ? ["HOST"] : Array.Empty<string>()],
            Hand = Enumerable.Range(0, 4).Select(i => "FOOD" + i).ToArray() };
        return new MatchState("COPIED-MOVEMENT-EXPLICIT-MIDGAME", 0, 5, "P2", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            turnPlayerId: "P2", playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(30, 20, new Dictionary<string, int> { [RuneTrait.Green] = copies }) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }

    internal static async Task Assemble(Driver game, int copies)
    {
        for (var i = 0; i < copies; i++)
        { await game.Act("P2", new AssembleEquipmentCommand("GEAR" + i, "HOST", ["ASSEMBLE_GREEN"])); await game.Top(); }
    }
    internal static async Task Finish(Driver game)
    {
        for (var i = 0; i < 64; i++)
        {
            if (game.State.PendingHandChoice is { } hand)
                await game.Act(hand.PlayerId, new ChooseHandCardsCommand(hand.ChoiceId, hand.ChoiceWindow,
                    [hand.LegalObjectIds.FirstOrDefault(id => id.StartsWith("FOOD")) ?? hand.LegalObjectIds[0]]));
            else if (game.State.TriggerQueue.Count > 0)
                await game.Act(game.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(
                    OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (game.State.StackItems.Count > 0) await game.Top();
            else return;
        }
        Assert.Fail("Movement skill chain did not complete within its finite bound.");
    }
    internal static void Save(Driver game, MatchState initial, string label)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_MOVEMENT_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame; ordinary assembly/movement/response commands and unchanged original replay",
            initial, final = game.State, commands = game.Commands(), events = game.Events()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
