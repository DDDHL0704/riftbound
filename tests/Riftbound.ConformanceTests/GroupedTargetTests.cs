using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.CurrentPowerTargetTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class GroupedTargetTests
{
    internal const string Spell="OGN·256/298";
    internal static MatchState Group(int count=4,int power=1)
    {
        var s=Position(Spell,power);
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D"]=s.CardObjects["D"] with {Tags=[CardObjectTags.UnitCard]}}};
        for(var i=2;i<=count;i++) {
            var id="D"+i;s=At(Source(s,"UNL-008/219",id),id,"P2","BATTLEFIELD","BF");
            s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){[id]=s.CardObjects[id] with {Power=power,Damage=0}}};
        }
        return s;
    }
    internal static string[] Ids(int count)=>Enumerable.Range(1,count).Select(i=>i==1?"D":"D"+i).ToArray();
    internal static MatchState OtherField(MatchState s,params string[] ids)
    {
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["BF2"]=s.CardObjects["BF"] with {ObjectId="BF2"}}};
        s=At(s,"BF2","P2","BATTLEFIELD","BF2");foreach(var id in ids)s=At(s,id,"P2","BATTLEFIELD","BF2");return s;
    }
    internal static async Task<ResolutionResult> ChangedGroup(string kind="power")
    {
        var cast=await Act(Group(),"P1",new PlayCardCommand("AOE",Spell,Ids(4)));var s=cast.State;
        if(kind=="power")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["D3"]=s.CardObjects["D3"] with {Power=2},["D4"]=s.CardObjects["D4"] with {Power=2}}};
        if(kind=="split")s=OtherField(s,"D3","D4");
        if(kind=="all-move")s=OtherField(s,Ids(4));
        return await Top(s);
    }
    internal static Task<ResolutionResult> Select(MatchState s,string option)=>Act(s,s.PendingRuleChoice!.Request.PlayerId,
        new PayCostCommand(s.PendingRuleChoice.Request.Id,"RULE_REPLACEMENT",[option]));

    [Theory]
    [InlineData(0)] [InlineData(5)] [InlineData(12)]
    public async Task AnyNumberIncludesZeroAndMoreThanFourZeroPowerUnits(int count)
    {
        var s=Group(Math.Max(1,count),0);Assert.True(Quote(s,Ids(count)).IsValid);
        var done=await OfficialSettReplacementTests.Open(s,Spell,Ids(count));Assert.Null(done.State.PendingRuleChoice);
        foreach(var id in Ids(count))Assert.Contains(id,done.State.PlayerZones["P2"].Graveyard);Restore(done.State);
    }
    [Fact]
    public async Task InitialTargetsMustShareAnExactBattlefield()
    {
        var s=OtherField(Group(2),"D2");var hash=MatchStateHasher.Hash(s);Assert.False(Quote(s,"D","D2").IsValid);
        var bad=await new CoreRuleEngine().ResolveAsync(s,new("bad","P1",CommandTypes.PlayCard),new PlayCardCommand("AOE",Spell,["D","D2"]),default);
        Assert.False(bad.Accepted);Assert.Equal(hash,MatchStateHasher.Hash(bad.State));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IncreasedTotalLetsCasterChooseEitherOfficialExampleSubset(bool twoLarge)
    {
        var opened=await ChangedGroup();Assert.NotNull(opened.State.PendingRuleChoice);Assert.Equal("P1",opened.State.PendingRuleChoice!.Request.PlayerId);
        Assert.Empty(opened.State.PlayerZones["P2"].Graveyard);Restore(opened.State);
        var s=(await Select(opened.State,twoLarge?"REMOVE:D":"REMOVE:D4")).State;
        if(twoLarge)s=(await Select(s,"REMOVE:D2")).State;
        var done=await Select(s,"CONFIRM");Assert.Null(done.State.PendingRuleChoice);
        var expected=twoLarge?new[]{"D3","D4"}:new[]{"D","D2","D3"};
        Assert.Equal(expected.Order(),done.State.PlayerZones["P2"].Graveyard.Order());Restore(done.State);
    }
    [Fact]
    public async Task SplitGroupAllowsChoosingTheOtherBattlefield()
    {
        var opened=await ChangedGroup("split");Assert.NotNull(opened.State.PendingRuleChoice);
        var one=await Select(opened.State,"REMOVE:D");var two=await Select(one.State,"REMOVE:D2");var done=await Select(two.State,"CONFIRM");
        Assert.Equal(new[]{"D3","D4"},done.State.PlayerZones["P2"].Graveyard.Order());Restore(done.State);
    }
    [Fact]
    public async Task AllTargetsMovingTogetherRemainLegalWithoutReselection()
    {
        var done=await ChangedGroup("all-move");Assert.Null(done.State.PendingRuleChoice);Assert.Equal(Ids(4),done.State.PlayerZones["P2"].Graveyard.Order());Restore(done.State);
    }
    [Fact]
    public async Task OpponentForgedAdditionAndPrematureConfirmationAreRejected()
    {
        var opened=await ChangedGroup();var s=opened.State;Assert.NotNull(s.PendingRuleChoice);var hash=MatchStateHasher.Hash(s);
        foreach(var pair in new[]{("P2","REMOVE:D"),("P1","ADD:A"),("P1","CONFIRM")}){
            var bad=await new CoreRuleEngine().ResolveAsync(s,new(pair.Item1+pair.Item2,pair.Item1,CommandTypes.PayCost),new PayCostCommand(s.PendingRuleChoice!.Request.Id,"RULE_REPLACEMENT",[pair.Item2]),default);
            Assert.False(bad.Accepted);Assert.Equal(hash,MatchStateHasher.Hash(bad.State));
        }
    }
    [Fact]
    public async Task CasterMayChooseEmptySubsetAndEffectDoesNotRetarget()
    {
        var s=(await ChangedGroup()).State;Assert.NotNull(s.PendingRuleChoice);
        foreach(var id in Ids(4))s=(await Select(s,"REMOVE:"+id)).State;
        var done=await Select(s,"CONFIRM");Assert.Empty(done.State.PlayerZones["P2"].Graveyard);Assert.Empty(done.State.StackItems);Restore(done.State);
    }
    [Fact]
    public async Task RealPowerBindResponseOpensSubsetChoiceBeforeAnyDestruction()
    {
        var s=Group();s=s with {
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["BUFF"]=new("BUFF",cardNo:"SFD·151/221",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.SpellCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {Hand=[..s.PlayerZones["P2"].Hand,"BUFF"]}},
            RunePools=new Dictionary<string,RunePool>(s.RunePools){["P2"]=new(20,20)}};
        var cast=await Act(s,"P1",new PlayCardCommand("AOE",Spell,Ids(4)));
        var passed=await Act(cast.State,"P1",new PassPriorityCommand());
        var response=await Act(passed.State,"P2",new PlayCardCommand("BUFF","SFD·151/221",["D3","D4"]));
        var buffed=await Top(response.State);Assert.Equal(2,buffed.State.CardObjects["D3"].Power);
        var opened=await Top(buffed.State);Assert.NotNull(opened.State.PendingRuleChoice);Restore(opened.State);
        Assert.All(Ids(4),id=>Assert.Contains(id,opened.State.PlayerZones["P2"].Battlefields));
        var removed=await Select(opened.State,"REMOVE:D4");var done=await Select(removed.State,"CONFIRM");
        Assert.All(new[]{"D","D2","D3"},id=>Assert.Contains(id,done.State.PlayerZones["P2"].Graveyard));Assert.Contains("D4",done.State.PlayerZones["P2"].Battlefields);Restore(done.State);
    }

}
