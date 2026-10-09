using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialInsightTriggerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LibraryWaitsForResponseThenPrivatelyKeepsOrRecycles(bool recycle)
    {
        var initial=Library();
        initial=initial with {PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {MainDeck=["D1","D2","D3","D4"]}},
            CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["D4"]=initial.CardObjects["D3"] with {ObjectId="D4"}}};
        var played=await Act(initial,"P1",new PlayCardCommand("C","OGN·083/298",[]));
        Assert.Equal(initial.PlayerZones["P1"].MainDeck,played.State.PlayerZones["P1"].MainDeck);
        Assert.Single(played.State.StackItems); Assert.Null(played.State.StackItems[0].InsightContext); Restore(played.State);
        var completed=await ResolveTop(played.State);
        Assert.Equal(["D1","D2"],completed.State.PlayerZones["P1"].Hand);
        Assert.Single(completed.State.StackItems); Assert.NotNull(completed.State.StackItems[0].InsightContext);
        var opened=await ResolveTop(completed.State);
        Assert.Equal(["D3"],opened.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("\"D3\"",JsonSerializer.Serialize(opened.Prompts["P2"]));
        Assert.DoesNotContain("\"D3\"",JsonSerializer.Serialize(opened.Snapshots["P2"])); Restore(opened.State);
        var done=await Choose(opened.State,recycle?["D3"]:[]);
        Assert.Empty(done.State.StackItems);
        Assert.Equal(recycle?"D4":"D3",done.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Contains("LIB",done.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain("LIB",done.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("\"D3\"",JsonSerializer.Serialize(done.Events)); Restore(done.State);
    }

    [Theory]
    [InlineData("P2",false,"OGN·083/298",false)]
    [InlineData("P1",true,"OGN·083/298",false)]
    [InlineData("P1",false,"SFD·125/221",false)]
    [InlineData("P1",false,"OGN·064/298",false)]
    [InlineData("P1",false,"OGN·064/298",true)]
    public async Task LibraryUsesControlVisibilityCardTypeAndActuallyPaidMana(string controller,bool faceDown,string card,bool echo)
    {
        var state=Library(card);
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["LIB"]=state.CardObjects["LIB"] with {ControllerId=controller,IsFaceDown=faceDown}}};
        var played=await Act(state,"P1",new PlayCardCommand("C",card,card=="OGN·064/298"?["S1"]:[],OptionalCosts:echo?["ECHO"]:[]));
        if (played.State.StackItems.Count>0) played=await ResolveTop(played.State);
        Assert.Equal(echo?1:0,played.State.StackItems.Count(s=>s.InsightContext is not null));
    }

    [Fact]
    public async Task CapturedLibrarySurvivesControlChangeAndUsesCurrentDeckTop()
    {
        var played=await Act(Library(),"P1",new PlayCardCommand("C","OGN·083/298",[]));
        played=await ResolveTop(played.State);
        var state=played.State with {CardObjects=new Dictionary<string,CardObjectState>(played.State.CardObjects){["LIB"]=played.State.CardObjects["LIB"] with {ControllerId="P2"}},
            PlayerZones=new Dictionary<string,PlayerZones>(played.State.PlayerZones){["P1"]=played.State.PlayerZones["P1"] with {MainDeck=["D2","D3"],Hand=["D1"]}}};
        var opened=await ResolveTop(state);
        Assert.Equal("P1",opened.State.PendingCardChoice!.PlayerId);Assert.Equal(["D2"],opened.State.PendingCardChoice.ContextObjectIds);
        var item=opened.State.StackItems[^1];
        var forged=opened.State with {StackItems=opened.State.StackItems.Take(opened.State.StackItems.Count-1).Append(item with {InsightContext=item.InsightContext! with {Count=2}}).ToArray()};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged),e=>e.Contains("Insight"));Restore(opened.State);
    }

    [Fact]
    public async Task EveryControlledLibraryQueuesAnIndependentTrigger()
    {
        var state=Library();
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["LIB2"]=new("LIB2",cardNo:"UNL-211/219",ownerId:"P1",controllerId:"P1",tags:[P6TokenFactoryCatalog.BattlefieldCardTag])},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Battlefields=["LIB2"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["LIB2"]=new("P1","BATTLEFIELD","LIB2")}};
        var played=await Act(state,"P1",new PlayCardCommand("C","OGN·083/298",[]));
        played=await ResolveTop(played.State);
        Assert.Equal(2,played.State.TriggerQueue.Count(t=>t.InsightContext is not null));Restore(played.State);
        var ordered=await Act(played.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:played.State.TriggerQueue.Select(t=>t.TriggerId).Reverse().ToArray()));
        Assert.Equal(2,ordered.State.StackItems.Count(s=>s.InsightContext is not null));Restore(ordered.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task VisionaryDestructionCostAllowsAnyRecycleSubsetAndRemainingTopOrder(int recycleCount)
    {
        var died=await Act(Visionary(),"P1",new ActivateAbilityCommand("MALZ",P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,["V"]));
        Assert.Contains("V",died.State.PlayerZones["P2"].Graveyard); // Captured controller differs from physical owner.
        Assert.Single(died.State.StackItems);Assert.Equal(2,died.State.StackItems[0].InsightContext!.Count);
        Assert.Equal("P1",died.State.StackItems[0].ControllerId);Restore(died.State);
        var opened=await ResolveTop(died.State);Restore(opened.State);
        Assert.Equal(["D1","D2"],opened.State.PendingCardChoice!.ContextObjectIds);
        Assert.DoesNotContain("\"D1\"",JsonSerializer.Serialize(opened.Prompts["P2"]));
        var selected=new[]{"D1","D2"}.Take(recycleCount).ToArray();
        var done=await Choose(opened.State,selected);
        if(recycleCount==0)
        {
            Assert.Equal("INSIGHT_ORDER",done.State.PendingCardChoice!.ChoiceWindow);Restore(done.State);
            var invalid=await new CoreRuleEngine().ResolveAsync(done.State,new("bad","P1",CommandTypes.ChooseCards),new ChooseCardsCommand(done.State.PendingCardChoice.ChoiceId,"INSIGHT_ORDER",["D1","D1"]),default);
            Assert.False(invalid.Accepted);Assert.Equal(MatchStateHasher.Hash(done.State),MatchStateHasher.Hash(invalid.State));
            done=await Choose(done.State,["D2","D1"]);
            Assert.Equal(["D2","D1","D3"],done.State.PlayerZones["P1"].MainDeck);
        }
        else if(recycleCount==1)Assert.Equal(["D2","D3","D1"],done.State.PlayerZones["P1"].MainDeck);
        else {Assert.Equal("D3",done.State.PlayerZones["P1"].MainDeck[0]);Assert.Equal(new[]{"D1","D2"},done.State.PlayerZones["P1"].MainDeck.Skip(1).Order().ToArray());}
        Assert.Null(done.State.PendingCardChoice);Assert.Empty(done.State.StackItems);Restore(done.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DamageDeathTriggersButRecallReplacementDoesNot(bool replaced)
    {
        var state=Visionary();
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["C"]=new("C",cardNo:"OGS·003/024",ownerId:"P1",controllerId:"P1"),["V"]=state.CardObjects["V"] with {Damage=3,
            UntilEndOfTurnEffects=replaced?["RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN"]:[]}},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Hand=["C"],Base=["MALZ"],Battlefields=["FIELD","V"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["FIELD"]=new("P1","BATTLEFIELD","FIELD"),["V"]=new("P1","BATTLEFIELD","FIELD")}};
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["FIELD"]=new("FIELD",cardNo:"OGN·294/298",ownerId:"P1",controllerId:"P1",tags:[P6TokenFactoryCatalog.BattlefieldCardTag])}};
        var played=await Act(state,"P1",new PlayCardCommand("C","OGS·003/024",["V"]));
        var died=await ResolveTop(played.State);
        Assert.Equal(replaced?0:1,died.State.StackItems.Count(s=>s.InsightContext is not null));
        if(replaced)Assert.Contains("V",died.State.PlayerZones["P1"].Base);
    }

    internal static MatchState Library(string card="OGN·083/298")
    {
        var state=OfficialCounterRepeatTests.Position(card);
        return state with {StackItems=card=="OGN·064/298"?state.StackItems:[],PriorityPlayerId=card=="OGN·064/298"?"P1":null,
            TimingState=card=="OGN·064/298"?TimingStates.NeutralClosed:TimingStates.NeutralOpen,
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["LIB"]=new("LIB",cardNo:"UNL-211/219",ownerId:"P2",controllerId:"P1",tags:[P6TokenFactoryCatalog.BattlefieldCardTag]),
                ["D3"]=new("D3",cardNo:"SFD·125/221",ownerId:"P1",controllerId:"P1")},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {MainDeck=["D1","D2","D3"]},["P2"]=PlayerZones.Empty with {Battlefields=["LIB"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>{["LIB"]=new("P2","BATTLEFIELD","LIB")}};
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ShortDeckInsightDoesAsMuchAsPossibleWithoutBurnout(int remaining)
    {
        var state=Visionary();
        state=state with {PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {MainDeck=state.PlayerZones["P1"].MainDeck.Take(remaining).ToArray()}}};
        var died=await Act(state,"P1",new ActivateAbilityCommand("MALZ",P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,["V"]));
        var opened=await ResolveTop(died.State);
        if(remaining==0)Assert.Null(opened.State.PendingCardChoice);
        else {Assert.Equal(1,opened.State.PendingCardChoice!.MaxCount);opened=await Choose(opened.State,[]);Assert.Null(opened.State.PendingCardChoice);}
        Assert.Equal(state.PlayerScores,opened.State.PlayerScores);
        Assert.DoesNotContain(opened.Events,e=>e.Kind.Contains("BURN") || e.Kind=="CARD_DRAWN");Restore(opened.State);
    }
    internal static MatchState Visionary()
    {
        var state=OfficialInsightAndSpellLockTests.Position("OGN·083/298");
        return state with {StackItems=[],PriorityPlayerId=null,TimingState=TimingStates.NeutralOpen,
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["MALZ"]=new("MALZ",cardNo:"OGN·113/298",ownerId:"P1",controllerId:"P1",power:3,tags:[CardObjectTags.UnitCard]),
                ["V"]=new("V",cardNo:"UNL-062/219",ownerId:"P2",controllerId:"P1",power:4,tags:[CardObjectTags.UnitCard]),
                ["D3"]=new("D3",cardNo:"SFD·125/221",ownerId:"P1",controllerId:"P1")},
            PlayerZones=new Dictionary<string,PlayerZones>{["P1"]=PlayerZones.Empty with {Base=["MALZ","V"],MainDeck=["D1","D2","D3"]},["P2"]=PlayerZones.Empty},
            ObjectLocations=new Dictionary<string,ObjectLocationState>{["MALZ"]=new("P1","BASE"),["V"]=new("P1","BASE")}};
    }
    internal static Task<ResolutionResult> Act(MatchState s,string p,GameCommand c)=>OfficialInsightAndSpellLockTests.Act(s,p,c);
    internal static Task<ResolutionResult> ResolveTop(MatchState s)=>OfficialInsightAndSpellLockTests.ResolveTop(s);
    internal static MatchState Restore(MatchState s)=>OfficialInsightAndSpellLockTests.Restore(s);
    internal static Task<ResolutionResult> Choose(MatchState s,string[] ids)=>Act(s,s.PendingCardChoice!.PlayerId,new ChooseCardsCommand(s.PendingCardChoice.ChoiceId,s.PendingCardChoice.ChoiceWindow,ids));
}
