using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.DestructionOrderTests;

namespace Riftbound.ConformanceTests;

public sealed class LocalDestructionRecallNativeTests
{
    [Theory]
    [InlineData("local", "LOCAL_RECALL:D:S")]
    [InlineData("gear", "GEAR:D:G1")]
    [InlineData("simultaneous", "LOCAL_RECALL:D2:S")]
    [InlineData("source-after", "LOCAL_RECALL:D:S")]
    public async Task ProductionLocalRecallChoicesRestoreAndReplay(string branch, string selected)
    {
        var s = branch == "simultaneous" ? LocalDestructionRecallTests.PositionFor(true) : Gear(LocalDestructionRecallTests.PositionFor());
        if (branch == "source-after") s=LocalDestructionRecallTests.At(Source(LocalDestructionRecallTests.PositionFor(),"SFD·173/221","S2"),"S2","P2","BATTLEFIELD","BF");
        var opened = await Open(s, branch is "simultaneous" or "source-after" ? "UNL-180/219" : "OGN·229/298",
            branch is "simultaneous" or "source-after" ? [] : ["D"]);
        if (branch == "source-after") opened=await Select(opened.State,"LOCAL_RECALL:S:S2");
        var initial = OfficialInsightAndSpellLockTests.Restore(opened.State);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_LOCAL_RECALL_EVIDENCE");
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
        Assert.Contains("D", result.State.PlayerZones["P2"].Base);
        if(branch == "gear") Assert.Contains("G1",result.State.PlayerZones["P2"].Graveyard);
        if(branch == "local") Assert.Contains("G1",result.State.PlayerZones["P2"].Base);
        if(branch == "simultaneous") {
            Assert.Contains("D2",result.State.PlayerZones["P2"].Base); Assert.Contains("S",result.State.PlayerZones["P2"].Graveyard);
        }
        if(branch == "source-after") {
            Assert.Contains("S",result.State.PlayerZones["P2"].Base); Assert.Contains("S2",result.State.PlayerZones["P2"].Graveyard);
        }
        Export("result-snapshot.json",result.Snapshots["P2"]);
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
