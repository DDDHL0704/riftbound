using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CopiedDuelInsightTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachActuallyAttachedDianaTextCapturesItsOwnOptionalDuelInsight(int songs)
    {
        // CN719.1/383.3/437. This is a controlled position, not a legal
        // opening or native product acceptance. Owner and controller differ.
        var initial = Position(); var driver = new StandbyDefendObserverTests.Driver(initial);
        await Start(driver, songs);
        var contexts = Contexts(driver.State);
        Assert.Equal(songs + 1, contexts.Length);
        Assert.Single(contexts, c => c.Origin is null);
        Assert.Equal(songs, contexts.Count(c => c.Origin is not null));
        Assert.All(contexts, c => {
            Assert.Equal("P2", c.ControllerId); Assert.Equal("UNL-079/219", c.CardNo);
            Assert.Equal(1, c.Count); Assert.Equal("HILL", c.BattlefieldObjectId);
            Assert.Equal("P1", c.ReturnFocusPlayerId); Assert.False(c.PaymentAccepted);
        });
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    public static IEnumerable<object[]> PaymentCases()
    {
        for (var songs = 0; songs <= 2; songs++)
        for (var mask = 0; mask < 1 << (songs + 1); mask++)
        foreach (var recycle in new[] { false, true }) yield return [songs, mask, recycle];
    }

    [Theory]
    [MemberData(nameof(PaymentCases))]
    public async Task EachOptionalCostIsConfirmedBeforeIndependentCurrentDeckInsightRevealAndSpellDraw(int songs, int mask, bool recycle)
    {
        var initial = Position(); var cards = initial.CardObjects.ToDictionary();
        var spells = new HashSet<string>();
        for (var i = 2; i <= 8; i += 2)
        {
            var id = "P2-D" + i; spells.Add(id);
            cards[id] = cards[id] with { CardNo = "OGN·083/298", Tags = [CardObjectTags.SpellCard] };
        }
        initial = initial with { CardObjects = cards };
        var driver = new StandbyDefendObserverTests.Driver(initial); await Start(driver, songs); await Order(driver);
        var deck = driver.State.PlayerZones["P2"].MainDeck.ToList();
        var hand = driver.State.PlayerZones["P2"].Hand.ToList(); var payments = new HashSet<string>();
        var paid = 0; var tapped = 0;
        for (var i = 0; i <= songs; i++)
        {
            var pending = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            Assert.Equal("P2", pending.PlayerId); Assert.Equal("INSIGHT_EFFECT", pending.PaymentWindow);
            Assert.Equal(1, pending.ManaCost); Assert.True(payments.Add(pending.PaymentId));
            Assert.Null(driver.State.PendingCardChoice);
            Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "INSIGHT_REQUESTED" or "CARD_REVEALED");
            var pay = (mask & (1 << i)) != 0;
            if (pay && driver.State.RunePools["P2"].Mana == 0) await driver.Act("P2", new TapRuneCommand("PAYR" + ++tapped));
            var mana = driver.State.RunePools["P2"].Mana;
            await driver.Act("P2", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
            Assert.Equal(mana - (pay ? 1 : 0), driver.State.RunePools["P2"].Mana);
            Assert.Equal(deck, driver.State.PlayerZones["P2"].MainDeck);
            Assert.Equal(hand, driver.State.PlayerZones["P2"].Hand);
            if (pay) paid++;
        }
        Assert.Null(driver.State.PendingPayment); Assert.Equal(paid, driver.State.StackItems.Count);
        for (var i = 0; i < paid; i++)
        {
            await driver.Top(); var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal("P2", choice.PlayerId);
            Assert.Equal([deck[0]], choice.ContextObjectIds);
            var privatePrompt = System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildPrompts(driver.State)["P1"]);
            Assert.DoesNotContain("\"" + deck[0] + "\"", privatePrompt);
            var viewed = deck[0];
            if (recycle) { deck.RemoveAt(0); deck.Add(viewed); }
            var revealed = deck[0]; var draw = spells.Contains(revealed);
            if (draw) { deck.RemoveAt(0); hand.Add(revealed); }
            var done = await driver.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, recycle ? [viewed] : []));
            var reveal = Assert.Single(done.Events, e => e.Kind == "CARD_REVEALED");
            Assert.Equal(revealed, reveal.Payload["cardObjectId"]);
            Assert.Equal(deck, driver.State.PlayerZones["P2"].MainDeck);
            Assert.Equal(hand, driver.State.PlayerZones["P2"].Hand);
            Assert.Equal(draw ? 1 : 0, done.Events.Count(e => e.Kind == "CARD_DRAWN"));
        }
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.TriggerQueue);
        Assert.Null(driver.State.PendingPayment); Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(paid, driver.Events().Count(e => e.Event.Kind == "INSIGHT_REQUESTED"));
        Assert.Equal(paid, driver.Events().Count(e => e.Event.Kind == "CARD_REVEALED"));
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState); Assert.Equal("P1", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static async Task Order(StandbyDefendObserverTests.Driver driver)
    {
        if (driver.State.TriggerQueue.Count > 0)
            await driver.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
    }

    public static IEnumerable<object[]> PublicForgeryCases()
        => from viewer in new[] { "P1", "P2", "spectator" }
           from ordered in new[] { false, true }
           from mutation in new[] { "REMOVE", "COUNT", "SOURCE", "TOKEN", "MALFORMED", "UNKNOWN_FIELD", "WRONG_SLOT", "BOTH_SLOTS" }
           select new object[] { viewer, ordered, mutation };

    [Theory]
    [MemberData(nameof(PublicForgeryCases))]
    public async Task PublicQueueAndStackViewsBindEachCapturedDuelContext(string viewer, bool ordered, string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Start(driver, 1);
        if (ordered) await Order(driver);
        var state = driver.State; ArmamentMightTests.PublicRecovery(state);
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0,
            snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        var target = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
        IEnumerable<object?> items = ordered ? target.Stack : Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(target.Timing["triggerQueue"]);
        var item = items.Cast<Dictionary<string, object?>>().First(i => i.GetValueOrDefault("duelInsight") is InsightTriggerContext { Origin: not null });
        var context = Assert.IsType<InsightTriggerContext>(item["duelInsight"]);
        switch (mutation)
        {
            case "REMOVE": item.Remove("duelInsight"); break;
            case "COUNT": item["duelInsight"] = context with { Count = 99 }; break;
            case "SOURCE": item["duelInsight"] = context with { Origin = context.Origin! with { Unit = new("UNIT", 0) } }; break;
            case "TOKEN": item["duelInsight"] = context with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo }; break;
            case "MALFORMED": item["duelInsight"] = "FORGED"; break;
            case "WRONG_SLOT": item.Remove("duelInsight"); item["lastBreathInsight"] = context; break;
            case "BOTH_SLOTS": item["lastBreathInsight"] = context; break;
            default:
                var raw = System.Text.Json.JsonSerializer.SerializeToNode(context)!.AsObject(); raw["unexpected"] = "FORGED";
                item["duelInsight"] = System.Text.Json.JsonSerializer.SerializeToElement(raw); break;
        }
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator),
            e => e.Contains("captured duel Insight", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SOURCE")] [InlineData("SOURCE_GENERATION")] [InlineData("EQUIPMENT")]
    [InlineData("EQUIPMENT_GENERATION")] [InlineData("REVISION")] [InlineData("CAPTURE_FUTURE")]
    [InlineData("CAPTURE_NEGATIVE")] [InlineData("FACE_FUTURE")] [InlineData("CARD")]
    [InlineData("TOKEN_FACTORY")] [InlineData("UNKNOWN_TOKEN_FACTORY")]
    [InlineData("COUNT")] [InlineData("CONTROLLER")] [InlineData("EVENT")] [InlineData("QUEUE_CARD")]
    public async Task StructuralRecoveryRejectsMalformedCopiedDuelSourceBindings(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position()); await Start(driver, 1);
        var trigger = Assert.Single(driver.State.TriggerQueue, t => t.InsightContext?.Origin is not null);
        var c = trigger.InsightContext!; var origin = c.Origin!;
        var changed = mutation switch {
            "SOURCE" => c with { Origin = origin with { Unit = origin.Unit with { ObjectId = "UNIT" } } },
            "SOURCE_GENERATION" => c with { SourceGeneration = 99 },
            "EQUIPMENT" => c with { Origin = origin with { Equipment = origin.Equipment with { ObjectId = "UNKNOWN" } } },
            "EQUIPMENT_GENERATION" => c with { Origin = origin with { Equipment = origin.Equipment with { Generation = 99 } } },
            "REVISION" => c with { Origin = origin with { Revision = 99 } },
            "CAPTURE_FUTURE" => c with { Origin = origin with { CapturedTick = driver.State.Tick + 1 } },
            "CAPTURE_NEGATIVE" => c with { Origin = origin with { CapturedTick = -1 } },
            "FACE_FUTURE" => c with { Origin = origin with { FaceRevision = 99 } },
            "CARD" => c with { Origin = origin with { CardNo = "UNL-062/219" } },
            "TOKEN_FACTORY" => c with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo },
            "UNKNOWN_TOKEN_FACTORY" => c with { SourceTokenFactoryCardNo = "UNKNOWN-TOKEN-FACTORY" },
            "COUNT" => c with { Count = 2 }, "CONTROLLER" => c with { ControllerId = "P1" }, _ => c };
        var altered = trigger with { InsightContext = changed,
            TriggeredByEventKind = mutation == "EVENT" ? "CARD_PLAYED" : trigger.TriggeredByEventKind,
            SourceCardNo = mutation == "QUEUE_CARD" ? "UNL-062/219" : trigger.SourceCardNo };
        var state = driver.State with { TriggerQueue = driver.State.TriggerQueue.Select(t => t.TriggerId == trigger.TriggerId ? altered : t).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("Insight", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CoordinatedShapeValidDuelChangesStillFailIndependentOriginalActionLog(bool changeFocus)
    {
        var initial = Position(); var driver = new StandbyDefendObserverTests.Driver(initial); await Start(driver, 1);
        var copied = Assert.Single(driver.State.TriggerQueue, t => t.InsightContext?.Origin is not null);
        var changed = driver.State with { TriggerQueue = driver.State.TriggerQueue.Select(t => t.TriggerId == copied.TriggerId
            ? t with { InsightContext = changeFocus ? t.InsightContext! with { ReturnFocusPlayerId = "P2" }
                : t.InsightContext! with { Origin = null } } : t).ToArray() };
        var commands = driver.Commands(); var events = driver.Events(); var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(changed); var prompts = ResolutionResult.BuildPrompts(changed);
        var views = changed.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, changed.Tick, sequence,
            snapshots[id], changed.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(changed.RoomId, changed.Tick, sequence, [], changed);
        var errors = MatchRecoveryValidator.Validate(changed.RoomId, sequence, commands, events, views, changed, changed.Tick, spectator);
        Assert.True(errors.Count == 0, string.Join(';', errors));
        var frame = new MatchRecoveryFrame(changed.RoomId, changed.Tick, sequence, commands, events, views, errors, changed, initial, spectator);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActuallyBorrowedAndRetainedDianaTextUsesItsHistoricalSourceFaceAtDuelStart(bool detachBeforeDuel)
    {
        var initial = AttachedRuleTextTests.AddSong(ShadySpectaclesTests.Position("UNL-079/219"), "SONG");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "DETACH", "SFD·011/221");
        var driver = new StandbyDefendObserverTests.Driver(initial); await Borrow(driver, "HOST");
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await driver.Top();
        if (detachBeforeDuel)
        {
            await driver.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await driver.Top();
            Assert.Equal("OGN·096/298", driver.State.CardObjects["HOST"].CardNo);
        }
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var contexts = Contexts(driver.State); Assert.Equal(detachBeforeDuel ? 1 : 2, contexts.Length);
        Assert.All(contexts, c => { Assert.Equal("UNL-079/219", c.CardNo); Assert.NotNull(c.Origin); });
        Assert.Contains(contexts, c => c.Origin!.Equipment.ObjectId == "SONG");
        if (!detachBeforeDuel) Assert.Contains(contexts, c => c.Origin!.Equipment.ObjectId == "GEAR");
        await OrderFor(driver, "P1");
        for (var i = 0; i < contexts.Length; i++)
        {
            var pending = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            await driver.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["DECLINE"]));
        }
        Assert.Empty(Contexts(driver.State)); Assert.Equal("P1", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActuallyCreatedBorrowedTokenKeepsConfirmedDuelInsightAfterActualReactionDestruction(bool song)
    {
        // VEN137 applicability remains unresolved; this is a controlled
        // lifecycle/security branch, not a regional legal-opening claim.
        var initial = ShadySpectaclesTests.Position("UNL-079/219");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "MAKE", "SFD·031/221");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "DESTROY", "SFD·163/221");
        if (song) initial = AttachedRuleTextTests.AddSong(initial, "SONG");
        var cards = initial.CardObjects.ToDictionary(); cards["F"] = cards["F"] with { CardNo = "OGN·294/298" };
        foreach (var id in new[] { "PAYR1", "PAYR2" })
            cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:red"]);
        initial = initial with { CardObjects = cards };
        foreach (var id in new[] { "PAYR1", "PAYR2" }) initial = LocalDestructionRecallTests.At(initial, id, "P1", "BASE");
        // The previous hold fixture has no opponent deck. This test advances
        // real turns, so give that player ordinary live deck/rune objects first.
        var opponentDeck = Enumerable.Range(1, 8).Select(i => "P2-FIX-D" + i).ToArray();
        var opponentRunes = Enumerable.Range(1, 3).Select(i => "P2-FIX-R" + i).ToArray();
        cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        foreach (var id in opponentDeck) { cards[id] = new(id, cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]); locations[id] = new("P2", "MAIN_DECK"); }
        foreach (var id in opponentRunes) { cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.RuneCard, "COLOR:red"]); locations[id] = new("P2", "RUNE_DECK"); }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { MainDeck = opponentDeck, RuneDeck = opponentRunes } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("MAKE", "SFD·031/221", [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo).ObjectId;
        await Borrow(driver, token);
        if (song) { await driver.Act("P1", new AssembleEquipmentCommand("SONG", token, ["ASSEMBLE_GREEN"])); await driver.Top(); }
        await driver.Act("P1", new EndTurnCommand()); await driver.Act("P2", new EndTurnCommand());
        await driver.Act("P1", new MoveUnitCommand(token, "BASE", "BATTLEFIELD:F", []));
        var count = song ? 2 : 1; Assert.Equal(count, Contexts(driver.State).Length); await OrderFor(driver, "P1");
        for (var i = 1; i <= count; i++)
        {
            var pending = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            await driver.Act("P1", new TapRuneCommand("PAYR" + i));
            await driver.Act("P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["PAY"]));
        }
        // SFD163 has Reaction. Malzahar's Swift resource skill cannot be
        // activated during confirmation or this closed response window.
        await driver.Act("P1", new TapRuneCommand("R1", ["R1", "R2"]));
        await driver.Act("P1", new PlayCardCommand("DESTROY", "SFD·163/221", [token, "HOST"])); await driver.Top();
        Assert.False(driver.State.CardObjects.ContainsKey(token));
        Assert.Equal(count, Contexts(driver.State).Length);
        Assert.All(Contexts(driver.State), c => { Assert.Equal(P6TokenFactoryCatalog.SandSoldierTokenCardNo, c.SourceTokenFactoryCardNo); Assert.Equal(token, c.Origin!.Unit.ObjectId); });
        foreach (var factory in new string?[] { null, "UNKNOWN-TOKEN-FACTORY" })
        {
            var bad = driver.State with { StackItems = driver.State.StackItems.Select(t => t.InsightContext is { } c
                ? t with { InsightContext = c with { SourceTokenFactoryCardNo = factory } } : t).ToArray() };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(bad));
        }
        for (var i = 0; i < count; i++)
        {
            var resolved = await driver.Top();
            if (driver.State.PendingCardChoice is { } choice)
                resolved = await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            Assert.DoesNotContain(resolved.Events, e => e.Kind.Contains("BURN", StringComparison.Ordinal));
        }
        Assert.Empty(Contexts(driver.State)); Assert.Null(driver.State.PendingPayment); Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState); Assert.Equal("P1", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task Borrow(StandbyDefendObserverTests.Driver driver, string host)
    {
        await driver.Act("P1", new AssembleEquipmentCommand("GEAR", host, ["ASSEMBLE_YELLOW"])); await driver.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        await driver.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("UNL-079/219", driver.State.CardObjects[host].CardNo);
    }

    private static async Task OrderFor(StandbyDefendObserverTests.Driver driver, string player)
    {
        if (driver.State.TriggerQueue.Count > 0)
            await driver.Act(player, new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
    }

    [Fact]
    public async Task AlternateDianaPrintingRetainsItsExactCopiedCardIdentity()
    {
        var initial = Position(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["DIANA"] = initial.CardObjects["DIANA"] with { CardNo = "UNL-079a/219" } } };
        var driver = new StandbyDefendObserverTests.Driver(initial); await Start(driver, 1); await Order(driver);
        Assert.Equal(2, Contexts(driver.State).Length);
        Assert.All(Contexts(driver.State), c => Assert.Equal("UNL-079a/219", c.CardNo));
        Assert.Equal("UNL-079a/219", Assert.Single(Contexts(driver.State), c => c.Origin is not null).Origin!.CardNo);
        for (var i = 0; i < 2; i++)
        {
            var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            await driver.Act("P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        }
        Assert.Empty(Contexts(driver.State)); Assert.Equal("P1", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task EmptyOrOneSpellDeckFinishesEachPaidDuelInstructionWithoutAnExtraDraw(int remaining)
    {
        var initial = Position(); var deck = initial.PlayerZones["P2"].MainDeck.Take(remaining).ToArray();
        var removed = initial.PlayerZones["P2"].MainDeck.Skip(remaining).ToHashSet();
        var cards = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary();
        if (remaining == 1) cards[deck[0]] = cards[deck[0]] with { CardNo = "OGN·083/298", Tags = [CardObjectTags.SpellCard] };
        initial = initial with { CardObjects = cards, ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { MainDeck = deck } } };
        var driver = new StandbyDefendObserverTests.Driver(initial); await Start(driver, 1); await Order(driver);
        var scores = driver.State.PlayerScores;
        for (var i = 1; i <= 2; i++)
        {
            var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            await driver.Act("P2", new TapRuneCommand("PAYR" + i));
            await driver.Act("P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        }
        for (var i = 0; i < 2; i++)
        {
            var done = await driver.Top();
            if (driver.State.PendingCardChoice is { } choice)
                done = await driver.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            Assert.DoesNotContain(done.Events, e => e.Kind.Contains("BURN", StringComparison.Ordinal));
        }
        Assert.Empty(driver.State.PlayerZones["P2"].MainDeck); Assert.Equal(remaining, driver.State.PlayerZones["P2"].Hand.Count);
        Assert.Equal(remaining, driver.Events().Count(e => e.Event.Kind == "CARD_REVEALED"));
        Assert.Equal(scores, driver.State.PlayerScores); Assert.Empty(Contexts(driver.State));
        Assert.Null(driver.State.PendingPayment); Assert.Null(driver.State.PendingCardChoice); Assert.Equal("P1", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task IndependentPaymentsAndPrivateChoiceRejectForeignStaleOutsideAndDuplicateIntentWithoutMutation()
    {
        var initial = Position(); var driver = new StandbyDefendObserverTests.Driver(initial); await Start(driver, 1); await Order(driver);
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        await Reject(driver.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await Reject(driver.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"])); // no mana yet
        await Reject(driver.State, "P2", new PayCostCommand("STALE", payment.PaymentWindow, ["DECLINE"]));
        await Reject(driver.State, "P2", new PayCostCommand(payment.PaymentId, "WRONG", ["DECLINE"]));
        await Reject(driver.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY", "PAY"]));
        await driver.Act("P2", new TapRuneCommand("PAYR1"));
        await driver.Act("P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        var second = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment); Assert.NotEqual(payment.PaymentId, second.PaymentId);
        await Reject(driver.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await driver.Act("P2", new PayCostCommand(second.PaymentId, second.PaymentWindow, ["DECLINE"]));
        await driver.Top(); var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await Reject(driver.State, "P2", new ChooseCardsCommand("STALE", choice.ChoiceWindow, []));
        await Reject(driver.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        await Reject(driver.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.ContextObjectIds[0], choice.ContextObjectIds[0]]));
        await driver.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Empty(Contexts(driver.State)); Assert.Equal("P1", driver.State.FocusPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-copied-duel", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }

    internal static InsightTriggerContext[] Contexts(MatchState state) => state.TriggerQueue.Select(t => t.InsightContext)
        .Concat(state.StackItems.Select(t => t.InsightContext)).Where(c => c?.Kind == "DUEL").Cast<InsightTriggerContext>().ToArray();

    internal static async Task Start(StandbyDefendObserverTests.Driver driver, int songs)
    {
        for (var i = 1; i <= songs; i++)
        {
            await driver.Act("P2", new AssembleEquipmentCommand("SONG" + i, "DIANA", ["ASSEMBLE_GREEN"]));
            await driver.Top();
            Assert.Equal(driver.State.CardObjects["DIANA"].CardNo, driver.State.CardObjects["SONG" + i].AttachedRuleText!.CardNo);
        }
        await driver.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", driver.State.TurnPlayerId);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:HILL", []));
    }

    internal static MatchState Position()
    {
        var cards = new Dictionary<string, CardObjectState> {
            ["HILL"] = new("HILL", cardNo: "OGN·294/298", ownerId: "P2", controllerId: "P2", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["OTHER"] = new("OTHER", cardNo: "OGN·278/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["UNIT"] = new("UNIT", cardNo: "OGN·001/298", ownerId: "P1", controllerId: "P1", power: 2, tags: [CardObjectTags.UnitCard]),
            ["DIANA"] = new("DIANA", cardNo: "UNL-079/219", ownerId: "P1", controllerId: "P2", power: 3, tags: [CardObjectTags.UnitCard]) };
        var locations = new Dictionary<string, ObjectLocationState> {
            ["HILL"] = new("P2", "BATTLEFIELD", "HILL"), ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER"),
            ["UNIT"] = new("P1", "BASE"), ["DIANA"] = new("P2", "BATTLEFIELD", "HILL") };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 8).Select(i => player + "-D" + i).ToArray();
            var runes = Enumerable.Range(1, 3).Select(i => player + "-R" + i).ToArray();
            foreach (var id in deck) { cards[id] = new(id, cardNo: "OGN·001/298", ownerId: player, controllerId: player, tags: [CardObjectTags.UnitCard]); locations[id] = new(player, "MAIN_DECK"); }
            foreach (var id in runes) { cards[id] = new(id, cardNo: "OGN·007/298", ownerId: player, controllerId: player, tags: [CardObjectTags.RuneCard, "COLOR:red"]); locations[id] = new(player, "RUNE_DECK"); }
            zones[player] = PlayerZones.Empty with { MainDeck = deck, RuneDeck = runes,
                Base = player == "P1" ? ["UNIT"] : [], Battlefields = player == "P1" ? ["OTHER"] : ["HILL", "DIANA"] };
        }
        foreach (var id in new[] { "PAYR1", "PAYR2", "PAYR3" })
        {
            cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P2", "BASE");
        }
        foreach (var id in new[] { "SONG1", "SONG2" })
        {
            cards[id] = new(id, cardNo: "SFD·059/221", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]);
            locations[id] = new("P2", "BASE");
        }
        zones["P2"] = zones["P2"] with { Base = ["PAYR1", "PAYR2", "PAYR3", "SONG1", "SONG2"] };
        return new("controlled-copied-duel", 0, 5, "P2", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) });
    }
}
