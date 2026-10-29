using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN355.1.a/356.1.b.3: choose Haste before determining the final fee.
// CN445.2.a permits refusing that fee; CN445.1.b does not permit refusing zero.
public sealed class SelectedRecastFeeOpeningTests
{
    private const string Parent = "OGN·198/298", Gift = "UNL-125/219", Unit = "OGN·010/298";
    private const string Refusal = "REFUSE_PLAY_PAYMENT";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("alice", "decline-funded")] [InlineData("bob", "decline-funded")]
    [InlineData("alice", "decline-unfunded")] [InlineData("bob", "decline-unfunded")]
    [InlineData("alice", "pay")] [InlineData("bob", "pay")]
    [InlineData("alice", "free")] [InlineData("bob", "free")]
    [InlineData("alice", "zero")] [InlineData("bob", "zero")]
    public async Task ChosenHasteFeeCanBeRefusedWithoutForcingTheFreeAlternative(string actor, string route)
    {
        var room = "SELECTED-RECAST-FEE-" + actor + "-" + route;
        // Preserve the first player and both battlefields, with the three required cards
        // naturally drawn before rune channeling finishes for either seat.
        var initial = MatchState.Create(room) with { Seed = 21821 };
        var journal = new Journal(); var engine = new CoreRuleEngine();
        var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false);
        IMatchSession session = new MatchSession(initial, engine, journal, NoopMatchPlayerStore.Instance, options);
        var verifyColdRecoveryAfterEveryCommand = true;
        OfficialDecklist? deck = null; ResolutionResult? core = null, native = null; object? forgery = null;
        PlayCostQuoteDto? defaultQuote = null, selectedQuote = null;
        try
        {
            var catalog = await OfficialCardCatalog.LoadDefaultAsync();
            var fillers = new[] { "OGN·012/298", "OGN·013/298", "OGN·015/298", "OGN·018/298", "OGN·167/298",
                "OGN·175/298", "OGN·176/298", "UNL-126/219", "UNL-129/219", "SFD·014/221" };
            deck = new("UNL-185/219", "UNL-028/219", new[] { Parent, Gift, Unit }.Concat(fillers)
                .SelectMany(no => Enumerable.Repeat(no, 3)).Append("UNL-028/219").ToArray(),
                Enumerable.Repeat("UNL-R01", 4).Concat(Enumerable.Repeat("UNL-R05", 8)).ToArray(),
                ["UNL-219/219", "OGN·294/298", "OGN·297/298"]);
            var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
            Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
            Assert.Empty(initial.Seats); Assert.Empty(initial.CardObjects); Assert.Equal(0, initial.Tick);
            await session.EnsurePlayerAsync("alice", default); await Cold();
            await session.EnsurePlayerAsync("bob", default); await Cold();
            foreach (var player in new[] { "alice", "bob" })
                await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await Act("alice", new ReadyCommand()); await Act("bob", new ReadyCommand());
            await Act(State().ActivePlayerId, new MulliganCommand([]));
            await Act(State().OpeningSecondActionPlayerId!, new MulliganCommand([]));
            // Natural draws and rune channeling are setup for the payment assertion. Replaying the
            // entire accepted action log after every EndTurn makes this fixture quadratic in the
            // number of setup turns, while the critical payment/refusal stages below still perform
            // full cold recovery checks.
            verifyColdRecoveryAfterEveryCommand = false;
            for (var turns = 0; State().TurnPlayerId != actor || Runes().Length < 12
                || Hand(Parent) is null || Hand(Gift) is null || Hand(Unit) is null; turns++)
            {
                Assert.True(turns < 70 && State().Status == MatchStatuses.InProgress);
                await Act(State().TurnPlayerId, new EndTurnCommand());
            }
            verifyColdRecoveryAfterEveryCommand = true;
            await Cold();
            Assert.All(State().RunePools.Values, p => Assert.Equal(RunePool.Empty, p));
            var parent = Hand(Parent)!; var gift = Hand(Gift)!; var unit = Hand(Unit)!;
            var purple = Runes().Where(id => State().CardObjects[id].CardNo == "UNL-R05").Take(2).ToArray();
            var red = Runes().First(id => State().CardObjects[id].CardNo == "UNL-R01");
            verifyColdRecoveryAfterEveryCommand = false;
            var manaRunes = Runes().Except(purple).ToArray();
            await Act(actor, new TapRuneCommand(manaRunes[0], manaRunes));
            await Act(actor, new RecycleRuneCommand(purple[0], purple));
            verifyColdRecoveryAfterEveryCommand = true;
            await Cold();
            Assert.Equal(10, State().RunePools[actor].Mana);
            var deckCount = State().PlayerZones[actor].MainDeck.Count;
            await Act(actor, new PlayCardCommand(gift, Gift, [unit])); await Drain();
            Assert.Equal(deckCount - 2, State().PlayerZones[actor].MainDeck.Count);
            Assert.Contains(unit, State().PlayerZones[actor].Graveyard);
            var funded = route != "decline-unfunded";
            if (funded) await Act(actor, new RecycleRuneCommand(red));
            await Act(actor, new PlayCardCommand(parent, Parent, [unit])); await Drain();
            var pending = Assert.IsType<PendingEffectPlayState>(State().PendingEffectPlay);
            Assert.False(pending.Optional); Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower);
            Assert.Equal(1, State().RunePools[actor].Mana);
            Assert.Equal(funded ? 1 : 0, State().RunePools[actor].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
            var play = new PlayCardCommand(unit, Unit, []);
            var candidate = session.PromptFor(actor).Candidates!.Single(c => c.Action == CommandTypes.PlayCard);
            var haste = candidate.OptionalCosts!.Single(c => c.Id == "HASTE_READY").Id;
            var selected = play with { OptionalCosts = [haste] };
            defaultQuote = engine.PreviewPlayCard(State(), actor, Request(play));
            selectedQuote = engine.PreviewPlayCard(State(), actor, Request(selected));
            Assert.True(defaultQuote.CanPay, defaultQuote.Message); Assert.Equal(0, defaultQuote.Cost!.Mana);
            Assert.Equal(0, defaultQuote.Cost.GenericPower + defaultQuote.Cost.PowerByTrait.Values.Sum());
            Assert.True(selectedQuote.IsValid, selectedQuote.Message); Assert.Equal(funded, selectedQuote.CanPay);
            Assert.Equal(1, selectedQuote.Cost!.Mana); Assert.Equal(1, selectedQuote.Cost.PowerByTrait[RuneTrait.Red]);
            var before = MatchStateHasher.Hash(State());
            var commandCount = Commands().Length; var eventCount = Events().Length;
            var originalCommands = MatchStateHasher.HashValue(Commands()); var originalEvents = MatchStateHasher.HashValue(Events());
            if (route.StartsWith("decline", StringComparison.Ordinal))
            {
                var wire = Bound(actor, Refuse(selected));
                core = await engine.ResolveAsync(State(), new("selected-refusal-core", actor, Refusal), GameCommandJsonMapper.Map(wire), default);
                var probe = new MatchSession(State(), engine, NoopMatchJournal.Instance);
                native = await probe.SubmitAsync(actor, "selected-refusal-native", GameCommandJsonMapper.Map(wire), wire, default);
                // Retain both real results before the first positive assertion.
                Assert.True(core.Accepted, core.ErrorMessage); Assert.True(native.Accepted, native.ErrorMessage);
                Assert.Contains(session.PromptFor(actor).Candidates!, c => c.Action == Refusal && c.Enabled && c.CommandTemplate!.CmdType == Refusal);
                Assert.DoesNotContain(session.PromptFor(actor == "alice" ? "bob" : "alice").Candidates!, c => c.Action == Refusal);
                Assert.Equal(before, MatchStateHasher.Hash(State()));
                await Send(actor, Refuse(selected));
                Assert.Contains(unit, State().PlayerZones[actor].Graveyard);
                Assert.DoesNotContain(State().CardPlays.Finalized, p => p.Source.ObjectId == unit);
                Assert.Equal(1, State().RunePools[actor].Mana);
                Assert.Equal(funded ? 1 : 0, State().RunePools[actor].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
                var frame = Clone(Frame(State()));
                var at = Array.FindLastIndex(frame.Commands.ToArray(), c => c.CommandType == Refusal && c.Accepted);
                Assert.True(at >= 0);
                var altered = frame.Commands.ToArray(); var changed = JsonNode.Parse(altered[at].RawCommand!.Value.GetRawText())!.AsObject();
                changed["optionalCosts"] = new JsonArray();
                altered[at] = altered[at] with { RawCommand = JsonSerializer.SerializeToElement(changed, Json) };
                frame = frame with { Commands = altered }; Assert.Empty(frame.ValidationErrors);
                var alteredShape = MatchRecoveryValidator.Validate(room, frame.LastEventSequence, frame.Commands, frame.Events,
                    frame.PlayerViews, frame.AuthoritativeState!, frame.CurrentTick, frame.SpectatorReplayFrame);
                Assert.Empty(alteredShape);
                var writes = new Journal();
                var registry = new InMemoryMatchSessionRegistry(engine, writes, new Store(frame), NoopMatchPlayerStore.Instance, options);
                var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(room, default));
                Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code); Assert.Empty(writes.Entries);
                forgery = new { frame.ValidationErrors, error.Code, error.Message, removedSelectedFeeFromAcceptedOriginalHistory = true };
            }
            else
            {
                if (route == "zero")
                {
                    var falseFee = Refuse(play); falseFee["manaCost"] = 99; falseFee["powerCost"] = 99;
                    await Send(actor, falseFee, accepted: false);
                }
                await Act(actor, route == "pay" ? selected : play); await Drain();
                Assert.Contains(unit, State().PlayerZones[actor].Base);
                Assert.Equal(route != "pay", State().CardObjects[unit].IsExhausted);
                Assert.Equal(route == "pay" ? 0 : 1, State().RunePools[actor].Mana);
                Assert.Equal(route == "pay" ? 0 : 1, State().RunePools[actor].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
                Assert.Contains(State().CardPlays.Finalized, p => p.Source.ObjectId == unit);
            }
            Assert.Null(State().PendingEffectPlay); Assert.Contains(parent, State().PlayerZones[actor].Graveyard);
            Assert.Contains(gift, State().PlayerZones[actor].Graveyard);
            Assert.Equal(originalCommands, MatchStateHasher.HashValue(Commands().Take(commandCount).ToArray()));
            Assert.Equal(originalEvents, MatchStateHasher.HashValue(Events().Take(eventCount).ToArray()));
            await Drain(); await Cold();
            Assert.Equal(MatchStatuses.InProgress, State().Status); Assert.Null(State().WinnerPlayerId);
            Assert.All(State().PlayerScores.Values, score => Assert.Equal(0, score));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State(), engine, default, Events());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            Assert.Equal(MatchSession.RoomSeatCommandType, Commands()[0].CommandType);
            Assert.Equal(MatchSession.RoomSeatCommandType, Commands()[1].CommandType);
            Assert.DoesNotContain(Commands(), c => c.CommandType.StartsWith("DEV_SEED_SCENARIO", StringComparison.Ordinal));
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_SELECTED_RECAST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, actor + "-" + route + ".json"), JsonSerializer.Serialize(new {
                    scope = "Empty room, real seats, submitted red/purple 40/12/3 deck validated against explicit ChinaStandard20260724 snapshot, fixed seed, native Ready/Mulligan/natural draws and twelve physical runes. Actual Gift discard/draw2 and paid Harrowing derive default zero versus chosen Haste 1mana+1red; funded/unfunded explicit refusal, actual payment/free play and false-client-price zero rejection. Per-command privacy, dedup and production-registry JSON cold recovery preserve original input; altered accepted refusal history must reject. No claim of latest format/source completeness, SQL/SignalR/process restart, full L2/L3 or whole-card.",
                    initial, deck, actor, route, defaultQuote, selectedQuote, core, native, forgery,
                    final = journal.Entries.LastOrDefault()?.AuthoritativeState, commands = Commands(), events = Events() }, Json));
            }
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        string? Hand(string no) => State().PlayerZones[actor].Hand.FirstOrDefault(id => State().CardObjects[id].CardNo == no);
        string[] Runes() => State().PlayerZones[actor].Base.Where(id => State().CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
        PlayCostPreviewRequestDto Request(PlayCardCommand play) => new("selected-fee-quote", session.PromptFor(actor).PromptId!, State().Tick, play);
        JsonObject Refuse(PlayCardCommand play) { var raw = JsonSerializer.SerializeToNode(play, Json)!.AsObject(); raw["cmdType"] = Refusal; return raw; }
        JsonElement Bound(string player, JsonObject raw) { raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State().Tick; return JsonSerializer.SerializeToElement(raw, Json); }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) {
                TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        MatchRecoveryFrame Clone(MatchRecoveryFrame frame) => JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        MatchRecoveryFrame Frame(MatchState state)
        {
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(room, state.Tick, sequence, [], state);
            var errors = MatchRecoveryValidator.Validate(room, sequence, Commands(), Events(), views, state, state.Tick, spectator);
            return new(room, state.Tick, sequence, Commands(), Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }
        string WireHash(object? value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, Json));
        async Task Cold()
        {
            var frame = Clone(Frame(State())); Assert.Empty(frame.ValidationErrors);
            session = await new InMemoryMatchSessionRegistry(engine, journal, new Store(frame), NoopMatchPlayerStore.Instance, options).GetOrCreateAsync(room, default);
            foreach (var player in frame.PlayerViews.Keys)
            {
                Assert.Equal(WireHash(frame.PlayerViews[player].Snapshot), WireHash(session.SnapshotFor(player)));
                Assert.Equal(WireHash(frame.PlayerViews[player].Prompt), WireHash(session.PromptFor(player)));
            }
        }
        async Task Act(string player, GameCommand command) => await Send(player, JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject());
        async Task Send(string player, JsonObject raw, bool accepted = true)
        {
            var before = MatchStateHasher.Hash(State()); var wire = Bound(player, raw); var command = GameCommandJsonMapper.Map(wire);
            var intent = "selected-fee-" + journal.Entries.Count;
            async ValueTask<ResolutionResult> Submit() => command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(player, intent, submitted, wire, default),
                ReadyCommand => await session.ReadyAsync(player, intent, wire, default),
                _ => await session.SubmitAsync(player, intent, command, wire, default) };
            var result = await Submit(); Assert.True(result.Accepted == accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            if (!accepted) { Assert.Equal(before, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events); }
            var count = journal.Entries.Count; var duplicate = await Submit();
            Assert.Equal(accepted, duplicate.Accepted); Assert.Equal(count, journal.Entries.Count);
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
            if (verifyColdRecoveryAfterEveryCommand) await Cold();
        }
        async Task Drain()
        {
            for (var i = 0; i < 30; i++)
            {
                if (State().PendingEffectPlay is not null) return;
                Assert.Null(State().PendingCardChoice);
                if (State().StackItems.Count > 0) await Act(State().PriorityPlayerId!, new PassPriorityCommand());
                else if (State().FocusPlayerId is { } focus) await Act(focus, new PassFocusCommand());
                else return;
            }
            throw new InvalidOperationException("The selected-fee recast window did not close.");
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(roomId == frame.RoomId ? frame : null);
    }
}
