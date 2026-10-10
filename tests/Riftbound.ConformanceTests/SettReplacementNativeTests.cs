using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;

namespace Riftbound.ConformanceTests;

public sealed class SettReplacementNativeTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task NativeReplacementChoicesRestoreAndReplay(bool rune, bool accept)
    {
        var opened = await Open(rune ? WithRune(Position(power:0, two:true)) : Position(two:true));
        var initial = OfficialInsightAndSpellLockTests.Restore(opened.State);
        var branch = (rune ? "sett-rune" : "sett") + (accept ? "-accept" : "-decline");
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial,engine,journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SETT_EVIDENCE"); var directory = root is null ? null : Path.Combine(root,branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string file, object value) { if(directory is null) return; Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory,file),JsonSerializer.Serialize(value,value.GetType(),json)); }
        var prompt=session.PromptFor("P2"); Export("prompt.json",prompt); Export("snapshot.json",opened.Snapshots["P2"]);
        var p=initial.PendingRuleChoice!.Request; var selected = accept ? $"SETT:D2:LEGEND:{(rune?"RUNE:RUNE":"ANY")}" : "DECLINE";
        GameCommand command = new PayCostCommand(p.Id,"RULE_REPLACEMENT",[selected]);
        var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
        var path=directory is null?null:Path.Combine(directory,"command.json"); var usedNative=path is not null&&File.Exists(path);
        if(usedNative) { using var d=JsonDocument.Parse(File.ReadAllText(path!)); raw=d.RootElement.Clone(); command=GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString()); Assert.Equal(initial.Tick,raw.GetProperty("snapshotTick").GetInt64()); }
        var result=await session.SubmitAsync("P2",branch,command,raw,default); Assert.True(result.Accepted,result.ErrorMessage);
        OfficialGraveyardRecastTests.Restore(result.State);
        Assert.Equal(accept,result.State.PlayerZones["P2"].Base.Contains("D2")); Assert.Contains("D",result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(accept,result.State.CardObjects["LEGEND"].IsExhausted);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("replay.json",new{usedNative,replay});
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
