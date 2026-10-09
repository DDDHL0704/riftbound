using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;

public sealed class OfficialUnitRevivalTests
{
    [Fact]
    public async Task MatronConfirmsFirstThenReplaysAUnitWithNoRemainingPower()
    {
        var state=Position("OGN·226/298","SFD·062/221") with {RunePools=new Dictionary<string,RunePool>{{"P1",new(4,2)},{"P2",new(10,10)}}};
        var played=await Act(state,"P1",new PlayCardCommand("C","OGN·226/298",["G"]));
        Assert.Contains("C",played.State.PlayerZones["P1"].Base);Assert.Null(played.State.PendingEffectPlay);Assert.Single(played.State.StackItems);
        Assert.Equal(0,played.State.RunePools["P1"].Power);Restore(played.State);
        var opened=await Top(played.State);Assert.True(opened.State.PendingEffectPlay!.IgnoreBasePower);Restore(opened.State);
        var child=await Act(opened.State,"P1",new PlayCardCommand("G","SFD·062/221",["ALLY"]));
        Assert.Contains("G",child.State.PlayerZones["P1"].Base);Assert.True(child.State.CardObjects["ALLY"].IsExhausted);Assert.Single(child.State.StackItems);Restore(child.State);
        var done=await Top(child.State);Assert.False(done.State.CardObjects["ALLY"].IsExhausted);Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·226/298",true)]
    [InlineData("UNL-168/219",false)]
    public async Task GraveyardPlayUsesBaseCostWaiversWithoutDuplicatePlay(string source,bool optional)
    {
        var state = Position(source,"UNL-137/219") with {
            RunePools = new Dictionary<string,RunePool>{{"P1", optional ? new(4,2) : new(2,1)},{"P2",new(10,10)}}};
        var played=await Act(state,"P1",new PlayCardCommand("C",source,["G"]));
        var opened=await Top(played.State);var pending=opened.State.PendingEffectPlay!;
        Assert.Equal(optional,pending.Optional);Assert.Equal(new[]{"G"},pending.Sources.Keys);Restore(opened.State);
        var pool=opened.State.RunePools["P1"];
        Assert.Equal(0, pool.Power);
        var command=new PlayCardCommand("G","UNL-137/219",[]);
        var quote=new CoreRuleEngine().PreviewPlayCard(opened.State,"P1",PlayCostPreviewTests.Request(opened.State,command));
        Assert.True(quote.CanPay,quote.Message);Assert.Equal(1,quote.Cost!.PrintedPower);Assert.Equal(0,quote.Cost.Mana);Assert.Equal(0,quote.Cost.PowerByTrait.Values.Sum());
        var done=await Act(opened.State,"P1",command);
        Assert.Null(done.State.PendingEffectPlay);Assert.Equal(pool,done.State.RunePools["P1"]);Assert.True(done.State.CardObjects["G"].IsExhausted);Restore(done.State);
    }

    [Fact]
    public async Task LoyaltyAnimalDiscountAppearsInPromptQuoteAndPayment()
    {
        var state=Position("UNL-168/219","SFD·069/221");
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["G"]=state.CardObjects["G"] with {Tags=[CardObjectTags.UnitCard,"魄罗"]}},
            RunePools=new Dictionary<string,RunePool>{{"P1",new(0,1)},{"P2",new(10,10)}}};
        var session=new MatchSession(state,new CoreRuleEngine(),NoopMatchJournal.Instance);
        Assert.Contains(session.PromptFor("P1").Candidates!,c=>c.Action==CommandTypes.PlayCard && c.Enabled);
        var command=new PlayCardCommand("C","UNL-168/219",["G"]);
        var quote=new CoreRuleEngine().PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.True(quote.CanPay,quote.Message);Assert.Equal(0,quote.Cost!.Mana);
        var cast=await Act(state,"P1",command);Assert.Equal(0,cast.State.RunePools["P1"].Power);
        var opened=await Top(cast.State);var done=await Act(opened.State,"P1",new PlayCardCommand("G","SFD·069/221",[]));Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·208/298",true)]
    [InlineData("OGN·116/298",true)]
    [InlineData("SFD·071/221",false)]
    public async Task CruelRevivalComparesBothCostComponents(string target,bool allowed)
    {
        var state=Position("UNL-142/219",target);
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["ALLY"]=state.CardObjects["ALLY"] with {CardNo="OGN·195/298",ManaCost=10}}};
        var cmd=new PlayCardCommand("C","UNL-142/219",["G"],OptionalCosts:["DESTROY_FRIENDLY_UNIT:ALLY"]);
        var result=await new CoreRuleEngine().ResolveAsync(state,new("cruel","P1",cmd.CmdType),cmd,default);
        Assert.Equal(allowed,result.Accepted);
        if(!allowed){Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(result.State));return;}
        Assert.Contains("ALLY",result.State.PlayerZones["P1"].Graveyard);Restore(result.State);
    }

    [Fact]
    public async Task CruelRevivalKeepsChildAdditionalDestructionCost()
    {
        var state=Position("UNL-142/219","OGN·208/298");
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["ALLY"]=state.CardObjects["ALLY"] with {CardNo="OGN·195/298",ManaCost=10}}};
        var cast=await Act(state,"P1",new PlayCardCommand("C","UNL-142/219",["G"],OptionalCosts:["DESTROY_FRIENDLY_UNIT:ALLY"]));
        var opened=await Top(cast.State);
        var cmd=new PlayCardCommand("G","OGN·208/298",[]);
        var rejected=await new CoreRuleEngine().ResolveAsync(opened.State,new("missing-cost","P1",cmd.CmdType),cmd,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(opened.State),MatchStateHasher.Hash(rejected.State));
        var paid=await Act(opened.State,"P1",cmd with {OptionalCosts=["DESTROY_FRIENDLY_UNIT:SECOND"]});
        Assert.Contains("SECOND",paid.State.PlayerZones["P1"].Graveyard);Assert.Contains("G",paid.State.PlayerZones["P1"].Base);Restore(paid.State);
    }

    [Fact]
    public async Task CruelRevivalCannotSelectTheUnitPaidAsItsCost()
    {
        var state=Position("UNL-142/219");var cmd=new PlayCardCommand("C","UNL-142/219",["ALLY"],OptionalCosts:["DESTROY_FRIENDLY_UNIT:ALLY"]);
        var bad=await new CoreRuleEngine().ResolveAsync(state,new("same-card","P1",cmd.CmdType),cmd,default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(bad.State));
    }

    [Fact]
    public async Task MatronOptionalReplayCanBeDeclined()
    {
        var cast=await Act(Position("OGN·226/298"),"P1",new PlayCardCommand("C","OGN·226/298",["G"]));
        var opened=await Top(cast.State);var p=opened.State.PendingEffectPlay!;
        var done=await Act(opened.State,"P1",new ChooseCardsCommand(p.ChoiceId,"EFFECT_PLAY",[]));
        Assert.Contains("G",done.State.PlayerZones["P1"].Graveyard);Assert.Empty(done.State.StackItems);Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·048/298",true)]
    [InlineData("UNL-200/219",false)]
    public async Task DefianceChecksTargetPrintedPowerNotPlayersRemainingPower(string target,bool allowed)
    {
        var state=OfficialCounterRepeatTests.Position("OGN·045/298");
        var stack=state.StackItems[0];
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(target,out var definition));
        state=state with {StackItems=[stack with {CardNo=target,EffectKind=definition.EffectKind}],
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){[stack.SourceObjectId]=state.CardObjects[stack.SourceObjectId] with {CardNo=target}},
            RunePools=new Dictionary<string,RunePool>(state.RunePools){["P1"]=new(1,1)}};
        var cmd=new PlayCardCommand("C","OGN·045/298",[stack.StackItemId]);
        var result=await new CoreRuleEngine().ResolveAsync(state,new("counter-limit","P1",cmd.CmdType),cmd,default);
        Assert.Equal(allowed,result.Accepted);
        if(!allowed)Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("waiver")]
    [InlineData("source-zone")]
    [InlineData("destination")]
    [InlineData("actor")]
    [InlineData("optional")]
    [InlineData("generation")]
    [InlineData("outside-targets")]
    [InlineData("completed")]
    public async Task RecoveryRejectsForgedGraveyardPlayPolicy(string change)
    {
        var cast = await Act(Position("UNL-168/219"), "P1", new PlayCardCommand("C", "UNL-168/219", ["G"]));
        var opened = await Top(cast.State);
        var p = opened.State.PendingEffectPlay!;
        Restore(opened.State);
        p = change switch {
            "waiver" => p with {IgnoreBasePower = false},
            "source-zone" => p with {SourceZone = "HAND"},
            "destination" => p with {DestinationPolicy = "ANY"},
            "actor" => p with {PlayerId = "P2"},
            "optional" => p with {Optional = true},
            "generation" => p with {Sources = new Dictionary<string,long>{{"G", 100}}},
            "outside-targets" => p with {Parent = p.Parent with {TargetObjectIds = []}},
            _ => p with {Parent = p.Parent with {EffectPlayCompleted = true}}
        };
        var corrupt = opened.State with {PendingEffectPlay = p};
        Assert.Contains("invalid graveyard unit effect-play continuation",
            MatchRecoveryValidator.Validate(corrupt.RoomId, 0, [], [], new Dictionary<string,RecoveredPlayerView>(), corrupt, corrupt.Tick));
    }

    [Fact]
    public async Task MatronWithoutASelectedTargetDoesNotOpenAnEmptyComposer()
    {
        var cast = await Act(Position("OGN·226/298"), "P1", new PlayCardCommand("C", "OGN·226/298", []));
        var done = cast.State.StackItems.Count == 0 ? cast : await Top(cast.State);
        Assert.Null(done.State.PendingEffectPlay);
        Assert.Contains("C", done.State.PlayerZones["P1"].Base);
        Assert.Contains("G", done.State.PlayerZones["P1"].Graveyard);
        Restore(done.State);
    }

    internal static MatchState Position(string source,string target="OGN·096/298")
    {
        var state=OfficialSpellCompletionTests.Position(source,withSources:false);
        var unit=source=="OGN·226/298";
        return state with {
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
                ["C"]=state.CardObjects["C"] with {Tags=[unit?CardObjectTags.UnitCard:CardObjectTags.SpellCard]},
                ["G"]=new("G",cardNo:target,ownerId:"P1",controllerId:"P1",tags:target=="UNL-137/219"?[CardObjectTags.UnitCard,"魄罗"]:[CardObjectTags.UnitCard]),
                ["ALLY"]=new("ALLY",cardNo:"SFD·075/221",power:3,manaCost:4,ownerId:"P1",controllerId:"P1",isExhausted:true,tags:[CardObjectTags.UnitCard,"机械"]),
                ["SECOND"]=new("SECOND",cardNo:"SFD·075/221",power:3,manaCost:4,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard,"机械"])},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Graveyard=["G"],Base=["ALLY","SECOND"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["G"]=new("P1","GRAVEYARD"),["ALLY"]=new("P1","BASE"),["SECOND"]=new("P1","BASE")}};
    }
}
