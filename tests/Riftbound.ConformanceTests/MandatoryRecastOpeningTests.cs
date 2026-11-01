using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MandatoryRecastOpeningTests
{
    private const string Parent = "OGN·198/298", Gift = "UNL-125/219";
    private const string PaidUnit = "OGN·118/298", FreeUnit = "OGN·096/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("P1", "decline-unfunded")] [InlineData("P2", "decline-unfunded")]
    [InlineData("P1", "decline-funded")] [InlineData("P2", "decline-funded")]
    [InlineData("P1", "pay")] [InlineData("P2", "pay")]
    [InlineData("P1", "zero")] [InlineData("P2", "zero")]
    public async Task RealRoomAndDeckHistoryPreserveRefusalPaymentAndZeroCostAcrossColdReload(string actor, string mode)
    {
        var room = "CANONICAL-MANDATORY-RECAST-" + actor + "-" + mode;
        var initial = MatchState.Create(room) with { Seed = 424242 };
        var journal = new Journal(); var engine = new CoreRuleEngine();
        var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false);
        IMatchSession session = new MatchSession(initial, engine, journal, NoopMatchPlayerStore.Instance, options);
        var verifyColdRecoveryAfterEveryCommand = true;
        OfficialDecklist? deck = null; object? forgery = null;
        try
        {
            var catalog = await OfficialCardCatalog.LoadDefaultAsync();
            var fillers = new[] { "OGN·084/298", "OGN·086/298", "OGN·087/298", "OGN·088/298", "OGN·091/298",
                "OGN·092/298", "OGN·097/298", "OGN·100/298", "OGN·103/298" };
            deck = new("UNL-197/219", "UNL-149/219", new[] { Parent, Gift, PaidUnit, FreeUnit }
                .Concat(fillers).SelectMany(no => Enumerable.Repeat(no, 3)).Append("UNL-149/219").ToArray(),
                Enumerable.Repeat("UNL-R03", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(),
                ["OGN·278/298", "OGN·294/298", "OGN·297/298"]);
            var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
            Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
            Assert.Empty(initial.Seats); Assert.Empty(initial.CardObjects); Assert.Equal(0, initial.Tick);
            await session.EnsurePlayerAsync("P1", default); await Cold();
            await session.EnsurePlayerAsync("P2", default); await Cold();
            foreach (var player in new[] { "P1", "P2" })
                await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
            await Act(State().ActivePlayerId, new MulliganCommand([]));
            await Act(State().OpeningSecondActionPlayerId!, new MulliganCommand([]));
            var unitNo = mode == "zero" ? FreeUnit : PaidUnit;
            // Natural draws and rune channeling are setup for the payment assertion. Replaying the
            // entire accepted action log after every EndTurn makes this fixture quadratic in the
            // number of setup turns, while the critical payment/refusal stages below still perform
            // full cold recovery checks.
            verifyColdRecoveryAfterEveryCommand = false;
            for (var turns = 0; State().TurnPlayerId != actor || Runes().Length < 12
                || Hand(Parent) is null || Hand(Gift) is null || Hand(unitNo) is null; turns++)
            {
                Assert.True(turns < 70 && State().Status == MatchStatuses.InProgress, "Wait only for natural draws and physical rune channeling.");
                await Act(State().TurnPlayerId, new EndTurnCommand());
            }
            verifyColdRecoveryAfterEveryCommand = true;
            await Cold();
            Assert.All(State().RunePools.Values, p => Assert.Equal(RunePool.Empty, p));
            var parent = Hand(Parent)!; var gift = Hand(Gift)!; var unit = Hand(unitNo)!;
            var purple = Runes().Where(id => State().CardObjects[id].CardNo == "UNL-R05").Take(2).ToArray();
            var blue = Runes().First(id => State().CardObjects[id].CardNo == "UNL-R03");
            Assert.Equal(2, purple.Length);
            verifyColdRecoveryAfterEveryCommand = false;
            var manaRunes = Runes().Except(purple.Append(blue)).ToArray();
            await Act(actor, new TapRuneCommand(manaRunes[0], manaRunes));
            await Act(actor, new RecycleRuneCommand(purple[0], purple));
            verifyColdRecoveryAfterEveryCommand = true;
            await Cold();
            Assert.Equal(9, State().RunePools[actor].Mana);
            var deckCount = State().PlayerZones[actor].MainDeck.Count;
            await Act(actor, new PlayCardCommand(gift, Gift, [unit])); await Drain();
            Assert.Equal(deckCount - 2, State().PlayerZones[actor].MainDeck.Count);
            Assert.Contains(unit, State().PlayerZones[actor].Graveyard);
            Assert.Contains(gift, State().PlayerZones[actor].Graveyard);
            Assert.Equal(6, State().RunePools[actor].Mana);
            if (mode == "decline-funded") await Act(actor, new RecycleRuneCommand(blue));
            await Act(actor, new PlayCardCommand(parent, Parent, [unit])); await Drain();
            var pending = Assert.IsType<PendingEffectPlayState>(State().PendingEffectPlay);
            Assert.False(pending.Optional); Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower);
            Assert.Equal(0, State().RunePools[actor].Mana);
            Assert.Equal(mode == "decline-funded" ? 1 : 0, State().RunePools[actor].TotalPower);
            var priorCommands = MatchStateHasher.HashValue(Commands()); var priorEvents = MatchStateHasher.HashValue(Events());
            var commandCount = Commands().Length; var eventCount = Events().Length;
            if (actor == "P1" && mode == "decline-unfunded")
            {
                var before = MatchStateHasher.Hash(State());
                var minted = State() with { RunePools = new Dictionary<string, RunePool>(State().RunePools) {
                    [actor] = State().RunePools[actor] with { Mana = 1 } } };
                var frame = Clone(Frame(minted)); Assert.Empty(frame.ValidationErrors);
                var writes = new Journal();
                var registry = new InMemoryMatchSessionRegistry(engine, writes, new Store(frame), NoopMatchPlayerStore.Instance, options);
                var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(room, default));
                Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code); Assert.Empty(writes.Entries);
                Assert.Equal(before, MatchStateHasher.Hash(State()));
                forgery = new { frame.ValidationErrors, error.Code, error.Message, originalCommandsHash = priorCommands, originalEventsHash = priorEvents };
            }
            var refusal = new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []);
            if (mode.StartsWith("decline", StringComparison.Ordinal))
            {
                Assert.Contains(session.PromptFor(actor).Candidates!, c => c.Action == CommandTypes.ChooseCards && c.Enabled);
                await Act(actor, refusal);
                Assert.Contains(unit, State().PlayerZones[actor].Graveyard);
                Assert.DoesNotContain(State().CardPlays.Finalized, p => p.Source.ObjectId == unit);
                Assert.Equal(mode == "decline-funded" ? 1 : 0, State().RunePools[actor].TotalPower);
            }
            else
            {
                if (mode == "zero")
                {
                    Assert.DoesNotContain(session.PromptFor(actor).Candidates!, c => c.Action == CommandTypes.ChooseCards && c.Enabled);
                    await Act(actor, refusal, accepted: false, expectedError: ErrorCodes.PromptExpired);
                }
                else await Act(actor, new RecycleRuneCommand(blue));
                var play = new PlayCardCommand(unit, unitNo, []);
                var quote = engine.PreviewPlayCard(State(), actor, new("canonical-recast-quote", session.PromptFor(actor).PromptId!, State().Tick, play));
                Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
                Assert.Equal(mode == "zero" ? 0 : 1, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Blue));
                await Act(actor, play); await Drain();
                Assert.Contains(unit, State().PlayerZones[actor].Base);
                Assert.Equal(0, State().RunePools[actor].TotalPower);
                Assert.Contains(State().CardPlays.Finalized, p => p.Source.ObjectId == unit);
            }
            Assert.Equal(priorCommands, MatchStateHasher.HashValue(Commands().Take(commandCount).ToArray()));
            Assert.Equal(priorEvents, MatchStateHasher.HashValue(Events().Take(eventCount).ToArray()));
            Assert.Null(State().PendingEffectPlay); Assert.Contains(parent, State().PlayerZones[actor].Graveyard);
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
            if (Environment.GetEnvironmentVariable("RIFTBOUND_CANONICAL_RECAST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, actor + "-" + mode + ".json"), JsonSerializer.Serialize(new {
                    scope = "Empty room, real seats, validated blue/purple 40/12/3 decks, seed424242, Ready/Mulligan and natural draws/channeling. Actual Moonsilver Gift discards the unit and draws two, then actual nine-mana/two-purple setup pays Gift and Harrowing. Both seats cover funded/unfunded refusal, actual blue payment and zero refusal rejection. Every native command is deduplicated and JSON-cold-loaded through the production registry with unchanged original history. No SQL/SignalR/process restart/full L2/L3/whole-card claim.",
                    initial, deck, actor, mode, final = journal.Entries.LastOrDefault()?.AuthoritativeState, commands = Commands(), events = Events(), forgery }, Json));
            }
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        string? Hand(string no) => State().PlayerZones[actor].Hand.FirstOrDefault(id => State().CardObjects[id].CardNo == no);
        string[] Runes() => State().PlayerZones[actor].Base.Where(id => State().CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
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
        async Task Cold()
        {
            var frame = Clone(Frame(State())); Assert.Empty(frame.ValidationErrors);
            session = await new InMemoryMatchSessionRegistry(engine, journal, new Store(frame), NoopMatchPlayerStore.Instance, options).GetOrCreateAsync(room, default);
            foreach (var player in frame.PlayerViews.Keys)
            {
                // Persisted untyped metadata retains camel-case wire keys;
                // live anonymous CLR metadata uses its declared property names.
                // Compare the entire actual wire shape on both sides.
                Assert.Equal(WireHash(frame.PlayerViews[player].Snapshot), WireHash(session.SnapshotFor(player)));
                Assert.Equal(WireHash(frame.PlayerViews[player].Prompt), WireHash(session.PromptFor(player)));
            }
        }
        string WireHash(object? value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, Json));
        async Task Act(string player, GameCommand command, bool accepted = true, string? expectedError = null)
        {
            var before = MatchStateHasher.Hash(State());
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State().Tick;
            var wire = JsonSerializer.SerializeToElement(raw, Json); command = GameCommandJsonMapper.Map(wire);
            var intent = "canonical-recast-" + journal.Entries.Count;
            async ValueTask<ResolutionResult> Send() => command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(player, intent, submitted, wire, default),
                ReadyCommand => await session.ReadyAsync(player, intent, wire, default),
                _ => await session.SubmitAsync(player, intent, command, wire, default) };
            var result = await Send(); Assert.True(result.Accepted == accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            if (!accepted) { Assert.Equal(expectedError ?? ErrorCodes.InvalidTarget, result.ErrorCode); Assert.Equal(before, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events); }
            var count = journal.Entries.Count; var duplicate = await Send();
            Assert.Equal(accepted, duplicate.Accepted); Assert.Equal(count, journal.Entries.Count);
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
            if (verifyColdRecoveryAfterEveryCommand) await Cold();
        }
        async Task Drain()
        {
            // A response window is one recovery boundary: every command still
            // keeps its duplicate and rule assertions, while restoring the same
            // growing journal after each pass would replay identical history.
            var recover = verifyColdRecoveryAfterEveryCommand;
            verifyColdRecoveryAfterEveryCommand = false;
            var closed = false;
            for (var i = 0; i < 30; i++)
            {
                if (State().PendingEffectPlay is not null) { closed = true; break; }
                Assert.Null(State().PendingCardChoice);
                if (State().StackItems.Count > 0) await Act(State().PriorityPlayerId!, new PassPriorityCommand());
                else if (State().FocusPlayerId is { } focus) await Act(focus, new PassFocusCommand());
                else { closed = true; break; }
            }
            verifyColdRecoveryAfterEveryCommand = recover;
            Assert.True(closed, "The finite canonical recast window did not close.");
            if (recover) await Cold();
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
