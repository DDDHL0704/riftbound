using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EndDeckPlayTests
{
    internal const string Aurora="OGN·160/298";

    [Theory]
    [InlineData("OGS·017/024", 2, false)]
    [InlineData("OGN·073/298", 4, true)]
    public async Task ActualEndRuneAbilityChoosesUpToThePrintedLimitBeforeAResponseWindow(string card, int limit, bool battlefield)
    {
        var initial=RunePosition(card,battlefield);var driver=Driver(initial,"runes-"+limit);
        await driver.Act("P1",new EndTurnCommand());
        var choice=Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("END_STEP_RUNE_TARGETS",choice.ChoiceWindow);Assert.Equal(0,choice.RequiredCount);Assert.Equal(limit,choice.MaxCount);
        Assert.Equal("P1",driver.State.TurnPlayerId);Assert.All(RuneIds, id=>Assert.True(driver.State.CardObjects[id].IsExhausted));
        var selected=RuneIds.Take(limit).ToArray();await driver.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,selected));
        Assert.All(RuneIds, id=>Assert.True(driver.State.CardObjects[id].IsExhausted));Assert.NotNull(driver.State.PriorityPlayerId);
        await driver.Top();Assert.Equal("P2",driver.State.TurnPlayerId);
        foreach(var id in RuneIds)Assert.Equal(!selected.Contains(id),driver.State.CardObjects[id].IsExhausted);
        Assert.Equal(limit,driver.Events().Count(e=>e.Event.Kind=="RUNE_READIED"&&e.Event.Payload["playerId"] as string=="P1"));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("OGS·017/024",false)]
    [InlineData("OGN·073/298",true)]
    public async Task ActualEndRuneAbilityCanChooseNone(string card,bool battlefield)
    {
        var initial=RunePosition(card,battlefield);var driver=Driver(initial,"runes-none-"+(battlefield?"sona":"annie"));
        await driver.Act("P1",new EndTurnCommand());var c=driver.State.PendingCardChoice!;
        await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[]));await driver.Top();
        Assert.All(RuneIds,id=>Assert.True(driver.State.CardObjects[id].IsExhausted));Assert.Equal("P2",driver.State.TurnPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARecycledRuneTargetIsNotReplacedByAnotherRuneAtResolution()
    {
        var initial=RunePosition("OGS·017/024",false);var driver=Driver(initial,"chosen-rune-recycled");
        await driver.Act("P1",new EndTurnCommand());var c=driver.State.PendingCardChoice!;
        await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[RuneIds[0],RuneIds[1]]));
        await driver.Act("P1",new RecycleRuneCommand(RuneIds[0]));await driver.Top();
        Assert.Contains(RuneIds[0],driver.State.PlayerZones["P1"].RuneDeck);
        Assert.False(driver.State.CardObjects[RuneIds[1]].IsExhausted);Assert.True(driver.State.CardObjects[RuneIds[2]].IsExhausted);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="RUNE_READIED"&&System.Text.Json.JsonSerializer.Serialize(e.Event.Payload).Contains(RuneIds[0]));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task SonaInBaseDoesNotTriggerAnEndRuneAbility()
    {
        var initial=RunePosition("OGN·073/298",false);var driver=Driver(initial,"sona-base");
        await driver.Act("P1",new EndTurnCommand());Assert.Equal("P2",driver.State.TurnPlayerId);
        Assert.All(RuneIds,id=>Assert.True(driver.State.CardObjects[id].IsExhausted));
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="TURN_END_STEP_STARTED");await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task MovingSonaAfterTargetConfirmationDoesNotEraseItsCapturedRuneAbility()
    {
        var initial=AttackDamageDistributionTests.Spell(RunePosition("OGN·073/298",true),"FLASH","OGS·011/024","P1");
        var driver=Driver(initial,"sona-leaves-battlefield");await driver.Act("P1",new EndTurnCommand());var c=driver.State.PendingCardChoice!;
        await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[RuneIds[3]]));
        await driver.Act("P1",new PlayCardCommand("FLASH","OGS·011/024",["HOST"]));await driver.Top();
        Assert.Equal("BASE",driver.State.ObjectLocations["HOST"].Zone);Assert.Equal("P1",driver.State.TurnPlayerId);
        await driver.Top();Assert.False(driver.State.CardObjects[RuneIds[3]].IsExhausted);Assert.True(driver.State.CardObjects[RuneIds[0]].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AnOrdinaryEquipmentDestructionSpellCannotBypassTheEndResponsePermission()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"SALVAGE","SFD·005/221","P1");
        var driver=Driver(initial,"ordinary-end-response-rejected");await driver.Act("P1",new EndTurnCommand());
        var before=driver.State;var command=new PlayCardCommand("SALVAGE","SFD·005/221",["GEAR"]);
        var rejected=await new CoreRuleEngine().ResolveAsync(before,new("slow-response","P1",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(before),MatchStateHasher.Hash(rejected.State));
        await driver.Top();await driver.Act("P1",new PlayCardCommand("END_UNIT","OGN·096/298",[]));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("malformed")] [InlineData("turn")] [InlineData("count")] [InlineData("tick")] [InlineData("source")]
    public async Task RecoveryRejectsForgedDelayedEndAbilityMarkers(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(PeakPosition());await driver.Act("P1",new MoveUnitCommand("HOST","BASE","BATTLEFIELD:F",[]));
        while(driver.State.FocusPlayerId is not null)await driver.Act(driver.State.FocusPlayerId,new PassFocusCommand());
        await driver.Top();var marker=Assert.Single(driver.State.UntilEndOfTurnEffects,id=>id.StartsWith("DELAYED_END_READY:"));
        var c=System.Text.Json.JsonSerializer.Deserialize<EndAbilityContext>(Convert.FromBase64String(marker["DELAYED_END_READY:".Length..]))!;
        var changed=mutation switch {"turn"=>c with {TurnNumber=99},"count"=>c with {RuneCount=99},"tick"=>c with {CreationTick=999},
            "source"=>c with {Source=c.Source with {CardNo="OGN·294/298"}},_=>c};
        var bad=mutation=="malformed"?"DELAYED_END_READY:wrong": "DELAYED_END_READY:"+Convert.ToBase64String(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(changed));
        var state=driver.State with {UntilEndOfTurnEffects=driver.State.UntilEndOfTurnEffects.Select(x=>x==marker?bad:x).ToArray()};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state),e=>e.Contains("delayed end-step"));
    }

    [Fact]
    public async Task ActualUnopposedConquestCreatesTheDelayOnlyAfterItsRespondableAbilityResolves()
    {
        var initial=PeakPosition();var driver=Driver(initial,"peak-delayed-runes");
        await driver.Act("P1",new MoveUnitCommand("HOST","BASE","BATTLEFIELD:F",[]));
        while(driver.State.FocusPlayerId is not null)await driver.Act(driver.State.FocusPlayerId,new PassFocusCommand());
        Assert.DoesNotContain(driver.State.UntilEndOfTurnEffects,s=>s.StartsWith("DELAYED_END_READY:"));
        Assert.Contains(driver.State.StackItems,s=>s.EndAbility is {Kind:"DELAYED_READY_RUNES"});
        await driver.Top();Assert.Single(driver.State.UntilEndOfTurnEffects,s=>s.StartsWith("DELAYED_END_READY:"));
        Assert.Null(driver.State.PendingCardChoice);
        if(driver.State.FocusPlayerId is not null){await driver.Act(driver.State.FocusPlayerId,new PassFocusCommand());await driver.Act(driver.State.FocusPlayerId!,new PassFocusCommand());}
        await driver.Act("P1",new EndTurnCommand());var c=driver.State.PendingCardChoice!;
        Assert.Equal(2,c.MaxCount);await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[RuneIds[2],RuneIds[4]]));await driver.Top();
        Assert.False(driver.State.CardObjects[RuneIds[2]].IsExhausted);Assert.False(driver.State.CardObjects[RuneIds[4]].IsExhausted);
        Assert.True(driver.State.CardObjects[RuneIds[0]].IsExhausted);Assert.Equal("P2",driver.State.TurnPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ASpellOnlyCounterCannotRemoveTheCapturedEndAbility()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"COUNTER","OGN·064/298","P2");
        var driver=Driver(initial,"spell-counter-rejected");await driver.Act("P1",new EndTurnCommand());var id=driver.State.StackItems.Single().StackItemId;
        await driver.Act("P1",new PassPriorityCommand());
        var before=driver.State;var command=new PlayCardCommand("COUNTER","OGN·064/298",[id]);
        var rejected=await new CoreRuleEngine().ResolveAsync(before,new("wrong-kind","P2",command.CmdType),command,default);
        Assert.False(rejected.Accepted);Assert.Equal(MatchStateHasher.Hash(before),MatchStateHasher.Hash(rejected.State));
        await driver.Act("P2",new PassPriorityCommand());Assert.NotNull(driver.State.PendingEffectPlay);
        await driver.Act("P1",new PlayCardCommand("END_UNIT","OGN·096/298",[]));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task RevealingASequenceStopsAtTheFirstUnitAndDoesNotExposeTheNextCard()
    {
        var initial=Position();var cards=initial.CardObjects.ToDictionary();var locations=initial.ObjectLocations.ToDictionary();
        var prefixes=Enumerable.Range(1,4).Select(n=>"PREFIX_"+n).ToArray();
        foreach(var id in prefixes){cards[id]=new(id,cardNo:"SFD·042/221",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]);locations[id]=new("P1","MAIN_DECK");}
        cards["SECRET_NEXT"]=new("SECRET_NEXT",cardNo:"SFD·042/221",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]);locations["SECRET_NEXT"]=new("P1","MAIN_DECK");
        initial=initial with {CardObjects=cards,ObjectLocations=locations,PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {MainDeck=prefixes.Concat(["END_PREFIX","END_UNIT","SECRET_NEXT"]).ToArray()}}};
        var driver=Driver(initial,"long-reveal-prefix");await driver.Act("P1",new EndTurnCommand());await driver.Top();
        var reveal=Assert.Single(driver.Events(),e=>e.Event.Kind=="CARDS_REVEALED");Assert.Equal(6,reveal.Event.Payload["count"]);
        Assert.DoesNotContain("SECRET_NEXT",System.Text.Json.JsonSerializer.Serialize(reveal.Event.Payload));Assert.Equal("SECRET_NEXT",driver.State.PlayerZones["P1"].MainDeck[0]);
        await driver.Act("P1",new PlayCardCommand("END_UNIT","OGN·096/298",[]));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("OGN·208/298")] [InlineData("SFD·044/221")] [InlineData("UNL-166/219")]
    public async Task EndFreePlayIgnoresPrintedMandatoryAdditionalCosts(string card)
    {
        var initial=DeckPlaySequenceTests.TopCard(Position(),"END_UNIT",card);
        initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P1"]=RunePool.Empty}};
        var driver=Driver(initial,"aurora-free-"+card.Replace('·','-').Replace('/','-'));await driver.Act("P1",new EndTurnCommand());await driver.Top();
        await driver.Act("P1",new PlayCardCommand("END_UNIT",card,[]));Assert.Contains("END_UNIT",driver.State.PlayerZones["P1"].Base);
        Assert.All(driver.Events().Where(e=>e.Event.Kind=="COST_PAID"),e=>Assert.Equal(0,e.Event.Payload["mana"]));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    internal static MatchState PeakPosition()
    {
        var state=RunePosition("OGN·073/298",false);
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
            ["HOST"]=state.CardObjects["HOST"] with {CardNo="OGN·096/298",Power=1},
            ["F"]=state.CardObjects["F"] with {CardNo="OGN·289/298",ControllerId=null}}};
        return state;
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task EmptyOrUnitlessDeckFinishesWithoutAFreePlayPermission(bool empty)
    {
        var initial=Position();var moved=empty?new[]{"END_PREFIX","END_UNIT"}:new[]{"END_UNIT"};
        initial=initial with {PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {
            MainDeck=empty?[]:["END_PREFIX"],Hand=initial.PlayerZones["P1"].Hand.Concat(moved).ToArray()}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["END_UNIT"]=new("P1","HAND"),["END_PREFIX"]=new("P1",empty?"HAND":"MAIN_DECK")}};
        var driver=Driver(initial,empty?"empty-deck":"unitless-deck");await driver.Act("P1",new EndTurnCommand());await driver.Top();
        Assert.Null(driver.State.PendingEffectPlay);Assert.Equal("P2",driver.State.TurnPlayerId);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARD_BANISHED");
        Assert.Equal(empty?0:1,driver.Events().Count(e=>e.Event.Kind=="CARDS_RECYCLED"));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("step-player")] [InlineData("step-turn")] [InlineData("missing-step")]
    [InlineData("count")] [InlineData("controller")] [InlineData("source-card")] [InlineData("generation")]
    [InlineData("choice-count")] [InlineData("choice-actor")] [InlineData("choice-source")] [InlineData("choice-ids")]
    public async Task RecoveryRejectsForgedEndStepAndRuneChoice(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(RunePosition("OGS·017/024",false));await driver.Act("P1",new EndTurnCommand());
        var state=driver.State;var item=state.StackItems.Single();var c=item.EndAbility!;
        switch(mutation){
            case "step-player":state=state with {EndStep=state.EndStep! with {PlayerId="P2"}};break;
            case "step-turn":state=state with {EndStep=state.EndStep! with {TurnNumber=99}};break;
            case "missing-step":state=state with {EndStep=null};break;
            case "count":c=c with {RuneCount=9};break;
            case "controller":c=c with {Source=c.Source with {ControllerId="P2"}};break;
            case "source-card":c=c with {Source=c.Source with {CardNo="OGN·096/298"}};break;
            case "generation":c=c with {Source=c.Source with {ObjectGeneration=99}};break;
            case "choice-count":state=state with {PendingCardChoice=state.PendingCardChoice! with {MaxCount=5}};break;
            case "choice-actor":state=state with {PendingCardChoice=state.PendingCardChoice! with {PlayerId="P2"}};break;
            case "choice-source":state=state with {PendingCardChoice=state.PendingCardChoice! with {SourceObjectId="HOST"}};break;
            case "choice-ids":state=state with {PendingCardChoice=state.PendingCardChoice! with {LegalObjectIds=["HOST"]}};break;
        }
        state=state with {StackItems=[item with {EndAbility=c}]};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state),e=>e.Contains("end-step"));
    }

    [Theory]
    [InlineData("generation")] [InlineData("unit-target")] [InlineData("repeat")]
    public async Task RecoveryRejectsForgedConfirmedRuneTargets(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(RunePosition("OGS·017/024",false));await driver.Act("P1",new EndTurnCommand());var c=driver.State.PendingCardChoice!;
        await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[RuneIds[0]]));var item=driver.State.StackItems.Single();
        item=mutation switch {"generation"=>item with {TargetGenerations=new Dictionary<string,long>{[RuneIds[0]]=99}},
            "unit-target"=>item with {TargetObjectIds=["HOST"],TargetGenerations=new Dictionary<string,long>{["HOST"]=0}},
            _=>item with {EffectRepeatCount=2}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {StackItems=[item]}),e=>e.Contains("end-step"));
    }

    [Theory]
    [InlineData("permission")] [InlineData("source-generation")] [InlineData("second-unit")]
    public async Task RecoveryRejectsForgedAuroraPendingPlayAndRevealSequence(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await driver.Act("P1",new EndTurnCommand());await driver.Top();var p=driver.State.PendingEffectPlay!;
        if(mutation=="permission")p=p with {IgnoreAllCosts=false};
        if(mutation=="source-generation")p=p with {Parent=p.Parent with {DeckPlaySequence=p.Parent.DeckPlaySequence! with {Source=new("GEAR",99)}}};
        if(mutation=="second-unit")p=p with {Parent=p.Parent with {DeckPlaySequence=p.Parent.DeckPlaySequence! with {
            Viewed=new Dictionary<string,IReadOnlyDictionary<string,long>> { ["P1"]=new Dictionary<string,long>{{"END_UNIT",0},{"HOOK_TOP",0}} }}}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {PendingEffectPlay=p}),e=>e.Contains("deck"));
    }

    internal static readonly string[] RuneIds=["END_RUNE_1","END_RUNE_2","END_RUNE_3","END_RUNE_4","END_RUNE_5"];
    internal static MatchState RunePosition(string card,bool battlefield)
    {
        var state=Position();state=LocalDestructionRecallTests.At(state,"GEAR","P1","HAND");
        state=state with {PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Hand=state.PlayerZones["P1"].Hand.Append("GEAR").ToArray(),Battlefields=state.PlayerZones["P1"].Battlefields.Where(id=>id!="GEAR").ToArray()}}};
        var source=card=="OGS·017/024"?"END_LEGEND":"HOST";
        if(source=="HOST")state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){[source]=state.CardObjects[source] with {CardNo=card,Power=3}}};
        else state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){[source]=new(source,cardNo:card,ownerId:"P1",controllerId:"P1")},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){[source]=new("P1","LEGEND")},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {LegendZone=[source]}}};
        if(battlefield)state=LocalDestructionRecallTests.At(state,source,"P1","BATTLEFIELD","F");
        var cards=state.CardObjects.ToDictionary();var locations=state.ObjectLocations.ToDictionary();
        foreach(var id in RuneIds){cards[id]=new(id,isExhausted:true,cardNo:"OGN·295/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.RuneCard,"COLOR:red"]);locations[id]=new("P1","BASE");}
        return state with {CardObjects=cards,ObjectLocations=locations,PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {Base=state.PlayerZones["P1"].Base.Concat(RuneIds).ToArray()}}};
    }

    [Fact]
    public async Task ActualEndTurnCapturesTheAbilityBeforeCleanupAndDefersTheNextTurnUntilFreePlayFinishes()
    {
        var initial=Position();var driver=Driver(initial,"actual-end-unit");await driver.Act("P1",new EndTurnCommand());
        Assert.Equal("P1",driver.State.TurnPlayerId);Assert.Equal(MatchPhases.TurnEnd,driver.State.Phase);
        Assert.Single(driver.State.StackItems);Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARDS_REVEALED");
        await driver.Top();var pending=Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        Assert.True(pending.IgnoreAllCosts);Assert.Equal("STACK",driver.State.ObjectLocations["END_UNIT"].Zone);
        Assert.Equal(["END_PREFIX"],driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(2,Assert.Single(driver.Events(),e=>e.Event.Kind=="CARDS_REVEALED").Event.Payload["count"]);
        await driver.Act("P1",new PlayCardCommand("END_UNIT","OGN·096/298",[]));
        Assert.Contains("END_UNIT",driver.State.PlayerZones["P1"].Base);Assert.Equal("P2",driver.State.TurnPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    internal static MatchState Position()
    {
        var state=HookDeckPlayTests.Position();return state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
            ["GEAR"]=state.CardObjects["GEAR"] with {CardNo=Aurora},
            ["END_PREFIX"]=new("END_PREFIX",cardNo:"SFD·042/221",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]),
            ["END_UNIT"]=new("END_UNIT",cardNo:"OGN·096/298",manaCost:2,power:1,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]),
            ["P2_DRAW"]=new("P2_DRAW",cardNo:"SFD·042/221",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.EquipmentCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["END_PREFIX"]=new("P1","MAIN_DECK"),["END_UNIT"]=new("P1","MAIN_DECK"),["P2_DRAW"]=new("P2","MAIN_DECK"),["HOOK_TOP"]=new("P1","HAND"),["HOOK_OTHER"]=new("P1","HAND")},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {MainDeck=["END_PREFIX","END_UNIT"],Hand=state.PlayerZones["P1"].Hand.Concat(["HOOK_TOP","HOOK_OTHER"]).ToArray()},
                ["P2"]=state.PlayerZones["P2"] with {MainDeck=["P2_DRAW"]}},
            RunePools=new Dictionary<string,RunePool>(state.RunePools){["P1"]=new(20,20),["P2"]=new(20,20)}};
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state,string branch)
    {
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_END_DECK_EVIDENCE");return new(state,string.IsNullOrWhiteSpace(root)?null:Path.Combine(root,branch));
    }
}
