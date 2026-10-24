using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedGenerationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NewIncarnationInvalidatesEarnedSelfOrConfirmedTarget(bool self)
    {
        var initial = Position(self); var run = new Driver(initial);
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds));
        if(self)await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));var attackerNo=self?"SFD·110/221":"OGN·210/298";
        await run.Act("P2",new PlayCardCommand("ATTACKER",attackerNo,[],Destination:"BASE"));await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));var generation=run.State.CardObjects["ATTACKER"].ObjectGeneration;
        if(!self){var choice=Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);await run.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["ATTACKER"]));Assert.Equal(generation,Assert.Single(run.State.StackItems).TargetGenerations!["ATTACKER"]);}
        if(run.State.PriorityPlayerId!="P2")await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());
        await run.Act("P2",new PlayCardCommand("GUST","OGN·169/298",["ATTACKER"]));await run.Top();
        Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Hand);Assert.Equal(generation+1,run.State.CardObjects["ATTACKER"].ObjectGeneration);Assert.Single(run.State.StackItems);
        await run.Top();Assert.Equal(self?3:2,run.State.CardObjects["ATTACKER"].Power);Assert.Empty(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(run.State.CardObjects["ATTACKER"].UntilEndOfTurnEffects,e=>e.StartsWith("BATTLE_LIMITED_GRANT:")||e.StartsWith("COMBAT_KEYWORD:坚守2:"));
        Assert.DoesNotContain(run.Events(),e=>e.Event.Kind=="UNIT_POWER_CHANGED"&&e.Event.Payload.GetValueOrDefault("duration") as string=="UNTIL_END_OF_BATTLE");
        for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
        for(var i=0;run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());}
        Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");await run.Replay();
    }

    private static MatchState Position(bool self)
    {
        var state=BattleLimitedTestSupport.Position("plain");return state with{CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["HOME"]=OfficialGarenConquestBattlefieldCountTests.Printed("HOME",self?"OGN·278/298":"OGN·279/298","P1") with{ControllerId="P1"},["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER",self?"SFD·110/221":"OGN·210/298","P2"),["P2-ORANGE"]=OfficialGarenConquestBattlefieldCountTests.Printed("P2-ORANGE","UNL-R04","P2"),["GUST"]=OfficialGarenConquestBattlefieldCountTests.Printed("GUST","OGN·169/298","P2")},PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with{Base=[..state.PlayerZones["P2"].Base,"P2-ORANGE"],Hand=[..state.PlayerZones["P2"].Hand,"GUST"]}},ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["P2-ORANGE"]=new("P2","BASE"),["GUST"]=new("P2","HAND")}};
    }
}
