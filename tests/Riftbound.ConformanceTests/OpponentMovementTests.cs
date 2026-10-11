using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.ChosenMovementTests;

namespace Riftbound.ConformanceTests;
public sealed class OpponentMovementTests
{
    internal static MatchState Position(string card = "OGN·158/298", string location = "BF", int movedCount = 1)
    {
        var s = MovementSimpleTriggerTests.Position("UNL-008/219");
        if (movedCount > 1) {
            s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) { ["D2"] = s.CardObjects["D"] with { ObjectId = "D2" } } };
            s = At(s,"D2","P2","BATTLEFIELD","BF");
        }
        s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["V"] = new("V",cardNo:card,power:10,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) } };
        return At(s,"V","P2",location=="BASE"?"BASE":"BATTLEFIELD",location=="BASE"?null:location);
    }

    [Theory]
    [InlineData("OGN·158/298","BF",true)]
    [InlineData("OGN·158a/298","BF",true)]
    [InlineData("OGN·158/298","BF2",false)]
    [InlineData("OGN·158/298","BASE",true)]
    public async Task OpponentActorAndPostMoveLocationDetermineTheTrigger(string card,string location,bool expected)
    {
        var moved = await MovementObserverAuditTests.Move(Position(card,location));
        Assert.DoesNotContain("DRAW-TOP",moved.State.PlayerZones["P2"].Hand);
        if (!expected) { Assert.Empty(moved.State.StackItems); return; }
        var item = Assert.Single(moved.State.StackItems); Assert.Equal("V",item.SourceObjectId);
        Assert.Equal("P2",item.ControllerId); Assert.Equal("P1",item.MovementObserver!.Unit.MovingPlayerId);
        // The opponent moved my unit: the performer's identity matters, not the unit's controller.
        Assert.Equal("P2",item.MovementObserver.Unit.ControllerId);
        var done = await Top(moved.State); Assert.Contains("DRAW-TOP",done.State.PlayerZones["P2"].Hand); Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OneSimultaneousActionDrawsOnceRegardlessOfUnitOrder(bool reverse)
    {
        var ids = reverse ? new[]{"D2","D"} : new[]{"D","D2"};
        var moved = await Cast(Position(movedCount:2),"BATTLEFIELD:BF2",ids);
        Assert.Single(moved.State.StackItems);
        var done = await Top(moved.State);
        Assert.Contains("DRAW-TOP",done.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("DRAW-NEXT",done.State.PlayerZones["P2"].Hand); Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ObserverMovingInSameActionUsesFinalLocationForAllUnits(bool reverse)
    {
        var s = Position();
        // A previously applied -8 power modifier makes this otherwise ten-power unit legal for Tendrils.
        s = s with { CardObjects = new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["V"] = s.CardObjects["V"] with { Power=2,UntilEndOfTurnPowerModifier=-8 } } };
        var ids = reverse ? new[]{"V","D"} : new[]{"D","V"};
        var moved = await Cast(s,"BATTLEFIELD:BF2",ids);
        Assert.Empty(moved.State.StackItems); Assert.Empty(moved.State.TriggerQueue);
        Assert.Equal("BF2",moved.State.ObjectLocations["V"].BattlefieldObjectId);
        Assert.DoesNotContain("DRAW-TOP",moved.State.PlayerZones["P2"].Hand); Restore(moved.State);
    }

    [Fact]
    public async Task StandardGroupMovesAlsoCountAsOneOpponentAction()
    {
        var s = Position(movedCount:2); s=At(At(s,"D","P1","BASE"),"D2","P1","BASE");
        var moved = await Act(s,"P1",new MoveUnitCommand("D","BASE","BATTLEFIELD:BF2",[],["D","D2"]));
        var item=Assert.Single(moved.State.StackItems);Assert.Equal("V",item.SourceObjectId);
        var done=await Top(moved.State);
        Assert.Contains("DRAW-TOP",done.State.PlayerZones["P2"].Hand);Assert.DoesNotContain("DRAW-NEXT",done.State.PlayerZones["P2"].Hand);Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BaseDestinationAndOwnPerformedMovementDoNotTrigger(bool ownAction)
    {
        var s=Position();
        if(ownAction) s=At(s,"V","P1","BASE");
        var moved=await MovementObserverAuditTests.Move(s,ownAction?"BATTLEFIELD:BF2":"BASE:P2");
        Assert.Empty(moved.State.StackItems);Assert.Empty(moved.State.TriggerQueue);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EarnedOpponentDrawSurvivesSourceLeavingOrChangingController(bool gone)
    {
        var moved=await MovementObserverAuditTests.Move(Position());
        var s=At(moved.State,"V","P1","BASE");
        if(gone) s=s with {
            CardObjects=s.CardObjects.Where(p=>p.Key!="V").ToDictionary(),
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P1"]=s.PlayerZones["P1"] with {Base=s.PlayerZones["P1"].Base.Where(id=>id!="V").ToArray()} },
            ObjectLocations=s.ObjectLocations.Where(p=>p.Key!="V").ToDictionary() };
        Restore(s);var done=await Top(s);Assert.Contains("DRAW-TOP",done.State.PlayerZones["P2"].Hand);Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HiddenObserverDoesNotTrigger(bool standby)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["V"]=s.CardObjects["V"] with { IsFaceDown=!standby, Tags=standby?[CardObjectTags.UnitCard,CardObjectTags.Standby]:[CardObjectTags.UnitCard] } }};
        var moved=await MovementObserverAuditTests.Move(s);Assert.Empty(moved.State.StackItems);
    }

    [Fact]
    public async Task RecoveryRejectsMissingDuplicateOrContradictoryMovementBatch()
    {
        var moved=await Cast(Position(movedCount:2),"BATTLEFIELD:BF2","D","D2");
        var item=Assert.Single(moved.State.StackItems);var c=item.MovementObserver!;
        var batch=c.MovementBatch!.ToArray();
        foreach(var bad in new[] { c with {MovementBatch=null},c with {MovementBatch=[batch[0],batch[0]]},
            c with {SourceLocation=new("P2","BATTLEFIELD","BF2")},c with {SourceLocation=new("P1","BASE")},
            c with {MovementBatch=[batch[0],batch[1] with {MovingPlayerId="ABSENT"}]} })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(moved.State with {StackItems=[item with {MovementObserver=bad}]}),e=>e.Contains("movement observer"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ProhibitedMovesNeverSupplyFalsePostEventLocations(bool alsoBlockOtherUnit)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["V"]=s.CardObjects["V"] with {Power=2,UntilEndOfTurnPowerModifier=-8,UntilEndOfTurnEffects=[MovementRestrictionRules.ForPlayer("P1")]},
            ["D"]=s.CardObjects["D"] with {UntilEndOfTurnEffects=alsoBlockOtherUnit?[MovementRestrictionRules.ForPlayer("P1")]:[]} }};
        var moved=await Cast(s,"BATTLEFIELD:BF2","V","D");
        Assert.Equal("BF",moved.State.ObjectLocations["V"].BattlefieldObjectId);
        if(alsoBlockOtherUnit) {Assert.Empty(moved.State.StackItems);Assert.Equal("BF",moved.State.ObjectLocations["D"].BattlefieldObjectId);}
        else {
            var item=Assert.Single(moved.State.StackItems);Assert.Single(item.MovementObserver!.MovementBatch!);
            Assert.Equal("D",item.MovementObserver.Unit.ObjectId);
            Assert.Equal("BF",item.MovementObserver.SourceLocation!.BattlefieldObjectId);
            var done=await Top(moved.State);Assert.Contains("DRAW-TOP",done.State.PlayerZones["P2"].Hand);Restore(done.State);
        }
    }

}
