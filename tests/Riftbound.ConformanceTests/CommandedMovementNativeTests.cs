using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;
public sealed class CommandedMovementNativeTests
{
    [Theory]
    [InlineData("D")] [InlineData("D2")]
    public async Task NativeOpponentChoiceRestoresAndReplays(string selected)
    {
        var initial=OfficialInsightAndSpellLockTests.Restore(CommandedMovementTests.Position());
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_COMMANDED_MOVEMENT_EVIDENCE");
        var dir=root is null?null:Path.Combine(root,selected);var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Export(string name,object value) {if(dir is null)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        var playPrompt=session.PromptFor("P1");Export("play-prompt.json",playPrompt);
        var request=new PlayCostPreviewRequestDto("quote",playPrompt.PromptId!,initial.Tick,CommandedMovementTests.Command());
        var nativePlay=dir is not null&&File.Exists(Path.Combine(dir,"play-command.json"));
        if(nativePlay)request=JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!,"play-preview.json")),json)!;
        var quote=engine.PreviewPlayCard(initial,"P1",request);Assert.True(quote.IsValid,quote.Message);Assert.True(quote.CanPay,quote.Message);Export("play-quote.json",quote);
        GameCommand play=request.Command;var playRaw=JsonSerializer.SerializeToElement(play,play.GetType(),json);
        if(nativePlay){using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!,"play-command.json")));playRaw=doc.RootElement.Clone();play=GameCommandJsonMapper.Map(playRaw);
            Assert.Equal(playPrompt.PromptId,playRaw.GetProperty("promptId").GetString());Assert.Equal(initial.Tick,playRaw.GetProperty("snapshotTick").GetInt64());}
        Assert.Equal(CommandedMovementTests.Spell,Assert.IsType<PlayCardCommand>(play).CardNo);
        Assert.Equal(new[]{"A"},Assert.IsType<PlayCardCommand>(play).TargetObjectIds);Assert.Equal("BATTLEFIELD:BF2",Assert.IsType<PlayCardCommand>(play).Destination);
        var opened=await session.SubmitAsync("P1","play",play,playRaw,default);Assert.True(opened.Accepted,opened.ErrorMessage);
        for(var i=0;i<2;i++){var pass=new PassPriorityCommand();opened=await session.SubmitAsync(opened.State.PriorityPlayerId!,"pass-"+i,pass,JsonSerializer.SerializeToElement(pass,json),default);Assert.True(opened.Accepted,opened.ErrorMessage);}
        OfficialGraveyardRecastTests.Restore(opened.State);
        var prompt=session.PromptFor("P2");Export("prompt.json",prompt);Export("snapshot.json",ResolutionResult.BuildSnapshots(opened.State)["P2"]);
        GameCommand command=new PayCostCommand(opened.State.PendingRuleChoice!.Request.Id,"RULE_REPLACEMENT",["MOVE:"+selected]);
        var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
        var native=dir is not null&&File.Exists(Path.Combine(dir,"command.json"));
        if(native){using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!,"command.json")));raw=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString());Assert.Equal(opened.State.Tick,raw.GetProperty("snapshotTick").GetInt64());}
        Assert.Equal(["MOVE:"+selected],Assert.IsType<PayCostCommand>(command).PaymentChoiceIds);
        var result=await session.SubmitAsync("P2","move-choice",command,raw,default);Assert.True(result.Accepted,result.ErrorMessage);
        var duplicate=await session.SubmitAsync("P2","move-choice",command,raw,default);Assert.Equal(MatchStateHasher.Hash(result.State),MatchStateHasher.Hash(duplicate.State));
        Assert.Null(result.State.PendingRuleChoice);Assert.Equal("BF2",result.State.ObjectLocations["A"].BattlefieldObjectId);
        Assert.Equal("BF2",result.State.ObjectLocations[selected].BattlefieldObjectId);OfficialGraveyardRecastTests.Restore(result.State);
        Export("result-snapshot.json",result.Snapshots["P2"]);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));
        Export("replay.json",new{native=native&&nativePlay,replay,commands=commands.Length});
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
