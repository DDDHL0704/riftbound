using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

public sealed class RevealedHandContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualNativeSelectionAndOwnerPlayRestoreAndReplay(bool decline)
    {
        var initial=OfficialRevealedHandPlayTests.Position();var engine=new CoreRuleEngine();var journal=new Journal();
        var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_REVEALED_HAND_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        var actual=new List<bool>();
        void Export(string stage,string name,object value)
        {
            if(root is null)return;var dir=Path.Combine(root,stage);Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));
        }
        async Task<ResolutionResult> Submit(string player,GameCommand cmd,JsonElement? raw=null)
        {
            var result=await session.SubmitAsync(player,"hand-play-"+journal.Entries.Count,cmd,raw??JsonSerializer.SerializeToElement(cmd,cmd.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);return result;
        }
        var result=await Submit("P1",new PlayCardCommand("C","UNL-139/219",["BF"]));
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        async Task<ResolutionResult> Native(string stage,string player,GameCommand command)
        {
            var prompt=session.PromptFor(player);Export(stage,"prompt.json",prompt);
            Export(stage,"snapshot.json",ResolutionResult.BuildSnapshots(result.State)[player]);
            if(command is PlayCardCommand play)
            {
                Export(stage,"candidate.json",Assert.Single(prompt.Candidates!,c=>c.Action==CommandTypes.PlayCard));
                var quote=engine.PreviewPlayCard(result.State,player,new("revealed-hand",prompt.PromptId!,result.State.Tick,play));
                Assert.True(quote.CanPay,quote.Message);Assert.Equal(0,quote.Cost!.Mana);Export(stage,"quote.json",quote);
            }
            JsonElement? native=null;var path=root is null?null:Path.Combine(root,stage,"native-command.json");
            if(path is not null && File.Exists(path))
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(path));native=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(native.Value);
                Assert.Equal(prompt.PromptId,native.Value.GetProperty("promptId").GetString());Assert.Equal(result.State.Tick,native.Value.GetProperty("snapshotTick").GetInt64());
            }
            actual.Add(native.HasValue);return await Submit(player,command,native);
        }
        result=await Native(decline?"hand-decline":"hand-pick","P1",new ChooseCardsCommand(result.State.PendingEffectPlay!.ChoiceId,"REVEALED_HAND_PLAY",decline?[]:["U"]));
        if(!decline)result=await Native("hand-play","P2",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF"));
        Assert.Null(result.State.PendingEffectPlay);Assert.Empty(result.State.StackItems);
        if(decline)Export("hand-hidden","snapshot.json",ResolutionResult.BuildSnapshots(result.State)["P1"]);
        if(decline)Assert.Contains("U",result.State.PlayerZones["P2"].Hand);
        else {Assert.Equal("BF",result.State.ObjectLocations["U"].BattlefieldObjectId);Assert.Contains("STUNNED",result.State.CardObjects["U"].UntilEndOfTurnEffects);}
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export(decline?"hand-decline":"hand-play","replay.json",new{nativeCommandsUsed=actual.All(x=>x),replay});
    }
    private sealed class Journal:IMatchJournal
    {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
