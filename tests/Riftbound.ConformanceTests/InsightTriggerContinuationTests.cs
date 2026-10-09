using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class InsightTriggerContinuationTests
{
    [Theory]
    [InlineData("library",0)]
    [InlineData("library",1)]
    [InlineData("visionary",0)]
    [InlineData("visionary",1)]
    [InlineData("visionary",2)]
    public async Task NativeInsightChoicesRecoverAndReplay(string scenario,int recycleCount)
    {
        var initial=scenario=="library"?OfficialInsightTriggerTests.Library():OfficialInsightTriggerTests.Visionary();
        var engine=new CoreRuleEngine();var journal=new Journal();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_INSIGHT_TRIGGER_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Export(string name,object value){if(string.IsNullOrEmpty(root))return;Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        async Task<ResolutionResult> Submit(string player,GameCommand command)
        {
            var result=await session.SubmitAsync(player,scenario+journal.Entries.Count,command,JsonSerializer.SerializeToElement(command,command.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialInsightTriggerTests.Restore(result.State);return result;
        }
        GameCommand Native(string name,GameCommand fallback)
        {
            var path=string.IsNullOrEmpty(root)?null:Path.Combine(root,"native-"+name+"-command.json");
            if(path is null || !File.Exists(path))return fallback;
            using var doc=JsonDocument.Parse(File.ReadAllText(path));return GameCommandJsonMapper.Map(doc.RootElement);
        }
        var result=await Submit("P1",scenario=="library"?new PlayCardCommand("C","OGN·083/298",[]):new ActivateAbilityCommand("MALZ",P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,["V"]));
        result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        if(scenario=="library")
        {
            result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
            result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        }
        var choice=result.State.PendingCardChoice!;
        if(recycleCount==1)Export(scenario+"-prompt.json",result.Prompts["P1"]);
        Assert.DoesNotContain("\"D1\"",JsonSerializer.Serialize(result.Snapshots["P2"]));
        var branch=recycleCount switch{0=>"keep",1=>"recycle",_=>"all"};
        result=await Submit("P1",Native(scenario+"-"+branch,new ChooseCardsCommand(choice.ChoiceId,"INSIGHT",choice.LegalObjectIds.Take(recycleCount).ToArray())));
        if(scenario=="visionary" && recycleCount==0)
        {
            Export("visionary-order-prompt.json",result.Prompts["P1"]);
            choice=result.State.PendingCardChoice!;
            Assert.Equal("INSIGHT_ORDER",choice.ChoiceWindow);
            Assert.DoesNotContain("\"D1\"",JsonSerializer.Serialize(result.Prompts["P2"]));
            result=await Submit("P1",Native("visionary-order",new ChooseCardsCommand(choice.ChoiceId,"INSIGHT_ORDER",["D2","D1"])));
            Assert.Equal(["D2","D1","D3"],result.State.PlayerZones["P1"].MainDeck);
        }
        Assert.Null(result.State.PendingCardChoice);

        Assert.Empty(result.State.StackItems);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export(scenario+"-"+branch+"-replay.json",replay);
    }
    private sealed class Journal:IMatchJournal
    {public List<MatchJournalEntry> Entries{get;}=[];public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}}
}
