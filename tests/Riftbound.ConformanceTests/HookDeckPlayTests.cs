using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HookDeckPlayTests
{
    internal const string Hook="OGN·242/298";

    [Fact]
    public async Task AUniqueEligibleUnitStillRequiresAnOptionalBanishChoiceAndRealFreePlayConfirmation()
    {
        var initial=Position();var driver=Driver(initial,"unique-unit");await Activate(driver);await driver.Top();
        Assert.Contains("HOST",driver.State.PlayerZones["P1"].Graveyard);
        var choice=Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);Assert.Equal("DECK_PLAY_SELECTION",choice.ChoiceWindow);
        Assert.Equal(["HOOK_TOP"],choice.LegalObjectIds);Assert.Equal(0,choice.RequiredCount);
        await DeckPlaySequenceTests.Select(driver,"HOOK_TOP");
        Assert.Equal("STACK",driver.State.ObjectLocations["HOOK_TOP"].Zone);Assert.True(driver.State.PendingEffectPlay!.IgnoreAllCosts);
        var mana=driver.State.RunePools["P1"].Mana;await driver.Act("P1",new PlayCardCommand("HOOK_TOP","OGN·096/298",[]));
        Assert.Equal(mana,driver.State.RunePools["P1"].Mana);Assert.Contains("HOOK_TOP",driver.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task TheControllerCanDeclineTheOnlyEligibleUnitWithoutAutomaticPlay()
    {
        var initial=Position();var driver=Driver(initial,"unique-decline");await Activate(driver);await driver.Top();
        var choice=driver.State.PendingCardChoice!;await driver.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        Assert.Null(driver.State.PendingEffectPlay);Assert.Empty(driver.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE");
        Assert.Equal(2,Assert.Single(driver.Events(),e=>e.Event.Kind=="CARDS_RECYCLED").Event.Payload["count"]);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("OGN·208/298")]
    [InlineData("SFD·044/221")]
    [InlineData("UNL-166/219")]
    public async Task FreePendingPlayIgnoresPrintedManaPowerAndMandatoryAdditionalCosts(string card)
    {
        var initial=DeckPlaySequenceTests.TopCard(Position("SFD·148/221"),"HOOK_TOP",card);
        initial=initial with {RunePools=new Dictionary<string,RunePool>(initial.RunePools){["P1"]=new(1,0,new Dictionary<string,int>{[RuneTrait.Yellow]=1})}};
        var driver=Driver(initial,"free-"+card.Replace('·','-').Replace('/','-'));await Activate(driver);await driver.Top();await DeckPlaySequenceTests.Select(driver,"HOOK_TOP");
        Assert.Equal(RunePool.Empty,driver.State.RunePools["P1"]);Assert.True(driver.State.PendingEffectPlay!.IgnoreAllCosts);
        await driver.Act("P1",new PlayCardCommand("HOOK_TOP",card,[]));Assert.Equal(RunePool.Empty,driver.State.RunePools["P1"]);
        Assert.Contains("HOOK_TOP",driver.State.PlayerZones["P1"].Base);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARealReactionReturningTheTargetStillLooksButSuppliesNoKilledUnitMight()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"RETURN","UNL-128/219","P2");
        initial=OfficialHoldSequenceTests.AddUnit(initial,"P2_TARGET","OGN·001/298","F","P2");initial=LocalDestructionRecallTests.At(initial,"P2_TARGET","P2","BASE");
        var driver=Driver(initial,"target-returned");await Activate(driver);await driver.Act("P1",new PassPriorityCommand());
        await driver.Act("P2",new PlayCardCommand("RETURN","UNL-128/219",["P2_TARGET","HOST"]));await driver.Top();await driver.Top();
        Assert.Contains("HOST",driver.State.PlayerZones["P1"].Hand);Assert.Null(driver.State.PendingCardChoice);Assert.Null(driver.State.PendingEffectPlay);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE");
        Assert.Equal(2,Assert.Single(driver.Events(),e=>e.Event.Kind=="CARDS_RECYCLED").Event.Payload["count"]);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ActualAssembledMightIsCapturedBeforeTheTargetAndItsEquipmentLeaveTheField()
    {
        var initial=DeckPlaySequenceTests.TopCard(AttachedRuleTextTests.AddSong(Position("SFD·125/221"),"BOOST"),"HOOK_TOP","OGN·208/298");
        initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["BOOST"]=initial.CardObjects["BOOST"] with {CardNo="SFD·033/221"}}};
        var driver=Driver(initial,"real-assembled-might");await driver.Act("P1",new AssembleEquipmentCommand("BOOST","HOST",["ASSEMBLE_GREEN"]));await driver.Top();
        await Activate(driver);await driver.Top();Assert.Equal(5,driver.State.StackItems.Last().DestroyDeckPlay!.KilledMight);
        await DeckPlaySequenceTests.Select(driver,"HOOK_TOP");await driver.Act("P1",new PlayCardCommand("HOOK_TOP","OGN·208/298",[]));
        Assert.Contains("HOOK_TOP",driver.State.PlayerZones["P1"].Base);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task AnActuallyCreatedSandSoldierCanBeKilledAndItsMightSurvivesItsCessation()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"SANDCRAFT","SFD·031/221","P1");var driver=Driver(initial,"actual-killed-token");
        await driver.Act("P1",new PlayCardCommand("SANDCRAFT","SFD·031/221",[]));await driver.Top();
        var token=Assert.Single(driver.State.CardObjects.Values,c=>c.TokenFactoryCardNo==P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        await Activate(driver,token);await driver.Top();Assert.False(driver.State.CardObjects.ContainsKey(token));
        Assert.Equal(2,driver.State.StackItems.Last().DestroyDeckPlay!.KilledMight);await DeckPlaySequenceTests.Select(driver,"HOOK_TOP");
        await driver.Act("P1",new PlayCardCommand("HOOK_TOP","OGN·096/298",[]));Assert.Contains("HOOK_TOP",driver.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARealReturnReactionCanCeaseTheDeclaredTokenBeforeTheHookWithoutInventingKilledMight()
    {
        var initial=AttackDamageDistributionTests.Spell(Position(),"SANDCRAFT","SFD·031/221","P1");
        initial=AttackDamageDistributionTests.Spell(initial,"RETURN","UNL-128/219","P2");
        initial=OfficialHoldSequenceTests.AddUnit(initial,"P2_TARGET","OGN·001/298","F","P2");initial=LocalDestructionRecallTests.At(initial,"P2_TARGET","P2","BASE");
        var driver=Driver(initial,"declared-token-returned");await driver.Act("P1",new PlayCardCommand("SANDCRAFT","SFD·031/221",[]));await driver.Top();
        var token=Assert.Single(driver.State.CardObjects.Values,c=>c.TokenFactoryCardNo==P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        await Activate(driver,token);await driver.Act("P1",new PassPriorityCommand());await driver.Act("P2",new PlayCardCommand("RETURN","UNL-128/219",["P2_TARGET",token]));await driver.Top();
        Assert.False(driver.State.CardObjects.ContainsKey(token));await driver.Top();Assert.Null(driver.State.PendingCardChoice);Assert.Null(driver.State.PendingEffectPlay);
        Assert.Equal(2,Assert.Single(driver.Events(),e=>e.Event.Kind=="CARDS_RECYCLED").Event.Payload["count"]);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARealDeathReplacementLeavesNoKilledUnitReferentButStillRecyclesTheLookedCards()
    {
        var initial=Position();initial=LocalDestructionRecallTests.At(initial,"HOST","P1","BATTLEFIELD","F");
        initial=initial with {CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects){["REPLACER"]=new("REPLACER",cardNo:"OGN·077/298",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["REPLACER"]=new("P1","BASE")},
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {Base=initial.PlayerZones["P1"].Base.Append("REPLACER").ToArray()}}};
        var driver=Driver(initial,"real-replaced-death");await Activate(driver);await driver.Top();
        Assert.Contains("REPLACER",driver.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(driver.Events(),e=>e.Event.Kind=="UNIT_RECALLED_TO_BASE" && e.Event.Payload["sourceObjectId"] as string=="REPLACER");
        Assert.Contains("HOST",driver.State.PlayerZones["P1"].Base);Assert.DoesNotContain("HOST",driver.State.PlayerZones["P1"].Graveyard);
        Assert.Null(driver.State.PendingCardChoice);Assert.Null(driver.State.PendingEffectPlay);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE");Assert.Equal(2,Assert.Single(driver.Events(),e=>e.Event.Kind=="CARDS_RECYCLED").Event.Payload["count"]);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task KillingTheOnlyFriendlyUnitRetainsBattlefieldControlThroughTheRealPendingPlay()
    {
        var initial=LocalDestructionRecallTests.At(Position(),"HOST","P1","BATTLEFIELD","F");var driver=Driver(initial,"sole-unit-controlled-field");
        await Activate(driver);await driver.Top();await DeckPlaySequenceTests.Select(driver,"HOOK_TOP");
        Assert.Equal("P1",driver.State.CardObjects["F"].ControllerId);
        await driver.Act("P1",new PlayCardCommand("HOOK_TOP","OGN·096/298",[],Destination:"BATTLEFIELD:F"));
        Assert.Equal("F",driver.State.ObjectLocations["HOOK_TOP"].BattlefieldObjectId);Assert.Equal("P1",driver.State.CardObjects["F"].ControllerId);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Fact]
    public async Task ARealMoonfallResponseMakesTheKilledUnitsMightZeroBeforeSelection()
    {
        var initial=AttackDamageDistributionTests.Spell(Position("SFD·125/221"),"MOONFALL","UNL-063/219","P2");var driver=Driver(initial,"real-zero-might");
        await Activate(driver);await driver.Act("P1",new PassPriorityCommand());await driver.Act("P2",new PlayCardCommand("MOONFALL","UNL-063/219",["HOST"]));await driver.Top();
        if(driver.State.PendingCardChoice is {ChoiceWindow:"INSIGHT"} insight)await driver.Act("P2",new ChooseCardsCommand(insight.ChoiceId,insight.ChoiceWindow,[]));
        await driver.Top();Assert.Equal(0,driver.State.StackItems.Last().DestroyDeckPlay!.KilledMight);
        Assert.Equal(["HOOK_TOP"],driver.State.PendingCardChoice!.LegalObjectIds);await DeckPlaySequenceTests.Select(driver,"HOOK_TOP");
        await driver.Act("P1",new PlayCardCommand("HOOK_TOP","OGN·096/298",[]));await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("source-generation")]
    [InlineData("target-generation")]
    [InlineData("might")]
    [InlineData("missing-kill")]
    [InlineData("private-frame")]
    [InlineData("token-factory")]
    [InlineData("ability")]
    [InlineData("not-prepared")]
    [InlineData("cost")]
    [InlineData("kind")]
    [InlineData("repeat")]
    public async Task RecoveryRejectsForgedKillHistorySelectionAndFreePlayPermission(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position());await Activate(driver);await driver.Top();await DeckPlaySequenceTests.Select(driver,"HOOK_TOP");
        var p=driver.State.PendingEffectPlay!;var context=p.Parent.DestroyDeckPlay!;
        var bad=context;
        if(mutation=="source-generation")bad=context with {Source=context.Source with {Generation=999}};
        if(mutation=="target-generation")bad=context with {Target=context.Target with {Generation=999}};
        if(mutation=="might")bad=context with {KilledMight=99};
        if(mutation=="missing-kill")bad=context with {KilledMight=null,KilledFrame=null};
        if(mutation=="not-prepared")bad=context with {Prepared=false};
        if(mutation=="ability")bad=context with {AbilityId="FORGED-ABILITY"};
        if(mutation=="private-frame")bad=context with {KilledFrame=context.KilledFrame! with {
            PlayerZones=new Dictionary<string,PlayerZones>(context.KilledFrame.PlayerZones){["P1"]=context.KilledFrame.PlayerZones["P1"] with {MainDeck=["HOOK_OTHER"]}}}};
        if(mutation=="token-factory")bad=context with {DeclaredFrame=context.DeclaredFrame with {
            CardObjects=new Dictionary<string,CardObjectState>(context.DeclaredFrame.CardObjects){["HOST"]=context.DeclaredFrame.CardObjects["HOST"] with {TokenFactoryCardNo="FORGED-TOKEN"}}}};
        var parent=p.Parent with {DestroyDeckPlay=bad};
        var forged=p with {Parent=parent,IgnoreAllCosts=mutation=="cost"?false:p.IgnoreAllCosts};
        if(mutation=="kind")forged=forged with {Parent=parent with {DeckPlaySequence=parent.DeckPlaySequence! with {Kind="SINGLE_PUBLIC_PLAY"}}};
        if(mutation=="repeat")forged=forged with {Parent=parent with {EffectRepeatCount=2}};
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {PendingEffectPlay=forged}),e=>e.Contains("deck"));
    }

    internal static async Task Activate(StandbyDefendObserverTests.Driver driver,string target="HOST")
    {
        var ability=Assert.Single(P4ActivatedAbilityCatalog.GetAll(),a=>a.SourceCardNo==Hook&&a.Kind==ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest);
        await driver.Act("P1",new ActivateAbilityCommand("GEAR",ability.AbilityId,[target]));
    }

    internal static MatchState Position(string target="OGN·096/298")
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(target,out var unit));
        var state=ArmamentMightTests.Position(Hook,target,unit.SourceUnitPower);
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){["GEAR"]=state.CardObjects["GEAR"] with {Tags=[CardObjectTags.EquipmentCard]}}};
        var oldDeck=state.PlayerZones["P1"].MainDeck.ToHashSet();
        state=state with {CardObjects=state.CardObjects.Where(p=>!oldDeck.Contains(p.Key)).ToDictionary(),
            ObjectLocations=state.ObjectLocations.Where(p=>!oldDeck.Contains(p.Key)).ToDictionary()};
        state=state with {CardObjects=new Dictionary<string,CardObjectState>(state.CardObjects){
            ["HOOK_TOP"]=new("HOOK_TOP",cardNo:"OGN·096/298",manaCost:2,power:1,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]),
            ["HOOK_OTHER"]=new("HOOK_OTHER",cardNo:"SFD·042/221",manaCost:2,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard])},
            ObjectLocations=new Dictionary<string,ObjectLocationState>(state.ObjectLocations){["HOOK_TOP"]=new("P1","MAIN_DECK"),["HOOK_OTHER"]=new("P1","MAIN_DECK")},
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones){["P1"]=state.PlayerZones["P1"] with {MainDeck=["HOOK_TOP","HOOK_OTHER"]}},
            RunePools=new Dictionary<string,RunePool>(state.RunePools){["P1"]=new(20,20),["P2"]=new(20,20)}};
        return state;
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state,string branch)
    {
        var root=Environment.GetEnvironmentVariable("RIFTBOUND_HOOK_DECK_EVIDENCE");return new(state,string.IsNullOrWhiteSpace(root)?null:Path.Combine(root,branch));
    }
}
