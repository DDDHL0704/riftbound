using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SingleDeckPlaySequenceTests
{
    internal const string Reinforce = "OGN·062/298";
    internal const string VoidRush = "SFD·188/221";

    [Fact]
    public async Task ReinforceSelectsPrivatelyAndBanishesBeforeTheParentEndsAndTheUnitIsActuallyPlayed()
    {
        var initial = Position(Reinforce); var driver = Driver(initial, "reinforce-unit");
        await driver.Act("P1", new PlayCardCommand("PARENT", Reinforce, [])); await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("DECK_PLAY_SELECTION", choice.ChoiceWindow); Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(["P1_TOP"], choice.LegalObjectIds);
        await DeckPlaySequenceTests.Select(driver, "P1_TOP");
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("STACK", driver.State.ObjectLocations["P1_TOP"].Zone);
        Assert.Equal(2, driver.State.CardObjects["P1_TOP"].ObjectGeneration);
        Assert.NotNull(Assert.Single(driver.State.StackItems).DeckPlayPendingCard);
        Assert.Equal(5, driver.State.PendingEffectPlay!.ManaReduction);
        await driver.Act("P1", new PlayCardCommand("P1_TOP", "OGN·096/298", []));
        Assert.True(driver.State.CardObjects["P1_TOP"].IsExhausted);
        Assert.Contains("P1_TOP", driver.State.PlayerZones["P1"].Base);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task VoidRushBanishesOneAndDrawsTheOtherBeforeAnySelectedCardIsConfirmed()
    {
        var initial = Position(VoidRush); var driver = Driver(initial, "void-rush-unit");
        await driver.Act("P1", new PlayCardCommand("PARENT", VoidRush, [])); await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(["P1_TOP", "N1"], choice.ContextObjectIds);
        await DeckPlaySequenceTests.Select(driver, "P1_TOP");
        Assert.Contains("N1", driver.State.PlayerZones["P1"].Hand);
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("P1_TOP", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, driver.State.PendingEffectPlay!.ManaReduction);
        await driver.Act("P1", new PlayCardCommand("P1_TOP", "OGN·096/298", []));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARDS_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·125/221", 4, false)]
    [InlineData("SFD·042/221", 2, false)]
    [InlineData("OGN·048/298", 2, true)]
    public async Task VoidBurrowerPaidConquestRevealsThenBanishesBeforeItsAbilityEndsAndPaysNormalCost(string card, int costMana, bool spell)
    {
        var initial = OfficialLegendDeckPlayTests.Position(top:[card,"SFD·042/221"]);
        var driver = Driver(initial, "void-burrower-" + card.Replace('·','-').Replace('/','-'));
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand()); await driver.Act("P2", new PassFocusCommand());
        var cost = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act(cost.PlayerId, new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["LEGEND"])); await driver.Top();
        Assert.NotNull(driver.State.PendingCardChoice);
        await DeckPlaySequenceTests.Select(driver, "DECK0");
        Assert.True(driver.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal("STACK", driver.State.ObjectLocations["DECK0"].Zone);
        Assert.NotNull(Assert.Single(driver.State.StackItems).DeckPlayPendingCard);
        var mana = driver.State.RunePools["P1"].Mana;
        await driver.Act("P1", new PlayCardCommand("DECK0", card, []));
        Assert.Equal(mana - costMana, driver.State.RunePools["P1"].Mana);
        if(spell){Assert.Null(Assert.Single(driver.State.StackItems).DeckPlayPendingCard);await driver.Top();}
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("OGN·208/298", "DESTROY_FRIENDLY_UNIT:HOST")]
    [InlineData("SFD·044/221", "RETURN_FRIENDLY_EQUIPMENT:COST_GEAR")]
    [InlineData("UNL-166/219", "DESTROY_FRIENDLY_TRAIT_UNIT:COST_PORO")]
    public async Task ChosenUnitsOfferTheirActualMandatoryAdditionalCostToTheProductionComposer(string unit, string cost)
    {
        var initial = DeckPlaySequenceTests.TopCard(Position(Reinforce), "P1_TOP", unit);
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["COST_GEAR"] = new("COST_GEAR",cardNo:"SFD·042/221",ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.EquipmentCard]),
            ["COST_PORO"] = new("COST_PORO",cardNo:"UNL-137/219",manaCost:2,power:1,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard,"魄罗"]) },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) {
                ["COST_GEAR"] = new("P1","BASE"),["COST_PORO"] = new("P1","BASE") },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base=initial.PlayerZones["P1"].Base.Concat(["COST_GEAR","COST_PORO"]).ToArray() } } };
        var driver = Driver(initial, "reinforce-mandatory-" + unit.Replace('·','-').Replace('/','-'));
        await driver.Act("P1",new PlayCardCommand("PARENT",Reinforce,[]));await driver.Top();await DeckPlaySequenceTests.Select(driver,"P1_TOP");
        var plain = new PlayCardCommand("P1_TOP",unit,[]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State,"P1",PlayCostPreviewTests.Request(driver.State,plain));
        Assert.False(quote.CanPay);
        var prompt = System.Text.Json.JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(driver.State)["P1"],new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var requirement = prompt.GetProperty("candidates").EnumerateArray().Single(c=>c.GetProperty("action").GetString()=="PLAY_CARD")
            .GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray().Single(r=>r.GetProperty("sourceObjectId").GetString()=="P1_TOP");
        Assert.True(requirement.GetProperty("composable").GetBoolean());
        Assert.Contains(requirement.GetProperty("optionalCostChoices").EnumerateArray(),c=>c.GetProperty("id").GetString()==cost);
        await driver.Act("P1",plain with { OptionalCosts=[cost] });
        Assert.Contains("P1_TOP",driver.State.PlayerZones["P1"].Base);
        Assert.Contains(cost.Split(':')[1],cost.StartsWith("RETURN_")?driver.State.PlayerZones["P1"].Hand:driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(Reinforce)]
    [InlineData(VoidRush)]
    public async Task DecliningBeforeBanishmentRecyclesOrDrawsExactlyTheViewedCards(string card)
    {
        var initial=Position(card);var driver=Driver(initial,card==Reinforce?"reinforce-decline":"void-rush-decline");
        await driver.Act("P1",new PlayCardCommand("PARENT",card,[]));await driver.Top();
        var choice=driver.State.PendingCardChoice!;
        await driver.Act("P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,[]));
        Assert.Empty(driver.State.PlayerZones["P1"].Banished);Assert.Null(driver.State.PendingEffectPlay);Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="CARD_BANISHED");
        if(card==VoidRush)Assert.Equal(["P1_TOP","N1"],driver.State.PlayerZones["P1"].Hand);
        else { Assert.Empty(driver.State.PlayerZones["P1"].Hand);Assert.Equal("TAIL",driver.State.PlayerZones["P1"].MainDeck[0]); }
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("OGN·208/298")]
    [InlineData("SFD·044/221")]
    [InlineData("UNL-166/219")]
    public async Task AnUnavailableMandatoryCostReturnsTheChosenUnitToBanishmentWithoutInventingAPlay(string unit)
    {
        var initial=DeckPlaySequenceTests.TopCard(Position(Reinforce),"P1_TOP",unit);
        var removed=initial.PlayerZones["P1"].Base.Concat(initial.PlayerZones["P1"].Battlefields)
            .Where(id=>initial.CardObjects.TryGetValue(id,out var c)&&c.Tags.Any(t=>t is CardObjectTags.UnitCard or CardObjectTags.EquipmentCard)).ToHashSet();
        initial=initial with { CardObjects=initial.CardObjects.Where(p=>!removed.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),
            ObjectLocations=initial.ObjectLocations.Where(p=>!removed.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),
            PlayerZones=initial.PlayerZones.ToDictionary(p=>p.Key,p=>p.Key!="P1"?p.Value:p.Value with {
                Base=p.Value.Base.Where(id=>!removed.Contains(id)).ToArray(),Battlefields=p.Value.Battlefields.Where(id=>!removed.Contains(id)).ToArray() }) };
        var driver=Driver(initial,"reinforce-unavailable-"+unit.Replace('·','-').Replace('/','-'));
        await driver.Act("P1",new PlayCardCommand("PARENT",Reinforce,[]));await driver.Top();await DeckPlaySequenceTests.Select(driver,"P1_TOP");
        var pending=driver.State.PendingEffectPlay!;
        await driver.Act("P1",new ChooseCardsCommand(pending.ChoiceId,"EFFECT_PLAY",[]));
        Assert.Contains("P1_TOP",driver.State.PlayerZones["P1"].Banished);Assert.Equal(3,driver.State.CardObjects["P1_TOP"].ObjectGeneration);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE");
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(Reinforce)]
    [InlineData(VoidRush)]
    public async Task RealEchoCompletesBothOptionalSelectionsAndRemainderInstructionsBeforeConfirmingEitherUnit(string card)
    {
        var initial=Position(card) with { UntilEndOfTurnEffects=[EchoCostRules.GrantPrefix+"P1"] };
        var index=card==Reinforce?5:2;
        initial=initial with { CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["SECOND"]=new("SECOND",cardNo:"OGN·096/298",manaCost:2,power:1,ownerId:"P1",controllerId:"P1",tags:[CardObjectTags.UnitCard]) },
            ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){["SECOND"]=new("P1","MAIN_DECK")},
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {
                MainDeck=initial.PlayerZones["P1"].MainDeck.Take(index).Concat(["SECOND"]).Concat(initial.PlayerZones["P1"].MainDeck.Skip(index)).ToArray() } } };
        var driver=Driver(initial,card==Reinforce?"reinforce-echo":"void-rush-echo");
        await driver.Act("P1",new PlayCardCommand("PARENT",card,[],OptionalCosts:["ECHO"]));await driver.Top();
        await DeckPlaySequenceTests.Select(driver,"P1_TOP");Assert.DoesNotContain("PARENT",driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE");
        await DeckPlaySequenceTests.Select(driver,"SECOND");Assert.Contains("PARENT",driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1_TOP","SECOND"],driver.State.StackItems.Select(i=>i.SourceObjectId));
        if(card==VoidRush)Assert.Equal(["N1","N2"],driver.State.PlayerZones["P1"].Hand);
        foreach(var id in new[]{"P1_TOP","SECOND"})await driver.Act("P1",new PlayCardCommand(id,"OGN·096/298",[]));
        Assert.Null(driver.State.PendingEffectPlay);Assert.Equal(2,driver.Events().Count(e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE"));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(Reinforce)]
    [InlineData(VoidRush)]
    public async Task EmptyDecksDoNotBurnOutWhenTheInstructionOnlyLooksOrDrawsUnbanishedViewedCards(string card)
    {
        var initial=Position(card);var removed=initial.PlayerZones["P1"].MainDeck.ToHashSet();
        initial=initial with { CardObjects=initial.CardObjects.Where(p=>!removed.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),
            ObjectLocations=initial.ObjectLocations.Where(p=>!removed.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),
            PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){["P1"]=initial.PlayerZones["P1"] with {MainDeck=[]}} };
        var driver=Driver(initial,card==Reinforce?"reinforce-empty":"void-rush-empty");
        await driver.Act("P1",new PlayCardCommand("PARENT",card,[]));await driver.Top();
        Assert.Empty(driver.State.StackItems);Assert.Null(driver.State.PendingCardChoice);Assert.Null(driver.State.PendingEffectPlay);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind is "BURNOUT_APPLIED" or "CARD_DRAWN" or "UNIT_PLAYED_TO_BASE");
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("SFD·042/221", 0, false)]
    [InlineData("OGN·048/298", 0, true)]
    public async Task VoidRushActuallyConfirmsEquipmentAndOrdinarySpellsWithTheTwoManaReduction(string card,int costMana,bool spell)
    {
        var initial=DeckPlaySequenceTests.TopCard(Position(VoidRush),"P1_TOP",card);
        var driver=Driver(initial,"void-rush-"+card.Replace('·','-').Replace('/','-'));
        await driver.Act("P1",new PlayCardCommand("PARENT",VoidRush,[]));await driver.Top();await DeckPlaySequenceTests.Select(driver,"P1_TOP");
        var before=driver.State.RunePools["P1"].Mana;
        await driver.Act("P1",new PlayCardCommand("P1_TOP",card,[]));Assert.Equal(before-costMana,driver.State.RunePools["P1"].Mana);
        if(spell){Assert.Null(Assert.Single(driver.State.StackItems).DeckPlayPendingCard);await driver.Top();}
        else Assert.Contains("P1_TOP",driver.State.PlayerZones["P1"].Base);
        Assert.Contains("N1",driver.State.PlayerZones["P1"].Hand);await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("reduction")]
    [InlineData("free-mana")]
    [InlineData("free-power")]
    [InlineData("all-costs")]
    [InlineData("old-deck-source")]
    [InlineData("wrong-actor")]
    [InlineData("missing-source")]
    [InlineData("card-policy")]
    [InlineData("missing-selection")]
    public async Task RecoveryRejectsIndependentSinglePlayerPermissionAndPolicyForgeries(string mutation)
    {
        var driver=new StandbyDefendObserverTests.Driver(Position(Reinforce));
        await driver.Act("P1",new PlayCardCommand("PARENT",Reinforce,[]));await driver.Top();await DeckPlaySequenceTests.Select(driver,"P1_TOP");
        var p=driver.State.PendingEffectPlay!;var c=p.Parent.DeckPlaySequence!;var bad=mutation switch {
            "reduction"=>p with {ManaReduction=100},"free-mana"=>p with {IgnoreBaseMana=true},"free-power"=>p with {IgnoreBasePower=true},
            "all-costs"=>p with {IgnoreAllCosts=true},"old-deck-source"=>p with {SourceZone="MAIN_DECK"},
            "wrong-actor"=>p with {PlayerId="P2"},
            "card-policy"=>p with {Parent=p.Parent with {CardNo=VoidRush,DeckPlaySequence=c with {CardNo=VoidRush}}},
            "missing-selection"=>p with {Parent=p.Parent with {DeckPlaySequence=c with {Selected=new Dictionary<string,ObjectBinding>()}}},
            _=>p with {Sources=new Dictionary<string,long>()} };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with {PendingEffectPlay=bad}),e=>e.Contains("deck"));
    }

    internal static MatchState Position(string card)
    {
        var state = DeckPlaySequenceTests.Position(card); var cards = new Dictionary<string,CardObjectState>(state.CardObjects);
        var locations = new Dictionary<string,ObjectLocationState>(state.ObjectLocations);
        var ids = new[] { "N1", "N2", "N3", "N4", "TAIL" };
        foreach (var id in ids)
        {
            cards[id] = new(id, cardNo:"SFD·042/221", ownerId:"P1", controllerId:"P1", tags:[CardObjectTags.EquipmentCard]);
            locations[id] = new("P1", "MAIN_DECK");
        }
        return state with { CardObjects=cards, ObjectLocations=locations,
            PlayerZones=new Dictionary<string,PlayerZones>(state.PlayerZones) { ["P1"]=state.PlayerZones["P1"] with {
                MainDeck=new[]{"P1_TOP"}.Concat(ids).Concat(state.PlayerZones["P1"].MainDeck.Skip(1)).ToArray() } } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SINGLE_DECK_PLAY_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
