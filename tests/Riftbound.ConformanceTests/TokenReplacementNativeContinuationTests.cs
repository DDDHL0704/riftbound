using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TokenReplacementNativeContinuationTests
{
    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    public async Task NativeChoiceCommandsRestoreAndReplay(string branch)
    {
        var initial=OfficialTokenReplacementTests.Position(2);
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_TOKEN_REPLACEMENT_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};
        void Export(string name,object value)
        {
            if(root is null)return;var dir=Path.Combine(root,branch);Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json));
        }
        async Task<ResolutionResult> Submit(string player,GameCommand command,JsonElement? raw=null)
        {
            var result=await session.SubmitAsync(player,branch+journal.Entries.Count,command,
                raw??JsonSerializer.SerializeToElement(command,command.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);return result;
        }
        var result=await Submit("P1",new PlayCardCommand("C","UNL-200/219",["TARGET"]));
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        Export("prompt.json",session.PromptFor("P1"));Export("snapshot.json",result.Snapshots["P1"]);
        var pending=result.State.PendingCardChoice!;
        GameCommand command=new ChooseCardsCommand(pending.ChoiceId,pending.ChoiceWindow,branch=="accept"?["Z2"]:[]);
        JsonElement? native=null;var path=root is null?null:Path.Combine(root,branch,"native-command.json");
        if(path is not null && File.Exists(path))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));native=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(native.Value);
            Assert.Equal(session.PromptFor("P1").PromptId,native.Value.GetProperty("promptId").GetString());
            Assert.Equal(result.State.Tick,native.Value.GetProperty("snapshotTick").GetInt64());
        }
        result=await Submit("P1",command,native);
        if(branch=="accept")
        {
            pending=result.State.PendingCardChoice!;Assert.Equal(["Z1"],pending.LegalObjectIds);
            result=await Submit("P1",new ChooseCardsCommand(pending.ChoiceId,pending.ChoiceWindow,["Z1"]));
        }
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        Assert.Equal(branch=="accept"?3:1,OfficialTokenReplacementTests.Tokens(result.State).Length);
        Assert.Empty(result.State.StackItems);Assert.Null(result.State.PendingCardChoice);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("replay.json",new{nativeCommandUsed=native.HasValue,replay});
    }
    private sealed class Journal:IMatchJournal
    {
        public List<MatchJournalEntry> Entries{get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
