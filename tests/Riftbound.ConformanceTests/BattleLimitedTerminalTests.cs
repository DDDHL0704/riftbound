using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedTerminalTests
{
    [Theory]
    [InlineData("final-conquest")]
    [InlineData("result-burnout")]
    public async Task TerminalCutoffBeforeCloseRetainsBattleEffects(string mode)
    {
        var initial = Position(mode); var run = new Driver(initial);
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funding=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funding[0],funding));await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));await run.Top();Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
        for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
        await run.Act("P2",new DeclareBattleCommand("HOME",["ATTACKER"],["HOST"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;run.State.Status==MatchStatuses.InProgress&&run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());}
        Assert.Equal(MatchStatuses.Finished,run.State.Status);Assert.Equal(mode=="final-conquest"?"P2":"P1",run.State.WinnerPlayerId);
        Assert.NotNull(run.State.PendingBattleResult);Assert.DoesNotContain(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");
        Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);var modifier=Assert.Single(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers);
        Assert.Equal("UNTIL_END_OF_BATTLE",modifier.Duration);Assert.Equal(3,modifier.SnapshotMight);
        Assert.Contains(run.State.UntilEndOfTurnEffects,e=>e.StartsWith("BATTLE_LIMITED_SCOPE:"));
        Assert.Contains(run.State.CardObjects["ATTACKER"].UntilEndOfTurnEffects,e=>e.StartsWith("BATTLE_LIMITED_GRANT:"));
        if(mode=="final-conquest") {Assert.Equal(8,run.State.PlayerScores["P2"]);Assert.Equal("P2",run.State.CardObjects["HOME"].ControllerId);}
        else {Assert.Equal(8,run.State.PlayerScores["P1"]);Assert.Equal(0,run.State.PlayerScores["P2"]);Assert.Equal("P1",run.State.CardObjects["HOME"].ControllerId);Assert.Single(run.Events(),e=>e.Event.Kind=="BURNOUT_APPLIED");Assert.DoesNotContain(run.Events(),e=>e.Event.Kind=="BATTLEFIELD_CONQUERED");}
        await run.Replay();
    }

    private static MatchState Position(string mode)
    {
        var state=BattleLimitedTestSupport.Position(mode);return state with{CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","SFD·110/221","P2"),["P2-ORANGE"]=OfficialGarenConquestBattlefieldCountTests.Printed("P2-ORANGE","UNL-R04","P2")},PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with{Base=[..state.PlayerZones["P2"].Base,"P2-ORANGE"]}},ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["P2-ORANGE"]=new("P2","BASE")}};
    }
}
