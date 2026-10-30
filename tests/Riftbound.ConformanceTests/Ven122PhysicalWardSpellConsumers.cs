using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver=Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Arena=Riftbound.ConformanceTests.Ven093And122PhysicalSelfConsumers;
using Common=Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;
namespace Riftbound.ConformanceTests;
public sealed class Ven122PhysicalWardSpellConsumers {
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 [Theory]
 [InlineData("P1",false)] [InlineData("P2",false)] [InlineData("P1",true)] [InlineData("P2",true)]
 public async Task ActualEnemyMoonlightUsesZeroOrTwoWardAndTheRejectedOldIntentCannotConsumeFreshPower(string actor,bool empowered) {
  var initial=Position(actor);var game=new Driver(initial);var host=Arena.Host(actor);var enemy=Arena.Other(actor);var spell=enemy+"-MOONLIGHT";
  try {
   if(actor=="P2")await game.Act("P1",new EndTurnCommand());
   var actorRunes=Enumerable.Range(0,3).Select(i=>Arena.Rune(actor,i)).ToArray();await game.Act(actor,new TapRuneCommand(actorRunes[0],actorRunes));
   var prefix=game.Events().Length;await game.Act(actor,new PlayCardCommand(host,"VEN·122",[]));Arena.Cost(game.Events().Skip(prefix),3,0);
   if(empowered){var empowerRunes=new[]{Arena.Rune(actor,3),Arena.Rune(actor,4)};await game.Act(actor,new TapRuneCommand(empowerRunes[0],empowerRunes));prefix=game.Events().Length;await game.Act(actor,new ActivateAbilityCommand(host,"EMPOWER:0:0",[]));Arena.Cost(game.Events().Skip(prefix),2,0);Assert.False(game.State.CardObjects[host].IsEmpowered);await game.Top();}
   Assert.Equal(3,game.State.CardObjects[host].Power);Assert.Equal(empowered?4:3,Arena.Might(game.State,actor,host));var generation=game.State.CardObjects[host].ObjectGeneration;
   await game.Act(actor,new EndTurnCommand());Assert.Equal(enemy,game.State.TurnPlayerId);
   var enemyRunes=Enumerable.Range(0,7).Select(i=>Arena.Rune(enemy,i)).ToArray();await game.Act(enemy,new TapRuneCommand(enemyRunes[0],enemyRunes));
   var play=new PlayCardCommand(spell,"UNL-066/219",[host]);
   if(empowered){
    await game.Act(enemy,new RecycleRuneCommand(Arena.Rune(enemy,0)));
    var node=JsonSerializer.SerializeToNode(play,Json)!.AsObject();node["promptId"]=game.PromptFor(enemy).PromptId;node["snapshotTick"]=game.State.Tick;
    var bad=new ConformanceCommand(enemy,"paid-ward-one-short-"+actor,JsonSerializer.SerializeToElement(node,Json));
    await game.ActRetained(bad,false);Assert.Equal(7,game.State.RunePools[enemy].Mana);Assert.Equal(1,game.State.RunePools[enemy].TotalPower);Assert.Contains(spell,game.State.PlayerZones[enemy].Hand);
    await game.Act(enemy,new RecycleRuneCommand(Arena.Rune(enemy,1)));await game.ActRetained(bad,false);
    Assert.Equal(7,game.State.RunePools[enemy].Mana);Assert.Equal(2,game.State.RunePools[enemy].TotalPower);
   }
   prefix=game.Events().Length;await game.Act(enemy,play);Arena.Cost(game.Events().Skip(prefix),7,empowered?2:0);
   var paid=Assert.Single(game.Events().Skip(prefix),e=>e.Event.Kind=="COST_PAID");Assert.Equal(empowered?2:0,paid.Event.Payload["spellshieldTaxPower"]);
   Assert.Equal(0,game.State.RunePools[enemy].Mana);Assert.Equal(0,game.State.RunePools[enemy].TotalPower);
   await game.Top();Assert.Equal(-7,game.State.CardObjects[host].Power);Assert.Equal(0,Arena.Might(game.State,actor,host));
   Assert.Equal("BASE",game.State.ObjectLocations[host].Zone);Assert.Equal(0,game.State.CardObjects[host].Damage);Assert.Equal(generation,game.State.CardObjects[host].ObjectGeneration);
   Assert.Equal(-10,Assert.Single(game.State.CardObjects[host].UntilEndOfTurnPowerModifiers).PowerDelta);
   await game.Act(enemy,new EndTurnCommand());Assert.Equal(3,game.State.CardObjects[host].Power);Assert.Equal(empowered?4:3,Arena.Might(game.State,actor,host));
   Assert.Equal(empowered,game.State.CardObjects[host].IsEmpowered);Assert.Equal(generation,game.State.CardObjects[host].ObjectGeneration);Common.AssertTwelveEach(game.State);await game.Replay();
  } finally {await Arena.Save("paid-moonlight-"+actor+"-"+empowered,initial,game);}
 }
 [Theory]
 [InlineData("P1",false)] [InlineData("P2",false)] [InlineData("P1",true)] [InlineData("P2",true)]
 public async Task ActualPaidOrbAbilityCommitsWardBeforeExhaustAndRejectedIntentCannotSpendLaterPower(string actor, bool empowered)
 {
  var initial = Position(actor, ability: true); var game = new Driver(initial);
  var host = Arena.Host(actor); var enemy = Arena.Other(actor); var orb = enemy + "-ORB";
  try
  {
   if (actor == "P2") await game.Act("P1", new EndTurnCommand());
   var actorRunes = Enumerable.Range(0, 3).Select(i => Arena.Rune(actor, i)).ToArray(); await game.Act(actor, new TapRuneCommand(actorRunes[0], actorRunes));
   var prefix = game.Events().Length; await game.Act(actor, new PlayCardCommand(host, "VEN·122", []));
   Arena.Cost(game.Events().Skip(prefix), 3, 0);
   if (empowered)
   {
    var empowerRunes = new[] { Arena.Rune(actor, 3), Arena.Rune(actor, 4) }; await game.Act(actor, new TapRuneCommand(empowerRunes[0], empowerRunes));
    prefix = game.Events().Length; await game.Act(actor, new ActivateAbilityCommand(host, "EMPOWER:0:0", []));
    Arena.Cost(game.Events().Skip(prefix), 2, 0); await game.Top();
   }
   Assert.Equal(empowered ? 4 : 3, Arena.Might(game.State, actor, host));
   var generation = game.State.CardObjects[host].ObjectGeneration;
   await game.Act(actor, new EndTurnCommand());
   await game.Act(enemy, new TapRuneCommand(Arena.Rune(enemy, 0))); prefix = game.Events().Length;
   await game.Act(enemy, new PlayCardCommand(orb, OrbOfRegretTests.Card, []));
   Arena.Cost(game.Events().Skip(prefix), 1, 0); Assert.False(game.State.CardObjects[orb].IsExhausted);
   var ability = new ActivateAbilityCommand(orb, OrbOfRegretTests.Ability, [host]);
   if (empowered)
   {
    await game.Act(enemy, new RecycleRuneCommand(Arena.Rune(enemy, 0)));
    var node = JsonSerializer.SerializeToNode(ability, Json)!.AsObject();
    node["promptId"] = game.PromptFor(enemy).PromptId; node["snapshotTick"] = game.State.Tick;
    var bad = new ConformanceCommand(enemy, "paid-orb-one-short-" + actor, JsonSerializer.SerializeToElement(node, Json));
    await game.ActRetained(bad, false);
    Assert.False(game.State.CardObjects[orb].IsExhausted); Assert.Equal(1, game.State.RunePools[enemy].TotalPower);
    Assert.Equal(4, Arena.Might(game.State, actor, host));
    await game.Act(enemy, new RecycleRuneCommand(Arena.Rune(enemy, 1))); await game.ActRetained(bad, false);
    Assert.False(game.State.CardObjects[orb].IsExhausted); Assert.Equal(2, game.State.RunePools[enemy].TotalPower);
   }
   prefix = game.Events().Length; await game.Act(enemy, ability);
   Arena.Cost(game.Events().Skip(prefix), 0, empowered ? 2 : 0);
   Assert.True(game.State.CardObjects[orb].IsExhausted); Assert.Equal(3, game.State.CardObjects[host].Power);
   Assert.Equal(empowered ? 4 : 3, Arena.Might(game.State, actor, host));
   Assert.Equal(0, game.State.RunePools[enemy].Mana); Assert.Equal(0, game.State.RunePools[enemy].TotalPower);
   await game.Top(); Assert.Equal(2, game.State.CardObjects[host].Power);
   Assert.Equal(empowered ? 3 : 2, Arena.Might(game.State, actor, host));
   Assert.Equal(-1, Assert.Single(game.State.CardObjects[host].UntilEndOfTurnPowerModifiers).PowerDelta);
   await game.Act(enemy, new EndTurnCommand()); Assert.Equal(3, game.State.CardObjects[host].Power);
   Assert.Equal(empowered ? 4 : 3, Arena.Might(game.State, actor, host));
   Assert.Equal(generation, game.State.CardObjects[host].ObjectGeneration);
   Common.AssertTwelveEach(game.State); await game.Replay();
  }
  finally { await Arena.Save("paid-orb-" + actor + "-" + empowered, initial, game); }
 }

 private static MatchState Position(string actor, bool ability = false)
 {
  var state = Arena.Position("VEN·122"); var enemy = Arena.Other(actor);
  var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
  var id = enemy + (ability ? "-ORB" : "-MOONLIGHT");
  cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, ability ? OrbOfRegretTests.Card : "UNL-066/219", enemy);
  locations[id] = new(enemy, "HAND"); zones[enemy] = zones[enemy] with { Hand = [..zones[enemy].Hand, id] };
  for (var i = 0; i < 12; i++)
  {
   var rune = Arena.Rune(enemy, i); cards[rune] = OfficialGarenConquestBattlefieldCountTests.Printed(rune, "UNL-R03", enemy);
  }
  return state with { RoomId = ability ? "PAID-122-WARD-ORB" : "PAID-122-WARD-MOONLIGHT", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
 }
}
