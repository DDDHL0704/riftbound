using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class EchoSingleDestinationTests
{
    internal static MatchState Position()
    {
        var s=ReflexiveMovementDuelTests.Position() with {UntilEndOfTurnEffects=[EchoCostRules.GrantPrefix+"P1"]};
        s=At(At(s,"ANCHOR","P1","BASE"),"BF2","P2","BATTLEFIELD","BF2");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["E2"]=new("E2",cardNo:"OGN·096/298",power:20,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard])}};
        return At(s,"E2","P2","BATTLEFIELD","BF2");
    }
    internal static PlayCardCommand Command(string destination="BATTLEFIELD:BF2")
        =>Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(JsonSerializer.SerializeToElement(new {
            cmdType="PLAY_CARD",sourceObjectId="AOE",cardNo=ReflexiveMovementDuelTests.Spell,targetObjectIds=new[]{"D"},
            destination="BASE:P2",optionalCosts=new[]{"ECHO"},repeatChoices=new[]{new {mode="",targetObjectIds=new[]{"D2"},destination}}
        })));
    [Fact]
    public async Task EchoMovesToTwoDistinctDeclaredLocationsAndCreatesDistinctDuelContexts()
    {
        var played=await Act(Position(),"P1",Command());Restore(played.State);
        var moved=await Top(played.State);
        Assert.Contains("D",moved.State.PlayerZones["P2"].Base);
        Assert.Equal("BF2",moved.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(new[]{"BASE","BATTLEFIELD"},moved.State.TriggerQueue.Select(t=>t.ReflexiveDuel!.Destination!.Zone));Restore(moved.State);
        var ordered=await Act(moved.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:moved.State.TriggerQueue.Select(t=>t.TriggerId).ToArray()));
        var result=ordered;
        for(var i=0;i<2;i++) {
            var choice=result.State.PendingCardChoice!;var target=choice.ContextObjectIds[0]=="D"?"E":"E2";
            Assert.Equal(new[]{target},choice.LegalObjectIds);
            result=await Act(result.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[target]));Restore(result.State);
        }
        result=await Top((await Top(result.State)).State);
        Assert.Equal(3,result.State.CardObjects["E"].Damage);Assert.Equal(2,result.State.CardObjects["E2"].Damage);Restore(result.State);
    }
    [Theory]
    [InlineData("BATTLEFIELD:BF")] [InlineData("BASE:P1")] [InlineData("BATTLEFIELD:missing")]
    public async Task InvalidLaterDestinationRejectsAtomicallyBeforeChargingEcho(string destination)
    {
        var s=Position();var command=Command(destination);var result=await new CoreRuleEngine().ResolveAsync(s,new("invalid","P1",command.CmdType),command,default);
        Assert.False(result.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("7")] [InlineData("true")] [InlineData("[]")] [InlineData("{}")]
    public void MalformedLaterDestinationCannotSilentlyUseTheFirstDestination(string value)
    {
        var json="{\"cmdType\":\"PLAY_CARD\",\"sourceObjectId\":\"AOE\",\"cardNo\":\"OGN·258/298\",\"targetObjectIds\":[\"D\"],\"destination\":\"BASE:P2\",\"optionalCosts\":[\"ECHO\"],\"repeatChoices\":[{\"mode\":\"\",\"targetObjectIds\":[\"D2\"],\"destination\":"+value+"}]}";
        using var document=JsonDocument.Parse(json);Assert.IsType<UnsupportedCommand>(GameCommandJsonMapper.Map(document.RootElement));
    }
    [Theory]
    [InlineData(0,"BATTLEFIELD:BF2")] [InlineData(1,"")] [InlineData(1,"BASE:P1")] [InlineData(1,"BATTLEFIELD:missing")]
    public async Task RecoveryRejectsAlteredInitialOrMalformedLaterDestination(int index,string destination)
    {
        var state=(await Act(Position(),"P1",Command())).State;var item=Assert.Single(state.StackItems);
        Assert.Equal(new[]{"BASE:P2","BATTLEFIELD:BF2"},item.RepeatExecutions!.Select(e=>e.Destination));Restore(state);
        var executions=item.RepeatExecutions!.ToArray();executions[index]=executions[index] with {Destination=destination};
        var bad=state with {StackItems=[item with {RepeatExecutions=executions}]};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad),error=>error.Contains("spell repeat execution plan"));
    }
    [Fact]
    public async Task BothPlayersAndSpectatorSeeAndValidateTheIndependentDeclarations()
    {
        var result=await Act(Position(),"P1",Command());var state=result.State;
        var views=state.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,state.Tick,0,result.Snapshots[id],state.Tick,0,result.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(state.RoomId,state.Tick,0,[],state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId,0,[],[],views,state,state.Tick,spectator));
        foreach(var view in views.Values) {
            var stack=Assert.IsType<Dictionary<string,object?>>(Assert.Single(view.Snapshot.Stack));
            var executions=Assert.IsType<Dictionary<string,object?>[]>(stack["repeatExecutions"]);
            Assert.Equal("BATTLEFIELD:BF2",executions[1]["destination"]);
            executions[1]["destination"]="BASE:P2";
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId,0,[],[],views,state,state.Tick),e=>e.Contains("spell repeat destinations differ"));
            executions[1]["destination"]="BATTLEFIELD:BF2";
        }
        var publicItem=Assert.IsType<Dictionary<string,object?>>(Assert.Single(spectator.SpectatorSnapshot.Stack));
        var publicExecutions=Assert.IsType<Dictionary<string,object?>[]>(publicItem["repeatExecutions"]);
        publicExecutions[1]["destination"]="BASE:P2";
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId,0,[],[],views,state,state.Tick,spectator),e=>e.Contains("spell repeat destinations differ"));
    }
    [Fact]
    public async Task ResponseInvalidationOfOneTargetDoesNotRedirectTheOtherExecution()
    {
        var state=(await Act(Position(),"P1",Command())).State;
        state=At(state,"D","P1","BASE");Restore(state);
        var result=await Top(state);Assert.Contains("D",result.State.PlayerZones["P1"].Base);
        Assert.Equal("BF2",result.State.ObjectLocations["D2"].BattlefieldObjectId);
        if(result.State.TriggerQueue.Count>1)result=await Act(result.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:result.State.TriggerQueue.Select(t=>t.TriggerId).ToArray()));
        Assert.Equal(new[]{"E2"},result.State.PendingCardChoice!.LegalObjectIds);Restore(result.State);
    }

    [Fact]
    public async Task CommandedMovementKeepsItsOwnDestinationThroughEachOpponentChoice()
    {
        var s=CommandedMovementTests.Position() with {UntilEndOfTurnEffects=[EchoCostRules.GrantPrefix+"P1"],RunePools=new Dictionary<string,RunePool>{["P1"]=new(20,20),["P2"]=new(20,20)}};
        s=At(At(At(s,"D","P2","BASE"),"D2","P2","BASE"),"BF","P1","BATTLEFIELD","BF");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A2"]=s.CardObjects["A"] with {ObjectId="A2"}}};
        s=At(s,"A2","P1","BATTLEFIELD","BF");
        var command=CommandedMovementTests.Command() with {OptionalCosts=["ECHO"],RepeatChoices=[new("",["ANCHOR"],Destination:"BATTLEFIELD:BF")]};
        var result=await Top((await Act(s,"P1",command)).State);Restore(result.State);
        Assert.Equal("P2",result.State.PendingRuleChoice!.Request.PlayerId);
        result=await DestructionOrderTests.Select(result.State,"MOVE:D");Restore(result.State);
        Assert.Equal("P2",result.State.PendingRuleChoice!.Request.PlayerId);
        result=await DestructionOrderTests.Select(result.State,"MOVE:D2");
        Assert.Equal("BF2",result.State.ObjectLocations["A"].BattlefieldObjectId);Assert.Equal("BF2",result.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.Equal("BF",result.State.ObjectLocations["ANCHOR"].BattlefieldObjectId);Assert.Equal("BF",result.State.ObjectLocations["D2"].BattlefieldObjectId);Restore(result.State);
    }
}
