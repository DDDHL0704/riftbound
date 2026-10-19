using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class EmpoweredInsightTests
{
    [Fact]
    public async Task ExactCurrentApprenticeMageSourceHasOrdinaryUnitEmpowerAndConditionalMight()
    {
        using var source = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "apprentice-mage-source.json")));
        var raw = source.RootElement.GetProperty("rawApiCard");
        Assert.Equal(37738, raw.GetProperty("id").GetInt64());
        Assert.Equal("VEN·047", raw.GetProperty("cardNo").GetString());
        Assert.False(raw.GetProperty("isPreview").GetBoolean());
        Assert.Equal("VEN·047/166", source.RootElement.GetProperty("printedImageCardNo").GetString());
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == "VEN·047");
        Assert.Equal("见习法师", card.CardName);
        Assert.Equal(raw.GetProperty("cardEffect").GetString(), card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Equal(3, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal(2, Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives.Single().Mana);
        Assert.Equal(1, EmpowerParser.Self(card.CardEffect).Might);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("VEN·047", out var definition));
        Assert.True(definition.PlaysSourceToBaseAsUnit);
        Assert.Equal(3, definition.ManaCost); Assert.Equal(3, definition.SourceUnitPower);
    }

    [Fact]
    public async Task RealEmpowerResolutionTriggersPrivateInsightTwoAndRetainedTopOrder()
    {
        var state = await EmpowerTests.Enter("VEN·047");
        var deck = state.PlayerZones["P1"].MainDeck.ToArray();
        var mana = state.RunePools["P1"].Mana;
        var activated = await EmpowerTests.Activate(state);
        Assert.Equal(mana - 2, activated.State.RunePools["P1"].Mana);
        Assert.False(activated.State.CardObjects["HOST"].IsEmpowered);
        Assert.Empty(activated.State.TriggerQueue);
        Assert.Equal(deck, activated.State.PlayerZones["P1"].MainDeck);
        var empowered = await Top(activated.State);
        Assert.True(empowered.State.CardObjects["HOST"].IsEmpowered);
        Assert.Equal(4, ArmamentMightTests.Might(empowered.State));
        Assert.Single(empowered.Events, e => e.Kind == "OBJECT_EMPOWERED");
        var trigger = Assert.Single(empowered.State.StackItems, i => i.InsightContext is not null);
        Assert.Equal("EMPOWERED", trigger.InsightContext!.Kind);
        Assert.Equal(2, trigger.InsightContext.Count);
        Assert.Equal("VEN·047", trigger.CardNo);
        Assert.Equal("P1", trigger.ControllerId);
        Assert.Equal(deck, empowered.State.PlayerZones["P1"].MainDeck);
        Assert.Null(empowered.State.PendingCardChoice);
        Restore(empowered.State); ArmamentMightTests.PublicRecovery(empowered.State);
        var opened = await Top(empowered.State);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow);
        Assert.Equal(deck.Take(2), choice.ContextObjectIds);
        Assert.DoesNotContain("\"" + deck[0] + "\"", JsonSerializer.Serialize(opened.Prompts["P2"]));
        Assert.DoesNotContain("\"" + deck[1] + "\"", JsonSerializer.Serialize(opened.Snapshots["P2"]));
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
        var kept = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        var order = Assert.IsType<PendingCardChoiceState>(kept.State.PendingCardChoice);
        Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
        var done = await Act(kept.State, "P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, [deck[1], deck[0]]));
        Assert.Equal(new[] { deck[1], deck[0] }.Concat(deck.Skip(2)), done.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EachCurrentPrintedTextInstanceCapturesItsOwnEmpoweredInsightAndCurrentDeck(int songs)
    {
        var (driver, initial) = await Capture(songs);
        Assert.Equal(songs + 1, driver.State.TriggerQueue.Count);
        Assert.Equal(4 + songs, ArmamentMightTests.Might(driver.State));
        Assert.All(driver.State.TriggerQueue, trigger => {
            Assert.Equal("OBJECT_EMPOWERED", trigger.TriggeredByEventKind);
            Assert.Equal("VEN·047", trigger.SourceCardNo);
            Assert.Equal("EMPOWERED", trigger.InsightContext!.Kind);
            Assert.Equal(2, trigger.InsightContext.Count);
        });
        Assert.Single(driver.State.TriggerQueue, trigger => trigger.InsightContext!.Origin is null);
        Assert.Equal(songs, driver.State.TriggerQueue.Count(trigger => trigger.InsightContext!.Origin is not null));
        var originalHand = driver.State.PlayerZones["P1"].Hand.ToArray();
        var deck = driver.State.PlayerZones["P1"].MainDeck.ToList();
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            driver.State.TriggerQueue.Select(trigger => trigger.TriggerId).Reverse().ToArray()));
        for (var index = 0; index <= songs; index++)
        {
            await driver.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal(deck.Take(2), choice.ContextObjectIds);
            var viewed = deck.Take(2).ToArray();
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, index == 0 ? viewed : []));
            if (index == 0)
            {
                deck.RemoveRange(0, 2); deck.AddRange(viewed);
            }
            else
            {
                var order = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
                Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
                await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, viewed.Reverse().ToArray()));
                (deck[0], deck[1]) = (deck[1], deck[0]);
            }
            var actual = driver.State.PlayerZones["P1"].MainDeck;
            Assert.Equal(deck.Count, actual.Count);
            Assert.Equal(deck.Take(deck.Count - 2), actual.Take(deck.Count - 2));
            Assert.Equal(deck.TakeLast(2).Order(StringComparer.Ordinal), actual.TakeLast(2).Order(StringComparer.Ordinal));
            Assert.Equal(originalHand, driver.State.PlayerZones["P1"].Hand);
        }
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.TriggerQueue);
        Assert.Equal(initial.Seed, driver.State.Seed);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    public static IEnumerable<object[]> PublicForgeryCases()
        => from viewer in new[] { "P1", "P2", "spectator" }
           from ordered in new[] { false, true }
           from mutation in new[] { "REMOVE", "COUNT", "SOURCE", "TOKEN", "MALFORMED", "UNKNOWN_FIELD", "WRONG_SLOT", "BOTH_LAST_BREATH", "BOTH_DUEL" }
           select new object[] { viewer, ordered, mutation };

    [Theory]
    [MemberData(nameof(PublicForgeryCases))]
    public async Task PublicQueueAndStackViewsIndependentlyBindTheEmpoweredContext(string viewer, bool ordered, string mutation)
    {
        var (driver, _) = await Capture(1);
        if (ordered) await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            driver.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        var state = driver.State; ArmamentMightTests.PublicRecovery(state);
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0,
            snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        var target = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
        IEnumerable<object?> items = ordered ? target.Stack
            : Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(target.Timing["triggerQueue"]);
        var item = items.Cast<Dictionary<string, object?>>().First(value =>
            value.GetValueOrDefault("empoweredInsight") is InsightTriggerContext { Origin: not null });
        var context = Assert.IsType<InsightTriggerContext>(item["empoweredInsight"]);
        switch (mutation)
        {
            case "REMOVE": item.Remove("empoweredInsight"); break;
            case "COUNT": item["empoweredInsight"] = context with { Count = 1 }; break;
            case "SOURCE": item["empoweredInsight"] = context with { Origin = context.Origin! with { Unit = new("OTHER", 0) } }; break;
            case "TOKEN": item["empoweredInsight"] = context with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo }; break;
            case "MALFORMED": item["empoweredInsight"] = "FORGED"; break;
            case "WRONG_SLOT": item.Remove("empoweredInsight"); item["duelInsight"] = context; break;
            case "BOTH_LAST_BREATH": item["lastBreathInsight"] = context; break;
            case "BOTH_DUEL": item["duelInsight"] = context; break;
            default:
                var raw = System.Text.Json.JsonSerializer.SerializeToNode(context)!.AsObject(); raw["unexpected"] = "FORGED";
                item["empoweredInsight"] = JsonSerializer.SerializeToElement(raw); break;
        }
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator),
            error => error.Contains("captured empowered Insight", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task NativeAndCopiedEmpoweredInsightKeepShortDecksWithoutDrawingOrBurnout(int remaining)
    {
        var initial = EmpowerTests.Position("VEN·047");
        var removed = initial.PlayerZones["P1"].MainDeck.Skip(remaining).ToHashSet();
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = initial.PlayerZones["P1"].MainDeck.Take(remaining).ToArray() } },
            CardObjects = initial.CardObjects.Where(pair => !removed.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value),
            ObjectLocations = initial.ObjectLocations.Where(pair => !removed.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value) };
        var (driver, _) = await Capture(1, initial);
        await ResolveKeeping(driver, 2);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, driver.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(driver.Events(), entry => entry.Event.Kind == "CARD_DRAWN" || entry.Event.Kind.Contains("BURN", StringComparison.Ordinal));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualReactionSourceDepartureDistinguishesAnUnresolvedActivationFromAlreadyCapturedInsight(bool beforeEmpowered)
    {
        var initial = AttachedRuleTextTests.AddSong(AttachedRuleTextTests.WithSpell(
            EmpowerTests.Position("VEN·047"), "P1", beforeEmpowered ? "UNL-184/219" : PowerfulSacrificeCostTests.Card), "SONG2");
        var driver = await Prepare(2, initial);
        await driver.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        if (!beforeEmpowered)
        {
            await driver.Top(); Assert.Equal(3, driver.State.TriggerQueue.Count);
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                driver.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        }
        await driver.Act("P1", beforeEmpowered ? new PlayCardCommand("SPELL", "UNL-184/219", ["HOST"])
            : PowerfulSacrificeCostTests.Command());
        if (beforeEmpowered)
        {
            await driver.Top(); Assert.NotNull(driver.State.PendingEffectPlay);
            await driver.Act("P1", new PlayCardCommand("HOST", "VEN·047", [], Destination: "BATTLEFIELD:F"));
            Assert.Contains("HOST", driver.State.PlayerZones["P1"].Battlefields);
        }
        else Assert.Contains("HOST", driver.State.PlayerZones["P1"].Graveyard);
        Assert.False(driver.State.CardObjects["HOST"].IsEmpowered);
        Assert.True(driver.State.CardObjects["HOST"].ObjectGeneration > 0);
        if (beforeEmpowered)
        {
            await driver.Top();
            Assert.DoesNotContain(driver.Events(), entry => entry.Event.Kind == "OBJECT_EMPOWERED");
        }
        else
        {
            await driver.Top();
            Assert.Equal(3, driver.State.StackItems.Count);
            await ResolveKeeping(driver, 3);
            Assert.Single(driver.Events(), entry => entry.Event.Kind == "OBJECT_EMPOWERED");
        }
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.TriggerQueue);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SOURCE")] [InlineData("SOURCE_GENERATION")] [InlineData("EQUIPMENT")]
    [InlineData("EQUIPMENT_GENERATION")] [InlineData("REVISION")] [InlineData("CAPTURE_FUTURE")]
    [InlineData("CAPTURE_NEGATIVE")] [InlineData("FACE_FUTURE")] [InlineData("CARD")]
    [InlineData("TOKEN_FACTORY")] [InlineData("UNKNOWN_TOKEN_FACTORY")]
    [InlineData("COUNT")] [InlineData("CONTROLLER")] [InlineData("EVENT")] [InlineData("QUEUE_CARD")]
    public async Task StructuralRecoveryRejectsMalformedCopiedEmpoweredSourceBindings(string mutation)
    {
        var (driver, _) = await Capture(1);
        var trigger = Assert.Single(driver.State.TriggerQueue, item => item.InsightContext?.Origin is not null);
        var context = trigger.InsightContext!; var origin = context.Origin!;
        var changed = mutation switch {
            "SOURCE" => context with { Origin = origin with { Unit = origin.Unit with { ObjectId = "OTHER" } } },
            "SOURCE_GENERATION" => context with { SourceGeneration = 99 },
            "EQUIPMENT" => context with { Origin = origin with { Equipment = origin.Equipment with { ObjectId = "UNKNOWN" } } },
            "EQUIPMENT_GENERATION" => context with { Origin = origin with { Equipment = origin.Equipment with { Generation = 99 } } },
            "REVISION" => context with { Origin = origin with { Revision = 99 } },
            "CAPTURE_FUTURE" => context with { Origin = origin with { CapturedTick = driver.State.Tick + 1 } },
            "CAPTURE_NEGATIVE" => context with { Origin = origin with { CapturedTick = -1 } },
            "FACE_FUTURE" => context with { Origin = origin with { FaceRevision = 99 } },
            "CARD" => context with { Origin = origin with { CardNo = "UNL-062/219" } },
            "TOKEN_FACTORY" => context with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo },
            "UNKNOWN_TOKEN_FACTORY" => context with { SourceTokenFactoryCardNo = "UNKNOWN-TOKEN-FACTORY" },
            "COUNT" => context with { Count = 1 }, "CONTROLLER" => context with { ControllerId = "P2" }, _ => context };
        var altered = trigger with { InsightContext = changed,
            TriggeredByEventKind = mutation == "EVENT" ? "CARD_PLAYED" : trigger.TriggeredByEventKind,
            SourceCardNo = mutation == "QUEUE_CARD" ? "UNL-062/219" : trigger.SourceCardNo };
        var state = driver.State with { TriggerQueue = driver.State.TriggerQueue.Select(item => item.TriggerId == trigger.TriggerId ? altered : item).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), error => error.Contains("Insight", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoordinatedShapeValidOriginRemovalStillFailsTheOriginalAcceptedActionLog()
    {
        var (driver, initial) = await Capture(1);
        var copied = Assert.Single(driver.State.TriggerQueue, trigger => trigger.InsightContext?.Origin is not null);
        var changed = driver.State with { TriggerQueue = driver.State.TriggerQueue.Select(trigger => trigger.TriggerId == copied.TriggerId
            ? trigger with { InsightContext = trigger.InsightContext! with { Origin = null } } : trigger).ToArray() };
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

    private static async Task ResolveKeeping(StandbyDefendObserverTests.Driver driver, int expected)
    {
        if (driver.State.TriggerQueue.Count > 0) await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            driver.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        for (var index = 0; index < expected; index++)
        {
            var deck = driver.State.PlayerZones["P1"].MainDeck.ToArray(); await driver.Top();
            if (deck.Length == 0) { Assert.Null(driver.State.PendingCardChoice); continue; }
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal(deck.Take(2), choice.ContextObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            if (deck.Length > 1)
            {
                var order = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
                await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, deck.Take(2).ToArray()));
            }
            Assert.Equal(deck, driver.State.PlayerZones["P1"].MainDeck);
        }
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
    }

    private static async Task<(StandbyDefendObserverTests.Driver Driver, MatchState Initial)> Capture(int songs, MatchState? fixture = null)
    {
        var initial = fixture ?? EmpowerTests.Position("VEN·047");
        for (var index = 2; index <= songs; index++) initial = AttachedRuleTextTests.AddSong(initial, "SONG" + index);
        var driver = await Prepare(songs, initial);
        await driver.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [])); await driver.Top();
        return (driver, initial);
    }

    private static async Task<StandbyDefendObserverTests.Driver> Prepare(int songs, MatchState initial)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HOST", "VEN·047", []));
        if (driver.State.StackItems.Count > 0) await driver.Top();
        for (var index = 1; index <= songs; index++)
        {
            var id = index == 1 ? "GEAR" : "SONG" + index;
            await driver.Act("P1", new AssembleEquipmentCommand(id, "HOST", ["ASSEMBLE_GREEN"])); await driver.Top();
        }
        return driver;
    }
}
