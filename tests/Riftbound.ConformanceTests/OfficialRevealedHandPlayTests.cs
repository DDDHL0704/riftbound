using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;

public sealed class OfficialRevealedHandPlayTests
{
    [Theory]
    [InlineData("OGN·208/298")]
    [InlineData("SFD·075/221")]
    [InlineData("SFD·071/221")]
    public async Task OwnerPlaysSelectedUnitToChosenEnemyControlledBattlefieldIgnoringAllCosts(string card)
    {
        var opened = await Open(Position(card));
        var p = opened.State.PendingEffectPlay!;
        Assert.Equal("P1", p.PlayerId);
        Assert.Contains("SPELL", p.RevealedHand!.Cards.Keys);
        Assert.Equal(new[]{"U"}, p.Sources.Keys);
        Restore(opened.State);
        var selected = await Select(opened.State, "U");
        Assert.Equal("P2", selected.State.PendingEffectPlay!.PlayerId);
        Assert.Equal("P2", selected.State.ActivePlayerId);
        Restore(selected.State);
        var cmd = new PlayCardCommand("U", card, [], Destination:"BATTLEFIELD:BF");
        var quote = new CoreRuleEngine().PreviewPlayCard(selected.State, "P2", PlayCostPreviewTests.Request(selected.State,cmd));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var done = await Act(selected.State, "P2", cmd);
        Assert.Null(done.State.PendingEffectPlay);
        Assert.Equal("BF", done.State.ObjectLocations["U"].BattlefieldObjectId);
        Assert.Contains("U", done.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("P2", done.State.CardObjects["U"].ControllerId);
        Assert.Contains("STUNNED", done.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.DoesNotContain("STUNNED",done.State.CardObjects["BF"].UntilEndOfTurnEffects);
        Assert.Single(done.Events,e=>e.Kind=="STATUS_EFFECT_APPLIED");
        Assert.True(done.Events.Count(e=>e.Kind=="SPELL_DUEL_STARTED")<=1);
        var orderedEvents=done.Events.Select(e=>e.Kind).ToList();
        if(orderedEvents.Contains("SPELL_DUEL_STARTED"))Assert.True(orderedEvents.IndexOf("STACK_ITEM_RESOLVED")<orderedEvents.IndexOf("SPELL_DUEL_STARTED"));
        Assert.Contains("ALLY", done.State.PlayerZones["P2"].Base);
        Assert.Equal(RunePool.Empty, done.State.RunePools["P2"]);
        Assert.DoesNotContain(done.Events, e=>e.Kind=="UNIT_DESTROYED");
        Restore(done.State);
    }

    [Fact]
    public async Task HandIsPrivateUntilResolutionAndUnselectedCardsBecomePrivateAgain()
    {
        var initial = Position();
        string View(MatchState s) => JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(s)["P1"]);
        Assert.DoesNotContain("UNL-200/219", View(initial));
        var cast = await Act(initial,"P1",new PlayCardCommand("C","UNL-139/219",["BF"]));
        Assert.DoesNotContain("UNL-200/219", View(cast.State));
        var opened = await Top(cast.State);
        Assert.Contains("UNL-200/219", View(opened.State));
        var shown=ResolutionResult.BuildSnapshots(opened.State)["P1"].Table!.Players.Single(p=>p.PlayerId=="P2").Zones;
        Assert.Equal(new[]{"U","SPELL"},shown.Hand);Assert.Equal(0,shown.HandHidden);
        Assert.DoesNotContain("OGN·097/298", View(opened.State)); // Unrelated deck identity stays hidden.
        var declined = await Select(opened.State);
        Assert.DoesNotContain("UNL-200/219", View(declined.State));
        var hidden=ResolutionResult.BuildSnapshots(declined.State)["P1"].Table!.Players.Single(p=>p.PlayerId=="P2").Zones;
        Assert.Empty(hidden.Hand);Assert.Equal(2,hidden.HandHidden);
        Assert.Equal(new[]{"U","SPELL"},declined.State.PlayerZones["P2"].Hand);
        Assert.Empty(declined.State.StackItems); Restore(declined.State);
    }

    [Fact]
    public async Task InvalidActorSourceDestinationAndAdditionalCostCannotMutateState()
    {
        var opened = await Open();
        await Reject(opened.State,"P2",new ChooseCardsCommand(opened.State.PendingEffectPlay!.ChoiceId,"REVEALED_HAND_PLAY",["U"]));
        await Reject(opened.State,"P1",new ChooseCardsCommand(opened.State.PendingEffectPlay!.ChoiceId,"REVEALED_HAND_PLAY",["SPELL"]));
        var selected = await Select(opened.State,"U");
        foreach(var command in new[]{new PlayCardCommand("U","OGN·208/298",[]),
            new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF2"),
            new PlayCardCommand("SPELL","UNL-200/219",["ALLY"],Destination:"BATTLEFIELD:BF"),
            new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF",OptionalCosts:["DESTROY_FRIENDLY_UNIT:ALLY"])})
            await Reject(selected.State,"P2",command);
        await Reject(selected.State,"P1",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF"));
    }

    [Fact]
    public async Task PlayAbilityGetsItsOwnTargetsAndResponseAfterEntry()
    {
        var opened = await Open(Position("SFD·062/221"));
        var selected = await Select(opened.State,"U");
        var child = await Act(selected.State,"P2",new PlayCardCommand("U","SFD·062/221",["ALLY"],Destination:"BATTLEFIELD:BF"));
        Assert.True(child.State.CardObjects["ALLY"].IsExhausted);
        Assert.Contains("STUNNED",child.State.CardObjects["U"].UntilEndOfTurnEffects);
        Assert.Single(child.State.StackItems); Restore(child.State);
        var done = await Top(child.State);
        Assert.False(done.State.CardObjects["ALLY"].IsExhausted); Restore(done.State);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("chooser")]
    [InlineData("destination")]
    [InlineData("waiver")]
    [InlineData("reveal")]
    [InlineData("generation")]
    public async Task RecoveryRejectsForgedRevealedHandPermissions(string mutation)
    {
        var opened=await Open(); var p=opened.State.PendingEffectPlay!; var h=p.RevealedHand!;
        p=mutation switch {
            "owner"=>p with {RevealedHand=h with {OwnerId="P1"}},
            "chooser"=>p with {PlayerId="P2"},
            "destination"=>p with {RevealedHand=h with {BattlefieldId="BF2"}},
            "waiver"=>p with {IgnoreAllCosts=false},
            "reveal"=>p with {RevealedHand=h with {Cards=new Dictionary<string,long>{{"DECK",0}}}},
            _=>p with {Sources=new Dictionary<string,long>{{"U",99}}}
        };
        var bad=opened.State with {PendingEffectPlay=p};
        Assert.Contains("invalid revealed hand effect-play continuation",OfficialInsightAndSpellLockTests.Errors(bad));
    }

    [Fact]
    public async Task NoBattlefieldTargetOrEmptyHandDoesNotRevealOrDeadlock()
    {
        var initial=Position();
        await Reject(initial,"P1",new PlayCardCommand("C","UNL-139/219",["U"]));
        var empty=initial with {PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P2"]=initial.PlayerZones["P2"] with {Hand=[]}}};
        var done=await Open(empty);Assert.Null(done.State.PendingEffectPlay);Assert.Empty(done.State.StackItems);
    }

    [Fact]
    public async Task AllCostWaiverOverridesSurchargesAndCannotBuyHaste()
    {
        var initial=Position("UNL-006/219") with {UntilEndOfTurnEffects=["BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P2:3"]};
        var opened=await Open(initial);var selected=await Select(opened.State,"U");
        var play=new PlayCardCommand("U","UNL-006/219",[],Destination:"BATTLEFIELD:BF");
        await Reject(selected.State,"P2",play with {OptionalCosts=["HASTE"]});
        var done=await Act(selected.State,"P2",play);
        Assert.True(done.State.CardObjects["U"].IsExhausted);Assert.Equal(RunePool.Empty,done.State.RunePools["P2"]);
        Restore(done.State);
    }

    [Fact]
    public async Task AllCostWaiverAlsoRemovesWardOnThePlayedUnitsAbilityTarget()
    {
        var initial=Position("OGN·097/298");
        initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){
            ["ENEMY"]=new("ENEMY",cardNo:"SFD·071/221",power:6,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard,"法盾"])},
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {Base=["ENEMY"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["ENEMY"]=new("P1","BASE")}};
        var opened=await Open(initial);var selected=await Select(opened.State,"U");
        var play=new PlayCardCommand("U","OGN·097/298",["ENEMY"],Destination:"BATTLEFIELD:BF");
        var quote=new CoreRuleEngine().PreviewPlayCard(selected.State,"P2",PlayCostPreviewTests.Request(selected.State,play));
        Assert.True(quote.CanPay,quote.Message);Assert.Equal(0,quote.Cost!.GenericPower+quote.Cost.PowerByTrait.Values.Sum());
        var child=await Act(selected.State,"P2",play);Assert.Single(child.State.StackItems);
        var done=await Top(child.State);Assert.Equal(-2,done.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifier);Restore(done.State);
    }

    [Fact]
    public async Task CannotPlayRestrictionRemainsEvenWhenAllCostsAreIgnored()
    {
        var initial=Position();initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["BF"]=initial.CardObjects["BF"] with {CardNo="SFD·216/221"}}};
        var opened=await Open(initial);var selected=await Select(opened.State,"U");
        await Reject(selected.State,"P2",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF"));
        var p=selected.State.PendingEffectPlay!;
        var done=await Act(selected.State,"P2",new ChooseCardsCommand(p.ChoiceId,"EFFECT_PLAY",[]));
        Assert.Null(done.State.PendingEffectPlay);Assert.Contains("U",done.State.PlayerZones["P2"].Hand);Assert.Empty(done.State.CardObjects["U"].UntilEndOfTurnEffects);Restore(done.State);
    }

    [Fact]
    public async Task StandbyRevealUsesTheSamePublicSelectionAndCrossPlayerContinuation()
    {
        var initial=Position();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·048/298",out var bottom));
        initial=initial with {TimingState=TimingStates.NeutralClosed,PriorityPlayerId="P1",
            StackItems=[new("BOTTOM","P2","BOTTOMCARD",bottom.EffectKind,bottom.CardNo,[])],
            RunePools=new Dictionary<string,RunePool>{{"P1",RunePool.Empty},{"P2",RunePool.Empty}},
            CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["BOTTOMCARD"]=new("BOTTOMCARD",cardNo:bottom.CardNo,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.SpellCard]),["C"]=initial.CardObjects["C"] with {IsFaceDown=true,Tags=[CardObjectTags.SpellCard,CardObjectTags.Standby]}},
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {Hand=[],Battlefields=["BF","C"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["C"]=new("P1","BATTLEFIELD","BF")}};
        var cast=await Act(initial,"P1",new RevealCardCommand("C","UNL-139/219",["BF"],"STANDBY_REACTION",["STANDBY_REVEAL_0"],"STACK"));
        var opened=await Top(cast.State);Restore(opened.State);
        var selected=await Select(opened.State,"U");
        var done=await Act(selected.State,"P2",new PlayCardCommand("U","OGN·208/298",[],Destination:"BATTLEFIELD:BF"));
        Assert.Contains("STUNNED",done.State.CardObjects["U"].UntilEndOfTurnEffects);Restore(done.State);
    }

    [Fact]
    public async Task PlayerAndSpectatorRecoveryHonorOnlyTheCurrentRevealWindow()
    {
        var opened=await Open();var closed=await Select(opened.State);
        foreach(var state in new[]{opened.State,closed.State})
        {
            var snapshots=ResolutionResult.BuildSnapshots(state);var prompts=ResolutionResult.BuildPrompts(state);
            var views=state.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,state.Tick,0,snapshots[id],state.Tick,0,prompts[id]));
            var frame=MatchReplayRedactor.BuildSpectatorFrame(state.RoomId,state.Tick,0,[],state);
            var errors=MatchRecoveryValidator.Validate(state.RoomId,0,[],[],views,state,state.Tick,frame);
            Assert.True(errors.Count==0,string.Join("\n",errors));
        }
    }

    internal static async Task<ResolutionResult> Open(MatchState? state=null)
    {
        var cast=await Act(state??Position(),"P1",new PlayCardCommand("C","UNL-139/219",["BF"]));
        return await Top(cast.State);
    }
    internal static Task<ResolutionResult> Select(MatchState state,params string[] ids)
        => Act(state,"P1",new ChooseCardsCommand(state.PendingEffectPlay!.ChoiceId,"REVEALED_HAND_PLAY",ids));
    private static async Task Reject(MatchState state,string player,GameCommand command)
    {
        var result=await new CoreRuleEngine().ResolveAsync(state,new("invalid",player,command.CmdType),command,default);
        Assert.False(result.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(result.State));
    }
    internal static MatchState Position(string unit="OGN·208/298")
    {
        var s=OfficialSpellCompletionTests.Position("UNL-139/219",withSources:false);
        return s with {
            RunePools=new Dictionary<string,RunePool>{{"P1",new(2,2)},{"P2",RunePool.Empty}},
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
                ["BF"]=new("BF",cardNo:"OGN·296/298",ownerId:"P1",controllerId:"P1",tags:["CARD_TYPE:BATTLEFIELD"]),
                ["BF2"]=new("BF2",cardNo:"OGN·297/298",ownerId:"P2",controllerId:"P2",tags:["CARD_TYPE:BATTLEFIELD"]),
                ["U"]=new("U",cardNo:unit,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]),
                ["SPELL"]=new("SPELL",cardNo:"UNL-200/219",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.SpellCard]),
                ["DECK"]=new("DECK",cardNo:"OGN·097/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]),
                ["ALLY"]=new("ALLY",cardNo:"SFD·075/221",ownerId:"P2",controllerId:"P2",power:3,isExhausted:true,tags:[CardObjectTags.UnitCard,"机械"])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){
                ["P1"]=s.PlayerZones["P1"] with {Battlefields=["BF"]},
                ["P2"]=s.PlayerZones["P2"] with {Hand=["U","SPELL"],MainDeck=["DECK"],Base=["ALLY"],Battlefields=["BF2"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){
                ["BF"]=new("P1","BATTLEFIELD","BF"),["BF2"]=new("P2","BATTLEFIELD","BF2"),
                ["U"]=new("P2","HAND"),["SPELL"]=new("P2","HAND"),["DECK"]=new("P2","MAIN_DECK"),["ALLY"]=new("P2","BASE")}};
    }
}
