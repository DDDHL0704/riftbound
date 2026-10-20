using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

// Explicit semantic fixture; ordinary opening, SQL and product acceptance remain separate.
public sealed class SpellRemakeSingleTargetCompletionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoNewControllerFriendlyUnitSkipsAnImpossibleCompleteRemake(bool mirror)
    {
        var s=await Control(mirror,false);
        Assert.Null(s.PendingRuleChoice);
        var spell=Assert.Single(s.StackItems);
        Assert.Equal("P2",spell.ControllerId);
        Assert.Equal(new[]{"ONE"},spell.TargetObjectIds);
        Assert.Contains("CONTROL",s.PlayerZones["P2"].Graveyard);
        var hash=MatchStateHasher.Hash(s);
        var forged=new PayCostCommand("no-complete-remake","RULE_REPLACEMENT",["CONFIRM"]);
        var rejected=await new CoreRuleEngine().ResolveAsync(s,new("forged-no-complete","P2",forged.CmdType),forged,default);
        Assert.False(rejected.Accepted);Assert.Empty(rejected.Events);
        Assert.Equal(hash,MatchStateHasher.Hash(rejected.State));
        var pool=s.RunePools["P2"];
        var done=(await Top(s)).State;
        Assert.Empty(done.StackItems);Assert.Equal(9,done.CardObjects["ONE"].Power);
        Assert.Equal(pool,done.RunePools["P2"]);
        Assert.Contains("SPELL",done.PlayerZones["P1"].Graveyard);
        Restore(done);ArmamentMightTests.PublicRecovery(done);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAvailableNewControllerFriendlyUnitPreservesTheRemakeWindow(bool mirror)
    {
        var s=await Control(mirror,true);
        var request=Assert.IsType<RuleChoiceContinuation>(s.PendingRuleChoice).Request;
        Assert.Equal("P2",request.PlayerId);Assert.Equal("SPELL_CHOICE_REMAKE",request.Kind);
        Assert.DoesNotContain(request.Options,o=>o.Id=="CONFIRM");
        s=await RebuttalRulesTests.Choose(s,RebuttalRulesTests.Option(s,"重选目标 1 为","（TWO）"));
        s=await RebuttalRulesTests.Choose(s,"CONFIRM");
        Assert.Equal(new[]{"TWO"},Assert.Single(s.StackItems).TargetObjectIds);
        Assert.Equal("P2",Assert.Single(s.StackItems).ControllerId);
        Restore(s);ArmamentMightTests.PublicRecovery(s);
    }

    public static IEnumerable<object[]> FriendlySingleSlotConsumers()
    {
        foreach (var (card, mana) in new (string, int)[] {
            ("OGN·046/298",1), ("OGN·053/298",3), ("OGN·069/298",3), ("OGN·102/298",3),
            ("OGN·104/298",1), ("UNL-095/219",2), ("UNL-165/219",2), ("UNL-184/219",2) })
            foreach (var mirror in new[] { false, true }) yield return [card,mana,mirror];
    }

    [Theory]
    [MemberData(nameof(FriendlySingleSlotConsumers))]
    public async Task NoInPlayFriendlyTargetFinishesControlForEachApplicableConsumer(string card,int mana,bool mirror)
    {
        var s=await Control(mirror,false,card,mana,decoys:true);
        Assert.Null(s.PendingRuleChoice);
        var spell=Assert.Single(s.StackItems);
        Assert.Equal("P2",spell.ControllerId);Assert.Equal(card,spell.CardNo);
        Assert.Equal(new[]{"ONE"},spell.TargetObjectIds);
        Assert.Contains("CONTROL",s.PlayerZones["P2"].Graveyard);
        Assert.Contains("HAND-UNIT",s.PlayerZones["P2"].Hand);
        Assert.Contains("GRAVE-UNIT",s.PlayerZones["P2"].Graveyard);
        Assert.Equal(mirror?29:26,s.RunePools["P2"].Mana);
        Assert.Equal(mirror?28:27,s.RunePools["P2"].TotalPower);
        Restore(s);ArmamentMightTests.PublicRecovery(s);
    }

    [Theory]
    [MemberData(nameof(FriendlySingleSlotConsumers))]
    public async Task ACompleteFriendlyReplacementRemainsSelectableForEachConsumer(string card,int mana,bool mirror)
    {
        var s=await Control(mirror,true,card,mana,decoys:true);
        var request=Assert.IsType<RuleChoiceContinuation>(s.PendingRuleChoice).Request;
        Assert.Equal("P2",request.PlayerId);Assert.Equal("SPELL_CHOICE_REMAKE",request.Kind);
        Assert.DoesNotContain(request.Options,o=>o.Id=="CONFIRM");
        Assert.DoesNotContain(request.Options,o=>o.ObjectIds?.Any(id=>id is "HAND-UNIT" or "GRAVE-UNIT")==true);
        s=await RebuttalRulesTests.Choose(s,RebuttalRulesTests.Option(s,"重选目标 1 为","（TWO）"));
        s=await RebuttalRulesTests.Choose(s,"CONFIRM");
        var spell=Assert.Single(s.StackItems);
        Assert.Equal("P2",spell.ControllerId);Assert.Equal(new[]{"TWO"},spell.TargetObjectIds);
        Assert.Equal(s.CardObjects["TWO"].ObjectGeneration,spell.TargetGenerations!["TWO"]);
        Assert.Contains("CONTROL",s.PlayerZones["P2"].Graveyard);
        Assert.Equal(mirror?29:26,s.RunePools["P2"].Mana);
        Assert.Equal(mirror?28:27,s.RunePools["P2"].TotalPower);
        Restore(s);ArmamentMightTests.PublicRecovery(s);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEphemeralFriendlyUnitCannotCompleteCallOfTheShadowsRemake(bool mirror)
    {
        var s=await Control(mirror,true,"UNL-165/219",2,ephemeralFriendly:true);
        Assert.Null(s.PendingRuleChoice);
        var spell=Assert.Single(s.StackItems);
        Assert.Equal("P2",spell.ControllerId);Assert.Equal(new[]{"ONE"},spell.TargetObjectIds);
        Assert.Contains(CardObjectTags.Ephemeral,s.CardObjects["TWO"].Tags);
        Assert.Contains("CONTROL",s.PlayerZones["P2"].Graveyard);
        Restore(s);ArmamentMightTests.PublicRecovery(s);
    }

    private static async Task<MatchState> Control(bool mirror,bool friendly,string card="OGN·046/298",int mana=1,
        bool decoys=false,bool ephemeralFriendly=false)
    {
        var cards=new Dictionary<string,CardObjectState>{
            ["SPELL"]=new("SPELL",cardNo:card,manaCost:mana,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.SpellCard]),
            ["CONTROL"]=new("CONTROL",cardNo:mirror?"VEN·152":"OGN·080/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.SpellCard]),
            ["ONE"]=new("ONE",power:9,cardNo:"OGN·001/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard])};
        if(friendly)cards["TWO"]=new("TWO",power:9,cardNo:"OGN·001/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]);
        if(ephemeralFriendly)cards["TWO"]=cards["TWO"] with {Tags=[CardObjectTags.UnitCard,CardObjectTags.Ephemeral]};
        if(decoys)
            foreach(var id in new[]{"HAND-UNIT","GRAVE-UNIT"})
                cards[id]=new(id,power:9,cardNo:"OGN·001/298",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.UnitCard]);
        var initial=new MatchState("single-completion",0,1,"P1",new Dictionary<string,string>{["P1"]="P1",["P2"]="P2"},
            status:MatchStatuses.InProgress,phase:MatchPhases.Main,timingState:TimingStates.NeutralOpen,cardObjects:cards,
            playerZones:new Dictionary<string,PlayerZones>{
                ["P1"]=PlayerZones.Empty with{Hand=["SPELL"],Base=["ONE"]},
                ["P2"]=PlayerZones.Empty with{Hand=decoys?["CONTROL","HAND-UNIT"]:["CONTROL"],
                    Graveyard=decoys?["GRAVE-UNIT"]:[],Base=friendly?["TWO"]:[]}},
            runePools:new Dictionary<string,RunePool>{["P1"]=new(30,30),["P2"]=new(30,30)});
        var played=await Act(initial,"P1",new PlayCardCommand("SPELL",card,["ONE"]));
        var parent=Assert.Single(played.State.StackItems).StackItemId;
        var handed=await Act(played.State,"P1",new PassPriorityCommand());
        var cast=await Act(handed.State,"P2",new PlayCardCommand("CONTROL",mirror?"VEN·152":"OGN·080/298",[parent]));
        var s=(await Top(cast.State)).State;
        if(mirror){Assert.Equal("OPTIONAL_SPELL_CONTROL_PAYMENT",s.PendingRuleChoice!.Request.Kind);s=await RebuttalRulesTests.Choose(s,"PAY:ANY");}
        return s;
    }
}
