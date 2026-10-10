using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.DestructionOrderTests;

namespace Riftbound.ConformanceTests;

public sealed class LocalDestructionRecallTests
{
    internal static MatchState At(MatchState s, string id, string player, string zone, string? field = null)
    {
        var zones = s.PlayerZones.ToDictionary(p=>p.Key,p=>p.Value with {Base=p.Value.Base.Where(x=>x!=id).ToArray(),Battlefields=p.Value.Battlefields.Where(x=>x!=id).ToArray()});
        zones[player] = zone == "BASE" ? zones[player] with {Base=[..zones[player].Base,id]} : zones[player] with {Battlefields=[..zones[player].Battlefields,id]};
        return s with {PlayerZones=zones,CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {[id]=s.CardObjects[id] with {ControllerId=player}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {[id]=new(player,zone,field)}};
    }
    internal static MatchState PositionFor(bool two=false, string no="SFD·173/221")
    {
        var s=At(Source(NoReplacement(two),no,"S"),"S","P2","BATTLEFIELD","BF");
        return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["S"]=s.CardObjects["S"] with {Power=4}}};
    }
    internal static async Task<ResolutionResult> CastAnother(MatchState s,string id,params string[] targets)
    {
        const string no="OGN·229/298";
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){[id]=new(id,cardNo:no,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Hand=[..s.PlayerZones["P1"].Hand,id]}}};
        return await Top((await Act(s,"P1",new PlayCardCommand(id,no,targets))).State);
    }

    [Theory]
    [InlineData("SFD·173/221",false)] [InlineData("SFD·239/221",false)] [InlineData("SFD·239*/221",false)]
    [InlineData("SFD·173/221",true)] [InlineData("SFD·239/221",true)] [InlineData("SFD·239*/221",true)]
    public async Task SameLocationLowerPowerIsMandatoryForAllPrints(string no,bool inBase)
    {
        var s=PositionFor(no:no); if(inBase) s=At(At(s,"S","P2","BASE"),"D","P2","BASE");
        var done=await Open(s,"OGN·229/298","D");
        Assert.Null(done.State.PendingRuleChoice); Assert.Contains("D",done.State.PlayerZones["P2"].Base);
        Assert.Equal(0,done.State.CardObjects["D"].Damage); Assert.True(done.State.CardObjects["D"].IsExhausted);
        Assert.Equal(2,done.State.CardObjects["D"].Power); Assert.Contains(CardObjectTags.Boon,done.State.CardObjects["D"].Tags);
        Assert.Equal(0,done.State.CardObjects["D"].ObjectGeneration);
        Assert.DoesNotContain(done.Events,e=>e.Kind is "UNIT_DESTROYED" or "UNIT_MOVED" or "COST_PAID");
        var recall=Assert.Single(done.Events,e=>e.Kind=="UNIT_RECALLED_TO_BASE"); Assert.Equal("S",recall.Payload["sourceObjectId"]);
        Restore(done.State);
    }

    [Theory]
    [InlineData(3,true)] [InlineData(4,false)] [InlineData(5,false)]
    public async Task ComparisonUsesCurrentPowerAndIsStrict(int targetPower,bool saved)
    {
        var s=PositionFor();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {Power=targetPower}}};
        var done=await Open(s,"OGN·229/298","D");
        Assert.Equal(saved,done.State.PlayerZones["P2"].Base.Contains("D"));Assert.Equal(!saved,done.State.PlayerZones["P2"].Graveyard.Contains("D"));Restore(done.State);
    }

    [Theory]
    [InlineData("base")] [InlineData("other-field")] [InlineData("enemy-controller")] [InlineData("face-down")] [InlineData("not-unit")]
    public async Task IneligibleSourceCannotProtect(string branch)
    {
        var s=PositionFor();
        if(branch=="base") s=At(s,"S","P2","BASE");
        if(branch=="other-field") {
            s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["BF2"]=s.CardObjects["BF"] with {ObjectId="BF2"}}};
            s=At(At(s,"BF2","P2","BATTLEFIELD","BF2"),"S","P2","BATTLEFIELD","BF2");
        }
        if(branch=="enemy-controller") s=At(s,"S","P1","BASE");
        if(branch is "face-down" or "not-unit") s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["S"]=s.CardObjects["S"] with {IsFaceDown=branch=="face-down",Tags=branch=="not-unit"?[CardObjectTags.EquipmentCard]:[CardObjectTags.UnitCard]} }};
        if(branch=="not-unit") s=At(s,"S","P2","BASE");
        var done=await Open(s,"OGN·229/298","D");Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task SourceCannotReplaceItsOwnDeath()
    {var done=await Open(PositionFor(),"OGN·229/298","S");Assert.Contains("S",done.State.PlayerZones["P2"].Graveyard);Restore(done.State);}

    [Fact]
    public async Task SimultaneouslyDyingSourceSavesBothOtherUnitsBeforeItsDeath()
    {
        var opened=await Open(PositionFor(true),"UNL-180/219");Restore(opened.State);
        Assert.DoesNotContain(opened.State.PendingRuleChoice!.Request.Options,o=>o.Id=="DECLINE");
        var done=await Select(opened.State,"LOCAL_RECALL:D2:S");
        Assert.Contains("S",done.State.PlayerZones["P2"].Graveyard);Assert.Contains("D",done.State.PlayerZones["P2"].Base);Assert.Contains("D2",done.State.PlayerZones["P2"].Base);
        Assert.Equal(1,done.State.DeathLedger.Counts["P2"]);
        var ev=done.Events.ToList();Assert.True(ev.FindLastIndex(e=>e.Kind=="UNIT_RECALLED_TO_BASE")<ev.FindIndex(e=>e.Kind=="UNIT_DESTROYED"));Restore(done.State);
    }

    [Fact]
    public async Task RecalledSourceRetainsItsReplacementForTheSameBatchAtItsOldLocation()
    {
        var s=At(Source(PositionFor(),"SFD·173/221","S2"),"S2","P2","BATTLEFIELD","BF");
        var opened=await Open(s,"UNL-180/219");var first=await Select(opened.State,"LOCAL_RECALL:S:S2");Restore(first.State);
        Assert.Contains(first.State.PendingRuleChoice!.Request.Options,o=>o.Id=="LOCAL_RECALL:D:S");
        var done=await Select(first.State,"LOCAL_RECALL:D:S");
        Assert.Contains("S2",done.State.PlayerZones["P2"].Graveyard);Assert.Contains("S",done.State.PlayerZones["P2"].Base);Assert.Contains("D",done.State.PlayerZones["P2"].Base);Restore(done.State);
    }

    [Theory]
    [InlineData("LOCAL_RECALL:D:S",false)] [InlineData("BANISH:D",true)]
    public async Task AffectedControllerChoosesBetweenRecallAndBanish(string choice,bool banished)
    {
        var s=PositionFor();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {UntilEndOfTurnEffects=["BANISH_IF_DESTROYED_THIS_TURN"]}}};
        var opened=await Open(s,"OGN·229/298","D");var done=await Select(opened.State,choice);
        Assert.Equal(banished,done.State.PlayerZones["P2"].Banished.Contains("D"));Assert.Equal(!banished,done.State.PlayerZones["P2"].Base.Contains("D"));Restore(done.State);
    }

    [Fact]
    public async Task DecliningOptionalSettStillAppliesMandatoryLocalRecall()
    {
        var s=PositionFor();s=s with {RunePools=new Dictionary<string,RunePool>(s.RunePools){["P2"]=new(7,1)}};
        var done=await Select((await Open(s)).State,"DECLINE");Assert.Contains("D",done.State.PlayerZones["P2"].Base);
        Assert.Equal(1,done.State.RunePools["P2"].Power);Assert.False(done.State.CardObjects["LEGEND"].IsExhausted);Restore(done.State);
    }

    [Theory]
    [InlineData("LOCAL_RECALL:D:S",false)] [InlineData("GEAR:D:G1",true)]
    public async Task LocalRecallAndHourglassShareTheExistingChoice(string choice,bool gearDestroyed)
    {
        var opened=await Open(Gear(PositionFor()),"OGN·229/298","D");var done=await Select(opened.State,choice);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base);Assert.Equal(gearDestroyed,done.State.PlayerZones["P2"].Graveyard.Contains("G1"));Restore(done.State);
    }
    [Theory]
    [InlineData("SFD·173/221")] [InlineData("SFD·239/221")] [InlineData("SFD·239*/221")]
    public async Task RealSorakaPlayEnablesRecallForControllerRatherThanOwner(string no)
    {
        var s=At(At(PositionFor(no:no),"S","P1","BASE"),"D","P1","BASE");
        s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {
            Base=s.PlayerZones["P1"].Base.Where(id=>id!="S").ToArray(),Hand=["AOE","S"]}},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["S"]=s.CardObjects["S"] with {OwnerId="P1"}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["S"]=new("P1","HAND")}};
        var played=await Act(s,"P1",new PlayCardCommand("S",no,[]));Assert.Contains("S",played.State.PlayerZones["P1"].Base);
        Assert.Equal(4,played.State.CardObjects["S"].Power);
        var done=await Open(played.State,"OGN·229/298","D");Assert.Contains("D",done.State.PlayerZones["P1"].Base);
        Assert.Equal("P2",done.State.CardObjects["D"].OwnerId);Assert.Equal("P1",done.State.CardObjects["D"].ControllerId);Restore(done.State);
    }

    [Fact]
    public async Task LethalSpellDamageUsesTheSameRecall()
    {var done=await Open(PositionFor());Assert.Contains("D",done.State.PlayerZones["P2"].Base);Assert.Equal(1,done.State.CardObjects["S"].Damage);Restore(done.State);}

    [Fact]
    public async Task SameSourceCanProtectAgainOnALaterDestructionEvent()
    {
        var s=At(At(PositionFor(),"S","P2","BASE"),"D","P2","BASE");
        var first=await Open(s,"OGN·229/298","D");var second=await CastAnother(first.State,"SECOND","D");
        Assert.Contains("D",second.State.PlayerZones["P2"].Base);Assert.Single(second.Events,e=>e.Kind=="UNIT_RECALLED_TO_BASE");Restore(second.State);
    }

    [Fact]
    public async Task LaterIndependentEventCannotUseTheRecalledSourcesOldLocation()
    {
        var s=At(Source(PositionFor(),"SFD·173/221","S2"),"S2","P2","BATTLEFIELD","BF");
        s=s with {RunePools=new Dictionary<string,RunePool>(s.RunePools){["P1"]=new(40,20)}};
        var first=await Open(s,"OGN·229/298","S");Assert.Contains("S",first.State.PlayerZones["P2"].Base);
        var second=await CastAnother(first.State,"SECOND","S2");Assert.Contains("S2",second.State.PlayerZones["P2"].Graveyard);
        var third=await CastAnother(second.State,"THIRD","D");Assert.Contains("D",third.State.PlayerZones["P2"].Graveyard);Restore(third.State);
    }

    [Fact]
    public async Task ActualCombatDeathRecallsLowerPowerDefenderBeforeControlIsAwarded()
    {
        var s=PositionFor();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["A"]=s.CardObjects["A"] with {Power=7},["D"]=s.CardObjects["D"] with {Damage=0}}};
        var moved=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var focus=await Act(moved.State,"P1",new PassFocusCommand());focus=await Act(focus.State,"P2",new PassFocusCommand());
        var battle=await Act(focus.State,"P1",new DeclareBattleCommand("BF",["A"],["D","S"],["COMBAT_ASSIGNMENT"]));
        var done=await CombatTestDriver.FinishAsync(battle);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base);Assert.Contains("S",done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("P1",done.State.BattlefieldStates["BF"].ControllerId);Assert.False(done.State.BattleState.IsActive);Restore(done.State);
    }

    [Fact]
    public async Task MandatoryRecallRejectsDeclineForgedOptionsAndForeignPlayer()
    {
        var opened=await Open(PositionFor(true),"UNL-180/219");var state=opened.State;
        Assert.NotNull(state.PendingRuleChoice);var hash=MatchStateHasher.Hash(state);
        foreach(var (player,choice) in new[]{("P2","DECLINE"),("P2","LOCAL_RECALL:S:S"),("P1","LOCAL_RECALL:D:S"),("P2","LOCAL_RECALL:D:UNKNOWN")}) {
            var result=await new CoreRuleEngine().ResolveAsync(state,new("bad",player,CommandTypes.PayCost),
                new PayCostCommand(state.PendingRuleChoice.Request.Id,"RULE_REPLACEMENT",[choice]),default);
            Assert.False(result.Accepted);Assert.Equal(hash,MatchStateHasher.Hash(result.State));Assert.Empty(result.Events);
        }
    }

    [Theory]
    [InlineData(2,true)] [InlineData(3,false)]
    public async Task PowerChangedByARealSpellIsUsedForComparison(int power,bool saved)
    {
        var s=PositionFor();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["D"]=s.CardObjects["D"] with {Power=power},
            ["P1-DRAW"]=new("P1-DRAW",cardNo:"OGN·012/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {MainDeck=["P1-DRAW"]}}};
        var weakened=await Open(s,"OGN·095/298","S");Assert.Equal(3,weakened.State.CardObjects["S"].Power);
        Assert.Equal(MatchStatuses.InProgress,weakened.State.Status);Assert.Contains("P1-DRAW",weakened.State.PlayerZones["P1"].Hand);
        var done=await CastAnother(weakened.State,"SECOND","D");Assert.Equal(saved,done.State.PlayerZones["P2"].Base.Contains("D"));
        Assert.Equal(!saved,done.State.PlayerZones["P2"].Graveyard.Contains("D"));Restore(done.State);
    }

    [Fact]
    public async Task OpposingSorakaAtTheSameBattlefieldCannotSaveDefender()
    {
        var s=At(PositionFor(),"S","P1","BASE");
        var moved=await Act(s,"P1",new MoveUnitCommand("S","BASE","BATTLEFIELD:BF",[]));
        var focus=await Act(moved.State,"P1",new PassFocusCommand());focus=await Act(focus.State,"P2",new PassFocusCommand());
        var battle=await Act(focus.State,"P1",new DeclareBattleCommand("BF",["S"],["D"],["COMBAT_ASSIGNMENT"]));
        var done=await CombatTestDriver.FinishAsync(battle);Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="UNIT_RECALLED_TO_BASE");Restore(done.State);
    }

    [Theory]
    [InlineData("local",true)] [InlineData("gear",true)] [InlineData("sett",true)] [InlineData("temporary",true)]
    [InlineData("local",false)] [InlineData("gear",false)] [InlineData("sett",false)] [InlineData("temporary",false)]
    public async Task RecallKeepsAttachedArmamentWithHostWithoutMovingOrChangingItsState(string family,bool exhausted)
    {
        var s=family=="local" ? PositionFor() : NoReplacement();
        if(family=="gear") s=Gear(s);
        if(family=="sett") s=s with {RunePools=new Dictionary<string,RunePool>(s.RunePools){["P2"]=new(7,1)}};
        if(family=="temporary") s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {UntilEndOfTurnEffects=["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"]}}};
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["ARMAMENT"]=new("ARMAMENT",cardNo:"SFD·022/221",ownerId:"P2",controllerId:"P2",isExhausted:exhausted,
                tags:[CardObjectTags.EquipmentCard,"武装","灵便"],attachedToObjectId:"D")},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {Battlefields=[..s.PlayerZones["P2"].Battlefields,"ARMAMENT"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["ARMAMENT"]=new("P2","BATTLEFIELD","BF")}};
        var done=await Open(s,"OGN·229/298","D");if(family=="sett") done=await Choose(done.State);
        Assert.Contains("D",done.State.PlayerZones["P2"].Base);Assert.Contains("ARMAMENT",done.State.PlayerZones["P2"].Base);
        Assert.Equal("D",done.State.CardObjects["ARMAMENT"].AttachedToObjectId);Assert.Equal(exhausted,done.State.CardObjects["ARMAMENT"].IsExhausted);
        Assert.Equal("BASE",done.State.ObjectLocations["ARMAMENT"].Zone);
        Assert.DoesNotContain(done.Events,e=>e.Kind is "UNIT_MOVED" or "EQUIPMENT_DETACHED");Restore(done.State);
    }

    [Theory]
    [InlineData(true,false)] [InlineData(false,false)] [InlineData(true,true)]
    public async Task CombatRolePowerBonusesParticipateInTheComparison(bool sourceBonus,bool stunned)
    {
        var s=PositionFor();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["A"]=s.CardObjects["A"] with {Power=20},
            ["S"]=s.CardObjects["S"] with {Tags=sourceBonus ? [CardObjectTags.UnitCard,"坚守2"] : [CardObjectTags.UnitCard], UntilEndOfTurnEffects=stunned ? ["STUNNED"] : []},
            ["D"]=s.CardObjects["D"] with {Damage=0,Power=sourceBonus ? 5 : 2,
                Tags=sourceBonus ? [CardObjectTags.UnitCard] : [CardObjectTags.UnitCard,"坚守3"]}}};
        var moved=await Act(s,"P1",new MoveUnitCommand("A","BASE","BATTLEFIELD:BF",[]));
        var focus=await Act(moved.State,"P1",new PassFocusCommand());focus=await Act(focus.State,"P2",new PassFocusCommand());
        var battle=await Act(focus.State,"P1",new DeclareBattleCommand("BF",["A"],["D","S"],["COMBAT_ASSIGNMENT"]));
        var done=await CombatTestDriver.FinishAsync(battle);
        Assert.Equal(sourceBonus,done.State.PlayerZones["P2"].Base.Contains("D"));
        Assert.Equal(!sourceBonus,done.State.PlayerZones["P2"].Graveyard.Contains("D"));Restore(done.State);
    }

    [Fact]
    public async Task SteadfastDoesNotAddPowerOutsideItsCombatRole()
    {
        var s=At(At(PositionFor(),"S","P2","BASE"),"D","P2","BASE");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["S"]=s.CardObjects["S"] with {Tags=[CardObjectTags.UnitCard,"坚守2"]},["D"]=s.CardObjects["D"] with {Power=5}}};
        var done=await Open(s,"OGN·229/298","D");Assert.Contains("D",done.State.PlayerZones["P2"].Graveyard);Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CurrentPowerIncludesSameLocationAuraOutsideCombat(bool inBase)
    {
        var s=PositionFor();if(inBase) s=At(At(s,"S","P2","BASE"),"D","P2","BASE");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {
            CardNo="OGS·013/024",Power=4,Damage=0,Tags=[CardObjectTags.UnitCard]}}};
        // Garen gives the other friendly unit +1: Soraka is 5, Garen is 4.
        var done=await Open(s,"OGN·229/298","D");Assert.Contains("D",done.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="UNIT_DESTROYED");Restore(done.State);
    }

}
