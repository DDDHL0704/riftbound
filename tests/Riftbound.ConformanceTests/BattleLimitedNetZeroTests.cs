using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedNetZeroTests
{
    [Fact]
    public async Task ZeroNetLedgerProjectsAndExpiresEachRealDuration()
    {
        var initial = Position(); var run = new Driver(initial);
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds));await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));await run.Top();Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
        for(var i=1;i<=3;i++) {
            if(run.State.FocusPlayerId!="P1")await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());Assert.Equal("P1",run.State.FocusPlayerId);
            if(i==1){var funding=Enumerable.Range(1,4).Select(n=>"P1-FUND-"+n).ToArray();await run.Act("P1",new TapRuneCommand(funding[0],funding));}
            await run.Act("P1",new PlayCardCommand("DEBUFF-"+i,"OGN·095/298",["ATTACKER"]));await run.Top();
            Assert.Equal(6-i,run.State.CardObjects["ATTACKER"].Power);
        }
        var card=run.State.CardObjects["ATTACKER"];Assert.Equal(0,card.UntilEndOfTurnPowerModifier);Assert.Equal(4,card.UntilEndOfTurnPowerModifiers.Count);
        var publicRows=run.State.ContinuousEffects.Where(e=>e.Layer=="POWER_MODIFIER"&&e.TargetObjectId=="ATTACKER").ToArray();
        Assert.Equal(4,publicRows.Length);Assert.Equal(1,publicRows.Count(e=>e.Duration=="UNTIL_END_OF_BATTLE"));Assert.Equal(3,publicRows.Count(e=>e.Duration=="UNTIL_END_OF_TURN"));
        for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
        await run.Act("P2",new DeclareBattleCommand("HOME",["ATTACKER"],["HOST"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());}
        Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");Assert.Equal(0,run.State.CardObjects["ATTACKER"].Power);
        Assert.Equal(-3,run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifier);Assert.Equal(3,run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers.Count);
        Assert.All(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers,p=>Assert.Equal("UNTIL_END_OF_TURN",p.Duration));
        await run.Act("P2",new EndTurnCommand());Assert.Equal(3,run.State.CardObjects["ATTACKER"].Power);Assert.Empty(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers);await run.Replay();
    }

    // Constructed component/projection control only: no accepted birth or recovery-history claim.
    // Both rows are ordinary EOT; no battle grant is needed to expose a nonempty net-zero ledger.
    [Fact]
    public void OrdinaryEotOnlyZeroNetLedgerKeepsBothPublicRowsWithoutBattleMarkers()
    {
        var initial=BattleLimitedTestSupport.Position("plain");
        var plus=new PowerModifierLedgerEntry("ORDINARY_EOT_PLUS","ORDINARY_PLUS","UNTIL_END_OF_TURN","HOST","HOST","OGN·219/298",2,4,6,"EXPLICIT_COMPONENT_FIXTURE",2,0,6,1);
        var minus=new PowerModifierLedgerEntry("ORDINARY_EOT_MINUS","ORDINARY_MINUS","UNTIL_END_OF_TURN","HOST","HOST","OGN·219/298",-2,4,4,"EXPLICIT_COMPONENT_FIXTURE",-2,0,4,2);
        var host=OfficialGarenConquestBattlefieldCountTests.Printed("HOST","OGN·219/298","P1") with{
            ObjectGeneration=1,UntilEndOfTurnPowerModifier=0,UntilEndOfTurnPowerModifiers=[plus,minus]};
        var state=initial with{
            CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["HOST"]=host},
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with{
                Hand=initial.PlayerZones["P1"].Hand.Where(id=>id!="HOST").ToArray(),Base=["HOST"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["HOST"]=new("P1","BASE")}};
        Assert.Empty(state.UntilEndOfTurnEffects);Assert.Empty(host.UntilEndOfTurnEffects);Assert.Equal(0,host.UntilEndOfTurnPowerModifier);
        var rows=state.ContinuousEffects.Where(e=>e.Layer=="POWER_MODIFIER"&&e.TargetObjectId=="HOST").ToArray();
        Assert.Equal(2,rows.Length);Assert.Equal(new[]{2,-2},rows.Select(e=>e.PowerDelta).ToArray());
        Assert.All(rows,row=>Assert.Equal("UNTIL_END_OF_TURN",row.Duration));
        foreach(var snapshot in ResolutionResult.BuildSnapshots(state).Values) {
            var wire=JsonSerializer.Serialize(snapshot);Assert.Contains("ORDINARY_EOT_PLUS",wire);Assert.Contains("ORDINARY_EOT_MINUS",wire);
        }
        ArmamentMightTests.PublicRecovery(state);
    }

    private static MatchState Position()
    {
        var state=BattleLimitedTestSupport.Position("plain");var cards=new Dictionary<string,CardObjectState>(state.CardObjects){["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","SFD·110/221","P2"),["P2-ORANGE"]=OfficialGarenConquestBattlefieldCountTests.Printed("P2-ORANGE","UNL-R04","P2")};var zones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with{Base=[..state.PlayerZones["P2"].Base,"P2-ORANGE"]}};var locations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["P2-ORANGE"]=new("P2","BASE")};
        foreach(var i in Enumerable.Range(1,3)){var id="DEBUFF-"+i;cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,"OGN·095/298","P1");zones["P1"]=zones["P1"] with{Hand=[..zones["P1"].Hand,id]};locations[id]=new("P1","HAND");}
        foreach(var i in Enumerable.Range(1,4)){var id="P1-FUND-"+i;cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,"UNL-R06","P1");zones["P1"]=zones["P1"] with{Base=[..zones["P1"].Base,id]};locations[id]=new("P1","BASE");}
        return state with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
