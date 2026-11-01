using Riftbound.Contracts;using Riftbound.Engine;using Xunit;
using Driver=Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
namespace Riftbound.ConformanceTests;
// Current CN Guardian image: mandatory replacement. Two physically played/assembled Guardians
// create the actual competing replacement choice. Each contributes1 Might before the Fio snapshot.
public sealed class BattleLimitedExpiryReplacementTests
{
 public BattleLimitedExpiryReplacementTests(Xunit.Abstractions.ITestOutputHelper output) => BattleLimitedTestSupport.Runtime(output);
 [Fact]
 public async Task TwoActualGuardiansPreserveReplacementChoiceRecoveryAtBattleLimitedExpiry()
 {
var initial=BattleLimitedTestSupport.Position("plain");
var cards=new Dictionary<string,CardObjectState>(initial.CardObjects){["ATTACKER"]=BattleLimitedCleanupTests.Printed("ATTACKER","SFD·110/221","P2")};
var zones=new Dictionary<string,PlayerZones>(initial.PlayerZones);var locations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations);
void Put(string id,string no,string owner,string zone){cards[id]=BattleLimitedCleanupTests.Printed(id,no,owner);locations[id]=new(owner,zone);zones[owner]=zone=="BASE"?zones[owner] with{Base=[..zones[owner].Base,id]}:zone=="LEGEND"?zones[owner] with{LegendZone=[..zones[owner].LegendZone,id]}:zones[owner] with{Hand=[..zones[owner].Hand,id]};}
Put("P2-ORANGE","UNL-R04","P2","BASE");Put("P2-GREEN","UNL-R02","P2","BASE");Put("GEAR","SFD·051/221","P2","HAND");Put("GEAR-2","SFD·051/221","P2","HAND");Put("P2-GREEN-2","UNL-R02","P2","BASE");Put("PING","OGN·133/298","P1","HAND");Put("LEGEND","SFD·185/221","P2","LEGEND");
foreach(var i in Enumerable.Range(1,2)){Put("DAMAGE-"+i,"OGN·127/298","P1","HAND");Put("P1-ORANGE-"+i,"UNL-R04","P1","BASE");}
foreach(var i in Enumerable.Range(1,6))Put("P1-FUND-"+i,"UNL-R06","P1","BASE");
initial=initial with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
// Keep command acceptance and duplicate-intent checks for every action, while
// restoring the growing journal at the externally visible battle boundaries.
var run=new Driver(initial) { RecoverAfterEveryCommand = false };

 await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
 var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds));await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));await run.Act("P2",new RecycleRuneCommand("P2-GREEN",["P2-GREEN","P2-GREEN-2"]));
 await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));await run.Act("P2",new PlayCardCommand("GEAR","SFD·051/221",[],Destination:"BASE"));await run.Act("P2",new AssembleEquipmentCommand("GEAR","ATTACKER",["ASSEMBLE_GREEN"]));await run.Top();Assert.Equal("ATTACKER",run.State.CardObjects["GEAR"].AttachedToObjectId);await run.Act("P2",new PlayCardCommand("GEAR-2","SFD·051/221",[],Destination:"BASE"));await run.Act("P2",new AssembleEquipmentCommand("GEAR-2","ATTACKER",["ASSEMBLE_GREEN"]));await run.Top();Assert.Equal("ATTACKER",run.State.CardObjects["GEAR-2"].AttachedToObjectId);await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
 await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));await run.Top();Assert.Equal(8,run.State.CardObjects["ATTACKER"].Power);
 await run.ColdCheckpoint();
 var doubled=Assert.Single(run.Events(),e=>e.Event.Kind=="UNIT_POWER_CHANGED"&&Equals(e.Event.Payload.GetValueOrDefault("duration"),"UNTIL_END_OF_BATTLE"));
 Assert.Equal(5,Convert.ToInt32(doubled.Event.Payload["snapshotMight"]));Assert.Equal(5,Convert.ToInt32(doubled.Event.Payload["powerDelta"]));
 if(run.State.FocusPlayerId!="P1")await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());
 var p1funds=Enumerable.Range(1,6).Select(i=>"P1-FUND-"+i).ToArray();await run.Act("P1",new TapRuneCommand(p1funds[0],p1funds));
 foreach(var i in Enumerable.Range(1,2))await run.Act("P1",new RecycleRuneCommand("P1-ORANGE-"+i));
 for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
 await run.Act("P2",new DeclareBattleCommand("HOME",["ATTACKER"],["HOST"],["COMBAT_ASSIGNMENT"]));
 Assert.Equal("RESULT_TRIGGERS",run.State.PendingBattleResult!.Stage);Assert.Equal(0,run.State.CardObjects["ATTACKER"].Damage);
 await run.ColdCheckpoint(); run.RecoverAfterEveryCommand = true;
 foreach(var i in Enumerable.Range(1,2)){
  if(run.State.PriorityPlayerId!="P1")await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());
  await run.Act("P1",new PlayCardCommand("DAMAGE-"+i,"OGN·127/298",[]));await run.Top();
  Assert.Equal(i*2,run.State.CardObjects["ATTACKER"].Damage);Assert.Equal(8,run.State.CardObjects["ATTACKER"].Power);
 }
 if(run.State.PriorityPlayerId!="P1")await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());
 await run.Act("P1",new PlayCardCommand("PING","OGN·133/298",[]));await run.Top();Assert.Equal(5,run.State.CardObjects["ATTACKER"].Damage);
 await run.Top();Assert.NotNull(run.State.PendingRuleChoice);var replacement=run.State.PendingRuleChoice!;
 Assert.Equal("P2",replacement.Request.PlayerId);Assert.Contains(replacement.Request.Options,o=>o.Id=="ATTACHED_GEAR:ATTACKER:GEAR");
 await run.Act("P2",new PayCostCommand(replacement.Request.Id,"RULE_REPLACEMENT",["ATTACHED_GEAR:ATTACKER:GEAR"]));
 Assert.Null(run.State.PendingBattleResult);Assert.False(run.State.BattleState.IsActive);
 Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");
 // Source 143.2b: damage earned after the one battle cleanup stays, then becomes lethal when the bounded +5 expires (two Guardians supply2 Might).
 Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Base);Assert.Contains("GEAR",run.State.PlayerZones["P2"].Graveyard);
 Assert.Equal("ATTACKER",run.State.CardObjects["GEAR-2"].AttachedToObjectId);Assert.DoesNotContain("GEAR-2",run.State.PlayerZones["P2"].Graveyard);Assert.Equal(3,run.State.CardObjects["ATTACKER"].Power);Assert.Equal(0,run.State.CardObjects["ATTACKER"].Damage);Assert.Equal(1,run.State.CardObjects["ATTACKER"].ObjectGeneration);
 Assert.DoesNotContain(run.Events(),e=>e.Event.Kind=="UNIT_DESTROYED"&&Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"),"ATTACKER"));
 Assert.Equal(0,run.State.DeathLedger.Counts.GetValueOrDefault("P2"));
 Assert.DoesNotContain(run.Events(),e=>e.Event.Payload.ContainsKey("capturedUnitDeath"));await run.ColdCheckpoint();await run.Replay();BattleLimitedCleanupTests.Save("expiry-two-guardian-replacement",initial,run);

 }
}
