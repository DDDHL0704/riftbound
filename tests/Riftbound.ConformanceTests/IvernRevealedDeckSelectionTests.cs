using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class IvernRevealedDeckSelectionTests
{
    private const string Ivern = "UNL-051/219";
    private static MatchState Position(string no = Ivern, string chosen = "UNL-137/219")
    {
        var state = RadGearTestPosition.Create("OGN·280/298");
        state = RadGearTestPosition.Put(state, "IVERN", no, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        foreach (var id in state.PlayerZones["P1"].MainDeck.ToArray())
            state = RadUnitEffectTestFixture.Place(state, id, "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "SEEN-UNIT", chosen, "P1", "MAIN_DECK");
        state = RadGearTestPosition.Put(state, "SEEN-SPELL", "OGN·104/298", "P1", "MAIN_DECK");
        state = RadGearTestPosition.Put(state, "SEEN-GEAR", "SFD·022/221", "P1", "MAIN_DECK");
        state = RadGearTestPosition.Put(state, "TAIL", "OGN·219/298", "P1", "MAIN_DECK");
        return state;
    }

    private static Task Choose(Driver game, params string[] selected)
    {
        var choice = game.State.PendingCardChoice!;
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
    }

    private static async Task<Driver> Begin(string no = Ivern, string chosen = "UNL-137/219", MatchState? initial = null)
    {
        var game = new Driver(initial ?? Position(no, chosen));
        await game.Act("P1", new PlayCardCommand("IVERN", no, []));
        Assert.Contains("IVERN", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Single(game.State.StackItems);
        Assert.DoesNotContain("SEEN-UNIT", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await game.Top();
        Assert.Equal("DECK_EFFECT", game.State.PendingCardChoice!.ChoiceWindow);
        return game;
    }

    [Theory, InlineData(Ivern), InlineData("UNL-051a/219")]
    public async Task TheIndependentPlaySkillLooksThreePrivatelyThenRevealsDrawsAndRecyclesBeforeTheBoon(string no)
    {
        var game = await Begin(no);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(["SEEN-UNIT", "SEEN-SPELL", "SEEN-GEAR"], choice.ContextObjectIds);
        Assert.Equal(["SEEN-UNIT"], choice.LegalObjectIds);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        var prompt = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.ChooseCards);
        var viewed = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(prompt.Metadata!["viewedCards"]);
        Assert.Equal(choice.ContextObjectIds, viewed.Select(card => card.Id));
        Assert.All(viewed, card => Assert.Equal(CoreRuleEngine.DeckChoiceLabel(game.State, card.Id), card.Label));
        Assert.Equal("择日再战", Assert.Single(viewed, card => card.Id == "SEEN-SPELL").Label);
        Assert.DoesNotContain("SEEN-SPELL", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        Assert.DoesNotContain("SEEN-SPELL", JsonSerializer.Serialize(game.PromptFor("P2")));
        await Choose(game, "SEEN-UNIT");
        Assert.Contains("SEEN-UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("TAIL", game.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(new[] { "SEEN-SPELL", "SEEN-GEAR" }.Order(), game.State.PlayerZones["P1"].MainDeck.Skip(1).Order());
        var revealed = Assert.Single(game.Events(), e => e.Event.Kind == "CARD_REVEALED");
        Assert.Equal("SEEN-UNIT", revealed.Event.Payload["objectId"]);
        Assert.DoesNotContain("SEEN-SPELL", JsonSerializer.Serialize(game.Events().Select(e => e.Event.Payload)));
        Assert.Equal("TRIGGER_CONFIRMATION", game.State.PendingCardChoice!.ChoiceWindow);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await Choose(game, "ALLY");
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("UNL-154/219"), InlineData("UNL-036/219"), InlineData("UNL-047/219"), InlineData("UNL-137/219")]
    public async Task EachPrintedAnimalTraitEarnsExactlyOneIndependentBoon(string selected)
    {
        var game = await Begin(chosen: selected); await Choose(game, "SEEN-UNIT");
        Assert.Single(game.State.StackItems); await Choose(game, "ALLY"); await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Single(game.Events(), e => e.Event.Kind == CoreRuleEngine.DeckSelectionBoonCreatedEvent);
        Assert.Empty(game.State.StackItems); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task RevealingAUnitWithoutTheFourTraitsStillDrawsItWithoutABoon()
    {
        var game = await Begin(chosen: "OGN·219/298"); await Choose(game, "SEEN-UNIT");
        Assert.Contains("SEEN-UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DecliningTheRevealRecyclesAllThreeWithoutRevealingThemOrCreatingABoon()
    {
        var game = await Begin(); await Choose(game);
        Assert.Equal("TAIL", game.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal(new[] { "SEEN-UNIT", "SEEN-SPELL", "SEEN-GEAR" }.Order(), game.State.PlayerZones["P1"].MainDeck.Skip(1).Order());
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_REVEALED" or CoreRuleEngine.DeckSelectionBoonCreatedEvent);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.ColdCheckpoint();
    }

    [Theory, InlineData("spell"), InlineData("gear"), InlineData("outside-three"), InlineData("duplicate"), InlineData("other-player")]
    public async Task IllegalPrivateSelectionsRejectAtomicallyAndDoNotInvalidateTheRealChoice(string invalid)
    {
        var game = await Begin(); var choice = game.State.PendingCardChoice!;
        string[] ids = invalid switch { "spell" => ["SEEN-SPELL"], "gear" => ["SEEN-GEAR"],
            "outside-three" => ["TAIL"], "duplicate" => ["SEEN-UNIT", "SEEN-UNIT"], _ => ["SEEN-UNIT"] };
        await RadGearTestPosition.Reject(game.State, invalid == "other-player" ? "P2" : "P1",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        await Choose(game, "SEEN-UNIT"); await Choose(game, "ALLY"); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningTheSourceBeforeTheLookDoesNotCancelThePrivateSelectionOrItsNestedBoon()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("IVERN", Ivern, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["IVERN"])); await game.Top();
        Assert.Contains("IVERN", game.State.PlayerZones["P1"].Hand);
        var firstContinuationEvent = game.Events().Length;
        await game.Top(); await Choose(game, "SEEN-UNIT");
        Assert.DoesNotContain("IVERN", game.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("\"IVERN\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await Choose(game, "ALLY"); await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain("\"IVERN\"", JsonSerializer.Serialize(game.PromptFor("P2")));
        Assert.DoesNotContain("\"IVERN\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick,
            game.Events().Length, [], game.State);
        Assert.DoesNotContain("\"IVERN\"", JsonSerializer.Serialize(spectator));
        Assert.All(game.Events().Skip(firstContinuationEvent),
            entry => Assert.DoesNotContain("\"IVERN\"", JsonSerializer.Serialize(entry.Event.Payload)));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("erase-skill"), InlineData("erase-fact"), InlineData("trait"), InlineData("card"),
        InlineData("tick"), InlineData("parent")]
    public async Task OriginalAcceptedRevealAndNestedSkillHistoryCannotBeCoherentlyRewritten(string mutation)
    {
        var game = await Begin(); await Choose(game, "SEEN-UNIT"); await Choose(game, "ALLY");
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!; var fact = context.RevealedDeckSelection!;
        var altered = item with { UnitEvent = context with { RevealedDeckSelection = mutation switch {
            "erase-fact" => null, "trait" => fact with { Traits = [CardObjectTags.UnitCard, "猫科"] },
            "card" => fact with { Card = new("TAIL", 0) }, "tick" => fact with { RevealedTick = fact.RevealedTick - 1 },
            "parent" => fact with { ParentStackItemId = "invented-parent" }, _ => fact } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game,
            game.State with { StackItems = mutation == "erase-skill" ? [] : [altered] }));
        await game.Top(); await game.ColdCheckpoint();
    }
    [Theory, InlineData(Ivern), InlineData("UNL-051a/219")]
    public async Task AnActualOwnTurnHoldLooksAndDrawsBeforeTheOrdinaryChannelAndDraw(string no)
    {
        var initial = ExactOtherFriendlyHoldTests.Position(no, 0);
        initial = RadGearTestPosition.Put(initial, "HOST", no, "P1", "BATTLEFIELD", "F");
        foreach (var id in initial.PlayerZones["P1"].MainDeck.ToArray())
            initial = RadUnitEffectTestFixture.Place(initial, id, "P1", "GRAVEYARD");
        foreach (var (id, card) in new[] { ("HOLD-ANIMAL", "UNL-137/219"), ("HOLD-SPELL", "OGN·104/298"),
            ("HOLD-GEAR", "SFD·022/221"), ("HOLD-TAIL", "OGN·219/298") })
            initial = RadGearTestPosition.Put(initial, id, card, "P1", "MAIN_DECK");
        var game = new Driver(initial);
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Equal("PLAY_OR_HOLD", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Top();
        Assert.Equal("DECK_EFFECT", game.State.PendingCardChoice!.ChoiceWindow);
        await Choose(game, "HOLD-ANIMAL");
        Assert.Contains("HOLD-ANIMAL", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.NotNull(game.State.TurnStartStep);
        await Choose(game, "HOST"); await game.Top();
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["HOST"]));
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.Equal(1, game.State.PlayerScores["P1"]); await game.ColdCheckpoint();
    }

    [Theory, InlineData(0), InlineData(1), InlineData(2)]
    public async Task ShortDecksUseOnlyTheActuallyLookedCardsAndDoNotBurnOut(int count)
    {
        var initial = Position();
        foreach (var id in initial.PlayerZones["P1"].MainDeck.Skip(count).ToArray())
            initial = RadUnitEffectTestFixture.Place(initial, id, "P1", "GRAVEYARD");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("IVERN", Ivern, [])); await game.Top();
        if (count == 0) Assert.Null(game.State.PendingCardChoice);
        else
        {
            Assert.Equal(count, game.State.PendingCardChoice!.ContextObjectIds.Count);
            await Choose(game, "SEEN-UNIT"); await Choose(game, "ALLY"); await game.Top();
            Assert.Contains("SEEN-UNIT", game.State.PlayerZones["P1"].Hand);
            Assert.Equal(count - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        }
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind.Contains("BURN", StringComparison.Ordinal));
        Assert.Empty(game.State.StackItems); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task RecyclingTheChosenTopBeforeRevealStillRevealsTheOriginalChosenAnimal()
    {
        var initial = RadGearTestPosition.Put(Position(), "SEED", "SFD·018/221", "P1", "BASE");
        var game = await Begin(initial: initial);
        await Choose(game, "SEEN-UNIT");
        var before = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal(CoreRuleEngine.BeforeDeckRevealChoiceKind, before.Kind);
        await game.Act("P1", new PayCostCommand(before.Id, CoreRuleEngine.RuleChoiceWindow, ["RECYCLE"]));
        Assert.Contains("SEEN-UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("SEEN-UNIT", Assert.Single(game.Events(), e => e.Event.Kind == "CARD_REVEALED").Event.Payload["objectId"]);
        await Choose(game, "ALLY"); await game.Top();
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["ALLY"])); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ARealNocturneCanDeclineItsPrivateLookThenBanishAtRevealWithoutAnAnimalBoon()
    {
        var game = new Driver(Position(chosen: DeckObservationPlayTests.Nocturne));
        await game.Act("P1", new PlayCardCommand("IVERN", Ivern, [])); await game.Top();
        var look = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("DECK_OBSERVATION_BANISH", look.Kind);
        Assert.False(look.Preview!.DeckObservationView!.Public);
        await game.Act("P1", new PayCostCommand(look.Id, CoreRuleEngine.RuleChoiceWindow, ["KEEP"]));
        await Choose(game, "SEEN-UNIT");
        var reveal = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("DECK_OBSERVATION_BANISH", reveal.Kind);
        Assert.True(reveal.Preview!.DeckObservationView!.Public);
        await game.Act("P1", new PayCostCommand(reveal.Id, CoreRuleEngine.RuleChoiceWindow, ["BANISH"]));
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_REVEALED");
        Assert.DoesNotContain("SEEN-UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == CoreRuleEngine.DeckSelectionBoonCreatedEvent);
        var permission = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        await game.Act("P1", new ChooseCardsCommand(permission.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("SEEN-UNIT", game.State.PlayerZones["P1"].Banished);
        await game.ColdCheckpoint();
    }
    [Fact]
    public async Task AnObservedPrivateCardKeepsItsOriginalSourceEvenWhenAnotherRealIvernHasTheSameValidShape()
    {
        var initial = RadGearTestPosition.Put(Position(), "OTHER-IVERN", Ivern, "P1", "HAND");
        // The first Ivern declines its original top three, leaving the fourth
        // card as the real next top. Both sources are played through the journal.
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TAIL"] = OfficialGarenConquestBattlefieldCountTests.Printed("TAIL", DeckObservationPlayTests.Nocturne, "P1") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("OTHER-IVERN", Ivern, [])); await game.Top();
        await Choose(game);
        Assert.Equal("TAIL", game.State.PlayerZones["P1"].MainDeck[0]);
        await game.Act("P1", new PlayCardCommand("IVERN", Ivern, [])); await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("DECK_OBSERVATION_BANISH", request.Kind);
        await game.Act("P1", new PayCostCommand(request.Id, CoreRuleEngine.RuleChoiceWindow, ["BANISH"]));
        var original = Assert.Single(game.State.ObservedDeckPlays!);
        var forged = original with { Instruction = original.Instruction with {
            Parent = original.Instruction.Parent with { SourceObjectId = "OTHER-IVERN" } } };
        var coordinated = game.State with {
            ObservedDeckPlays = [forged],
            StackItems = game.State.StackItems.Select(item => item.ObservedDeckPlay?.Id == original.Id
                ? item with { ObservedDeckPlay = forged } : item).ToArray() };
        Assert.True(CoreRuleEngine.ValidObservedDeckPlay(coordinated, forged));
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, coordinated),
            error => error.Contains("original accepted observation", StringComparison.Ordinal));
        await Choose(game);
        var permission = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        await game.Act("P1", new ChooseCardsCommand(permission.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("TAIL", game.State.PlayerZones["P1"].Banished);
        await game.ColdCheckpoint();
    }
}
