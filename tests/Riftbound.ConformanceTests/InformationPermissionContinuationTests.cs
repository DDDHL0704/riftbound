using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

public sealed class InformationPermissionContinuationTests
{
    [Fact]
    public async Task NativeAcknowledgeAndTurnExpiryRestoreAndReplay()
    {
        const string branch="hand-scout-continue";
        var initial=OfficialInformationPermissionTests.Position();var engine=new CoreRuleEngine();var journal=new Journal();
        var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_INFORMATION_EVIDENCE");
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
        Export("before","snapshot.json",ResolutionResult.BuildSnapshots(initial)["P1"]);
        var result=await Submit("P1",new ActivateAbilityCommand("MALZ",P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,["V"]));
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        async Task<ResolutionResult> Native(string stage,string player,GameCommand command)
        {
            var prompt=session.PromptFor(player);Export(stage,"prompt.json",prompt);
            Export(stage,"snapshot.json",ResolutionResult.BuildSnapshots(result.State)[player]);
            JsonElement? native=null;var path=root is null?null:Path.Combine(root,stage,"native-command.json");
            if(path is not null && File.Exists(path))
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(path));native=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(native.Value);
                Assert.Equal(prompt.PromptId,native.Value.GetProperty("promptId").GetString());Assert.Equal(result.State.Tick,native.Value.GetProperty("snapshotTick").GetInt64());
            }
            actual.Add(native.HasValue);return await Submit(player,command,native);
        }
        result=await Native(branch,"P1",new ChooseCardsCommand(result.State.PendingCardChoice!.ChoiceId,"REVEALED_HAND_EFFECT",[]));
        Assert.Null(result.State.PendingCardChoice);Assert.Empty(result.State.StackItems);Assert.Single(result.State.FaceDownLookPermissions);
        Assert.Equal(1,result.State.PlayerExperience["P1"]);
        Export("granted","snapshot.json",ResolutionResult.BuildSnapshots(result.State)["P1"]);
        Export("spectator","snapshot.json",ResolutionResult.BuildSpectatorSnapshot(result.State));
        result=await Submit("P1",new EndTurnCommand());
        Assert.Empty(result.State.FaceDownLookPermissions);Export("expired","snapshot.json",ResolutionResult.BuildSnapshots(result.State)["P1"]);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export(branch,"replay.json",new{nativeCommandsUsed=actual.All(x=>x),replay});
    }
    private sealed class Journal:IMatchJournal
    {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
