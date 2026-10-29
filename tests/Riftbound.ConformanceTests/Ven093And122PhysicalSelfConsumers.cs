using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;
namespace Riftbound.ConformanceTests;
public sealed class Ven093And122PhysicalSelfConsumers
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    [Theory]
    [InlineData("P1", "VEN·093", 4, 4, "游走")]
    [InlineData("P2", "VEN·093", 4, 4, "游走")]
    [InlineData("P1", "VEN·122", 3, 3, "法盾2")]
    [InlineData("P2", "VEN·122", 3, 3, "法盾2")]
    public async Task PrintedPlayAndIndependentNativeTwoManaResolveOnlyOnTheSameCurrentSource(string actor, string no, int printedMana, int printedMight, string keyword)
    {
        var initial = Position(no); var game = new Driver(initial); var id = Host(actor);
        try {
            if (actor == "P2") await game.Act("P1", new EndTurnCommand());
            Assert.Equal(0, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            for (var i=0;i<printedMana;i++) await game.Act(actor,new TapRuneCommand(Rune(actor,i)));
            var prefix=game.Events().Length;await game.Act(actor,new PlayCardCommand(id,no,[]));
            Cost(game.Events().Skip(prefix),printedMana,0);Assert.Equal(0,game.State.RunePools[actor].Mana);
            Assert.Equal(printedMight,Might(game.State,actor,id));Assert.Equal(printedMight,game.State.CardObjects[id].Power);
            Assert.True(game.State.CardObjects[id].IsExhausted);Assert.False(game.State.CardObjects[id].IsEmpowered);
            Assert.DoesNotContain(keyword,game.State.CardObjects[id].Tags);var generation=game.State.CardObjects[id].ObjectGeneration;
            await game.Act(actor,new TapRuneCommand(Rune(actor,printedMana)));
            var activate=new ActivateAbilityCommand(id,"EMPOWER:0:0",[]);var node=JsonSerializer.SerializeToNode(activate,Json)!.AsObject();
            node["promptId"]=game.PromptFor(actor).PromptId;node["snapshotTick"]=game.State.Tick;
            var retained=new ConformanceCommand(actor,"paid-self-one-short-"+actor+no,JsonSerializer.SerializeToElement(node,Json));
            await game.ActRetained(retained,false);Assert.Equal(1,game.State.RunePools[actor].Mana);Assert.Empty(game.State.StackItems);
            await game.Act(actor,new TapRuneCommand(Rune(actor,printedMana+1)));
            await game.ActRetained(retained,false);Assert.Equal(2,game.State.RunePools[actor].Mana);Assert.False(game.State.CardObjects[id].IsEmpowered);
            prefix=game.Events().Length;await game.Act(actor,activate);Cost(game.Events().Skip(prefix),2,0);
            Assert.Equal(0,game.State.RunePools[actor].Mana);Assert.Equal(2,Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            Assert.False(game.State.CardObjects[id].IsEmpowered);Assert.Equal(printedMight,Might(game.State,actor,id));
            Assert.True(game.State.CardObjects[id].IsExhausted);await game.Top();
            Assert.True(game.State.CardObjects[id].IsEmpowered);Assert.Equal(printedMight,game.State.CardObjects[id].Power);
            Assert.Equal(printedMight+1,Might(game.State,actor,id));Assert.Contains(keyword,game.State.CardObjects[id].RuleDerivedTags!);
            Assert.Equal(generation,game.State.CardObjects[id].ObjectGeneration);
            await Ven001PhysicalThresholdConsumerTests.Reject(game,actor,activate,"paid-self-repeat-"+actor+no);
            await game.Act(actor,new EndTurnCommand());await game.Act(Other(actor),new EndTurnCommand());
            Assert.Equal(printedMight+1,Might(game.State,actor,id));Assert.False(game.State.CardObjects[id].IsExhausted);
            if(no=="VEN·093") {
                await game.Act(actor,new MoveUnitCommand(id,"BASE","BATTLEFIELD:BF",[]));
                await Common.CompleteMovementWindow(()=>game.State,game.Act);
                await game.Act(actor,new EndTurnCommand());await game.Act(Other(actor),new EndTurnCommand());
                Assert.False(game.State.CardObjects[id].IsExhausted);
                await Ven001PhysicalThresholdConsumerTests.Reject(game,actor,new MoveUnitCommand(id,"BATTLEFIELD:BF","BATTLEFIELD:OTHER",[]),"paid-self-missing-roam-"+actor);
                await game.Act(actor,new MoveUnitCommand(id,"BATTLEFIELD:BF","BATTLEFIELD:OTHER",["ROAM"]));
                Assert.Equal("OTHER",game.State.ObjectLocations[id].BattlefieldObjectId);Assert.Equal(generation,game.State.CardObjects[id].ObjectGeneration);
                Assert.Equal(4,game.State.CardObjects[id].Power);Assert.Equal(5,Might(game.State,actor,id));
                await Common.CompleteMovementWindow(()=>game.State,game.Act);
            }
            Common.AssertTwelveEach(game.State);await game.Replay();
        } finally { await Save(actor+"-"+no,initial,game); }
    }
    internal static MatchState Position(string no) {
        var state=VenUnitEntryBasicTests.Position(no);var cards=state.CardObjects.ToDictionary();var locations=state.ObjectLocations.ToDictionary();var zones=state.PlayerZones.ToDictionary();
        cards["BF"]=cards["BF"] with {ControllerId=null};cards["OTHER"]=cards["OTHER"] with {ControllerId=null};
        cards[Host("P2")]=OfficialGarenConquestBattlefieldCountTests.Printed(Host("P2"),no,"P2");locations[Host("P2")]=new("P2","HAND");zones["P2"]=zones["P2"] with {Hand=[Host("P2")]};
        foreach(var player in new[]{"P1","P2"}) for(var i=0;i<12;i++) {var id=Rune(player,i);cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,no=="VEN·093"?"UNL-R05":"UNL-R06",player);locations[id]=new(player,"BASE");zones[player]=zones[player] with {Base=[..zones[player].Base,id]};}
        return state with {RoomId="PAID-093-122-SELF",CardObjects=cards,ObjectLocations=locations,PlayerZones=zones,RunePools=new Dictionary<string,RunePool>{["P1"]=RunePool.Empty,["P2"]=RunePool.Empty}};
    }
    internal static string Host(string p)=>p=="P1"?"UNIT":"P2-SELF-UNIT";
    internal static string Other(string p)=>p=="P1"?"P2":"P1";
    internal static string Rune(string p,int i)=>p+"-SELF-RUNE-"+i.ToString("D2");
    internal static int Might(MatchState s,string actor,string id)=>JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(s)[actor],Json).GetProperty("players").GetProperty(actor).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    internal static void Cost(IEnumerable<RecoveredEvent> events,int mana,int power){var x=Assert.Single(events,e=>e.Event.Kind=="COST_PAID");Assert.Equal(mana,x.Event.Payload["totalManaCost"]);Assert.Equal(power,x.Event.Payload["totalPowerCost"]);}
    internal static async Task Save(string name,MatchState initial,Driver game){if(Environment.GetEnvironmentVariable("RIFTBOUND_VEN093122_PAID_EVIDENCE") is not {Length:>0} dir)return;Directory.CreateDirectory(dir);await File.WriteAllTextAsync(Path.Combine(dir,name+".json"),JsonSerializer.Serialize(new{scope="Declaredmidgame0pools/exact12physicaleach, actualnormal4or3mana plusseparate2activation, realturns/nativeRoam andoriginalprivacy/recovery/dedup/fullhistory. Controlledmidgame input; legalopening separate; applicability/L2/L3/wholecardOPEN.",initial,final=game.State,commands=game.Commands(),events=game.Events(),ruleInputs=RuleInputIdentity.Current},Json));}
}
