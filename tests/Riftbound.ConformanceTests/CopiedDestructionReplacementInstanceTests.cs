using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedDestructionReplacementInstanceTests
{
    [Theory]
    [InlineData("SFD·173/221", false)] [InlineData("SFD·173/221", true)]
    [InlineData("SFD·239/221", false)] [InlineData("SFD·239/221", true)]
    [InlineData("SFD·239*/221", false)] [InlineData("SFD·239*/221", true)]
    public async Task ActualCopiedSorakaCanStartItsUnusedGroupAfterTheSourceIsRecalled(string no, bool copied)
    {
        var initial = Position(no, copied); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("S", no, [], Destination: "BATTLEFIELD:HOME"));
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" })
            await run.Act("P1", new PlayCardCommand(id, "OGN·210/298", [], Destination:
                id.StartsWith("FIELD", StringComparison.Ordinal) ? "BATTLEFIELD:HOME" : "BASE"));
        await run.Act("P1", new PlayCardCommand("G1", "OGN·077/298", [], Destination: "BASE"));
        if (copied) {
            await run.Act("P1", new PlayCardCommand("SONG", "SFD·059/221", [], Destination: "BASE"));
            await run.Act("P1", new AssembleEquipmentCommand("SONG", "S", ["ASSEMBLE_GREEN"])); await run.Top();
            Assert.Equal("S", run.State.CardObjects["SONG"].AttachedToObjectId);
            Assert.Equal(no, run.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        }
        Assert.Equal(4, run.State.CardObjects["S"].Power);
        Assert.All(new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }, id => Assert.Equal(2, run.State.CardObjects[id].Power));
        await run.Act("P1", new EndTurnCommand());
        var runes = Enumerable.Range(1, 9).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(runes[0], runes));
        await run.Act("P2", new RecycleRuneCommand(runes[0], runes.Take(3).ToArray()));
        await run.Act("P2", new PlayCardCommand("AOE", "UNL-180/219", [])); await run.Top();
        await Select(run, "LOCAL_RECALL:FIELD-1:S");
        await Select(run, "GEAR:S:G1");
        Export("frontier", initial, run, no, copied);
        if (copied) {
            var pending = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
            Assert.Equal("P1", pending.Request.PlayerId);
            var option = Assert.Single(pending.Request.Options, o => o.ObjectIds!.Contains("BASE-1")
                && o.Id.StartsWith("LOCAL_RECALL:", StringComparison.Ordinal));
            await run.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option.Id]));
        }
        Assert.Contains("S", run.State.PlayerZones["P1"].Base);
        Assert.Contains("G1", run.State.PlayerZones["P1"].Graveyard);
        Assert.All(new[] { "FIELD-1", "FIELD-2" }, id => Assert.Contains(id, run.State.PlayerZones["P1"].Base));
        foreach (var id in new[] { "BASE-1", "BASE-2" }) {
            Assert.Equal(copied, run.State.PlayerZones["P1"].Base.Contains(id));
            Assert.Equal(!copied, run.State.PlayerZones["P1"].Graveyard.Contains(id));
        }
        Assert.Null(run.State.PendingRuleChoice); await run.Replay(); Export("complete", initial, run, no, copied);
    }

    private static async Task Select(Driver run, string id)
    {
        var request = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice).Request;
        Assert.Contains(request.Options, option => option.Id == id);
        await run.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]));
    }

    private static MatchState Position(string no, bool copied)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string number, string player, string zone, string? field = null) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, number, player); locations[id] = new(player, zone, field);
        }
        Put("S", no, "P1", "HAND"); Put("G1", "OGN·077/298", "P1", "HAND");
        Put("HOME", "OGN·278/298", "P1", "BATTLEFIELD", "HOME"); cards["HOME"] = cards["HOME"] with { ControllerId = "P1" };
        var hand = new List<string> { "S", "G1" };
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }) { Put(id, "OGN·210/298", "P1", "HAND"); hand.Add(id); }
        if (copied) { Put("SONG", "SFD·059/221", "P1", "HAND"); hand.Add("SONG"); }
        Put("AOE", "UNL-180/219", "P2", "HAND");
        var runes = Enumerable.Range(1, 9).Select(i => "P2-FUND-" + i).ToArray(); foreach (var id in runes) Put(id, "UNL-R06", "P2", "BASE");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 4).Select(i => player + "-DRAW-" + i).Append("PRIVATE-" + player).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { MainDeck = deck, Hand = player == "P1" ? hand.ToArray() : ["AOE"],
                Base = player == "P1" ? [] : runes, Battlefields = player == "P1" ? ["HOME"] : [] };
        }
        return new MatchState("C8-COPIED-LOCAL-REPLACEMENT", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(70, 0, new Dictionary<string, int> {
                [RuneTrait.Green] = 10, [RuneTrait.Yellow] = 10 }), ["P2"] = RunePool.Empty }, seed: 424242) with { CardPlays = new(5, [], []) };
    }

    private static void Export(string stage, MatchState initial, Driver run, string no, bool copied)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C8_COPIED_DESTRUCTION_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); var name = (stage + "-" + no + "-" + copied).Replace('/', '_').Replace('*', 's');
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = run.State, commands = run.Commands(), events = run.Events(),
            expectedBaseTargetsSaved = copied, rule = "CN719.1/479.1.d plus CN370.2/373.2; each copied replacement text is an independent unstarted group."
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
