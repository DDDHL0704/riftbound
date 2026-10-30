using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class EquipChainTests
{
    [Fact]
    public async Task EquipPaysThenWaitsForBothPlayersBeforeAttaching()
    {
        var s=Position();var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        Assert.Null(activated.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(4,Might(activated.State));
        Assert.Single(activated.State.StackItems);Assert.Equal(19,activated.State.RunePools["P1"].Power);
        Assert.DoesNotContain(activated.Events,e=>e.Kind=="EQUIPMENT_ATTACHED");PublicRecovery(activated.State);Restore(activated.State);
        var passed=await Act(activated.State,"P1",new PassPriorityCommand());Assert.Null(passed.State.CardObjects["GEAR"].AttachedToObjectId);
        var done=await Act(passed.State,"P2",new PassPriorityCommand());Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(6,Might(done.State));Assert.Empty(done.State.StackItems);PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task EquipAttachmentChangesLocationWithoutMovingOrExhausting()
    {
        var s=At(Position(),"HOST","P1","BATTLEFIELD","F");
        var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        var done=await Top(activated.State);Assert.Equal("F",done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Contains("GEAR",done.State.PlayerZones["P1"].Battlefields);Assert.DoesNotContain("GEAR",done.State.PlayerZones["P1"].Base);
        Assert.False(done.State.CardObjects["GEAR"].IsExhausted);Assert.DoesNotContain(done.Events,e=>e.Kind is "UNIT_MOVED" or "EQUIPMENT_MOVED_WITH_UNIT");
        PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task HextechGauntletCostUsesCurrentMightIncludingOtherArmaments()
    {
        var s=Position("UNL-188/219",power:1);s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["SWORD"]=new("SWORD",cardNo:"SFD·022/221",ownerId:"P1",controllerId:"P1",attachedToObjectId:"HOST",tags:[CardObjectTags.EquipmentCard,CardEquipmentKeywordNames.Weapon])},
            RunePools=new Dictionary<string,RunePool>(s.RunePools){["P1"]=new(0,1)}};
        s=At(s,"SWORD","P1","BASE");Assert.Equal(3,Might(s));
        var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_3_ANY_POWER"]));
        Assert.Null(activated.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(0,activated.State.RunePools["P1"].Mana);
        var done=await Top(activated.State);Assert.Equal(6,Might(done.State));PublicRecovery(done.State);Restore(done.State);
    }

    internal static MatchState Response(MatchState s,string no,string player="P1") => s with {
        CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["RESPONSE"]=new("RESPONSE",cardNo:no,ownerId:player,controllerId:player,tags:[CardObjectTags.SpellCard])},
        PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){[player]=s.PlayerZones[player] with {Hand=[..s.PlayerZones[player].Hand.Where(id=>id!="RESPONSE"),"RESPONSE"]}},
        ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["RESPONSE"]=new(player,"HAND")},
        RunePools=new Dictionary<string,RunePool>(s.RunePools){[player]=new(20,20)}};
    [Fact]
    public async Task ActualGaleCanRemoveTheHostBeforeEquipGrantsMightAndCostsStayPaid()
    {
        var s=Response(At(Position(power:2),"HOST","P1","BATTLEFIELD","F"),"OGN·169/298","P2");
        var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        var passed=await Act(activated.State,"P1",new PassPriorityCommand());
        var response=await Act(passed.State,"P2",new PlayCardCommand("RESPONSE","OGN·169/298",["HOST"]));
        var returned=await Top(response.State);Assert.Contains("HOST",returned.State.PlayerZones["P1"].Hand);PublicRecovery(returned.State);Restore(returned.State);
        var done=await Top(returned.State);Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(19,done.State.RunePools["P1"].Power);Assert.DoesNotContain(done.Events,e=>e.Kind=="EQUIPMENT_ATTACHED");
        PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task ActualFlashMovesTheHostBeforeEquipAttachesAtItsNewLocation()
    {
        var s=Response(At(Position(),"HOST","P1","BATTLEFIELD","F"),"OGS·011/024");
        var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        var response=await Act(activated.State,"P1",new PlayCardCommand("RESPONSE","OGS·011/024",["HOST"]));
        var moved=await Top(response.State);Assert.Contains("HOST",moved.State.PlayerZones["P1"].Base);Assert.Null(moved.State.CardObjects["GEAR"].AttachedToObjectId);
        var done=await Top(moved.State);Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("BASE",done.State.ObjectLocations["GEAR"].Zone);Assert.Equal(6,Might(done.State));PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task DestroyCostQueuesLastBreathAboveEquipWithoutLosingItsDraw()
    {
        var s=DeathObserverAuditTests.Source(Position("SFD·178/221"),"UNL-172/219","COST","P1");
        var initialHand=s.PlayerZones["P1"].Hand.Count;
        var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_YELLOW","DESTROY_FRIENDLY_UNIT:COST"]));
        Assert.Contains("COST",activated.State.PlayerZones["P1"].Graveyard);Assert.Equal(2,activated.State.StackItems.Count);
        Assert.Equal("ASSEMBLE_EQUIPMENT",activated.State.StackItems[0].EffectKind);Assert.Equal("COST",activated.State.StackItems[1].SourceObjectId);
        Assert.Null(activated.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(initialHand,activated.State.PlayerZones["P1"].Hand.Count);PublicRecovery(activated.State);Restore(activated.State);
        var drawn=await Top(activated.State);Assert.Equal(initialHand+1,drawn.State.PlayerZones["P1"].Hand.Count);Assert.Null(drawn.State.CardObjects["GEAR"].AttachedToObjectId);
        var done=await Top(drawn.State);Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(8,Might(done.State));PublicRecovery(done.State);Restore(done.State);
    }
    [Theory]
    [InlineData("source-generation")] [InlineData("target-generation")] [InlineData("target-control")]
    public async Task EquipDoesNotAttachToAReplacementObjectOrOpponentControlledHost(string change)
    {
        var activated=await Act(Position(),"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));var s=activated.State;
        // Reconstructed response result isolates continuity/control from unrelated spell behavior.
        var id=change=="source-generation"?"GEAR":"HOST";
        if(change=="target-control")s=At(s,id,"P2","BASE");
        else s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){[id]=s.CardObjects[id] with {ObjectGeneration=1}}};
        var done=await Top(s);Assert.Null(done.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(19,done.State.RunePools["P1"].Power);
        PublicRecovery(done.State);Restore(done.State);
    }
    [Theory]
    [InlineData("missing")] [InlineData("generation")] [InlineData("card")]
    public async Task RecoveryRejectsForgedPrivateEquipContinuation(string change)
    {
        var activated=await Act(Position(),"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));var item=Assert.Single(activated.State.StackItems);
        var forged=item with {Equip=change=="missing"?null:change=="generation"?item.Equip! with {SourceGeneration=-1}:item.Equip! with {CardNo="OGN·001/298"}};
        var s=activated.State with {StackItems=[forged]};Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s));
    }

    [Theory]
    [InlineData("HIDDEN")] [InlineData("FORGED")]
    public async Task SpectatorCannotHideOrSubstituteAStillPublicEquipTarget(string fake)
    {
        var activated=await Act(Position(),"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        var s=activated.State;var frame=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        var payload=Assert.IsType<Dictionary<string,object?>>(Assert.Single(frame.SpectatorSnapshot.Stack));payload["targetObjectIds"]=new[]{fake};
        Assert.NotEmpty(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],new Dictionary<string,RecoveredPlayerView>(),s,s.Tick,frame));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ActualTakeUpResponseCanAttachEarlyThenEquipReattachesOrDoesNothing(bool sameHost)
    {
        var s=Response(DeathObserverAuditTests.Source(Position(),"OGN·001/298","SECOND-HOST","P1"),"SFD·011/221");
        var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        var response=await Act(activated.State,"P1",new PlayCardCommand("RESPONSE","SFD·011/221",[sameHost?"HOST":"SECOND-HOST","GEAR"]));
        var first=await Top(response.State);Assert.Equal(sameHost?"HOST":"SECOND-HOST",first.State.CardObjects["GEAR"].AttachedToObjectId);
        PublicRecovery(first.State);Restore(first.State);
        var done=await Top(first.State);Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(6,Might(done.State));
        Assert.Equal(sameHost?0:1,done.Events.Count(e=>e.Kind=="EQUIPMENT_ATTACHED"));
        Assert.Equal(sameHost?0:1,done.Events.Count(e=>e.Kind=="EQUIPMENT_DETACHED"));
        PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task EquipSourceControlChangeDoesNotChangeTheCapturedAbilityController()
    {
        var s=At(Position(),"HOST","P1","BATTLEFIELD","F");
        var activated=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        // Reconstructed control-change response: the same source remains on the field.
        var changed=At(activated.State,"GEAR","P2","BASE");
        var done=await Top(changed);Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("P2",done.State.CardObjects["GEAR"].ControllerId);Assert.Equal("P1",done.State.CardObjects["HOST"].ControllerId);
        Assert.Contains("GEAR",done.State.PlayerZones["P2"].Battlefields);Assert.Equal("F",done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(6,Might(done.State));PublicRecovery(done.State);Restore(done.State);
    }
}
