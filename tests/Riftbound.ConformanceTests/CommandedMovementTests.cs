using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;

namespace Riftbound.ConformanceTests;

public sealed class CommandedMovementTests
{
    internal const string Spell="UNL-101/219";
    internal static MatchState Position()
    {
        var s=ChosenMovementTests.Board(2);
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["AOE"]=s.CardObjects["AOE"] with {CardNo=Spell},
            ["ANCHOR"]=new("ANCHOR",cardNo:"OGN·001/298",power:2,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]) },
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(10,0)} };
        s=At(At(s,"BF2","P1","BATTLEFIELD","BF2"),"ANCHOR","P1","BATTLEFIELD","BF2");
        return s;
    }
    internal static PlayCardCommand Command(string destination="BATTLEFIELD:BF2",params string[] targets)
        =>new("AOE",Spell,targets.Length==0?["A"]:targets,Destination:destination);
    internal static async Task<ResolutionResult> Open(MatchState? s=null)
        =>await Top((await Act(s??Position(),"P1",Command())).State);

    [Fact]
    public async Task OpponentChoosesTheirUnitAfterSeeingTheFirstMovement()
    {
        var opened=await Open();var p=Assert.IsType<RuleChoiceContinuation>(opened.State.PendingRuleChoice);
        Assert.Equal("P2",p.Request.PlayerId);
        Assert.Equal(new[]{"MOVE:D","MOVE:D2"},p.Request.Options.Select(o=>o.Id).Order());
        var view=JsonSerializer.SerializeToElement(opened.Snapshots["P2"],new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("BF2",view.GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("A").GetProperty("location").GetProperty("battlefieldObjectId").GetString());
        Restore(opened.State);
        var done=await DestructionOrderTests.Select(opened.State,"MOVE:D2");
        Assert.Equal("BF2",done.State.ObjectLocations["A"].BattlefieldObjectId);
        Assert.Equal("BF2",done.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal("BF",done.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.False(done.State.CardObjects["A"].IsExhausted);
        var moves=done.Events.Where(e=>e.Kind=="UNIT_MOVED_TO_BATTLEFIELD").ToArray();Assert.Equal(2,moves.Length);
        Assert.Equal("P1",moves[0].Payload["movingPlayerId"]);Assert.Equal("P2",moves[1].Payload["movingPlayerId"]);Restore(done.State);
    }

    [Theory]
    [InlineData("BATTLEFIELD:BF")] [InlineData("BASE:P1")] [InlineData("")] [InlineData("BATTLEFIELD:ABSENT")]
    public void PreflightRequiresAnExistingControlledBattlefield(string destination)
    {
        var s=Position();var prompt=ResolutionResult.BuildPrompts(s)["P1"];
        var q=new CoreRuleEngine().PreviewPlayCard(s,"P1",new("q",prompt.PromptId!,s.Tick,Command(destination)));
        Assert.False(q.IsValid);
    }

    [Fact]
    public async Task CasterCannotPreselectTheOpponentsUnit()
    {
        var s=Position();var result=await new CoreRuleEngine().ResolveAsync(s,new("invalid","P1",CommandTypes.PlayCard),Command("BATTLEFIELD:BF2","A","D"),default);
        Assert.False(result.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ForgedPreviewAndChoiceCannotSurviveRecoveryOrChangeTheBoard()
    {
        var opened=await Open();var s=opened.State;var p=s.PendingRuleChoice!;
        foreach(var (player,id,option) in new[]{("P1",p.Request.Id,"MOVE:D"),("P2","stale","MOVE:D"),("P2",p.Request.Id,"MOVE:A")}) {
            var result=await new CoreRuleEngine().ResolveAsync(s,new("bad",player,CommandTypes.PayCost),new PayCostCommand(id,"RULE_REPLACEMENT",[option]),default);
            Assert.False(result.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(result.State));
        }
        var preview=p.Request.Preview!;
        foreach(var request in new[]{p.Request with {Preview=null},p.Request with {PlayerId="P1"},
            p.Request with {Preview=At(preview,"A","P1","BASE")}})
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with {PendingRuleChoice=p with {Request=request}}));
        var restored=OfficialInsightAndSpellLockTests.Restore(s);
        var done=await DestructionOrderTests.Select(restored,"MOVE:D");Assert.Equal("BF2",done.State.ObjectLocations["D"].BattlefieldObjectId);Restore(done.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IneligibleOpponentUnitsAreExcludedAndNoEligibleUnitDoesNotUndoFirstMove(bool none)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["D"]=s.CardObjects["D"] with {UntilEndOfTurnEffects=[MovementRestrictionRules.ForPlayer("P2")]},
            ["D2"]=s.CardObjects["D2"] with {UntilEndOfTurnEffects=none?[MovementRestrictionRules.ForPlayer("P2")]:[]} }};
        var opened=await Open(s);
        if(none) {Assert.Null(opened.State.PendingRuleChoice);Assert.Equal("BF2",opened.State.ObjectLocations["A"].BattlefieldObjectId);}
        else {Assert.Equal("MOVE:D2",Assert.Single(opened.State.PendingRuleChoice!.Request.Options).Id);var done=await DestructionOrderTests.Select(opened.State,"MOVE:D2");Restore(done.State);}
        Assert.Equal("BF",opened.State.ObjectLocations["D"].BattlefieldObjectId);
    }

    [Fact]
    public async Task OwnSelectedUnitDoesNotChargeOpponentWardAndEquipmentFollows()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["D"]=s.CardObjects["D"] with {Tags=[CardObjectTags.UnitCard,"法盾9"]},
            ["GEAR"]=new("GEAR",ownerId:"P2",controllerId:"P2",attachedToObjectId:"D",isExhausted:true,tags:[CardObjectTags.EquipmentCard]) }};
        s=At(s,"GEAR","P2","BATTLEFIELD","BF");
        var opened=await Open(s);var done=await DestructionOrderTests.Select(opened.State,"MOVE:D");
        Assert.Equal(7,done.State.RunePools["P1"].Mana);Assert.Equal(s.RunePools["P2"],done.State.RunePools["P2"]);
        Assert.Equal("BF2",done.State.ObjectLocations["GEAR"].BattlefieldObjectId);Assert.True(done.State.CardObjects["GEAR"].IsExhausted);
        Assert.Single(done.Events,e=>e.Kind=="EQUIPMENT_MOVED_WITH_UNIT");Restore(done.State);
    }

    internal static MatchState QaPosition()
    {
        var s=Position();var cards=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["D"]=s.CardObjects["D"] with {CardNo=MovementDiscardTests.Merchant},
            ["V"]=new("V",cardNo:"OGN·158/298",power:10,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]) };
        foreach(var id in new[]{"H1","H2","P1DRAW","P2DRAW"}) {
            var player=id=="P1DRAW"?"P1":"P2";
            cards[id]=new(id,cardNo:"OGN·004/298",ownerId:player,controllerId:player,tags:[CardObjectTags.UnitCard]);
        }
        s=s with {CardObjects=cards,PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {
            ["P1"]=s.PlayerZones["P1"] with {MainDeck=["P1DRAW"]},
            ["P2"]=s.PlayerZones["P2"] with {MainDeck=["P2DRAW"],Hand=["H1","H2"]} }};
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["BF3"]=s.CardObjects["BF"] with {ObjectId="BF3"}}};
        return At(At(s,"BF3","P1","BATTLEFIELD","BF3"),"V","P1","BATTLEFIELD","BF3");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OfficialMerchantVolibearQaUsesTurnPlayerFirstInsertion(bool merchantTurn)
    {
        var s=QaPosition();ResolutionResult moved;
        if(merchantTurn) {
            s=At(s,"D","P2","BASE") with {TurnPlayerId="P2",ActivePlayerId="P2",PriorityPlayerId="P2"};
            moved=await Act(s,"P2",new MoveUnitCommand("D","BASE","BATTLEFIELD:BF2",[]));
        } else moved=await DestructionOrderTests.Select((await Open(s)).State,"MOVE:D");
        Assert.Equal(2,moved.State.TriggerQueue.Count);
        var resolutionOrder=moved.State.TriggerQueue.OrderBy(t=>t.ControllerId==moved.State.TurnPlayerId?1:0).Select(t=>t.TriggerId).ToArray();
        moved=await Act(moved.State,moved.State.TurnPlayerId,new OrderTriggersCommand(OrderedTriggerIds:resolutionOrder));
        Assert.Equal(merchantTurn?new[]{"D","V"}:new[]{"V","D"},moved.State.StackItems.Select(i=>i.SourceObjectId));Restore(moved.State);
        var top=await Top(moved.State);
        if(merchantTurn) {
            Assert.Contains("P1DRAW",top.State.PlayerZones["P1"].Hand);Assert.Null(top.State.PendingHandChoice);
            top=await Top(top.State);
        } else Assert.DoesNotContain("P1DRAW",top.State.PlayerZones["P1"].Hand);
        var choice=Assert.IsType<PendingHandChoiceState>(top.State.PendingHandChoice);Assert.Equal("P2",choice.PlayerId);
        var discard=await Act(top.State,"P2",new ChooseHandCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["H2"]));
        Assert.Contains("P2DRAW",discard.State.PlayerZones["P2"].Hand);Assert.Contains("H2",discard.State.PlayerZones["P2"].Graveyard);
        var done=merchantTurn?discard:await Top(discard.State);Assert.Contains("P1DRAW",done.State.PlayerZones["P1"].Hand);Restore(done.State);
    }

    [Fact]
    public async Task EchoAsksOpponentSeparatelyAndRetainsTheFirstAnswerAcrossRecovery()
    {
        var s=Position();s=s with {UntilEndOfTurnEffects=[EchoCostRules.GrantPrefix+"P1"],
            RunePools=new Dictionary<string,RunePool>(s.RunePools) {["P1"]=new(20,20)},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A2"]=s.CardObjects["A"] with {ObjectId="A2"}}};
        s=At(s,"A2","P1","BASE");
        var command=Command() with {OptionalCosts=["ECHO"],RepeatChoices=[new("",["A2"])]};
        var played=await Act(s,"P1",command);Assert.Equal(2,played.State.StackItems.Last().RepeatExecutions!.Count);
        var first=await Top(played.State);var second=await DestructionOrderTests.Select(first.State,"MOVE:D");
        Restore(second.State);Assert.Single(second.State.PendingRuleChoice!.Answers);
        Assert.Equal("MOVE:D2",Assert.Single(second.State.PendingRuleChoice.Request.Options).Id);
        var done=await DestructionOrderTests.Select(OfficialInsightAndSpellLockTests.Restore(second.State),"MOVE:D2");
        foreach(var id in new[]{"A","A2","D","D2"})Assert.Equal("BF2",done.State.ObjectLocations[id].BattlefieldObjectId);
        Assert.Equal(4,done.Events.Count(e=>e.Kind=="UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal(2,done.Events.Count(e=>e.Kind=="SPELL_EXECUTION_COMPLETED"));Restore(done.State);
    }

    [Fact]
    public async Task IntermediatePreviewDoesNotRevealPrivateCardsOrOfferResponsePriority()
    {
        var opened=await Open(QaPosition());
        var p1=JsonSerializer.Serialize(opened.Snapshots["P1"]);var p2=JsonSerializer.Serialize(opened.Snapshots["P2"]);
        Assert.DoesNotContain("P2DRAW",p1);Assert.DoesNotContain("P1DRAW",p2);Assert.DoesNotContain("H1",p1);Assert.Contains("H1",p2);
        Assert.DoesNotContain(opened.Prompts["P1"].Candidates??[],c=>c.Action==CommandTypes.PassPriority);
        Assert.DoesNotContain(opened.Prompts["P2"].Candidates??[],c=>c.Action==CommandTypes.PlayCard);
        Restore(opened.State);
    }

    [Theory]
    [InlineData("generation")] [InlineData("control")] [InlineData("prohibited")] [InlineData("battlefield-control")]
    public async Task InvalidFirstMovementStillAttemptsIndependentOpponentInstruction(string change)
    {
        var cast=await Act(Position(),"P1",Command());var s=cast.State;
        if(change=="generation")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A"]=s.CardObjects["A"] with {ObjectGeneration=1}}};
        if(change=="control")s=At(s,"A","P2","BASE");
        if(change=="prohibited")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["A"]=s.CardObjects["A"] with {UntilEndOfTurnEffects=[MovementRestrictionRules.ForPlayer("P1")]}}};
        if(change=="battlefield-control")s=At(At(s,"ANCHOR","P2","BATTLEFIELD","BF2"),"BF2","P2","BATTLEFIELD","BF2");
        var opened=await Top(s);var done=await DestructionOrderTests.Select(opened.State,"MOVE:D");
        Assert.Equal("BASE",done.State.ObjectLocations["A"].Zone);Assert.Equal("BF2",done.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.Single(done.Events,e=>e.Kind=="UNIT_MOVED_TO_BATTLEFIELD");Restore(done.State);
    }

    [Fact]
    public async Task GraveyardRecastUsesMovementDestinationAndStillLetsOpponentChoose()
    {
        var s=OfficialGraveyardRecastTests.Kaisa("OGN·112/298");
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["C"]=s.CardObjects["C"] with {CardNo=Spell}},
            PlayerScores=new Dictionary<string,int>(s.PlayerScores) {["P1"]=4}};
        var conquered=await ConquestLifecycleRegressionTests.Conquer(s);var opened=await Top(conquered.State);Assert.NotNull(opened.State.PendingEffectPlay);
        var seed=Position();var cards=new Dictionary<string,CardObjectState>(opened.State.CardObjects);
        foreach(var id in new[]{"A","BF2","ANCHOR","D"})cards[id]=seed.CardObjects[id];
        s=opened.State with {CardObjects=cards};s=At(At(At(At(s,"A","P1","BASE"),"BF2","P1","BATTLEFIELD","BF2"),"ANCHOR","P1","BATTLEFIELD","BF2"),"D","P2","BASE");Restore(s);
        var command=new PlayCardCommand("C",Spell,["A"],Destination:"BATTLEFIELD:BF2");
        var quote=new CoreRuleEngine().PreviewPlayCard(s,"P1",new("quote",ResolutionResult.BuildPrompts(s)["P1"].PromptId!,s.Tick,command));Assert.True(quote.IsValid,quote.Message);
        var played=await Act(s,"P1",command);var choice=await Top(played.State);Assert.Equal("P2",choice.State.PendingRuleChoice!.Request.PlayerId);
        var done=await DestructionOrderTests.Select(choice.State,"MOVE:D");
        Assert.Equal("BF2",done.State.ObjectLocations["A"].BattlefieldObjectId);Assert.Equal("BF2",done.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.Contains("C",done.State.PlayerZones["P1"].MainDeck);Restore(done.State);
    }
}
