using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialGraveyardRecastTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FizzLetsPlayerChooseSecondSpellOrDeclineAfterResponding(bool accept)
    {
        var initial = Fizz();
        var entered = await Act(initial, "P1", new PlayCardCommand("F", "SFD·140/221", []));
        Assert.Single(entered.State.StackItems); Assert.Null(entered.State.PendingEffectPlay);
        var opened = await Top(entered.State); Restore(opened.State);
        Assert.Equal(new[] { "C", "OTHER" }, opened.State.PendingEffectPlay!.Sources.Keys.Order().ToArray());
        Assert.Empty(opened.State.PlayerZones["P1"].Hand);
        var candidate = Assert.Single(opened.Prompts["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.Sources!, c => c.Id == "OTHER");
        Assert.DoesNotContain(candidate.Destinations ?? [], d => d.Label == "基地");
        if (!accept)
        {
            var pending = opened.State.PendingEffectPlay;
            var done = await Act(opened.State, "P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            Assert.Empty(done.State.StackItems); Assert.Equal(2, done.State.PlayerZones["P1"].Graveyard.Count); Restore(done.State); return;
        }
        var played = await Act(opened.State, "P1", new PlayCardCommand("OTHER", "OGN·048/298", []));
        Assert.Single(played.State.StackItems); Assert.Null(played.State.PendingEffectPlay);
        Assert.NotNull(played.State.StackItems.Single().AfterPlayRecycle);
        Assert.Equal(0, played.State.StackItems.Single().PlayCost!.PaidMana);
        Assert.Empty(played.State.PlayerZones["P1"].Hand); Restore(played.State);
        var resolved = await Top(played.State);
        Assert.Equal("OTHER", resolved.State.PlayerZones["P1"].MainDeck[^1]);
        Assert.Single(resolved.State.PlayerZones["P1"].Hand);
        Assert.Contains("C", resolved.State.PlayerZones["P1"].Graveyard); Restore(resolved.State);
    }

    [Theory]
    [InlineData("OGN·064/298")]
    [InlineData("UNL-131/219")]
    public async Task CounteredRecastStillRecyclesEvenWhenCounterReturnsToHand(string counterCard)
    {
        var opened = await OpenFizz();
        var played = await Act(opened, "P1", new PlayCardCommand("C", "OGN·048/298", []));
        var p2 = AddCounter(played.State, counterCard);
        var passed = await Act(p2, "P1", new PassPriorityCommand());
        var countered = await Act(passed.State, "P2", new PlayCardCommand("COUNTER", counterCard, [played.State.StackItems.Single().StackItemId]));
        var done = await Top(countered.State);
        if (done.State.PendingCardChoice is { } insight)
            done = await Act(done.State, insight.PlayerId, new ChooseCardsCommand(insight.ChoiceId, insight.ChoiceWindow, []));
        Assert.Contains("C", done.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("C", done.State.PlayerZones["P1"].Graveyard.Concat(done.State.PlayerZones["P1"].Hand));
        Assert.Empty(done.State.PlayerZones["P1"].Hand); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·112/298")]
    [InlineData("OGN·112a/298")]
    public async Task KaisaConquestOffersCurrentScoreThresholdAfterResponse(string card)
    {
        var conquest = await ConquestLifecycleRegressionTests.Conquer(Kaisa(card));
        Assert.Equal(4, conquest.State.PlayerScores["P1"]);
        Assert.Null(conquest.State.PendingEffectPlay); Assert.Single(conquest.State.StackItems);
        Restore(conquest.State);
        var opened = await Top(conquest.State);
        Assert.Equal(new[] { "C" }, opened.State.PendingEffectPlay!.Sources.Keys);
        Assert.Contains("卡莎", opened.Prompts["P1"].Reason);
        Restore(opened.State);
        var noCost = await Act(opened.State, "P1", new PlayCardCommand("C", "OGN·048/298", []));
        Assert.Equal(0, noCost.State.StackItems.Single().PlayCost!.PaidMana);
        var done = await Top(noCost.State);
        Assert.Equal("C", done.State.PlayerZones["P1"].MainDeck[^1]); Restore(done.State);
    }

    [Fact]
    public async Task RecastEchoPaysAdditionalManaAndOnlyRecyclesOnce()
    {
        var state = Fizz("SFD·034/221");
        var entered = await Act(state, "P1", new PlayCardCommand("F", "SFD·140/221", []));
        var opened = await Top(entered.State);
        var cast = await Act(opened.State, "P1", new PlayCardCommand("C", "SFD·034/221", ["F"], OptionalCosts: ["ECHO"]));
        Assert.Equal(2, cast.State.StackItems.Single().PlayCost!.PaidMana);
        var done = await Top(cast.State);
        Assert.Equal(7, done.State.CardObjects["F"].Power);
        Assert.Single(done.Events, e => e.Kind == "CARDS_RECYCLED");
        Assert.Equal("C", done.State.PlayerZones["P1"].MainDeck[^1]); Restore(done.State);
    }

    [Fact]
    public async Task InvalidSourceAndForgedRecoveryCannotBroadenRecastPermission()
    {
        var opened = await OpenFizz(); var pending = opened.PendingEffectPlay!;
        var invalid = await new CoreRuleEngine().ResolveAsync(opened, new("bad", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("D1", "SFD·106/221", []), default);
        Assert.False(invalid.Accepted);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened with { PendingEffectPlay = pending with { IgnoreBasePower = true } }), e => e.Contains("effect-play"));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened with { PendingEffectPlay = pending with { Sources = new Dictionary<string,long>{{"C",999}} } }), e => e.Contains("effect-play"));
    }


    [Fact]
    public async Task KaisaEchoRocketRecyclesBeforeJhinEvenAfterFourManaPaid()
    {
        var state=Kaisa();
        state=state with {PlayerScores=new Dictionary<string,int>{{"P1",4},{"P2",0}},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){
                ["P1"]=state.PlayerZones["P1"] with {LegendZone=["JHIN"]},
                ["P2"]=state.PlayerZones["P2"] with {Base=["VICTIM"]}},
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
                ["C"]=state.CardObjects["C"] with {CardNo="SFD·077/221"},
                ["JHIN"]=new("JHIN",cardNo:"UNL-181/219",ownerId:"P1",controllerId:"P1",tags:["LEGEND_CARD"]),
                ["VICTIM"]=new("VICTIM",cardNo:"SFD·001/221",ownerId:"P2",controllerId:"P2",power:10,tags:[CardObjectTags.UnitCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["JHIN"]=new("P1","LEGEND"),["VICTIM"]=new("P2","BASE")}};
        var conquest=await ConquestLifecycleRegressionTests.Conquer(state); var opened=await Top(conquest.State);
        var played=await Act(opened.State,"P1",new PlayCardCommand("C","SFD·077/221",["VICTIM"],"BASE_UNIT_DAMAGE_4",["ECHO"]));
        Assert.Equal(4,played.State.StackItems.Single().PlayCost!.PaidMana);Restore(played.State);
        var done=await Top(played.State);
        Assert.Equal(8,done.State.CardObjects["VICTIM"].Damage);
        Assert.Contains("C",done.State.PlayerZones["P1"].MainDeck);Assert.Empty(done.State.LinkedExiles);
        Assert.Null(done.State.PendingCardChoice);Assert.Empty(done.State.StackItems);Restore(done.State);
    }

    [Fact]
    public async Task PrivateInsightFinishesBeforeRecyclingAndSurvivesSourceDeath()
    {
        var entered=await Act(Fizz("UNL-063/219"),"P1",new PlayCardCommand("F","SFD·140/221",[]));
        var opened=await Top(entered.State);
        var withDamage=opened.State with {CardObjects=new Dictionary<string,CardObjectState>(opened.State.CardObjects){["F"]=opened.State.CardObjects["F"] with {Damage=1}}};
        var played=await Act(withDamage,"P1",new PlayCardCommand("C","UNL-063/219",["F"]));
        var paused=await Top(played.State);
        Assert.Equal("INSIGHT",paused.State.PendingCardChoice!.ChoiceWindow);
        Assert.DoesNotContain("C",paused.State.PlayerZones["P1"].MainDeck);Restore(paused.State);
        var choice=paused.State.PendingCardChoice;
        var done=await Act(paused.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        Assert.Contains("F",done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("C",done.State.PlayerZones["P1"].MainDeck[^1]);Restore(done.State);
    }

    [Fact]
    public async Task FreeManaDoesNotWaivePrintedPowerAndPlayerChoosesAmongMultipleTargets()
    {
        var entered=await Act(Fizz("UNL-200/219"),"P1",new PlayCardCommand("F","SFD·140/221",[]));
        var opened=await Top(entered.State);
        var state=opened.State with {RunePools=new Dictionary<string,RunePool>(opened.State.RunePools){["P1"]=RunePool.Empty}};
        var command=new PlayCardCommand("C","UNL-200/219",["F"]);
        var quote=new CoreRuleEngine().PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.False(quote.CanPay);Assert.Equal(0,quote.Cost!.Mana);Assert.True(quote.Cost.GenericPower+quote.Cost.PowerByTrait.Values.Sum()>0);
        var rejected=await new CoreRuleEngine().ResolveAsync(state,new("no-power","P1",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(rejected.State));
        state=state with {RunePools=new Dictionary<string,RunePool>(state.RunePools){["P1"]=new(0,2)}};
        var cast=await Act(state,"P1",command);var done=await Top(cast.State);
        Assert.Contains(done.Events,e=>e.Kind=="UNIT_TOKEN_CREATED");Assert.Contains("C",done.State.PlayerZones["P1"].MainDeck);Restore(done.State);
    }

    [Fact]
    public async Task RecastObeysSpellLockAndStillAllowsDeclining()
    {
        var opened=await OpenFizz();
        var state=opened with {UntilEndOfTurnEffects=[CardPermissionKeywordRules.SpellPlayProhibitionPrefix+"P1"]};
        var command=new PlayCardCommand("C","OGN·048/298",[]);
        var rejected=await new CoreRuleEngine().ResolveAsync(state,new("lock","P1",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(rejected.State));
        var done=await Act(state,"P1",new ChooseCardsCommand(state.PendingEffectPlay!.ChoiceId,"EFFECT_PLAY",[]));
        Assert.Empty(done.State.StackItems);Assert.Contains("C",done.State.PlayerZones["P1"].Graveyard);Restore(done.State);
    }

    [Fact]
    public async Task MultipleConquestTriggersRetainSeparateChoices()
    {
        var state=Kaisa();
        state=state with {PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Base=["UNIT","UNIT2","RUNE","RUNE2"]}},
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["UNIT2"]=state.CardObjects["UNIT"] with {ObjectId="UNIT2"}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["UNIT2"]=new("P1","BASE")}};
        var moved=await Act(state,"P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[],SourceObjectIds:["UNIT","UNIT2"]));
        var pass=await Act(moved.State,"P1",new PassFocusCommand());var conquest=await Act(pass.State,"P2",new PassFocusCommand());
        Assert.Equal(2,conquest.State.TriggerQueue.Count);Restore(conquest.State);
        var ordered=await Act(conquest.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:conquest.State.TriggerQueue.Select(t=>t.TriggerId).Reverse().ToArray()));
        for(var i=0;i<2;i++)
        {
            var opened=await Top(ordered.State);Restore(opened.State);
            ordered=await Act(opened.State,"P1",new ChooseCardsCommand(opened.State.PendingEffectPlay!.ChoiceId,"EFFECT_PLAY",[]));Restore(ordered.State);
        }
        Assert.Empty(ordered.State.StackItems);Assert.Equal(2,ordered.State.PlayerZones["P1"].Graveyard.Count);
    }

    [Theory]
    [InlineData("UNL-147/219")]
    [InlineData("UNL-059/219")]
    public async Task RecastCannotTargetProtectedEnemy(string protectedCard)
    {
        var entered=await Act(Fizz("UNL-200/219"),"P1",new PlayCardCommand("F","SFD·140/221",[]));
        var opened=await Top(entered.State);
        var state=opened.State with {
            PlayerExperience=new Dictionary<string,int>{{"P1",0},{"P2",20}},
            PlayerZones=new Dictionary<string,PlayerZones>(opened.State.PlayerZones){["P2"]=opened.State.PlayerZones["P2"] with {Base=["PROTECTED"]}},
            CardObjects=new Dictionary<string,CardObjectState>(opened.State.CardObjects){["PROTECTED"]=new("PROTECTED",cardNo:protectedCard,ownerId:"P2",controllerId:"P2",power:10,tags:[CardObjectTags.UnitCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(opened.State.ObjectLocations){["PROTECTED"]=new("P2","BASE")}};
        var command=new PlayCardCommand("C","UNL-200/219",["PROTECTED"]);
        var rejected=await new CoreRuleEngine().ResolveAsync(state,new("protected","P1",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(rejected.State));
        var quote=new CoreRuleEngine().PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));Assert.False(quote.IsValid);
        Restore(state);
    }

    [Fact]
    public async Task NoEligibleSpellsDoNotOpenAnEmptyComposer()
    {
        var state=Fizz("OGN·114/298");
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["OTHER"]=state.CardObjects["OTHER"] with {CardNo="OGN·083/298"}}};
        var entered=await Act(state,"P1",new PlayCardCommand("F","SFD·140/221",[]));var done=await Top(entered.State);
        Assert.Null(done.State.PendingEffectPlay);Assert.Empty(done.State.StackItems);
        Assert.Equal(2,done.State.PlayerZones["P1"].Graveyard.Count);Restore(done.State);
    }

    [Fact]
    public async Task SuspendedRecastParentCannotForgeItsEffectKindGenerationOrActor()
    {
        var conquered=await ConquestLifecycleRegressionTests.Conquer(Kaisa());var opened=await Top(conquered.State);
        var pending=opened.State.PendingEffectPlay!;
        foreach(var invalid in new[]{
            pending with {Parent=pending.Parent with {EffectKind="SPELL_TRIGGER"}},
            pending with {Parent=pending.Parent with {RecastContext=pending.Parent.RecastContext! with {SourceGeneration=-1}}},
            pending with {PlayerId="UNKNOWN",Parent=pending.Parent with {ControllerId="UNKNOWN"}}})
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with {PendingEffectPlay=invalid}),e=>e.Contains("effect-play"));
        Restore(opened.State);
    }

    internal static MatchState Fizz(string spell = "OGN·048/298")
    {
        var state = OfficialSpellCompletionTests.Position(spell, withSources: false);
        return state with {
            PlayerZones = new Dictionary<string,PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand=["F"], Graveyard=["C","OTHER"] } },
            CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) {
                ["C"] = state.CardObjects["C"] with {ManaCost=0, Tags=[CardObjectTags.SpellCard]},
                ["F"] = new("F",cardNo:"SFD·140/221",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]),
                ["OTHER"] = new("OTHER",cardNo:"OGN·048/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard]) },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(state.ObjectLocations) {
                ["F"] = new("P1","HAND"), ["C"] = new("P1","GRAVEYARD"), ["OTHER"] = new("P1","GRAVEYARD") } };
    }
    internal static MatchState Kaisa(string card = "OGN·112/298")
    {
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298", 0);
        return state with { PlayerScores=new Dictionary<string,int>{{"P1",3},{"P2",0}},
            RunePools=new Dictionary<string,RunePool>{{"P1",new(20,20)},{"P2",new(20,20)}},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Graveyard=["C","EXPENSIVE"]}},
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["UNIT"]=state.CardObjects["UNIT"] with {CardNo=card},
                ["C"]=new("C",cardNo:"OGN·048/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard]),
                ["EXPENSIVE"]=new("EXPENSIVE",cardNo:"OGN·083/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["UNIT"]=new("P1","BASE"),["BF"]=new("P2","BATTLEFIELD","BF"),
                ["C"]=new("P1","GRAVEYARD"),["EXPENSIVE"]=new("P1","GRAVEYARD")} };
    }
    internal static MatchState AddCounter(MatchState state,string card) => state with {
        PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with {Hand=["COUNTER"]}},
        CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["COUNTER"]=new("COUNTER",cardNo:card,ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.SpellCard])},
        ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["COUNTER"]=new("P2","HAND")} };
    internal static async Task<MatchState> OpenFizz()
    { var entered=await Act(Fizz(),"P1",new PlayCardCommand("F","SFD·140/221",[]));return (await Top(entered.State)).State; }
    internal static Task<ResolutionResult> Act(MatchState state,string player,GameCommand command)=>OfficialSpellCompletionTests.Act(state,player,command);
    internal static Task<ResolutionResult> Top(MatchState state)=>OfficialSpellCompletionTests.Top(state);
    internal static void Restore(MatchState state)=>OfficialSpellCompletionTests.Restore(state);
}
