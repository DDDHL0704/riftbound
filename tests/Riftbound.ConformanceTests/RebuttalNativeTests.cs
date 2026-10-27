using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RebuttalNativeTests
{
    [Theory]
    [InlineData("decline")]
    [InlineData("retain")]
    [InlineData("target")]
    [InlineData("mode")]
    [InlineData("partial")]
    [InlineData("resource")]
    [InlineData("rune")]
    public async Task ProductionControlsCompleteResolutionPaymentAndRemakesAfterColdRecovery(string branch)
    {
        var spell=branch=="mode" ? "UNL-182/219" : branch=="partial" ? "UNL-202/219" : "OGN·095/298";
        var initial=RebuttalRulesTests.Position(spell);
        if(branch is "resource" or "rune") {
            initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools) {["P2"]=new(30,1)}};
            initial=branch=="resource" ? ReplacementResourceTests.AddResource(initial,P4ActivatedAbilityCatalog.PowerSigilCardNo,"R-RESOURCE")
                : LocalDestructionRecallTests.At(initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {
                    ["R-RUNE"]=new("R-RUNE",cardNo:"OGN·007/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.RuneCard,"COLOR:red"]) }},"R-RUNE","P2","BASE");
        }
        Restore(initial);ArmamentMightTests.PublicRecovery(initial);
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_REBUTTAL_EVIDENCE");
        var dir=root is null ? null : Path.Combine(root,branch);
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web) {WriteIndented=true,DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull};
        var engine=new CoreRuleEngine();var journal=new Journal();var session=new MatchSession(initial,engine,journal);var current=initial;
        var steps=new List<object>();var native=0;var coldRestores=0;var staleChecked=false;
        void Export(string name,object value) {
            if(dir is null)return;Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,value.GetType(),options));
        }
        async Task Submit(string name,string actor,GameCommand fallback) {
            var prompt=session.PromptFor(actor);
            Export(name+"-prompt.json",prompt);Export(name+"-snapshot.json",ResolutionResult.BuildSnapshots(current)[actor]);Export(name+"-selection.json",fallback);
            var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(fallback,fallback.GetType(),options),options)!;
            fields["promptId"]=JsonSerializer.SerializeToElement(prompt.PromptId);fields["snapshotTick"]=JsonSerializer.SerializeToElement(current.Tick);
            var raw=JsonSerializer.SerializeToElement(fields);var command=fallback;
            var path=dir is null ? null : Path.Combine(dir,name+"-command.json");
            if(path is not null && File.Exists(path)) {using var doc=JsonDocument.Parse(File.ReadAllText(path));raw=doc.RootElement.Clone();command=GameCommandJsonMapper.Map(raw);native++;}
            if(fallback is PlayCardCommand play)Export(name+"-quote.json",engine.PreviewPlayCard(current,actor,new("quote",prompt.PromptId!,current.Tick,play)));
            steps.Add(new {name,action=fallback.CmdType});
            var done=await session.SubmitAsync(actor,name,command,raw,default);Assert.True(done.Accepted,done.ErrorMessage);
            var entries=journal.Entries.Count;Assert.True((await session.SubmitAsync(actor,name,command,raw,default)).Accepted);Assert.Equal(entries,journal.Entries.Count);
            current=done.State;Restore(current);ArmamentMightTests.PublicRecovery(current);
            if(current.PendingRuleChoice is not null) {
                current=OfficialInsightAndSpellLockTests.Restore(current);
                var commands=Commands(journal);var events=Events(journal);var sequence=journal.Entries[^1].CompletedEventSequence;
                var snapshots=ResolutionResult.BuildSnapshots(current);var prompts=ResolutionResult.BuildPrompts(current);
                var views=current.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,current.Tick,sequence,snapshots[id],current.Tick,sequence,prompts[id]));
                var spectator=MatchReplayRedactor.BuildSpectatorFrame(current.RoomId,current.Tick,sequence,[],current);
                var errors=MatchRecoveryValidator.Validate(current.RoomId,sequence,commands,events,views,current,current.Tick,spectator);Assert.Empty(errors);
                session=MatchSession.Restore(new(current.RoomId,current.Tick,sequence,commands,events,views,errors,current,initial,spectator),new CoreRuleEngine(),journal);
                coldRestores++;entries=journal.Entries.Count;
                Assert.True((await session.SubmitAsync(actor,name,command,raw,default)).Accepted);Assert.Equal(entries,journal.Entries.Count);
                if(!staleChecked) {
                    var stale=await session.SubmitAsync(actor,"stale",command,raw,default);Assert.False(stale.Accepted);
                    Assert.Equal(MatchStateHasher.Hash(current),MatchStateHasher.Hash(stale.State));staleChecked=true;
                }
            }
        }
        async Task Passes(string prefix) {await Submit(prefix+"-1",current.PriorityPlayerId!,new PassPriorityCommand());await Submit(prefix+"-2",current.PriorityPlayerId!,new PassPriorityCommand());}
        async Task Choice(string name,string id) {var p=current.PendingRuleChoice!;await Submit(name,p.Request.PlayerId,new PayCostCommand(p.Request.Id,"RULE_REPLACEMENT",[id]));}
        await Submit("spell","P1",new PlayCardCommand("R-SPELL",spell,branch=="mode" ? [] : branch=="partial" ? ["R-OTHER","R-TARGET"] : ["R-TARGET"],
            branch=="mode" ? "DRAW_1" : "",MovementDestinations:branch=="partial" ? ["BATTLEFIELD:F","BATTLEFIELD:F"] : null));
        await Submit("offer-response","P1",new PassPriorityCommand());
        await Submit("rebuttal","P2",new PlayCardCommand("REBUTTAL",RebuttalRulesTests.Card,[current.StackItems.Single().StackItemId]));
        await Passes("rebuttal-pass");
        if(branch=="resource")await Choice("resource",Assert.Single(current.PendingRuleChoice!.Request.Options,o=>o.Id.StartsWith("RESOURCE:R-RESOURCE:")).Id);
        await Choice("payment",branch=="decline" ? "DECLINE" : branch=="resource"
            ? Assert.Single(current.PendingRuleChoice!.Request.Options,o=>o.Id.StartsWith("PAY:")).Id
            : branch=="rune" ? "PAY:RUNE:R-RUNE" : "PAY:ANY");
        if(branch!="decline") {
            if(branch=="mode") {
                await Choice("mode",RebuttalRulesTests.Option(current,"模式 BASE_UNIT_DAMAGE_3"));
                await Choice("new-target",RebuttalRulesTests.Option(current,"添加目标 1 为","（R-OTHER）"));
            } else if(branch=="target")await Choice("new-target",RebuttalRulesTests.Option(current,"重选目标 1 为","（R-OTHER）"));
            else if(branch=="partial") {
                await Choice("first-target",RebuttalRulesTests.Option(current,"重选目标 1 为","（R-TARGET）"));
                Assert.DoesNotContain(current.PendingRuleChoice!.Request.Options,o=>o.Id=="CONFIRM");
                await Choice("second-target",RebuttalRulesTests.Option(current,"重选目标 2 为","（R-OTHER）"));
            }
            await Choice("confirm",branch is "target" or "mode" or "partial" ? "CONFIRM" : "KEEP_ALL");
            Assert.Equal("P2",Assert.Single(current.StackItems).ControllerId);await Passes("spell-pass");
        }
        Assert.Empty(current.StackItems);Assert.Contains("R-SPELL",current.PlayerZones["P1"].Graveyard);
        if(branch=="mode")Assert.Equal(3,current.CardObjects["R-OTHER"].Damage);
        else if(branch=="target")Assert.Equal(8,current.CardObjects["R-OTHER"].Power);
        else if(branch=="partial")Assert.Equal("BATTLEFIELD",current.ObjectLocations["R-TARGET"].Zone);
        else Assert.Equal(branch=="decline" ? 9 : 8,current.CardObjects["R-TARGET"].Power);
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,Commands(journal),current,engine,default,Events(journal));
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));Assert.Single(journal.Entries,e=>!e.Accepted);
        if(native>0)Assert.Equal(steps.Count,native);Assert.True(coldRestores>0);
        Export("steps.json",steps);Export("replay.json",new {nativeCommands=native,accepted=steps.Count,staleRejected=1,coldRestores,dedupAfterColdRestore=true,replay});
    }
    private static RecoveredCommand[] Commands(Journal j)=>j.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
    private static RecoveredEvent[] Events(Journal j)=>j.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
    private sealed class Journal:IMatchJournal {public List<MatchJournalEntry> Entries {get;}=[];public ValueTask RecordAsync(MatchJournalEntry e,CancellationToken token) {Entries.Add(e);return ValueTask.CompletedTask;}}
}
