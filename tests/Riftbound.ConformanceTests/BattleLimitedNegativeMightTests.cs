using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
namespace Riftbound.ConformanceTests;
// CN143.2b /479.4b-c: remembered reductions outlive battle buffs;
// signed snapshot stays negative while the attempted increase is zero.
public sealed class BattleLimitedNegativeMightTests
{
    [Fact]
    public async Task RealRememberedReductionsSurviveCloseAndNextBattleDoubleRecordsSignedMight()
    {
        var initial = Position();
        // Keep typed/wire acceptance and duplicate-intent checks on every
        // command, but restore the complete journal at phase boundaries
        // instead of after every preparation command.
        var run = new Driver(initial) { RecoverAfterEveryCommand = false };
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds));await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));await run.Top();Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
        await run.ColdCheckpoint();
        for(var i=1;i<=5;i++) {
            if(run.State.FocusPlayerId!="P1")await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());Assert.Equal("P1",run.State.FocusPlayerId);
            if(i==1){var funding=Enumerable.Range(1,6).Select(n=>"P1-FUND-"+n).ToArray();await run.Act("P1",new TapRuneCommand(funding[0],funding));}
            await run.Act("P1",new PlayCardCommand("DEBUFF-"+i,"OGN·095/298",["ATTACKER"]));await run.Top();
            Assert.Equal(6-i,run.State.CardObjects["ATTACKER"].Power);
        }
        var card=run.State.CardObjects["ATTACKER"];Assert.Equal(-2,card.UntilEndOfTurnPowerModifier);Assert.Equal(6,card.UntilEndOfTurnPowerModifiers.Count);
        var publicRows=run.State.ContinuousEffects.Where(e=>e.Layer=="POWER_MODIFIER"&&e.TargetObjectId=="ATTACKER").ToArray();
        Assert.Equal(6,publicRows.Length);Assert.Equal(1,publicRows.Count(e=>e.Duration=="UNTIL_END_OF_BATTLE"));Assert.Equal(5,publicRows.Count(e=>e.Duration=="UNTIL_END_OF_TURN"));
        await run.ColdCheckpoint();
        if(run.State.FocusPlayerId!="P2")await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());
        await run.Act("P2",new PlayCardCommand("FLASH","OGS·011/024",["ATTACKER"]));await run.Top();
        Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Base);Assert.Equal(1,run.State.CardObjects["ATTACKER"].Power);
        for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
        for(var i=0;run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());}
        Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");Assert.Equal(-2,run.State.CardObjects["ATTACKER"].Power);
        Assert.Equal(-5,run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifier);Assert.Equal(5,run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers.Count);
        Assert.All(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers,p=>Assert.Equal("UNTIL_END_OF_TURN",p.Duration));
        Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Base);
        await run.ColdCheckpoint();
        await run.Act("P2",new RecycleRuneCommand("P2-PURPLE"));
        await run.Act("P2",new PlayCardCommand("WIND","OGN·173/298",["ATTACKER"],Destination:"BATTLEFIELD:HOME"));await run.Top();
        Assert.True(run.State.CardObjects["ATTACKER"].IsAttacking);Assert.Equal(-2,run.State.CardObjects["ATTACKER"].Power);
        Assert.Equal("BATTLE_DOUBLE_SELF_ATTACK",Assert.Single(run.State.StackItems).FieldContext!.Kind);
        await run.Top();Assert.Equal(-2,run.State.CardObjects["ATTACKER"].Power);
        var powerEvent=run.Events().Last(e=>e.Event.Kind=="UNIT_POWER_CHANGED").Event;
        Assert.Equal(0,powerEvent.Payload["powerDelta"]);Assert.Equal(-2,powerEvent.Payload["snapshotMight"]);
        Assert.All(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers,p=>Assert.Equal("UNTIL_END_OF_TURN",p.Duration));
        await run.ColdCheckpoint();
        await run.Replay();
    }

    private static MatchState Position()
    {
        var state=BattleLimitedTestSupport.Position("plain");var cards=new Dictionary<string,CardObjectState>(state.CardObjects){["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","SFD·110/221","P2"),["P2-ORANGE"]=OfficialGarenConquestBattlefieldCountTests.Printed("P2-ORANGE","UNL-R04","P2")};var zones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with{Base=[..state.PlayerZones["P2"].Base,"P2-ORANGE"]}};var locations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["P2-ORANGE"]=new("P2","BASE")};
        foreach(var i in Enumerable.Range(1,5)){var id="DEBUFF-"+i;cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,"OGN·095/298","P1");zones["P1"]=zones["P1"] with{Hand=[..zones["P1"].Hand,id]};locations[id]=new("P1","HAND");}
        foreach(var i in Enumerable.Range(1,6)){var id="P1-FUND-"+i;cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,"UNL-R06","P1");zones["P1"]=zones["P1"] with{Base=[..zones["P1"].Base,id]};locations[id]=new("P1","BASE");}
        foreach(var (id,no,zone) in new[]{("FLASH","OGS·011/024","HAND"),("WIND","OGN·173/298","HAND"),("P2-PURPLE","UNL-R05","BASE")}){cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,no,"P2");locations[id]=new("P2",zone);zones["P2"]=zone=="HAND"?zones["P2"] with{Hand=[..zones["P2"].Hand,id]}:zones["P2"] with{Base=[..zones["P2"].Base,id]};}
        return state with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
