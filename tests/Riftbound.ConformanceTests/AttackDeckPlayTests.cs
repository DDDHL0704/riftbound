using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AttackDeckPlayTests
{
    internal const string Queen = "SFD·170/221";

    [Theory]
    [InlineData(Queen)]
    [InlineData("SFD·170a/221")]
    public async Task ActualAttackCapturesTheAbilityAndWaitsUntilResolutionBeforeTheOptionalReveal(string card)
    {
        var initial=Position(card);var driver=Driver(initial,"attack-"+card.Replace('·','-').Replace('/','-'));
        await Attack(driver);
        Assert.True(driver.State.CardObjects["HOST"].IsAttacking);
        Assert.Single(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARDS_REVEALED");
        await driver.Top();
        var choice=Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("P1",choice.Request.PlayerId);Assert.Equal("DECK_REVEAL",choice.Request.Kind);
        await driver.Act("P1",new PayCostCommand(choice.Request.Id,"RULE_REPLACEMENT",["REVEAL"]));
        Assert.Equal(["ATK_TOP","ATK_OTHER"],driver.State.PendingCardChoice!.ContextObjectIds);
        await DeckPlaySequenceTests.Select(driver,"ATK_TOP");
        Assert.Equal("STACK",driver.State.ObjectLocations["ATK_TOP"].Zone);
        var mana=driver.State.RunePools["P1"].Mana;
        await driver.Act("P1",new PlayCardCommand("ATK_TOP","OGN·096/298",[],Destination:"BATTLEFIELD:F"));
        Assert.Equal(mana-2,driver.State.RunePools["P1"].Mana);
        Assert.Equal("F",driver.State.ObjectLocations["ATK_TOP"].BattlefieldObjectId);Assert.True(driver.State.CardObjects["ATK_TOP"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheControllerMaySkipRevealOrDeclineBanishmentWithoutInventingAPlay(bool reveal)
    {
        var initial=Position();var driver=Driver(initial,reveal?"reveal-decline-banish":"skip-reveal");await Attack(driver);await driver.Top();
        var choice=driver.State.PendingRuleChoice!;await driver.Act("P1",new PayCostCommand(choice.Request.Id,"RULE_REPLACEMENT",[reveal?"REVEAL":"SKIP"]));
        if(reveal){var cardChoice=driver.State.PendingCardChoice!;await driver.Act("P1",new ChooseCardsCommand(cardChoice.ChoiceId,cardChoice.ChoiceWindow,[]));}
        Assert.Empty(driver.State.PlayerZones["P1"].Banished);Assert.Null(driver.State.PendingEffectPlay);
        Assert.Equal(reveal,driver.Events().Any(e=>e.Event.Kind=="CARDS_REVEALED"));
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind is "CARD_BANISHED" or "UNIT_PLAYED_TO_BASE");
        if(!reveal)Assert.Equal(["ATK_TOP","ATK_OTHER"],driver.State.PlayerZones["P1"].MainDeck.Take(2));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("SFD·042/221",false)]
    [InlineData("OGN·048/298",true)]
    public async Task AttackCanChooseEquipmentOrANormalSpellAndStillPaysTheirPrintedCosts(string card,bool spell)
    {
        var initial=DeckPlaySequenceTests.TopCard(Position(),"ATK_TOP",card);var driver=Driver(initial,"attack-"+card.Replace('·','-').Replace('/','-'));
        await Attack(driver);await driver.Top();await Reveal(driver);await DeckPlaySequenceTests.Select(driver,"ATK_TOP");
        var mana=driver.State.RunePools["P1"].Mana;
        var forbidden=await new CoreRuleEngine().ResolveAsync(driver.State,new("no-gear-here","P1",CommandTypes.PlayCard),
            new PlayCardCommand("ATK_TOP",card,[],Destination:"BATTLEFIELD:F"),default);
        Assert.False(forbidden.Accepted);Assert.Equal(MatchStateHasher.Hash(driver.State),MatchStateHasher.Hash(forbidden.State));
        await driver.Act("P1",new PlayCardCommand("ATK_TOP",card,[]));Assert.Equal(mana-2,driver.State.RunePools["P1"].Mana);
        if(spell){Assert.NotEmpty(driver.State.StackItems);await driver.Top();Assert.Contains("ATK_TOP",driver.State.PlayerZones["P1"].Graveyard);}
        else Assert.Contains("ATK_TOP",driver.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARealReturnSpellResponseKeepsTheCapturedAbilityButRemovesDepartedSourcesHerePermission()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"RETURN","UNL-128/219","P2");var driver=Driver(initial,"source-returned");
        await Attack(driver);await driver.Act("P1",new PassPriorityCommand());await driver.Act("P2",new PlayCardCommand("RETURN","UNL-128/219",["ENEMY1","HOST"]));await driver.Top();
        Assert.Contains("HOST",driver.State.PlayerZones["P1"].Hand);Assert.Single(driver.State.StackItems);
        await driver.Top();await Reveal(driver);AssertPrivateDepartedSource(driver.State);await DeckPlaySequenceTests.Select(driver,"ATK_TOP");AssertPrivateDepartedSource(driver.State);
        var bad=await new CoreRuleEngine().ResolveAsync(driver.State,new("lost-here","P1",CommandTypes.PlayCard),
            new PlayCardCommand("ATK_TOP","OGN·096/298",[],Destination:"BATTLEFIELD:F"),default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(driver.State),MatchStateHasher.Hash(bad.State));
        await driver.Act("P1",new PlayCardCommand("ATK_TOP","OGN·096/298",[]));
        Assert.Contains("ATK_TOP",driver.State.PlayerZones["P1"].Base);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task RealShadyAttachmentCopiesTheAttackAbilityAndKeepsItAfterTheEquipmentIsRemoved()
    {
        var initial=Position();initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){
            ["HOST"]=initial.CardObjects["HOST"] with {CardNo="OGN·096/298",Power=1},
            ["GEAR"]=initial.CardObjects["GEAR"] with {CardNo="VEN·137"}}};
        initial=DeckPlaySequenceTests.TopCard(initial,"ATK_OTHER","OGN·096/298");
        initial=OfficialHoldSequenceTests.AddUnit(initial,"COPY-SOURCE",Queen,"F","P1");
        initial=LocalDestructionRecallTests.At(initial,"COPY-SOURCE","P1","BASE");
        initial=AttackDamageDistributionTests.Spell(initial,"DETACH","SFD·011/221","P1");
        var driver=Driver(initial,"real-copied-attack");
        await driver.Act("P1",new AssembleEquipmentCommand("GEAR","HOST",["ASSEMBLE_YELLOW"]));await driver.Top();
        var copy=driver.State.PendingRuleChoice!;await driver.Act("P1",new PayCostCommand(copy.Request.Id,"RULE_REPLACEMENT",["COPY:COPY-SOURCE"]));
        Assert.Equal(Queen,driver.State.CardObjects["HOST"].CardNo);
        await Attack(driver);Assert.NotNull(Assert.Single(driver.State.StackItems).FieldContext!.Origin);
        await driver.Act("P1",new PlayCardCommand("DETACH","SFD·011/221",["HOST","GEAR"]));await driver.Top();
        Assert.Equal("OGN·096/298",driver.State.CardObjects["HOST"].CardNo);
        Assert.Contains("ATK_TOP",driver.State.PlayerZones["P1"].Hand);
        await driver.Top();await Reveal(driver);await DeckPlaySequenceTests.Select(driver,"ATK_OTHER");
        await driver.Act("P1",new PlayCardCommand("ATK_OTHER","OGN·096/298",[],Destination:"BATTLEFIELD:F"));
        Assert.Equal("F",driver.State.ObjectLocations["ATK_OTHER"].BattlefieldObjectId);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task EmptyDeckRevealFinishesWithoutDrawingBurnoutOrAnInventedCardChoice()
    {
        var initial=Position();var deck=initial.PlayerZones["P1"].MainDeck.ToHashSet();
        initial=initial with { CardObjects=initial.CardObjects.Where(p=>!deck.Contains(p.Key)).ToDictionary(),
            ObjectLocations=initial.ObjectLocations.Where(p=>!deck.Contains(p.Key)).ToDictionary(),
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P1"]=initial.PlayerZones["P1"] with {MainDeck=[]} } };
        var driver=Driver(initial,"empty-deck");await Attack(driver);await driver.Top();await Reveal(driver);
        Assert.Null(driver.State.PendingCardChoice);Assert.Null(driver.State.PendingEffectPlay);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind.Contains("BURNOUT")||e.Event.Kind=="CARD_DRAWN");
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task UnpayablePrintedPowerReturnsTheSelectedRealStackCardToItsOwnersBanishment()
    {
        var initial=DeckPlaySequenceTests.TopCard(Position(),"ATK_TOP","UNL-137/219");
        initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P1"]=new(20,0)}};
        var driver=Driver(initial,"unpayable-power");await Attack(driver);await driver.Top();await Reveal(driver);await DeckPlaySequenceTests.Select(driver,"ATK_TOP");
        var before=MatchStateHasher.Hash(driver.State);
        var bad=await new CoreRuleEngine().ResolveAsync(driver.State,new("reject-power","P1",CommandTypes.PlayCard),new PlayCardCommand("ATK_TOP","UNL-137/219",[]),default);
        Assert.False(bad.Accepted);Assert.Equal(before,MatchStateHasher.Hash(bad.State));
        var pending=driver.State.PendingEffectPlay!;await driver.Act("P1",new ChooseCardsCommand(pending.ChoiceId,"EFFECT_PLAY",[]));
        Assert.Contains("ATK_TOP",driver.State.PlayerZones["P1"].Banished);Assert.Null(driver.State.PendingEffectPlay);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AnActuallyCreatedCopiedSandSoldierKeepsTheCapturedAttackAfterItCeasesToExist()
    {
        var initial=Position();initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["GEAR"]=initial.CardObjects["GEAR"] with {CardNo="VEN·137"}}};
        initial=OfficialHoldSequenceTests.AddUnit(initial,"COPY-SOURCE",Queen,"F","P1");initial=LocalDestructionRecallTests.At(initial,"COPY-SOURCE","P1","BASE");
        initial=AttackDamageDistributionTests.Spell(initial,"SANDCRAFT","SFD·031/221","P1");
        initial=AttackDamageDistributionTests.Spell(initial,"RETURN","UNL-128/219","P2");
        for(var i=0;i<4;i++)
        {
            var id="TOKEN-RESPONSE-RUNE-"+i;
            initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){[id]=new(id,cardNo:"UNL-R05",ownerId:"P2",controllerId:"P2",tags:[CardObjectTags.RuneCard,"COLOR:purple"])},
                ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){[id]=new("P2","BASE")},
                PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P2"]=initial.PlayerZones["P2"] with {Base=initial.PlayerZones["P2"].Base.Append(id).ToArray()}}};
        }
        for(var i=0;i<2;i++)
        {
            var id="TOKEN-PLAY-RUNE-"+i;
            initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){[id]=new(id,cardNo:"UNL-R05",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.RuneCard,"COLOR:purple"])},
                ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){[id]=new("P1","BASE")},
                PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {Base=initial.PlayerZones["P1"].Base.Append(id).ToArray()}}};
        }
        foreach(var player in initial.Seats.Keys)
            for(var i=0;i<8;i++)
            {
                var id="TOKEN-ATTACK-DECK-"+player+"-"+i;
                initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){[id]=new(id,cardNo:"OGN·096/298",ownerId:player,controllerId:player,tags:[CardObjectTags.UnitCard])},
                    ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){[id]=new(player,"MAIN_DECK")},
                    PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){[player]=initial.PlayerZones[player] with {MainDeck=initial.PlayerZones[player].MainDeck.Append(id).ToArray()}}};
            }
        foreach(var id in initial.PlayerZones["P1"].MainDeck)initial=DeckPlaySequenceTests.TopCard(initial,id,"OGN·096/298");
        var driver=Driver(initial,"copied-token-ceased");await driver.Act("P1",new PlayCardCommand("SANDCRAFT","SFD·031/221",[]));await driver.Top();
        var host=Assert.Single(driver.State.CardObjects.Values,c=>c.TokenFactoryCardNo==P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        await driver.Act("P1",new AssembleEquipmentCommand("GEAR",host,["ASSEMBLE_YELLOW"]));await driver.Top();
        var copy=driver.State.PendingRuleChoice!;await driver.Act("P1",new PayCostCommand(copy.Request.Id,"RULE_REPLACEMENT",["COPY:COPY-SOURCE"]));
        await driver.Act("P1",new EndTurnCommand());await driver.Act("P2",new EndTurnCommand());
        await Attack(driver,host);await driver.Act("P1",new PassPriorityCommand());
        for(var i=0;i<4;i++)await driver.Act("P2",new TapRuneCommand("TOKEN-RESPONSE-RUNE-"+i));
        await driver.Act("P2",new RecycleRuneCommand("TOKEN-RESPONSE-RUNE-0"));
        await driver.Act("P2",new PlayCardCommand("RETURN","UNL-128/219",["ENEMY1",host]));await driver.Top();
        Assert.False(driver.State.CardObjects.ContainsKey(host));var captured=Assert.Single(driver.State.StackItems);Assert.True(captured.FieldContext!.SourceCeasedToExist);
        foreach(var corrupt in new[]{captured.FieldContext with {SourceCeasedToExist=false},captured.FieldContext with {SourceGeneration=999},captured.FieldContext with {SourceTokenFactoryCardNo="FORGED-TOKEN"}})
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {StackItems=[captured with {FieldContext=corrupt}]}),e=>e.Contains("field trigger"));
        await driver.Top();await Reveal(driver);var choice=Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        var selected=choice.LegalObjectIds.First(id=>driver.State.CardObjects[id].CardNo=="OGN·096/298");await DeckPlaySequenceTests.Select(driver,selected);
        var bad=await new CoreRuleEngine().ResolveAsync(driver.State,new("ceased-no-here","P1",CommandTypes.PlayCard),new PlayCardCommand(selected,"OGN·096/298",[],Destination:"BATTLEFIELD:F"),default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(driver.State),MatchStateHasher.Hash(bad.State));
        for(var i=0;i<2;i++)await driver.Act("P1",new TapRuneCommand("TOKEN-PLAY-RUNE-"+i));
        await driver.Act("P1",new PlayCardCommand(selected,"OGN·096/298",[]));
        Assert.Contains(selected,driver.State.PlayerZones["P1"].Base);
        Assert.Null(driver.State.PendingEffectPlay);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("options")]
    [InlineData("transcript")]
    [InlineData("preview")]
    public async Task RecoveryRejectsForgedOptionalRevealBeforeAnyDeckInformationIsExposed(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await Attack(driver);await driver.Top();var p=driver.State.PendingRuleChoice!;
        var bad=mutation switch {"actor"=>p with {Request=p.Request with {PlayerId="P2"}},
            "options"=>p with {Request=p.Request with {Options=[new("SKIP","forged")] }},
            "transcript"=>p with {Answers=[new("forged","REVEAL")]},
            _=>p with {Request=p.Request with {Preview=p.Origin with {Tick=p.Origin.Tick+1}}}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {PendingRuleChoice=bad}),e=>e.Contains("replacement choice",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARDS_REVEALED");
    }

    [Fact]
    public async Task ARealReactionMovingTheSourceBackToBaseRemovesTheOldBattlefieldsHerePermission()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"FLASH","OGS·011/024","P1");var driver=Driver(initial,"source-moved-base");
        await Attack(driver);var generation=driver.State.CardObjects["HOST"].ObjectGeneration;
        await driver.Act("P1",new PlayCardCommand("FLASH","OGS·011/024",["HOST"]));await driver.Top();
        Assert.Equal("BASE",driver.State.ObjectLocations["HOST"].Zone);Assert.Equal(generation,driver.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(new ObjectLocationState("P1","BASE"),Assert.Single(driver.State.StackItems).FieldContext!.SourceLocation);
        await driver.Top();await Reveal(driver);await DeckPlaySequenceTests.Select(driver,"ATK_TOP");
        var bad=await new CoreRuleEngine().ResolveAsync(driver.State,new("old-here","P1",CommandTypes.PlayCard),new PlayCardCommand("ATK_TOP","OGN·096/298",[],Destination:"BATTLEFIELD:F"),default);
        Assert.False(bad.Accepted);Assert.Equal(MatchStateHasher.Hash(driver.State),MatchStateHasher.Hash(bad.State));
        await driver.Act("P1",new PlayCardCommand("ATK_TOP","OGN·096/298",[]));Assert.Contains("ATK_TOP",driver.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("source-generation")]
    [InlineData("source-location")]
    [InlineData("card")]
    [InlineData("destination-policy")]
    [InlineData("ignore-power")]
    public async Task RecoveryRejectsForgedCapturedAttackAndPendingPlayPermissions(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await Attack(driver);await driver.Top();await Reveal(driver);await DeckPlaySequenceTests.Select(driver,"ATK_TOP");
        var pending=driver.State.PendingEffectPlay!;var field=pending.Parent.FieldContext!;
        var bad=mutation switch {
            "source-generation"=>pending with {Parent=pending.Parent with {FieldContext=field with {SourceGeneration=999}}},
            "source-location"=>pending with {Parent=pending.Parent with {FieldContext=field with {SourceLocation=new("P1","BASE")}}},
            "card"=>pending with {Parent=pending.Parent with {FieldContext=field with {CardNo="OGN·096/298"}}},
            "destination-policy"=>pending with {DestinationPolicy="ANY"},
            _=>pending with {IgnoreBasePower=true}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {PendingEffectPlay=bad}),e=>e.Contains("deck"));
    }

    private static void AssertPrivateDepartedSource(MatchState state)
    {
        var key=state.PendingCardChoice is not null?"pendingCardChoice":"pendingEffectPlay";
        var snapshots=ResolutionResult.BuildSnapshots(state);var own=System.Text.Json.JsonSerializer.SerializeToElement(snapshots["P1"].Timing);
        var opponent=System.Text.Json.JsonSerializer.SerializeToElement(snapshots["P2"].Timing);
        Assert.Equal("HOST",own.GetProperty(key).GetProperty("sourceObjectId").GetString());
        Assert.Equal("HIDDEN",opponent.GetProperty(key).GetProperty("sourceObjectId").GetString());
        var privacy=new ResolutionResult(true,null,state,[],snapshots,ResolutionResult.BuildPrompts(state));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(privacy);
        var forged=snapshots.ToDictionary(p=>p.Key,p=>p.Value);
        var envelope=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,object?>>(System.Text.Json.JsonSerializer.Serialize(snapshots["P2"].Timing[key]))!;
        envelope["sourceObjectId"]="HOST";
        forged["P2"]=snapshots["P2"] with {Timing=new Dictionary<string,object?>(snapshots["P2"].Timing){[key]=envelope}};
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(()=>FullGameEndToEndTests.AssertNoHiddenZoneLeak(privacy with {Snapshots=forged}));
    }

    internal static async Task Reveal(StandbyDefendObserverTests.Driver driver)
    {
        var choice=driver.State.PendingRuleChoice!;
        await driver.Act(choice.Request.PlayerId,new PayCostCommand(choice.Request.Id,"RULE_REPLACEMENT",["REVEAL"]));
    }

    internal static async Task Attack(StandbyDefendObserverTests.Driver driver,string host="HOST")
    {
        await driver.Act("P1",new MoveUnitCommand(host,"BASE","BATTLEFIELD:F",[]));
        for(var i=0;i<4&&driver.State.FocusPlayerId is not null&&driver.State.StackItems.Count==0;i++)
            await driver.Act(driver.State.FocusPlayerId!,new PassFocusCommand());
        if(ResolutionResult.BuildPrompts(driver.State)["P1"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            await driver.Act("P1",new DeclareBattleCommand("F",["HOST"],["ENEMY1","ENEMY2","ENEMY3"],["COMBAT_ASSIGNMENT"]));
    }

    internal static MatchState Position(string card=Queen)
    {
        var state=AttackDamageDistributionTests.Position(card);
        return state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
            ["HOST"]=state.CardObjects["HOST"] with {Power=5},
            ["ATK_TOP"]=new("ATK_TOP",cardNo:"OGN·096/298",manaCost:2,power:1,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]),
            ["ATK_OTHER"]=new("ATK_OTHER",cardNo:"SFD·042/221",manaCost:2,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["ATK_TOP"]=new("P1","MAIN_DECK"),["ATK_OTHER"]=new("P1","MAIN_DECK")},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {MainDeck=new[]{"ATK_TOP","ATK_OTHER"}.Concat(state.PlayerZones["P1"].MainDeck).ToArray()}}};
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state,string branch)
    {
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_ATTACK_DECK_EVIDENCE");
        return new(state,string.IsNullOrWhiteSpace(root)?null:Path.Combine(root,branch));
    }
}
