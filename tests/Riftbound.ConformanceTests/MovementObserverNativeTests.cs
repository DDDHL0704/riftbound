using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;
public sealed class MovementObserverNativeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionMovementOrderRestoresAndReplays(bool reverse)
    {
        var result=await MovementObserverAuditTests.Move(MovementObserverAuditTests.Position(tavern: true), "BASE:P2");
        var initial=OfficialInsightAndSpellLockTests.Restore(result.State);
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var prompt=session.PromptFor("P2");
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_MOVEMENT_EVIDENCE");
        var dir=root is null?null:Path.Combine(root,reverse?"reverse":"forward");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true };
        void Export(string name,object value) { if(dir is null)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json)); }
        Export("prompt.json",prompt);Export("snapshot.json",result.Snapshots["P2"]);
        var ids=initial.TriggerQueue.Select(t=>t.TriggerId).ToArray();if(reverse)Array.Reverse(ids);
        GameCommand command=new OrderTriggersCommand(OrderedTriggerIds:ids);
        var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
        var native=dir is not null && File.Exists(Path.Combine(dir,"command.json"));
        if(native) { using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!,"command.json")));raw=document.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString());Assert.Equal(initial.Tick,raw.GetProperty("snapshotTick").GetInt64()); }
        result=await session.SubmitAsync("P2","order",command,raw,default);Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);
        Assert.Equal(ids.Reverse().Select(id=>"ordered-"+id),result.State.StackItems.Select(item=>item.StackItemId));
        for(var index=0;result.State.StackItems.Count>0 && index<8;index++) {
            var pass=new PassPriorityCommand();result=await session.SubmitAsync(result.State.PriorityPlayerId!,"pass-"+index,pass,JsonSerializer.SerializeToElement(pass,json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);
        }
        Assert.Empty(result.State.StackItems);Assert.Equal(3,result.State.CardObjects["D"].Power);
        var gold=Assert.Single(result.State.PlayerZones["P2"].Base,id=>result.State.CardObjects[id].Tags.Contains("金币"));
        Assert.True(result.State.CardObjects[gold].IsExhausted);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("replay.json",new{native,replay,commands=commands.Length});
    }

    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry);return ValueTask.CompletedTask; }
    }
}
