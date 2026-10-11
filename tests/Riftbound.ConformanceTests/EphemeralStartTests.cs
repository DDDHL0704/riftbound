using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
namespace Riftbound.ConformanceTests;
public sealed class EphemeralStartTests
{
    internal static MatchState Position()
    {
        var s=OfficialHoldSequenceTests.State("OGN·294/298");
        s=OfficialHoldSequenceTests.AddUnit(s,"E","OGN·001/298","F","P1");
        return s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["E"]=s.CardObjects["E"] with {Tags=s.CardObjects["E"].Tags.Concat([CardObjectTags.Ephemeral]).ToArray()} }};
    }
    internal static Task<ResolutionResult> Start(MatchState s)=>Act(s,s.TurnPlayerId,new PassPriorityCommand());
    [Fact]
    public async Task EphemeralWaitsForResponsesBeforeDestructionAndScoring()
    {
        var opened=await Start(Position());Assert.Single(opened.State.StackItems);
        Assert.Contains("E",opened.State.PlayerZones["P1"].Battlefields);Assert.Empty(opened.State.PlayerZones["P1"].Hand);
        Assert.Equal("SCORE",opened.State.TurnStartStep);Restore(opened.State);
        var done=await Top(opened.State);Assert.Contains("E",done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(MatchPhases.Main,done.State.Phase);Restore(done.State);
    }
    [Fact]
    public async Task AttachedAxePrintedEphemeralDoesNotTrigger()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["E"]=s.CardObjects["E"] with {Tags=[CardObjectTags.UnitCard]},
            ["AXE"]=new("AXE",cardNo:"SFD·186/221",ownerId:"P1",controllerId:"P1",attachedToObjectId:"E",
                tags:[CardObjectTags.EquipmentCard,"武装",CardObjectTags.Ephemeral]) }};
        s=At(s,"AXE","P1","BATTLEFIELD","F");
        var done=await Start(s);Assert.Empty(done.State.StackItems);Assert.Contains("AXE",done.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("E",done.State.CardObjects["AXE"].AttachedToObjectId);Restore(done.State);
    }

    internal static MatchState AxePosition(bool printedEphemeral=true)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["E"]=s.CardObjects["E"] with {Tags=[CardObjectTags.UnitCard]},
            ["AXE"]=new("AXE",cardNo:printedEphemeral?"SFD·186/221":"SFD·185/221",ownerId:"P1",controllerId:"P1",attachedToObjectId:"E",
                tags:printedEphemeral?[CardObjectTags.EquipmentCard,"武装",CardObjectTags.Ephemeral]:[CardObjectTags.EquipmentCard,"武装"]) }};
        return At(s,"AXE","P1","BATTLEFIELD","F");
    }
    internal static async Task<ResolutionResult> GrantAshes(MatchState s)
    {
        s=s with {Phase=MatchPhases.Main,TimingState=TimingStates.NeutralOpen,TurnStartStep=null,
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
                ["ASHES"]=new("ASHES",cardNo:"UNL-070/219",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard]) },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {["P1"]=s.PlayerZones["P1"] with {Hand=["ASHES"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["ASHES"]=new("P1","HAND")} };
        return await Top((await Act(s,"P1",new PlayCardCommand("ASHES","UNL-070/219",["AXE"]))).State);
    }
    internal static MatchState Beginning(MatchState s)=>s with {Phase=MatchPhases.TurnStart,TimingState=TimingStates.NeutralClosed,
        TurnStartStep="START",PriorityPlayerId=null,PassedPriorityPlayerIds=[],FocusPlayerId=null};
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ActualAshesGrantsActiveEphemeralEvenWhenAttachedCardAlreadyHasIt(bool printed)
    {
        var granted=await GrantAshes(AxePosition(printed));Assert.Equal(new[]{CardObjectTags.Ephemeral},granted.State.CardObjects["AXE"].GrantedTags);
        Assert.Equal("E",granted.State.CardObjects["AXE"].AttachedToObjectId);Restore(granted.State);
        var opened=await Start(Beginning(granted.State));Assert.Single(opened.State.StackItems);Restore(opened.State);
        var done=await Top(opened.State);Assert.Contains("AXE",done.State.PlayerZones["P1"].Graveyard);
        Assert.Null(done.State.CardObjects["AXE"].GrantedTags);Assert.Contains("E",done.State.PlayerZones["P1"].Battlefields);Restore(done.State);
    }
    [Theory]
    [InlineData("BASE")] [InlineData("OTHER")] [InlineData("controller")] [InlineData("keyword")]
    public async Task CapturedSelfReferenceSurvivesFieldMovementControlAndKeywordChanges(string change)
    {
        var opened=await Start(Position());var s=opened.State;
        if(change=="BASE")s=At(s,"E","P1","BASE");
        if(change=="OTHER")s=At(s,"E","P1","BATTLEFIELD","OTHER");
        if(change=="controller")s=At(s,"E","P2","BASE");
        if(change=="keyword")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["E"]=s.CardObjects["E"] with {Tags=[CardObjectTags.UnitCard]}}};
        Restore(s);var done=await Top(s);Assert.Contains("E",done.State.PlayerZones["P1"].Graveyard);Restore(done.State);
    }
    [Fact]
    public async Task ReturnedNewIncarnationIsNotDestroyedByOldEphemeralSkill()
    {
        var opened=await Start(Position());var s=opened.State with {CardObjects=new Dictionary<string,CardObjectState>(opened.State.CardObjects) {
            ["E"]=opened.State.CardObjects["E"] with {ObjectGeneration=1} }};
        Restore(s);var done=await Top(s);Assert.Contains("E",done.State.PlayerZones["P1"].Battlefields);Restore(done.State);
    }
    [Theory]
    [InlineData("hidden")] [InlineData("opponent")] [InlineData("dirty-controller")]
    public async Task OnlyVisibleControlledPermanentsTrigger(string branch)
    {
        var s=Position();if(branch=="opponent")s=At(s,"E","P2","BASE");
        else s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["E"]=branch=="hidden"
            ?s.CardObjects["E"] with {IsFaceDown=true,Tags=[CardObjectTags.UnitCard,CardObjectTags.Ephemeral,CardObjectTags.Standby]}
            :s.CardObjects["E"] with {ControllerId="P2"}}};
        var done=await Start(s);Assert.Empty(done.State.StackItems);Assert.True(done.State.CardObjects.ContainsKey("E"));
    }
    [Theory]
    [InlineData("F","P1",false,true)] [InlineData("OTHER","P1",false,false)]
    [InlineData("F","P2",false,false)] [InlineData("F","P1",true,false)]
    public async Task LeblancSuppressesOnlyVisibleFriendlySameFieldTrigger(string field,string controller,bool hidden,bool suppressed)
    {
        var s=OfficialHoldSequenceTests.AddUnit(Position(),"LEB","UNL-090/219",field,controller);
        if(hidden)s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["LEB"]=s.CardObjects["LEB"] with {IsFaceDown=true,Tags=[CardObjectTags.UnitCard,CardObjectTags.Standby]}}};
        var opened=await Start(s);Assert.Equal(suppressed?0:1,opened.State.StackItems.Count);
        if(!suppressed) {
            // Suppression added after capture cannot cancel an already-triggered skill.
            var response=At(opened.State,"LEB","P1","BATTLEFIELD","F");
            var done=await Top(response);Assert.Contains("E",done.State.PlayerZones["P1"].Graveyard);
        } else Assert.Contains("E",opened.State.PlayerZones["P1"].Battlefields);
    }
    [Theory]
    [InlineData("OGN·096/298",2)] [InlineData("UNL-172/219",3)] [InlineData("UNL-172a/219",3)]
    public async Task EphemeralDestructionQueuesLastBreathBeforeNormalTurnDraw(string card,int totalDraws)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["E"]=s.CardObjects["E"] with {CardNo=card}}};
        var death=await Top((await Start(s)).State);Assert.Single(death.State.StackItems);Assert.Empty(death.State.PlayerZones["P1"].Hand);Restore(death.State);
        var done=await TurnSequenceTestDriver.Complete(death);Assert.Equal(totalDraws,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Fact]
    public async Task RecallReplacementPreventsDeathWithoutRepeatingEphemeral()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["E"]=s.CardObjects["E"] with {UntilEndOfTurnEffects=["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"]}}};
        var done=await Top((await Start(s)).State);Assert.Contains("E",done.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(done.Events,e=>e.Kind=="UNIT_DESTROYED");Assert.Empty(done.State.StackItems);Restore(done.State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LabMayConsumeEphemeralAsCostBeforeEitherOrderedSkillResolves(bool reverse)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["F"]=s.CardObjects["F"] with {CardNo="UNL-209/219"}}};
        var opened=await Start(s);Assert.Equal(2,opened.State.TriggerQueue.Count);Restore(opened.State);
        var ids=opened.State.TriggerQueue.Select(t=>t.TriggerId).ToArray();if(reverse)Array.Reverse(ids);
        var ordered=await Act(opened.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:ids));
        var c=ordered.State.PendingCardChoice!;Assert.NotNull(c);
        var paid=await Act(ordered.State,"P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["E"]));
        Assert.Single(paid.Events,e=>e.Kind=="UNIT_DESTROYED");Assert.Empty(paid.State.PlayerZones["P1"].Hand);Restore(paid.State);
        var done=await TurnSequenceTestDriver.Complete(paid);Assert.Equal(2,done.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(done.Events,e=>e.Kind=="UNIT_DESTROYED");Restore(done.State);
    }
    [Theory]
    [InlineData("generation")] [InlineData("missing")] [InlineData("controller")] [InlineData("target")]
    public async Task InvalidEphemeralCapturedContextCannotRecover(string branch)
    {
        var opened=await Start(Position());var item=opened.State.StackItems.Single();
        item=item with {EphemeralStart=branch=="missing"?null:branch=="generation"?item.EphemeralStart! with {SourceGeneration=-1}:item.EphemeralStart,
            ControllerId=branch=="controller"?"P2":item.ControllerId,TargetObjectIds=branch=="target"?["E"]:[]};
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(opened.State with {StackItems=[item]}));
    }

    [Fact]
    public async Task CopiedLeblancTokenCeasesButCapturedLastBreathKeepsTwoDraws()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["E"]=s.CardObjects["E"] with {CardNo="UNL-172/219",TokenFactoryCardNo=P6TokenFactoryCatalog.ImageTokenCardNo} }};
        var death=await Top((await Start(s)).State);Assert.False(death.State.CardObjects.ContainsKey("E"));
        Assert.Equal("UNL-172/219",Assert.Single(death.State.StackItems).CardNo);Restore(death.State);
        var done=await TurnSequenceTestDriver.Complete(death);Assert.Equal(3,done.State.PlayerZones["P1"].Hand.Count);Restore(done.State);
    }
    [Fact]
    public async Task LeblancLastBreathOutsideOwnBeginningDrawsOnlyOne()
    {
        var s=At(Position(),"E","P2","BASE");
        s=s with {Phase=MatchPhases.Main,TimingState=TimingStates.NeutralOpen,
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
                ["E"]=s.CardObjects["E"] with {CardNo="UNL-172/219",OwnerId="P2"},
                ["KILL"]=new("KILL",cardNo:"OGN·229/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard]) },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {
                ["P1"]=s.PlayerZones["P1"] with {Hand=["KILL"]},
                ["P2"]=s.PlayerZones["P2"] with {MainDeck=["P2-DRAW"]} },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) {["KILL"]=new("P1","HAND"),["P2-DRAW"]=new("P2","MAIN_DECK")} };
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {["P2-DRAW"]=new("P2-DRAW",cardNo:"OGN·001/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard])}};
        var death=await Top((await Act(s,"P1",new PlayCardCommand("KILL","OGN·229/298",["E"]))).State);
        Assert.Single(death.State.StackItems);var done=await Top(death.State);Assert.Single(done.State.PlayerZones["P2"].Hand);Restore(done.State);
    }
    [Fact]
    public async Task BothPlayersAndSpectatorRejectChangedEphemeralContext()
    {
        var opened=await Start(Position());var s=opened.State;
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,opened.Snapshots[id],s.Tick,0,opened.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        foreach(var snapshot in views.Values.Select(v=>v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var payload=Assert.IsType<Dictionary<string,object?>>(Assert.Single(snapshot.Stack));var original=payload["ephemeralStart"];
            payload["ephemeralStart"]=s.StackItems[0].EphemeralStart! with {SourceGeneration=99};
            Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator),e=>e.Contains("ephemeral start context differs"));
            payload["ephemeralStart"]=original;
        }
    }
    [Theory]
    [InlineData("missing-tag")] [InlineData("duplicate")] [InlineData("empty")] [InlineData("blank")]
    public void GrantedKeywordProvenanceMustBeCanonicalAndPresentInActualTags(string change)
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["E"]=s.CardObjects["E"] with {GrantedTags=change switch {
                "missing-tag"=>[CardObjectTags.Spellshield],"duplicate"=>[CardObjectTags.Ephemeral,CardObjectTags.Ephemeral],"blank"=>[""],_=>[]}} }};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s),e=>e.Contains("invalid granted tags"));
    }
    [Fact]
    public async Task BothPlayersAndSpectatorRejectChangedGrantedKeywordProjection()
    {
        var result=await GrantAshes(AxePosition());var s=result.State;
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,result.Snapshots[id],s.Tick,0,result.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        foreach(var snapshot in views.Values.Select(v=>v.Snapshot).Append(spectator.SpectatorSnapshot)) {
            var player=Assert.IsType<Dictionary<string,object?>>(snapshot.Players["P1"]);
            var objects=Assert.IsType<Dictionary<string,object?>>(player["objects"]);
            var axe=Assert.IsType<Dictionary<string,object?>>(objects["AXE"]);var original=axe["grantedTags"];
            axe["grantedTags"]=Array.Empty<string>();
            Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator),e=>e.Contains("granted tags differ"));
            axe["grantedTags"]=original;
        }
    }
}
