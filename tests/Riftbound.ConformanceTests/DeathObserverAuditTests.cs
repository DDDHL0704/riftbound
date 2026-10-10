using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.ReplacementResourceTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class DeathObserverAuditTests
{
    internal static MatchState Source(MatchState s,string no,string id="OBSERVER",string player="P2") => s with {
        CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { [id]=new(id,power:8,cardNo:no,ownerId:player,controllerId:player,tags:[CardObjectTags.UnitCard]) },
        PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { [player]=s.PlayerZones[player] with { Base=s.PlayerZones[player].Base.Append(id).ToArray() } },
        ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { [id]=new(player,"BASE") } };
    internal static MatchState NoReplacement(bool two=false)
    {
        var s=Position(power:0,two:two);
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects) { ["D"] = s.CardObjects["D"] with { CardNo = "UNL-008/219" } };
        if (two) cards["D2"] = cards["D2"] with { CardNo = "UNL-008/219" };
        return s with { CardObjects = cards };
    }
    [Theory]
    [InlineData("ARC-006/006")]
    [InlineData("UNL-068/219")]
    [InlineData("UNL-129/219")]
    public async Task EveryDeathTriggersOnceEvenWhenTwoUnitsDieTogether(string no)
    {
        var result=await Open(Source(NoReplacement(true),no));
        Assert.Equal(2,result.Events.Count(e=>e.Kind=="TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("sourceObjectId") as string=="OBSERVER"));
        Restore(result.State);
    }
    [Fact]
    public async Task AnimatedGoldAndFollowingDeathKeepFirstDeathAndRecovery()
    {
        var s=Source(Source(Position(power:0),"OGN·118/298"),"ARC-006/006","VIKTOR");
        s=AddResource(s,P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["RESOURCE"]=s.CardObjects["RESOURCE"] with { Power=5,Tags=[..s.CardObjects["RESOURCE"].Tags,CardObjectTags.UnitCard] } } };
        var generated=await Resource((await Open(s)).State);
        var done=await Choose(generated.State,"DECLINE");
        Assert.Single(done.Events,e=>e.Kind=="TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("sourceObjectId") as string=="OBSERVER");
        var errors=OfficialInsightAndSpellLockTests.Errors(done.State);
        Assert.True(errors.Count==0,string.Join("; ",errors)); Restore(done.State);
    }
    [Fact]
    public async Task DeathBelongsToControllerNotOwner()
    {
        var s=Source(NoReplacement(),"UNL-129/219");
        s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { ["D"]=s.CardObjects["D"] with { OwnerId="P1" } } };
        var result=await Open(s);
        Assert.Single(result.Events,e=>e.Kind=="TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("sourceObjectId") as string=="OBSERVER");
    }

    [Theory]
    [InlineData("ARC-006/006")]
    [InlineData("UNL-068/219")]
    [InlineData("UNL-129/219")]
    [InlineData("OGN·118/298")]
    public async Task ObserverDyingInTheSameActionDoesNotTrigger(string no)
    {
        var s=Source(NoReplacement(),no);
        s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { ["OBSERVER"]=s.CardObjects["OBSERVER"] with { Power=1,Damage=0 } } };
        var done=await Open(s,"UNL-180/219");
        Assert.DoesNotContain(done.Events,e=>e.Kind=="TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("sourceObjectId") as string=="OBSERVER");
        Restore(done.State);
    }

    [Theory]
    [InlineData("ARC-006/006")]
    [InlineData("UNL-129/219")]
    [InlineData("OGN·118/298")]
    public async Task EarnedTriggerSurvivesSourceLeavingOrChangingController(string no)
    {
        var earned=await Open(Source(NoReplacement(),no));
        var s=earned.State;
        Assert.Single(s.StackItems);
        s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { ["OBSERVER"]=s.CardObjects["OBSERVER"] with { ControllerId="P1",ObjectGeneration=1 } },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"]=s.PlayerZones["P2"] with { Base=s.PlayerZones["P2"].Base.Where(id=>id!="OBSERVER").ToArray(),Graveyard=s.PlayerZones["P2"].Graveyard.Append("OBSERVER").ToArray() } },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["OBSERVER"]=new("P2","GRAVEYARD") } };
        Restore(s);
        var done=await Top(s); Restore(done.State);
        if(no=="ARC-006/006") { var token=Assert.Single(done.State.PlayerZones["P2"].Base,id=>id.Contains("-TOKEN-")); Assert.True(done.State.CardObjects[token].IsExhausted); }
        else if(no=="UNL-129/219") Assert.Equal(1,done.State.PlayerExperience["P2"]);
        else Assert.Contains("DRAW",done.State.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CentaurCannotBuffANewIncarnation()
    {
        var earned=await Open(Source(NoReplacement(),"UNL-068/219"));
        var s=earned.State with { CardObjects=new Dictionary<string,CardObjectState>(earned.State.CardObjects) {
            ["OBSERVER"]=earned.State.CardObjects["OBSERVER"] with { ObjectGeneration=1,Power=3 } } };
        var done=await Top(s); Restore(done.State); Assert.Equal(3,done.State.CardObjects["OBSERVER"].Power);
    }

    [Fact]
    public async Task FirstDeathLedgerBelongsToControllerAndDoesNotDependOnObserverBeingPresent()
    {
        var first=await Open(NoReplacement());
        Assert.Equal(1,first.State.DeathLedger.Counts["P2"]);
        var s=Source(first.State,"OGN·118/298");
        s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { ["D"]=s.CardObjects["D"] with { Damage=1,Power=2,ObjectGeneration=1,Tags=[CardObjectTags.UnitCard] } },
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) {
                ["P1"]=s.PlayerZones["P1"] with { Hand=["AOE"],Graveyard=[] },
                ["P2"]=s.PlayerZones["P2"] with { Graveyard=[],Battlefields=["BF","D"] } },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["D"]=new("P2","BATTLEFIELD","BF") } };
        var second=await Open(s);
        Assert.Equal(2,second.State.DeathLedger.Counts["P2"]);
        Assert.DoesNotContain(second.Events,e=>e.Kind=="TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("sourceObjectId") as string=="OBSERVER");
        Restore(second.State);
    }

    [Fact]
    public async Task CapturedDeathRejectsTamperingAndSurvivesVictimChangingZones()
    {
        var earned=await Open(Source(NoReplacement(true),"ARC-006/006"));
        var s=earned.State; var t=s.TriggerQueue[0]; var c=t.DeathObserver!;
        foreach(var forged in new[] { c with { Kind="BAD" },c with { CardNo="UNL-008/219" },c with { SourceGeneration=-1 },
            c with { Destroyed=c.Destroyed with { ControllerId="P1" } },c with { Destroyed=c.Destroyed with { WasMinion=true } },c with { Destroyed=c.Destroyed with { ObjectId=t.SourceObjectId } } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(s with { TriggerQueue=[t with { DeathObserver=forged },s.TriggerQueue[1]] }));
        s=s with { PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones) { ["P2"]=s.PlayerZones["P2"] with { Graveyard=[],Hand=[..s.PlayerZones["P2"].Hand,"D","D2"] } },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations) { ["D"]=new("P2","HAND"),["D2"]=new("P2","HAND") } };
        Restore(s);
        var result=await Act(s,"P2",new OrderTriggersCommand(OrderedTriggerIds:s.TriggerQueue.Select(x=>x.TriggerId).Reverse().ToArray()));
        Restore(result.State); var top=await Top(result.State); Restore(top.State);
    }

    [Fact]
    public async Task PlayerAndSpectatorViewsRecoverMultipleTokenDeathObservers()
    {
        var s=Source(Source(NoReplacement(),"ARC-006/006"),"UNL-129/219","JAW");
        var token=P6TokenFactoryCatalog.GetAll().Single(t=>t.CardNo=="SFD·T01").CreateObject("D","P2","P2") with { Damage=2 };
        s=s with { CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) { ["D"]=token } };
        var result=await Open(s); s=result.State; Assert.DoesNotContain("D",s.CardObjects.Keys); Assert.Equal(2,s.TriggerQueue.Count);
        var views=s.Seats.Keys.ToDictionary(id=>id,id=>new RecoveredPlayerView(id,s.Tick,0,result.Snapshots[id],s.Tick,0,result.Prompts[id]));
        var spectator=MatchReplayRedactor.BuildSpectatorFrame(s.RoomId,s.Tick,0,[],s);
        var errors=MatchRecoveryValidator.Validate(s.RoomId,0,[],[],views,s,s.Tick,spectator);
        Assert.True(errors.Count==0,string.Join("; ",errors));
    }
    [Fact]
    public async Task ViktorUsesSharedTokenReplacementBeforeEnteringPlay()
    {
        var s = Source(Source(NoReplacement(), "ARC-006/006"), "UNL-086/219", "ZILEAN");
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with {
                Base = ["OBSERVER"], Battlefields = [..s.PlayerZones["P2"].Battlefields, "ZILEAN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["ZILEAN"] = new("P2", "BATTLEFIELD", "BF") }
        };
        var pending = await Top((await Open(s)).State);
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", pending.State.PendingCardChoice!.ChoiceWindow);
        Assert.DoesNotContain(pending.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
        Restore(pending.State);
        var done = await OfficialTokenReplacementTests.Choose(pending.State, "ZILEAN");
        var tokens = done.State.CardObjects.Values.Where(c => c.TokenFactoryCardNo is not null).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.All(tokens, c => { Assert.True(c.IsExhausted); Assert.Equal("P2", c.ControllerId); Assert.Contains(c.ObjectId, done.State.PlayerZones["P2"].Base); });
        Assert.Contains("TOKEN_ENTRY_REPLACEMENT_USED", done.State.CardObjects["ZILEAN"].UntilEndOfTurnEffects);
        Restore(done.State);
    }

    [Fact]
    public async Task DisappearedObserverKeepsItsEarnedSkillsAndPublicContextCannotBeForged()
    {
        var s = Source(NoReplacement(true), "UNL-129/219");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["OBSERVER"] = s.CardObjects["OBSERVER"] with { TokenFactoryCardNo = "SFD·T01" } } };
        s = (await Open(s)).State;
        s = s with {
            CardObjects = s.CardObjects.Where(p => p.Key != "OBSERVER").ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = s.ObjectLocations.Where(p => p.Key != "OBSERVER").ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = [] } }
        };
        var session = new MatchSession(s, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var snapshots = ResolutionResult.BuildSnapshots(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, 0, snapshots[id], s.Tick, 0, session.PromptFor(id)));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s);
        var errors = MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick, spectator);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        var publicQueue = Assert.IsAssignableFrom<IEnumerable<object>>(snapshots["P2"].Timing!["triggerQueue"]);
        var first = Assert.IsType<Dictionary<string, object?>>(publicQueue.First());
        first["deathObserver"] = s.TriggerQueue[0].DeathObserver! with { SourceGeneration = 20 };
        Assert.Contains(MatchRecoveryValidator.Validate(s.RoomId, 0, [], [], views, s, s.Tick), e => e.Contains("captured death observer differs"));
        var ordered = await Act(s, "P2", new OrderTriggersCommand(OrderedTriggerIds: s.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var done = await Top((await Top(ordered.State)).State);
        Assert.Equal(2, done.State.PlayerExperience["P2"]); Restore(done.State);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("D2")]
    public async Task FirstSimultaneousDeathIsChosenAndResumesAfterRecovery(string victim)
    {
        var opened = await Open(Source(NoReplacement(true), "OGN·118/298"));
        var pending = Assert.IsType<RuleChoiceContinuation>(opened.State.PendingRuleChoice);
        Assert.Equal(2, pending.Request.Options.Count);
        Assert.Empty(opened.State.DeathLedger.Counts);
        var restored = OfficialInsightAndSpellLockTests.Restore(opened.State);
        var option = pending.Request.Options.Single(o => o.ObjectIds![0] == victim);
        var done = await Act(restored, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option.Id]));
        Assert.Equal(victim, Assert.Single(done.State.StackItems).DeathObserver!.Destroyed.ObjectId);
        Assert.Equal(2, done.State.DeathLedger.Counts["P2"]);
        Assert.Single(done.Events, e => e.Kind == "TRIGGER_QUEUED");
        var resolved = await Top(done.State);
        Assert.Contains("DRAW", resolved.State.PlayerZones["P2"].Hand);
        Restore(resolved.State);
    }

    [Fact]
    public async Task NestedResourceDeathTriggersObserverBeforeItsOuterDeath()
    {
        var s = AddResource(Position(power: 0), P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { CardNo = "UNL-129/219" },
            ["RESOURCE"] = s.CardObjects["RESOURCE"] with {
                Power = 5, Tags = [..s.CardObjects["RESOURCE"].Tags, CardObjectTags.UnitCard] }
        } };
        var generated = await Resource((await Open(s)).State);
        var done = await Choose(generated.State, "DECLINE");
        Assert.Contains("D", done.State.PlayerZones["P2"].Graveyard);
        var item = Assert.Single(done.State.StackItems);
        Assert.Equal("RESOURCE", item.DeathObserver!.Destroyed.ObjectId);
        Assert.Equal("D", item.SourceObjectId);
        Restore(done.State);
        var resolved = await Top(done.State);
        Assert.Equal(1, resolved.State.PlayerExperience["P2"]);
        Restore(resolved.State);
    }

    [Fact]
    public async Task RetiredStackRecordCannotBypassRejectionByChangingEffectKind()
    {
        var earned = await Open(Source(NoReplacement(), "ARC-006/006"));
        var item = Assert.Single(earned.State.StackItems);
        var retired = earned.State with { StackItems = [item with {
            StackItemId = "ordered-TRIGGER-old-OBSERVER-D-VIKTOR_DESTROYED_NON_MINION_CREATE_MINION",
            EffectKind = "WRONG_EFFECT", DeathObserver = null
        }] };
        Assert.Contains("obsolete uncaptured death observer stack", OfficialInsightAndSpellLockTests.Errors(retired));
    }

}
