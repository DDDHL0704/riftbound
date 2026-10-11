using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class ElderDragonTests
{
    internal static MatchState Position(string no="UNL-118/219")
    {
        var s=ReflexiveMovementDuelTests.Position();
        return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["D"]=s.CardObjects["D"] with {Power=20},
            ["AOE"]=s.CardObjects["AOE"] with {CardNo="OGN·252/298"},
            ["ELDER"]=new("ELDER",cardNo:no,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Hand=[..s.PlayerZones["P1"].Hand,"ELDER"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["ELDER"]=new("P1","HAND")},
            RunePools=new Dictionary<string,RunePool>(s.RunePools){["P1"]=new(50,50)}};
    }
    internal static MatchState WithElder(MatchState state,string controller="P1",bool hidden=false)
    {
        var s=state with {PlayerZones=state.PlayerZones.ToDictionary(p=>p.Key,p=>p.Value with {Hand=p.Value.Hand.Where(id=>id!="ELDER").ToArray()}),
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["ELDER"]=state.CardObjects["ELDER"] with {Power=10}}};
        s=At(s,"ELDER",controller,hidden?"BATTLEFIELD":"BASE",hidden?"BF2":null);
        return !hidden?s:s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["ELDER"]=s.CardObjects["ELDER"] with {IsFaceDown=true,Tags=[CardObjectTags.UnitCard,CardObjectTags.Standby]}}};
    }
    [Theory]
    [InlineData("UNL-118/219")] [InlineData("UNL-118a/219")]
    public async Task EarlierDamageBecomesLethalAsDragonEntersBeforeItsPlayAbility(string no)
    {
        var damaged=await Top((await Act(Position(no),"P1",new PlayCardCommand("AOE","OGN·252/298",["D"]))).State);
        Assert.Equal(6,damaged.State.CardObjects["D"].Damage);Assert.Contains("D",damaged.State.PlayerZones["P2"].Battlefields);
        var entered=await Act(damaged.State,"P1",new PlayCardCommand("ELDER",no,[]));
        Assert.Contains("ELDER",entered.State.PlayerZones["P1"].Base);
        Assert.Contains("D",entered.State.PlayerZones["P2"].Graveyard);Restore(entered.State);
    }

    [Theory]
    [InlineData("P1",false,true)] [InlineData("P2",false,false)] [InlineData("P1",true,false)]
    public async Task OnlyVisibleControlledDragonMakesItsControllersDamageLethal(string controller,bool hidden,bool destroyed)
    {
        var s=WithElder(Position(),controller,hidden);
        var result=await Top((await Act(s,"P1",new PlayCardCommand("AOE","OGN·252/298",["D"]))).State);
        Assert.Equal(destroyed,result.State.PlayerZones["P2"].Graveyard.Contains("D"));
        if(!destroyed)Assert.Equal(new[]{"P1"},result.State.CardObjects["D"].DamageMarkedByPlayerIds);Restore(result.State);
    }
    [Fact]
    public async Task FriendlyDuelUnitDamageUsesItsControllerAndKillsWithOnePoint()
    {
        var s=WithElder(Position());s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["A"]=s.CardObjects["A"] with {Power=1},["AOE"]=s.CardObjects["AOE"] with {CardNo="OGN·128/298"}}};
        var done=await Top((await Act(s,"P1",new PlayCardCommand("AOE","OGN·128/298",["A","D"]))).State);
        Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);Assert.Contains("A",done.State.PlayerZones["P1"].Graveyard);Restore(done.State);
    }
    [Fact]
    public async Task EnemyUnitsInDragonsRageDoNotUseTheCastersElderDragon()
    {
        var s=WithElder(Position());s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["D"]=s.CardObjects["D"] with {Power=3},["AOE"]=s.CardObjects["AOE"] with {CardNo=ReflexiveMovementDuelTests.Spell}}};
        var opened=await ReflexiveMovementDuelTests.Open(s);var done=await Top((await ReflexiveMovementDuelTests.Choose(opened.State)).State);
        Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);Assert.Contains("E",done.State.PlayerZones["P2"].Base);
        Assert.Equal(3,done.State.CardObjects["E"].Damage);Assert.Equal(new[]{"P2"},done.State.CardObjects["E"].DamageMarkedByPlayerIds);Restore(done.State);
    }
    [Theory]
    [InlineData("PREVENT_NEXT_DAMAGE_THIS_TURN")] [InlineData("immune")]
    public async Task PreventedDamageLeavesNoPlayerMarkAndCannotBecomeLethal(string protection)
    {
        var s=WithElder(Position());
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["D"]=s.CardObjects["D"] with {
            CardNo=protection=="immune"?"OGN·189/298":s.CardObjects["D"].CardNo,
            MovementCountThisTurn=protection=="immune"?2:0,UntilEndOfTurnEffects=protection=="immune"?[]:[protection]}}};
        var done=await Top((await Act(s,"P1",new PlayCardCommand("AOE","OGN·252/298",["D"]))).State);
        Assert.Equal(0,done.State.CardObjects["D"].Damage);Assert.Null(done.State.CardObjects["D"].DamageMarkedByPlayerIds);Assert.Contains("D",done.State.PlayerZones["P2"].Battlefields);Restore(done.State);
    }

    internal static Task<ResolutionResult> Play(MatchState? state=null)=>Act(state??Position(),"P1",new PlayCardCommand("ELDER","UNL-118/219",[]));
    internal static Task<ResolutionResult> Select(MatchState state,params string[] targets)
        =>Act(state,"P1",new ChooseCardsCommand(state.PendingCardChoice!.ChoiceId,state.PendingCardChoice.ChoiceWindow,targets));
    [Fact]
    public async Task PlayAbilityChoosesAtMostOneEnemyPerLocationAfterEntry()
    {
        var played=await Play();Assert.Contains("ELDER",played.State.PlayerZones["P1"].Base);
        var choice=Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);Assert.Equal(0,choice.RequiredCount);Assert.Equal(2,choice.MaxCount);
        Assert.Null(played.State.PriorityPlayerId);Assert.Equal(0,played.State.CardObjects["D"].Damage);Restore(played.State);
        var bad=new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["D","D2"]);
        var rejected=await new CoreRuleEngine().ResolveAsync(played.State,new("bad","P1",bad.CmdType),bad,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(played.State),MatchStateHasher.Hash(rejected.State));
        var selected=await Select(played.State,"D","E");Restore(selected.State);Assert.NotNull(selected.State.PriorityPlayerId);
        var done=await Top(selected.State);Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);Assert.Contains("E",done.State.PlayerZones["P2"].Graveyard);Assert.Contains("D2",done.State.PlayerZones["P2"].Battlefields);Restore(done.State);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task MultiTargetWardCanBePaidOrDeclinedAfterChoosingDifferentLocations(bool pay)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]},["E"]=s.CardObjects["E"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]}}};
        var selected=await Select((await Play(s)).State,"D","E");var pending=Assert.IsType<PendingPaymentState>(selected.State.PendingPayment);Assert.Equal(2,pending.PowerCost);Restore(selected.State);
        var paid=await Act(selected.State,"P1",new PayCostCommand(pending.PaymentId,pending.PaymentWindow,[pay?"PAY":"DECLINE"]));Restore(paid.State);
        if(pay){var done=await Top(paid.State);Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);Assert.Contains("E",done.State.PlayerZones["P2"].Graveyard);Restore(done.State);}
        else{Assert.Empty(paid.State.StackItems);Assert.Equal(0,paid.State.CardObjects["D"].Damage);Assert.Equal(0,paid.State.CardObjects["E"].Damage);}
    }
    [Fact]
    public async Task ResponseMoveInvalidatesOnlyTheTargetThatLeftItsDeclaredLocation()
    {
        var selected=await Select((await Play()).State,"D","E");
        var s=At(selected.State,"D","P2","BASE");Restore(s);var done=await Top(s);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base);Assert.Equal(0,done.State.CardObjects["D"].Damage);
        Assert.Contains("E",done.State.PlayerZones["P2"].Graveyard);Restore(done.State);
    }

    internal static async Task<ResolutionResult> Combat(bool elder=true,int defenderPower=5)
    {
        var s=DeathAndDuelLifecycleTests.State();var cards=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["A"]=s.CardObjects["A"] with {CardNo="OGN·001/298",Power=3},
            ["D"]=s.CardObjects["D"] with {CardNo="OGN·001/298",Power=defenderPower},
            ["ELDER"]=new("ELDER",cardNo:"UNL-118/219",power:10,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard])};
        foreach(var id in new[]{"D2","D3","D4"})cards[id]=new(id,cardNo:"OGN·001/298",power:defenderPower,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]);
        s=s with {CardObjects=cards};foreach(var id in new[]{"D2","D3","D4"})s=At(s,id,"P2","BATTLEFIELD","BF");
        s=At(s,"ELDER",elder?"P1":"P2","BASE");
        var moved=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var focused=await Act(moved.State,"P1",new PassFocusCommand());focused=await Act(focused.State,"P2",new PassFocusCommand());
        var opened=await Act(focused.State,"P1",new DeclareBattleCommand("BF",["A"],["D","D2","D3","D4"],["COMBAT_ASSIGNMENT"]));
        for(var j=0;j<12&&opened.State.PriorityPlayerId is not null;j++)opened=await Act(opened.State,opened.State.PriorityPlayerId,new PassPriorityCommand());
        return opened;
    }
    [Theory]
    [InlineData(true,5,true)] [InlineData(false,5,false)] [InlineData(false,0,true)]
    public async Task CombatUsesModifiedNonzeroLethalThreshold(bool elder,int defenderPower,bool accepted)
    {
        var opened=await Combat(elder,defenderPower);Restore(opened.State);
        var command=new AssignCombatDamageCommand(opened.State.BattleState.BattleId!,"BF",[new("A","D",1),new("A","D2",1),new("A","D3",1)]);
        var assigned=await new CoreRuleEngine().ResolveAsync(opened.State,new("assign","P1",command.CmdType),command,default);
        Assert.True(accepted==assigned.Accepted,assigned.ErrorMessage+" "+System.Text.Json.JsonSerializer.Serialize(opened.Prompts["P1"]));
        if(!accepted){Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(assigned.State));return;}
        var done=await CombatTestDriver.FinishAsync(assigned);
        Assert.All(new[]{"D","D2","D3"},id=>Assert.Contains(id,done.State.PlayerZones["P2"].Graveyard));
        Assert.Contains("D4",done.State.PlayerZones["P2"].Battlefields);Restore(done.State);
    }
    [Theory]
    [InlineData("unknown")] [InlineData("duplicate")] [InlineData("zero-damage")]
    public async Task RecoveryRejectsForgedDamageMarkingPlayers(string change)
    {
        var damaged=await Top((await Act(Position(),"P1",new PlayCardCommand("AOE","OGN·252/298",["D"]))).State);var s=damaged.State;
        Assert.Equal(new[]{"P1"},s.CardObjects["D"].DamageMarkedByPlayerIds);Restore(s);
        var changed=s.CardObjects["D"] with {Damage=change=="zero-damage"?0:6,DamageMarkedByPlayerIds=change=="unknown"?["FORGED"]:change=="duplicate"?["P1","P1"]:["P1"]};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=changed}}),e=>e.Contains("damage marking players"));
    }
    [Theory]
    [InlineData("missing")] [InlineData("card")] [InlineData("location")] [InlineData("generation")]
    public async Task RecoveryRejectsForgedPerLocationPlayAbility(string change)
    {
        var selected=await Select((await Play()).State,"D","E");var s=selected.State;var item=Assert.Single(s.StackItems);var context=item.LocationDamage!;
        var invalid=item with {LocationDamage=change=="missing"?null:change=="card"?context with {CardNo="OGN·001/298"}:change=="location"?context with {TargetLocations=new Dictionary<string,ObjectLocationState>(context.TargetLocations!){["D"]=new("P2","HAND")}}:context,
            TargetGenerations=change=="generation"?new Dictionary<string,long>{{"D",-1},{"E",0}}:item.TargetGenerations};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {StackItems=[invalid]}),e=>e.Contains("per-location damage"));
    }

    [Fact]
    public async Task EndTurnRemovesDamageAndItsPlayerMarksTogether()
    {
        var damaged=await Top((await Act(Position(),"P1",new PlayCardCommand("AOE","OGN·252/298",["D"]))).State);
        var ended=await Act(damaged.State,"P1",new EndTurnCommand());
        Assert.Equal(0,ended.State.CardObjects["D"].Damage);Assert.Null(ended.State.CardObjects["D"].DamageMarkedByPlayerIds);Restore(ended.State);
    }
    [Fact]
    public async Task PlayAbilitySurvivesSourceDepartureWithoutKeepingThePassive()
    {
        var selected=await Select((await Play()).State,"D","E");var s=selected.State;
        s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Base=s.PlayerZones["P1"].Base.Where(id=>id!="ELDER").ToArray(),Graveyard=[..s.PlayerZones["P1"].Graveyard,"ELDER"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["ELDER"]=new("P1","GRAVEYARD")},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["ELDER"]=s.CardObjects["ELDER"] with {ObjectGeneration=s.CardObjects["ELDER"].ObjectGeneration+1}}};
        Restore(s);var done=await Top(s);
        Assert.Equal(1,done.State.CardObjects["D"].Damage);Assert.Equal(1,done.State.CardObjects["E"].Damage);Assert.DoesNotContain("D",done.State.PlayerZones["P2"].Graveyard);Assert.DoesNotContain("E",done.State.PlayerZones["P2"].Graveyard);Restore(done.State);
    }
    [Fact]
    public async Task TwoTargetWardCannotBeForgedToCostOnePower()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]},["E"]=s.CardObjects["E"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Spellshield]}}};
        var selected=await Select((await Play(s)).State,"D","E");s=selected.State;
        var bad=s with {PendingPayment=s.PendingPayment! with {PowerCost=1}};
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(bad));
        var command=new PayCostCommand(bad.PendingPayment!.PaymentId,bad.PendingPayment.PaymentWindow,["PAY"]);
        var rejected=await new CoreRuleEngine().ResolveAsync(bad,new("forged","P1",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(bad),MatchStateHasher.Hash(rejected.State));
    }
    [Fact]
    public async Task PublicTargetLocationContextCannotDriftFromTheSavedAbility()
    {
        var selected=await Select((await Play()).State,"D","E");var s=selected.State;
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,selected.Snapshots[id],s.Tick,0,selected.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        Assert.Empty(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator));
        foreach(var snapshot in views.Values.Select(v=>v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var payload=Assert.IsType<Dictionary<string,object?>>(Assert.Single(snapshot.Stack));var original=payload["locationDamage"];
            payload["locationDamage"]=s.StackItems[0].LocationDamage! with {TargetLocations=new Dictionary<string,ObjectLocationState>(s.StackItems[0].LocationDamage!.TargetLocations!){["D"]=new("P2","BASE")}};
            Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator),e=>e.Contains("per-location damage context differs"));payload["locationDamage"]=original;
        }
    }

    [Theory]
    [InlineData("control")] [InlineData("dirty-controller")] [InlineData("new-object")]
    public async Task LocationDamageCannotHitAnInvalidatedTarget(string change)
    {
        var selected=await Select((await Play()).State,"D","E");var s=selected.State;
        if(change=="control")s=At(s,"D","P1","BATTLEFIELD","BF");
        else s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=change=="dirty-controller"?s.CardObjects["D"] with {ControllerId="P1"}:s.CardObjects["D"] with {ObjectGeneration=s.CardObjects["D"].ObjectGeneration+1}}};
        var done=await Top(s);Assert.Equal(0,done.State.CardObjects["D"].Damage);Assert.Contains("E",done.State.PlayerZones["P2"].Graveyard);
        if(change!="dirty-controller")Restore(done.State);
    }
    [Fact]
    public async Task PendingCombatAssignmentCannotBeBypassedByEndingTheTurn()
    {
        var opened = await Combat();
        var command = new EndTurnCommand();
        var rejected = await new CoreRuleEngine().ResolveAsync(opened.State,
            new("end-during-assignment", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(3, Convert.ToInt32(opened.Prompts["P1"].View!.Metadata!["totalAssignableDamage"]));
    }

}
