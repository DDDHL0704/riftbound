using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BeforeDeckRevealTests
{
    private const string Seedling = "SFD·018/221";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualSeedlingPlayPrivatelyLooksBeforeTheSmithRevealsAndContinuesWithTheCurrentTop(bool recycle)
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "SEED", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SEED", Seedling, [], Destination: "BASE"));
        await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal(CoreRuleEngine.BeforeDeckRevealChoiceKind, choice.Request.Kind);
        Assert.Equal("P1-D0", Assert.Single(choice.Request.Preview!.DeckObservationView!.Cards).Key);
        Assert.Contains("P1-D0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        Assert.DoesNotContain("P1-D0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARDS_REVEALED");
        await Decide(game, recycle);
        var marker = Assert.Single(game.Events(), e => e.Event.Kind == CoreRuleEngine.BeforeDeckRevealLookEvent).Event;
        Assert.DoesNotContain("P1-D0", JsonSerializer.Serialize(marker.Payload));
        Assert.DoesNotContain("lookHash", marker.Payload.Keys);
        Assert.All(game.Events().Where(e => e.Event.Kind == "CARDS_RECYCLED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SEED")),
            e => Assert.DoesNotContain("cardIds", e.Event.Payload.Keys));
        var revealed = Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_REVEALED").Event;
        Assert.Equal(new[] { recycle ? "P1-D1" : "P1-D0" }, Assert.IsType<string[]>(revealed.Payload["cardIds"]));
        Assert.Equal(recycle, game.State.PlayerZones["P1"].Hand.Contains("P1-D1"));
        Assert.Equal(recycle ? new[] { "P1-D0" } : new[] { "P1-D1", "P1-D0" }, game.State.PlayerZones["P1"].MainDeck);
        Assert.Null(game.State.PendingRuleChoice); await game.Replay();
    }

    [Fact]
    public async Task EachCurrentSeedlingAppliesOnceBeforeOneRevealInstruction()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "SEED2", Seedling, "P1", "BASE"));
        await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        var order = game.State.PendingRuleChoice!.Request;
        Assert.Equal(CoreRuleEngine.BeforeDeckRevealOrderKind, order.Kind);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-before-order", "P1", CommandTypes.PayCost),
            new PayCostCommand(order.Id, CoreRuleEngine.RuleChoiceWindow, ["SOURCE:99"]), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", new PayCostCommand(order.Id, CoreRuleEngine.RuleChoiceWindow, ["SOURCE:1"]));
        Assert.Equal("SEED2", game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.Source.ObjectId);
        await Decide(game, true);
        Assert.Equal("P1-D1", game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.Card.ObjectId);
        Assert.Equal("SEED", game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.Source.ObjectId);
        await Decide(game, false);
        Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == CoreRuleEngine.BeforeDeckRevealLookEvent));
        Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_REVEALED"); await game.Replay();
    }

    [Theory]
    [InlineData("off-field")] [InlineData("opponent")] [InlineData("empty")]
    public async Task OnlyCurrentFieldSourcesControlledByTheRevealingPlayerApplyAndEmptyDecksNeedNoChoice(string branch)
    {
        var state = branch == "empty" ? ApprenticeBlacksmithRevealTests.Position("OGN·219/298", 0) : Position();
        if (branch == "empty") state = RadGearTestPosition.Put(state, "SEED", Seedling, "P1", "BASE");
        if (branch == "off-field") state = RadUnitEffectTestFixture.Place(state, "SEED", "P1", "HAND");
        if (branch == "opponent") state = RadUnitEffectTestFixture.Place(state, "SEED", "P2", "BASE");
        var game = new Driver(state); await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == CoreRuleEngine.BeforeDeckRevealLookEvent);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED"); await game.Replay();
    }

    [Fact]
    public async Task ANocturneSeenInThePrivatePrelookMayBanishBeforeTheOriginalRevealContinues()
    {
        var game = new Driver(Position("OGN·194/298"));
        await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("DECK_OBSERVATION_BANISH", request.Kind);
        Assert.False(request.Preview!.DeckObservationView!.Public);
        Assert.NotNull(request.Preview.DeckObservationView.BeforeReveal);
        await game.Act("P1", new PayCostCommand(request.Id, CoreRuleEngine.RuleChoiceWindow, ["BANISH"]));
        Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand);
        var captured = Assert.Single(game.State.ObservedDeckPlays!);
        Assert.Equal("LOOK", captured.Instruction.Kind); Assert.NotNull(captured.Instruction.BeforeReveal);
        var forged = game.State with { ObservedDeckPlays = [captured with {
            Instruction = captured.Instruction with { BeforeReveal = null } }] };
        Assert.Contains(Errors(game, forged), error => error.Contains("original accepted observation", StringComparison.Ordinal));
        var pending = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Banished); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task APrivateRunePeekCanRecycleBeforeFateRevealsAndDoesNotGeneratePower(bool recycle)
    {
        var state = RadGearTestPosition.Put(Position(), "FATE", "OGN·200/298", "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "R0", "OGN·042/298", "P1", "RUNE_DECK");
        state = RadGearTestPosition.Put(state, "R1", "OGN·126/298", "P1", "RUNE_DECK");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("FATE", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal("ATTACK", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Top();
        Assert.Equal("RUNE_DECK", game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.DeckZone);
        Assert.Contains("R0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        Assert.DoesNotContain("R0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        var power = game.State.RunePools["P1"].TotalPower; await Decide(game, recycle);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(recycle ? new[] { "R0", "R1" } : new[] { "R1", "R0" }, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(recycle ? "R1" : "R0", Assert.IsType<string[]>(Assert.Single(game.Events(),
            e => e.Event.Kind == "CARDS_REVEALED").Event.Payload["cardIds"])[0]);
        await game.Replay();
    }

    [Fact]
    public async Task BanishingTheOnlyPrivatelyLookedCardPreservesTheBanishWhenTheGuessedRevealIsEmpty()
    {
        var state = RadUnitEffectTestFixture.Place(Position("OGN·194/298"), "P1-D1", "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "LENS", "RAD·064/167", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("LENS", "RAD_064_PAY_ONE_EXHAUST_GUESS_DECK_TOP", ["P2"]));
        await game.Top();
        var guess = game.State.PendingRuleChoice!.Request;
        Assert.Equal("DECK_TOP_TYPE_GUESS", guess.Kind);
        await game.Act("P2", new PayCostCommand(guess.Id, CoreRuleEngine.RuleChoiceWindow, ["UNIT"]));
        var banish = game.State.PendingRuleChoice!.Request;
        Assert.Equal("DECK_OBSERVATION_BANISH", banish.Kind);
        await game.Act("P1", new PayCostCommand(banish.Id, CoreRuleEngine.RuleChoiceWindow, ["BANISH"]));
        Assert.Empty(game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal("STACK", game.State.ObjectLocations["P1-D0"].Zone);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "MAIN_DECK_CARDS_REVEALED" or "CARD_DRAWN" or "BURNOUT_APPLIED");
        var pending = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Banished);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecyclingBeforeAFilteredRevealKeepsTheOriginalSelectionAndTheOriginalFiveCardRemainder(bool selectTop)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SEED", Seedling, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "SPELL", "RAD·033/167", "P1", "HAND");
        var selected = selectTop ? "P1-D0" : "P1-D1";
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [selected] = OfficialGarenConquestBattlefieldCountTests.Printed(selected, "SFD·022/221", "P1") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·033/167", [], Mode: "LOOK_EQUIPMENT"));
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice); // Looking alone never applies the reveal replacement.
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(5, choice.ContextObjectIds.Count);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [selected]));
        Assert.True(game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.SelectionReveal);
        await Decide(game, true);
        Assert.Contains(selected, game.State.PlayerZones["P1"].Hand);
        Assert.Equal("P1-D5", game.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(5, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(selected, Assert.Single(game.Events(), e => e.Event.Kind == "CARD_REVEALED").Event.Payload["objectId"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARDS_REVEALED");
        await game.Replay();
    }

    [Fact]
    public async Task BlindFuryLetsTheInstructedOpponentApplyTheirOwnSeedlingBeforeTheirDeckIsRevealed()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SEED1", Seedling, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "SEED2", Seedling, "P2", "BASE");
        state = RadGearTestPosition.Put(state, "SPELL", DeckPlaySequenceTests.BlindFury, "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", DeckPlaySequenceTests.BlindFury, [])); await game.Top();
        var peek = game.State.PendingRuleChoice!.Request;
        Assert.Equal("P2", peek.PlayerId);
        Assert.Equal("SEED2", peek.Preview!.DeckObservationView!.BeforeReveal!.Source.ObjectId);
        Assert.DoesNotContain("P2-D0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await Decide(game, true);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(new[] { "P2-D1" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2-D1"]));
        await game.Act("P1", new PlayCardCommand("P2-D1", "OGN·219/298", []));
        Assert.Single(game.Events(), e => e.Event.Kind == CoreRuleEngine.BeforeDeckRevealLookEvent);
        Assert.Equal("P2-D0", game.State.PlayerZones["P2"].MainDeck[^1]); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AControllerRevealingAnOpponentsDeckUsesTheirOwnSeedlingAndKeepsTheForeignPrelookPrivate(bool recycle)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SEED1", Seedling, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "SEED2", Seedling, "P2", "BASE");
        state = RadGearTestPosition.Put(state, "TRAINER", "RAD·018/167", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P2-D0"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-D0", "OGN·194/298", "P2") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("TRAINER", "RAD·018/167", [], Destination: "BASE")); await game.Top();
        var peek = game.State.PendingRuleChoice!.Request;
        Assert.Equal(CoreRuleEngine.BeforeDeckRevealChoiceKind, peek.Kind);
        Assert.Equal("P1", peek.PlayerId);
        var view = peek.Preview!.DeckObservationView!;
        Assert.Equal("P2", view.DeckOwnerId); Assert.Equal("SEED1", view.BeforeReveal!.Source.ObjectId);
        Assert.Contains("P2-D0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        Assert.DoesNotContain("P2-D0", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        var forged = game.State with { PendingRuleChoice = game.State.PendingRuleChoice! with { Request = peek with {
            Preview = peek.Preview! with { DeckObservationView = view with { DeckOwnerId = null } } } } };
        Assert.NotEmpty(Errors(game, forged));
        await Decide(game, recycle);
        Assert.Null(game.State.PendingRuleChoice); // Neither player receives an owned Nocturne observation permission.
        var choice = game.State.PendingCardChoice!; var selected = recycle ? "P2-D1" : "P2-D0";
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(new[] { selected }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [selected]));
        await game.Act("P1", new PlayCardCommand(selected, recycle ? "OGN·219/298" : "OGN·194/298", []));
        Assert.Single(game.Events(), e => e.Event.Kind == CoreRuleEngine.BeforeDeckRevealLookEvent);
        if (recycle) Assert.Equal("P2-D0", game.State.PlayerZones["P2"].MainDeck[^1]);
        Assert.Empty(game.State.ObservedDeckPlays ?? []); await game.Replay();
    }

    [Fact]
    public async Task ARealSongCopiesThePrintedReplacementAsOneAdditionalPrivateLook()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "SONG", "SFD·059/221", "P1", "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "SEED", ["ASSEMBLE_GREEN"])); await game.Top();
        await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        var order = game.State.PendingRuleChoice!.Request;
        Assert.Equal(CoreRuleEngine.BeforeDeckRevealOrderKind, order.Kind);
        await game.Act("P1", new PayCostCommand(order.Id, CoreRuleEngine.RuleChoiceWindow, ["SOURCE:0"]));
        Assert.Null(game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.Origin);
        await Decide(game, false);
        Assert.NotNull(game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.Origin);
        await Decide(game, true);
        Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == CoreRuleEngine.BeforeDeckRevealLookEvent)); await game.Replay();
    }

    [Fact]
    public async Task ASelectedNocturneBanishedByThePrelookIsNotRevealedDrawnOrReplacedByAnotherCard()
    {
        var state = RadGearTestPosition.Put(Position("OGN·194/298"), "SPELL", "UNL-032/219", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "UNL-032/219", [])); await game.Top();
        var firstLook = game.State.PendingRuleChoice!.Request;
        Assert.Null(firstLook.Preview!.DeckObservationView!.BeforeReveal);
        await game.Act("P1", new PayCostCommand(firstLook.Id, CoreRuleEngine.RuleChoiceWindow, ["KEEP"]));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-D0"]));
        var prelook = game.State.PendingRuleChoice!.Request;
        Assert.True(prelook.Preview!.DeckObservationView!.BeforeReveal!.SelectionReveal);
        await game.Act("P1", new PayCostCommand(prelook.Id, CoreRuleEngine.RuleChoiceWindow, ["BANISH"]));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_REVEALED" or "CARD_DRAWN");
        Assert.DoesNotContain("P1-D1", game.State.PlayerZones["P1"].Hand);
        var pending = game.State.PendingEffectPlay!;
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Banished); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DecliningNocturnesPrivateOfferStillAllowsRecyclingOrItsSeparatePublicRevealOffer(bool recycle)
    {
        var game = new Driver(Position("OGN·194/298"));
        await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        var privateLook = game.State.PendingRuleChoice!.Request;
        await game.Act("P1", new PayCostCommand(privateLook.Id, CoreRuleEngine.RuleChoiceWindow, ["KEEP"]));
        await Decide(game, recycle);
        if (recycle)
        {
            Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand);
            Assert.Contains("P1-D0", game.State.PlayerZones["P1"].MainDeck);
            Assert.Null(game.State.PendingRuleChoice); Assert.Null(game.State.PendingEffectPlay);
        }
        else
        {
            var reveal = game.State.PendingRuleChoice!.Request;
            Assert.True(reveal.Preview!.DeckObservationView!.Public);
            Assert.Null(reveal.Preview.DeckObservationView.BeforeReveal);
            await game.Act("P1", new PayCostCommand(reveal.Id, CoreRuleEngine.RuleChoiceWindow, ["BANISH"]));
            var captured = Assert.Single(game.State.ObservedDeckPlays!);
            Assert.Equal("REVEAL", captured.Instruction.Kind); Assert.Null(captured.Instruction.BeforeReveal);
            var pending = game.State.PendingEffectPlay!;
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Banished);
            Assert.DoesNotContain("P1-D1", game.State.PlayerZones["P1"].Hand);
        }
        await game.Replay();
    }

    [Fact]
    public async Task ARealEchoKeepsEachOriginalSelectionAndRunsItsOwnPrelookAtTheNextExecution()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SEED", Seedling, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "SPELL", "UNL-032/219", "P1", "HAND") with {
            UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SPELL", "UNL-032/219", [], OptionalCosts: ["ECHO"]));
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount); await game.Top();
        var first = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, ["P1-D0"]));
        Assert.Equal(0, game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.ExecutionIndex);
        await Decide(game, true);
        Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand);
        var second = game.State.PendingCardChoice!;
        Assert.Equal("P1-D3", second.ContextObjectIds[0]);
        await game.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, ["P1-D3"]));
        Assert.Equal(1, game.State.PendingRuleChoice!.Request.Preview!.DeckObservationView!.BeforeReveal!.ExecutionIndex);
        await Decide(game, false);
        Assert.Contains("P1-D3", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == CoreRuleEngine.BeforeDeckRevealLookEvent));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task APrivatePrelookRecycleStillCapturesTheNormalIndependentBoonObserver()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "BOON", "OGN·235/298", "P1", "BASE"));
        await ApprenticeBlacksmithRevealTests.Move(game); await game.Top(); await Decide(game, true);
        Assert.Contains("P1-D1", game.State.PlayerZones["P1"].Hand);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ALLY", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task MalformedPrivateDisclosureIsRejectedByItsSemanticValidatorWithoutThrowing()
    {
        var game = new Driver(Position()); await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        var state = CoreRuleEngine.VisibleRuleChoiceState(game.State); var view = state.DeckObservationView!;
        var context = view.BeforeReveal!;
        foreach (var malformed in new[] {
            state with { DeckObservationView = null },
            state with { DeckObservationView = view with { Cards = null! } },
            state with { DeckObservationView = view with { BeforeReveal = context with { PlayerId = null! } } },
            state with { DeckObservationView = view with { BeforeReveal = context with { DeckOwnerId = "UNKNOWN" } } },
            state with { DeckObservationView = view with { BeforeReveal = context with { Source = null! } } } })
            Assert.False(CoreRuleEngine.ValidBeforeDeckRevealDisclosure(malformed));
        Assert.True(CoreRuleEngine.ValidBeforeDeckRevealDisclosure(state));
        await Decide(game, false); await game.Replay();
    }

    [Fact]
    public async Task AChangedPrivatePeekCannotBeRecoveredOrAnsweredAndARejectedAnswerIsAtomic()
    {
        var game = new Driver(Position()); await ApprenticeBlacksmithRevealTests.Move(game); await game.Top();
        var pending = game.State.PendingRuleChoice!; var preview = pending.Request.Preview!;
        var bad = game.State with { PendingRuleChoice = pending with { Request = pending.Request with {
            Preview = preview with { DeckObservationView = preview.DeckObservationView! with { Public = true } } } } };
        Assert.NotEmpty(Errors(game, bad)); var hash = MatchStateHasher.Hash(bad);
        var result = await new CoreRuleEngine().ResolveAsync(bad, new("forged-peek", "P1", CommandTypes.PayCost),
            new PayCostCommand(pending.Request.Id, CoreRuleEngine.RuleChoiceWindow, ["KEEP"]), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState Position(string top = "OGN·219/298")
    {
        var state = RadGearTestPosition.Put(ApprenticeBlacksmithRevealTests.Position(top, 2), "SEED", Seedling, "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P1-D1"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-D1", "SFD·022/221", "P1") } };
    }
    private static async Task Decide(Driver game, bool recycle)
    {
        while (game.State.PendingRuleChoice?.Request.Kind == CoreRuleEngine.BeforeDeckRevealOrderKind)
        {
            var order = game.State.PendingRuleChoice.Request;
            await game.Act(order.PlayerId, new PayCostCommand(order.Id, CoreRuleEngine.RuleChoiceWindow, [order.Options[0].Id]));
        }
        var choice = game.State.PendingRuleChoice!.Request;
        await game.Act(choice.PlayerId, new PayCostCommand(choice.Id, CoreRuleEngine.RuleChoiceWindow, [recycle ? "RECYCLE" : "KEEP"]));
    }
    private static IReadOnlyList<string> Errors(Driver game, MatchState state)
    {
        var sequence = game.Commands().Last().CompletedEventSequence;
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, ResolutionResult.BuildSnapshots(state)[player], state.Tick, sequence,
            ResolutionResult.BuildPrompts(state)[player]));
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views,
            state, state.Tick, MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }
}
