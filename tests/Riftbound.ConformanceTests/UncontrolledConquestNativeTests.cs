using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class UncontrolledConquestNativeTests
{
    [Theory]
    [InlineData("plain")] [InlineData("ward-pay")] [InlineData("ward-decline")]
    public async Task ProductionTargetAndWardChoicesRestoreAndReplayFromInitialMovement(string branch)
    {
        var initial=UncontrolledConquestTests.Position();
        if(branch!="plain") initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools) {["P1"]=new(3,3)},
            CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {["ENEMY"]=initial.CardObjects["ENEMY"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]}}};
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_UNCONTROLLED_CONQUEST_EVIDENCE");var directory=root is null?null:Path.Combine(root,branch);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web) {WriteIndented=true};var native=0;
        void Export(string name,object value) {if(directory is null)return;Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value,value.GetType(),json));}
        async Task<ResolutionResult> Submit(string player,GameCommand cmd) {
            var result=await session.SubmitAsync(player,branch+journal.Entries.Count,cmd,JsonSerializer.SerializeToElement(cmd,cmd.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);Restore(result.State);return result;
        }
        GameCommand Native(string name,ResolutionResult result,GameCommand fallback) {
            var prompt=session.PromptFor("P1");Export(name+"-prompt.json",prompt);Export(name+"-snapshot.json",result.Snapshots["P1"]);
            var path=directory is null?null:Path.Combine(directory,name+"-command.json");if(path is null || !File.Exists(path))return fallback;
            using var doc=JsonDocument.Parse(File.ReadAllText(path));Assert.Equal(prompt.PromptId,doc.RootElement.GetProperty("promptId").GetString());Assert.Equal(result.State.Tick,doc.RootElement.GetProperty("snapshotTick").GetInt64());native++;return GameCommandJsonMapper.Map(doc.RootElement);
        }
        var result=await Submit("P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[]));
        result=await Submit("P1",new PassFocusCommand());result=await Submit("P2",new PassFocusCommand());
        var c=result.State.PendingCardChoice!;result=await Submit("P1",Native("target",result,new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["ENEMY"])));
        Assert.Equal(0,result.State.CardObjects["ENEMY"].Damage);
        if(branch!="plain") {var p=result.State.PendingPayment!;result=await Submit("P1",Native("payment",result,new PayCostCommand(p.PaymentId,p.PaymentWindow,[branch=="ward-pay"?"PAY":"DECLINE"])));}
        if(branch!="ward-decline") for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        Assert.Equal(branch=="ward-decline"?0:5,result.State.CardObjects["ENEMY"].Damage);Assert.Empty(result.State.StackItems);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("result-snapshot.json",result.Snapshots["P1"]);Export("replay.json",new {nativeCommands=native,commands=commands.Length,replay});
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken cancellationToken) {Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
