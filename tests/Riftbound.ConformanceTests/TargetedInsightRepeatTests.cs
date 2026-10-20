using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed class TargetedInsightRepeatTests
{
    [Fact]
    public async Task ThreeEmptyInsightsFinishWithoutAChoiceDrawOrBurnout()
    {
        var initial = OfficialCounterRepeatTests.Position("UNL-131/219");
        initial = initial with { CardObjects = initial.CardObjects.Where(p => p.Key is not ("D1" or "D2")).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key is not ("D1" or "D2")).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [] } },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 },
            UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var done = await ResolveTop((await Act(initial, "P1", new PlayCardCommand("C", "UNL-131/219", ["S1"],
            OptionalCosts: ["ECHO", "ECHO:GRANTED:2"], RepeatChoices: [new("", ["S2"]), new("", ["S1"])]))).State);
        Assert.Null(done.State.PendingCardChoice); Assert.Empty(done.State.StackItems);
        Assert.Equal(MatchStatuses.InProgress, done.State.Status); Assert.Equal(7, done.State.PlayerScores["P2"]);
        Assert.Equal(3, done.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.DoesNotContain(done.Events, e => e.Kind.Contains("BURN", StringComparison.Ordinal) || e.Kind == "CARD_DRAWN");
        Assert.Single(done.State.PlayerZones["P1"].Graveyard, id => id == "C"); Restore(done.State);
    }

    [Theory]
    [InlineData("UNL-063/219")]
    [InlineData("UNL-131/219")]
    public async Task AnInvalidLaterDeclarationCannotSpendAnyCostOrConsumeTheGrant(string card)
    {
        var state = Position(card);
        var command = new PlayCardCommand("C", card, card == "UNL-063/219" ? ["U"] : ["S1"],
            OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["NOT-A-LEGAL-TARGET"])]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-later", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("S1", 1)]
    [InlineData("S2", 2)]
    public async Task EachCounterExecutionThenPerformsItsOwnInsightEvenWhenItsLaterTargetIsGone(string laterTarget, int countered)
    {
        var initial = OfficialCounterRepeatTests.Position("UNL-131/219");
        var command = new PlayCardCommand("C", "UNL-131/219", ["S1"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", [laterTarget])]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var played = await Act(initial, "P1", command);
        var declared = played.State.StackItems.Last();
        Assert.Equal(2, declared.RepeatExecutions!.Count);
        Assert.Equal(new[] { "S1" }, declared.RepeatExecutions[0].TargetObjectIds);
        Assert.Equal(new[] { laterTarget }, declared.RepeatExecutions[1].TargetObjectIds);
        var opened = await ResolveTop(played.State);
        Assert.Equal(new[] { "SPELL1" }, opened.State.PlayerZones["P2"].Hand);
        var first = opened.State.PendingCardChoice!;
        Assert.Equal("INSIGHT", first.ChoiceWindow); Assert.Equal(new[] { "D1" }, first.ContextObjectIds);
        Restore(opened.State);
        var next = await Act(opened.State, "P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, ["D1"]));
        var second = next.State.PendingCardChoice!;
        Assert.Equal("INSIGHT", second.ChoiceWindow); Assert.NotEqual(first.ChoiceId, second.ChoiceId);
        Assert.Equal(1, second.DeckContext!.ExecutionIndex); Assert.Equal(new[] { "D2" }, second.ContextObjectIds);
        Assert.Equal(countered, next.State.PlayerZones["P2"].Hand.Count);
        Assert.DoesNotContain("C", next.State.PlayerZones["P1"].Graveyard);
        Restore(next.State);
        var done = await Act(next.State, "P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, []));
        Assert.Null(done.State.PendingCardChoice);
        Assert.Equal(new[] { "D2", "D1" }, done.State.PlayerZones["P1"].MainDeck);
        Assert.Single(done.State.PlayerZones["P1"].Graveyard, id => id == "C");
        Assert.Equal(1, done.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(countered == 1, done.State.StackItems.Any(s => s.StackItemId == "S2"));
        Restore(done.State);
    }

    [Theory]
    [InlineData("U", 2, 9)]
    [InlineData("V", 6, 5)]
    public async Task EachPowerModifierUsesItsOwnDeclaredTargetBeforeTheMatchingInsight(string laterTarget, int firstPower, int secondPower)
    {
        var initial = Position("UNL-063/219");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["U"] = initial.CardObjects["U"] with { Power = 10 },
                ["V"] = initial.CardObjects["U"] with { ObjectId = "V", Power = 9 } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Base = ["U", "V"] } } };
        var command = new PlayCardCommand("C", "UNL-063/219", ["U"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", [laterTarget])]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var opened = await ResolveTop((await Act(initial, "P1", command)).State);
        Assert.Equal(6, opened.State.CardObjects["U"].Power); Assert.Equal(9, opened.State.CardObjects["V"].Power);
        Restore(opened.State);
        var first = opened.State.PendingCardChoice!;
        var next = await Act(opened.State, "P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []));
        Assert.Equal(firstPower, next.State.CardObjects["U"].Power); Assert.Equal(secondPower, next.State.CardObjects["V"].Power);
        var second = next.State.PendingCardChoice!;
        Assert.NotEqual(first.ChoiceId, second.ChoiceId); Assert.Equal(1, second.DeckContext!.ExecutionIndex);
        Restore(next.State);
        var done = await Act(next.State, "P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, ["D1"]));
        Assert.Null(done.State.PendingCardChoice); Assert.Contains("C", done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { "D2", "D1" }, done.State.PlayerZones["P1"].MainDeck); Restore(done.State);
    }

    [Fact]
    public async Task ReversalKeepsDeclaredExecutionsButMovesBothPrivateInsightsToTheActualController()
    {
        var initial = AttackDamageDistributionTests.Spell(OfficialCounterRepeatTests.Position("UNL-131/219"), "REVERSAL", "OGN·080/298", "P2");
        var cards = initial.CardObjects.ToDictionary();
        foreach (var id in new[] { "E1", "E2" }) cards[id] = new(id, cardNo: "OGN·048/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]);
        initial = initial with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["E1", "E2"] } } };
        var state = (await Act(initial, "P1", new PlayCardCommand("C", "UNL-131/219", ["S1"],
            OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["S2"])]))).State;
        var parent = state.StackItems.Last().StackItemId;
        state = (await Act(state, "P1", new PassPriorityCommand())).State;
        state = (await Act(state, "P2", new PlayCardCommand("REVERSAL", "OGN·080/298", [parent], OptionalCosts: []))).State;
        state = (await ResolveTop(state)).State;
        state = await RebuttalRulesTests.Choose(state, "KEEP_ALL");
        state = (await ResolveTop(state)).State;
        for (var execution = 0; execution < 2; execution++)
        {
            var choice = state.PendingCardChoice!;
            Assert.Equal("P2", choice.PlayerId); Assert.Equal(execution, choice.DeckContext!.ExecutionIndex);
            Assert.Equal(new[] { execution == 0 ? "E1" : "E2" }, choice.ContextObjectIds);
            Assert.DoesNotContain("\"E1\"", System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state)["P1"]));
            Assert.DoesNotContain("\"E2\"", System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P1"]));
            Restore(state);
            state = (await Act(state, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, execution == 0 ? ["E1"] : []))).State;
        }
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, state.PlayerZones["P1"].MainDeck);
        Assert.Contains("C", state.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("C", state.PlayerZones["P2"].Graveyard);
        Assert.Contains("SPELL1", state.PlayerZones["P2"].Hand); Assert.Contains("SPELL2", state.PlayerZones["P2"].Hand);
        Restore(state);
    }

    [Theory]
    [InlineData("MISSING_PLAN")] [InlineData("SHORT_PLAN")] [InlineData("INDEX")]
    [InlineData("OLD_CHOICE")] [InlineData("OLD_PROGRESS")] [InlineData("WRONG_KIND")]
    public async Task IndependentRecoveryRejectsTargetPlanAndPrivateProgressTampering(string branch)
    {
        var initial = OfficialCounterRepeatTests.Position("UNL-131/219");
        var state = (await ResolveTop((await Act(initial, "P1", new PlayCardCommand("C", "UNL-131/219", ["S1"],
            OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["S2"])]))).State)).State;
        var first = state.PendingCardChoice!;
        state = (await Act(state, "P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []))).State;
        var item = state.StackItems.Last(); var choice = state.PendingCardChoice!;
        if (branch == "MISSING_PLAN") item = item with { RepeatExecutions = null };
        if (branch == "SHORT_PLAN") item = item with { RepeatExecutions = [item.RepeatExecutions![0]] };
        if (branch == "INDEX") choice = choice with { DeckContext = choice.DeckContext! with { ExecutionIndex = 0 } };
        if (branch == "OLD_CHOICE") choice = choice with { ChoiceId = first.ChoiceId };
        if (branch == "OLD_PROGRESS") item = item with { CompletedDeckExecutions = 0 };
        if (branch == "WRONG_KIND") item = item with { EffectKind = "NIGHTFALL_LULLABY_COUNTER_SPELL_AND_SPELL_LOCK", CardNo = "UNL-190/219" };
        Assert.NotEmpty(Errors(state with { StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(item).ToArray(), PendingCardChoice = choice }));
        Restore(state);
    }

    [Theory]
    [InlineData("UNL-063/219", 1)]
    [InlineData("UNL-131/219", 2)]
    public async Task ProductionDeclaredTargetsAndPrivateChoicesColdRecoverDeduplicateAndFullyReplay(string card, int echoes)
    {
        var initial = Position(card) with { Tick = 0 };
        if (card == "UNL-063/219")
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["U"] = initial.CardObjects["U"] with { Power = 10 },
                    ["V"] = initial.CardObjects["U"] with { ObjectId = "V", Power = 9 } },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Base = ["U", "V"] } } };
        if (echoes == 2) initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_TARGETED_INSIGHT_EVIDENCE");
        var branch = card == "UNL-063/219" ? "eclipse" : "abandon";
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, branch));
        driver.Export("initial.json", initial);
        await driver.Act("P1", new PlayCardCommand("C", card, card == "UNL-063/219" ? ["U"] : ["S1"],
            OptionalCosts: echoes == 1 ? ["ECHO"] : ["ECHO", "ECHO:GRANTED:2"],
            RepeatChoices: card == "UNL-063/219" ? [new("", ["V"])] : [new("", ["S2"]), new("", ["S1"])]));
        await driver.Top();
        for (var execution = 0; execution <= echoes; execution++)
        {
            var choice = driver.State.PendingCardChoice!;
            Assert.Equal(execution, choice.DeckContext!.ExecutionIndex);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                card == "UNL-131/219" ? execution < 2 ? choice.ContextObjectIds : [] : execution == 1 ? choice.ContextObjectIds : []));
        }
        Assert.Null(driver.State.PendingCardChoice); Assert.Empty(driver.State.StackItems);
        Assert.Equal(1, driver.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Single(driver.State.PlayerZones["P1"].Graveyard, id => id == "C");
        if (card == "UNL-131/219")
        { Assert.Equal(new[] { "SPELL1", "SPELL2" }, driver.State.PlayerZones["P2"].Hand); Assert.Equal(new[] { "D1", "D2" }, driver.State.PlayerZones["P1"].MainDeck); }
        else
        { Assert.Equal(6, driver.State.CardObjects["U"].Power); Assert.Equal(5, driver.State.CardObjects["V"].Power); Assert.Equal(new[] { "D2", "D1" }, driver.State.PlayerZones["P1"].MainDeck); }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.True(driver.NativeCommands == 0 || driver.NativeCommands == driver.Steps.Count);
        driver.Export("steps.json", Enumerable.Range(0, driver.Steps.Count).Select(i => "defend-step-" + i).ToArray());
        driver.Export("receipt.json", new { card, echoes, native = driver.NativeCommands, commands = driver.Commands().Length,
            events = driver.Events().Length, coldRecoveries = driver.Steps.Count, finalHash = MatchStateHasher.Hash(driver.State), replay });
    }
}
