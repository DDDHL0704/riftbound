using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedCopiedTokenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualCopiedTokenDoublesOrCeasesBeforeResolution(bool cease)
    {
        var initial = Position(); var run = new Driver(initial); string? tokenId = null;
        await run.Act("P1",new PlayCardCommand("HOST","OGN·210/298",[],Destination:"BATTLEFIELD:HOME"));await run.Act("P1",new EndTurnCommand());
        var funds=Enumerable.Range(1,12).Select(i=>"P2-FUND-"+i).ToArray();await run.Act("P2",new TapRuneCommand(funds[0],funds));await run.Act("P2",new RecycleRuneCommand("P2-ORANGE"));
        await run.Act("P2",new RecycleRuneCommand("COPY-BLUE-1",["COPY-BLUE-1","COPY-BLUE-2"]));
        await run.Act("P2",new PlayCardCommand("ATTACKER","SFD·110/221",[],Destination:"BASE"));
        await run.Act("P2",new PlayCardCommand("MIRROR","UNL-200/219",["ATTACKER"]));await run.Top();
        tokenId=Assert.IsType<string>(Assert.Single(run.Events(),e=>e.Event.Kind=="UNIT_TOKEN_CREATED").Event.Payload["tokenObjectId"]);
        Assert.Equal("UNL·T06",run.State.CardObjects[tokenId].CardNo);Assert.Equal(0,run.State.CardObjects[tokenId].Power);Assert.Single(run.State.StackItems);
        await run.Top();Assert.Equal("SFD·110/221",run.State.CardObjects[tokenId].CardNo);Assert.Equal("UNL·T06",run.State.CardObjects[tokenId].TokenFactoryCardNo);Assert.False(run.State.CardObjects[tokenId].IsExhausted);
        await run.Act("P2",new MoveUnitCommand(tokenId,"BASE","BATTLEFIELD:HOME",[]));
        var earned=Assert.Single(run.State.StackItems);Assert.Equal(tokenId,earned.SourceObjectId);Assert.Equal("UNL·T06",earned.FieldContext!.SourceTokenFactoryCardNo);
        if(cease) {
            if(run.State.PriorityPlayerId!="P2")await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());
            await run.Act("P2",new PlayCardCommand("GUST","OGN·169/298",[tokenId]));await run.Top();
            Assert.False(run.State.CardObjects.ContainsKey(tokenId));var remaining=Assert.Single(run.State.StackItems);Assert.True(remaining.FieldContext!.SourceCeasedToExist);Assert.Equal("UNL·T06",remaining.FieldContext.SourceTokenFactoryCardNo);
            await run.Top();Assert.False(run.State.CardObjects.ContainsKey(tokenId));Assert.Equal(3,run.State.CardObjects["ATTACKER"].Power);
            Assert.Contains(run.Events(),e=>e.Event.Kind=="TOKEN_CEASED_TO_EXIST");Assert.DoesNotContain(run.Events(),e=>e.Event.Kind=="UNIT_POWER_CHANGED"&&e.Event.Payload.GetValueOrDefault("duration") as string=="UNTIL_END_OF_BATTLE");
        }
        else {await run.Top();Assert.Equal(6,run.State.CardObjects[tokenId].Power);Assert.Equal(3,Assert.Single(run.State.CardObjects[tokenId].UntilEndOfTurnPowerModifiers).SnapshotMight);}
        for(var i=0;run.State.FocusPlayerId is not null;i++){Assert.InRange(i,0,3);await run.Act(run.State.FocusPlayerId,new PassFocusCommand());}
        if(!cease)await run.Act("P2",new DeclareBattleCommand("HOME",[tokenId],["HOST"],["COMBAT_ASSIGNMENT"]));
        for(var i=0;run.State.PendingBattleResult is not null;i++){Assert.InRange(i,0,7);await run.Act(run.State.PriorityPlayerId!,new PassPriorityCommand());}
        Assert.Contains(run.Events(),e=>e.Event.Kind=="BATTLE_CLOSED");
        if(!cease){Assert.Equal(3,run.State.CardObjects[tokenId].Power);Assert.Empty(run.State.CardObjects[tokenId].UntilEndOfTurnPowerModifiers);}await run.Replay();
    }

    private static MatchState Position()
    {
        var state=BattleLimitedTestSupport.Position("plain");var cards=new Dictionary<string,CardObjectState>(state.CardObjects){["ATTACKER"]=OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER","SFD·110/221","P2")};var zones=new Dictionary<string,PlayerZones>(state.PlayerZones);var locations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations);
        void Put(string id,string no,string zone){cards[id]=OfficialGarenConquestBattlefieldCountTests.Printed(id,no,"P2");locations[id]=new("P2",zone);zones["P2"]=zone=="BASE"?zones["P2"] with{Base=[..zones["P2"].Base,id]}:zones["P2"] with{Hand=[..zones["P2"].Hand,id]};}
        Put("P2-ORANGE","UNL-R04","BASE");Put("COPY-BLUE-1","UNL-R03","BASE");Put("COPY-BLUE-2","UNL-R03","BASE");Put("MIRROR","UNL-200/219","HAND");Put("GUST","OGN·169/298","HAND");return state with{CardObjects=cards,PlayerZones=zones,ObjectLocations=locations};
    }
}
