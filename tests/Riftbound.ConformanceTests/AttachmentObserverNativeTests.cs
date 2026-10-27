using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class AttachmentObserverNativeTests
{
    [Fact]
    public async Task ProductionEquipPaymentAndBothResponseWindowsReplayWithoutEarlyDraw()
    {
        var initial=AttachmentObserverTests.Jax();var journal=new Journal();var engine=new CoreRuleEngine();
        var session=new MatchSession(initial,engine,journal);var root=Environment.GetEnvironmentVariable("RIFTBOUND_ATTACHMENT_EVIDENCE");
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true};var native=0;var current=initial;
        void Export(string name,object value) {
            if(root is null)return;Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,name),JsonSerializer.Serialize(value,value.GetType(),options));
        }
        async Task<ResolutionResult> Submit(string name,string player,GameCommand fallback) {
            var state=current;var prompt=session.PromptFor(player);
            Export(name+"-prompt.json",prompt);Export(name+"-snapshot.json",ResolutionResult.BuildSnapshots(state)[player]);
            var path=root is null?null:Path.Combine(root,name+"-command.json");var command=fallback;
            if(path is not null && File.Exists(path)) {
                using var doc=JsonDocument.Parse(File.ReadAllText(path));
                Assert.Equal(prompt.PromptId,doc.RootElement.GetProperty("promptId").GetString());
                Assert.Equal(state.Tick,doc.RootElement.GetProperty("snapshotTick").GetInt64());
                command=GameCommandJsonMapper.Map(doc.RootElement);native++;
            }
            var result=await session.SubmitAsync(player,"attachment-native-"+journal.Entries.Count,command,
                JsonSerializer.SerializeToElement(command,command.GetType(),options),default);
            Assert.True(result.Accepted,result.ErrorMessage);Restore(result.State);PublicRecovery(result.State);current=result.State;return result;
        }
        var result=await Submit("assemble","P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        Assert.Null(result.State.CardObjects["GEAR"].AttachedToObjectId);
        result=await Submit("equip-pass-1","P1",new PassPriorityCommand());
        result=await Submit("equip-pass-2","P2",new PassPriorityCommand());
        Assert.Equal("HOST",result.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Empty(result.State.PlayerZones["P1"].Hand);
        var payment=result.State.PendingPayment!;
        result=await Submit("pay","P1",new PayCostCommand(payment.PaymentId,payment.PaymentWindow,["PAY"]));
        Assert.Null(result.State.PendingPayment);Assert.Empty(result.State.PlayerZones["P1"].Hand);
        result=await Submit("draw-pass-1","P1",new PassPriorityCommand());Assert.Empty(result.State.PlayerZones["P1"].Hand);
        result=await Submit("draw-pass-2","P2",new PassPriorityCommand());Assert.Single(result.State.PlayerZones["P1"].Hand);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));
        Export("result-snapshot.json",result.Snapshots["P1"]);Export("replay.json",new {nativeCommands=native,commands=commands.Length,replay});
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token){Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
