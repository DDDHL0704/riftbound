using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class DuskRoseLabNativeTests
{
    [Theory]
    [InlineData("pay")] [InlineData("decline")]
    public async Task ProductionDestructionCostChoiceRecoversAndReplays(string branch)
    {
        var initial=DuskRoseLabStartTests.Position();var engine=new CoreRuleEngine();var journal=new Journal();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_DUSK_EVIDENCE");var directory=root is null?null:Path.Combine(root,branch);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};var native=0;
        void Export(string name,object value) {if(directory is null)return;Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        async Task<ResolutionResult> Submit(string player,GameCommand command) {
            var result=await session.SubmitAsync(player,branch+journal.Entries.Count,command,JsonSerializer.SerializeToElement(command,command.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);Restore(result.State);return result;
        }
        var opened=await Submit("P1",new PassPriorityCommand());var prompt=session.PromptFor("P1");
        Export("cost-prompt.json",prompt);Export("cost-snapshot.json",opened.Snapshots["P1"]);
        var c=opened.State.PendingCardChoice!;GameCommand command=new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,branch=="pay"?["B"]:[]);
        var path=directory is null?null:Path.Combine(directory,"cost-command.json");
        if(path is not null&&File.Exists(path)) {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));Assert.Equal(prompt.PromptId,doc.RootElement.GetProperty("promptId").GetString());
            Assert.Equal(opened.State.Tick,doc.RootElement.GetProperty("snapshotTick").GetInt64());command=GameCommandJsonMapper.Map(doc.RootElement);native++;
        }
        var result=await Submit("P1",command);
        if(branch=="pay") {
            Assert.Contains("B",result.State.PlayerZones["P1"].Graveyard);Assert.Empty(result.State.PlayerZones["P1"].Hand);Export("paid-snapshot.json",result.Snapshots["P1"]);
            for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        }
        Assert.Equal(branch=="pay"?2:1,result.State.PlayerZones["P1"].Hand.Count);
        Assert.Contains("A",result.State.PlayerZones["P1"].Battlefields);Assert.Equal(branch=="pay",result.State.PlayerZones["P1"].Graveyard.Contains("B"));
        Assert.Equal(MatchPhases.Main,result.State.Phase);Assert.Empty(result.State.StackItems);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("replay.json",new{nativeCommands=native,commands=commands.Length,replay});Export("result-snapshot.json",result.Snapshots["P1"]);
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
