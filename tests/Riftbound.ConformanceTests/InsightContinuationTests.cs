using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class InsightContinuationTests
{
    [Theory]
    [InlineData("abandon", false)]
    [InlineData("abandon", true)]
    [InlineData("eclipse", false)]
    [InlineData("eclipse", true)]
    public async Task PrivateChoiceRecoversAndNativeCommandReplays(string scenario, bool recycle)
    {
        var card=scenario=="abandon"?"UNL-131/219":"UNL-063/219";
        var initial=OfficialInsightAndSpellLockTests.Position(card);
        var engine=new CoreRuleEngine();var journal=new Journal();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_INSIGHT_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Export(string name,object value)
        {if(string.IsNullOrEmpty(root))return;Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        async Task<ResolutionResult> Submit(string player,GameCommand command)
        {
            var result=await session.SubmitAsync(player,scenario+journal.Entries.Count,command,JsonSerializer.SerializeToElement(command,command.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialInsightAndSpellLockTests.Restore(result.State);return result;
        }
        var result=await Submit("P1",OfficialInsightAndSpellLockTests.Play(card));
        result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        var choice=result.State.PendingCardChoice!;
        if(recycle)Export(scenario+"-prompt.json",result.Prompts["P1"]);
        GameCommand command=new ChooseCardsCommand(choice.ChoiceId,"INSIGHT",recycle?["D1"]:[]);
        var path=string.IsNullOrEmpty(root)?null:Path.Combine(root,"native-"+scenario+(recycle?"":"-keep")+"-command.json");
        if(path is not null && File.Exists(path))
        {using var document=JsonDocument.Parse(File.ReadAllText(path));command=GameCommandJsonMapper.Map(document.RootElement);}
        result=await Submit("P1",command);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Equal(recycle?"D2":"D1",result.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Contains("C",result.State.PlayerZones["P1"].Graveyard);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export(scenario+(recycle?"-recycle":"-keep")+"-replay.json",replay);
    }
    private sealed class Journal:IMatchJournal
    {public List<MatchJournalEntry> Entries{get;}=[];public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}}
}
