using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class MovementThresholdNativeTests
{
    [Fact]
    public async Task ProductionImmuneTargetAssignmentRestoresAndReplays()
    {
        var initial=(await MovementThresholdTests.ImmuneCombat()).State;
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var directory=Environment.GetEnvironmentVariable("RIFTBOUND_MOVEMENT_THRESHOLD_EVIDENCE");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web) {WriteIndented=true};var native=0;
        void Export(string name,object value) {if(directory is null)return;Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        var prompt=session.PromptFor("P1");Export("assignment-prompt.json",prompt);Export("assignment-snapshot.json",ResolutionResult.BuildSnapshots(initial)["P1"]);
        GameCommand command=new AssignCombatDamageCommand(initial.BattleState.BattleId!,"BF",[new("A","OTHER",5)]);
        var path=directory is null?null:Path.Combine(directory,"assignment-command.json");
        if(path is not null && File.Exists(path)) {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));Assert.Equal(prompt.PromptId,doc.RootElement.GetProperty("promptId").GetString());Assert.Equal(initial.Tick,doc.RootElement.GetProperty("snapshotTick").GetInt64());
            command=GameCommandJsonMapper.Map(doc.RootElement);native++;
        }
        async Task<ResolutionResult> Submit(string player,GameCommand cmd) {
            var result=await session.SubmitAsync(player,"native-"+journal.Entries.Count,cmd,JsonSerializer.SerializeToElement(cmd,cmd.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);Restore(result.State);return result;
        }
        var result=await Submit("P1",command);Assert.Equal(0,result.State.CardObjects["D"].Damage);
        for(var i=0;i<12 && result.State.BattleState.IsActive;i++) {
            var p=result.Prompts.Values.First(p=>p.Actionable);GameCommand cmd=p.Actions.Contains(CommandTypes.AssignCombatDamage)
                ?new AssignCombatDamageCommand(result.State.BattleState.BattleId!,"BF",CombatTestDriver.Assignments(p)):new PassPriorityCommand();
            result=await Submit(p.PlayerId,cmd);
        }
        Assert.False(result.State.BattleState.IsActive);Assert.Contains("D",result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("OTHER",result.State.PlayerZones["P2"].Graveyard);Assert.Equal(0,result.State.CardObjects["D"].Damage);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("result-snapshot.json",result.Snapshots["P1"]);Export("replay.json",new {nativeCommands=native,commands=commands.Length,replay});
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken cancellationToken) {Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
