using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GroupedTargetNativeTests
{
    [Theory]
    [InlineData("power")]
    [InlineData("split")]
    public async Task NativeSubsetChoicesRestoreAndReplay(string branch)
    {
        var opened=await GroupedTargetTests.ChangedGroup(branch);
        var initial=OfficialInsightAndSpellLockTests.Restore(opened.State);
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_GROUP_TARGET_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var allNative=true;var steps=new[]{"REMOVE:D","REMOVE:D2","CONFIRM"};var result=opened;
        for(var i=0;i<steps.Length;i++) {
            var dir=root is null?null:Path.Combine(root,branch,i.ToString());
            void Export(string name,object value) {if(dir is null)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));}
            var state=result.State;var prompt=session.PromptFor("P1");Export("prompt.json",prompt);Export("snapshot.json",ResolutionResult.BuildSnapshots(state)["P1"]);Export("selection.json",new{selected=steps[i]});
            GameCommand command=new PayCostCommand(state.PendingRuleChoice!.Request.Id,"RULE_REPLACEMENT",[steps[i]]);
            var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
            var native=dir is not null&&File.Exists(Path.Combine(dir,"command.json"));allNative &=native;
            if(native){using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!,"command.json")));raw=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);
                Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString());Assert.Equal(state.Tick,raw.GetProperty("snapshotTick").GetInt64());}
            Assert.Equal([steps[i]],Assert.IsType<PayCostCommand>(command).PaymentChoiceIds);
            result=await session.SubmitAsync("P1","subset-"+i,command,raw,default);Assert.True(result.Accepted,result.ErrorMessage);
            var duplicate=await session.SubmitAsync("P1","subset-"+i,command,raw,default);Assert.Equal(MatchStateHasher.Hash(result.State),MatchStateHasher.Hash(duplicate.State));
            OfficialGraveyardRecastTests.Restore(result.State);Export("result-snapshot.json",result.Snapshots["P1"]);
        }
        Assert.Null(result.State.PendingRuleChoice);Assert.Equal(new[]{"D3","D4"},result.State.PlayerZones["P2"].Graveyard.Order());
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));
        if(root is not null)File.WriteAllText(Path.Combine(root,branch,"replay.json"),JsonSerializer.Serialize(new{native=allNative,replay,commands=commands.Length},json));
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
