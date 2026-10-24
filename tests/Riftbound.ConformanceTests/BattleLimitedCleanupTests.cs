using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN143.3b2/468.1a1: explicit midgame fixture, actual plays and movement.
// Original driver retains every command's strict views, cold restore, duplicate and original replay checks.
public sealed class BattleLimitedCleanupTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BattleCleanupHealsEscapedParticipantAndUnrelatedBaseAndOtherBattlefieldUnits(bool includeUnrelatedUnits)
    {
        var initial = Position(includeUnrelatedUnits); var run = new Driver(initial);
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));
        await run.Act("P1",new EndTurnCommand());
        foreach(var start in includeUnrelatedUnits ? new[]{1,13} : new[]{1}) {
            var funds=Enumerable.Range(start,12).Select(i=>"P2-FUND-"+i).ToArray();
            await run.Act("P2",new TapRuneCommand(funds[0],funds));
        }
        await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));
        await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"])); await run.Top();
        if(includeUnrelatedUnits) foreach(var id in new[]{"CONTROL-BASE","CONTROL-OTHER"}) {
            await run.Act("P2",new PlayCardCommand(id,"OGN·219/298",[],Destination:"BASE"));
            await run.Act("P2",new PlayCardCommand("READY-"+id,"OGN·146/298",[id])); await run.Top();
            await run.Act("P2",new MoveUnitCommand(id,"BASE","BATTLEFIELD:OTHER",[]));
            await BattleLimitedTestSupport.PassFocus(run);
        }
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[])); await run.Top();
        Assert.Equal(6,run.State.CardObjects["ATTACKER"].Power);
        if(run.State.FocusPlayerId!="P1") await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());
        var funding=Enumerable.Range(1,includeUnrelatedUnits?10:6).Select(i=>"P1-FUND-"+i).ToArray();
        await run.Act("P1",new TapRuneCommand(funding[0],funding));
        var spellCount=includeUnrelatedUnits?4:2;
        foreach(var i in Enumerable.Range(1,spellCount)) await run.Act("P1",new RecycleRuneCommand("P1-RED-"+i));
        foreach(var i in Enumerable.Range(1,spellCount)) {
            if(run.State.FocusPlayerId!="P1") await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());
            var target=i<=2?"ATTACKER":i==3?"CONTROL-BASE":"CONTROL-OTHER";
            await run.Act("P1",new PlayCardCommand("DAMAGE-"+i,"OGS·003/024",[target])); await run.Top();
        }
        Assert.Equal(4,run.State.CardObjects["ATTACKER"].Damage);
        if(run.State.FocusPlayerId!="P2") await run.Act(run.State.FocusPlayerId!,new PassFocusCommand());
        await run.Act("P2",new PlayCardCommand("FLASH","OGS·011/024",includeUnrelatedUnits?["ATTACKER","CONTROL-BASE"]:["ATTACKER"])); await run.Top();
        Assert.Contains("ATTACKER",run.State.PlayerZones["P2"].Base);
        if(includeUnrelatedUnits) {
            Assert.Contains("CONTROL-BASE",run.State.PlayerZones["P2"].Base);
            Assert.Equal(new ObjectLocationState("P2","BATTLEFIELD","OTHER"),run.State.ObjectLocations["CONTROL-OTHER"]);
            foreach(var id in new[]{"CONTROL-BASE","CONTROL-OTHER"}) Assert.Equal(2,run.State.CardObjects[id].Damage);
        }
        await BattleLimitedTestSupport.PassFocus(run);
        Assert.Null(run.State.PendingBattleResult); Assert.False(run.State.BattleState.IsActive);
        var expected=includeUnrelatedUnits?new[]{"ATTACKER","CONTROL-BASE","CONTROL-OTHER"}:new[]{"ATTACKER"};
        foreach(var id in expected) { Assert.Equal(0,run.State.CardObjects[id].Damage); Assert.Equal(1,run.State.CardObjects[id].ObjectGeneration); }
        Assert.Equal(3,run.State.CardObjects["ATTACKER"].Power);
        var healed=Assert.Single(run.Events(),e=>e.Event.Kind=="DAMAGE_REMOVED");
        var ids=JsonSerializer.SerializeToElement(healed.Event.Payload["objectIds"]).EnumerateArray().Select(e=>e.GetString()!).ToArray();
        Assert.Equal(expected,ids); Assert.Equal(includeUnrelatedUnits?8:4,Convert.ToInt32(healed.Event.Payload["totalDamageRemoved"]));
        Assert.True(healed.Sequence<Assert.Single(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED").Sequence);
        await run.Replay(); Save("global-heal-"+includeUnrelatedUnits,initial,run);
    }

    private static MatchState Position(bool controls)
    {
        var initial=BattleLimitedTestSupport.Position("plain"); var cards=new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["ATTACKER"]=Printed("ATTACKER","SFD·110/221","P2") };
        var zones=new Dictionary<string,PlayerZones>(initial.PlayerZones); var locations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations);
        void Put(string id,string no,string owner,string zone) {
            cards[id]=Printed(id,no,owner); locations[id]=new(owner,zone);
            zones[owner]=zone=="BASE"?zones[owner] with{Base=[..zones[owner].Base,id]}:zones[owner] with{Hand=[..zones[owner].Hand,id]};
        }
        Put("P2-ORANGE","UNL-R04","P2","BASE"); Put("FLASH","OGS·011/024","P2","HAND");
        foreach(var i in Enumerable.Range(1,controls?4:2)) {Put("DAMAGE-"+i,"OGS·003/024","P1","HAND");Put("P1-RED-"+i,"UNL-R01","P1","BASE");}
        foreach(var i in Enumerable.Range(1,controls?10:6)) Put("P1-FUND-"+i,"UNL-R06","P1","BASE");
        if(controls) {
            // The canonical OTHER is a Maw's Lair, which prohibits the required actual return to base.
            cards["OTHER"]=Printed("OTHER","OGN·278/298","P1") with{ControllerId=null};
            foreach(var i in Enumerable.Range(13,12)) Put("P2-FUND-"+i,"UNL-R06","P2","BASE");
            foreach(var id in new[]{"CONTROL-BASE","CONTROL-OTHER"}) {Put(id,"OGN·219/298","P2","HAND");Put("READY-"+id,"OGN·146/298","P2","HAND");}
        }
        return initial with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
    internal static CardObjectState Printed(string id,string no,string owner)=>OfficialGarenConquestBattlefieldCountTests.Printed(id,no,owner);
    internal static void Save(string key,MatchState initial,Driver run)
    {
        if(Environment.GetEnvironmentVariable("RIFTBOUND_C11_CLEANUP_EVIDENCE") is not {Length:>0} root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root,key+".json"),JsonSerializer.Serialize(new{initial,final=run.State,commands=run.Commands(),events=run.Events(),scope="Explicit midgame fixture; actual canonical commands and strict recovery/cold/duplicate/original replay. No SQL/API/natural-opening or whole-card claim."},new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
    }
}
