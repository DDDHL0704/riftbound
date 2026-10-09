using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

public sealed class MechanicalRecastContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCostChoiceAndPlayRestoreAndReplay(bool decline)
    {
        var initial=OfficialMechanicalRecastTests.Position();var journal=new Journal();
        var session=new MatchSession(initial,new CoreRuleEngine(),journal);var root=Environment.GetEnvironmentVariable("RIFTBOUND_MECHANICAL_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Export(string stage,string name,object value)
        {if(root is null)return;var dir=Path.Combine(root,stage);Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        async Task<ResolutionResult> Submit(string player,GameCommand command,JsonElement? raw=null)
        {
            var result=await session.SubmitAsync(player,$"mechanical-{journal.Entries.Count}",command,
                raw??JsonSerializer.SerializeToElement(command,command.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);return result;
        }
        var result=await Submit("P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[]));
        result=await Submit("P1",new PassFocusCommand());result=await Submit("P2",new PassFocusCommand());
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        var usedNative=new List<bool>();
        async Task<ResolutionResult> Native(string stage,GameCommand fallback)
        {
            var prompt=session.PromptFor("P1");Export(stage,"prompt.json",prompt);
            if(fallback is PlayCardCommand play)
            {
                Export(stage,"candidate.json",Assert.Single(prompt.Candidates!,c=>c.Action==CommandTypes.PlayCard));
                var quote=new CoreRuleEngine().PreviewPlayCard(result.State,"P1",new("native-mechanical",prompt.PromptId!,result.State.Tick,play));
                Assert.True(quote.CanPay,quote.Message);Assert.Equal(1,quote.Cost!.Mana);Assert.Equal(1,quote.Cost.PowerByTrait.Values.Sum());Export(stage,"quote.json",quote);
            }
            var path=root is null?null:Path.Combine(root,stage,"native-command.json");JsonElement? raw=null;
            if(path is not null && File.Exists(path))
            {using var doc=JsonDocument.Parse(File.ReadAllText(path));raw=doc.RootElement.Clone();fallback=GameCommandJsonMapper.Map(raw.Value);
                Assert.Equal(prompt.PromptId,raw.Value.GetProperty("promptId").GetString());Assert.Equal(result.State.Tick,raw.Value.GetProperty("snapshotTick").GetInt64());}
            usedNative.Add(raw.HasValue);return await Submit("P1",fallback,raw);
        }
        var choice=result.State.PendingCardChoice!;
        result=await Native(decline?"decline":"choose-unit",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,decline?[]:["B"]));
        if(!decline)result=await Native("play-mech",new PlayCardCommand("MECH2","SFD·075/221",[]));
        Assert.Empty(result.State.StackItems);Assert.Null(result.State.PendingEffectPlay);
        Assert.Contains(decline?"B":"MECH2",result.State.PlayerZones["P1"].Base);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,new CoreRuleEngine(),default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export(decline?"decline":"play-mech","replay.json",new{nativeCommandsUsed=usedNative.All(x=>x),replay});
    }
    private sealed class Journal:IMatchJournal
    {public List<MatchJournalEntry> Entries{get;}=[];public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}}
}
