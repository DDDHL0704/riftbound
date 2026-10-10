using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.DestructionOrderTests;

namespace Riftbound.ConformanceTests;

public sealed class DestructionOrderNativeTests
{
    [Theory]
    [InlineData("gear", "GEAR:D:G1")]
    [InlineData("sett", "SETT:D:LEGEND:ANY")]
    [InlineData("mandatory", "GEAR:D2:G1")]
    [InlineData("banish", "BANISH:D")]
    [InlineData("altar-pay", "ALTAR:D:BF")]
    [InlineData("altar-decline", "DECLINE")]
    public async Task ProductionReplacementDecisionsRestoreAndReplay(string branch, string selected)
    {
        var s = branch is "gear" or "sett" ? Gear(Position()) : Gear(NoReplacement(branch == "mandatory"));
        if (branch == "banish") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { UntilEndOfTurnEffects = ["BANISH_IF_DESTROYED_THIS_TURN"] } } };
        var opened = branch.StartsWith("altar") ? await AltarBattle() : await Open(s);
        var initial = OfficialInsightAndSpellLockTests.Restore(opened.State);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_ORDER_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        var prompt = session.PromptFor("P2"); Export("prompt.json", prompt); Export("snapshot.json", opened.Snapshots["P2"]);
        Export("selection.json", new { selected });
        GameCommand command = new PayCostCommand(initial.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [selected]);
        var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
        var native = dir is not null && File.Exists(Path.Combine(dir, "command.json"));
        if (native) {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!, "command.json")));
            raw = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString());
            Assert.Equal(initial.Tick, raw.GetProperty("snapshotTick").GetInt64());
        }
        Assert.Equal([selected], Assert.IsType<PayCostCommand>(command).PaymentChoiceIds);
        var result = await session.SubmitAsync("P2", "replacement", command, raw, default);
        Assert.True(result.Accepted, result.ErrorMessage); Assert.Null(result.State.PendingRuleChoice);
        OfficialGraveyardRecastTests.Restore(result.State);
        if (branch == "banish") Assert.Contains("D", result.State.PlayerZones["P2"].Banished);
        else if (branch == "altar-decline") Assert.Contains("D", result.State.PlayerZones["P2"].Graveyard);
        else Assert.Contains(branch == "mandatory" ? "D2" : "D", result.State.PlayerZones["P2"].Base);
        var duplicate = await session.SubmitAsync("P2", "replacement", command, raw, default);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev,i) => new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("replay.json",new {native,replay,commands=commands.Length});
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry);return ValueTask.CompletedTask; }
    }
}
