using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

public sealed class RecastNativeContinuationTests
{
    [Theory]
    [InlineData("pick-second")]
    [InlineData("pay-power")]
    [InlineData("decline")]
    [InlineData("unit-play")]
    [InlineData("unit-decline")]
    public async Task ProductionRecastCommandRestoresAndReplays(string branch)
    {
        var power = branch == "pay-power";
        var unit = branch.StartsWith("unit-");
        var decline = branch.EndsWith("decline");
        var initial = unit ? OfficialUnitRevivalTests.Position("OGN·226/298")
            : OfficialGraveyardRecastTests.Fizz(power ? "UNL-200/219" : "OGN·048/298");
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial,engine,journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_RECAST_EVIDENCE");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) {WriteIndented=true};
        void Export(string name, object value)
        {
            if(root is null)return;var path=Path.Combine(root,branch);Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,name),JsonSerializer.Serialize(value,value.GetType(),json));
        }
        async Task<ResolutionResult> Submit(string player,GameCommand command,JsonElement? raw=null)
        {
            var result=await session.SubmitAsync(player,branch+journal.Entries.Count,command,
                raw??JsonSerializer.SerializeToElement(command,command.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);OfficialGraveyardRecastTests.Restore(result.State);return result;
        }
        var result=await Submit("P1",new PlayCardCommand(unit?"C":"F",unit?"OGN·226/298":"SFD·140/221",unit?["G"]:[]));
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        var pending=result.State.PendingEffectPlay!;Assert.NotNull(pending);
        var prompt=session.PromptFor("P1");Export("prompt.json",prompt);
        GameCommand command=decline ? new ChooseCardsCommand(pending.ChoiceId,"EFFECT_PLAY",[])
            :new PlayCardCommand(unit?"G":power?"C":"OTHER",unit?"OGN·096/298":power?"UNL-200/219":"OGN·048/298",power?["F"]:[]);
        if(command is PlayCardCommand play)
        {
            Export("candidate.json",Assert.Single(prompt.Candidates!,c=>c.Action==CommandTypes.PlayCard));
            var quote=engine.PreviewPlayCard(result.State,"P1",new("native-recast",prompt.PromptId!,result.State.Tick,play));
            Assert.True(quote.CanPay,quote.Message);Assert.Equal(0,quote.Cost!.Mana);
            Assert.Equal(power?2:0,quote.Cost.GenericPower+quote.Cost.PowerByTrait.Values.Sum());Export("quote.json",quote);
        }
        JsonElement? native=null;
        var nativePath=root is null?null:Path.Combine(root,branch,"native-command.json");
        if(nativePath is not null && File.Exists(nativePath))
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(nativePath));native=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(native.Value);
            Assert.Equal(prompt.PromptId,native.Value.GetProperty("promptId").GetString());
            Assert.Equal(result.State.Tick,native.Value.GetProperty("snapshotTick").GetInt64());
        }
        result=await Submit("P1",command,native);
        if(!decline && !unit)for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        Assert.Empty(result.State.StackItems);Assert.Null(result.State.PendingEffectPlay);
        if(unit) Assert.Contains("G", decline ? result.State.PlayerZones["P1"].Graveyard : result.State.PlayerZones["P1"].Base);
        else if(decline)Assert.Equal(2,result.State.PlayerZones["P1"].Graveyard.Count);
        else Assert.Contains(power?"C":"OTHER",result.State.PlayerZones["P1"].MainDeck);
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
