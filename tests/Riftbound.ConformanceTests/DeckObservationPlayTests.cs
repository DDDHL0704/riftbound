using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeckObservationPlayTests
{
    internal const string Nocturne="OGN·194/298";

    [Fact]
    public async Task ARealCardTrickPausesForTheSeenCardsStaticBanishBeforeChoosingTheRemainingCards()
    {
        var initial=Position();var driver=Driver(initial,"card-trick-one");
        await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();
        var c=Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH",c.Request.Kind);
        await driver.Act("P1",new PayCostCommand(c.Request.Id,"RULE_REPLACEMENT",["BANISH"]));
        var choice=Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.DoesNotContain("NOCT",choice.LegalObjectIds);Assert.Equal(["END_PREFIX","END_UNIT"],choice.ContextObjectIds);
        await driver.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["END_PREFIX"]));
        Assert.Contains("END_PREFIX",driver.State.PlayerZones["P1"].Hand);
        Assert.Equal("STACK",driver.State.ObjectLocations["NOCT"].Zone);
        Assert.NotNull(driver.State.PendingEffectPlay);
        var play=new PlayCardCommand("NOCT",Nocturne,[]);
        var quote=new CoreRuleEngine().PreviewPlayCard(driver.State,"P1",PlayCostPreviewTests.Request(driver.State,play));
        Assert.True(quote.CanPay,quote.Message);Assert.Equal(0,quote.Cost!.Mana);Assert.Equal(1,quote.Cost.GenericPower);
        await driver.Act("P1",play);
        Assert.Contains("NOCT",driver.State.PlayerZones["P1"].Base);Assert.True(driver.State.CardObjects["NOCT"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AuroraStopsAtTheRevealedUnitAndUsesOnlyTheChosenPlayPermission(bool banish)
    {
        var initial=Position();var driver=Driver(initial,banish?"aurora-self-banish":"aurora-free");
        await driver.Act("P1",new EndTurnCommand());await driver.Top();await Decide(driver,banish);
        Assert.NotNull(driver.State.PendingEffectPlay);
        Assert.DoesNotContain("END_UNIT",System.Text.Json.JsonSerializer.Serialize(driver.Events().Where(e=>e.Event.Kind=="CARDS_REVEALED").Select(e=>e.Event.Payload)));
        var power=driver.State.RunePools["P1"].Power;
        await driver.Act("P1",new PlayCardCommand("NOCT",Nocturne,[]));Assert.Equal("P2",driver.State.TurnPlayerId);
        var paid=driver.Events().Where(e=>e.Event.Kind=="COST_PAID").Last();Assert.Equal(banish?1:0,paid.Event.Payload["power"]);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task BothPlayersSeeARevealedUnitWhileItsStaticBanishChoiceIsPending()
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await driver.Act("P1",new EndTurnCommand());await driver.Top();
        var views=ResolutionResult.BuildSnapshots(driver.State);
        foreach(var player in new[]{"P1","P2"})Assert.Contains("NOCT",System.Text.Json.JsonSerializer.Serialize(views[player]));
        Assert.DoesNotContain("END_UNIT",System.Text.Json.JsonSerializer.Serialize(views["P2"]));
        var pending=driver.State.PendingRuleChoice!;
        var bad=pending with {Request=pending.Request with {Preview=pending.Request.Preview! with {
            DeckObservationView=pending.Request.Preview!.DeckObservationView! with {Cards=new Dictionary<string,long>{{"END_UNIT",0}}}}}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {PendingRuleChoice=bad}),e=>e.Contains("replacement choice continuation"));
    }

    [Fact]
    public async Task ThreeObservedUnitsCanAllBeBanishedBeforeTheOriginalPutIntoHandInstruction()
    {
        var initial=Position();var cards=initial.CardObjects.ToDictionary();var locations=initial.ObjectLocations.ToDictionary();
        foreach(var id in new[]{"NOCT2","NOCT3"}){cards[id]=cards["NOCT"] with {ObjectId=id};locations[id]=new("P1","MAIN_DECK");}
        initial=initial with {CardObjects=cards,ObjectLocations=locations,PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {MainDeck=["NOCT","NOCT2","NOCT3","END_PREFIX","END_UNIT"]}}};
        var driver=Driver(initial,"three-nocturnes");await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();
        for(var i=0;i<3;i++)await Decide(driver,true);
        Assert.Null(driver.State.PendingCardChoice);Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind is "CARD_ADDED_TO_HAND" or "CARDS_RECYCLED");
        foreach(var id in new[]{"NOCT","NOCT2","NOCT3"})await driver.Act("P1",new PlayCardCommand(id,Nocturne,[]));
        Assert.All(new[]{"NOCT","NOCT2","NOCT3"},id=>Assert.Contains(id,driver.State.PlayerZones["P1"].Base));
        Assert.Equal(["END_PREFIX","END_UNIT"],driver.State.PlayerZones["P1"].MainDeck);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ASeenUnitMayStayInTheOriginalEffectOrRemainBanishedAfterDecliningPlay(bool banish)
    {
        var initial=Position();var driver=Driver(initial,banish?"banish-decline-play":"keep-put-hand");
        await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();await Decide(driver,banish);
        var c=driver.State.PendingCardChoice!;
        await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[banish?"END_PREFIX":"NOCT"]));
        if(banish){var p=driver.State.PendingEffectPlay!;await driver.Act("P1",new ChooseCardsCommand(p.ChoiceId,"EFFECT_PLAY",[]));Assert.Contains("NOCT",driver.State.PlayerZones["P1"].Banished);}
        else Assert.Contains("NOCT",driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("NOCT",driver.State.PlayerZones["P1"].Base);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("permission")] [InlineData("generation")] [InlineData("cost")] [InlineData("actor")]
    [InlineData("unseen")] [InlineData("parent-kind")] [InlineData("duplicate")]
    public async Task RecoveryRejectsForgedObservedDeckHistoryAndPlayPermission(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();await Decide(driver,true);
        var choice=driver.State.PendingCardChoice!;await driver.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["END_PREFIX"]));
        var state=driver.State;var p=state.PendingEffectPlay!;var c=p.Parent.ObservedPlayPermission!.Context;
        if(mutation=="permission")p=p with {IgnoreAllCosts=true};
        if(mutation=="generation")c=c with {Card=c.Card with {Generation=99}};
        if(mutation=="cost")c=c with {PowerCost=0};
        if(mutation=="actor")c=c with {Instruction=c.Instruction with {PlayerId="P2"}};
        if(mutation=="unseen")c=c with {Instruction=c.Instruction with {Seen=[new("END_PREFIX",0)]}};
        if(mutation=="parent-kind")c=c with {Instruction=c.Instruction with {Parent=c.Instruction.Parent with {EffectKind="FORGED-LOOK"}}};
        if(mutation=="duplicate")c=c with {Instruction=c.Instruction with {Seen=[new("NOCT",0),new("NOCT",0)]}};
        p=p with {Parent=p.Parent with {ObservedPlayPermission=p.Parent.ObservedPlayPermission! with {Context=c}}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with {PendingEffectPlay=p}),e=>e.Contains("observed deck"));
    }

    [Fact]
    public async Task InsightWithItsOnlyCardBanishedCompletesBeforeTheUnitsOwnPayment()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"INSIGHT","UNL-063/219","P1");
        var driver=Driver(initial,"insight-banish-only");await driver.Act("P1",new PlayCardCommand("INSIGHT","UNL-063/219",["HOST"]));await driver.Top();
        await Decide(driver,true);Assert.DoesNotContain("END_PREFIX",driver.State.PlayerZones["P1"].Hand);
        Assert.Null(driver.State.PendingCardChoice);Assert.NotNull(driver.State.PendingEffectPlay);
        await driver.Act("P1",new PlayCardCommand("NOCT",Nocturne,[]));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARealPredictUnitLooksOnItsAbilityAndDoesNotChooseADeckCardAtDeclaration()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"PREDICT","OGN·171/298","P1");
        var driver=Driver(initial,"predict-unit");await driver.Act("P1",new PlayCardCommand("PREDICT","OGN·171/298",[]));
        Assert.Null(driver.State.PendingRuleChoice);Assert.Contains("PREDICT",driver.State.PlayerZones["P1"].Base);
        await driver.Top();await Decide(driver,true);Assert.Null(driver.State.PendingCardChoice);
        await driver.Act("P1",new PlayCardCommand("NOCT",Nocturne,[]));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task OwnPlayPermissionUsesAnyRuneDomainInsteadOfThePrintedDomain()
    {
        var initial=Position();initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P1"]=new(1,0)},
            CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["RED_RUNE"]=new("RED_RUNE",isExhausted:true,cardNo:"OGN·007/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.RuneCard,"COLOR:red"])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["RED_RUNE"]=new("P1","BASE")},
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {Base=initial.PlayerZones["P1"].Base.Append("RED_RUNE").ToArray()}}};
        var driver=Driver(initial,"off-domain-rune-payment");await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();await Decide(driver,true);
        var c=driver.State.PendingCardChoice!;await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["END_PREFIX"]));
        await driver.Act("P1",new RecycleRuneCommand("RED_RUNE"));await driver.Act("P1",new PlayCardCommand("NOCT",Nocturne,[]));
        Assert.Equal(RunePool.Empty,driver.State.RunePools["P1"]);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AStaticBanishChoiceDoesNotRevealPrivateLookedCardsToTheOtherPlayer()
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();
        var prompts=ResolutionResult.BuildPrompts(driver.State);Assert.Contains("魔腾",prompts["P1"].Reason);
        Assert.DoesNotContain("NOCT",System.Text.Json.JsonSerializer.Serialize(prompts["P2"]));
        Assert.DoesNotContain("NOCT",System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        Assert.DoesNotContain("NOCT",System.Text.Json.JsonSerializer.Serialize(driver.Events().Select(e=>e.Event.Payload)));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task BlindFuryAllowsTheOwnerToInterceptBeforeTheOpponentsPlayPermission(bool banish)
    {
        var initial=DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BlindFury),"P2_TOP",Nocturne);
        initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P2"]=new(0,1)}};
        var driver=Driver(initial,banish?"blind-fury-own-banish":"blind-fury-keep");
        await driver.Act("P1",new PlayCardCommand("PARENT",DeckPlaySequenceTests.BlindFury,[]));await driver.Top();
        Assert.Equal("P2",driver.State.PendingRuleChoice!.Request.PlayerId);await Decide(driver,banish);
        if(!banish)await DeckPlaySequenceTests.Select(driver,"P2_TOP");
        var player=banish?"P2":"P1";Assert.Equal(player,driver.State.PendingEffectPlay!.PlayerId);
        await driver.Act(player,new PlayCardCommand("P2_TOP",Nocturne,[]));
        Assert.Equal("P2",driver.State.CardObjects["P2_TOP"].OwnerId);
        Assert.Equal(player,driver.State.CardObjects["P2_TOP"].ControllerId);
        Assert.Contains("P2_TOP",driver.State.PlayerZones[player].Base);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task BrightFutureCompletesBothOriginalChoicesBeforeEitherObservedUnitIsConfirmed()
    {
        var initial=DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BrightFuture),"P1_TOP",Nocturne),"P2_TOP",Nocturne);
        initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P2"]=new(0,1)}};
        var driver=Driver(initial,"bright-future-both-nocturnes");
        await driver.Act("P1",new PlayCardCommand("PARENT",DeckPlaySequenceTests.BrightFuture,[]));await driver.Top();
        Assert.Equal("P1",driver.State.PendingRuleChoice!.Request.PlayerId);await Decide(driver,true);
        var first=Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("P1",first.PlayerId);Assert.Null(driver.State.PendingEffectPlay);
        await driver.Act(first.PlayerId,new ChooseCardsCommand(first.ChoiceId,first.ChoiceWindow,[first.LegalObjectIds.First()]));
        Assert.Equal("P2",driver.State.PendingRuleChoice!.Request.PlayerId);await Decide(driver,true);
        while(driver.State.PendingCardChoice is { } choice)
        {
            Assert.Null(driver.State.PendingEffectPlay);
            await driver.Act(choice.PlayerId,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[choice.LegalObjectIds.First()]));
        }
        Assert.Contains("PARENT",driver.State.PlayerZones["P1"].Graveyard);
        foreach(var player in new[]{"P1","P2"})
        {
            Assert.Equal(player,driver.State.PendingEffectPlay!.PlayerId);
            await driver.Act(player,new PlayCardCommand(player+"_TOP",Nocturne,[]));
            Assert.Contains(player+"_TOP",driver.State.PlayerZones[player].Base);
        }
        while(driver.State.PendingEffectPlay is { } original)
        {
            var id=original.Sources.Keys.Single();var card=driver.State.CardObjects[id];
            await driver.Act(original.PlayerId,new PlayCardCommand(id,card.CardNo!,[]));
        }
        Assert.Null(driver.State.PendingEffectPlay);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task DianaCanObserveTheSameKeptUnitAgainWhenTheRemainingInstructionRevealsIt()
    {
        var initial=WithNocturne(OfficialInsightSourceTests.Diana(),"D1","P2");
        var driver=Driver(initial,"diana-look-then-reveal");
        await driver.Act("P1",new MoveUnitCommand("UNIT",Destination:"BATTLEFIELD:HILL",SourceObjectIds:["UNIT"]));
        var p=driver.State.PendingPayment!;await driver.Act("P2",new PayCostCommand(p.PaymentId,p.PaymentWindow,["PAY"]));
        await driver.Top();Assert.False(driver.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.Public);
        await Decide(driver,false);var c=driver.State.PendingCardChoice!;
        await driver.Act("P2",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[]));
        Assert.True(driver.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.Public);
        await Decide(driver,true);Assert.Empty(driver.State.PlayerZones["P2"].Hand);
        await driver.Act("P2",new PlayCardCommand("D1",Nocturne,[]));
        Assert.Equal("P1",driver.State.FocusPlayerId);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ALastBreathInsightUsesTheCapturedAbilityAfterRemovingOneOfItsTwoSeenCards()
    {
        var initial=WithNocturne(OfficialInsightTriggerTests.Visionary(),"D1","P1") with {Tick=0};
        var driver=Driver(initial,"last-breath-insight-two");
        await driver.Act("P1",new ActivateAbilityCommand("MALZ",P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,["V"]));
        await driver.Top();await Decide(driver,true);
        var c=driver.State.PendingCardChoice!;Assert.Equal(["D2"],c.ContextObjectIds);
        await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,[]));
        await driver.Act("P1",new PlayCardCommand("D1",Nocturne,[]));
        Assert.Equal(["D2","D3"],driver.State.PlayerZones["P1"].MainDeck);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task OrdinaryDrawingDoesNotOfferTheUnitsDeckObservationPermission()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"DRAW","OGN·083/298","P1");
        var driver=Driver(initial,"ordinary-draw");await driver.Act("P1",new PlayCardCommand("DRAW","OGN·083/298",[]));await driver.Top();
        Assert.Null(driver.State.PendingRuleChoice);Assert.Null(driver.State.PendingEffectPlay);
        Assert.Contains("NOCT",driver.State.PlayerZones["P1"].Hand);Assert.Null(driver.State.ObservedDeckPlays);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task RecoveryRejectsAnObservedPermissionInjectedIntoAnOrdinaryStackSlot()
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();await Decide(driver,true);
        var c=driver.State.PendingCardChoice!;await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["END_PREFIX"]));
        var state=driver.State;var slot=state.StackItems.Single();
        var forged=slot with {ObservedPlayPermission=state.PendingEffectPlay!.Parent.ObservedPlayPermission};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with {StackItems=[forged]}),e=>e.Contains("observed deck pending slot"));
    }

    [Fact]
    public async Task HookKeepsItsKilledMightLimitSeparateFromTheObservedUnitsOwnPermission()
    {
        var initial=WithNocturne(HookDeckPlayTests.Position(),"HOOK_TOP","P1");var driver=Driver(initial,"hook-own-permission");
        await HookDeckPlayTests.Activate(driver);await driver.Top();await Decide(driver,true);
        Assert.NotNull(driver.State.PendingEffectPlay);Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(4,driver.State.CardObjects["HOOK_TOP"].Power);
        await driver.Act("P1",new PlayCardCommand("HOOK_TOP",Nocturne,[]));
        Assert.Contains("HOST",driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AttackRevealCompletesItsOriginalOptionalChoiceBeforeTheObservedUnitPlays()
    {
        var initial=WithNocturne(AttackDeckPlayTests.Position(),"ATK_TOP","P1");var driver=Driver(initial,"attack-own-permission");
        await AttackDeckPlayTests.Attack(driver);await driver.Top();await AttackDeckPlayTests.Reveal(driver);await Decide(driver,true);
        if(driver.State.PendingCardChoice is { } choice)
            await driver.Act(choice.PlayerId,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        Assert.Equal("P1",driver.State.PendingEffectPlay!.PlayerId);
        await driver.Act("P1",new PlayCardCommand("ATK_TOP",Nocturne,[]));
        Assert.Contains("ATK_TOP",driver.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AConquestLegendCanObserveAUnitWithoutReplacingItsOwnExhaustionCost()
    {
        var initial=WithNocturne(OfficialLegendDeckPlayTests.Position(),"DECK0","P1");var driver=Driver(initial,"conquest-legend");
        await driver.Act("P1",new MoveUnitCommand("UNIT","BASE","BATTLEFIELD:BF",[]));
        await driver.Act("P1",new PassFocusCommand());await driver.Act("P2",new PassFocusCommand());
        var cost=driver.State.PendingCardChoice!;
        await driver.Act("P1",new ChooseCardsCommand(cost.ChoiceId,cost.ChoiceWindow,["LEGEND"]));
        await driver.Top();await Decide(driver,true);Assert.True(driver.State.CardObjects["LEGEND"].IsExhausted);
        var choice=driver.State.PendingCardChoice!;await driver.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        await driver.Act("P1",new PlayCardCommand("DECK0",Nocturne,[]));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AHeldEquipmentLookCanBanishAnIneligibleUnitBeforeCompletingTheOriginalFilter()
    {
        var initial=WithNocturne(OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·294/298"),"SOURCE","SFD·058/221","F","P1"),"D0","P1");
        initial=initial with {Phase=MatchPhases.Main,TimingState=TimingStates.NeutralOpen,TurnPlayerId="P2",ActivePlayerId="P2",
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {Base=["R1"],RuneDeck=["R2","R3"]}},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["R1"]=new("P1","BASE")}};
        var driver=Driver(initial,"held-equipment-look");await driver.Act("P2",new EndTurnCommand());
        await driver.Top();await Decide(driver,true);
        if(driver.State.PendingCardChoice is { } choice)
            await driver.Act(choice.PlayerId,new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        await driver.Act("P1",new RecycleRuneCommand("R1"));await driver.Act("P1",new PlayCardCommand("D0",Nocturne,[]));
        Assert.Contains("D0",driver.State.PlayerZones["P1"].Base);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task TeemoCountsTheOriginalRevealAndRecyclesOnlyCardsStillInTheDeck()
    {
        var initial=WithNocturne(StandbyDefendTeemoTests.BuildState("OGN·121/298"),"P2-MAIN-NON-STANDBY-001","P2") with {Tick=0};
        initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P2"]=new(0,1)}};
        var driver=Driver(initial,"teemo-counted-reveal");
        await driver.Act("P1",new DeclareBattleCommand("P1-TEEMO-DEFEND-BATTLEFIELD",["P1-TEEMO-DEFEND-ATTACKER"],["P2-TEEMO-FACE-UP-DEFENDER"],["COMBAT_ASSIGNMENT"]));
        var c=driver.State.PendingCardChoice!;await driver.Act("P2",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["P1-TEEMO-DEFEND-ATTACKER"]));
        await driver.Top();await Decide(driver,true);
        var reveal=Assert.Single(driver.Events(),e=>e.Event.Kind=="MAIN_DECK_CARDS_REVEALED");Assert.Equal(5,reveal.Event.Payload["count"]);
        Assert.Equal(2,reveal.Event.Payload["damageAmount"]);Assert.Equal(4,Assert.Single(driver.Events(),e=>e.Event.Kind=="CARDS_RECYCLED").Event.Payload["count"]);
        await driver.Act("P2",new PlayCardCommand("P2-MAIN-NON-STANDBY-001",Nocturne,[]));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ADefendedBattlefieldDoesNotRecycleTheUnitAlreadyBanishedByItsOwnStaticAbility()
    {
        var initial=WithNocturne(StandbyDefendTeemoTests.BuildState("OGN·009/298",battlefieldCardNo:"SFD·215/221",battlefieldControllerId:"P2"),"P2-MAIN-STANDBY-001","P2") with {Tick=0};
        initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P2"]=new(0,1)}};
        var driver=Driver(initial,"battlefield-defend-reveal");
        await driver.Act("P1",new DeclareBattleCommand("P1-TEEMO-DEFEND-BATTLEFIELD",["P1-TEEMO-DEFEND-ATTACKER"],["P2-TEEMO-FACE-UP-DEFENDER"],["COMBAT_ASSIGNMENT"]));
        await Decide(driver,true);Assert.NotNull(driver.State.PendingEffectPlay);
        await driver.Act("P2",new PlayCardCommand("P2-MAIN-STANDBY-001",Nocturne,[]));
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARDS_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AFilteredSelectionRevealsTheKeptUnitBeforeDrawingAndOffersItsPermissionAgain()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"FILTER","UNL-032/219","P1");var driver=Driver(initial,"filtered-second-reveal");
        await driver.Act("P1",new PlayCardCommand("FILTER","UNL-032/219",[]));await driver.Top();await Decide(driver,false);
        var c=driver.State.PendingCardChoice!;await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["NOCT"]));
        Assert.True(driver.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.Public);await Decide(driver,true);
        Assert.DoesNotContain("NOCT",driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARD_DRAWN");
        await driver.Act("P1",new PlayCardCommand("NOCT",Nocturne,[]));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SurrenderDoesNotStartAnotherObservedConfirmation(bool beforeOriginalChoiceCompletes)
    {
        var initial=Position();var driver=Driver(initial,beforeOriginalChoiceCompletes?"surrender-during-look":"surrender-during-permission");
        await driver.Act("P1",new PlayCardCommand("TRICK","OGN·183/298",[]));await driver.Top();
        if(!beforeOriginalChoiceCompletes)
        {
            await Decide(driver,true);var c=driver.State.PendingCardChoice!;
            await driver.Act("P1",new ChooseCardsCommand(c.ChoiceId,c.ChoiceWindow,["END_PREFIX"]));
        }
        var waiting=ResolutionResult.BuildPrompts(driver.State)["P2"];
        Assert.False(waiting.Actionable);Assert.Contains(waiting.Candidates!,c=>c.Action==CommandTypes.Surrender && c.Enabled);
        await driver.Act("P2",new SurrenderCommand());Assert.Equal(MatchStatuses.Finished,driver.State.Status);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE");
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    internal static MatchState WithNocturne(MatchState state,string id,string owner)
        => state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
            [id]=new(id,cardNo:Nocturne,power:4,manaCost:4,ownerId:owner,controllerId:owner,tags:[CardObjectTags.UnitCard,"游走"])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){[id]=new(owner,"MAIN_DECK")}};

    internal static async Task Decide(StandbyDefendObserverTests.Driver driver,bool banish)
    {
        var c=Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        await driver.Act(c.Request.PlayerId,new PayCostCommand(c.Request.Id,"RULE_REPLACEMENT",[banish?"BANISH":"KEEP"]));
    }

    internal static MatchState Position()
    {
        var state=AttackDamageDistributionTests.Spell(EndDeckPlayTests.Position(),"TRICK","OGN·183/298","P1");
        return state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
            ["NOCT"]=new("NOCT",cardNo:Nocturne,power:4,manaCost:4,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard,"游走"])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["NOCT"]=new("P1","MAIN_DECK")},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {MainDeck=["NOCT","END_PREFIX","END_UNIT"]}}};
    }
    internal static StandbyDefendObserverTests.Driver Driver(MatchState state,string branch)
    {
        var path=Environment.GetEnvironmentVariable("RIFTBOUND_DECK_OBSERVATION_EVIDENCE");
        return new(state,string.IsNullOrWhiteSpace(path)?null:Path.Combine(path,branch));
    }
}
