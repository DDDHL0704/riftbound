using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class ReflexiveMovementDuelTests
{
    internal const string Spell="OGN·258/298";
    internal static MatchState Position()
    {
        var s=MovementThresholdTests.Position("OGN·096/298");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["AOE"]=s.CardObjects["AOE"] with {CardNo=Spell},
            ["D"]=s.CardObjects["D"] with {Power=3},
            ["E"]=new("E",cardNo:"OGN·096/298",power:20,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard])}};
        return At(s,"E","P2","BASE");
    }
    internal static PlayCardCommand Command(string destination="BASE:P2")=>new("AOE",Spell,["D"],Destination:destination);
    [Theory]
    [InlineData("BASE:P2")] [InlineData("BATTLEFIELD:BF2")]
    public async Task SpellDeclaresOnlyMovedEnemyAndDestination(string destination)
    {
        var s=Position();if(destination.StartsWith("BATTLEFIELD:"))s=At(At(At(s,"ANCHOR","P1","BASE"),"BF2","P2","BATTLEFIELD","BF2"),"E","P2","BATTLEFIELD","BF2");
        var played=await Act(s,"P1",Command(destination));Assert.Equal(new[]{"D"},Assert.Single(played.State.StackItems).TargetObjectIds);
        var moved=await Top(played.State);Assert.Equal(1,moved.State.CardObjects["D"].MovementCountThisTurn);
        var choice=Assert.IsType<PendingCardChoiceState>(moved.State.PendingCardChoice);Assert.Equal(new[]{"E"},choice.LegalObjectIds);
        Assert.Null(moved.State.PriorityPlayerId);Assert.Equal(0,moved.State.CardObjects["E"].Damage);Restore(moved.State);
        var selected=await Act(moved.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["E"]));
        Assert.NotNull(selected.State.PriorityPlayerId);Assert.Equal(0,selected.State.CardObjects["E"].Damage);
        var done=await Top(selected.State);Assert.Equal(3,done.State.CardObjects["E"].Damage);Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);Restore(done.State);
    }
    [Fact]
    public async Task CannotPreselectTheLaterTargetDuringSpellAnnouncement()
    {
        var s=Position();var cmd=Command() with {TargetObjectIds=["D","E"]};
        var bad=await new CoreRuleEngine().ResolveAsync(s,new("bad","P1",cmd.CmdType),cmd,default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(bad.State));
    }

    internal static async Task<ResolutionResult> Open(MatchState? s=null,string destination="BASE:P2")
        => await Top((await Act(s??Position(),"P1",Command(destination))).State);
    internal static Task<ResolutionResult> Choose(MatchState s,string id="E")
        => Act(s,"P1",new ChooseCardsCommand(s.PendingCardChoice!.ChoiceId,s.PendingCardChoice.ChoiceWindow,[id]));
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task WardIsChargedAtTheTwoSeparateTargetConfirmations(bool pay)
    {
        var plain=await Act(Position(),"P1",Command());var s=Position();
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["D"]=s.CardObjects["D"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]},
            ["E"]=s.CardObjects["E"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]}}};
        var played=await Act(s,"P1",Command());Assert.Equal(plain.State.RunePools["P1"].Power-1,played.State.RunePools["P1"].Power);
        var opened=await Top(played.State);var selected=await Choose(opened.State);Restore(selected.State);
        var pending=Assert.IsType<PendingPaymentState>(selected.State.PendingPayment);Assert.Equal(1,pending.PowerCost);Assert.Null(selected.State.PriorityPlayerId);
        var paid=await Act(selected.State,"P1",new PayCostCommand(pending.PaymentId,pending.PaymentWindow,[pay?"PAY":"DECLINE"]));Restore(paid.State);
        Assert.Equal(selected.State.RunePools["P1"].Power-(pay?1:0),paid.State.RunePools["P1"].Power);
        if(pay){var done=await Top(paid.State);Assert.Equal(3,done.State.CardObjects["E"].Damage);Restore(done.State);}
        else {Assert.Empty(paid.State.StackItems);Assert.Equal(0,paid.State.CardObjects["E"].Damage);Assert.Contains("D",paid.State.PlayerZones["P2"].Base);}
    }
    [Theory]
    [InlineData("second-moved")] [InlineData("second-new-object")] [InlineData("second-control")]
    [InlineData("first-new-object")]
    public async Task ResponseInvalidationCannotDamageAnUnrelatedObject(string change)
    {
        var selected=await Choose((await Open()).State);var s=selected.State;
        if(change=="second-moved")s=At(s,"E","P2","BATTLEFIELD","BF");
        else if(change=="second-control")s=At(s,"E","P1","BASE");
        else {var id=change=="first-new-object"?"D":"E";s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {[id]=s.CardObjects[id] with {ObjectGeneration=1,MovementCountThisTurn=0}}};}
        var done=await Top(s);Assert.Equal(0,done.State.CardObjects["D"].Damage);Assert.Equal(0,done.State.CardObjects["E"].Damage);Restore(done.State);
    }
    [Fact]
    public async Task MissingLaterTargetDoesNotUndoTheCompletedMove()
    {
        var s=At(Position(),"E","P2","BATTLEFIELD","BF");var moved=await Open(s);
        Assert.Contains("D",moved.State.PlayerZones["P2"].Base);Assert.Null(moved.State.PendingCardChoice);Assert.Empty(moved.State.StackItems);
        Assert.Contains(moved.Events,e=>e.Kind=="TRIGGER_NOT_CONFIRMED");Restore(moved.State);
    }
    [Fact]
    public async Task KaynsSecondActualMoveMakesTheLaterDuelUnableToDamageHim()
    {
        var s=MovementThresholdTests.Position("OGN·096/298");s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["D"]=s.CardObjects["D"] with {CardNo="OGN·189/298",Power=6}}};
        s=(await MovementThresholdTests.Move(s,1)).State;Assert.Equal(1,s.CardObjects["D"].MovementCountThisTurn);
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["AOE"]=s.CardObjects["AOE"] with {CardNo=Spell},["D2"]=s.CardObjects["D2"] with {Power=20}}};
        var moved=await Open(s,"BATTLEFIELD:BF");Assert.Equal(2,moved.State.CardObjects["D"].MovementCountThisTurn);
        var done=await Top((await Choose(moved.State,"D2")).State);
        Assert.Equal(0,done.State.CardObjects["D"].Damage);Assert.Equal(6,done.State.CardObjects["D2"].Damage);Restore(done.State);
    }
    [Fact]
    public async Task BothPowersAreReadAtResolutionAndDamageHasUnitSources()
    {
        var selected=await Choose((await Open()).State);var s=selected.State;
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["D"]=s.CardObjects["D"] with {Power=8},["E"]=s.CardObjects["E"] with {Power=12}}};
        var done=await Top(s);Assert.Equal(8,done.State.CardObjects["E"].Damage);
        var events=done.Events.Where(e=>e.Kind=="DAMAGE_APPLIED").ToArray();Assert.Equal(2,events.Length);
        Assert.Equal("D",events[0].Payload["damageSourceObjectId"]);Assert.Equal("E",events[1].Payload["damageSourceObjectId"]);
        var death=Assert.Single(done.Events,e=>e.Kind=="UNIT_DESTROYED");Assert.Equal("E",death.Payload["sourceObjectId"]);Assert.Equal("P2",death.Payload["destroyedByPlayerId"]);Restore(done.State);
    }
    [Theory]
    [InlineData("identity")] [InlineData("card")] [InlineData("location")] [InlineData("missing")]
    public async Task RecoveryRejectsForgedEmbeddedContexts(string change)
    {
        var opened=await Open();var s=opened.State;var item=Assert.Single(s.StackItems);var c=item.ReflexiveDuel!;
        var modified=item with {ReflexiveDuel=change switch {
            "identity"=>c with {MovedUnit=c.MovedUnit with {Generation=-1}},
            "card"=>c with {CardNo="OGN·001/298"},
            "location"=>c with {Destination=new("P2","HAND")},_=>null}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {StackItems=[modified]}),e=>e.Contains("reflexive movement duel"));
    }

    [Fact]
    public async Task EchoCreatesTwoIndependentlyConfirmedDuelsAfterItsMovements()
    {
        var s=Position();s=s with {UntilEndOfTurnEffects=[EchoCostRules.GrantPrefix+"P1"]};
        var command=Command() with {OptionalCosts=["ECHO"],RepeatChoices=[new("",["D2"])]};
        var result=await Top((await Act(s,"P1",command)).State);
        Assert.Equal(1,result.State.CardObjects["D"].MovementCountThisTurn);Assert.Equal(1,result.State.CardObjects["D2"].MovementCountThisTurn);
        Assert.Equal(2,result.State.TriggerQueue.Count(t=>t.ReflexiveDuel is not null));Restore(result.State);
        var queued=result.State;var queuedViews=queued.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,queued.Tick,0,result.Snapshots[id],queued.Tick,0,result.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(queued.RoomId,queued.Tick,0,[],queued);
        var errors=MatchRecoveryValidator.Validate(queued.RoomId,0,[],[],queuedViews,queued,queued.Tick,spectator);Assert.True(errors.Count==0,string.Join("; ",errors));
        var publicQueue=Assert.IsAssignableFrom<IEnumerable<object>>(result.Snapshots["P2"].Timing!["triggerQueue"]);
        var projected=Assert.IsType<Dictionary<string,object?>>(publicQueue.First());var original=projected["reflexiveDuel"];
        projected["reflexiveDuel"]=queued.TriggerQueue[0].ReflexiveDuel! with {Destination=new("P2","BATTLEFIELD","BF2")};
        Assert.Contains(MatchRecoveryValidator.Validate(queued.RoomId,0,[],[],queuedViews,queued,queued.Tick),e=>e.Contains("reflexive movement duel differs"));
        projected["reflexiveDuel"]=original;
        result=await Act(result.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:result.State.TriggerQueue.Select(t=>t.TriggerId).ToArray()));
        var views=result.State.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,result.State.Tick,0,result.Snapshots[id],result.State.Tick,0,result.Prompts[id]));
        Assert.Empty(MatchRecoveryValidator.Validate(result.State.RoomId,0,[],[],views,result.State,result.State.Tick));
        for(var i=0;i<2;i++)result=await Choose(result.State);
        Assert.Equal(0,result.State.CardObjects["E"].Damage);
        result=await Top(result.State);result=await Top(result.State);
        Assert.Equal(5,result.State.CardObjects["E"].Damage);Assert.Contains("D",result.State.PlayerZones["P2"].Graveyard);Assert.Contains("D2",result.State.PlayerZones["P2"].Graveyard);Restore(result.State);
    }
    [Fact]
    public async Task BothPlayersCanRecoverTheCurrentTargetAndResponseWindows()
    {
        var result=await Open();
        for(var i=0;i<2;i++) {
            var state=result.State;
            var views=state.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,state.Tick,0,result.Snapshots[id],state.Tick,0,result.Prompts[id]));
            Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId,0,[],[],views,state,state.Tick));
            if(i==0)result=await Choose(state);
        }
    }
}
