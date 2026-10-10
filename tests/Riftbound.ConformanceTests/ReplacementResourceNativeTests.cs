using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.ReplacementResourceTests;

namespace Riftbound.ConformanceTests;

public sealed class ReplacementResourceNativeTests
{
    [Theory]
    [InlineData(false,true)]
    [InlineData(false,false)]
    [InlineData(true,true)]
    [InlineData(true,false)]
    public async Task NativeResourceAndReplacementChoicesRestoreAndReplay(bool sigil,bool accept)
    {
        var opened = await Open(AddResource(Position(power:0,two:true),sigil ? P4ActivatedAbilityCatalog.RageSigilCardNo : P4ActivatedAbilityCatalog.GoldTokenUnlCardNo));
        var initial = OfficialInsightAndSpellLockTests.Restore(opened.State);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial,engine,journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_RESOURCE_EVIDENCE");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var state = initial; var nativeCount = 0;
        for (var step=1;step<=2;step++)
        {
            var branch = $"resource-{(sigil?"sigil":"gold")}-step{step}-{(accept?"accept":"decline")}";
            var dir = root is null ? null : Path.Combine(root,branch);
            void Export(string file,object value) { if(dir is null) return; Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir,file),JsonSerializer.Serialize(value,value.GetType(),json)); }
            var prompt = session.PromptFor("P2"); Export("prompt.json",prompt); Export("snapshot.json",ResolutionResult.BuildSnapshots(state)["P2"]);
            var p=state.PendingRuleChoice!.Request;
            var selected = step==1 ? p.Options.Single(o=>o.Id.StartsWith("RESOURCE:")).Id : accept ? $"SETT:D2:LEGEND:{(sigil?"TRAIT:red":"ANY")}" : "DECLINE";
            GameCommand command = new PayCostCommand(p.Id,"RULE_REPLACEMENT",[selected]);
            var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
            var path=dir is null?null:Path.Combine(dir,"command.json"); var usedNative=path is not null&&File.Exists(path);
            if(usedNative) { using var d=JsonDocument.Parse(File.ReadAllText(path!)); raw=d.RootElement.Clone(); command=GameCommandJsonMapper.Map(raw);
                Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString()); Assert.Equal(state.Tick,raw.GetProperty("snapshotTick").GetInt64()); nativeCount++; }
            var result=await session.SubmitAsync("P2",branch,command,raw,default); Assert.True(result.Accepted,result.ErrorMessage);
            OfficialGraveyardRecastTests.Restore(result.State); state=result.State;
            if(step==1) { Assert.NotNull(state.PendingRuleChoice); Assert.Single(state.PendingRuleChoice.Answers);
                var restoredSession=new MatchSession(OfficialInsightAndSpellLockTests.Restore(state),engine,NoopMatchJournal.Instance);
                Assert.Equal(MatchStateHasher.HashValue(session.PromptFor("P2")),MatchStateHasher.HashValue(restoredSession.PromptFor("P2"))); }
            else { Assert.Null(state.PendingRuleChoice); Assert.Single(result.Events,e=>e.Kind=="ABILITY_ACTIVATED");
                Assert.Equal(accept,state.PlayerZones["P2"].Base.Contains("D2")); Assert.Equal(accept?0:1,state.RunePools["P2"].TotalPower); }
            var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
            var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
            var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,state,engine,default,events);
            Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("replay.json",new{usedNative,nativeCount,replay});
        }
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
