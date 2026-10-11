using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.EquipChainTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class AttachmentObserverTests
{
    internal static MatchState Jax(string card="SFD·119/221")
    {
        var s=Position();return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["HOST"]=s.CardObjects["HOST"] with {CardNo=card,Power=3}}};
    }
    internal static async Task<ResolutionResult> Equipped(MatchState s) => await EquipTestDriver.FinishAsync(
        await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"])));
    internal static async Task<ResolutionResult> Pay(MatchState s,string choice="PAY")
    {
        var p=Assert.IsType<PendingPaymentState>(s.PendingPayment);
        if(choice=="PAY")choice=p.LegalPaymentChoiceIds.First(x=>x!="DECLINE");
        return await Act(s,p.PlayerId,new PayCostCommand(p.PaymentId,p.PaymentWindow,[choice]));
    }
    internal static async Task<ResolutionResult> FinishPaidDraw(ResolutionResult paid)
    {
        Assert.True(paid.Accepted,paid.ErrorMessage);Assert.DoesNotContain(paid.Events,e=>e.Kind=="CARD_DRAWN");
        var item=Assert.Single(paid.State.StackItems,i=>i.AttachmentObserver is not null);
        var hand=paid.State.PlayerZones[item.ControllerId].Hand.ToArray();var result=paid;var events=paid.Events.ToList();
        for(var n=0;result.State.StackItems.Any(i=>i.StackItemId==item.StackItemId);n++) {
            Assert.True(n<8);result=await Act(result.State,result.State.PriorityPlayerId!,new PassPriorityCommand());events.AddRange(result.Events);
            if(result.State.StackItems.Any(i=>i.StackItemId==item.StackItemId))Assert.Equal(hand,result.State.PlayerZones[item.ControllerId].Hand);
        }
        return result with {Events=events};
    }
    [Theory] [InlineData("SFD·119/221")] [InlineData("SFD·119a/221")]
    public async Task AttachmentDrawPaysBeforeResponsesAndOnlyDrawsOnResolution(string card)
    {
        var opened=await Equipped(Jax(card));var hand=opened.State.PlayerZones["P1"].Hand.Count;
        var mana=opened.State.RunePools["P1"].Mana;Assert.NotNull(opened.State.PendingPayment);
        var paid=await Pay(opened.State);Assert.Null(paid.State.PendingPayment);
        Assert.Equal(mana-1,paid.State.RunePools["P1"].Mana);
        Assert.Equal(hand,paid.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(paid.State.StackItems);Assert.DoesNotContain(paid.Events,e=>e.Kind=="CARD_DRAWN");
        PublicRecovery(paid.State);Restore(paid.State);
        var done=await Top(paid.State);Assert.Equal(hand+1,done.State.PlayerZones["P1"].Hand.Count);
        Assert.Contains(done.Events,e=>e.Kind=="CARD_DRAWN");PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task TakeUpReattachesFromAnotherHostAtTheNewLocationAndCapturesDrawTrigger()
    {
        var s=DeathObserverAuditTests.Source(Jax(),"OGN·001/298","SECOND-HOST","P1");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["GEAR"]=s.CardObjects["GEAR"] with {AttachedToObjectId="SECOND-HOST"}}};
        s=Response(At(s,"HOST","P1","BATTLEFIELD","F"),"SFD·011/221");
        var spell=await Act(s,"P1",new PlayCardCommand("RESPONSE","SFD·011/221",["HOST","GEAR"]));
        var done=await Top(spell.State);Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("F",done.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Single(done.Events,e=>e.Kind=="EQUIPMENT_DETACHED");Assert.Single(done.Events,e=>e.Kind=="EQUIPMENT_ATTACHED");
        Assert.NotNull(done.State.PendingPayment);Assert.DoesNotContain(done.Events,e=>e.Kind is "UNIT_MOVED" or "EQUIPMENT_MOVED_WITH_UNIT");
        PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task AgileDirectPlayAttachesAtBattlefieldAndCapturesDrawTrigger()
    {
        var s=At(Jax(),"HOST","P1","BATTLEFIELD","F");s=s with {
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Base=s.PlayerZones["P1"].Base.Where(x=>x!="GEAR").ToArray(),Hand=["GEAR"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["GEAR"]=new("P1","HAND")}};
        var spell=await Act(s,"P1",new PlayCardCommand("GEAR","SFD·022/221",["HOST"]));
        var done=await Top(spell.State);Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("F",done.State.ObjectLocations["GEAR"].BattlefieldObjectId);Assert.NotNull(done.State.PendingPayment);
        PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task ActualDetachAndGaleResponsesCannotEraseAPaidAttachmentDraw()
    {
        var s=Response(At(Jax(),"HOST","P1","BATTLEFIELD","F"),"SFD·011/221","P2");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["P2-DRAW"]=new("P2-DRAW",cardNo:"OGN·001/298",ownerId:"P2",controllerId:"P2",power:2,tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {MainDeck=["P2-DRAW"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["P2-DRAW"]=new("P2","MAIN_DECK")}};
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["GALE"]=new("GALE",cardNo:"OGN·169/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {Hand=[..s.PlayerZones["P2"].Hand,"GALE"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["GALE"]=new("P2","HAND")}};
        var opened=await Equipped(s);var hand=opened.State.PlayerZones["P1"].Hand.Count;var paid=await Pay(opened.State);
        var pass=await Act(paid.State,"P1",new PassPriorityCommand());
        var detach=await Act(pass.State,"P2",new PlayCardCommand("RESPONSE","SFD·011/221",["HOST","GEAR"]));
        var detached=await Top(detach.State);Assert.Null(detached.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(3,Might(detached.State));PublicRecovery(detached.State);Restore(detached.State);
        pass=await Act(detached.State,"P1",new PassPriorityCommand());
        var gale=await Act(pass.State,"P2",new PlayCardCommand("GALE","OGN·169/298",["HOST"]));
        var returned=await Top(gale.State);Assert.Contains("HOST",returned.State.PlayerZones["P1"].Hand);
        PublicRecovery(returned.State);Restore(returned.State);
        var done=await Top(returned.State);Assert.Equal(hand+2,done.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(done.Events,e=>e.Kind=="CARD_DRAWN");PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task OpponentTakeUpCannotTriggerTheOtherPlayersAttachmentDraw()
    {
        var s=Response(At(At(Jax(),"HOST","P2","BASE"),"GEAR","P2","BASE"),"SFD·011/221");
        var spell=await Act(s,"P1",new PlayCardCommand("RESPONSE","SFD·011/221",["HOST","GEAR"]));
        var done=await Top(spell.State);Assert.Equal("HOST",done.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(done.State.PendingPayment);Assert.Empty(done.State.StackItems);
        Assert.Empty(done.State.PlayerZones["P2"].Hand);PublicRecovery(done.State);Restore(done.State);
    }
    [Theory] [InlineData("DECLINE")] [InlineData("insufficient")]
    public async Task AttachmentDrawCanBeDeclinedOrLeaveAnInsufficientPaymentUnchanged(string choice)
    {
        var s=Jax();if(choice=="insufficient")s=s with {RunePools=new Dictionary<string,RunePool>(s.RunePools){["P1"]=new(0,20)}};
        var opened=await Equipped(s);var p=opened.State.PendingPayment!;
        var result=await new CoreRuleEngine().ResolveAsync(opened.State,new("decline","P1",CommandTypes.PayCost),
            new PayCostCommand(p.PaymentId,p.PaymentWindow,[choice=="DECLINE"?"DECLINE":"PAY"]),default);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        if(choice=="DECLINE") { Assert.True(result.Accepted);Assert.Null(result.State.PendingPayment);Assert.Empty(result.State.StackItems); }
        else { Assert.False(result.Accepted);Assert.Equal(ErrorCodes.InsufficientCost,result.ErrorCode);Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(result.State)); }
        PublicRecovery(result.State);Restore(result.State);
    }
    [Fact]
    public async Task EquipResolvingAfterTakeUpAttachedToTheSameHostDoesNotCreateAnotherTrigger()
    {
        var s=Response(Jax(),"SFD·011/221");
        var equip=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        var spell=await Act(equip.State,"P1",new PlayCardCommand("RESPONSE","SFD·011/221",["HOST","GEAR"]));
        var attached=await Top(spell.State);Assert.NotNull(attached.State.PendingPayment);
        var declined=await Pay(attached.State,"DECLINE");Assert.Single(declined.State.StackItems);
        var done=await Top(declined.State);Assert.Null(done.State.PendingPayment);Assert.Empty(done.State.StackItems);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="EQUIPMENT_ATTACHED");PublicRecovery(done.State);Restore(done.State);
    }
    [Fact]
    public async Task GearControlChangeKeepsTheCapturedEquippingPlayerAsTheTriggerActor()
    {
        var s=At(Jax(),"HOST","P1","BATTLEFIELD","F");
        var equip=await Act(s,"P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_RED"]));
        // Isolate a same-object control-change response from the spell granting it.
        var changed=At(equip.State,"GEAR","P2","BASE");
        var attached=await Top(changed);Assert.Equal("P1",attached.State.PendingPayment!.PlayerId);
        Assert.Equal("P2",attached.State.CardObjects["GEAR"].ControllerId);PublicRecovery(attached.State);Restore(attached.State);
        var done=await Top((await Pay(attached.State)).State);
        Assert.Single(done.State.PlayerZones["P1"].Hand);Assert.Empty(done.State.PlayerZones["P2"].Hand);
        Assert.Equal("P2",done.State.CardObjects["GEAR"].ControllerId);PublicRecovery(done.State);Restore(done.State);
    }
    [Theory] [InlineData("missing")] [InlineData("generation")] [InlineData("card")] [InlineData("actor")] [InlineData("gear")] [InlineData("future-source")] [InlineData("future-gear")]
    public async Task RecoveryRejectsForgedCapturedAttachmentMetadata(string change)
    {
        var opened=await Equipped(Jax());var item=Assert.Single(opened.State.StackItems);var c=item.AttachmentObserver!;
        var invalid=change switch {
            "missing"=>null,"generation"=>c with {SourceGeneration=-1},"card"=>c with {CardNo="OGN·001/298"},
            "actor"=>c with {AttachingPlayerId="P2"},"future-source"=>c with {SourceGeneration=999},
            "future-gear"=>c with {Equipment=c.Equipment with {Generation=999}},_=>c with {Equipment=c.Equipment with {ObjectId="HOST"}}};
        var forged=opened.State with {StackItems=[item with {AttachmentObserver=invalid}]};
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }
}
