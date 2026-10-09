using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialUnitEntryTriggerTests
{
    [Theory]
    [InlineData("UNL-150/219")]
    [InlineData("UNL-150a/219")]
    public async Task OrdinaryEntryWaitsForResponseThenRestrictsOnlyTheEnteringPlayer(string source)
    {
        var entered=await Act(Position(source),"P1",new PlayCardCommand("C","OGN·219/298",[]));
        Assert.DoesNotContain("STUNNED",entered.State.CardObjects["C"].UntilEndOfTurnEffects);
        Assert.Equal("P2",entered.State.StackItems.Single().ControllerId); Restore(entered.State);
        if (source == "UNL-150/219") OfficialReflexiveCopyTests.Evidence("entry", entered);
        var done=await Top(entered.State); var unit=done.State.CardObjects["C"];
        Assert.Contains("STUNNED",unit.UntilEndOfTurnEffects);
        Assert.False(MovementRestrictionRules.CanMove(unit,"P1")); Assert.True(MovementRestrictionRules.CanMove(unit,"P2")); Restore(done.State);
        var ready=done.State with { CardObjects=new Dictionary<string,CardObjectState>(done.State.CardObjects){["C"]=unit with {IsExhausted=false}} };
        foreach(var command in new[]{new MoveUnitCommand("C","BASE","BATTLEFIELD:BF",[]),new MoveUnitCommand("C","BASE","BATTLEFIELD:BF",[],["C"])})
        {
            var rejected=await new CoreRuleEngine().ResolveAsync(ready,new("blocked","P1",command.CmdType),command,default);
            Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(ready),MatchStateHasher.Hash(rejected.State));
        }
        var prompts=ResolutionResult.BuildPrompts(ready)["P1"];
        Assert.DoesNotContain(prompts.Candidates!.Where(c=>c.Action==CommandTypes.MoveUnit).SelectMany(c=>c.Sources??[]),c=>c.Id=="C");
        var next=await Act(ready,"P1",new EndTurnCommand());
        Assert.True(MovementRestrictionRules.CanMove(next.State.CardObjects["C"],"P1")); Restore(next.State);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("hidden")]
    [InlineData("friendly")]
    public async Task SourcesOutsideRequiredPublicBattlefieldDoNotTrigger(string mode)
    {
        var s=Position();
        if(mode=="hidden")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["VEX"]=s.CardObjects["VEX"] with {IsFaceDown=true}}};
        if(mode=="base")s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {Base=["ALLY","VEX"],Battlefields=["BF2"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["VEX"]=new("P2","BASE")}};
        if(mode=="friendly")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["VEX"]=s.CardObjects["VEX"] with {ControllerId="P1"}},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Battlefields=["BF","VEX"]},["P2"]=s.PlayerZones["P2"] with {Battlefields=["BF2"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["VEX"]=new("P1","BATTLEFIELD","BF")}};
        var result=await Act(s,"P1",new PlayCardCommand("C","OGN·219/298",[]));
        Assert.Empty(result.State.StackItems);Assert.Empty(result.State.TriggerQueue);Assert.DoesNotContain("STUNNED",result.State.CardObjects["C"].UntilEndOfTurnEffects);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceLeavingAfterTriggerDoesNotCancelStun(bool copiedSource)
    {
        var s=Position();
        if(copiedSource)s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["VEX"]=s.CardObjects["VEX"] with {TokenFactoryCardNo=P6TokenFactoryCatalog.ImageTokenCardNo} } };s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["SPELL"]=s.CardObjects["SPELL"] with {CardNo="OGN·104/298"}}};
        var entered=await Act(s,"P1",new PlayCardCommand("C","OGN·219/298",[]));
        var bounced=await Act(entered.State,"P2",new PlayCardCommand("SPELL","OGN·104/298",["VEX"]));
        var sourceGone=await Top(bounced.State);Assert.Equal(!copiedSource,sourceGone.State.PlayerZones["P2"].Hand.Contains("VEX"));
        Assert.Equal(!copiedSource,sourceGone.State.CardObjects.ContainsKey("VEX"));Restore(sourceGone.State);
        var done=await Top(sourceGone.State);Assert.Contains("STUNNED",done.State.CardObjects["C"].UntilEndOfTurnEffects);Restore(done.State);
    }

    [Fact]
    public async Task CopyingEnemyVexDoesNotRemoveTheStunFromZeroImage()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["C"]=new("C",cardNo:"UNL-200/219",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])}};
        var cast=await Act(s,"P1",new PlayCardCommand("C","UNL-200/219",["VEX"]));
        var created=await Top(cast.State);Assert.Equal(2,created.State.TriggerQueue.Count);Restore(created.State);
        var ordered=await Act(created.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:created.State.TriggerQueue.Select(t=>t.TriggerId).Reverse().ToArray()));
        var first=await Top(ordered.State);var done=await Top(first.State);
        var image=done.State.CardObjects["C-TOKEN-001"];
        Assert.Equal("UNL-150/219",image.CardNo);Assert.Equal(4,image.Power);
        Assert.Contains("STUNNED",image.UntilEndOfTurnEffects);Assert.False(MovementRestrictionRules.CanMove(image,"P1"));Restore(done.State);
    }

    [Theory]
    [InlineData("P1", false)]
    [InlineData("P2", true)]
    public async Task MovementSpellChecksTheEffectControllerInsteadOfTheUnitController(string actor, bool moves)
    {
        var entered=await Act(Position(),"P1",new PlayCardCommand("C","OGN·219/298",[]));
        var finished=await Top(entered.State);var s=finished.State;
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["ATT"]=new("ATT",cardNo:"OGN·219/298",ownerId:"P1",controllerId:"P1",power:4,tags:[CardObjectTags.UnitCard]),
            ["MOVE"]=new("MOVE",cardNo:"OGN·168/298",ownerId:actor,controllerId:actor,tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){
                ["P1"]=s.PlayerZones["P1"] with {Base=["ATT"],Battlefields=["BF","C"],Hand=s.PlayerZones["P1"].Hand.Concat(actor=="P1"?["MOVE"]:[]).ToArray()},
                ["P2"]=s.PlayerZones["P2"] with {Hand=s.PlayerZones["P2"].Hand.Concat(actor=="P2"?["MOVE"]:[]).ToArray()}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){
                ["C"]=new("P1","BATTLEFIELD","BF"),["ATT"]=new("P1","BASE"),["MOVE"]=new(actor,"HAND")}};
        var duel=await Act(s,"P1",new MoveUnitCommand("ATT","BASE","BATTLEFIELD:BF2",[]));
        if(actor=="P2")duel=await Act(duel.State,"P1",new PassFocusCommand());
        var cast=await Act(duel.State,actor,new PlayCardCommand("MOVE","OGN·168/298",["C"]));
        var done=await Top(cast.State);
        Assert.Equal(moves,done.State.PlayerZones["P1"].Base.Contains("C"));
        Assert.Equal(!moves,done.State.PlayerZones["P1"].Battlefields.Contains("C"));Restore(done.State);
    }

    [Fact]
    public async Task RecoveryRejectsMissingOrForgedEntryContexts()
    {
        var s=(await Act(Position(),"P1",new PlayCardCommand("C","OGN·219/298",[]))).State;
        var item=s.StackItems.Single();var c=item.UnitEntryContext!;
        foreach(var invalid in new[]{item with {UnitEntryContext=null},item with {EffectKind="DRAW_ONE"},
            item with {UnitEntryContext=c with {EnteringPlayerId="P2"}},
            item with {UnitEntryContext=c with {EnteringPlayerId="UNKNOWN"}},
            item with {UnitEntryContext=c with {Entered=new("C",-1)}},
            item with {UnitEntryContext=c with {CardNo="OGN·219/298"}}})
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {StackItems=[invalid]}),e=>e.Contains("unit entry"));
    }

    [Fact]
    public async Task CopyAndEntryTriggerCommandsRestoreAndReplayTogether()
    {
        var initial=Position();initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["C"]=new("C",cardNo:"UNL-200/219",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])} };
        var journal=new Journal();var engine=new CoreRuleEngine();var session=new MatchSession(initial,engine,journal);
        var json=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var result=await session.SubmitAsync(player,"copy-entry-"+journal.Entries.Count,command,
                System.Text.Json.JsonSerializer.SerializeToElement(command,command.GetType(),json),default);
            Assert.True(result.Accepted,result.ErrorMessage);Restore(result.State);return result;
        }
        var result=await Submit("P1",new PlayCardCommand("C","UNL-200/219",["VEX"]));
        for(var i=0;i<2;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        Assert.Equal(2,result.State.TriggerQueue.Count);
        result=await Submit("P1",new OrderTriggersCommand(OrderedTriggerIds:result.State.TriggerQueue.Select(t=>t.TriggerId).Reverse().ToArray()));
        for(var i=0;i<4;i++)result=await Submit(result.State.PriorityPlayerId!,new PassPriorityCommand());
        Assert.Empty(result.State.StackItems);
        Assert.Equal("UNL-150/219",result.State.CardObjects["C-TOKEN-001"].CardNo);
        Assert.False(MovementRestrictionRules.CanMove(result.State.CardObjects["C-TOKEN-001"],"P1"));
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; }=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token)
        { Entries.Add(entry);return ValueTask.CompletedTask; }
    }

    internal static MatchState Position(string source="UNL-150/219")
    {
        var s=OfficialRevealedHandPlayTests.Position();
        return s with {RunePools=new Dictionary<string,RunePool>{{"P1",new(20,20)},{"P2",new(20,20)}},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
                ["C"]=new("C",cardNo:"OGN·219/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]),
                ["VEX"]=new("VEX",cardNo:source,power:4,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {Battlefields=["BF2","VEX"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["VEX"]=new("P2","BATTLEFIELD","BF2")}};
    }
}
