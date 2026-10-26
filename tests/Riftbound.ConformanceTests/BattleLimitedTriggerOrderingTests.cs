using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedTriggerOrderingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SnapshotUsesTheActualSimultaneousSkillResolutionOrder(bool keywordFirst)
    {
        var initial = Position(); var run = new Driver(initial);
        await run.Act("P1",new RecycleRuneCommand("P1-ORANGE"));
        await run.Act("P1",new PlayCardCommand("HOST","SFD·110/221",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds));
        await run.Act("P2",new PlayCardCommand("ATTACKER","OGN·210/298",[],Destination:"BASE"));
        await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));
        Assert.Equal(2,run.State.TriggerQueue.Count);
        var firstSource=keywordFirst?"HOME":"HOST";
        var order=run.State.TriggerQueue.OrderBy(t=>t.SourceObjectId==firstSource?0:1).Select(t=>t.TriggerId).ToArray();
        var orderingPlayer=ResolutionResult.BuildPrompts(run.State).Single(p=>p.Value.Actionable&&p.Value.Actions.Contains(CommandTypes.OrderTriggers)).Key;
        Assert.Equal("P1",orderingPlayer);
        await run.Act(orderingPlayer,new OrderTriggersCommand(OrderedTriggerIds:order));
        var choice=Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);Assert.Equal("HOME",choice.SourceObjectId);
        await run.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["HOST"]));
        Assert.Equal(firstSource,run.State.StackItems[^1].SourceObjectId);
        Assert.All(run.State.StackItems.Where(i=>i.SourceObjectId=="HOST"),i=>{Assert.Empty(i.TargetObjectIds);Assert.Null(i.TargetGenerations);});
        await run.Top();await run.Top();
        var modifier=Assert.Single(run.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(keywordFirst?5:3,modifier.SnapshotMight);Assert.Equal(keywordFirst?8:6,run.State.CardObjects["HOST"].Power);
        var host=ResolutionResult.BuildSnapshots(run.State)["P1"];
        var snapshotJson=JsonSerializer.SerializeToElement(host,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var objectPayload=snapshotJson.GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST");
        Assert.Equal(keywordFirst?10:8,objectPayload.GetProperty("effectivePower").GetInt32());
        for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
        await run.Act("P2",new DeclareBattleCommand("HOME",["ATTACKER"],["HOST"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());}
        Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");Assert.Equal(3,run.State.CardObjects["HOST"].Power);
        Assert.Empty(run.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);Assert.DoesNotContain(run.State.CardObjects["HOST"].UntilEndOfTurnEffects,t=>t.StartsWith("COMBAT_KEYWORD:坚守2:"));
        await run.Replay();
    }

    private static MatchState Position()
    {
        var state=BattleLimitedTestSupport.Position("plain");var cards=new Dictionary<string,CardObjectState>(state.CardObjects){["HOST"]=OfficialGarenConquestBattlefieldCountTests.Printed("HOST","SFD·110/221","P1"),["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","OGN·210/298","P2"),["HOME"]=OfficialGarenConquestBattlefieldCountTests.Printed("HOME","OGN·279/298","P1") with{ControllerId="P1"},["P1-ORANGE"]=OfficialGarenConquestBattlefieldCountTests.Printed("P1-ORANGE","UNL-R04","P1")};
        return state with{CardObjects=cards,PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with{Base=[..state.PlayerZones["P1"].Base,"P1-ORANGE"]}},ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["P1-ORANGE"]=new("P1","BASE")}};
    }
}
