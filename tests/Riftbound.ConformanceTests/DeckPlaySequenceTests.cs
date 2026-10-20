using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeckPlaySequenceTests
{
    internal const string BlindFury = "OGN·025/298";
    internal const string BrightFuture = "OGN·115/298";

    [Fact]
    public async Task BlindFuryIsDeclaredWithoutPrivateTargetsThenRevealsExilesAndActuallyPlaysTheCard()
    {
        var initial = Position(BlindFury); var driver = Driver(initial, "blind-fury-unit");
        await driver.Act("P1", new PlayCardCommand("PARENT", BlindFury, [])); await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(["P2_TOP"], choice.LegalObjectIds);
        await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2_TOP"]));
        Assert.Equal("STACK", driver.State.ObjectLocations["P2_TOP"].Zone);
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("P1", driver.State.PendingEffectPlay!.PlayerId);
        await driver.Act("P1", new PlayCardCommand("P2_TOP", "OGN·096/298", []));
        Assert.Equal("P2", driver.State.CardObjects["P2_TOP"].OwnerId);
        Assert.Equal("P1", driver.State.CardObjects["P2_TOP"].ControllerId);
        Assert.True(driver.State.CardObjects["P2_TOP"].IsExhausted);
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BrightFutureUsesPrivateChoicesThenRealOpponentFirstPlaysWithManaWaived()
    {
        var initial = Position(BrightFuture); var driver = Driver(initial, "bright-future-units");
        await driver.Act("P1", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Top();
        await Select(driver, "P1_TOP"); await Select(driver, "P2_TOP");
        Assert.Equal(["P2_TOP", "P1_TOP"], driver.State.StackItems.Select(i => i.SourceObjectId));
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("P2", driver.State.PendingEffectPlay!.PlayerId);
        await driver.Act("P2", new PlayCardCommand("P2_TOP", "OGN·096/298", []));
        Assert.Equal("P1", driver.State.PendingEffectPlay!.PlayerId);
        await driver.Act("P1", new PlayCardCommand("P1_TOP", "OGN·096/298", []));
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.True(driver.State.CardObjects["P1_TOP"].IsExhausted);
        Assert.True(driver.State.CardObjects["P2_TOP"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·022/221", true)]
    [InlineData("UNL-091/219", false)]
    public async Task BlindFuryActuallyPlaysEquipmentOrANormalSpellAndKeepsTheOpponentsOwnership(string card, bool equipment)
    {
        var initial = TopCard(Position(BlindFury), "P2_TOP", card);
        var driver = Driver(initial, equipment ? "blind-fury-equipment" : "blind-fury-spell");
        await driver.Act("P1", new PlayCardCommand("PARENT", BlindFury, [])); await driver.Top(); await Select(driver, "P2_TOP");
        var paid = driver.State.RunePools["P1"];
        await driver.Act("P1", new PlayCardCommand("P2_TOP", card, equipment ? ["HOST"] : []));
        Assert.Equal(paid, driver.State.RunePools["P1"]); Assert.Equal("P2", driver.State.CardObjects["P2_TOP"].OwnerId);
        if (equipment) Assert.Contains("P2_TOP", driver.State.PlayerZones["P1"].Base);
        else { Assert.Single(driver.State.StackItems); await driver.Top(); Assert.Contains("P2_TOP", driver.State.PlayerZones["P2"].Graveyard); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BrightFutureKeepsBothOrdinarySpellsInTheirOriginalPendingOrderAndNoResponseBeforeConfirmationEnds()
    {
        var initial = TopCard(TopCard(Position(BrightFuture), "P1_TOP", "UNL-091/219"), "P2_TOP", "UNL-091/219");
        initial = initial with { RunePools = new Dictionary<string,RunePool>(initial.RunePools) { ["P2"] = new(0,20) } };
        var driver = Driver(initial, "bright-future-two-spells");
        await driver.Act("P1", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Top(); await Select(driver, "P1_TOP"); await Select(driver, "P2_TOP");
        await driver.Act("P2", new PlayCardCommand("P2_TOP", "UNL-091/219", []));
        Assert.Equal(["P2_TOP", "P1_TOP"], driver.State.StackItems.Select(i => i.SourceObjectId));
        Assert.Null(driver.State.StackItems[0].DeckPlayPendingCard); Assert.NotNull(driver.State.StackItems[1].DeckPlayPendingCard);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("early-pass", "P2", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(driver.State), MatchStateHasher.Hash(rejected.State));
        await driver.Act("P1", new PlayCardCommand("P1_TOP", "UNL-091/219", []));
        Assert.Equal(["P2_TOP", "P1_TOP"], driver.State.StackItems.Select(i => i.SourceObjectId));
        await driver.Top(); await driver.Top(); await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BrightFutureCannotWaivePrintedPowerAndReturnsTheUnplayableCardToBanishment()
    {
        var initial = TopCard(Position(BrightFuture), "P2_TOP", "UNL-137/219");
        var driver = Driver(initial, "bright-future-unaffordable");
        await driver.Act("P1", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Top(); await Select(driver, "P1_TOP"); await Select(driver, "P2_TOP");
        var request = new PlayCardCommand("P2_TOP", "UNL-137/219", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", PlayCostPreviewTests.Request(driver.State, request));
        Assert.False(quote.CanPay); Assert.True(quote.Cost!.PrintedPower > 0); Assert.Equal(0, quote.Cost.Mana);
        var pending = driver.State.PendingEffectPlay!;
        await driver.Act("P2", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("P2_TOP", driver.State.PlayerZones["P2"].Banished);
        await driver.Act("P1", new PlayCardCommand("P1_TOP", "OGN·096/298", []));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_PLAYED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "P2_TOP"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BrightFuturePermitsAnActualMatchingRuneRecycleToPayTheChosenUnitsPrintedPower()
    {
        var initial = TopCard(Position(BrightFuture), "P2_TOP", "UNL-137/219");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["PAY_RUNE"] = new("PAY_RUNE", cardNo:"UNL-R05", ownerId:"P2", controllerId:"P2", tags:[CardObjectTags.RuneCard,"COLOR:purple"]) },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) { ["PAY_RUNE"] = new("P2","BASE") },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with {
                Base = initial.PlayerZones["P2"].Base.Append("PAY_RUNE").ToArray() } } };
        var driver = Driver(initial, "bright-future-real-rune-payment");
        await driver.Act("P1", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Top(); await Select(driver, "P1_TOP"); await Select(driver, "P2_TOP");
        await driver.Act("P2", new RecycleRuneCommand("PAY_RUNE"));
        await driver.Act("P2", new PlayCardCommand("P2_TOP", "UNL-137/219", []));
        Assert.Equal(0, driver.State.RunePools["P2"].TotalPower);
        Assert.Contains("PAY_RUNE", driver.State.PlayerZones["P2"].RuneDeck);
        await driver.Act("P1", new PlayCardCommand("P1_TOP", "OGN·096/298", []));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(BlindFury)]
    [InlineData(BrightFuture)]
    public async Task LookingOrRevealingEmptyDecksFinishesTheParentWithoutBurnoutOrInventedPlays(string card)
    {
        var initial = Position(card); var cards = initial.CardObjects.ToDictionary(p => p.Key,p => p.Value);
        var locations = initial.ObjectLocations.ToDictionary(p => p.Key,p => p.Value);
        foreach (var id in initial.PlayerZones.Values.SelectMany(z => z.MainDeck)) { cards.Remove(id); locations.Remove(id); }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key,p => p.Value with { MainDeck = [] }) };
        var driver = Driver(initial, card == BlindFury ? "blind-fury-empty" : "bright-future-empty");
        await driver.Act("P1", new PlayCardCommand("PARENT", card, [])); await driver.Top();
        Assert.Null(driver.State.PendingCardChoice); Assert.Null(driver.State.PendingEffectPlay); Assert.Empty(driver.State.StackItems);
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "BURNOUT" or "CARD_DRAWN" or "UNIT_PLAYED_TO_BASE");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task RealControlChangeKeepsTheTurnPlayersChoiceOrderAndFinishesTheSourceBeforeConfirmation()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(BrightFuture), "REFLECTION", "OGN·080/298", "P1");
        initial = initial with { TurnPlayerId="P2", ActivePlayerId="P2",
            RunePools = new Dictionary<string,RunePool>(initial.RunePools) { ["P2"] = new(20,20) },
            CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) { ["PARENT"] = initial.CardObjects["PARENT"] with { OwnerId="P2",ControllerId="P2" } },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) { ["PARENT"] = new("P2","HAND") },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "PARENT").ToArray() },
                ["P2"] = initial.PlayerZones["P2"] with { Hand = initial.PlayerZones["P2"].Hand.Append("PARENT").ToArray() } } };
        var driver = Driver(initial, "bright-future-real-control-change");
        await driver.Act("P2", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Act("P2", new PassPriorityCommand());
        await driver.Act("P1", new PlayCardCommand("REFLECTION", "OGN·080/298", [Assert.Single(driver.State.StackItems).StackItemId])); await driver.Top();
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal("P1", Assert.Single(driver.State.StackItems).ControllerId); await driver.Top();
        Assert.Equal("P2", driver.State.PendingCardChoice!.PlayerId); await Select(driver,"P2_TOP"); await Select(driver,"P1_TOP");
        Assert.Equal(["P1_TOP","P2_TOP"],driver.State.StackItems.Select(i => i.SourceObjectId));
        Assert.Contains("PARENT",driver.State.PlayerZones["P2"].Graveyard);
        await driver.Act("P1",new PlayCardCommand("P1_TOP","OGN·096/298",[])); await driver.Act("P2",new PlayCardCommand("P2_TOP","OGN·096/298",[]));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData(BlindFury)]
    [InlineData(BrightFuture)]
    public async Task RealEchoCompletesBothChoiceExecutionsBeforeTheParentFinishesAndAnyChosenCardIsConfirmed(string card)
    {
        var initial = Position(card) with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        foreach (var player in initial.Seats.Keys)
        {
            var id=player+"_SECOND";
            initial=initial with { CardObjects=new Dictionary<string,CardObjectState>(initial.CardObjects) {
                [id]=new(id,cardNo:"OGN·096/298",power:1,manaCost:2,ownerId:player,controllerId:player,tags:[CardObjectTags.UnitCard]) },
                ObjectLocations=new Dictionary<string,ObjectLocationState>(initial.ObjectLocations){[id]=new(player,"MAIN_DECK")},
                PlayerZones=new Dictionary<string,PlayerZones>(initial.PlayerZones){[player]=initial.PlayerZones[player] with {
                    MainDeck=card==BrightFuture
                        ? initial.PlayerZones[player].MainDeck.Take(5).Concat([id]).Concat(initial.PlayerZones[player].MainDeck.Skip(5)).ToArray()
                        : initial.PlayerZones[player].MainDeck.Take(1).Concat([id]).Concat(initial.PlayerZones[player].MainDeck.Skip(1)).ToArray() } } };
        }
        var driver=Driver(initial,card==BlindFury?"blind-fury-echo":"bright-future-echo");
        await driver.Act("P1",new PlayCardCommand("PARENT",card,[],OptionalCosts:["ECHO"]));await driver.Top();
        if(card==BrightFuture) await Select(driver,"P1_TOP");await Select(driver,"P2_TOP");
        Assert.DoesNotContain("PARENT",driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(driver.Events(),e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE");
        if(card==BrightFuture) await Select(driver,"P1_SECOND");await Select(driver,"P2_SECOND");
        var order=card==BlindFury?new[]{"P2_TOP","P2_SECOND"}:new[]{"P2_TOP","P1_TOP","P2_SECOND","P1_SECOND"};
        Assert.Equal(order,driver.State.StackItems.Select(i=>i.SourceObjectId));
        Assert.Contains("PARENT",driver.State.PlayerZones["P1"].Graveyard);
        foreach(var id in order)await driver.Act(card==BlindFury?"P1":id[..2],new PlayCardCommand(id,"OGN·096/298",[]));
        Assert.Null(driver.State.PendingEffectPlay);Assert.Empty(driver.State.StackItems);
        Assert.Equal(order.Length,driver.Events().Count(e=>e.Event.Kind=="UNIT_PLAYED_TO_BASE"));
        await RideTheWindCurrentMovementTests.Replay(initial,driver);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("order")]
    [InlineData("generation")]
    [InlineData("source")]
    [InlineData("card")]
    [InlineData("kind")]
    [InlineData("mana")]
    [InlineData("power")]
    [InlineData("all-costs")]
    [InlineData("parent-completed")]
    [InlineData("missing-slot")]
    public async Task RecoveryRejectsIndependentPendingSequenceAndPermissionForgeries(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position(BrightFuture));
        await driver.Act("P1", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Top(); await Select(driver, "P1_TOP"); await Select(driver, "P2_TOP");
        var state = driver.State; var p = state.PendingEffectPlay!; var c = p.Parent.DeckPlaySequence!;
        var bad = mutation switch {
            "actor" => p with { PlayerId = "P1" }, "order" => p with { Parent = p.Parent with { DeckPlaySequence = c with { PlayIndex = 1 } } },
            "generation" => p with { Sources = new Dictionary<string,long> { ["P2_TOP"] = 999999 } },
            "source" => p with { Parent = p.Parent with { DeckPlaySequence = c with { Source = new("HOST", 0) } } },
            "card" => p with { Parent = p.Parent with { DeckPlaySequence = c with { CardNo = BlindFury } } },
            "kind" => p with { Parent = p.Parent with { DeckPlaySequence = c with { Kind = "OPPONENT_REVEAL_PLAY" } } },
            "mana" => p with { IgnoreBaseMana = false }, "power" => p with { IgnoreBasePower = true },
            "all-costs" => p with { IgnoreAllCosts = true },
            "parent-completed" => p with { Parent = p.Parent with { DeckPlaySequence = c with { ParentCompleted = false } } }, _ => p };
        var corrupted = state with { PendingEffectPlay = bad,
            StackItems = mutation == "missing-slot" ? state.StackItems.Skip(1).ToArray() : state.StackItems };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(corrupted), e => e.Contains("deck"));
    }

    [Fact]
    public async Task RejectedPowerPaymentKeepsTheRealPendingCardAndAllAuthoritativeStateUnchanged()
    {
        var initial = TopCard(Position(BrightFuture), "P2_TOP", "UNL-137/219");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Top();
        await Select(driver, "P1_TOP"); await Select(driver, "P2_TOP");
        var before = MatchStateHasher.Hash(driver.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("insufficient-power", "P2", CommandTypes.PlayCard),
            new PlayCardCommand("P2_TOP", "UNL-137/219", []), default);
        Assert.False(rejected.Accepted); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.NotNull(rejected.State.StackItems[0].DeckPlayPendingCard);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(rejected.State));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("owner")]
    [InlineData("generation")]
    [InlineData("parent")]
    [InlineData("order")]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task RecoveryRejectsPublicPendingCardReceiptForgeriesForBothPlayersAndSpectators(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position(BrightFuture));
        await driver.Act("P1", new PlayCardCommand("PARENT", BrightFuture, [])); await driver.Top();
        await Select(driver, "P1_TOP"); await Select(driver, "P2_TOP");
        var state = driver.State; var receipt = state.StackItems[0].DeckPlayPendingCard!;
        object? bad = mutation switch {
            "actor" => receipt with { PlayerId = "P1" }, "owner" => receipt with { OwnerId = "P1" },
            "generation" => receipt with { Card = receipt.Card with { Generation = 999999 } },
            "parent" => receipt with { ParentStackItemId = "forged-parent" }, "order" => receipt with { OrderIndex = 1 },
            "malformed" => "forged-receipt", _ => null };
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var row = Assert.IsType<Dictionary<string,object?>>(snapshot.Stack[0]); var original = row["deckPlayPendingCard"];
            row["deckPlayPendingCard"] = bad;
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator), e => e.Contains("deck pending card"));
            row["deckPlayPendingCard"] = original;
        }
    }

    internal static MatchState TopCard(MatchState state, string id, string no)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var b));
        return state with { CardObjects = new Dictionary<string,CardObjectState>(state.CardObjects) { [id] = state.CardObjects[id] with {
            CardNo = no, Power = b.SourceUnitPower, ManaCost = b.ManaCost,
            Tags = [b.PlaysSourceToBaseAsUnit ? CardObjectTags.UnitCard : b.PlaysSourceToBaseAsEquipment ? CardObjectTags.EquipmentCard : CardObjectTags.SpellCard] } } };
    }

    internal static async Task Select(StandbyDefendObserverTests.Driver driver, string card)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [card]));
    }

    internal static MatchState Position(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(SacredProtectorTests.Position(), "PARENT", card, "P1");
        var cards = initial.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = initial.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        var zones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        foreach (var player in initial.Seats.Keys)
        {
            var id = player + "_TOP";
            cards[id] = new(id, cardNo: "OGN·096/298", power: 1, manaCost: 2, ownerId: player, controllerId: player, tags: [CardObjectTags.UnitCard]);
            locations[id] = new(player, "MAIN_DECK"); zones[player] = zones[player] with { MainDeck = zones[player].MainDeck.Prepend(id).ToArray() };
        }
        return initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string,RunePool>(initial.RunePools) { ["P1"] = new(20,20), ["P2"] = new(0,0) } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_DECK_PLAY_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
