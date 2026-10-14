using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RecycledCardBoonTests
{
    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task OwnPrintedPredictRecycleOpensBoonTargetBeforeIndependentResponses(string card)
    {
        var initial = GrantedPredictTests.Hand(card);
        var played = await Act(initial, "P1", new PlayCardCommand("MECH", card, []));
        var opened = await Top(played.State); var insight = opened.State.PendingCardChoice!;
        var top = Assert.Single(insight.LegalObjectIds);
        var recycled = await Act(opened.State, "P1", new ChooseCardsCommand(insight.ChoiceId, insight.ChoiceWindow, [top]));
        var target = Assert.IsType<PendingCardChoiceState>(recycled.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", target.ChoiceWindow); Assert.Equal(1, target.RequiredCount);
        Assert.Contains("MECH", target.LegalObjectIds);
        Assert.DoesNotContain(CardObjectTags.Boon, recycled.State.CardObjects["MECH"].Tags); Restore(recycled.State);
        var chosen = await Act(recycled.State, "P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["MECH"]));
        Assert.DoesNotContain(CardObjectTags.Boon, chosen.State.CardObjects["MECH"].Tags); Restore(chosen.State);
        var done = await Top(chosen.State);
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["MECH"].Tags); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task OfficialHookFaqConfirmsNewPermanentBeforeChoosingItForRecycleBoon(string card)
    {
        var initial = AddKarma(HookDeckPlayTests.Position(), card);
        var driver = HookDeckPlayTests.Driver(initial, "karma-hook-" + card);
        await HookDeckPlayTests.Activate(driver); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "HOOK_TOP");
        Assert.NotNull(driver.State.PendingEffectPlay);
        Assert.Equal("STACK", driver.State.ObjectLocations["HOOK_TOP"].Zone);
        await driver.Act("P1", new PlayCardCommand("HOOK_TOP", "OGN·096/298", []));
        Assert.Contains("HOOK_TOP", driver.State.PlayerZones["P1"].Base);
        var target = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", target.ChoiceWindow); Assert.Contains("HOOK_TOP", target.LegalObjectIds);
        Assert.DoesNotContain(CardObjectTags.Boon, driver.State.CardObjects["HOOK_TOP"].Tags);
        await driver.Act("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["HOOK_TOP"]));
        await driver.Top(); Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects["HOOK_TOP"].Tags);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task KeepingTheObservedCardDoesNotCreateAnyRecycleTrigger(string card)
    {
        var initial = GrantedPredictTests.Hand(card);
        var opened = await Top((await Act(initial, "P1", new PlayCardCommand("MECH", card, []))).State);
        var choice = opened.State.PendingCardChoice!;
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue); Assert.Null(done.State.PendingCardChoice);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["MECH"].Tags); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task RuneResourceRecycleIsExcludedByTheOfficialErrata(string card)
    {
        var initial = AddKarma(ConquestTokenTriggerTests.Position(), card);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["RUNE"] = initial.CardObjects["RUNE"] with { CardNo = "UNL-R03", IsExhausted = false, Tags = [CardObjectTags.RuneCard, "COLOR:blue"] } } };
        var done = await Act(initial, "P1", new RecycleRuneCommand("RUNE"));
        Assert.Contains("RUNE", done.State.PlayerZones["P1"].RuneDeck);
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue); Assert.Null(done.State.PendingCardChoice);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["KARMA"].Tags); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task RecyclingTwoCardsInOneInstructionCreatesOneBoonSkill(string card)
    {
        var initial = AddKarma(OfficialInsightSourceTests.Blossom(), card);
        var active = await Act(initial, "P1", new ActivateAbilityCommand("B", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, []));
        var opened = await Top(active.State); var insight = opened.State.PendingCardChoice!;
        var recycled = await Act(opened.State, "P1", new ChooseCardsCommand(insight.ChoiceId, insight.ChoiceWindow, ["D1", "D2"]));
        var item = Assert.Single(recycled.State.StackItems);
        Assert.Equal(2, item.FieldContext!.RecycledBoon!.CardCount); Restore(recycled.State);
        var choice = recycled.State.PendingCardChoice!;
        var chosen = await Act(recycled.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["KARMA"]));
        var done = await Top(chosen.State);
        Assert.Single(done.Events, e => e.Kind == "BOON_GRANTED"); Assert.Equal(7, done.State.CardObjects["KARMA"].Power); Restore(done.State);
    }

    [Fact]
    public async Task IndependentRecoveryRejectsForgedActorCountTickFaceAndPrinting()
    {
        var state = await OpenOwnRecycle("OGN·235/298"); var item = Assert.Single(state.StackItems);
        var context = item.FieldContext!; var origin = context.RecycledBoon!;
        foreach (var changed in new[] { context with { RecycledBoon = null },
            context with { RecycledBoon = origin with { PlayerId = "P2" } },
            context with { RecycledBoon = origin with { CardCount = 0 } },
            context with { RecycledBoon = origin with { CapturedTick = state.Tick + 1 } },
            context with { RecycledBoon = origin with { FaceRevision = origin.FaceRevision + 1 } },
            context with { CardNo = "SFD·237/221" } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item with { FieldContext = changed }] }),
                e => e.Contains("field trigger", StringComparison.Ordinal));
        Restore(state);
    }

    [Theory]
    [InlineData("P2", "MECH")]
    [InlineData("P1", "D1")]
    [InlineData("P1", "")]
    public async Task WrongActorPrivateCardAndMandatoryDeclineRejectWithoutChangingState(string player, string target)
    {
        var state = await OpenOwnRecycle("OGN·235/298"); var choice = state.PendingCardChoice!;
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, target.Length == 0 ? [] : [target]);
        var bad = await new CoreRuleEngine().ResolveAsync(state, new("invalid-boon", player, command.CmdType), command, default);
        Assert.False(bad.Accepted); Assert.Empty(bad.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(bad.State));
    }

    [Fact]
    public async Task AttachedPrintedCopyCreatesASeparateRecycleSkillAndDoesNotStackExistingBoon()
    {
        var attached = (await AttachedRuleTextTests.Attach(AttachedRuleTextTests.Position("OGN·235/298", 0))).State;
        var bloom = OfficialInsightSourceTests.Blossom();
        var oldDeck = attached.PlayerZones["P1"].MainDeck.ToHashSet();
        var cards = attached.CardObjects.Where(p => !oldDeck.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        foreach (var id in new[] { "B", "D1", "D2", "D3" }) cards[id] = bloom.CardObjects[id];
        var initial = attached with { CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(attached.PlayerZones) {
                ["P1"] = attached.PlayerZones["P1"] with { Base = attached.PlayerZones["P1"].Base.Append("B").ToArray(), MainDeck = ["D1", "D2", "D3"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(attached.ObjectLocations.Where(p => !oldDeck.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value)) {
                ["B"] = new("P1", "BASE"), ["D1"] = new("P1", "MAIN_DECK"), ["D2"] = new("P1", "MAIN_DECK"), ["D3"] = new("P1", "MAIN_DECK") } };
        var opened = await Top((await Act(initial, "P1", new ActivateAbilityCommand("B", P4ActivatedAbilityCatalog.ScryingBlossomAbilityId, []))).State);
        var insight = opened.State.PendingCardChoice!;
        var recycled = await Act(opened.State, "P1", new ChooseCardsCommand(insight.ChoiceId, insight.ChoiceWindow, ["D1"]));
        Assert.Equal(2, recycled.State.TriggerQueue.Count);
        Assert.Single(recycled.State.TriggerQueue, t => t.FieldContext?.Origin is not null); Restore(recycled.State);
        var current = (await Act(recycled.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: recycled.State.TriggerQueue.Select(t => t.TriggerId).ToArray()))).State;
        for (var i = 0; i < 2; i++) {
            var choice = current.PendingCardChoice!;
            current = (await Act(current, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]))).State; Restore(current);
        }
        var first = await Top(current); var second = await Top(first.State);
        Assert.Equal(7, second.State.CardObjects["HOST"].Power);
        Assert.True((bool)Assert.Single(second.Events, e => e.Kind == "BOON_GRANTED").Payload["alreadyHadBoon"]!); Restore(second.State);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task RecastRecycleCostOnlyObservesTheActorsOwnMainDeck(string owner)
    {
        var initial = AddKarma(AddKarma(OfficialMechanicalRecastTests.Position(), "OGN·235/298"),
            "SFD·237/221", "P2", "OTHER_KARMA");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["B"] = initial.CardObjects["B"] with { OwnerId = owner } } };
        var conquered = await ConquestLifecycleRegressionTests.Conquer(initial);
        var opened = await Top(conquered.State); var recycle = opened.State.PendingCardChoice!;
        var paid = await Act(opened.State, "P1", new ChooseCardsCommand(recycle.ChoiceId, recycle.ChoiceWindow, ["B"]));
        Assert.Contains("B", paid.State.PlayerZones[owner].MainDeck);
        Assert.Equal(owner == "P1" ? 1 : 0, paid.State.TriggerQueue.Count(t => t.FieldContext?.Kind == "RECYCLE_BOON"));
        Assert.DoesNotContain(paid.State.TriggerQueue, t => t.SourceObjectId == "OTHER_KARMA"); Restore(paid.State);
    }

    [Fact]
    public async Task MandatoryOpponentTopSelectionRejectsDeclineWithoutRecycleOrBoon()
    {
        var initial = AddKarma(AddKarma(DeckPlaySequenceTests.Position(DeckPlaySequenceTests.BlindFury), "OGN·235/298"),
            "SFD·237/221", "P2", "OTHER_KARMA");
        var driver = DeckPlaySequenceTests.Driver(initial, "karma-opponent-deck-decline");
        await driver.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BlindFury, [])); await driver.Top();
        var choice = driver.State.PendingCardChoice!;
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []);
        var bad = await new CoreRuleEngine().ResolveAsync(driver.State, new("invalid-decline", "P1", command.CmdType), command, default);
        Assert.False(bad.Accepted); Assert.Empty(bad.Events); Assert.Equal(MatchStateHasher.Hash(driver.State), MatchStateHasher.Hash(bad.State));
    }

    internal static async Task<MatchState> OpenOwnRecycle(string card)
    {
        var opened = await Top((await Act(GrantedPredictTests.Hand(card), "P1", new PlayCardCommand("MECH", card, []))).State);
        var choice = opened.State.PendingCardChoice!;
        return (await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]))).State;
    }

    internal static MatchState AddKarma(MatchState state, string card, string player = "P1", string id = "KARMA")
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: card, ownerId: player, controllerId: player, power: 6, tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [player] = state.PlayerZones[player] with { Base = state.PlayerZones[player].Base.Append(id).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BASE") } };
}
