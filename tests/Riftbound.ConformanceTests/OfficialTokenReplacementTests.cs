using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialTokenReplacementTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task OptionalReplacementPrecedesEntryAndAllImagesInheritReflexiveCopy(int sources)
    {
        var opened = await Open(Position(sources));
        Assert.NotNull(opened.State.PendingCardChoice);
        Assert.Empty(Tokens(opened.State));
        Assert.Single(opened.State.StackItems); Assert.DoesNotContain("C",opened.State.PlayerZones["P1"].Graveyard); Restore(opened.State);
        for(var i=sources;i>0;i--) opened=await Choose(opened.State,"Z"+i);
        Assert.Null(opened.State.PendingCardChoice);
        Assert.Equal(sources+1,Tokens(opened.State).Length);
        Assert.All(Tokens(opened.State),c=>{Assert.Equal(0,c.Power);Assert.Equal("UNL·T06",c.CardNo);Assert.False(c.IsExhausted);Assert.DoesNotContain(CardObjectTags.Ephemeral,c.Tags);});
        Assert.Equal(sources+1,opened.State.StackItems.Single().ReflexiveCopy!.Recipients.Count); Restore(opened.State);
        var copied=await Top(opened.State);
        Assert.All(Tokens(copied.State),c=>{Assert.Equal("SFD·068/221",c.CardNo);Assert.Equal(3,c.Power);Assert.Contains(CardObjectTags.Ephemeral,c.Tags);}); Restore(copied.State);
    }

    [Fact]
    public async Task DeclinePreservesUseForALaterPlayButAcceptConsumesIt()
    {
        var first=await Choose((await Open(Position())).State);
        first=await Top(first.State);
        Assert.Single(Tokens(first.State));
        var next=OfficialCopyIdentityTests.AddSpell(first.State,"SECOND","UNL-200/219");
        var second=await Open(next,"SECOND");Assert.NotNull(second.State.PendingCardChoice);
        second=await Choose(second.State,"Z1");second=await Top(second.State);
        Assert.Equal(3,Tokens(second.State).Length);
        var third=await Open(OfficialCopyIdentityTests.AddSpell(second.State,"THIRD","UNL-200/219"),"THIRD");
        Assert.Null(third.State.PendingCardChoice);Assert.Equal(4,Tokens(third.State).Length);Restore(third.State);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("hidden")]
    [InlineData("opponent")]
    [InlineData("unlocated")]
    public async Task OnlyAVisibleFriendlySourceOnARealBattlefieldCanReplace(string mode)
    {
        var s=Position();var z=s.CardObjects["Z1"];
        if(mode=="hidden")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["Z1"]=z with {IsFaceDown=true}}};
        if(mode=="base")s=s with {PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Battlefields=["BFZ"],Base=["Z1"]}},ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["Z1"]=new("P1","BASE")}};
        if(mode=="unlocated")s=s with {ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["Z1"]=new("P1","BATTLEFIELD","missing")}};
        if(mode=="opponent")s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["Z1"]=z with {ControllerId="P2"},["BFZ"]=s.CardObjects["BFZ"] with {ControllerId="P2"}},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Battlefields=["BFZ"]},["P2"]=s.PlayerZones["P2"] with {Battlefields=["Z1"]}},ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["Z1"]=new("P2","BATTLEFIELD","BFZ")}};
        var done=await Open(s);Assert.Null(done.State.PendingCardChoice);Assert.Single(Tokens(done.State));
    }

    [Theory]
    [InlineData("OGN·094/298",2,"瞬息")]
    [InlineData("SFD·076/221",2,"机械")]
    [InlineData("UNL-069/219",3,"瞬息")]
    public async Task SharedCreationKeepsFactoryCharacteristicsAndSubsequentDraw(string card, int count, string tag)
    {
        var s=Position(card:card);var before=s.PlayerZones["P1"].Hand.Count;
        var cast=await Act(s,"P1",new PlayCardCommand("C",card,[]));var pending=await Top(cast.State);
        Assert.Empty(Tokens(pending.State));
        var done=await Choose(pending.State,"Z1");Assert.Equal(count,Tokens(done.State).Length);
        Assert.All(Tokens(done.State),c=>{Assert.Contains(tag,c.Tags);Assert.Contains(CardObjectTags.UnitCard,c.Tags);Assert.Equal(card=="SFD·076/221",c.IsExhausted);});
        if(card=="SFD·076/221")Assert.Equal(before,done.State.PlayerZones["P1"].Hand.Count);
        Restore(done.State);
    }

    [Fact]
    public async Task DecliningFirstOfMultipleTokensCanUseReplacementOnSecond()
    {
        var cast=await Act(Position(card:"UNL-069/219"),"P1",new PlayCardCommand("C","UNL-069/219",[]));
        var first=await Top(cast.State);var second=await Choose(first.State);
        Assert.Empty(Tokens(second.State));Assert.Equal(1,second.State.StackItems.Single().TokenEntryPlan!.NextToken);Restore(second.State);
        var done=await Choose(second.State,"Z1");Assert.Equal(3,Tokens(done.State).Length);Restore(done.State);
    }

    [Fact]
    public async Task EchoCanPauseInALaterExecutionWithoutReplayingEarlierEntry()
    {
        var s=Position(card:"SFD·031/221");
        var cast=await Act(s,"P1",new PlayCardCommand("C","SFD·031/221",[],OptionalCosts:["ECHO"]));
        var first=await Top(cast.State);var second=await Choose(first.State);
        Assert.Single(Tokens(second.State));Assert.Equal(1,second.State.StackItems.Single().CompletedRepeatExecutions);Restore(second.State);
        var done=await Choose(second.State,"Z1");Assert.Equal(3,Tokens(done.State).Length);
        Assert.Empty(done.State.StackItems);Assert.Single(done.State.PlayerZones["P1"].Graveyard,id=>id=="C");Restore(done.State);
    }

    [Fact]
    public async Task ChoiceCannotBeStolenOrForgedAndRecoveryRetainsConsumedUse()
    {
        var s=(await Open(Position(2))).State;var c=s.PendingCardChoice!;
        var stolen=await new CoreRuleEngine().ResolveAsync(s,new("steal","P2",CommandTypes.ChooseCards),new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["Z1"]),default);
        Assert.False(stolen.Accepted);Assert.Equal(MatchStateHasher.Hash(s),MatchStateHasher.Hash(stolen.State));
        s=(await Choose(s,"Z2")).State;Restore(s);Assert.Equal(["Z1"],s.PendingCardChoice!.LegalObjectIds);
        var item=s.StackItems.Single();var p=item.TokenEntryPlan!;
        foreach(var invalid in new[]{p with {OriginalCount=99},p with {NextToken=-1},p with {Applied=[p.Applied[0],p.Applied[0]]},p with {Applied=[new(new("TARGET",0),0)]}})
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with {StackItems=[item with {TokenEntryPlan=invalid}]}),e=>e.Contains("token entry"));
    }

    [Fact]
    public async Task CopySourceStillMissingAtResolutionDoesNotEraseExtraImages()
    {
        var opened=await Open(Position(2));opened=await Choose(opened.State,"Z1");opened=await Choose(opened.State,"Z2");
        var s=opened.State;
        s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["TARGET"]=s.CardObjects["TARGET"] with {ObjectGeneration=2}}};
        var done=await Top(s);Assert.Equal(3,Tokens(done.State).Length);
        Assert.All(Tokens(done.State),t=>{Assert.Equal(0,t.Power);Assert.Contains(CardObjectTags.Ephemeral,t.Tags);});Restore(done.State);
    }

    [Fact]
    public async Task ReplacementUseExpiresAtTurnEnd()
    {
        var done=await Choose((await Open(Position())).State,"Z1");done=await Top(done.State);
        Assert.Contains("TOKEN_ENTRY_REPLACEMENT_USED",done.State.CardObjects["Z1"].UntilEndOfTurnEffects);
        var next=await Act(done.State,"P1",new EndTurnCommand());
        Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED",next.State.CardObjects["Z1"].UntilEndOfTurnEffects);Restore(next.State);
    }

    [Fact]
    public async Task OrdinaryPermanentPlayAbilityUsesSameReplacementWindow()
    {
        var cast=await Act(Position(card:"OGN·211/298"),"P1",new PlayCardCommand("C","OGN·211/298",[]));
        Assert.Contains("C",cast.State.PlayerZones["P1"].Base);
        Assert.Null(cast.State.PendingCardChoice);Assert.Empty(Tokens(cast.State));
        var opened=await Top(cast.State);Assert.NotNull(opened.State.PendingCardChoice);Restore(opened.State);
        var done=await Choose(opened.State,"Z1");Assert.Equal(2,Tokens(done.State).Length);Restore(done.State);
    }

    [Fact]
    public async Task EachExtraTokenProducesItsOwnEnemyEntryTrigger()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["V"]=new("V",cardNo:"UNL-150/219",ownerId:"P2",controllerId:"P2",power:4,tags:[CardObjectTags.UnitCard]),
            ["BFV"]=new("BFV",cardNo:"OGN·296/298",ownerId:"P2",controllerId:"P2",tags:[P6TokenFactoryCatalog.BattlefieldCardTag])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {Battlefields=["BFV","V"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["V"]=new("P2","BATTLEFIELD","BFV"),["BFV"]=new("P2","BATTLEFIELD","BFV")}};
        var pending=await Open(s);Assert.Empty(pending.State.TriggerQueue);
        var done=await Choose(pending.State,"Z1");Assert.Equal(3,done.State.TriggerQueue.Count);
        Assert.Equal(2,done.State.TriggerQueue.Count(t=>t.UnitEntryContext is not null));Restore(done.State);
        done=await Act(done.State,"P1",new OrderTriggersCommand(OrderedTriggerIds:done.State.TriggerQueue.Select(t=>t.TriggerId).Reverse().ToArray()));
        for(var i=0;i<3;i++)done=await Top(done.State);
        Assert.All(Tokens(done.State),t=>{Assert.Contains("STUNNED",t.UntilEndOfTurnEffects);Assert.False(MovementRestrictionRules.CanMove(t,"P1"));});Restore(done.State);
    }

    [Fact]
    public async Task LastBreathReplacementWaitsBeforeCreatingDormantWarhawks()
    {
        var s=Position();s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects) {
            ["DEAD"]=new("DEAD",cardNo:"UNL-153/219",ownerId:"P1",controllerId:"P1",power:2,tags:[CardObjectTags.UnitCard])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Base=["DEAD"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["DEAD"]=new("P1","BASE")}};
        s=OfficialCopyIdentityTests.AddSpell(s,"KILL","OGN·229/298");
        var cast=await Act(s,"P1",new PlayCardCommand("KILL","OGN·229/298",["DEAD"]));
        var died=await Top(cast.State);Assert.Single(died.State.StackItems);
        var pending=await Top(died.State);Assert.Empty(Tokens(pending.State));Restore(pending.State);
        var done=await Choose(pending.State,"Z1");Assert.Equal(2,Tokens(done.State).Length);
        Assert.All(Tokens(done.State),t=>{Assert.True(t.IsExhausted);Assert.Contains(CardObjectTags.Spellshield,t.Tags);});Restore(done.State);
    }

    [Fact]
    public async Task HeldTokenReplacementSuspendsBeforeChannelAndKeepsDormancy()
    {
        var s=Position();s=s with {Phase=MatchPhases.TurnStart,TimingState=TimingStates.NeutralClosed,
            CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){["BFZ"]=s.CardObjects["BFZ"] with {CardNo="OGN·275/298"}}};
        var start=await Act(s,"P1",new PassPriorityCommand());var pending=await Top(start.State);
        Assert.NotNull(pending.State.PendingCardChoice);Assert.Empty(Tokens(pending.State));Restore(pending.State);
        var done=await Choose(pending.State,"Z1");Assert.Equal(2,Tokens(done.State).Length);
        Assert.All(Tokens(done.State),t=>Assert.True(t.IsExhausted));Restore(done.State);
    }

    [Fact]
    public async Task EntryTriggersWaitForAllEchoExecutionsAndReplacementChoicesBeforeOrdering()
    {
        var s=Position(card:"SFD·031/221");s=s with {CardObjects=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["V"]=new("V",cardNo:"UNL-150/219",ownerId:"P2",controllerId:"P2",power:4,tags:[CardObjectTags.UnitCard]),
            ["BFV"]=new("BFV",cardNo:"OGN·296/298",ownerId:"P2",controllerId:"P2",tags:[P6TokenFactoryCatalog.BattlefieldCardTag])},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P2"]=s.PlayerZones["P2"] with {Battlefields=["BFV","V"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["V"]=new("P2","BATTLEFIELD","BFV"),["BFV"]=new("P2","BATTLEFIELD","BFV")}};
        var cast=await Act(s,"P1",new PlayCardCommand("C","SFD·031/221",[],OptionalCosts:["ECHO"]));
        var first=await Top(cast.State);var second=await Choose(first.State);
        Assert.Single(second.State.TriggerQueue);Assert.Single(second.State.StackItems);
        Assert.NotNull(second.State.PendingCardChoice);Restore(second.State);
        var done=await Choose(second.State,"Z1");
        Assert.Empty(done.State.StackItems);Assert.Equal(3,done.State.TriggerQueue.Count);
        Assert.All(done.State.TriggerQueue,t=>Assert.NotNull(t.UnitEntryContext));Restore(done.State);
    }

    internal static CardObjectState[] Tokens(MatchState s)=>s.CardObjects.Values.Where(c=>c.TokenFactoryCardNo is not null).ToArray();
    internal static async Task<ResolutionResult> Open(MatchState s,string source="C")
    {var cast=await Act(s,"P1",new PlayCardCommand(source,"UNL-200/219",["TARGET"]));return await Top(cast.State);}
    internal static Task<ResolutionResult> Choose(MatchState s,params string[] ids)=>Act(s,s.PendingCardChoice!.PlayerId,new ChooseCardsCommand(s.PendingCardChoice.ChoiceId,s.PendingCardChoice.ChoiceWindow,ids));
    internal static MatchState Position(int sources=1,string card="UNL-200/219")
    {
        var s=OfficialCopyIdentityTests.Position();var cards=new Dictionary<string,CardObjectState>(s.CardObjects){
            ["C"]=s.CardObjects["C"] with {CardNo=card},
            ["BFZ"]=new("BFZ",cardNo:"OGN·297/298",ownerId:"P1",controllerId:"P1",tags:[P6TokenFactoryCatalog.BattlefieldCardTag])};
        var locations=new Dictionary<string,ObjectLocationState>(s.ObjectLocations){["BFZ"]=new("P1","BATTLEFIELD","BFZ")};
        var ids=Enumerable.Range(1,sources).Select(i=>"Z"+i).ToArray();
        foreach(var id in ids){cards[id]=new(id,cardNo:"UNL-086/219",power:5,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]);locations[id]=new("P1","BATTLEFIELD","BFZ");}
        return s with {CardObjects=cards,ObjectLocations=locations,RunePools=new Dictionary<string,RunePool>{{"P1",new(30,30)},{"P2",new(30,30)}},
            PlayerZones=new Dictionary<string,PlayerZones>(s.PlayerZones){["P1"]=s.PlayerZones["P1"] with {Battlefields=s.PlayerZones["P1"].Battlefields.Concat(new[]{"BFZ"}).Concat(ids).ToArray()}}};
    }
}
