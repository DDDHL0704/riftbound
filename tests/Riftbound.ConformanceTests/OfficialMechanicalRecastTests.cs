using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialMechanicalRecastTests
{
    [Theory]
    [InlineData("SFD·026/221")]
    [InlineData("SFD·026a/221")]
    public async Task ConquestWaitsForResponseAndChosenUnitThenFormalPlay(string card)
    {
        var conquest = await ConquestLifecycleRegressionTests.Conquer(Position(card));
        Assert.Null(conquest.State.PendingCardChoice); Assert.Single(conquest.State.StackItems); Restore(conquest.State);
        var opened = await Top(conquest.State); var choice = opened.State.PendingCardChoice!;
        Assert.Equal("RECYCLE_FOR_EFFECT_PLAY", choice.ChoiceWindow);
        Assert.Equal(new[] { "A", "B" }, choice.LegalObjectIds); Restore(opened.State);
        var paid = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["B"]));
        Assert.Contains("A", paid.State.PlayerZones["P1"].Base);
        Assert.Contains("B", paid.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(paid.Events, e => e.Kind.Contains("DESTROYED"));
        Assert.Equal(3, paid.State.PendingEffectPlay!.ManaReduction);
        Assert.False(paid.State.PendingEffectPlay.IgnoreBasePower); Restore(paid.State);
        var play = new PlayCardCommand("MECH2", "SFD·075/221", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(paid.State, "P1", PlayCostPreviewTests.Request(paid.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait.Values.Sum());
        var done = await Act(paid.State, "P1", play);
        Assert.Null(done.State.PendingEffectPlay); Assert.Contains("MECH2", done.State.PlayerZones["P1"].Base);
        Assert.Contains("MECH", done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(9, done.State.RunePools["P1"].Mana); Assert.Equal(9, done.State.RunePools["P1"].Power);
        Assert.True(done.State.CardObjects["MECH2"].IsExhausted); Restore(done.State);
    }

    [Fact]
    public async Task DeclineBeforeRecyclingKeepsAllCardsAndResources()
    {
        var opened = await Open(); var choice = opened.State.PendingCardChoice!;
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Contains("B", done.State.PlayerZones["P1"].Base); Assert.Empty(done.State.StackItems);
        Assert.Null(done.State.PendingEffectPlay); Assert.DoesNotContain(done.Events, e => e.Kind == "CARDS_RECYCLED"); Restore(done.State);
    }

    [Fact]
    public async Task RobotTokenPaysRecyclingCostAndItsPowerSurvivesDisappearance()
    {
        var position = Position();
        position = position with { CardObjects = new Dictionary<string,CardObjectState>(position.CardObjects) {
            ["B"] = position.CardObjects["B"] with { CardNo="SFD·T01", Tags=[CardObjectTags.UnitCard,"机械"], Power=3 } } };
        var opened = await Open(position); var choice = opened.State.PendingCardChoice!;
        var paid = await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        Assert.False(paid.State.CardObjects.ContainsKey("B")); Assert.DoesNotContain("B",paid.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(3,paid.State.PendingEffectPlay!.ManaReduction); Restore(paid.State);
        var done = await Act(paid.State,"P1",new PlayCardCommand("MECH","SFD·075/221",[]));
        Assert.Contains("MECH",done.State.PlayerZones["P1"].Base); Restore(done.State);
    }

    [Fact]
    public async Task InsufficientPowerRejectsWithoutRefundingAlreadyPaidRecycleCost()
    {
        var opened=await Open(); var choice=opened.State.PendingCardChoice!;
        var paid=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        var state=paid.State with {RunePools=new Dictionary<string,RunePool>(paid.State.RunePools){["P1"]=new(10,0)}};
        var play=new PlayCardCommand("MECH","SFD·075/221",[]);
        var rejected=await new CoreRuleEngine().ResolveAsync(state,new("no-power","P1",play.CmdType),play,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(rejected.State));
        Assert.Contains("B",rejected.State.PlayerZones["P1"].MainDeck);Restore(rejected.State);
    }

    [Fact]
    public async Task EnemySelfAndNonMechanicalSourcesAreRejected()
    {
        var opened=await Open();var choice=opened.State.PendingCardChoice!;
        foreach(var id in new[]{"UNIT","ENEMY"})
        {
            var cmd=new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[id]);
            var rejected=await new CoreRuleEngine().ResolveAsync(opened.State,new("bad-choice","P1",cmd.CmdType),cmd,default);
            Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(rejected.State));
        }
        var paid=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        var command=new PlayCardCommand("C","OGN·048/298",[]);
        var invalid=await new CoreRuleEngine().ResolveAsync(paid.State,new("bad-source","P1",command.CmdType),command,default);
        Assert.False(invalid.Accepted);Assert.Equal(MatchStateHasher.Hash(paid.State),MatchStateHasher.Hash(invalid.State));
    }

    [Fact]
    public async Task StolenUnitRecyclesToOwnerDeckAndNoEligibleMechFinishes()
    {
        var position=Position();
        position=position with {CardObjects=new Dictionary<string,CardObjectState>(position.CardObjects){
            ["B"]=position.CardObjects["B"] with {OwnerId="P2"},
            ["MECH"]=position.CardObjects["MECH"] with {Tags=[CardObjectTags.UnitCard]},
            ["MECH2"]=position.CardObjects["MECH2"] with {Tags=[CardObjectTags.UnitCard]}}};
        var opened=await Open(position);var choice=opened.State.PendingCardChoice!;
        var done=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        Assert.Contains("B",done.State.PlayerZones["P2"].MainDeck);Assert.Null(done.State.PendingEffectPlay);Assert.Empty(done.State.StackItems);Restore(done.State);
    }

    [Fact]
    public async Task RecoveryCannotForgeChoiceOrFreePlayCostPolicy()
    {
        var opened=await Open();var choice=opened.State.PendingCardChoice!;
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with {PendingCardChoice=choice with {LegalObjectIds=["UNIT"]}}),e=>e.Contains("recycling"));
        var paid=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        var pending=paid.State.PendingEffectPlay!;
        foreach(var invalid in new[]{pending with {IgnoreBasePower=true},pending with {ManaReduction=999},
            pending with {Parent=pending.Parent with {RecycledUnit=null},DestinationPolicy="STACK",IgnoreBaseMana=true,Optional=true,ManaReduction=0}})
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with {PendingEffectPlay=invalid}),e=>e.Contains("effect-play"));
        Restore(paid.State);
    }

    internal static async Task<ResolutionResult> Open(MatchState? state=null)
        => await Top((await ConquestLifecycleRegressionTests.Conquer(state??Position())).State);

    [Fact]
    public async Task ReplayedUnitUsesChosenPlayTargetAndRespondablePlayAbility()
    {
        var state=Position();
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
            ["UNIT"]=state.CardObjects["UNIT"] with {Tags=[CardObjectTags.UnitCard,"机械"]},
            ["MECH"]=state.CardObjects["MECH"] with {CardNo="SFD·062/221"}}};
        var opened=await Open(state);var choice=opened.State.PendingCardChoice!;
        var paid=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        Assert.True(paid.State.CardObjects["UNIT"].IsExhausted);
        var played=await Act(paid.State,"P1",new PlayCardCommand("MECH","SFD·062/221",["UNIT"]));
        Assert.Contains("MECH",played.State.PlayerZones["P1"].Base);
        Assert.True(played.State.CardObjects["UNIT"].IsExhausted);Assert.Single(played.State.StackItems);Restore(played.State);
        var done=await Top(played.State);Assert.False(done.State.CardObjects["UNIT"].IsExhausted);Restore(done.State);
    }

    [Fact]
    public async Task CapturedConquestSurvivesSourceLeavingBeforeResolution()
    {
        var conquered=await ConquestLifecycleRegressionTests.Conquer(Position());
        var state=conquered.State;
        state=state with {
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {
                Battlefields=state.PlayerZones["P1"].Battlefields.Where(id=>id!="UNIT").ToArray(),
                Graveyard=state.PlayerZones["P1"].Graveyard.Append("UNIT").ToArray()}},
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["UNIT"]=state.CardObjects["UNIT"] with {ObjectGeneration=1}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["UNIT"]=new("P1","GRAVEYARD")}};
        var opened=await Top(state);var choice=opened.State.PendingCardChoice!;
        Assert.Contains("B",choice.LegalObjectIds);Restore(opened.State);
        var paid=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        Assert.Equal(3,paid.State.PendingEffectPlay!.ManaReduction);Restore(paid.State);
    }

    [Fact]
    public async Task RecyclingDiscountIncludesContinuousPowerBeforeLeavingPlay()
    {
        var state=Position();
        state=state with {
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
                ["B"]=state.CardObjects["B"] with {CardNo="SFD·075/221",Tags=[CardObjectTags.UnitCard,"机械"]},
                ["AURA"]=new("AURA",cardNo:"SFD·089/221",ownerId:"P1",controllerId:"P1",power:5,tags:[CardObjectTags.UnitCard,"机械"])},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Base=state.PlayerZones["P1"].Base.Append("AURA").ToArray()}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["AURA"]=new("P1","BASE")}};
        var opened=await Open(state);var choice=opened.State.PendingCardChoice!;
        var paid=await Act(opened.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["B"]));
        Assert.Equal(4,paid.State.PendingEffectPlay!.ManaReduction);Restore(paid.State);
        Assert.Equal(3,paid.State.CardObjects["B"].Power);
    }

    internal static MatchState Position(string card="SFD·026/221")
    {
        var state=Kaisa(card);
        var cards=new Dictionary<string,CardObjectState>(state.CardObjects){
            ["A"]=new("A",cardNo:"SFD·125/221",ownerId:"P1",controllerId:"P1",power:1,tags:[CardObjectTags.UnitCard]),
            ["B"]=new("B",cardNo:"SFD·125/221",ownerId:"P1",controllerId:"P1",power:3,tags:[CardObjectTags.UnitCard]),
            ["MECH"]=new("MECH",cardNo:"SFD·075/221",ownerId:"P1",controllerId:"P1",power:3,tags:[CardObjectTags.UnitCard,"机械"]),
            ["MECH2"]=new("MECH2",cardNo:"SFD·075/221",ownerId:"P1",controllerId:"P1",power:3,tags:[CardObjectTags.UnitCard,"机械"]),
            ["ENEMY"]=new("ENEMY",cardNo:"SFD·125/221",ownerId:"P2",controllerId:"P2",power:3,tags:[CardObjectTags.UnitCard])};
        var zones=new Dictionary<string,PlayerZones>(state.PlayerZones){
            ["P1"]=state.PlayerZones["P1"] with {Base=["UNIT","A","B"],Graveyard=["C","EXPENSIVE","MECH","MECH2"]},
            ["P2"]=state.PlayerZones["P2"] with {Base=["ENEMY"]}};
        var locations=zones.SelectMany(p=>new[]{("BASE",p.Value.Base),("GRAVEYARD",p.Value.Graveyard)}.SelectMany(z=>z.Item2.Select(id=>(id,location:new ObjectLocationState(p.Key,z.Item1))))).ToDictionary(x=>x.id,x=>x.location);
        return state with {CardObjects=cards,PlayerZones=zones,RunePools=new Dictionary<string,RunePool>{{"P1",new(10,10)},{"P2",new(10,10)}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations.Concat(locations).GroupBy(x=>x.Key).ToDictionary(g=>g.Key,g=>g.Last().Value))};
    }
}
