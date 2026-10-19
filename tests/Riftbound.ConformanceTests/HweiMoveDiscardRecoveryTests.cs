using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.HweiMoveDiscardTypesTests;

namespace Riftbound.ConformanceTests;

public sealed class HweiMoveDiscardRecoveryTests
{
    [Theory]
    [InlineData("ACTOR")]
    [InlineData("LEGAL_HAND")]
    [InlineData("COUNT")]
    [InlineData("DRAW_AGAIN")]
    [InlineData("SOURCE")]
    public async Task PendingDiscardRejectsForgedActorHandCountDrawAndSource(string attack)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Parent(driver);
        var state = driver.State; var p = state.PendingHandChoice!;
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var bad = attack switch {
            "ACTOR" => p with { PlayerId = "P2" },
            "LEGAL_HAND" => p with { LegalObjectIds = p.LegalObjectIds.Where(id => id != "D0").ToArray() },
            "COUNT" => p with { RequiredCount = 0 },
            "DRAW_AGAIN" => p with { DrawCount = 1 },
            _ => p with { SourceObjectId = "OTHER" }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { PendingHandChoice = bad }));
    }

    [Theory]
    [InlineData("NO_CONTEXT")]
    [InlineData("NO_INLINE")]
    [InlineData("TYPES")]
    [InlineData("BRANCH")]
    [InlineData("FUTURE")]
    [InlineData("ZERO_TICK")]
    [InlineData("UNKNOWN_DISCARD")]
    [InlineData("DISCARD_GENERATION")]
    [InlineData("CARD_NO")]
    [InlineData("CARD_NO_AND_TYPES")]
    [InlineData("PARENT_EFFECT")]
    [InlineData("CONTROLLER")]
    [InlineData("SOURCE")]
    [InlineData("SOURCE_GENERATION")]
    [InlineData("SOURCE_CARD_NO")]
    [InlineData("EVENT")]
    [InlineData("DUPLICATE_BRANCH")]
    [InlineData("UNCAPTURED_DISCARD")]
    public async Task InlineStateRejectsForgedTypeSourceGenerationTimingRoleAndDuplicateBranch(string attack)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Parent(driver); await Discard(driver, "D0");
        var state = driver.State; Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var trigger = state.TriggerQueue[0]; var c = trigger.MovementObserver!; var inline = c.DiscardInline!;
        var forged = attack switch {
            "NO_CONTEXT" => trigger with { MovementObserver = null },
            "NO_INLINE" => trigger with { MovementObserver = c with { DiscardInline = null } },
            "TYPES" => trigger with { MovementObserver = c with { DiscardInline = inline with { Types = 1 } } },
            "BRANCH" => trigger with { MovementObserver = c with { DiscardInline = inline with { BranchType = 4 } } },
            "FUTURE" => trigger with { MovementObserver = c with { DiscardInline = inline with { CreatedAtTick = state.Tick + 1 } } },
            "ZERO_TICK" => trigger with { MovementObserver = c with { DiscardInline = inline with { CreatedAtTick = 0 } } },
            "UNKNOWN_DISCARD" => trigger with { MovementObserver = c with { DiscardInline = inline with { DiscardedCard = new("UNSEEN", 0) } } },
            "DISCARD_GENERATION" => trigger with { MovementObserver = c with { DiscardInline = inline with {
                DiscardedCard = inline.DiscardedCard with { Generation = state.CardObjects[inline.DiscardedCard.ObjectId].ObjectGeneration } } } },
            "CARD_NO" => trigger with { MovementObserver = c with { DiscardInline = inline with { CardNo = "OGN·083/298" } } },
            "CARD_NO_AND_TYPES" => trigger with { MovementObserver = c with { DiscardInline = inline with { CardNo = "OGN·083/298", Types = 4, BranchType = 4 } } },
            "PARENT_EFFECT" => trigger with { EffectKind = "CAPTURED_UNIT_MOVEMENT_OBSERVER" },
            "CONTROLLER" => trigger with { ControllerId = "P2" },
            "SOURCE" => trigger with { SourceObjectId = "OTHER" },
            "SOURCE_GENERATION" => trigger with { MovementObserver = c with { SourceGeneration = c.SourceGeneration + 1 } },
            "SOURCE_CARD_NO" => trigger with { SourceCardNo = "OGN·096/298" },
            "EVENT" => trigger with { TriggeredByEventKind = "UNIT_MOVED_TO_BATTLEFIELD" },
            "UNCAPTURED_DISCARD" => trigger with { MovementObserver = null, EffectKind = "LEGITIMATE_LOOKING_SKILL" },
            _ => trigger with { TriggerId = trigger.TriggerId + "-duplicate" }
        };
        var queue = attack == "DUPLICATE_BRANCH" ? state.TriggerQueue.Append(forged).ToArray()
            : state.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId ? forged : t).ToArray();
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { TriggerQueue = queue }));
    }

    [Theory]
    [InlineData("MAX")]
    [InlineData("LEGAL")]
    [InlineData("STACK")]
    [InlineData("WINDOW")]
    public async Task RuneConfirmationCannotBeReboundToAnotherWindowStackOrTargetSet(string attack)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position("OGN·072/298"));
        await Parent(driver); await Discard(driver, "HAND");
        var state = driver.State; var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var bad = attack switch {
            "MAX" => choice with { MaxCount = 3 },
            "LEGAL" => choice with { LegalObjectIds = ["HOST"], ContextObjectIds = ["HOST"] },
            "STACK" => choice with { ResolvingStackItemId = "another-stack-item" },
            _ => choice with { ChoiceWindow = "ANOTHER_WINDOW" }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(state with { PendingCardChoice = bad }));
    }

    [Fact]
    public async Task CoordinatedEarlierInlineCreationTickStillFailsOriginalJournalReplay()
    {
        using var recipe = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "hwei-move-discard-openings.json")));
        var run = new CopiedDuelInsightOpeningTests.Opening(81, OfficialDeckFormat.CoreRules);
        foreach (var entry in recipe.RootElement.GetProperty("openingIntents").EnumerateArray())
            await run.Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
        var host = recipe.RootElement.GetProperty("host").GetString()!;
        var top = run.State.PlayerZones["P1"].MainDeck[0];
        await run.Submit("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + run.State.PlayerZones["P1"].Battlefields.First(run.State.BattlefieldStates.ContainsKey)));
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
        var p = run.State.PendingHandChoice!;
        await run.Submit("P1", new ChooseHandCardsCommand(p.ChoiceId, p.ChoiceWindow, [top]));
        await run.Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var state = run.State;
        var forged = state with { StackItems = state.StackItems.Select(item => item with {
            MovementObserver = item.MovementObserver! with { DiscardInline = item.MovementObserver!.DiscardInline! with { CreatedAtTick = 1 } } }).ToArray() };
        Assert.All(state.StackItems, item => Assert.True(item.MovementObserver!.DiscardInline!.CreatedAtTick > 1));
        var frame = run.Frame(forged);
        // All private/public views are coherently regenerated, so the stored
        // original command journal, rather than view disagreement, must reject it.
        Assert.Empty(frame.ValidationErrors);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(run.Frame(), new CoreRuleEngine(), default));
    }

    [Fact]
    public async Task RecycledConfirmedRuneLosesItsOldTargetIdentityAndIsNotReadiedInDeck()
    {
        var initial = Position("OGN·072/298"); var driver = new StandbyDefendObserverTests.Driver(initial);
        await Parent(driver); await Discard(driver, "HAND"); var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["R0"]));
        await driver.Act("P1", new RecycleRuneCommand("R0")); await Finish(driver);
        Assert.Contains("R0", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.True(driver.State.CardObjects["R0"].ObjectGeneration > initial.CardObjects["R0"].ObjectGeneration);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "RUNE_READIED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualSourceReturnBeforeResolutionKeepsDrawDiscardAndRunesButCannotBuffNewHandIncarnation()
    {
        var initial = SourceReturnPosition();
        var driver = await ReturnSource(initial);
        await driver.Top(); Assert.NotNull(driver.State.PendingHandChoice); await Discard(driver, "D0"); await Finish(driver);
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN"); Assert.All(new[] { "R0", "R1" }, id => Assert.False(driver.State.CardObjects[id].IsExhausted));
        Assert.Empty(driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(5, driver.State.CardObjects["HOST"].Power);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CurrentHandIncarnationCannotBorrowHistoricalMovementDisclosure(bool afterDraw, bool extraHandDisclosure)
    {
        var driver = await ReturnSource(SourceReturnPosition());
        if (afterDraw) await driver.Top();
        var state = driver.State;
        var snapshots = ResolutionResult.BuildSnapshots(state).ToDictionary();
        if (extraHandDisclosure)
        {
            var timing = snapshots["P2"].Timing.ToDictionary();
            timing["forgedCurrentHandDisclosure"] = "HOST";
            snapshots["P2"] = snapshots["P2"] with { Timing = timing };
        }
        else
        {
            var currentGeneration = state.CardObjects["HOST"].ObjectGeneration;
            MovementObserverContext Forge(MovementObserverContext context) => context with {
                SourceGeneration = currentGeneration, Unit = context.Unit with { Generation = currentGeneration } };
            state = afterDraw
                ? state with { PendingHandChoice = state.PendingHandChoice! with { MovementObserver = Forge(state.PendingHandChoice!.MovementObserver!) } }
                : state with { StackItems = state.StackItems.Select(item => item with { MovementObserver = Forge(item.MovementObserver!) }).ToArray() };
            snapshots = ResolutionResult.BuildSnapshots(state).ToDictionary();
        }
        var forged = new ResolutionResult(true, null, state, [], snapshots, ResolutionResult.BuildPrompts(state));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(forged));
    }

    private static MatchState SourceReturnPosition()
    {
        var initial = Position(); var cards = initial.CardObjects.ToDictionary(); cards.Remove("HAND");
        // Avoid collision with the public zone value HAND in the existing
        // string-value privacy assertion; keep the assertion itself unchanged.
        cards["PRIVATE-ORIGINAL-HAND"] = Card("PRIVATE-ORIGINAL-HAND", Dual);
        cards["RETURN-SPELL"] = Card("RETURN-SPELL", "OGN·104/298");
        var locations = initial.ObjectLocations.ToDictionary(); locations.Remove("HAND");
        locations["PRIVATE-ORIGINAL-HAND"] = new("P1", "HAND"); locations["RETURN-SPELL"] = new("P1", "HAND");
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string,RunePool>(initial.RunePools) { ["P1"] = new(1, 0) },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = ["PRIVATE-ORIGINAL-HAND", "RETURN-SPELL"] } } };
        return initial;
    }

    private static async Task<StandbyDefendObserverTests.Driver> ReturnSource(MatchState initial)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F"));
        await driver.Act("P1", new PlayCardCommand("RETURN-SPELL", "OGN·104/298", ["HOST"]));
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        await driver.Top();
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["HOST"].ObjectGeneration > initial.CardObjects["HOST"].ObjectGeneration);
        return driver;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyMainDeckUsesRealBurnoutAndFinishesBeforeDiscardIfNoCardCanBeDrawn(bool refill)
    {
        var initial = Position(deckCount: 0);
        if (refill) initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) { ["REFILL"] = Card("REFILL", Dual) },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) { ["REFILL"] = new("P1", "GRAVEYARD") },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Graveyard = ["REFILL"] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F")); await driver.Top();
        if (refill)
        {
            Assert.Contains("REFILL", driver.State.PendingHandChoice!.LegalObjectIds);
            Assert.Equal(1, driver.State.PlayerScores["P2"]);
            await Discard(driver, "REFILL"); await Finish(driver); Assert.Equal(8, Might(driver.State));
            Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        }
        else
        {
            Assert.Equal(MatchStatuses.Finished, driver.State.Status); Assert.Equal("P2", driver.State.WinnerPlayerId);
            Assert.Equal(8, driver.State.PlayerScores["P2"]); Assert.Null(driver.State.PendingHandChoice);
            Assert.Empty(driver.State.TriggerQueue); Assert.Empty(driver.State.StackItems);
            Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "CARD_DISCARDED" or "CARD_DRAWN");
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task GearInlineWithNoRunesNeedsNoSelectionAndStillAllowsItsUnitInline()
    {
        var initial = Position(runeCount: 0); var driver = new StandbyDefendObserverTests.Driver(initial);
        await Parent(driver); await Discard(driver, "D0"); await Finish(driver);
        Assert.Equal(8, Might(driver.State)); Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "RUNE_READIED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

}
