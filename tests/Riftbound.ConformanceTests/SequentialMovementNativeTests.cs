using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;
public sealed class SequentialMovementNativeTests
{
    [Theory]
    [InlineData("base","BATTLEFIELD:BF2","BASE:P2")]
    [InlineData("same","BATTLEFIELD:BF3","BATTLEFIELD:BF3")]
    [InlineData("split","BATTLEFIELD:BF","BATTLEFIELD:BF2")]
    [InlineData("echo","BATTLEFIELD:BF2","BASE:P2")]
    public async Task ProductionMovementDestinationsPreviewSubmitRestoreAndReplay(string branch,string first,string second)
    {
        var setup=branch=="same"?SequentialMovementTests.NeutralPosition():SequentialMovementTests.Position();
        if(branch=="echo") {
            setup=setup with {UntilEndOfTurnEffects=[EchoCostRules.GrantPrefix+"P1"],CardObjects=new Dictionary<string,CardObjectState>(setup.CardObjects) {["A2"]=setup.CardObjects["A"] with {ObjectId="A2"}}};
            setup=LocalDestructionRecallTests.At(setup,"A2","P1","BASE");
        }
        var initial=OfficialInsightAndSpellLockTests.Restore(setup);
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_SEQUENTIAL_MOVEMENT_EVIDENCE");var dir=root is null?null:Path.Combine(root,branch);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Export(string name,object value){if(dir is null)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        var prompt=session.PromptFor("P1");Export("prompt.json",prompt);Export("selection.json",new{destinations=new[]{first,second}});
        var expected=Assert.IsType<PlayCardCommand>(SequentialMovementTests.Command(first,second));
        if(branch=="echo")expected=expected with {OptionalCosts=["ECHO"],RepeatChoices=[new("",["A2","D2"],["BATTLEFIELD:BF","BATTLEFIELD:BF2"])]};
        var request=new PlayCostPreviewRequestDto("quote",prompt.PromptId!,initial.Tick,expected);
        var native=dir is not null&&File.Exists(Path.Combine(dir,"command.json"));
        if(native)request=JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!,"preview.json")),json)!;
        var quote=engine.PreviewPlayCard(initial,"P1",request);Assert.True(quote.IsValid,quote.Message);Assert.True(quote.CanPay,quote.Message);Export("quote.json",quote);
        GameCommand command=expected;var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
        if(native){using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!,"command.json")));raw=document.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString());Assert.Equal(initial.Tick,raw.GetProperty("snapshotTick").GetInt64());}
        var play=Assert.IsType<PlayCardCommand>(command);Assert.Equal(new[]{"A","D"},play.TargetObjectIds);Assert.Equal(new[]{first,second},play.MovementDestinations);Assert.Equal(play.MovementDestinations,request.Command.MovementDestinations);
        var result=await session.SubmitAsync("P1","play",command,raw,default);Assert.True(result.Accepted,result.ErrorMessage);Export("play-snapshot.json",result.Snapshots["P2"]);OfficialGraveyardRecastTests.Restore(result.State);
        for(var i=0;i<2;i++){var pass=new PassPriorityCommand();result=await session.SubmitAsync(result.State.PriorityPlayerId!,"pass-"+i,pass,JsonSerializer.SerializeToElement(pass,json),default);Assert.True(result.Accepted,result.ErrorMessage);}
        Assert.Equal(first[12..],result.State.ObjectLocations["A"].BattlefieldObjectId);Assert.Equal(second.StartsWith("BASE")?"BASE":"BATTLEFIELD",result.State.ObjectLocations["D"].Zone);
        Assert.Equal(second.StartsWith("BASE")?null:second[12..],result.State.ObjectLocations["D"].BattlefieldObjectId);OfficialGraveyardRecastTests.Restore(result.State);Export("result-snapshot.json",result.Snapshots["P2"]);
        if(branch=="echo") {
            Assert.Equal(new[]{"BATTLEFIELD:BF","BATTLEFIELD:BF2"},Assert.Single(play.RepeatChoices!).MovementDestinations);
            Assert.Equal("BF",result.State.ObjectLocations["A2"].BattlefieldObjectId);Assert.Equal("BF2",result.State.ObjectLocations["D2"].BattlefieldObjectId);
        }
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("replay.json",new{native,replay,commands=commands.Length});
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries{get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
