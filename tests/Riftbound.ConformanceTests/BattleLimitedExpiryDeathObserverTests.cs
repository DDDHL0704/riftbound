using Riftbound.Contracts;using Riftbound.Engine;using Xunit;
using Driver=Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
namespace Riftbound.ConformanceTests;
// Real UNL068 play at BASE does not alter the battlefield's one-on-one role admission.
// A post-cleanup death must enter the existing death ledger and ordinary observer collector.
public sealed class BattleLimitedExpiryDeathObserverTests
{
 public BattleLimitedExpiryDeathObserverTests(Xunit.Abstractions.ITestOutputHelper output) => BattleLimitedTestSupport.Runtime(output);
 [Fact]
 public async Task ExpiryDeathUpdatesLedgerAndQueuesTheActualFriendlyDeathObserverOnce()
 {
var initial=BattleLimitedTestSupport.Position("plain");
var cards=new Dictionary<string,CardObjectState>(initial.CardObjects){["ATTACKER"]=BattleLimitedCleanupTests.Printed("ATTACKER","SFD·110/221","P2")};
var zones=new Dictionary<string,PlayerZones>(initial.PlayerZones);var locations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations);
void Put(string id,string no,string owner,string zone){cards[id]=BattleLimitedCleanupTests.Printed(id,no,owner);locations[id]=new(owner,zone);zones[owner]=zone=="BASE"?zones[owner] with{Base=[..zones[owner].Base,id]}:zone=="LEGEND"?zones[owner] with{LegendZone=[..zones[owner].LegendZone,id]}:zones[owner] with{Hand=[..zones[owner].Hand,id]};}
Put("P2-ORANGE","UNL-R04","P2","BASE");Put("OBSERVER","UNL-068/219","P2","HAND");Put("LEGEND","SFD·185/221","P2","LEGEND");
foreach(var i in Enumerable.Range(1,2)){Put("DAMAGE-"+i,"OGN·127/298","P1","HAND");Put("P1-ORANGE-"+i,"UNL-R04","P1","BASE");}
foreach(var i in Enumerable.Range(1,6))Put("P1-FUND-"+i,"UNL-R06","P1","BASE");
initial=initial with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
var run=new Driver(initial);

 await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
 var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds));await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));
 await run.Act("P2",new PlayCardCommand("OBSERVER","UNL-068/219",[],Destination:"BASE"));
 await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
 await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));await run.Top();Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
 if(run.State.FocusPlayerId!="P1")await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());
 var p1funds=Enumerable.Range(1,6).Select(i=>"P1-FUND-"+i).ToArray();await run.Act("P1",new TapRuneCommand(p1funds[0],p1funds));
 foreach(var i in Enumerable.Range(1,2))await run.Act("P1",new RecycleRuneCommand("P1-ORANGE-"+i));
 for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
 await run.Act("P2",new DeclareBattleCommand("HOME",["ATTACKER"],["HOST"],["COMBAT_ASSIGNMENT"]));
 Assert.Equal("RESULT_TRIGGERS",run.State.PendingBattleResult!.Stage);Assert.Equal(0,run.State.CardObjects["ATTACKER"].Damage);
 foreach(var i in Enumerable.Range(1,2)){
  if(run.State.PriorityPlayerId!="P1")await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());
  await run.Act("P1",new PlayCardCommand("DAMAGE-"+i,"OGN·127/298",[]));await run.Top();
  Assert.Equal(i*2,run.State.CardObjects["ATTACKER"].Damage);Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
 }
 await run.Top();Assert.Null(run.State.PendingBattleResult);Assert.False(run.State.BattleState.IsActive);
 Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");
 // Source 143.2b: damage earned after the one battle cleanup stays, then becomes lethal when the bounded +3 expires.
 Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Graveyard);
 Assert.Equal(1,run.State.DeathLedger.Counts.GetValueOrDefault("P2"));
 Assert.Single(run.Events(),e=>e.Event.Kind=="TRIGGER_QUEUED"&&Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"),"OBSERVER"));
 await run.Top();Assert.Equal(7,run.State.CardObjects["OBSERVER"].Power);
 Assert.DoesNotContain(run.Events(),e=>e.Event.Payload.ContainsKey("capturedUnitDeath"));
 await run.Replay();BattleLimitedCleanupTests.Save("expiry-death-observer",initial,run);

 }
}
