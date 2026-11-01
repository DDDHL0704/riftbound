using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedFirstRoleBoundaryTests
{
    [Theory]
    [InlineData("initial-condition-false-rejoin")]
    [InlineData("self-moved-before-resolution")]
    [InlineData("surrender-retains-duration")]
    [InlineData("steadfast-base-target-expires")]
    public async Task FirstRoleAndDurationSurviveActualResponses(string mode)
    {
        var initial = Position(mode);
        // Keep command acceptance and duplicate-intent checks for every
        // action, while moving full journal recovery to the common setup and
        // each mode's resolution boundary.
        var run = new Driver(initial) { RecoverAfterEveryCommand = false };
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
        if (mode == "initial-condition-false-rejoin") await run.Act("P1", new PlayCardCommand("SECOND-HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
        await run.Act("P1", new EndTurnCommand());
        var funding = Enumerable.Range(1, 20).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(funding[0], funding.Take(12).ToArray()));
        await run.Act("P2", new TapRuneCommand(funding[12], funding.Skip(12).ToArray()));
        await run.Act("P2", new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2", new RecycleRuneCommand("P2-PURPLE-1", ["P2-PURPLE-1", "P2-PURPLE-2"]));
        if (mode == "steadfast-base-target-expires") await run.Act("P2", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BASE"));
        var attackerNo = mode == "steadfast-base-target-expires" ? "OGN·142/298" : "SFD·110/221";
        await run.Act("P2", new PlayCardCommand("ATTACKER", attackerNo, [], Destination: "BASE"));
        await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"])); await run.Top();
        await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", []));
        var generation = run.State.CardObjects["ATTACKER"].ObjectGeneration;
        await run.ColdCheckpoint(); run.RecoverAfterEveryCommand = true;
        if (mode == "initial-condition-false-rejoin") {
            Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
            await Focus(run, "P1"); var own = Enumerable.Range(1, 4).Select(i => "P1-FUND-" + i).ToArray();
            await run.Act("P1", new TapRuneCommand(own[0], own)); await run.Act("P1", new RecycleRuneCommand("P1-PURPLE"));
            await run.Act("P1", new PlayCardCommand("P1-WIND", "OGN·173/298", ["SECOND-HOST"], Destination: "BASE:P1")); await run.Top();
            foreach (var (id, destination) in new[] { ("P2-WIND-1", "BASE:P2"), ("P2-WIND-2", "BATTLEFIELD:HOME") }) {
                await Focus(run,"P2"); await run.Act("P2", new PlayCardCommand(id,"OGN·173/298",["ATTACKER"],Destination:destination)); await run.Top();
            }
            Assert.True(run.State.CardObjects["ATTACKER"].IsAttacking);
            Assert.Equal(generation,run.State.CardObjects["ATTACKER"].ObjectGeneration);
            Assert.Equal(3,run.State.CardObjects["ATTACKER"].Power); Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
            Assert.DoesNotContain(run.Events(),e=>e.Event.Kind=="TRIGGER_QUEUED" && e.Event.Payload.GetValueOrDefault("sourceObjectId") as string=="ATTACKER");
            await run.ColdCheckpoint();
        }
        else if (mode == "self-moved-before-resolution") {
            await Priority(run,"P2"); await run.Act("P2",new PlayCardCommand("FLASH","OGS·011/024",["ATTACKER"])); await run.Top();
            Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Base); Assert.Single(run.State.StackItems);
            await run.Top(); Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
            Assert.Equal(generation,run.State.CardObjects["ATTACKER"].ObjectGeneration);
            await CloseFocus(run); await FinishResult(run);
            Assert.Equal(3,run.State.CardObjects["ATTACKER"].Power); Assert.Empty(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers);
            Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");
            await run.ColdCheckpoint();
        }
        else if (mode == "surrender-retains-duration") {
            await run.Top(); Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
            await run.Act("P1",new SurrenderCommand()); Assert.Equal(MatchStatuses.Finished,run.State.Status); Assert.Equal("P2",run.State.WinnerPlayerId);
            Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
            Assert.Equal("UNTIL_END_OF_BATTLE",Assert.Single(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers).Duration);
            Assert.DoesNotContain(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");
            await run.ColdCheckpoint();
        }
        else {
            var choice=Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice); Assert.Contains("ALLY",choice.LegalObjectIds);
            await run.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["ALLY"])); await run.Top();
            Assert.Contains(run.State.CardObjects["ALLY"].UntilEndOfTurnEffects,e=>e.StartsWith("COMBAT_KEYWORD:坚守2:"));
            Assert.Contains("ALLY",run.State.PlayerZones["P2"].Base);
            await CloseFocus(run); await run.Act("P2",new DeclareBattleCommand("HOME",["ATTACKER"],["HOST"],["COMBAT_ASSIGNMENT"])); await FinishResult(run);
            Assert.Contains("ALLY",run.State.PlayerZones["P2"].Base);
            Assert.DoesNotContain(run.State.CardObjects["ALLY"].UntilEndOfTurnEffects,e=>e.StartsWith("COMBAT_KEYWORD:坚守2:") || e.StartsWith("BATTLE_LIMITED_GRANT:"));
            Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");
            await run.ColdCheckpoint();
        }
        await run.Replay();
        var publicJson=JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State));
        Assert.DoesNotContain("BATTLE_LIMITED_SCOPE:",publicJson); Assert.DoesNotContain("BATTLE_ROLE_FIRST:",publicJson); Assert.DoesNotContain("BATTLE_LIMITED_GRANT:",publicJson);
    }

    private static async Task Focus(Driver run,string player) { if(run.State.FocusPlayerId!=player)await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());Assert.Equal(player,run.State.FocusPlayerId); }
    private static async Task Priority(Driver run,string player) { if(run.State.PriorityPlayerId!=player)await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());Assert.Equal(player,run.State.PriorityPlayerId); }
    private static async Task CloseFocus(Driver run) { for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());} }
    private static async Task FinishResult(Driver run) { for(var i=0;run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());} }
    private static MatchState Position(string mode)
    {
        var state=BattleLimitedTestSupport.Position("plain"); var cards=new Dictionary<string,CardObjectState>(state.CardObjects);
        var zones=new Dictionary<string,PlayerZones>(state.PlayerZones); var locations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations);
        cards["HOME"]=OfficialGarenConquestBattlefieldCountTests.Printed("HOME",mode=="steadfast-base-target-expires"?"OGN·279/298":"OGN·278/298","P1") with {ControllerId="P1"};
        cards["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER",mode=="steadfast-base-target-expires"?"OGN·142/298":"SFD·110/221","P2");
        void Put(string id,string no,string owner,string zone){cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,no,owner);locations[id]=new(owner,zone);zones[owner]=zone=="BASE"?zones[owner] with {Base=[..zones[owner].Base,id]}:zones[owner] with {Hand=[..zones[owner].Hand,id]};}
        foreach(var i in Enumerable.Range(13,8))Put("P2-FUND-"+i,"UNL-R06","P2","BASE");
        Put("FLASH","OGS·011/024","P2","HAND");
        Put("P2-ORANGE","UNL-R04","P2","BASE"); Put("P2-PURPLE-1","UNL-R05","P2","BASE"); Put("P2-PURPLE-2","UNL-R05","P2","BASE");
        Put("P2-WIND-1","OGN·173/298","P2","HAND");Put("P2-WIND-2","OGN·173/298","P2","HAND");
        if(mode=="initial-condition-false-rejoin") {Put("SECOND-HOST","OGN·210/298","P1","HAND");Put("P1-WIND","OGN·173/298","P1","HAND");Put("P1-PURPLE","UNL-R05","P1","BASE");foreach(var i in Enumerable.Range(1,4))Put("P1-FUND-"+i,"UNL-R06","P1","BASE");}
        if(mode=="steadfast-base-target-expires")Put("ALLY","OGN·210/298","P2","HAND");
        return state with {CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
