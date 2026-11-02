using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattleLimitedExpiryDamageTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualResultAndControlResponsesKeepNewDamageUntilExpiryRunsNormalLethalCleanup(bool duringControl)
    {
        var initial=Position(duringControl); var run=new Driver(initial);
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME")); await run.Act("P1",new EndTurnCommand());
        var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray(); await run.Act("P2",new TapRuneCommand(funds[0],funds));
        await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));
        await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"])); await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[])); await run.Top();
        Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
        if(run.State.FocusPlayerId!="P1") await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());
        var funding=Enumerable.Range(1,6).Select(i=>"P1-FUND-"+i).ToArray(); await run.Act("P1",new TapRuneCommand(funding[0],funding));
        foreach(var i in Enumerable.Range(1,2)) await run.Act("P1",new RecycleRuneCommand("P1-ORANGE-"+i));
        await BattleLimitedTestSupport.PassFocus(run); await BattleLimitedTestSupport.Declare(run);
        Assert.Equal("RESULT_TRIGGERS",run.State.PendingBattleResult!.Stage); Assert.Equal(0,run.State.CardObjects["ATTACKER"].Damage);
        if(duringControl) {
            await run.Top(); Assert.Equal("CONTROL_TRIGGERS",run.State.PendingBattleResult!.Stage);
            var payment=Assert.IsType<PendingPaymentState>(run.State.PendingPayment);
            await run.Act("P2",new PayCostCommand(payment.PaymentId,payment.PaymentWindow,["PAY"]));
        }
        foreach(var i in Enumerable.Range(1,2)) {
            if(run.State.PriorityPlayerId!="P1") await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());
            await run.Act("P1",new PlayCardCommand("DAMAGE-"+i,"OGN·127/298",[])); await run.Top();
            Assert.Equal(duringControl?"CONTROL_TRIGGERS":"RESULT_TRIGGERS",run.State.PendingBattleResult!.Stage);
            Assert.Equal(i*2,run.State.CardObjects["ATTACKER"].Damage); Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
        }
        await run.Top(); Assert.Null(run.State.PendingBattleResult); Assert.False(run.State.BattleState.IsActive);
        Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Graveyard); Assert.Equal(2,run.State.CardObjects["ATTACKER"].ObjectGeneration);
        var events=run.Events(); var healed=Assert.Single(events,e=>e.Event.Kind=="DAMAGE_REMOVED");
        var closed=Assert.Single(events,e=>e.Event.Kind=="BATTLE_CLOSED");
        var destroyed=Assert.Single(events,e=>e.Event.Kind=="UNIT_DESTROYED"&&Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"),"ATTACKER"));
        Assert.True(healed.Sequence<closed.Sequence&&closed.Sequence<destroyed.Sequence);
        Assert.Equal("LETHAL_DAMAGE",destroyed.Event.Payload["reason"]); Assert.Equal(1,run.State.PlayerScores["P2"]);
        Assert.Equal(1,run.State.DeathLedger.Counts.GetValueOrDefault("P2"));
        Assert.DoesNotContain(events,e=>e.Event.Payload.ContainsKey("capturedUnitDeath"));
        await run.Replay(); BattleLimitedCleanupTests.Save("expiry-response-control-"+duringControl,initial,run);
    }
    private static MatchState Position(bool control)
    {
        var initial=BattleLimitedTestSupport.Position("plain"); var cards=new Dictionary<string,CardObjectState>(initial.CardObjects){["ATTACKER"]=BattleLimitedCleanupTests.Printed("ATTACKER","SFD·110/221","P2")};
        var zones=new Dictionary<string,PlayerZones>(initial.PlayerZones); var locations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations);
        void Put(string id,string no,string owner,string zone) {
            cards[id]=BattleLimitedCleanupTests.Printed(id,no,owner); locations[id]=new(owner,zone);
            zones[owner]=zone=="BASE"?zones[owner] with{Base=[..zones[owner].Base,id]}:zone=="LEGEND"?zones[owner] with{LegendZone=[..zones[owner].LegendZone,id]}:zones[owner] with{Hand=[..zones[owner].Hand,id]};
        }
        Put("P2-ORANGE","UNL-R04","P2","BASE"); Put("LEGEND","SFD·185/221","P2","LEGEND");
        foreach(var i in Enumerable.Range(1,2)){Put("DAMAGE-"+i,"OGN·127/298","P1","HAND");Put("P1-ORANGE-"+i,"UNL-R04","P1","BASE");}
        foreach(var i in Enumerable.Range(1,6)) Put("P1-FUND-"+i,"UNL-R06","P1","BASE");
        if(control) cards["HOME"]=BattleLimitedCleanupTests.Printed("HOME","SFD·218/221","P1") with{ControllerId="P1"};
        return initial with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
