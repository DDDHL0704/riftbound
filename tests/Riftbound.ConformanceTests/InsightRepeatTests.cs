using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed class InsightRepeatTests
{
    private const string Card = "VEN·056";

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EachPaidEchoCompletesItsPrivateInsightAndTwoDrawsBeforeTheNextExecution(int echoes)
    {
        var initial = Position(echoes);
        var driver = DeckObservationPlayTests.Driver(initial, "insight-repeat-" + echoes);
        var command = new PlayCardCommand("SPELL", Card, [], OptionalCosts: Costs(echoes));
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(7 * (echoes + 1), quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower);
        await driver.Act("P1", command);
        Assert.Equal(1, driver.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Single(driver.State.CardPlays.Finalized);
        await driver.Top();
        var seenChoices = new HashSet<string>(StringComparer.Ordinal);
        var expectedDeck = initial.PlayerZones["P1"].MainDeck.ToList();
        var expectedHand = new List<string>();
        for (var execution = 0; execution <= echoes; execution++)
        {
            var item = Assert.Single(driver.State.StackItems);
            Assert.Equal(execution, item.CompletedDeckExecutions);
            Assert.Equal(echoes + 1, item.EffectRepeatCount);
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("INSIGHT", choice.ChoiceWindow);
            Assert.True(seenChoices.Add(choice.ChoiceId));
            Assert.Equal(execution, choice.DeckContext!.ExecutionIndex);
            Assert.Equal(expectedDeck.Take(5), choice.ContextObjectIds);
            Assert.Equal(expectedHand, driver.State.PlayerZones["P1"].Hand);
            Assert.Empty(driver.State.CardPlays.Resolved);
            Assert.DoesNotContain("SPELL", driver.State.PlayerZones["P1"].Graveyard);
            Private(driver.State); Restore(driver.State);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("INSIGHT_ORDER", choice.ChoiceWindow);
            Assert.True(seenChoices.Add(choice.ChoiceId));
            Assert.Equal(execution, choice.DeckContext!.ExecutionIndex);
            Assert.Equal(expectedHand, driver.State.PlayerZones["P1"].Hand);
            var ordered = expectedDeck.Take(5).Reverse().ToArray();
            expectedDeck = ordered.Concat(expectedDeck.Skip(5)).ToList();
            expectedHand.AddRange(expectedDeck.Take(2)); expectedDeck.RemoveRange(0, 2);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ordered));
            Assert.Equal(expectedHand, driver.State.PlayerZones["P1"].Hand);
            Assert.Equal(expectedDeck, driver.State.PlayerZones["P1"].MainDeck);
        }
        Assert.Null(driver.State.PendingCardChoice); Assert.Empty(driver.State.StackItems);
        Assert.Single(driver.State.CardPlays.Finalized); Assert.Single(driver.State.CardPlays.Resolved);
        Assert.Single(driver.State.PlayerZones["P1"].Graveyard, id => id == "SPELL");
        Assert.Equal(echoes + 1, driver.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.All(driver.Events().Where(e => e.Event.Kind == "CARD_DRAWN"), e => Assert.Equal(2, e.Event.Payload["count"]));
        Assert.Equal(echoes + 1, driver.Events().Count(e => e.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("KEEP", "D5,D4,D7,D6")]
    [InlineData("SOME", "D5,D3,D9,D8")]
    [InlineData("ALL", "D6,D7,D12,D11")]
    public async Task TheNextExecutionObservesTheActualDeckAfterTheFirstRecycleOrderAndDraw(string branch, string hand)
    {
        var initial = Position(); var driver = DeckObservationPlayTests.Driver(initial, "insight-recycle-" + branch);
        await driver.Act("P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: ["ECHO"]));
        await driver.Top();
        var first = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow,
            branch == "ALL" ? first.ContextObjectIds : branch == "SOME" ? ["D2", "D4"] : []));
        if (driver.State.PendingCardChoice is { ChoiceWindow: "INSIGHT_ORDER" } order)
            await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, order.ContextObjectIds.Reverse().ToArray()));
        var second = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("INSIGHT", second.ChoiceWindow); Assert.Equal(1, second.DeckContext!.ExecutionIndex);
        Assert.Equal(branch switch { "ALL" => new[] { "D8", "D9", "D10", "D11", "D12" },
            "SOME" => ["D1", "D6", "D7", "D8", "D9"], _ => ["D3", "D2", "D1", "D6", "D7"] }, second.ContextObjectIds);
        var stale = new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []);
        await Rejected(driver.State, "P1", stale);
        await Rejected(driver.State, "P2", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, []));
        await Rejected(driver.State, "P1", new PassPriorityCommand());
        await driver.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, []));
        order = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, order.ContextObjectIds.Reverse().ToArray()));
        Assert.Equal(hand.Split(','), driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(branch == "KEEP" ? initial.RngCursor : initial.RngCursor + 1, driver.State.RngCursor);
        Assert.Equal(Enumerable.Range(1, 12).Select(i => "D" + i).Order(), driver.State.PlayerZones["P1"].MainDeck.Concat(driver.State.PlayerZones["P1"].Hand).Order());
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task AWinningDrawStopsFurtherRepeatsWithoutInventingASecondPlay(int count)
    {
        var initial = Position(deckCards: count) with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var driver = DeckObservationPlayTests.Driver(initial, "insight-repeat-burnout-" + count);
        await driver.Act("P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: ["ECHO"]));
        await driver.Top();
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                choice.ChoiceWindow == "INSIGHT_ORDER" ? choice.ContextObjectIds : []));
        Assert.Equal(MatchStatuses.Finished, driver.State.Status); Assert.Equal("P2", driver.State.WinnerPlayerId);
        Assert.Equal(8, driver.State.PlayerScores["P2"]); Assert.Empty(driver.State.StackItems);
        Assert.Equal(count, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(driver.State.CardPlays.Finalized); Assert.Single(driver.State.CardPlays.Resolved);
        Assert.Equal(count < 2 ? 1 : 2, driver.Events().Count(e => e.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task CounteringTheSingleChainItemCancelsEveryPaidExecution()
    {
        var initial = Position(2); var driver = DeckObservationPlayTests.Driver(initial, "insight-repeat-counter");
        await driver.Act("P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: Costs(2)));
        var parent = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [parent]));
        await driver.Top();
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(driver.State.PlayerZones["P1"].Hand); Assert.Null(driver.State.PendingCardChoice);
        Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind.StartsWith("INSIGHT", StringComparison.Ordinal) || e.Event.Kind is "CARD_DRAWN" or "SPELL_EXECUTION_COMPLETED");
        Assert.Single(driver.State.CardPlays.Finalized, p => p.Source.ObjectId == "SPELL");
        Assert.DoesNotContain(driver.State.CardPlays.Resolved, p => p.Source.ObjectId == "SPELL");
        Assert.Equal(1, driver.State.PlayerCardsPlayedThisTurn["P1"]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ARealGateActivationGrantsThePrintedSevenManaRepeatBeforeTheSpellIsPlayed()
    {
        var initial = Position(0);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GATE"] = new("GATE", cardNo: "SFD·078/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = ["GATE"] } } };
        var driver = DeckObservationPlayTests.Driver(initial, "insight-real-gate");
        await driver.Act("P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []));
        Assert.True(driver.State.CardObjects["GATE"].IsExhausted);
        Assert.DoesNotContain(driver.State.UntilEndOfTurnEffects, marker => EchoCostRules.IsGrant(marker, "P1"));
        await driver.Top();
        Assert.Single(driver.State.UntilEndOfTurnEffects, marker => EchoCostRules.IsGrant(marker, "P1"));
        var command = new PlayCardCommand("SPELL", Card, [], OptionalCosts: ["ECHO"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(14, quote.Cost!.Mana);
        await driver.Act("P1", command);
        Assert.DoesNotContain(driver.State.UntilEndOfTurnEffects, marker => EchoCostRules.IsGrant(marker, "P1"));
        await driver.Top();
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                choice.ChoiceWindow == "INSIGHT_ORDER" ? choice.ContextObjectIds : []));
        Assert.Equal(new[] { "D1", "D2", "D3", "D4" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(26, driver.State.RunePools["P1"].Mana);
        Assert.Equal(19, driver.State.RunePools["P1"].TotalPower);
        Assert.Single(driver.State.CardPlays.Finalized); Assert.Single(driver.State.CardPlays.Resolved);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BothExecutionsCaptureObservedPlaysButNeitherCanPreemptTheOriginalSpell()
    {
        var initial = Position(); var cards = initial.CardObjects.ToDictionary();
        foreach (var id in new[] { "D1", "D8" })
            cards[id] = new(id, cardNo: DeckObservationPlayTests.Nocturne, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
        initial = initial with { CardObjects = cards };
        var driver = DeckObservationPlayTests.Driver(initial, "insight-repeat-observed");
        await driver.Act("P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: ["ECHO"])); await driver.Top();
        for (var execution = 0; execution < 2; execution++)
        {
            Assert.NotNull(driver.State.PendingRuleChoice); Assert.Null(driver.State.PendingEffectPlay);
            await DeckObservationPlayTests.Decide(driver, true);
            var choice = driver.State.PendingCardChoice!;
            Assert.Equal(4, choice.MaxCount); Assert.Equal(execution, choice.DeckContext!.ExecutionIndex);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            var order = driver.State.PendingCardChoice!;
            Assert.Empty(driver.State.CardPlays.Resolved);
            await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, order.ContextObjectIds.Reverse().ToArray()));
        }
        Assert.Equal(new[] { "D5", "D4", "D7", "D6" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Single(driver.State.CardPlays.Resolved); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { 0, 1 }, driver.State.ObservedDeckPlays!.Select(c => c.Instruction.ExecutionIndex));
        foreach (var id in new[] { "D1", "D8" })
        {
            Assert.NotNull(driver.State.PendingEffectPlay);
            await driver.Act("P1", new PlayCardCommand(id, DeckObservationPlayTests.Nocturne, []));
            Assert.Contains(id, driver.State.PlayerZones["P1"].Base);
        }
        Assert.Null(driver.State.PendingEffectPlay); Assert.Empty(driver.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ProductionControlCommandsColdRecoverDeduplicateAndReplayEveryExecution(int echoes)
    {
        var initial = Position(echoes);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_INSIGHT_REPEAT_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, "echo-" + echoes));
        driver.Export("initial.json", initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: Costs(echoes))); await driver.Top();
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                choice.ChoiceWindow == "INSIGHT_ORDER" ? choice.ContextObjectIds.Reverse().ToArray() : []));
        Assert.Equal(echoes == 1 ? new[] { "D5", "D4", "D7", "D6" } : ["D5", "D4", "D7", "D6", "D9", "D8"], driver.State.PlayerZones["P1"].Hand);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.True(driver.NativeCommands == 0 || driver.NativeCommands == driver.Steps.Count);
        driver.Export("steps.json", Enumerable.Range(0, driver.Steps.Count).Select(i => "defend-step-" + i).ToArray());
        driver.Export("receipt.json", new { echoes, native = driver.NativeCommands, commands = driver.Commands().Length,
            events = driver.Events().Length, coldRecoveries = driver.Steps.Count, finalHash = MatchStateHasher.Hash(driver.State), replay });
    }

    [Theory]
    [InlineData("MANA")] [InlineData("UNKNOWN")] [InlineData("DUPLICATE")] [InlineData("DECLARATION")]
    public async Task RejectedRepeatCostsNeverSpendResourcesConsumeGrantsOrEnterTheChain(string branch)
    {
        var initial = Position();
        if (branch == "MANA") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(13, 20) } };
        var command = new PlayCardCommand("SPELL", Card, [], OptionalCosts: branch switch {
            "UNKNOWN" => ["ECHO:GRANTED:99"], "DUPLICATE" => ["ECHO", "ECHO"], _ => ["ECHO"] },
            RepeatChoices: branch == "DECLARATION" ? [new("", ["COUNTER"])] : null);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.False(quote.CanPay);
        await Rejected(initial, "P1", command);
    }

    [Theory]
    [InlineData("INDEX")] [InlineData("PROGRESS")] [InlineData("COUNT")] [InlineData("OLD_ID")]
    [InlineData("DONE")] [InlineData("TARGETED_KIND")]
    public async Task RecoveryIndependentlyRejectsForgedProgressAndEarlierExecutionIdentities(string branch)
    {
        var driver = DeckObservationPlayTests.Driver(Position(), "insight-repeat-tamper-" + branch);
        await driver.Act("P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: ["ECHO"])); await driver.Top();
        var first = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []));
        var order = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, order.ContextObjectIds));
        var state = driver.State; var item = Assert.Single(state.StackItems); var choice = state.PendingCardChoice!;
        if (branch == "INDEX") choice = choice with { DeckContext = choice.DeckContext! with { ExecutionIndex = 0 } };
        if (branch == "OLD_ID") choice = choice with { ChoiceId = first.ChoiceId };
        if (branch == "PROGRESS") item = item with { CompletedDeckExecutions = 2 };
        if (branch == "COUNT") item = item with { EffectRepeatCount = 1 };
        if (branch == "DONE") item = item with { DeckChoiceCompleted = true };
        if (branch == "TARGETED_KIND") item = item with { EffectKind = "ECLIPSE_POWER_MINUS_4_INSIGHT", CardNo = "UNL-063/219" };
        Assert.Contains(Errors(state with { StackItems = [item], PendingCardChoice = choice }), error => error.Contains("Insight", StringComparison.Ordinal));
        Restore(state);
    }

    internal static MatchState Position(int echoes = 1, int deckCards = 12)
    {
        var state = ClairvoyanceTests.Position(deckCards) with { Tick = 0 };
        var effects = state.UntilEndOfTurnEffects;
        for (var i = 0; i < echoes; i++) effects = EchoCostRules.AddBaseCostGrant(effects, "P1");
        return state with { UntilEndOfTurnEffects = effects,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(40, 20) } };
    }

    private static string[] Costs(int echoes) => echoes == 1 ? ["ECHO"] : ["ECHO", "ECHO:GRANTED:2"];

    private static void Private(MatchState state)
    {
        var snapshot = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P2"]);
        var prompt = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state)["P2"]);
        foreach (var id in Enumerable.Range(1, 12).Select(i => "D" + i))
        { Assert.DoesNotContain("\"" + id + "\"", snapshot); Assert.DoesNotContain("\"" + id + "\"", prompt); }
    }

    private static async Task Rejected(MatchState state, string actor, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-repeat", actor, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(state.RunePools, result.State.RunePools); Assert.Equal(state.UntilEndOfTurnEffects, result.State.UntilEndOfTurnEffects);
    }
}
