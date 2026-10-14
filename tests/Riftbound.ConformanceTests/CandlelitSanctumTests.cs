using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CandlelitSanctumTests
{
    [Fact]
    public async Task ConquestQueuesAResponseableAbilityWithoutLookingRecyclingOrLeakingTheDeck()
    {
        var initial=Position();var driver=Driver(initial,"response-before-look");await Conquer(driver);
        Assert.Single(driver.State.StackItems);Assert.Null(driver.State.PendingCardChoice);
        Assert.NotNull(driver.State.PriorityPlayerId);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck,driver.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind is "CARDS_REVEALED" or "CARDS_RECYCLED");
        foreach(var player in new[]{"P1","P2"})
            Assert.DoesNotContain("DRAW1",JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)[player]));
        await driver.Top();var choice=Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(0,choice.RequiredCount);Assert.Equal(2,choice.MaxCount);
        Assert.Equal(["DRAW1","DRAW2"],choice.LegalObjectIds);
        Assert.DoesNotContain("DRAW1",JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        Assert.Contains("DRAW1",JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P1"]));
        await Choose(driver,[]);await Choose(driver,["DRAW2","DRAW1"]);
        Assert.Equal(["DRAW2","DRAW1","DRAW3"],driver.State.PlayerZones["P1"].MainDeck);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task TheControllerChoosesAnyRecycleSubsetThenOrdersOnlyTheRetainedCards(int recycle)
    {
        var initial=Position();var driver=Driver(initial,"recycle-"+recycle);await Conquer(driver);await driver.Top();
        await Choose(driver,new[]{"DRAW1","DRAW2"}.Take(recycle).ToArray());
        if(recycle==0)await Choose(driver,["DRAW2","DRAW1"]);
        var deck=driver.State.PlayerZones["P1"].MainDeck;
        Assert.Equal(recycle==2?"DRAW3":"DRAW2",deck[0]);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck.Order(),deck.Order());
        Assert.Equal(initial.RngCursor+(recycle==2?1:0),driver.State.RngCursor);
        Assert.Empty(driver.State.StackItems);Assert.Null(driver.State.PendingCardChoice);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind is "CARDS_REVEALED" or "CARD_DRAWN");
        var events=driver.Events().Where(e=>e.Event.Kind=="CARDS_RECYCLED").ToArray();
        if(recycle==0)Assert.Empty(events);else Assert.Equal(recycle,Assert.Single(events).Event.Payload["count"]);
        Assert.DoesNotContain("DRAW1",JsonSerializer.Serialize(driver.Events()));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARealReactionFinishesBeforeTheConquestAbilityLooksAtTheAdjustedDeck()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"MOONFALL","UNL-063/219","P1");
        var driver=Driver(initial,"reaction-adjusts-top");await Conquer(driver);
        await driver.Act("P1",new PlayCardCommand("MOONFALL","UNL-063/219",["UNIT"]));
        await driver.Top();Assert.Equal(["DRAW1"],driver.State.PendingCardChoice!.LegalObjectIds);
        await Choose(driver,["DRAW1"]);Assert.Null(driver.State.PendingCardChoice);Assert.Single(driver.State.StackItems);
        await driver.Top();Assert.Equal(["DRAW2","DRAW3"],driver.State.PendingCardChoice!.LegalObjectIds);
        await Choose(driver,[]);await Choose(driver,["DRAW3","DRAW2"]);
        Assert.Equal(["DRAW3","DRAW2","DRAW1"],driver.State.PlayerZones["P1"].MainDeck);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task NocturnesLeaveTheOriginalLookBeforeSubsetAndOrderThenConfirmInCaptureOrder(int banish)
    {
        var initial=DeckObservationPlayTests.WithNocturne(DeckObservationPlayTests.WithNocturne(Position(),"DRAW1","P1"),"DRAW2","P1");
        var driver=Driver(initial,"nocturnes-"+banish);await Conquer(driver);await driver.Top();
        foreach(var i in Enumerable.Range(0,2))await DeckObservationPlayTests.Decide(driver,i<banish);
        if(driver.State.PendingCardChoice is not null)
        {
            Assert.Equal(2-banish,driver.State.PendingCardChoice.MaxCount);await Choose(driver,[]);
            if(banish==0)await Choose(driver,["DRAW2","DRAW1"]);
        }
        foreach(var id in new[]{"DRAW1","DRAW2"}.Take(banish))
        {
            Assert.Equal(id,driver.State.PendingEffectPlay!.Sources.Keys.Single());
            await driver.Act("P1",new PlayCardCommand(id,DeckObservationPlayTests.Nocturne,[]));
            Assert.Contains(id,driver.State.PlayerZones["P1"].Base);
        }
        Assert.Null(driver.State.PendingEffectPlay);Assert.Empty(driver.State.StackItems);
        Assert.Equal(10-banish,driver.State.RunePools["P1"].Power);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARDS_REVEALED");
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task AShortDeckLooksOnlyAtExistingCardsWithoutDrawingOrBurning(int count)
    {
        var initial=Position();var all=initial.PlayerZones["P1"].MainDeck;
        initial=initial with {PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){
            ["P1"]=initial.PlayerZones["P1"] with {MainDeck=all.Take(count).ToArray(),Hand=all.Skip(count).ToArray()}}};
        var driver=Driver(initial,"short-"+count);await Conquer(driver);var scores=driver.State.PlayerScores;await driver.Top();
        if(count==1){Assert.Equal(1,driver.State.PendingCardChoice!.MaxCount);await Choose(driver,[]);}
        Assert.Null(driver.State.PendingCardChoice);Assert.Empty(driver.State.StackItems);
        Assert.Equal(scores,driver.State.PlayerScores);Assert.Equal(count,driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind is "CARD_DRAWN" or "MAIN_DECK_BURNED_OUT");
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("count")] [InlineData("battlefield")] [InlineData("source")] [InlineData("generation")]
    public async Task RecoveryRejectsForgedConquestLookCapture(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await Conquer(driver);
        var state=driver.State;var item=Assert.Single(state.StackItems);var context=item.InsightContext!;
        if(mutation=="source")item=item with {SourceObjectId="UNIT"};
        else context=mutation switch {"count"=>context with {Count=99},"battlefield"=>context with {BattlefieldObjectId="UNIT"},
            _=>context with {SourceGeneration=99}};
        var bad=state with {StackItems=[item with {InsightContext=context}]};
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(bad));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SimultaneousLegendCostIsConfirmedBeforeEitherChosenResolutionOrderCanLook(bool reverse)
    {
        var initial=OfficialLegendDeckPlayTests.Position(top:["SFD·125/221","SFD·125/221"]);
        initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){
            ["BF"]=initial.CardObjects["BF"] with {CardNo="OGN·291/298"}}};
        var driver=Driver(initial,"legend-order-"+reverse);await Conquer(driver);
        Assert.Equal(2,driver.State.TriggerQueue.Count);Assert.False(driver.State.CardObjects["LEGEND"].IsExhausted);
        var order=driver.State.TriggerQueue.Select(t=>t.TriggerId).ToArray();if(reverse)Array.Reverse(order);
        await driver.Act("P1",new OrderTriggersCommand(OrderedTriggerIds:order));
        Assert.DoesNotContain("DECK0",JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P1"]));
        await Choose(driver,["LEGEND"]);Assert.True(driver.State.CardObjects["LEGEND"].IsExhausted);
        for(var i=0;driver.State.StackItems.Count>0 && i<12;i++)
        {
            if(driver.State.PendingEffectPlay is { } play)
            {
                var id=play.Sources.Keys.Single();await driver.Act("P1",new PlayCardCommand(id,driver.State.CardObjects[id].CardNo!,[]));
            }
            else if(driver.State.PendingCardChoice is { } choice)
                await Choose(driver,choice.ChoiceWindow=="INSIGHT"?[]:choice.ChoiceWindow=="INSIGHT_ORDER"
                    ? choice.LegalObjectIds.Reverse().ToArray():[choice.LegalObjectIds.First()]);
            else await driver.Top();
        }
        Assert.Empty(driver.State.StackItems);Assert.Null(driver.State.PendingCardChoice);
        Assert.Single(driver.State.PlayerZones["P1"].Base,id=>id.StartsWith("DECK"));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("opponent")] [InlineData("stale")] [InlineData("outside")]
    [InlineData("duplicate")] [InlineData("too-many")] [InlineData("incomplete-order")]
    public async Task IllegalPrivateChoicesRejectAtomically(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await Conquer(driver);await driver.Top();
        if(mutation=="incomplete-order")await Choose(driver,[]);
        var state=driver.State;var c=state.PendingCardChoice!;
        var selection=mutation switch {"outside"=>new[]{"DRAW3"},"duplicate"=>new[]{"DRAW1","DRAW1"},
            "too-many"=>new[]{"DRAW1","DRAW2","DRAW3"},_=>new[]{"DRAW1"}};
        var command=new ChooseCardsCommand(mutation=="stale"?"EXPIRED":c.ChoiceId,c.ChoiceWindow,selection);
        var rejected=await new CoreRuleEngine().ResolveAsync(state,new("bad-private",mutation=="opponent"?"P2":"P1",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(rejected.State));
    }

    internal static MatchState Position()
    {
        var state=ConquestLifecycleRegressionTests.State("OGN·291/298",0);
        var cards=state.CardObjects.ToDictionary();cards["UNIT"]=cards["UNIT"] with {CardNo="OGN·096/298",Power=1};
        return state with {CardObjects=cards,RunePools=new Dictionary<string,RunePool>(state.RunePools){["P1"]=new(10,10)}};
    }

    internal static async Task Conquer(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[]));
        while(driver.State.FocusPlayerId is not null)await driver.Act(driver.State.FocusPlayerId,new PassFocusCommand());
    }
    internal static async Task Choose(StandbyDefendObserverTests.Driver driver,string[] cards)
    {
        var choice=Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act(choice.PlayerId,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,cards));
    }
    internal static StandbyDefendObserverTests.Driver Driver(MatchState state,string branch)
    {
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_CANDLELIT_EVIDENCE");
        return new(state,string.IsNullOrWhiteSpace(root)?null:Path.Combine(root,branch));
    }
}
