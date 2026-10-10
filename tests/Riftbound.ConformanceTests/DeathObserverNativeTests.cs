using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;

namespace Riftbound.ConformanceTests;

public sealed class DeathObserverNativeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionOrderCommandsRestoreAndReplay(bool reverse)
    {
        var result=await Open(Source(NoReplacement(true),"UNL-129/219"));
        var initial=OfficialInsightAndSpellLockTests.Restore(result.State);
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var prompt=session.PromptFor("P2");
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_DEATH_EVIDENCE");
        var dir=root is null?null:Path.Combine(root,reverse?"reverse":"forward");
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true };
        void Export(string name,object value) { if(dir is null)return;Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),json)); }
        Export("prompt.json",prompt);Export("snapshot.json",result.Snapshots["P2"]);
        var ids=initial.TriggerQueue.Select(t=>t.TriggerId).ToArray();if(reverse)Array.Reverse(ids);
        GameCommand command=new OrderTriggersCommand(OrderedTriggerIds:ids);
        var raw=JsonSerializer.SerializeToElement(command,command.GetType(),json);
        var native=dir is not null && File.Exists(Path.Combine(dir,"command.json"));
        if(native) { using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!,"command.json")));raw=document.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId,raw.GetProperty("promptId").GetString());Assert.Equal(initial.Tick,raw.GetProperty("snapshotTick").GetInt64()); }
        result=await session.SubmitAsync("P2","order",command,raw,default);Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);
        Assert.Equal(ids.Reverse().Select(id=>"ordered-"+id),result.State.StackItems.Select(item=>item.StackItemId));
        for(var index=0;result.State.StackItems.Count>0 && index<8;index++) {
            var pass=new PassPriorityCommand();result=await session.SubmitAsync(result.State.PriorityPlayerId!,"pass-"+index,pass,JsonSerializer.SerializeToElement(pass,json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);
        }
        Assert.Empty(result.State.StackItems);Assert.Equal(2,result.State.PlayerExperience["P2"]);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Export("replay.json",new{native,replay,commands=commands.Length});
    }

    [Theory]
    [InlineData("D")]
    [InlineData("D2")]
    public async Task ProductionFirstDeathChoiceRestoresAndReplays(string victim)
    {
        var opened = await Open(Source(NoReplacement(true), "OGN·118/298"));
        var initial = OfficialInsightAndSpellLockTests.Restore(opened.State);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var prompt = session.PromptFor("P2");
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_DEATH_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, "first-" + victim);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        Export("prompt.json", prompt); Export("snapshot.json", opened.Snapshots["P2"]);
        var request = initial.PendingRuleChoice!.Request;
        var option = request.Options.Single(o => o.ObjectIds![0] == victim);
        GameCommand command = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]);
        var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
        var native = dir is not null && File.Exists(Path.Combine(dir, "command.json"));
        if (native) {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!, "command.json")));
            raw = doc.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString());
            Assert.Equal(initial.Tick, raw.GetProperty("snapshotTick").GetInt64());
        }
        var result = await session.SubmitAsync("P2", "choose-first", command, raw, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(victim, Assert.Single(result.State.StackItems).DeathObserver!.Destroyed.ObjectId);
        OfficialGraveyardRecastTests.Restore(result.State);
        for (var index = 0; result.State.StackItems.Count > 0 && index < 4; index++) {
            var pass = new PassPriorityCommand();
            result = await session.SubmitAsync(result.State.PriorityPlayerId!, "pass-" + index, pass, JsonSerializer.SerializeToElement(pass, json), default);
            Assert.True(result.Accepted, result.ErrorMessage); OfficialGraveyardRecastTests.Restore(result.State);
        }
        Assert.Empty(result.State.StackItems); Assert.Contains("DRAW", result.State.PlayerZones["P2"].Hand);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Export("replay.json", new { native, replay, commands = commands.Length });
    }
    private sealed class Journal:IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry);return ValueTask.CompletedTask; }
    }
}
