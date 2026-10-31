using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedCopiedRuleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedRuleInstancesResolveInOrderAfterAttachmentChange(bool detach)
    {
        var initial = Position(); var run = new Driver(initial) { RecoverAfterEveryCommand = false };
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funding=Enumerable.Range(1,20).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funding[0],funding.Take(12).ToArray()));await run.Act("P2",new TapRuneCommand(funding[12],funding.Skip(12).ToArray()));
        await run.Act("P2",new RecycleRuneCommand("ORANGE"));
        var greens=Enumerable.Range(1,4).Select(i=>"GREEN-"+i).ToArray();await run.Act("P2",new RecycleRuneCommand(greens[0],greens));
        if(detach)await run.Act("P2",new RecycleRuneCommand("RED"));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));
        foreach(var id in new[]{"SONG-1","SONG-2"}) {
            await run.Act("P2",new PlayCardCommand(id,"SFD·059/221",[],Destination:"BASE"));
            await run.Act("P2",new AssembleEquipmentCommand(id,"ATTACKER",["ASSEMBLE_GREEN"]));await run.Top();
            var origin=Assert.IsType<AttachedRuleTextState>(run.State.CardObjects[id].AttachedRuleText);
            Assert.Equal("SFD·110/221",origin.CardNo);Assert.Equal("ATTACKER",origin.Unit.ObjectId);Assert.Equal(1,origin.Revision);
        }
        await run.Act("P2",new PlayCardCommand("READY","OGN·146/298",["ATTACKER"]));await run.Top();
        await run.Act("P2",new MoveUnitCommand("ATTACKER","BASE","BATTLEFIELD:HOME",[]));
        await run.ColdCheckpoint(); run.RecoverAfterEveryCommand = true;
        var earned=run.State.TriggerQueue.Select(t=>t.FieldContext).Concat(run.State.StackItems.Select(i=>i.FieldContext)).Where(c=>c is not null).ToArray();
        Assert.Equal(3,earned.Length);Assert.Single(earned,c=>c!.Origin is null);
        Assert.Equal(new[]{"SONG-1","SONG-2"},earned.Where(c=>c!.Origin is not null).Select(c=>c!.Origin!.Equipment.ObjectId).Order().ToArray());
        if(run.State.TriggerQueue.Count>0)await run.Act("P2",new OrderTriggersCommand(OrderedTriggerIds:run.State.TriggerQueue.Select(t=>t.TriggerId).ToArray()));
        if(detach) {
            if(run.State.PriorityPlayerId!="P2")await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());
            await run.Act("P2",new PlayCardCommand("DETACH","SFD·011/221",["ATTACKER","SONG-1"]));await run.Top();
            Assert.Null(run.State.CardObjects["SONG-1"].AttachedToObjectId);Assert.Null(run.State.CardObjects["SONG-1"].AttachedRuleText);
            Assert.Equal(3,run.State.StackItems.Count);
        }
        foreach(var expected in new[]{6,12,24}) {await run.Top();Assert.Equal(expected,run.State.CardObjects["ATTACKER"].Power);}
        var ledger=run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers;
        Assert.Equal(new[]{3,6,12},ledger.Select(p=>p.SnapshotMight!.Value).ToArray());
        Assert.Equal(3,ledger.Select(p=>p.EffectId).Distinct().Count());Assert.All(ledger,p=>Assert.Equal("UNTIL_END_OF_BATTLE",p.Duration));
        await run.Replay();
    }

    private static MatchState Position()
    {
        var state=BattleLimitedTestSupport.Position("plain");var cards=new Dictionary<string,CardObjectState>(state.CardObjects);
        var zones=new Dictionary<string,PlayerZones>(state.PlayerZones);var locations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations);
        cards["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","SFD·110/221","P2");
        void Put(string id,string no,string zone){cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,no,"P2");locations[id]=new("P2",zone);zones["P2"]=zone=="BASE"?zones["P2"] with {Base=[..zones["P2"].Base,id]}:zones["P2"] with {Hand=[..zones["P2"].Hand,id]};}
        foreach(var i in Enumerable.Range(13,8))Put("P2-FUND-"+i,"UNL-R06","BASE");foreach(var i in Enumerable.Range(1,4))Put("GREEN-"+i,"UNL-R02","BASE");
        Put("ORANGE","UNL-R04","BASE");Put("RED","UNL-R01","BASE");Put("SONG-1","SFD·059/221","HAND");Put("SONG-2","SFD·059/221","HAND");Put("DETACH","SFD·011/221","HAND");
        return state with {CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
