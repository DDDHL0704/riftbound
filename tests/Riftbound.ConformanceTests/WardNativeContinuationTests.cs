using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;
public sealed class WardNativeContinuationTests
{
    [Fact]
    public async Task NativeQuoteAndConfirmationRestoreAndReplayWithGenericWardPower()
    {
        var initial=OfficialWardCostTests.State(new(2,1)); var engine=new CoreRuleEngine();
        var journal=new Journal(); var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_WARD_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Export(string name,object value) {if(root is null)return;Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        var prompt=session.PromptFor("P1"); var candidate=Assert.Single(prompt.Candidates!,c=>c.Action==CommandTypes.PlayCard);
        GameCommand command=new PlayCardCommand("CARD","OGS·003/024",["T"]);
        var quote=engine.PreviewPlayCard(initial,"P1",new("ward-native",prompt.PromptId!,initial.Tick,(PlayCardCommand)command));
        Assert.True(quote.CanPay);Assert.Equal(2,quote.Cost!.Mana);Assert.Equal(1,quote.Cost.GenericPower);
        Export("native-candidate.json",candidate);Export("native-quote.json",quote);
        var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
        if(root is not null && File.Exists(Path.Combine(root,"native-command.json"))) {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"native-command.json")));raw=document.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString());Assert.Equal(initial.Tick,raw.GetProperty("snapshotTick").GetInt64());
        }
        var result=await session.SubmitAsync("P1","ward-play",command,raw,default);Assert.True(result.Accepted,result.ErrorMessage);
        Assert.Equal(new RunePool(0,0),result.State.RunePools["P1"]);OfficialInsightAndSpellLockTests.Restore(result.State);
        for(var i=0;i<2;i++) {var pass=new PassPriorityCommand();result=await session.SubmitAsync(result.State.PriorityPlayerId!,"pass"+i,pass,JsonSerializer.SerializeToElement(pass,json),default);Assert.True(result.Accepted,result.ErrorMessage);OfficialInsightAndSpellLockTests.Restore(result.State);}
        Assert.Equal(2,result.State.CardObjects["T"].Damage);Assert.Empty(result.State.StackItems);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("native-replay.json",replay);
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
