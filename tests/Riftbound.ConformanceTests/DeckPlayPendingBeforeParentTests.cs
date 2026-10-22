using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeckPlayPendingBeforeParentTests
{
    [Theory]
    [InlineData(SingleDeckPlaySequenceTests.Reinforce)]
    [InlineData(SingleDeckPlaySequenceTests.VoidRush)]
    [InlineData(DeckPlaySequenceTests.BlindFury)]
    [InlineData(DeckPlaySequenceTests.BrightFuture)]
    public async Task FirstExecutionCreatesPendingCardsBeforeTheRepeatedParentContinues(string card)
    {
        var initial = EchoPosition(card);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"]));
        await driver.Top();
        foreach (var id in FirstSelections(card)) await DeckPlaySequenceTests.Select(driver, id);

        var parent = Assert.Single(driver.State.StackItems, i => i.SourceObjectId == "PARENT");
        var expected = FirstPendingOrder(card);
        Assert.False(parent.DeckPlaySequence!.ParentCompleted);
        Assert.Equal(1, parent.DeckPlaySequence.ExecutionIndex);
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Equal(expected, driver.State.StackItems.Where(i => i.DeckPlayPendingCard is not null).Select(i => i.SourceObjectId));
        foreach (var id in expected)
        {
            Assert.Equal("STACK", driver.State.ObjectLocations[id].Zone);
            Assert.Equal(2, driver.State.CardObjects[id].ObjectGeneration);
            Assert.DoesNotContain(id, driver.State.PlayerZones[driver.State.CardObjects[id].OwnerId!].Banished);
        }
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BASE");
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task BrightFutureFinishesAllBanishAndRecycleSelectionsBeforeItsFirstPlayInstruction()
    {
        var initial = EchoPosition(DeckPlaySequenceTests.BrightFuture);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", DeckPlaySequenceTests.BrightFuture, [], OptionalCosts: ["ECHO"]));
        await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP");
        Assert.Contains("P1_TOP", driver.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain(driver.State.StackItems, i => i.DeckPlayPendingCard is not null);
        Assert.Equal("P2", driver.State.PendingCardChoice!.PlayerId);
        Assert.Null(driver.State.PendingEffectPlay);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(SingleDeckPlaySequenceTests.Reinforce)]
    [InlineData(SingleDeckPlaySequenceTests.VoidRush)]
    [InlineData(DeckPlaySequenceTests.BlindFury)]
    [InlineData(DeckPlaySequenceTests.BrightFuture)]
    public async Task PendingCardsStayUnconfirmedUntilTheCapturedParentFinishesThenPlayInInsertionOrder(string card)
    {
        var initial = EchoPosition(card);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"])); await driver.Top();
        foreach (var id in FirstSelections(card)) await DeckPlaySequenceTests.Select(driver, id);
        foreach (var id in FirstSelections(card).Select(id => id.Replace("_TOP", "_SECOND")))
            await DeckPlaySequenceTests.Select(driver, id);
        var order = FirstPendingOrder(card).Concat(FirstPendingOrder(card).Select(id => id.Replace("_TOP", "_SECOND"))).ToArray();
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(order, driver.State.StackItems.Select(i => i.SourceObjectId));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BASE");
        var pendingEvents = driver.Events().Where(e => e.Event.Kind == "STACK_ITEM_PENDING").ToArray();
        Assert.Equal(order, pendingEvents.Select(e => e.Event.Payload["sourceObjectId"]));
        var secondChoice = driver.Events().Where(e => e.Event.Kind == "CARD_CHOICE_REQUESTED").Skip(FirstSelections(card).Length).First();
        Assert.True(pendingEvents[FirstPendingOrder(card).Length - 1].Sequence < secondChoice.Sequence);
        foreach (var id in order)
        {
            Assert.Equal(id, Assert.Single(driver.State.PendingEffectPlay!.Sources).Key);
            await driver.Act(card == DeckPlaySequenceTests.BlindFury ? "P1" : id[..2], new PlayCardCommand(id, "OGN·096/298", []));
            Assert.True(driver.State.CardObjects[id].IsExhausted);
        }
        Assert.Null(driver.State.PendingEffectPlay); Assert.Empty(driver.State.StackItems);
        Assert.Equal(order.Length, driver.Events().Count(e => e.Event.Kind == "UNIT_PLAYED_TO_BASE"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(SingleDeckPlaySequenceTests.Reinforce, true)]
    [InlineData(SingleDeckPlaySequenceTests.Reinforce, false)]
    [InlineData(SingleDeckPlaySequenceTests.VoidRush, true)]
    [InlineData(SingleDeckPlaySequenceTests.VoidRush, false)]
    public async Task DecliningEitherExecutionDoesNotRemoveOrDuplicateTheOtherPendingCard(string card, bool declineFirst)
    {
        var initial = EchoPosition(card); var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"])); await driver.Top();
        var first = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, declineFirst ? [] : ["P1_TOP"]));
        var second = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, declineFirst ? ["P1_SECOND"] : []));
        var selected = declineFirst ? "P1_SECOND" : "P1_TOP";
        Assert.Equal(selected, Assert.Single(driver.State.StackItems).SourceObjectId);
        Assert.Single(driver.Events(), e => e.Event.Kind == "STACK_ITEM_PENDING");
        await driver.Act("P1", new PlayCardCommand(selected, "OGN·096/298", []));
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BASE");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("missing-slot")]
    [InlineData("missing-parent")]
    [InlineData("duplicate-slot")]
    [InlineData("slot-before-parent")]
    [InlineData("parent-completed")]
    [InlineData("parent-effect-completed")]
    [InlineData("parent-choice")]
    [InlineData("queue-missing")]
    [InlineData("queue-generation")]
    [InlineData("queue-before-banish")]
    [InlineData("queue-execution")]
    [InlineData("queue-card")]
    [InlineData("slot-parent")]
    [InlineData("slot-generation")]
    [InlineData("slot-order")]
    [InlineData("slot-actor")]
    [InlineData("slot-owner")]
    [InlineData("slot-card")]
    [InlineData("slot-confirmed")]
    public async Task ColdRecoveryRejectsIndependentlyForgedPendingChildrenWhileTheParentStillResolves(string mutation)
    {
        var driver = await FirstReinforce(); var state = driver.State;
        var parent = state.StackItems.Single(i => i.SourceObjectId == "PARENT"); var c = parent.DeckPlaySequence!;
        var slot = Assert.Single(state.StackItems, i => i.DeckPlayPendingCard is not null); var receipt = slot.DeckPlayPendingCard!;
        if (mutation == "parent-completed") parent = parent with { DeckPlaySequence = c with { ParentCompleted = true } };
        if (mutation == "parent-effect-completed") parent = parent with { EffectPlayCompleted = true };
        if (mutation == "queue-missing") parent = parent with { DeckPlaySequence = c with { Queue = [] } };
        if (mutation == "queue-generation") parent = parent with { DeckPlaySequence = c with { Queue = [c.Queue[0] with { Card = new("P1_TOP", 99) }] } };
        if (mutation == "queue-before-banish") parent = parent with { DeckPlaySequence = c with { Queue = [c.Queue[0] with { BeforeBanishGeneration = 99 }] } };
        if (mutation == "queue-execution") parent = parent with { DeckPlaySequence = c with { Queue = [c.Queue[0] with { ExecutionIndex = 1 }] } };
        if (mutation == "queue-card") parent = parent with { DeckPlaySequence = c with { Queue = [c.Queue[0] with { CardNo = "OGN·097/298" }] } };
        if (mutation == "slot-parent") receipt = receipt with { ParentStackItemId = "OTHER" };
        if (mutation == "slot-generation") receipt = receipt with { Card = new("P1_TOP", 99) };
        if (mutation == "slot-order") receipt = receipt with { OrderIndex = 1 };
        if (mutation == "slot-actor") receipt = receipt with { PlayerId = "P2" };
        if (mutation == "slot-owner") receipt = receipt with { OwnerId = "P2" };
        if (mutation == "slot-card") receipt = receipt with { CardNo = "OGN·097/298" };
        slot = slot with { DeckPlayPendingCard = receipt, SourceConfirmed = mutation == "slot-confirmed" };
        IReadOnlyList<StackItemState> stack = mutation switch {
            "missing-slot" => [parent], "missing-parent" => [slot], "duplicate-slot" => [parent, slot, slot],
            "slot-before-parent" => [slot, parent], _ => [parent, slot]
        };
        var choice = state.PendingCardChoice!;
        var forged = state with { StackItems = stack, PendingCardChoice = mutation == "parent-choice"
            ? choice with { DeckContext = choice.DeckContext! with { StackItemId = slot.StackItemId } } : choice };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Theory]
    [InlineData("P1", false)]
    [InlineData("P2", false)]
    [InlineData("P1", true)]
    [InlineData("P2", true)]
    public async Task NeitherPlayerMayPlayOrPassThePendingChildBeforeTheParentChoiceFinishes(string player, bool pass)
    {
        var driver = await FirstReinforce(); var state = driver.State;
        GameCommand command = pass ? new PassPriorityCommand() : new PlayCardCommand("P1_TOP", "OGN·096/298", []);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("early", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData(SingleDeckPlaySequenceTests.Reinforce, true)]
    [InlineData(SingleDeckPlaySequenceTests.Reinforce, false)]
    [InlineData(DeckPlaySequenceTests.BrightFuture, true)]
    [InlineData(DeckPlaySequenceTests.BrightFuture, false)]
    public async Task ASecondExecutionObservationCannotJumpAheadOfTheFirstExecutionsPendingCards(string card, bool playObserved)
    {
        var initial = DeckPlaySequenceTests.TopCard(EchoPosition(card), "P1_SECOND", DeckObservationPlayTests.Nocturne);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"])); await driver.Top();
        foreach (var id in FirstSelections(card)) await DeckPlaySequenceTests.Select(driver, id);
        Assert.Equal("DECK_OBSERVATION_BANISH", driver.State.PendingRuleChoice!.Request.Kind);
        await DeckObservationPlayTests.Decide(driver, true);
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                choice.LegalObjectIds.Take(choice.MaxCount).ToArray()));
        var publicOrder = driver.State.StackItems.Select(i => i.SourceObjectId).ToArray();
        foreach (var id in FirstPendingOrder(card))
            Assert.True(Array.IndexOf(publicOrder, id) < Array.IndexOf(publicOrder, "P1_SECOND"));
        foreach (var id in FirstPendingOrder(card))
        {
            var first = driver.State.PendingEffectPlay!;
            Assert.Null(first.Parent.ObservedPlayPermission);
            Assert.Equal(id, Assert.Single(first.Sources).Key);
            await driver.Act(id[..2], new PlayCardCommand(id, "OGN·096/298", []));
        }
        var observed = driver.State.PendingEffectPlay!;
        Assert.NotNull(observed.Parent.ObservedPlayPermission);
        Assert.Equal("P1_SECOND", Assert.Single(observed.Sources).Key);
        if (playObserved) await driver.Act("P1", new PlayCardCommand("P1_SECOND", DeckObservationPlayTests.Nocturne, []));
        else await driver.Act("P1", new ChooseCardsCommand(observed.ChoiceId, "EFFECT_PLAY", []));
        while (driver.State.PendingEffectPlay is { } pending)
        {
            var id = Assert.Single(pending.Sources).Key;
            var play = new PlayCardCommand(id, driver.State.CardObjects[id].CardNo!, []);
            var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, pending.PlayerId, PlayCostPreviewTests.Request(driver.State, play));
            if (quote.CanPay) await driver.Act(pending.PlayerId, play);
            else await driver.Act(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        }
        Assert.Equal(playObserved, driver.State.PlayerZones["P1"].Base.Contains("P1_SECOND"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(SingleDeckPlaySequenceTests.Reinforce)]
    [InlineData(DeckPlaySequenceTests.BrightFuture)]
    public async Task RecoveryRejectsAnOtherwiseMatchingLaterObservationPermissionBeforeTheEarlierCard(string card)
    {
        var initial = DeckPlaySequenceTests.TopCard(EchoPosition(card), "P1_SECOND", DeckObservationPlayTests.Nocturne);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"])); await driver.Top();
        foreach (var id in FirstSelections(card)) await DeckPlaySequenceTests.Select(driver, id);
        await DeckObservationPlayTests.Decide(driver, true);
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                choice.LegalObjectIds.Take(choice.MaxCount).ToArray()));
        var state = driver.State; var earlier = state.PendingEffectPlay!;
        var context = Assert.Single(state.ObservedDeckPlays!);
        Assert.Equal(1, context.Instruction.ExecutionIndex);
        Assert.Equal(0, earlier.Parent.DeckPlaySequence!.Queue[0].ExecutionIndex);
        var parent = new StackItemState("OBSERVED-PERMISSION:" + context.Id, "P1", context.Card.ObjectId,
            "OBSERVED_DECK_UNIT_PLAY_PERMISSION", context.CardNo) { ObservedPlayPermission = new(context, earlier) };
        var permission = new PendingEffectPlayState("OBSERVED-PLAY:" + context.Id, "P1", parent, "STACK",
            new Dictionary<string, long> { [context.Card.ObjectId] = context.Card.Generation }, true, true, 0, "", true);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { PendingEffectPlay = permission }), e => e.Contains("observed deck play permission"));
    }

    private static async Task<StandbyDefendObserverTests.Driver> FirstReinforce()
    {
        var driver = new StandbyDefendObserverTests.Driver(EchoPosition(SingleDeckPlaySequenceTests.Reinforce));
        await driver.Act("P1", new PlayCardCommand("PARENT", SingleDeckPlaySequenceTests.Reinforce, [], OptionalCosts: ["ECHO"]));
        await driver.Top(); await DeckPlaySequenceTests.Select(driver, "P1_TOP"); return driver;
    }

    [Theory]
    [InlineData("negative-index")]
    [InlineData("null-queue")]
    [InlineData("null-entry")]
    public async Task RecoveryRejectsMalformedSuspendedDeckQueuesBeforeInspectingTheirOrder(string mutation)
    {
        const string card = DeckPlaySequenceTests.BrightFuture;
        var initial = DeckPlaySequenceTests.TopCard(EchoPosition(card), "P1_SECOND", DeckObservationPlayTests.Nocturne);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"])); await driver.Top();
        foreach (var id in FirstSelections(card)) await DeckPlaySequenceTests.Select(driver, id);
        await DeckObservationPlayTests.Decide(driver, true);
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                choice.LegalObjectIds.Take(choice.MaxCount).ToArray()));
        var state = driver.State; var earlier = state.PendingEffectPlay!;
        var context = Assert.Single(state.ObservedDeckPlays!); var sequence = earlier.Parent.DeckPlaySequence!;
        sequence = mutation switch {
            "negative-index" => sequence with { PlayIndex = -1 },
            "null-queue" => sequence with { Queue = null! },
            _ => sequence with { Queue = sequence.Queue.Select((q, i) => i == sequence.PlayIndex ? null! : q).ToArray() }
        };
        var malformed = earlier with { Parent = earlier.Parent with { DeckPlaySequence = sequence } };
        var parent = new StackItemState("OBSERVED-PERMISSION:" + context.Id, "P1", context.Card.ObjectId,
            "OBSERVED_DECK_UNIT_PLAY_PERMISSION", context.CardNo) { ObservedPlayPermission = new(context, malformed) };
        var permission = new PendingEffectPlayState("OBSERVED-PLAY:" + context.Id, "P1", parent, "STACK",
            new Dictionary<string, long> { [context.Card.ObjectId] = context.Card.Generation }, true, true, 0, "", true);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { PendingEffectPlay = permission }),
            error => error.Contains("invalid observed deck play permission"));
    }

    internal static string[] FirstSelections(string card)
        => card == DeckPlaySequenceTests.BrightFuture ? ["P1_TOP", "P2_TOP"]
            : card == DeckPlaySequenceTests.BlindFury ? ["P2_TOP"] : ["P1_TOP"];

    internal static string[] FirstPendingOrder(string card)
        => card == DeckPlaySequenceTests.BrightFuture ? ["P2_TOP", "P1_TOP"] : FirstSelections(card);

    internal static MatchState EchoPosition(string card)
    {
        var state = (card is SingleDeckPlaySequenceTests.Reinforce or SingleDeckPlaySequenceTests.VoidRush
            ? SingleDeckPlaySequenceTests.Position(card) : DeckPlaySequenceTests.Position(card))
            with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var count = card == DeckPlaySequenceTests.BlindFury ? 1 : card == SingleDeckPlaySequenceTests.VoidRush ? 2 : 5;
        foreach (var player in state.Seats.Keys)
        {
            var id = player + "_SECOND";
            state = state with {
                CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    [id] = new(id, cardNo: "OGN·096/298", manaCost: 2, power: 1,
                        ownerId: player, controllerId: player, tags: [CardObjectTags.UnitCard]) },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "MAIN_DECK") },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with {
                    MainDeck = state.PlayerZones[player].MainDeck.Take(count).Append(id)
                        .Concat(state.PlayerZones[player].MainDeck.Skip(count)).ToArray() } }
            };
        }
        return state;
    }
}
