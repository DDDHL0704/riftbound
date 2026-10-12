using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;

namespace Riftbound.ConformanceTests;
public sealed class SequentialMovementTests
{
    internal const string Spell="UNL-202/219";
    internal static MatchState Position()
    {
        var s=CommandedMovementTests.Position();
        return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["AOE"]=s.CardObjects["AOE"] with {CardNo=Spell}},
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(20,20)}};
    }
    internal static GameCommand Command(string first,string second)
        =>GameCommandJsonMapper.Map(JsonSerializer.SerializeToElement(new {cmdType="PLAY_CARD",sourceObjectId="AOE",cardNo=Spell,targetObjectIds=new[]{"A","D"},movementDestinations=new[]{first,second}}));

    [Theory]
    [InlineData("BATTLEFIELD:BF2","BASE:P2")]
    [InlineData("BATTLEFIELD:BF2","BATTLEFIELD:BF2")]
    [InlineData("BATTLEFIELD:BF","BATTLEFIELD:BF2")]
    public async Task BothMovementsUseTheirOwnDeclaredDestinationInOrder(string first,string second)
    {
        var played=await Act(Position(),"P1",Command(first,second));var done=await Top(played.State);
        Assert.Null(done.State.PendingRuleChoice);
        Assert.Equal(first[12..],done.State.ObjectLocations["A"].BattlefieldObjectId);
        Assert.Equal(second.StartsWith("BASE:")?"BASE":"BATTLEFIELD",done.State.ObjectLocations["D"].Zone);
        Assert.Equal(second.StartsWith("BASE:")?null:second[12..],done.State.ObjectLocations["D"].BattlefieldObjectId);
        var moves=done.Events.Where(e=>e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD").ToArray();
        Assert.Equal(new[]{"A","D"},moves.Select(e=>e.Payload["unitObjectId"]));
        Assert.All(moves,e=>Assert.Equal("P1",e.Payload["movingPlayerId"]));Restore(done.State);
    }

    [Theory]
    [InlineData("BASE:P1","BATTLEFIELD:BF2")]
    [InlineData("BATTLEFIELD:BF2","BATTLEFIELD:BF")]
    [InlineData("BATTLEFIELD:ABSENT","BASE:P2")]
    [InlineData("BATTLEFIELD:BF2","BASE:P1")]
    public async Task InvalidOrUnchangedDestinationIsRejectedBeforePayment(string first,string second)
    {
        var s=Position();var command=Command(first,second);var result=await new CoreRuleEngine().ResolveAsync(s,new("bad","P1",command.CmdType),command,default);
        Assert.False(result.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("null")] [InlineData("[]")] [InlineData("[\"BATTLEFIELD:BF2\"]")]
    [InlineData("[\"BATTLEFIELD:BF2\",7]")] [InlineData("42")] [InlineData("[\"BATTLEFIELD:BF2\",\"BASE:P2\",\"BASE:P1\"]")]
    public async Task MissingMalformedOrExtraDestinationsCannotSpendResources(string moves)
    {
        using var json=JsonDocument.Parse("{\"cmdType\":\"PLAY_CARD\",\"sourceObjectId\":\"AOE\",\"cardNo\":\"UNL-202/219\",\"targetObjectIds\":[\"A\",\"D\"],\"movementDestinations\":"+moves+"}");
        var command=GameCommandJsonMapper.Map(json.RootElement);var state=Position();
        var result=await new CoreRuleEngine().ResolveAsync(state,new("bad","P1",command.CmdType),command,default);
        Assert.False(result.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task TargetsAndMovementInstructionsKeepTheirPositionsAfterInvalidation()
    {
        var played=await Act(Position(),"P1",Command("BATTLEFIELD:BF2","BASE:P2"));
        var s=played.State with {CardObjects=new Dictionary<string,CardObjectState>(played.State.CardObjects) {["A"]=played.State.CardObjects["A"] with {ObjectGeneration=1}}};
        var done=await Top(s);Assert.Equal("BASE",done.State.ObjectLocations["A"].Zone);Assert.Equal("BASE",done.State.ObjectLocations["D"].Zone);
        Assert.Equal("P2",done.State.ObjectLocations["D"].PlayerId);Assert.Single(done.Events,e=>e.Kind=="UNIT_MOVED_TO_BASE");Restore(done.State);
    }

    [Fact]
    public async Task RecoveryPreservesDestinationsAndRejectsIncompletePlans()
    {
        var played=await Act(Position(),"P1",Command("BATTLEFIELD:BF2","BASE:P2"));var item=Assert.Single(played.State.StackItems);
        var restored=OfficialInsightAndSpellLockTests.Restore(played.State);
        Assert.Equal(new[]{"BATTLEFIELD:BF2","BASE:P2"},Assert.Single(restored.StackItems).MovementDestinations);
        foreach(var plan in new IReadOnlyList<string>?[]{null,[],["BATTLEFIELD:BF2"],["HAND:P1","BASE:P2"],["BATTLEFIELD:BF2","BASE:ABSENT"]})
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(played.State with {StackItems=[item with {MovementDestinations=plan}]}),e=>e.Contains("sequential movement"));
        var done=await Top(restored);Assert.Equal("BF2",done.State.ObjectLocations["A"].BattlefieldObjectId);Assert.Equal("BASE",done.State.ObjectLocations["D"].Zone);Restore(done.State);
    }

    [Fact]
    public async Task EchoCanDeclareNewTargetsAndNewDestinationsForEachExecution()
    {
        var s=Position();s=s with {UntilEndOfTurnEffects=[EchoCostRules.GrantPrefix+"P1"],
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A2"]=s.CardObjects["A"] with {ObjectId="A2"}}};s=At(s,"A2","P1","BASE");
        var command=Assert.IsType<PlayCardCommand>(Command("BATTLEFIELD:BF2","BASE:P2")) with {OptionalCosts=["ECHO"],RepeatChoices=[new("",["A2","D2"],["BATTLEFIELD:BF","BATTLEFIELD:BF2"])]};
        var played=await Act(s,"P1",command);var restored=OfficialInsightAndSpellLockTests.Restore(played.State);
        Assert.Equal(new[]{"BATTLEFIELD:BF","BATTLEFIELD:BF2"},restored.StackItems[^1].RepeatExecutions![1].MovementDestinations);
        var done=await Top(restored);Assert.Equal("BF2",done.State.ObjectLocations["A"].BattlefieldObjectId);Assert.Equal("BASE",done.State.ObjectLocations["D"].Zone);
        Assert.Equal("BF",done.State.ObjectLocations["A2"].BattlefieldObjectId);Assert.Equal("BF2",done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(4,done.Events.Count(e=>e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD"));Assert.Equal(2,done.Events.Count(e=>e.Kind=="SPELL_EXECUTION_COMPLETED"));Restore(done.State);
    }

    [Fact]
    public async Task SeparateInstructionsTriggerOpponentMovementTwice()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["V"]=new("V",cardNo:"OGN·158/298",power:10,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]) }};s=At(s,"V","P2","BASE");
        var done=await Top((await Act(s,"P1",Command("BATTLEFIELD:BF2","BATTLEFIELD:BF2"))).State);
        Assert.Equal(2,done.State.TriggerQueue.Count);Assert.All(done.State.TriggerQueue,t=>{Assert.Equal("V",t.SourceObjectId);Assert.Equal("P1",t.MovementObserver!.Unit.MovingPlayerId);});Restore(done.State);
    }

    [Fact]
    public async Task ServerPublishesBothDestinationsBeforeResponses()
    {
        var s=Position();var prompt=JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(s)["P1"],new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var requirement=prompt.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD")
            .GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray().Single(c=>c.GetProperty("sourceObjectId").GetString()=="AOE");
        Assert.Equal(2,requirement.GetProperty("movementDestinationChoicesByIndex").EnumerateObject().Count());
        var played=await Act(s,"P1",Command("BATTLEFIELD:BF2","BASE:P2"));
        var snapshot=JsonSerializer.SerializeToElement(played.Snapshots["P2"],new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("movementDestinations",snapshot.GetRawText());Assert.Contains("BATTLEFIELD:BF2",snapshot.GetRawText());Restore(played.State);
    }

    internal static MatchState NeutralPosition()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["BF3"]=s.CardObjects["BF"] with {ObjectId="BF3",ControllerId=null}}};
        s=At(s,"BF3","P2","BATTLEFIELD","BF3");return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["BF3"]=s.CardObjects["BF3"] with {ControllerId=null}}};
    }

    [Fact]
    public async Task BothUnitsEnteringUncontrolledBattlefieldMakeCasterTheAttacker()
    {
        var s=NeutralPosition();Assert.Null(s.BattlefieldStates["BF3"].ControllerId);
        var done=await Top((await Act(s,"P1",Command("BATTLEFIELD:BF3","BATTLEFIELD:BF3"))).State);
        Assert.Equal("P1",done.State.FocusPlayerId);
        var first=await Act(done.State,"P1",new PassFocusCommand());var second=await Act(first.State,"P2",new PassFocusCommand());
        Assert.Contains(second.Prompts["P1"].Candidates??[],c=>c.Action==CommandTypes.DeclareBattle);
        Assert.DoesNotContain(second.Prompts["P2"].Candidates??[],c=>c.Action==CommandTypes.DeclareBattle);
        var battle=await Act(second.State,"P1",new DeclareBattleCommand("BF3",["A"],["D"],["COMBAT_ASSIGNMENT"]));
        Assert.Contains(battle.Events,e=>e.Kind=="BATTLE_DECLARED" && Equals(e.Payload["playerId"],"P1"));
        Assert.True(done.State.BattleState.IsActive);Assert.Contains("A",done.State.BattleState.AttackerObjectIds);Assert.Contains("D",done.State.BattleState.DefenderObjectIds);Assert.True(done.State.CardObjects["A"].IsAttacking);
        Assert.Contains(battle.Events,e=>e.Kind=="BATTLE_CLOSED");Restore(done.State);Restore(battle.State);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ProhibitedMovementDoesNotCancelTheOtherInstruction(bool first)
    {
        var s=Position();var id=first?"A":"D";s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {[id]=s.CardObjects[id] with {UntilEndOfTurnEffects=[MovementRestrictionRules.ForPlayer("P1")]}}};
        var done=await Top((await Act(s,"P1",Command("BATTLEFIELD:BF2","BASE:P2"))).State);
        Assert.Equal(first?"BASE":"BATTLEFIELD",done.State.ObjectLocations["A"].Zone);Assert.Equal(first?"BASE":"BATTLEFIELD",done.State.ObjectLocations["D"].Zone);
        Assert.Single(done.Events,e=>e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");Restore(done.State);
    }

    [Fact]
    public async Task AttachedEquipmentFollowsEachInstructionWithoutChangingControllerOrState()
    {
        var s=Position();var cards=new Dictionary<string,CardObjectState>(s.CardObjects);
        cards["G1"]=new("G1",ownerId:"P1",controllerId:"P1",attachedToObjectId:"A",isExhausted:true,tags:[CardObjectTags.EquipmentCard]);
        cards["G2"]=new("G2",ownerId:"P1",controllerId:"P2",attachedToObjectId:"D",isExhausted:true,tags:[CardObjectTags.EquipmentCard]);
        s=s with {CardObjects=cards};s=At(At(s,"G1","P1","BASE"),"G2","P2","BATTLEFIELD","BF");
        var done=await Top((await Act(s,"P1",Command("BATTLEFIELD:BF2","BASE:P2"))).State);
        Assert.Equal("BF2",done.State.ObjectLocations["G1"].BattlefieldObjectId);Assert.Equal("BASE",done.State.ObjectLocations["G2"].Zone);
        Assert.Equal("P1",done.State.CardObjects["G2"].OwnerId);Assert.Equal("P2",done.State.CardObjects["G2"].ControllerId);
        Assert.All(new[]{"G1","G2"},id=>Assert.True(done.State.CardObjects[id].IsExhausted));Assert.Equal(2,done.Events.Count(e=>e.Kind=="EQUIPMENT_MOVED_WITH_UNIT"));Restore(done.State);
    }
    [Fact]
    public async Task GraveyardRecastKeepsBothSequentialDestinations()
    {
        var s=OfficialGraveyardRecastTests.Kaisa("OGN·112/298");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["C"]=s.CardObjects["C"] with {CardNo=Spell}},
            PlayerScores=new Dictionary<string,int>(s.PlayerScores) {["P1"]=4}};
        var conquered=await ConquestLifecycleRegressionTests.Conquer(s);var opened=await Top(conquered.State);Assert.NotNull(opened.State.PendingEffectPlay);
        var seed=Position();var cards=new Dictionary<string,CardObjectState>(opened.State.CardObjects);
        foreach(var id in new[]{"A","BF2","ANCHOR","D"})cards[id]=seed.CardObjects[id];
        s=opened.State with {CardObjects=cards};s=At(At(At(At(s,"A","P1","BASE"),"BF2","P1","BATTLEFIELD","BF2"),"ANCHOR","P1","BATTLEFIELD","BF2"),"D","P2","BATTLEFIELD","BF");Restore(s);
        var command=new PlayCardCommand("C",Spell,["A","D"],MovementDestinations:["BATTLEFIELD:BF2","BASE:P2"]);
        var quote=new CoreRuleEngine().PreviewPlayCard(s,"P1",new("quote",ResolutionResult.BuildPrompts(s)["P1"].PromptId!,s.Tick,command));Assert.True(quote.IsValid,quote.Message);
        var played=await Act(s,"P1",command);var done=await Top(played.State);
        Assert.Equal("BF2",done.State.ObjectLocations["A"].BattlefieldObjectId);Assert.Equal("BASE",done.State.ObjectLocations["D"].Zone);
        Assert.Contains("C",done.State.PlayerZones["P1"].MainDeck);Restore(done.State);
    }
}
