using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedWardTests
{
    [Fact]
    public async Task AnyUnitSelectionPreservesWardInvalidAndStaleChoiceRejections()
    {
        var initial = Position(); var run = new Driver(initial);
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funds=Enumerable.Range(1,20).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds.Take(12).ToArray()));await run.Act("P2",new TapRuneCommand(funds[12],funds.Skip(12).ToArray()));
        await run.Act("P2",new RecycleRuneCommand("P2-JAX-GREEN"));await run.Act("P2",new PlayCardCommand("WARD-TARGET","SFD·054/221",[],Destination:"BASE"));await run.Top();
        await run.Act("P2",new PlayCardCommand("ATTACKER","OGN·142/298",[],Destination:"BASE"));await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));var choice=Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
        Assert.Contains("WARD-TARGET",choice.LegalObjectIds);Assert.DoesNotContain("ILLEGAL-HAND",choice.LegalObjectIds);
        var selected=Wire(run.State,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["WARD-TARGET"]));
        await run.ActRetained(new ConformanceCommand("P1","c11-illegal-hand-target",Wire(run.State,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["ILLEGAL-HAND"]))),false);
        await run.ActRetained(new ConformanceCommand("P1","c11-confirmed-ward-target",selected),true);
        var payment=Assert.IsType<PendingPaymentState>(run.State.PendingPayment);Assert.Equal("TRIGGER_TARGET_COST",payment.PaymentWindow);Assert.Equal(1,payment.PowerCost);Assert.Null(run.State.PriorityPlayerId);
        Assert.DoesNotContain(run.State.CardObjects["WARD-TARGET"].UntilEndOfTurnEffects,e=>e.StartsWith("COMBAT_KEYWORD:坚守2:"));
        await run.ActRetained(new ConformanceCommand("P1","c11-new-intent-stale-choice",selected),false);
        await run.Act("P1",new RecycleRuneCommand("P1-WARD-RUNE"));Assert.Equal(1,run.State.RunePools["P1"].TotalPower);
        await run.Act("P1",new PayCostCommand(payment.PaymentId,payment.PaymentWindow,["PAY"]));Assert.Equal(0,run.State.RunePools["P1"].TotalPower);Assert.NotNull(run.State.PriorityPlayerId);
        await run.Top();Assert.Contains(run.State.CardObjects["WARD-TARGET"].UntilEndOfTurnEffects,e=>e.StartsWith("COMBAT_KEYWORD:坚守2:"));
        Assert.Contains(run.State.ContinuousEffects,e=>e.TargetObjectId=="WARD-TARGET"&&e.Duration=="UNTIL_END_OF_BATTLE");
        for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
        await run.Act("P2",new DeclareBattleCommand("HOME",["ATTACKER"],["HOST"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());}
        Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");Assert.DoesNotContain(run.State.CardObjects["WARD-TARGET"].UntilEndOfTurnEffects,e=>e.StartsWith("COMBAT_KEYWORD:坚守2:"));
        Assert.Equal(2,run.Commands().Count(c=>!c.Accepted));await run.Replay();
    }

    private static JsonElement Wire(MatchState state,GameCommand command){var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);var raw=JsonSerializer.SerializeToNode(command,command.GetType(),json)!.AsObject();raw["promptId"]=ResolutionResult.BuildPrompts(state)["P1"].PromptId;raw["snapshotTick"]=state.Tick;return JsonSerializer.SerializeToElement(raw,json);}
    private static MatchState Position()
    {
        var state=BattleLimitedTestSupport.Position("plain");var cards=new Dictionary<string,CardObjectState>(state.CardObjects){["HOME"]=OfficialGarenConquestBattlefieldCountTests.Printed("HOME","OGN·279/298","P1") with{ControllerId="P1"},["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","OGN·142/298","P2")};var zones=new Dictionary<string,PlayerZones>(state.PlayerZones);var locations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations);
        void Put(string id,string no,string owner,string zone){cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,no,owner);locations[id]=new(owner,zone);zones[owner]=zone=="BASE"?zones[owner] with{Base=[..zones[owner].Base,id]}:zones[owner] with{Hand=[..zones[owner].Hand,id]};}
        foreach(var i in Enumerable.Range(13,8))Put("P2-FUND-"+i,"UNL-R06","P2","BASE");Put("P2-JAX-GREEN","UNL-R02","P2","BASE");Put("WARD-TARGET","SFD·054/221","P2","HAND");Put("P1-WARD-RUNE","UNL-R06","P1","BASE");Put("ILLEGAL-HAND","OGN·219/298","P1","HAND");return state with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
