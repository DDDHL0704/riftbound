using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialInsightAndSpellLockTests;

namespace Riftbound.ConformanceTests;

public sealed class ClairvoyanceTests
{
    private const string Card = "VEN·056";

    [Theory]
    [InlineData("KEEP")]
    [InlineData("SOME")]
    [InlineData("ALL")]
    public async Task FiveCardInsightAndPrivateOrderingFinishBeforeEitherDraw(string branch)
    {
        var initial = Position();
        var played = await Act(initial, "P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: []));
        Assert.Null(played.State.PendingCardChoice);
        var opened = await ResolveTop(played.State);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow);
        Assert.Equal(new[] { "D1", "D2", "D3", "D4", "D5" }, choice.ContextObjectIds);
        Assert.Equal(5, choice.MaxCount);
        Assert.Empty(opened.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(opened.Events, e => e.Kind == "CARD_DRAWN");
        Assert.Empty(opened.State.CardPlays.Resolved);
        Private(opened); Restore(opened.State);
        string[] recycled = branch switch { "SOME" => ["D2", "D4"], "ALL" => ["D1", "D2", "D3", "D4", "D5"], _ => [] };
        var selected = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, recycled));
        if (branch != "ALL")
        {
            Assert.Empty(selected.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(selected.Events, e => e.Kind == "CARD_DRAWN");
            Assert.Empty(selected.State.CardPlays.Resolved);
            choice = Assert.IsType<PendingCardChoiceState>(selected.State.PendingCardChoice);
            Assert.Equal("INSIGHT_ORDER", choice.ChoiceWindow);
            Assert.Equal(branch == "KEEP" ? 5 : 3, choice.RequiredCount);
            Private(selected); Restore(selected.State);
            selected = await Act(selected.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                branch == "KEEP" ? ["D5", "D4", "D3", "D2", "D1"] : ["D5", "D3", "D1"]));
        }
        Assert.Null(selected.State.PendingCardChoice);
        Assert.Empty(selected.State.StackItems);
        Assert.Contains("SPELL", selected.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(branch switch { "KEEP" => new[] { "D5", "D4" }, "SOME" => ["D5", "D3"], _ => ["D6", "D7"] },
            selected.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, Assert.Single(selected.Events, e => e.Kind == "CARD_DRAWN").Payload["count"]);
        var expectedRemainder = Enumerable.Range(1, 7).Select(i => "D" + i).Except(selected.State.PlayerZones["P1"].Hand).Order();
        Assert.Equal(expectedRemainder, selected.State.PlayerZones["P1"].MainDeck.Order());
        Assert.Equal("SPELL", Assert.Single(selected.State.CardPlays.Resolved).Source.ObjectId);
        Private(selected); Restore(selected.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ShortDeckInsightDoesNotBurnOutButTheFollowingDrawMayEndTheMatch(int cards)
    {
        var initial = Position(cards) with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var opened = await ResolveTop((await Act(initial, "P1", new PlayCardCommand("SPELL", Card, []))).State);
        if (cards > 0)
        {
            Assert.Equal(7, opened.State.PlayerScores["P2"]);
            Assert.Equal(cards, opened.State.PendingCardChoice!.MaxCount);
            Assert.Empty(opened.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(opened.Events, e => e.Kind == "CARD_DRAWN");
            var choice = opened.State.PendingCardChoice;
            opened = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            if (cards > 1)
            {
                choice = opened.State.PendingCardChoice!;
                opened = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D3", "D2", "D1"]));
            }
        }
        Assert.Null(opened.State.PendingCardChoice);
        Assert.Empty(opened.State.StackItems);
        Assert.Equal(cards < 2 ? MatchStatuses.Finished : MatchStatuses.InProgress, opened.State.Status);
        Assert.Equal(cards < 2 ? "P2" : null, opened.State.WinnerPlayerId);
        Assert.Equal(Math.Min(2, cards), opened.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal("SPELL", Assert.Single(opened.State.CardPlays.Resolved).Source.ObjectId);
        Restore(opened.State);
    }

    [Fact]
    public async Task CounteredClairvoyanceDoesNotObserveOrDrawAndNeverRecordsSuccessfulResolution()
    {
        var initial = Position();
        var played = await Act(initial, "P1", new PlayCardCommand("SPELL", Card, []));
        var passed = await Act(played.State, "P1", new PassPriorityCommand());
        var counter = await Act(passed.State, "P2", new PlayCardCommand("COUNTER", "OGN·064/298", [Assert.Single(played.State.StackItems).StackItemId]));
        var done = await ResolveTop(counter.State);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, done.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(done.State.PlayerZones["P1"].Hand);
        Assert.Null(done.State.PendingCardChoice);
        Assert.DoesNotContain(done.Events, e => e.Kind == "CARD_DRAWN" || e.Kind.StartsWith("INSIGHT"));
        Assert.Contains(done.State.CardPlays.Finalized, e => e.Source.ObjectId == "SPELL");
        Assert.DoesNotContain(done.State.CardPlays.Resolved, e => e.Source.ObjectId == "SPELL");
        Restore(done.State);
    }

    [Fact]
    public async Task ReactionResolvesInsightAndBothDrawsBeforeTheOpponentsOlderSpell()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "OLDER", "OGN·048/298", "P2")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var state = (await Act(initial, "P2", new PlayCardCommand("OLDER", "OGN·048/298", []))).State;
        state = (await Act(state, "P2", new PassPriorityCommand())).State;
        state = (await Act(state, "P1", new PlayCardCommand("SPELL", Card, []))).State;
        Assert.Equal(2, state.StackItems.Count);
        state = (await ResolveTop(state)).State;
        var choice = state.PendingCardChoice!;
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(5, choice.MaxCount);
        Assert.Equal(initial.PlayerZones["P2"].MainDeck, state.PlayerZones["P2"].MainDeck);
        state = (await Act(state, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []))).State;
        choice = state.PendingCardChoice!;
        state = (await Act(state, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D5", "D4", "D3", "D2", "D1"]))).State;
        Assert.Equal(new[] { "D5", "D4" }, state.PlayerZones["P1"].Hand);
        Assert.Equal("OLDER", Assert.Single(state.StackItems).SourceObjectId);
        Assert.Equal(initial.PlayerZones["P2"].MainDeck, state.PlayerZones["P2"].MainDeck);
        state = (await ResolveTop(state)).State;
        Assert.Contains("E1", state.PlayerZones["P2"].Hand); Assert.Empty(state.StackItems);
        Restore(state);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheFifthObservedCardsOwnPlayWaitsUntilInsightAndBothDrawsFinish(bool banish)
    {
        var initial = AttackDamageDistributionTests.Spell(DeckObservationPlayTests.Position(), "CLAIR", Card, "P1");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        foreach (var id in new[] { "A", "B" })
            cards[id] = new(id, cardNo: "OGN·048/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        initial = initial with { Tick = 0, CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                MainDeck = ["A", "B", "END_PREFIX", "END_UNIT", "NOCT"] } } };
        var handBefore = initial.PlayerZones["P1"].Hand.Count - 1;
        var driver = DeckObservationPlayTests.Driver(initial, "clairvoyance-fifth-" + banish);
        await driver.Act("P1", new PlayCardCommand("CLAIR", Card, []));
        await driver.Top();
        Assert.NotNull(driver.State.PendingRuleChoice);
        Assert.Equal(handBefore, driver.State.PlayerZones["P1"].Hand.Count);
        await DeckObservationPlayTests.Decide(driver, banish);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(banish ? 4 : 5, choice.MaxCount);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(handBefore, driver.State.PlayerZones["P1"].Hand.Count);
        choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            banish ? ["END_UNIT", "END_PREFIX", "B", "A"] : ["NOCT", "END_UNIT", "END_PREFIX", "B", "A"]));
        Assert.Equal(handBefore + 2, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Contains("CLAIR", driver.State.PlayerZones["P1"].Graveyard);
        if (banish)
        {
            Assert.NotNull(driver.State.PendingEffectPlay);
            await driver.Act("P1", new PlayCardCommand("NOCT", DeckObservationPlayTests.Nocturne, []));
            Assert.Contains("NOCT", driver.State.PlayerZones["P1"].Base);
        }
        else
        {
            Assert.Null(driver.State.PendingEffectPlay);
            Assert.Contains("NOCT", driver.State.PlayerZones["P1"].Hand);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ReversalMakesTheActualResolvingControllerObserveAndDrawFromTheirOwnDeck()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "REVERSAL", "OGN·080/298", "P2");
        var state = (await Act(initial, "P1", new PlayCardCommand("SPELL", Card, []))).State;
        var parent = Assert.Single(state.StackItems).StackItemId;
        state = (await Act(state, "P1", new PassPriorityCommand())).State;
        state = (await Act(state, "P2", new PlayCardCommand("REVERSAL", "OGN·080/298", [parent], OptionalCosts: []))).State;
        state = (await ResolveTop(state)).State;
        state = (await ResolveTop(state)).State;
        var choice = Assert.IsType<PendingCardChoiceState>(state.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId); Assert.Equal(new[] { "E1", "E2" }, choice.ContextObjectIds);
        foreach (var id in new[] { "E1", "E2" })
        {
            Assert.DoesNotContain("\"" + id + "\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P1"]));
            Assert.DoesNotContain("\"" + id + "\"", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state)["P1"]));
        }
        state = (await Act(state, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []))).State;
        choice = state.PendingCardChoice!;
        state = (await Act(state, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["E2", "E1"]))).State;
        Assert.Contains("E1", state.PlayerZones["P2"].Hand); Assert.Contains("E2", state.PlayerZones["P2"].Hand);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, state.PlayerZones["P1"].MainDeck);
        Assert.Contains("SPELL", state.PlayerZones["P1"].Graveyard);
        Assert.Equal("P1", Assert.Single(state.CardPlays.Finalized, e => e.Source.ObjectId == "SPELL").PlayerId);
        Assert.Equal("P2", Assert.Single(state.CardPlays.Resolved, e => e.Source.ObjectId == "SPELL").PlayerId);
        Restore(state);
    }

    [Fact]
    public async Task FourManaLimitedRebuttalCannotTargetTheSevenManaClairvoyance()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "REBUTTAL", "VEN·152", "P2");
        var state = (await Act(initial, "P1", new PlayCardCommand("SPELL", Card, []))).State;
        state = (await Act(state, "P1", new PassPriorityCommand())).State;
        var command = new PlayCardCommand("REBUTTAL", "VEN·152", [Assert.Single(state.StackItems).StackItemId]);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("restricted", "P2", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(initial.RunePools["P2"], rejected.State.RunePools["P2"]);
        Restore(state);
    }

    [Fact]
    public async Task WrongPlayerOutsideTopDuplicateAndStaleChoicesDoNotDrawOrChangeState()
    {
        var state = (await ResolveTop((await Act(Position(), "P1", new PlayCardCommand("SPELL", Card, []))).State)).State;
        var choice = state.PendingCardChoice!;
        await Rejected("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await Rejected("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D6"]));
        await Rejected("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D1", "D1"]));
        await Rejected("P1", new ChooseCardsCommand(choice.ChoiceId + "-OLD", choice.ChoiceWindow, []));
        await Rejected("P1", new EndTurnCommand());
        state = (await Act(state, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []))).State;
        await Rejected("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        choice = state.PendingCardChoice!;
        await Rejected("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D1", "D2", "D3", "D4", "D4"]));
        await Rejected("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D1", "D2", "D3", "D4"]));
        Restore(state);
        async Task Rejected(string actor, GameCommand command)
        {
            var before = MatchStateHasher.Hash(state);
            var result = await new CoreRuleEngine().ResolveAsync(state, new("bad", actor, command.CmdType), command, default);
            Assert.False(result.Accepted); Assert.Empty(result.Events);
            Assert.Equal(before, MatchStateHasher.Hash(result.State));
            Assert.Empty(result.State.PlayerZones["P1"].Hand);
        }
    }

    [Theory]
    [InlineData("COUNT")]
    [InlineData("SIXTH")]
    [InlineData("DUPLICATE")]
    [InlineData("PLAYER")]
    [InlineData("PARENT")]
    public async Task PendingCountedInsightTamperingIsIndependentlyRejected(string branch)
    {
        var state = (await ResolveTop((await Act(Position(), "P1", new PlayCardCommand("SPELL", Card, []))).State)).State;
        var choice = state.PendingCardChoice!;
        var bad = branch switch {
            "COUNT" => choice with { MaxCount = 4 },
            "SIXTH" => choice with { LegalObjectIds = ["D2", "D3", "D4", "D5", "D6"], ContextObjectIds = ["D2", "D3", "D4", "D5", "D6"] },
            "DUPLICATE" => choice with { LegalObjectIds = ["D1", "D1", "D3", "D4", "D5"], ContextObjectIds = ["D1", "D1", "D3", "D4", "D5"] },
            "PLAYER" => choice with { PlayerId = "P2" },
            _ => choice with { DeckContext = choice.DeckContext! with { StackItemId = "OTHER" } } };
        Assert.Contains(Errors(state with { PendingCardChoice = bad }), e => e.Contains("Insight", StringComparison.Ordinal));
        Restore(state);
    }

    [Fact]
    public async Task CurrentExactSourceAndOldOneCardInsightConsumersKeepTheirSeparateInstructions()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == Card);
        Assert.Equal(37747, card.Id); Assert.Equal(7, card.Energy); Assert.Contains("{{洞察5}}", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Card, out var behavior));
        Assert.Equal(5, behavior.InsightCount); Assert.Equal(2, behavior.DrawAfterInsightCount);
        Assert.Equal(0, behavior.DrawCount); Assert.True(behavior.CanPlayDuringPriority);
        foreach (var old in new[] { "UNL-063/219", "UNL-131/219" })
        {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(old, out behavior));
            Assert.Equal(1, behavior.InsightCount); Assert.Equal(0, behavior.DrawAfterInsightCount);
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Riftbound.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var frozen = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName,
            "data/official/upstream/2026-10-14/source-refresh-20261013T164814Z/card-catalog.zh-CN.json")))!;
        var active = JsonNode.Parse(await File.ReadAllTextAsync(OfficialCardCatalog.DefaultCatalogPath()))!;
        Assert.True(JsonNode.DeepEquals(frozen["cards"]!.AsArray().Single(c => c!["id"]!.GetValue<int>() == 37747),
            active["cards"]!.AsArray().Single(c => c!["id"]!.GetValue<int>() == 37747)));
    }

    [Theory]
    [InlineData("keep")]
    [InlineData("some")]
    [InlineData("all")]
    [InlineData("burnout")]
    public async Task ProductionPrivateSelectionAndOrderingCommandsColdRecoverDeduplicateAndReplay(string branch)
    {
        var initial = Position(branch == "burnout" ? 1 : 7) with { Tick = 0 };
        if (branch == "burnout") initial = initial with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var state = initial; var engine = new CoreRuleEngine(); var journal = new Journal();
        var session = new MatchSession(state, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_CLAIRVOYANCE_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var steps = new List<string>(); var native = 0;
        Export("initial.json", initial);
        await Submit("play", "P1", new PlayCardCommand("SPELL", Card, [], OptionalCosts: []));
        for (var i = 0; i < 2; i++) await Submit("pass-" + i, state.PriorityPlayerId!, new PassPriorityCommand());
        var choice = state.PendingCardChoice!;
        Assert.Empty(state.PlayerZones["P1"].Hand);
        Assert.Equal(branch == "burnout" ? 1 : 5, choice.MaxCount);
        await Submit("recycle", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
            branch == "all" ? ["D1", "D2", "D3", "D4", "D5"] : branch == "some" ? ["D2", "D4"] : []));
        if (state.PendingCardChoice is { } ordering)
        {
            Assert.Empty(state.PlayerZones["P1"].Hand);
            await Submit("order", "P1", new ChooseCardsCommand(ordering.ChoiceId, ordering.ChoiceWindow,
                branch == "keep" ? ["D5", "D4", "D3", "D2", "D1"] : ["D5", "D3", "D1"]));
        }
        Assert.Null(state.PendingCardChoice); Assert.Empty(state.StackItems);
        Assert.Equal(branch switch { "keep" => new[] { "D5", "D4" }, "some" => ["D5", "D3"], "all" => ["D6", "D7"], _ => ["D1"] }, state.PlayerZones["P1"].Hand);
        Assert.Equal(branch == "burnout" ? MatchStatuses.Finished : MatchStatuses.InProgress, state.Status);
        Assert.Equal("SPELL", Assert.Single(state.CardPlays.Resolved).Source.ObjectId);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), state, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps);
        Export("receipt.json", new { branch, native, commands = journal.Entries.Count, events = Events().Length,
            coldRecoveries = steps.Count, state.Status, state.WinnerPlayerId, hands = state.PlayerZones["P1"].Hand, replay });
        if (native > 0) Assert.Equal(steps.Count, native);

        void Export(string name, object value)
        {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async Task Submit(string step, string actor, GameCommand fallback)
        {
            var prompt = session.PromptFor(actor); Export(step + "-prompt.json", prompt); Export(step + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) Export(step + "-quote.json", engine.PreviewPlayCard(state, actor, new("quote", prompt.PromptId!, state.Tick, play)));
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(state.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            if (dir is not null && File.Exists(Path.Combine(dir, step + "-command.json")))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, step + "-command.json")));
                raw = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw); native++;
            }
            var result = await session.SubmitAsync(actor, step, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State; Restore(state); steps.Add(step);
            var count = journal.Entries.Count;
            Assert.True((await session.SubmitAsync(actor, step, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            session = MatchSession.Restore(new(initial.RoomId, state.Tick, journal.Entries[^1].CompletedEventSequence,
                Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], state, initial), engine, journal);
            Assert.True((await session.SubmitAsync(actor, step, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var stale = await session.SubmitAsync(actor, "stale-" + step, command, raw, default);
            Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
            Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(stale.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static void Private(ResolutionResult result)
    {
        foreach (var id in Enumerable.Range(1, 7).Select(i => "D" + i))
        {
            Assert.DoesNotContain("\"" + id + "\"", JsonSerializer.Serialize(result.Prompts["P2"]));
            Assert.DoesNotContain("\"" + id + "\"", JsonSerializer.Serialize(result.Snapshots["P2"]));
            Assert.DoesNotContain("\"" + id + "\"", JsonSerializer.Serialize(result.Events));
        }
    }

    internal static MatchState Position(int deckCards = 7)
    {
        var state = CardPlayHistoryTests.Initial();
        var objects = state.CardObjects.Where(p => !p.Key.StartsWith("D", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value);
        objects["SPELL"] = objects["SPELL"] with { CardNo = Card, ManaCost = 7 };
        for (var i = 1; i <= deckCards; i++) objects["D" + i] = new("D" + i, cardNo: "OGN·048/298", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        return state with { CardObjects = objects, PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { MainDeck = Enumerable.Range(1, deckCards).Select(i => "D" + i).ToArray() } } };
    }
}
