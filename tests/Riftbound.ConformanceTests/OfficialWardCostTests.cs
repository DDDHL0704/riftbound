using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;
public sealed class OfficialWardCostTests
{
    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(3, 0, false)]
    public async Task WardCostsPowerAndPreviewMatchesCommit(int mana, int power, bool accepted)
    {
        var state = State(new(mana, power)); var command = new PlayCardCommand("CARD", "OGS·003/024", ["T"]);
        var engine = new CoreRuleEngine(); var quote = engine.PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.Equal(2,quote.Cost!.Mana); Assert.Equal(1,quote.Cost.GenericPower);
        Assert.Equal(accepted,quote.CanPay);
        var result = await engine.ResolveAsync(state,new("ward","P1",command.CmdType),command,default);
        Assert.Equal(accepted,result.Accepted);
        if(!accepted) Assert.Equal(MatchStateHasher.Hash(state),MatchStateHasher.Hash(result.State));
        else { Assert.Equal(mana-2,result.State.RunePools["P1"].Mana); Assert.Equal(power-1,result.State.RunePools["P1"].TotalPower); }
    }
    [Fact]
    public void ManaReductionCannotReduceWardPower()
    {
        var state=State(new(0,0)) with { UntilEndOfTurnEffects=["RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:DRAKE"] };
        var command=new PlayCardCommand("CARD","OGS·003/024",["T"]);
        var quote=new CoreRuleEngine().PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.False(quote.CanPay); Assert.Equal(0,quote.Cost!.Mana); Assert.Equal(1,quote.Cost.MissingPower);
    }
    [Theory]
    [InlineData("red")]
    [InlineData("blue")]
    public async Task AnyTraitCanPayWard(string trait)
    {
        var state=State(new(2,0,new Dictionary<string,int>{{trait,1}})); var command=new PlayCardCommand("CARD","OGS·003/024",["T"]);
        var result=await new CoreRuleEngine().ResolveAsync(state,new("trait","P1",command.CmdType),command,default);
        Assert.True(result.Accepted,result.ErrorMessage); Assert.Equal(0,result.State.RunePools["P1"].TotalPower);
    }
    [Theory]
    [InlineData("active", 3)]
    [InlineData("hidden", 2)]
    [InlineData("other-field", 2)]
    [InlineData("borrowed", 3)]
    public async Task EachIndependentWardSourceAddsAndScopeStillMatters(string mode, int tax)
    {
        var state=State(new(2,tax));
        state=state with { PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones) { ["P2"]=state.PlayerZones["P2"] with {Battlefields=["BF","T","G1","G2"]} },
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects) {
                ["G1"]=new("G1",cardNo:"UNL-041/219",ownerId:"P2",controllerId:"P2",power:3,tags:[CardObjectTags.UnitCard]),
                ["G2"]=new("G2",cardNo:"UNL-041/219",ownerId:mode=="borrowed"?"P1":"P2",controllerId:"P2",isFaceDown:mode=="hidden",power:3,tags:[CardObjectTags.UnitCard]) },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations) {
                ["G1"]=new("P2","BATTLEFIELD","BF"), ["G2"]=new("P2","BATTLEFIELD",mode=="other-field"?"OTHER":"BF") } };
        var command=new PlayCardCommand("CARD","OGS·003/024",["T"]);
        var quote=new CoreRuleEngine().PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.True(quote.CanPay,quote.Message); Assert.Equal(tax,quote.Cost!.GenericPower);
        var result=await new CoreRuleEngine().ResolveAsync(state,new("aura","P1",command.CmdType),command,default);
        Assert.True(result.Accepted,result.ErrorMessage); Assert.Equal(0,result.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task InlineRuneRecyclingFundsWardAndPreviewDoesNotSpendIt()
    {
        var state=State(new(2,0));
        state=state with {PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Base=["R"]}},
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["R"]=new("R",cardNo:"UNL-R03",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.RuneCard,"COLOR:blue"])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["R"]=new("P1","BASE")}};
        var before=MatchStateHasher.Hash(state);
        var command=new PlayCardCommand("CARD","OGS·003/024",["T"],OptionalCosts:["RECYCLE_RUNE:R"]);
        var engine=new CoreRuleEngine(); var quote=engine.PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.True(quote.CanPay,quote.Message); Assert.Equal(before,MatchStateHasher.Hash(state));
        Assert.Contains(ResolutionResult.BuildPrompts(state)["P1"].Candidates!,c=>c.Action==CommandTypes.PlayCard && c.Enabled);
        var result=await engine.ResolveAsync(state,new("inline","P1",command.CmdType),command,default);
        Assert.True(result.Accepted,result.ErrorMessage); Assert.Contains("R",result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0,result.State.RunePools["P1"].TotalPower); OfficialInsightAndSpellLockTests.Restore(result.State);
    }

    [Fact]
    public async Task RepeatedTargetPaysWardForEachSelection()
    {
        var state=State(new(20,20));
        state=state with { PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P2"]=state.PlayerZones["P2"] with {Base=["T"],Battlefields=["BF"]}},
            CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["CARD"]=state.CardObjects["CARD"] with {CardNo="SFD·077/221"}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["T"]=new("P2","BASE")} };
        var command=new PlayCardCommand("CARD","SFD·077/221",["T"],"BASE_UNIT_DAMAGE_4",["ECHO"]);
        var engine=new CoreRuleEngine();var quote=engine.PreviewPlayCard(state,"P1",PlayCostPreviewTests.Request(state,command));
        Assert.True(quote.CanPay,quote.Message);Assert.Equal(8,quote.Cost!.Mana);Assert.Equal(2,quote.Cost.GenericPower);
        var result=await engine.ResolveAsync(state,new("echo","P1",command.CmdType),command,default);Assert.True(result.Accepted,result.ErrorMessage);
        Assert.Equal(2,Assert.Single(result.Events,e=>e.Kind=="COST_PAID").Payload["spellshieldTaxPower"]);
    }

    internal static MatchState State(RunePool pool)
    {
        var s=PlayCostPreviewTests.Position(pool,"OGS·003/024");
        return s with { PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"]=PlayerZones.Empty with {Battlefields=["BF","T"]} },
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { ["BF"]=new("BF",cardNo:"OGN·282/298",ownerId:"P2",controllerId:"P2",tags:[P6TokenFactoryCatalog.BattlefieldCardTag]), ["T"]=new("T",cardNo:"UNL-104/219",ownerId:"P2",controllerId:"P2",power:5,tags:[CardObjectTags.UnitCard,CardObjectTags.Spellshield]) },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["T"]=new("P2","BATTLEFIELD","BF"),["BF"]=new("P2","BATTLEFIELD","BF")} };
    }
}
