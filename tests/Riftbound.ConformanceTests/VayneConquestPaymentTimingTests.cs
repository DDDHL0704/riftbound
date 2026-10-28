using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData("SPEND_MANA:1")]
    [InlineData("PAY")]
    public async Task VaynePaysAtConfirmationButReturnsOnlyAfterResponses(string choice)
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(responseCard: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.ManaCost);
        Assert.Equal("TRIGGER_PAYMENT", payment.PaymentWindow);
        Assert.Equal(new[] { "SPEND_MANA:1", "DECLINE" }, payment.LegalPaymentChoiceIds);
        var opened = Assert.Single(game.Events(), e => e.Event.Kind == "PAYMENT_WINDOW_OPENED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), AttackerObjectId)).Event.Payload;
        Assert.Equal(payment.PaymentId, opened["paymentId"]);
        Assert.Equal(payment.PaymentWindow, opened["paymentWindow"]);
        Assert.Equal(TriggerKinds.UnitConquestPayReturnSelfToHand, opened["trigger"]);
        Assert.Equal(BattlefieldObjectId, opened["battlefieldObjectId"]);
        Assert.Equal(new[] { "SPEND_MANA:1", "DECLINE" }, Assert.IsType<string[]>(opened["paymentChoices"]));
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        AssertVayneJsonColdFrame(initial, game);

        var paid = await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [choice]));
        var cost = Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(payment.PaymentId, cost.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, cost.Payload["paymentWindow"]);
        Assert.Equal(1, Convert.ToInt32(cost.Payload["totalManaCost"]));
        Assert.Equal(1, Convert.ToInt32(cost.Payload["mana"]));
        Assert.Equal(AttackerObjectId, cost.Payload["sourceObjectId"]);
        Assert.Equal(TriggerKinds.UnitConquestPayReturnSelfToHand, cost.Payload["reason"]);
        Assert.Equal(new[] { choice }, Assert.IsType<string[]>(cost.Payload["paymentChoiceIds"]));
        var closed = Assert.Single(paid.Events, e => e.Kind == "PAYMENT_WINDOW_CLOSED").Payload;
        Assert.Equal(payment.PaymentId, closed["paymentId"]);
        Assert.Equal(AttackerObjectId, closed["sourceObjectId"]);
        Assert.Equal(TriggerKinds.UnitConquestPayReturnSelfToHand, closed["trigger"]);
        Assert.Equal(false, closed["declined"]);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        var item = Assert.Single(game.State.StackItems, s => s.SourceObjectId == AttackerObjectId);
        Assert.Equal(new ObjectBinding(AttackerObjectId, initial.CardObjects[AttackerObjectId].ObjectGeneration), item.TriggerCost!.Source);
        Assert.Equal(1, item.TriggerCost.Mana);
        AssertVayneJsonColdFrame(initial, game);

        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("P2", game.State.PriorityPlayerId);
        await game.Act("P2", new PlayCardCommand("VAYNE-RESPONSE", "OGN·104/298", [AttackerObjectId]));
        await game.Top();
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.CardObjects[AttackerObjectId].ObjectGeneration + 1, game.State.CardObjects[AttackerObjectId].ObjectGeneration);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Contains(game.State.StackItems, s => s.SourceObjectId == AttackerObjectId);
        AssertVayneJsonColdFrame(initial, game);
        var resolved = await game.Top();
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        AssertVayneJsonColdFrame(initial, game);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task VayneDeclineDoesNotPayOrReturnTheSource()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var declined = await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(declined.Events, e => e.Kind is "COST_PAID" or "UNIT_RETURNED_TO_HAND");
        var closed = Assert.Single(declined.Events, e => e.Kind == "PAYMENT_WINDOW_CLOSED").Payload;
        Assert.Equal(payment.PaymentId, closed["paymentId"]);
        Assert.Equal(AttackerObjectId, closed["sourceObjectId"]);
        Assert.Equal(TriggerKinds.UnitConquestPayReturnSelfToHand, closed["trigger"]);
        Assert.Equal(true, closed["declined"]);
        Assert.DoesNotContain(game.State.StackItems, s => s.SourceObjectId == AttackerObjectId);
        AssertVayneJsonColdFrame(initial, game);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task SecondPlayerPaysOwnVayneAfterFirstPlayerCannotPayForIt()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(swapSides: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal("P2", payment.PlayerId);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]);
        var before = MatchStateHasher.Hash(game.State);
        var wrongPlayer = await new CoreRuleEngine().ResolveAsync(game.State,
            new("vayne-wrong-payer", "P1", command.CmdType), command, default);
        Assert.False(wrongPlayer.Accepted);
        Assert.Empty(wrongPlayer.Events);
        Assert.Equal(before, MatchStateHasher.Hash(wrongPlayer.State));
        var paid = await game.Act("P2", command);
        Assert.Equal(0, game.State.RunePools["P2"].Mana);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P2"].Battlefields);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        await game.Top();
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P2"].Hand);
        Assert.Equal(0, game.State.RunePools["P2"].Mana);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData("SPEND_MANA:1")]
    [InlineData("PAY")]
    public async Task VayneInsufficientConfirmationIsAtomic(string choice)
    {
        var (_, game, _) = await OpenVaynePaymentTimingBattle(mana: 0);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var before = MatchStateHasher.Hash(game.State);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [choice]);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("vayne-unfunded", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.NotNull(rejected.State.PendingPayment);
    }

    [Fact]
    public async Task SpellOnlyManaCannotPayVayneButATappedRuneCan()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle(mana: 0, spellOnly: true, includeRune: true);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]);
        var before = MatchStateHasher.Hash(game.State);
        var refused = await new CoreRuleEngine().ResolveAsync(game.State, new("vayne-restricted", "P1", command.CmdType), command, default);
        Assert.False(refused.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, refused.ErrorCode);
        Assert.Equal(before, MatchStateHasher.Hash(refused.State));
        Assert.Empty(refused.Events);

        await game.Act("P1", new TapRuneCommand("VAYNE-RUNE"));
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].SpellOnlyMana);
        payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var paid = await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].SpellOnlyMana);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "UNIT_RETURNED_TO_HAND");
        await game.Top();
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.RunePools["P1"].SpellOnlyMana);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task VaynePaymentAndReceiptRejectForgedBindingsAndCoordinatedRefund()
    {
        var (initial, game, _) = await OpenVaynePaymentTimingBattle();
        var pending = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.NotNull(pending.ResolvingStackItemId);
        var wrongCost = game.State with { PendingPayment = pending with { ManaCost = 0 } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(wrongCost), e => e.Contains("invalid trigger cost payment", StringComparison.Ordinal));
        var wrongExperience = game.State with { PendingPayment = pending with { ExperienceCost = 1 } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(wrongExperience), e => e.Contains("invalid trigger cost payment", StringComparison.Ordinal));
        var wrongPending = game.State with { PendingPayment = pending with { ResolvingStackItemId = "missing-vayne-stack" } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(wrongPending));

        await game.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["SPEND_MANA:1"]));
        var item = Assert.Single(game.State.StackItems, s => s.SourceObjectId == AttackerObjectId);
        var receipt = Assert.IsType<TriggerCostReceipt>(item.TriggerCost);
        var forgedItem = item with { TriggerCost = receipt with {
            Source = receipt.Source with { Generation = receipt.Source.Generation + 1 } } };
        var forgedReceipt = game.State with { StackItems = game.State.StackItems
            .Select(s => s.StackItemId == item.StackItemId ? forgedItem : s).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forgedReceipt), e => e.Contains("invalid trigger cost receipt", StringComparison.Ordinal));

        var pool = game.State.RunePools["P1"];
        var refund = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = pool with { Mana = pool.Mana + 1 } } };
        var frame = VayneRecoveryFrame(initial, game, refund);
        Assert.Empty(frame.ValidationErrors);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, refund,
            new CoreRuleEngine(), default, JournalRoundTrip(frame.Events));
        Assert.False(replay.IsMatch);
        Assert.NotEqual(replay.ExpectedStateHash, replay.ReplayedStateHash);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance,
            new ConquestContinuationRecoveryStore(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(refund.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Null(await registry.FindAsync(refund.RoomId, default));
        AssertVayneJsonColdFrame(initial, game);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoVayneConquerorsAndTheBattlefieldKeepIndependentPendingTriggers(bool minefield)
    {
        var (initial, game, settled) = await OpenVaynePaymentTimingBattle(twoVaynes: true, minefield: minefield);
        var sources = game.State.TriggerQueue.Select(t => t.SourceObjectId)
            .Concat(game.State.StackItems.Select(s => s.SourceObjectId)).ToHashSet(StringComparer.Ordinal);
        if (game.State.PendingPayment?.Reason is { } reason)
            foreach (var id in new[] { AttackerObjectId, SecondAttackerObjectId })
                if (reason.EndsWith("|" + id, StringComparison.Ordinal)) sources.Add(id);
        Assert.Contains(AttackerObjectId, sources);
        Assert.Contains(SecondAttackerObjectId, sources);
        Assert.All(new[] { AttackerObjectId, SecondAttackerObjectId },
            id => Assert.Contains(id, game.State.PlayerZones["P1"].Battlefields));
        Assert.DoesNotContain(settled.Events, e => e.Kind is "COST_PAID" or "UNIT_RETURNED_TO_HAND");
        if (minefield)
        {
            Assert.Contains(BattlefieldObjectId, sources);
            Assert.DoesNotContain(settled.Events, e => e.Kind == "CARDS_MILLED");
            Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count);
        }

        var queue = game.State.TriggerQueue.ToArray();
        Assert.All(queue, t => Assert.Equal("P1", t.ControllerId));
        var firstIndex = Array.FindIndex(queue, t => t.SourceObjectId == AttackerObjectId);
        var secondIndex = Array.FindIndex(queue, t => t.SourceObjectId == SecondAttackerObjectId);
        Assert.True(firstIndex >= 0 && secondIndex >= 0);
        (queue[firstIndex], queue[secondIndex]) = (queue[secondIndex], queue[firstIndex]);
        var order = queue.Select(t => t.TriggerId).ToArray();
        var ordered = await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        Assert.Equal(order, Assert.IsType<string[]>(Assert.Single(ordered.Events, e => e.Kind == "TRIGGERS_ORDERED").Payload["orderedTriggerIds"]));
        var first = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var paidSource = Assert.Single(game.State.StackItems, s => s.StackItemId == first.ResolvingStackItemId).SourceObjectId;
        await game.Act("P1", new PayCostCommand(first.PaymentId, first.PaymentWindow, ["SPEND_MANA:1"]));
        var second = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        var declinedSource = Assert.Single(game.State.StackItems, s => s.StackItemId == second.ResolvingStackItemId).SourceObjectId;
        Assert.NotEqual(paidSource, declinedSource);
        await game.Act("P1", new PayCostCommand(second.PaymentId, second.PaymentWindow, ["DECLINE"]));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        for (var step = 0; step < 4 && game.State.StackItems.Count > 0; step++) await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Contains(paidSource, game.State.PlayerZones["P1"].Hand);
        Assert.Contains(declinedSource, game.State.PlayerZones["P1"].Battlefields);
        if (minefield)
        {
            Assert.Empty(game.State.PlayerZones["P1"].MainDeck);
            Assert.Contains("VAYNE-DECK-1", game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains("VAYNE-DECK-2", game.State.PlayerZones["P1"].Graveyard);
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task CrimsonTreantAddsAnotherVayneConquestConfirmationWithItsOwnCost()
    {
        var (initial, game, settled) = await OpenVaynePaymentTimingBattle(mana: 2, treeSecond: true);
        Assert.DoesNotContain(settled.Events, e => e.Kind is "COST_PAID" or "UNIT_RETURNED_TO_HAND");
        if (game.State.PendingPayment is null && game.State.PendingCardChoice is null && game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var represented = game.State.TriggerQueue.Count(t => t.SourceObjectId == AttackerObjectId)
            + game.State.StackItems.Where(s => s.SourceObjectId == AttackerObjectId).Sum(s => s.EffectRepeatCount);
        if (game.State.PendingPayment is { ResolvingStackItemId: null, Reason: { } reason }
            && reason.EndsWith("|" + AttackerObjectId, StringComparison.Ordinal)) represented++;
        Assert.Equal(2, represented);

        var decisions = 0;
        for (var step = 0; step < 8 && decisions < 2; step++)
        {
            if (game.State.PendingPayment is { } payment)
            {
                var source = game.State.StackItems.Single(s => s.StackItemId == payment.ResolvingStackItemId).SourceObjectId;
                Assert.Equal(AttackerObjectId, source);
                await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
                    [decisions++ == 0 ? "SPEND_MANA:1" : "DECLINE"]));
            }
            else if (game.State.PendingCardChoice is { } choice)
                await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    choice.LegalObjectIds.Take(choice.RequiredCount).ToArray()));
            else
                throw new InvalidOperationException("Both Vayne confirmations must be handled before responses.");
        }
        Assert.Equal(2, decisions);
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), AttackerObjectId));
        Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_PAYMENT_DECLINED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), AttackerObjectId));
        AssertVayneJsonColdFrame(initial, game);
        for (var step = 0; step < 10 && (game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0); step++)
        {
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (game.State.PendingCardChoice is { } choice)
                await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    choice.LegalObjectIds.Take(choice.RequiredCount).ToArray()));
            else
            {
                Assert.Null(game.State.PendingPayment);
                await game.Top();
            }
        }
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Contains(AttackerObjectId, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Game, ResolutionResult Settled)>
        OpenVaynePaymentTimingBattle(int mana = 1, bool twoVaynes = false, bool minefield = false,
            bool spellOnly = false, bool includeRune = false, bool responseCard = false, bool swapSides = false,
            bool treeSecond = false)
    {
        var initial = SingleDefenderWireState(true) with { Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" } };
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [BattlefieldObjectId] = initial.CardObjects[BattlefieldObjectId] with {
                ControllerId = "P2", CardNo = minefield ? "SFD·212/221" : "OGN·275/298" },
            [AttackerObjectId] = initial.CardObjects[AttackerObjectId] with {
                CardNo = "OGN·035/298", Power = 2, Tags = [CardObjectTags.UnitCard, "强攻3"] },
            [BulwarkDefenderObjectId] = initial.CardObjects[BulwarkDefenderObjectId] with { Power = 1 }
        };
        if (twoVaynes) cards[SecondAttackerObjectId] = cards[SecondAttackerObjectId] with {
            CardNo = "OGN·035/298", Power = 2, Tags = [CardObjectTags.UnitCard, "强攻3"] };
        if (treeSecond) cards[SecondAttackerObjectId] = cards[SecondAttackerObjectId] with {
            CardNo = "UNL-029/219", Power = 4, Tags = [CardObjectTags.UnitCard] };
        initial = initial with { CardObjects = cards, RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(mana, 0, spellOnlyMana: spellOnly ? 2 : 0), ["P2"] = new(8, 4) } };
        if (includeRune)
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["VAYNE-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("VAYNE-RUNE", "UNL-R05", "P1") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "VAYNE-RUNE"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["VAYNE-RUNE"] = new("P1", "BASE") } };
        if (minefield)
        {
            var withDeck = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["VAYNE-DECK-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("VAYNE-DECK-1", "OGN·096/298", "P1"),
                ["VAYNE-DECK-2"] = OfficialGarenConquestBattlefieldCountTests.Printed("VAYNE-DECK-2", "OGN·096/298", "P1") };
            initial = initial with { CardObjects = withDeck,
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = initial.PlayerZones["P1"] with { MainDeck = ["VAYNE-DECK-1", "VAYNE-DECK-2"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["VAYNE-DECK-1"] = new("P1", "MAIN_DECK"), ["VAYNE-DECK-2"] = new("P1", "MAIN_DECK") } };
        }
        if (swapSides) initial = SwapVaynePlayers(initial);
        var attacker = swapSides ? "P2" : "P1";
        var defender = swapSides ? "P1" : "P2";
        if (responseCard)
            initial = AttackDamageDistributionTests.Spell(initial, "VAYNE-RESPONSE", "OGN·104/298", defender);
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act(attacker, new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId, SecondAttackerObjectId],
            [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        // The opponent's real response card can leave a battle-focus window
        // open before damage assignment. Pass that window with normal commands.
        for (var step = 0; step < 4; step++)
        {
            var candidates = ResolutionResult.BuildPrompts(game.State)[attacker].Candidates ?? [];
            if (candidates.Any(c => c.Action == CommandTypes.AssignCombatDamage)) break;
            if (game.State.PriorityPlayerId is { } priority)
                await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus)
                await game.Act(focus, new PassFocusCommand());
            else if (candidates.Any(c => c.Action == CommandTypes.DeclareBattle))
                await game.Act(attacker, new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId, SecondAttackerObjectId],
                    [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
            else
                throw new InvalidOperationException("Battle did not expose a response or damage-assignment action.");
        }
        Assert.Contains(ResolutionResult.BuildPrompts(game.State)[attacker].Candidates ?? [],
            c => c.Action == CommandTypes.AssignCombatDamage);
        await game.Act(attacker, new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BulwarkDefenderObjectId, 5),
                new(SecondAttackerObjectId, BulwarkDefenderObjectId, twoVaynes ? 5 : treeSecond ? 4 : 2)]));
        var settled = await game.Act(defender, new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, AttackerObjectId, 1)]));
        if (!twoVaynes && !treeSecond && game.State.PendingPayment is null && game.State.TriggerQueue.Count > 0)
            await game.Act(attacker, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        return (initial, game, settled);
    }

    private static MatchState SwapVaynePlayers(MatchState state)
    {
        static string? Swap(string? id) => id switch { "P1" => "P2", "P2" => "P1", _ => id };
        static Dictionary<string, T> SwapKeys<T>(IReadOnlyDictionary<string, T> values)
            => values.ToDictionary(pair => Swap(pair.Key)!, pair => pair.Value, StringComparer.Ordinal);
        return state with {
            ActivePlayerId = "P2", TurnPlayerId = "P2",
            PlayerZones = SwapKeys(state.PlayerZones), RunePools = SwapKeys(state.RunePools),
            PlayerScores = SwapKeys(state.PlayerScores), PlayerExperience = SwapKeys(state.PlayerExperience),
            PlayerCardsPlayedThisTurn = SwapKeys(state.PlayerCardsPlayedThisTurn),
            CardObjects = state.CardObjects.ToDictionary(pair => pair.Key,
                pair => pair.Value with { OwnerId = Swap(pair.Value.OwnerId), ControllerId = Swap(pair.Value.ControllerId) }, StringComparer.Ordinal),
            ObjectLocations = state.ObjectLocations.ToDictionary(pair => pair.Key,
                pair => pair.Value with { PlayerId = Swap(pair.Value.PlayerId)! }, StringComparer.Ordinal) };
    }

    private static MatchRecoveryFrame VayneRecoveryFrame(MatchState initial, StandbyDefendObserverTests.Driver game, MatchState state)
    {
        var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id,
            id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator)
            { OpeningSeed = initial.Seed };
    }

    private static void AssertVayneJsonColdFrame(MatchState initial, StandbyDefendObserverTests.Driver game)
    {
        var state = game.State;
        var frame = VayneRecoveryFrame(initial, game, state);
        Assert.Empty(frame.ValidationErrors);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, options), options)!;
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(state)[player]),
                MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(state)[player]),
                MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
}
