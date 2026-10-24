using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class EchoSingleDestinationNativeTests
{
    [Theory]
    [InlineData("rage")] [InlineData("tentacles")]
    public async Task ProductionEchoDestinationComposerRestoresAndReplays(string branch)
    {
        var initial=EchoSingleDestinationTests.Position();var command=EchoSingleDestinationTests.Command();
        if(branch=="tentacles") {
            command=command with {CardNo="UNL-054/219"};
            initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {["AOE"]=initial.CardObjects["AOE"] with {CardNo=command.CardNo}}};
        }
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_ECHO_DESTINATION_EVIDENCE");var dir=root is null?null:Path.Combine(root,branch);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};var native=0;
        void Export(string name,object value){if(dir is null)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        async Task<ResolutionResult> Submit(string player,GameCommand cmd){
            var result=await session.SubmitAsync(player,branch+journal.Entries.Count,cmd,JsonSerializer.SerializeToElement(cmd,cmd.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);Restore(result.State);return result;
        }
        var prompt=session.PromptFor("P1");Export("play-prompt.json",prompt);Export("play-snapshot.json",ResolutionResult.BuildSnapshots(initial)["P1"]);
        var requestPath=dir is null?null:Path.Combine(dir,"play-preview.json");
        var request=requestPath is not null&&File.Exists(requestPath)?JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(requestPath),json)!:new("play",prompt.PromptId!,initial.Tick,command);
        Assert.Equal("BASE:P2",request.Command.Destination);Assert.Equal("BATTLEFIELD:BF2",Assert.Single(request.Command.RepeatChoices!).Destination);
        var quote=engine.PreviewPlayCard(initial,"P1",request);Assert.True(quote.IsValid&&quote.CanPay,quote.Message);Export("play-quote.json",quote);
        GameCommand play=command;var path=dir is null?null:Path.Combine(dir,"play-command.json");
        if(path is not null&&File.Exists(path)) {using var document=JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(prompt.PromptId,document.RootElement.GetProperty("promptId").GetString());Assert.Equal(initial.Tick,document.RootElement.GetProperty("snapshotTick").GetInt64());
            play=GameCommandJsonMapper.Map(document.RootElement);native++;}
        var result=await Submit("P1",play);
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        Assert.Contains("D",result.State.PlayerZones["P2"].Base);Assert.Equal("BF2",result.State.ObjectLocations["D2"].BattlefieldObjectId);
        if(branch=="rage") {
            result=await Submit("P1",new OrderTriggersCommand(OrderedTriggerIds:result.State.TriggerQueue.Select(t=>t.TriggerId).ToArray()));
            for(var i=0;i<2;i++) {var choice=result.State.PendingCardChoice!;var target=choice.ContextObjectIds[0]=="D"?"E":"E2";
                result=await Submit("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[target]));}
            for(var i=0;i<4;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
            Assert.Equal(3,result.State.CardObjects["E"].Damage);Assert.Equal(2,result.State.CardObjects["E2"].Damage);
        }
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("result-snapshot.json",result.Snapshots["P1"]);Export("replay.json",new{nativeCommands=native,commands=commands.Length,replay});
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries{get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken cancellationToken){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
