using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Current CN SFD136 and DOC01 445.2/740.4/820: the target controller,
// rather than the caster, makes each optional payment while resolving.
public sealed class ConditionalCounterPaymentTests
{
    private const string Card = "SFD·136/221";

    [Fact]
    public async Task NormalDeclarationDoesNotChooseTheOpponentsPaymentInAdvance()
    {
        var initial = Position(2);
        var command = new PlayCardCommand("C", Card, ["S1"]);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("ordinary-declaration", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(3, result.State.StackItems.Count);
        Assert.Null(result.State.PendingRuleChoice);
        Assert.Equal(2, result.State.RunePools["P2"].Mana);
    }

    [Fact]
    public async Task TargetControllerCanPayAtResolutionAndKeepItsOriginalSpell()
    {
        var initial = Position(2);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("C", Card, ["S1"], Mode: DeclaredMode()));
        await ResolveCounter(driver);
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("P2", pending.Request.PlayerId);
        Assert.Equal(2, pending.Request.ManaCost);
        Assert.Contains(pending.Request.Options, o => o.Id == "DECLINE");
        Assert.Contains(pending.Request.Options, o => o.Id == "SPEND_MANA:2");
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        await Decide(driver, "SPEND_MANA:2");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(0, driver.State.RunePools["P2"].Mana);
        Assert.Equal(["S1", "S2"], driver.State.StackItems.Select(i => i.StackItemId));
        Assert.Empty(driver.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(["C"], driver.State.PlayerZones["P1"].Graveyard);
        Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("playerId"), "P2")
            && Equals(e.Event.Payload.GetValueOrDefault("mana"), 2));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task PrintedEchoOffersTwoIndependentTargetControllerPayments()
    {
        var initial = Position(4);
        var mode = DeclaredMode();
        var command = new PlayCardCommand("C", Card, ["S1"], Mode: mode,
            OptionalCosts: ["ECHO"], RepeatChoices: [new(mode, ["S2"])]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(16, driver.State.RunePools["P1"].Mana);
        await ResolveCounter(driver);
        Assert.Equal("P2", driver.State.PendingRuleChoice!.Request.PlayerId);
        await Decide(driver, "SPEND_MANA:2");
        var second = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("P2", second.Request.PlayerId);
        Assert.Equal(2, second.Request.Preview!.RunePools["P2"].Mana);
        await Decide(driver, "SPEND_MANA:2");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(0, driver.State.RunePools["P2"].Mana);
        Assert.Equal(["S1", "S2"], driver.State.StackItems.Select(i => i.StackItemId));
        Assert.Equal(["C"], driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, driver.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DecliningOrBeingUnableToPayCountersOnlyTheDeclaredSpell(int mana)
    {
        var initial = Position(mana);
        var driver = await Open(initial);
        if (mana == 2) await Decide(driver, "DECLINE");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(mana, driver.State.RunePools["P2"].Mana);
        Assert.Equal(["S2"], driver.State.StackItems.Select(s => s.StackItemId));
        Assert.Equal(["SPELL1"], driver.State.PlayerZones["P2"].Graveyard);
        Assert.Single(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetControllerCanTapItsRuneWhilePayingAndMayStillDecline(bool pay)
    {
        var initial = Position(1);
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
                { ["P2"] = initial.PlayerZones["P2"] with { Base = ["RUNE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["RUNE"] = new("RUNE", cardNo: "OGN·089/298", ownerId: "P2", controllerId: "P2",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
                { ["RUNE"] = new("P2", "BASE") } };
        var driver = await Open(initial);
        Assert.DoesNotContain(driver.State.PendingRuleChoice!.Request.Options, o => o.Id == "SPEND_MANA:2");
        var beforeResourceEvents = driver.Events().Length;
        await Decide(driver, "TAP_RUNE:RUNE");
        var request = driver.State.PendingRuleChoice!.Request;
        Assert.Equal("P2", request.PlayerId);
        Assert.Equal(2, request.Preview!.RunePools["P2"].Mana);
        Assert.True(request.Preview.CardObjects["RUNE"].IsExhausted);
        Assert.Equal(2, driver.State.RunePools["P2"].Mana);
        Assert.True(driver.State.CardObjects["RUNE"].IsExhausted);
        Assert.Equal(1, driver.State.PendingRuleChoice.Origin.RunePools["P2"].Mana);
        Assert.False(driver.State.PendingRuleChoice.Origin.CardObjects["RUNE"].IsExhausted);
        var confirmationEvents = driver.Events().Skip(beforeResourceEvents).ToArray();
        var tapped = Assert.Single(confirmationEvents, e => e.Event.Kind == "RUNE_TAPPED");
        var resourceCommand = driver.Commands()[^1];
        Assert.Equal(resourceCommand.CompletedTick, tapped.Tick);
        Assert.InRange(tapped.Sequence, resourceCommand.StartedEventSequence + 1, resourceCommand.CompletedEventSequence);
        Assert.DoesNotContain(confirmationEvents, e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "COUNTER_TARGET_MANA_PAYMENT"));
        Assert.DoesNotContain(confirmationEvents, e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        var beforeDecisionEvents = driver.Events().Length;
        await Decide(driver, pay ? "SPEND_MANA:2" : "DECLINE");
        Assert.True(driver.State.CardObjects["RUNE"].IsExhausted);
        Assert.Equal(pay ? 0 : 2, driver.State.RunePools["P2"].Mana);
        Assert.Equal(pay, driver.State.StackItems.Any(s => s.StackItemId == "S1"));
        Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_TAPPED");
        Assert.DoesNotContain(driver.Events().Skip(beforeDecisionEvents), e => e.Event.Kind == "RUNE_TAPPED");
        Assert.Equal(pay ? 1 : 0, driver.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "COUNTER_TARGET_MANA_PAYMENT")));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task TargetControllerCanUseItsReactionResourceEquipmentDuringResolution()
    {
        var initial = ReplacementResourceTests.AddResource(Position(1), P4ActivatedAbilityCatalog.EnergyChannelCardNo);
        var driver = await Open(initial);
        var option = Assert.Single(driver.State.PendingRuleChoice!.Request.Options,
            o => o.Id.StartsWith("RESOURCE:RESOURCE:", StringComparison.Ordinal));
        await Decide(driver, option.Id);
        Assert.Equal(2, driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P2"].Mana);
        await Decide(driver, "SPEND_MANA:2");
        Assert.True(driver.State.CardObjects["RESOURCE"].IsExhausted);
        Assert.Equal(0, driver.State.RunePools["P2"].Mana);
        Assert.Equal(["S1", "S2"], driver.State.StackItems.Select(s => s.StackItemId));
        Assert.Single(driver.Events(), e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("opponent")]
    [InlineData("stale")]
    [InlineData("free")]
    [InlineData("multiple")]
    public async Task UnauthorizedOrForgedPaymentCommandsRejectWithoutChangingAnyState(string branch)
    {
        var initial = Position(2);
        var driver = await Open(initial);
        var state = driver.State;
        var request = state.PendingRuleChoice!.Request;
        var command = new PayCostCommand(branch == "stale" ? "OLD" : request.Id, "RULE_REPLACEMENT",
            branch == "multiple" ? ["SPEND_MANA:2", "DECLINE"] : [branch == "free" ? "SPEND_MANA:0" : "SPEND_MANA:2"]);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("bad-counter-payment", branch == "opponent" ? "P1" : "P2", CommandTypes.PayCost), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(state)), MatchStateHasher.HashValue(result.Snapshots));
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(state)), MatchStateHasher.HashValue(result.Prompts));
        await Decide(driver, "DECLINE");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("payer")]
    [InlineData("cost")]
    [InlineData("kind")]
    [InlineData("option")]
    [InlineData("preview")]
    [InlineData("transcript")]
    public async Task RecoveryIndependentlyRejectsChangesToTheActualPaymentDecision(string branch)
    {
        var driver = await Open(Position(2));
        var state = OfficialInsightAndSpellLockTests.Restore(driver.State);
        var pending = state.PendingRuleChoice!;
        var request = pending.Request;
        request = branch switch {
            "payer" => request with { PlayerId = "P1" },
            "cost" => request with { ManaCost = 0 },
            "kind" => request with { Kind = "UNRELATED" },
            "option" => request with { Options = [.. request.Options, new("SPEND_MANA:0", "伪造免费支付")] },
            "preview" => request with { Preview = request.Preview! with {
                RunePools = new Dictionary<string, RunePool>(request.Preview!.RunePools) { ["P2"] = new(99, 0) } } },
            _ => request };
        var forged = pending with { Request = request };
        if (branch == "transcript") forged = forged with { Answers = [new("FORGED", "SPEND_MANA:2")] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { PendingRuleChoice = forged }));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public async Task CurrentSpellControllerPaysEvenWhenItsPhysicalOwnerIsTheOtherPlayer()
    {
        var initial = Position(4);
        initial = initial with {
            StackItems = [initial.StackItems[0] with { ControllerId = "P1" }, initial.StackItems[1]],
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects)
                { ["SPELL1"] = initial.CardObjects["SPELL1"] with { ControllerId = "P1" } } };
        var driver = await Open(initial);
        Assert.Equal("P1", driver.State.PendingRuleChoice!.Request.PlayerId);
        Assert.Equal(18, driver.State.RunePools["P1"].Mana);
        await Decide(driver, "SPEND_MANA:2");
        Assert.Equal(16, driver.State.RunePools["P1"].Mana);
        Assert.Equal(4, driver.State.RunePools["P2"].Mana);
        Assert.Equal("P2", driver.State.CardObjects["SPELL1"].OwnerId);
        Assert.Equal("P1", driver.State.StackItems.Single(s => s.StackItemId == "S1").ControllerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EchoRechecksResourcesAfterItsFirstIndependentPayment(bool sameTarget)
    {
        var initial = Position(2);
        var driver = await Open(initial, echo: true, second: sameTarget ? "S1" : "S2");
        await Decide(driver, "SPEND_MANA:2");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(0, driver.State.RunePools["P2"].Mana);
        Assert.Equal([sameTarget ? "S2" : "S1"], driver.State.StackItems.Select(s => s.StackItemId));
        Assert.Equal([sameTarget ? "SPELL1" : "SPELL2"], driver.State.PlayerZones["P2"].Graveyard);
        Assert.Single(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EchoDoesNotAskForPaymentForATargetAlreadyCounteredByItsFirstExecution(bool sameTarget)
    {
        var initial = Position(2);
        var driver = await Open(initial, echo: true, second: sameTarget ? "S1" : "S2");
        await Decide(driver, "DECLINE");
        if (!sameTarget) await Decide(driver, "SPEND_MANA:2");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(sameTarget ? 2 : 0, driver.State.RunePools["P2"].Mana);
        Assert.Equal(["S2"], driver.State.StackItems.Select(s => s.StackItemId));
        Assert.Equal(["SPELL1"], driver.State.PlayerZones["P2"].Graveyard);
        Assert.Single(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState Position(int targetMana)
    {
        var initial = OfficialCounterRepeatTests.Position(Card);
        return initial with { Tick = 0, UntilEndOfTurnEffects = [], RunePools = new Dictionary<string, RunePool>(initial.RunePools)
            { ["P2"] = new(targetMana, 0) } };
    }

    private static string DeclaredMode()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var definition));
        return definition.Mode;
    }

    private static async Task ResolveCounter(StandbyDefendObserverTests.Driver driver)
    {
        for (var i = 0; i < 4 && driver.State.PendingRuleChoice is null
            && driver.State.StackItems.Any(s => s.SourceObjectId == "C"); i++)
            await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
    }

    private static async Task<StandbyDefendObserverTests.Driver> Open(MatchState initial, bool echo = false, string second = "S2")
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var mode = DeclaredMode();
        await driver.Act("P1", new PlayCardCommand("C", Card, ["S1"], Mode: mode,
            OptionalCosts: echo ? ["ECHO"] : null, RepeatChoices: echo ? [new(mode, [second])] : null));
        await ResolveCounter(driver);
        return driver;
    }

    private static Task Decide(StandbyDefendObserverTests.Driver driver, string option)
    {
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        return driver.Act(pending.Request.PlayerId,
            new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option]));
    }
}
