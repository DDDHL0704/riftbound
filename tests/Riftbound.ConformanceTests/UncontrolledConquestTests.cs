using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;

public sealed class UncontrolledConquestTests
{
    internal static MatchState Position(string no="SFD·116/221")
    {
        var s=ConquestLifecycleRegressionTests.State("OGN·296/298");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["UNIT"]=s.CardObjects["UNIT"] with {CardNo=no,Power=5},
            ["ENEMY"]=new("ENEMY",power:20,cardNo:"OGN·096/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) }};
        s=At(At(s,"ENEMY","P2","BASE"),"BF","P2","BATTLEFIELD","BF");
        return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["BF"]=s.CardObjects["BF"] with {ControllerId=null}}};
    }
    internal static async Task<ResolutionResult> Choose(MatchState s,string id="ENEMY")
    {
        var c=Assert.IsType<PendingCardChoiceState>(s.PendingCardChoice);
        return await Act(s,c.PlayerId,new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[id]));
    }
    [Theory]
    [InlineData("SFD·116/221")]
    [InlineData("SFD·233/221")]
    [InlineData("SFD·233*/221")]
    public async Task EveryCurrentPrintingConfirmsEnemyBaseTargetBeforeResponse(string no)
    {
        var conquered=await ConquestLifecycleRegressionTests.Conquer(Position(no));
        var choice=Assert.IsType<PendingCardChoiceState>(conquered.State.PendingCardChoice);
        Assert.Equal(new[]{"ENEMY"},choice.LegalObjectIds);Assert.Equal(1,choice.RequiredCount);Assert.Null(conquered.State.PriorityPlayerId);
        Assert.Equal(0,conquered.State.CardObjects["ENEMY"].Damage);Restore(conquered.State);
        var selected=await Choose(conquered.State);Assert.Equal("P1",selected.State.PriorityPlayerId);Assert.Equal(0,selected.State.CardObjects["ENEMY"].Damage);
        var done=await Top(selected.State);Assert.Equal(5,done.State.CardObjects["ENEMY"].Damage);Restore(done.State);
    }
    [Fact]
    public async Task OfficialVoidAssaultQaClearsPreviousEnemyControlBeforeConquering()
    {
        var s=At(SequentialMovementTests.Position(),"D2","P2","BASE");s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["A"]=s.CardObjects["A"] with {CardNo="SFD·116/221",Power=5},["D"]=s.CardObjects["D"] with {Power=20}}};
        var moved=await Top((await Act(s,"P1",SequentialMovementTests.Command("BATTLEFIELD:BF","BASE:P2"))).State);
        var first=await Act(moved.State,moved.State.FocusPlayerId!,new PassFocusCommand());
        var conquered=await Act(first.State,first.State.FocusPlayerId!,new PassFocusCommand());
        Assert.Equal("P1",conquered.State.BattlefieldStates["BF"].ControllerId);
        var choice=Assert.IsType<PendingCardChoiceState>(conquered.State.PendingCardChoice);Assert.Contains("D",choice.LegalObjectIds);
        var done=await Top((await Choose(conquered.State,"D")).State);Assert.Equal(5,done.State.CardObjects["D"].Damage);Restore(done.State);
    }
    [Fact]
    public async Task DamageReadsCurrentPowerAfterResponseAndKeepsTriggerController()
    {
        var selected=await Choose((await ConquestLifecycleRegressionTests.Conquer(Position())).State);
        var s=At(selected.State,"UNIT","P2","BASE");s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["UNIT"]=s.CardObjects["UNIT"] with {Power=8}}};
        var done=await Top(s);Assert.Equal(8,done.State.CardObjects["ENEMY"].Damage);Restore(done.State);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task WardCanBePaidOrDeclinedBeforeAnyResponse(bool pay)
    {
        var s=Position();s=s with {RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(3,3)},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["ENEMY"]=s.CardObjects["ENEMY"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]}}};
        var target=await Choose((await ConquestLifecycleRegressionTests.Conquer(s)).State);Restore(target.State);
        var pending=Assert.IsType<PendingPaymentState>(target.State.PendingPayment);Assert.Equal(1,pending.PowerCost);Assert.Null(target.State.PriorityPlayerId);
        var paid=await Act(target.State,"P1",new PayCostCommand(pending.PaymentId,pending.PaymentWindow,[pay?"PAY":"DECLINE"]));Restore(paid.State);
        Assert.Equal(pay?2:3,paid.State.RunePools["P1"].Power);Assert.Equal(3,paid.State.RunePools["P1"].Mana);
        if(!pay) {Assert.Empty(paid.State.StackItems);Assert.Equal(0,paid.State.CardObjects["ENEMY"].Damage);return;}
        var done=await Top(paid.State);Assert.Equal(5,done.State.CardObjects["ENEMY"].Damage);Assert.DoesNotContain(done.Events,e=>e.Kind=="COST_PAID");Restore(done.State);
    }
    [Theory]
    [InlineData("new-target")] [InlineData("target-battlefield")] [InlineData("new-source")]
    public async Task ResolutionRechecksBaseTargetAndObjectIdentity(string change)
    {
        var selected=await Choose((await ConquestLifecycleRegressionTests.Conquer(Position())).State);var s=selected.State;
        if(change=="target-battlefield") s=At(s,"ENEMY","P2","BATTLEFIELD","BF");
        else {var id=change=="new-target"?"ENEMY":"UNIT";s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {[id]=s.CardObjects[id] with {ObjectGeneration=2,Power=50}}};}
        var done=await Top(s);Assert.Equal(0,done.State.CardObjects["ENEMY"].Damage);Assert.DoesNotContain(done.Events,e=>e.Kind=="DAMAGE_APPLIED");Restore(done.State);
    }
    [Fact]
    public async Task NoLegalEnemyBaseTargetRemovesUnconfirmedAbility()
    {
        var s=Position();s=At(s,"ENEMY","P1","BASE");var done=await ConquestLifecycleRegressionTests.Conquer(s);
        Assert.Equal(1,done.State.PlayerScores["P1"]);Assert.Null(done.State.PendingCardChoice);Assert.Empty(done.State.StackItems);
        Assert.Contains(done.Events,e=>e.Kind=="TRIGGER_NOT_CONFIRMED");Restore(done.State);
    }
    [Fact]
    public async Task EnemyControlledBattlefieldCombatDoesNotMeetUncontrolledCondition()
    {
        var s=OfficialLegendConquestTests.CombatPosition("OGN·269/298",power:10);
        var cards=new Dictionary<string,CardObjectState>(s.CardObjects) {["A"]=s.CardObjects["A"] with {CardNo="SFD·116/221"}};
        s=s with {CardObjects=cards};
        var done=await OfficialLegendConquestTests.CombatConquer(s);
        Assert.Contains(done.Events,e=>e.Kind=="BATTLEFIELD_CONQUERED");Assert.DoesNotContain(done.State.StackItems,i=>i.UnitConquest is not null);Restore(done.State);
    }
    [Fact]
    public async Task HostileTakeoverOfSoleOccupantMatchesOfficialQa()
    {
        var s=Position();s=At(s,"UNIT","P2","BATTLEFIELD","BF");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["BF"]=s.CardObjects["BF"] with {ControllerId="P2"}, ["STEAL"]=new("STEAL",cardNo:"SFD·202/221",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Hand=["STEAL"]}},
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(20,20)}};
        var changed=await Top((await Act(s,"P1",new PlayCardCommand("STEAL","SFD·202/221",["UNIT"]))).State);
        Assert.Equal("P1",changed.State.CardObjects["UNIT"].ControllerId);Assert.Null(changed.State.CardObjects["BF"].ControllerId);
        var first=await Act(changed.State,changed.State.FocusPlayerId!,new PassFocusCommand());var conquered=await Act(first.State,first.State.FocusPlayerId!,new PassFocusCommand());
        var done=await Top((await Choose(conquered.State)).State);Assert.Equal(5,done.State.CardObjects["ENEMY"].Damage);Restore(done.State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HoldActivationCannotInventAnUncontrolledConquest(bool equipment)
    {
        var s=OfficialHoldSequenceTests.State(equipment?"OGN·296/298":"OGN·286/298");
        s=OfficialHoldSequenceTests.AddUnit(s,"UNIT","SFD·116/221","F","P1");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["ENEMY"]=new("ENEMY",cardNo:"OGN·096/298",power:20,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard])}};s=At(s,"ENEMY","P2","BASE");
        if(equipment) {s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["GEAR"]=new("GEAR",cardNo:"SFD·030/221",ownerId:"P1",controllerId:"P1",attachedToObjectId:"UNIT",tags:[CardObjectTags.EquipmentCard,CardEquipmentKeywordNames.Weapon])}};s=At(s,"GEAR","P1","BATTLEFIELD","F");}
        var result=await Act(s,"P1",new PassPriorityCommand());
        for(var i=0;i<6 && result.State.StackItems.Count>0;i++) result=await Top(result.State);
        Assert.Null(result.State.PendingCardChoice);Assert.DoesNotContain(result.State.StackItems,i=>i.UnitConquest is not null);Assert.Equal(0,result.State.CardObjects["ENEMY"].Damage);Restore(result.State);
    }
    [Fact]
    public async Task RecoveryRejectsForgedConquestAndChoiceButPreservesConfirmedTarget()
    {
        var opened=await ConquestLifecycleRegressionTests.Conquer(Position());var item=Assert.Single(opened.State.StackItems);
        foreach(var forged in new[]{item with {UnitConquest=null},item with {UnitConquest=item.UnitConquest! with {PreviousControllerId="P2"}},item with {UnitConquest=item.UnitConquest! with {SourceGeneration=-1}}})
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with {StackItems=[forged]}),e=>e.Contains("unit conquest"));
        var choice=opened.State.PendingCardChoice!;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(opened.State with {PendingCardChoice=choice with {LegalObjectIds=["UNIT"]}}));
        var restored=OfficialInsightAndSpellLockTests.Restore(opened.State);var selected=await Choose(restored);Restore(selected.State);
        var done=await Top(OfficialInsightAndSpellLockTests.Restore(selected.State));Assert.Equal(5,done.State.CardObjects["ENEMY"].Damage);
    }
    [Theory]
    [InlineData("P2", "ENEMY")] [InlineData("P1", "UNIT")] [InlineData("P1", "")]
    public async Task InvalidPlayerTargetOrMandatoryDeclineIsAtomic(string player,string target)
    {
        var opened=await ConquestLifecycleRegressionTests.Conquer(Position());var c=opened.State.PendingCardChoice!;
        var cmd=new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,target.Length==0?[]:[target]);
        var bad=await new CoreRuleEngine().ResolveAsync(opened.State,new("bad",player,cmd.CmdType),cmd,default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(bad.State));
    }
    [Theory]
    [InlineData("PREVENT_NEXT_DAMAGE_THIS_TURN",0)]
    [InlineData("DAMAGE_RECEIVED_DOUBLED_THIS_TURN",10)]
    public async Task DamageUsesSharedPreventionAndReplacement(string effect,int expected)
    {
        var selected=await Choose((await ConquestLifecycleRegressionTests.Conquer(Position())).State);
        var s=selected.State with {CardObjects=new Dictionary<string,CardObjectState>(selected.State.CardObjects) {["ENEMY"]=selected.State.CardObjects["ENEMY"] with {UntilEndOfTurnEffects=[effect]}}};
        var done=await Top(s);Assert.Equal(expected,done.State.CardObjects["ENEMY"].Damage);Restore(done.State);
    }
    [Fact]
    public async Task OpponentsTurnConquestStillBelongsToTheConqueringUnitController()
    {
        var s=LocalDestructionRecallTests.At(SequentialMovementTests.Position(),"D2","P2","BASE");
        s=s with {TurnPlayerId="P2",ActivePlayerId="P2",CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["A"]=s.CardObjects["A"] with {CardNo="SFD·116/221",Power=5},["D"]=s.CardObjects["D"] with {Power=20},
            ["AOE"]=s.CardObjects["AOE"] with {OwnerId="P2",ControllerId="P2"}},
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P2"]=new(20,20)},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {
                ["P1"]=s.PlayerZones["P1"] with {Hand=[]},["P2"]=s.PlayerZones["P2"] with {Hand=["AOE"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["AOE"]=new("P2","HAND")}};
        var moved=await Top((await Act(s,"P2",new PlayCardCommand("AOE",SequentialMovementTests.Spell,["D","A"],MovementDestinations:["BASE:P2","BATTLEFIELD:BF"]))).State);
        var first=await Act(moved.State,moved.State.FocusPlayerId!,new PassFocusCommand());var conquered=await Act(first.State,first.State.FocusPlayerId!,new PassFocusCommand());
        Assert.Equal("P1",conquered.State.PendingCardChoice!.PlayerId);Assert.Equal("P2",conquered.State.TurnPlayerId);
        var done=await Top((await Choose(conquered.State,"D")).State);Assert.Equal(5,done.State.CardObjects["D"].Damage);Restore(done.State);
    }
    [Fact]
    public async Task MultipleConqueringSourcesConfirmIndependentTargetsBeforeResponses()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["SECOND"]=s.CardObjects["UNIT"] with {ObjectId="SECOND"}}};s=At(s,"SECOND","P1","BASE");
        var moved=await Act(s,"P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[],["UNIT","SECOND"]));
        var first=await Act(moved.State,"P1",new PassFocusCommand());var opened=await Act(first.State,"P2",new PassFocusCommand());
        Assert.Equal(2,opened.State.TriggerQueue.Count);Restore(opened.State);
        var ordered=await Act(opened.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:opened.State.TriggerQueue.Select(t=>t.TriggerId).Reverse().ToArray()));
        var chosen=await Choose(ordered.State);Assert.NotNull(chosen.State.PendingCardChoice);Assert.Null(chosen.State.PriorityPlayerId);Restore(chosen.State);
        chosen=await Choose(chosen.State);Assert.Null(chosen.State.PendingCardChoice);var one=await Top(chosen.State);Assert.Equal(5,one.State.CardObjects["ENEMY"].Damage);
        var done=await Top(one.State);Assert.Equal(10,done.State.CardObjects["ENEMY"].Damage);Restore(done.State);
    }
    [Fact]
    public async Task LethalSkillDamageUsesOwnerCorrectDestructionCleanup()
    {
        var selected=await Choose((await ConquestLifecycleRegressionTests.Conquer(Position())).State);
        var s=selected.State with {CardObjects=new Dictionary<string,CardObjectState>(selected.State.CardObjects) {["ENEMY"]=selected.State.CardObjects["ENEMY"] with {Power=4,OwnerId="P1"}}};
        var done=await Top(s);Assert.Contains("ENEMY",done.State.PlayerZones["P1"].Graveyard);Assert.DoesNotContain("ENEMY",done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(done.Events,e=>e.Kind=="UNIT_DESTROYED");Restore(done.State);
    }
    [Theory]
    [InlineData(0)] [InlineData(-3)]
    public async Task NonpositiveCurrentPowerDoesNotDealDamage(int power)
    {
        var selected=await Choose((await ConquestLifecycleRegressionTests.Conquer(Position())).State);
        var s=selected.State with {CardObjects=new Dictionary<string,CardObjectState>(selected.State.CardObjects) {["UNIT"]=selected.State.CardObjects["UNIT"] with {Power=power}}};
        var done=await Top(s);Assert.Equal(0,done.State.CardObjects["ENEMY"].Damage);Restore(done.State);
    }
}
