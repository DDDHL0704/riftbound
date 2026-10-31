using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MandatoryRecastHeldTaxOpeningTests
{
    private const string Parent = "OGN·198/298", Gift = "UNL-125/219", Pirate = "OGN·002/298";
    private const string Host = "OGN·013/298", Field = "UNL-219/219";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("alice", "decline")] [InlineData("bob", "decline")]
    [InlineData("alice", "pay")] [InlineData("bob", "pay")]
    [InlineData("alice", "discard")] [InlineData("bob", "discard")]
    public async Task ActualConquestAndHoldKeepDefaultTaxPaymentAndOptionalDiscardVoluntary(string actor, string choice)
    {
        var room = "CANONICAL-HELD-TAX-RECAST-" + actor + "-" + choice;
        var initial = MatchState.Create(room) with { Seed = 424242 };
        var journal = new Journal(); var engine = new CoreRuleEngine();
        var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false);
        IMatchSession session = new MatchSession(initial, engine, journal, NoopMatchPlayerStore.Instance, options);
        var verifyColdRecoveryAfterEveryCommand = true;
        OfficialDecklist? deck = null; PlayCostQuoteDto? defaultQuote = null, discountQuote = null; object? forgery = null;
        try
        {
            var catalog = await OfficialCardCatalog.LoadDefaultAsync();
            deck = new("UNL-185/219", "UNL-028/219", MainCards().Append("UNL-028/219").ToArray(),
                Enumerable.Repeat("UNL-R01", 4).Concat(Enumerable.Repeat("UNL-R05", 8)).ToArray(),
                [Field, "OGN·294/298", "OGN·297/298"]);
            var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
            Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
            Assert.Empty(initial.Seats); Assert.Empty(initial.CardObjects); Assert.Equal(0, initial.Tick);
            using (var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "opening-randomization-v1.json"))))
                foreach (var player in new[] { "alice", "bob" })
                {
                    var reference = vectors.RootElement.GetProperty("cases").EnumerateArray().Single(r =>
                        r.GetProperty("seed").GetInt64() == 424242 && r.GetProperty("scope").GetString() == player + ":BATTLEFIELDS");
                    Assert.Equal(player == "alice" ? 3 : 6, reference.GetProperty("cursor").GetInt64());
                    Assert.Equal(0, reference.GetProperty("permutation")[0].GetInt32());
                }
            await session.EnsurePlayerAsync("alice", default); await Cold();
            await session.EnsurePlayerAsync("bob", default); await Cold();
            foreach (var player in new[] { "alice", "bob" })
                await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await Act("alice", new ReadyCommand()); await Act("bob", new ReadyCommand());
            Assert.Equal(actor == "alice" ? "P1" : "P2", State().Seats[actor]);
            foreach (var no in new[] { Parent, Gift, Pirate, Host }) Assert.NotNull(Hand(no));
            var field = State().BattlefieldStates.Keys.Single(id => State().CardObjects[id].OwnerId == actor);
            Assert.Equal(Field, State().CardObjects[field].CardNo);
            await Act(State().ActivePlayerId, new MulliganCommand([]));
            await Act(State().OpeningSecondActionPlayerId!, new MulliganCommand([]));
            // Natural channeling is setup. Recover its complete original history once before
            // the first paid play, then retain cold recovery at every conquest/tax/payment step.
            verifyColdRecoveryAfterEveryCommand = false;
            for (var turns = 0; State().TurnPlayerId != actor || Runes().Length < 12; turns++)
            {
                Assert.True(turns < 30 && State().Status == MatchStatuses.InProgress);
                await Act(State().TurnPlayerId, new EndTurnCommand());
            }
            verifyColdRecoveryAfterEveryCommand = true;
            await Cold();
            var parent = Hand(Parent)!; var gift = Hand(Gift)!; var pirate = Hand(Pirate)!; var host = Hand(Host)!;
            Assert.All(State().RunePools.Values, p => Assert.Equal(RunePool.Empty, p));
            var hostRunes = Runes().Take(2).ToArray();
            await Act(actor, new TapRuneCommand(hostRunes[0], hostRunes));
            await Act(actor, new PlayCardCommand(host, Host, [])); await Drain();
            Assert.Contains(host, State().PlayerZones[actor].Base); Assert.True(State().CardObjects[host].IsExhausted);
            Assert.Equal(0, State().RunePools[actor].Mana);
            await NextOwnTurn(); Assert.False(State().CardObjects[host].IsExhausted);
            var generation = State().CardObjects[host].ObjectGeneration;
            await Act(actor, new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field, [])); await Drain();
            Assert.Equal(field, State().ObjectLocations[host].BattlefieldObjectId);
            Assert.Equal(generation, State().CardObjects[host].ObjectGeneration);
            Assert.Equal(actor, State().BattlefieldStates[field].ControllerId);
            Assert.Equal(actor, State().CardObjects[field].ControllerId);
            Assert.Equal(1, State().PlayerScores[actor]);
            await NextOwnTurn();
            Assert.Equal(2, State().PlayerScores[actor]);
            Assert.Contains(State().UntilEndOfTurnEffects, e => e.StartsWith(TaxPrefix(), StringComparison.Ordinal));
            Assert.All(State().RunePools.Values, p => Assert.Equal(RunePool.Empty, p));
            var scores = MatchStateHasher.HashValue(State().PlayerScores);
            var purple = Runes().Where(id => State().CardObjects[id].CardNo == "UNL-R05").Take(2).ToArray();
            Assert.Equal(12, Runes().Length); Assert.Equal(2, purple.Length);
            var manaRunes = Runes().Except(purple).ToArray();
            await Act(actor, new TapRuneCommand(manaRunes[0], manaRunes));
            await Act(actor, new RecycleRuneCommand(purple[0], purple));
            Assert.Equal(10, State().RunePools[actor].Mana);
            var remainingDeck = State().PlayerZones[actor].MainDeck.Count;
            await Act(actor, new PlayCardCommand(gift, Gift, [pirate])); await Drain();
            Assert.Equal(remainingDeck - 2, State().PlayerZones[actor].MainDeck.Count);
            Assert.Contains(pirate, State().PlayerZones[actor].Graveyard);
            Assert.Equal(7, State().RunePools[actor].Mana);
            var fodder = State().PlayerZones[actor].Hand.First(id => id != parent);
            await Act(actor, new PlayCardCommand(parent, Parent, [pirate])); await Drain();
            var pending = Assert.IsType<PendingEffectPlayState>(State().PendingEffectPlay);
            Assert.False(pending.Optional); Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreAllCosts);
            Assert.Equal(1, State().RunePools[actor].Mana); Assert.Equal(0, State().RunePools[actor].TotalPower);
            var play = new PlayCardCommand(pirate, Pirate, []);
            var discounted = play with { OptionalCosts = ["DISCARD_HAND_CARD:" + fodder] };
            defaultQuote = engine.PreviewPlayCard(State(), actor, new("held-default", session.PromptFor(actor).PromptId!, State().Tick, play));
            discountQuote = engine.PreviewPlayCard(State(), actor, new("held-discard", session.PromptFor(actor).PromptId!, State().Tick, discounted));
            Assert.True(defaultQuote.CanPay, defaultQuote.Message); Assert.True(discountQuote.CanPay, discountQuote.Message);
            Assert.Equal(1, defaultQuote.Cost!.Mana); Assert.Equal(0, discountQuote.Cost!.Mana);
            Assert.Equal(0, defaultQuote.Cost.GenericPower + defaultQuote.Cost.PowerByTrait.Values.Sum());
            Assert.Contains(session.PromptFor(actor).Candidates!, c => c.Action == CommandTypes.ChooseCards && c.Enabled);
            var hand = State().PlayerZones[actor].Hand.ToArray();
            var commandCount = Commands().Length; var eventCount = Events().Length;
            var prefixCommands = MatchStateHasher.HashValue(Commands()); var prefixEvents = MatchStateHasher.HashValue(Events());
            if (actor == "alice" && choice == "decline")
            {
                var before = MatchStateHasher.Hash(State());
                var minted = State() with { RunePools = new Dictionary<string, RunePool>(State().RunePools) {
                    [actor] = State().RunePools[actor] with { Mana = 2 } } };
                var frame = Clone(Frame(minted)); Assert.Empty(frame.ValidationErrors);
                var writes = new Journal();
                var registry = new InMemoryMatchSessionRegistry(engine, writes, new Store(frame), NoopMatchPlayerStore.Instance, options);
                var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(room, default));
                Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code); Assert.Empty(writes.Entries);
                Assert.Equal(before, MatchStateHasher.Hash(State()));
                forgery = new { frame.ValidationErrors, error.Code, error.Message, originalCommandsHash = prefixCommands, originalEventsHash = prefixEvents };
            }
            if (choice == "decline")
            {
                await Act(actor, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
                Assert.Equal(hand, State().PlayerZones[actor].Hand);
                Assert.Contains(pirate, State().PlayerZones[actor].Graveyard);
                Assert.DoesNotContain(State().CardPlays.Finalized, p => p.Source.ObjectId == pirate);
                Assert.Equal(1, State().RunePools[actor].Mana);
            }
            else
            {
                await Act(actor, choice == "discard" ? discounted : play); await Drain();
                Assert.Contains(pirate, State().PlayerZones[actor].Base);
                Assert.Contains(State().CardPlays.Finalized, p => p.Source.ObjectId == pirate);
                Assert.Equal(choice == "discard" ? 1 : 0, State().RunePools[actor].Mana);
                if (choice == "discard")
                {
                    Assert.DoesNotContain(fodder, State().PlayerZones[actor].Hand);
                    Assert.Contains(fodder, State().PlayerZones[actor].Graveyard);
                }
                else Assert.Equal(hand, State().PlayerZones[actor].Hand);
            }
            Assert.Equal(prefixCommands, MatchStateHasher.HashValue(Commands().Take(commandCount).ToArray()));
            Assert.Equal(prefixEvents, MatchStateHasher.HashValue(Events().Take(eventCount).ToArray()));
            Assert.Null(State().PendingEffectPlay); Assert.Contains(parent, State().PlayerZones[actor].Graveyard);
            Assert.Contains(gift, State().PlayerZones[actor].Graveyard);
            Assert.Equal(0, State().RunePools[actor].TotalPower);
            Assert.Equal(scores, MatchStateHasher.HashValue(State().PlayerScores));
            Assert.Equal(MatchStatuses.InProgress, State().Status); Assert.Null(State().WinnerPlayerId);
            await Cold();
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State(), engine, default, Events());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            Assert.Equal(MatchSession.RoomSeatCommandType, Commands()[0].CommandType);
            Assert.Equal(MatchSession.RoomSeatCommandType, Commands()[1].CommandType);
            Assert.DoesNotContain(Commands(), c => c.CommandType.StartsWith("DEV_SEED_SCENARIO", StringComparison.Ordinal));
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_HELD_RECAST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, actor + "-" + choice + ".json"), JsonSerializer.Serialize(new {
                    scope = "Real seats, validated red/purple Pyke40/12/3 decks, seed424242 and independent pre-submission blueprint; no future runtime deck order read or changed. Natural runes, real two-mana Poro play/readiness/movement/conquest, next real held step and tax, then actual ten mana/two purple payment of Gift/Harrowing. Default one-mana versus voluntary discard-to-zero, both seats and all three choices. Every native command checks privacy/dedup. JSON production-registry cold recovery checks the complete natural-channeling prefix and every later conquest/tax/payment command, followed by full original-history replay. No SQL/SignalR/real process restart/full L2/L3/whole-card claim.",
                    initial, deck, actor, choice, final = journal.Entries.LastOrDefault()?.AuthoritativeState, defaultQuote, discountQuote,
                    commands = Commands(), events = Events(), forgery }, Json));
            }
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        string TaxPrefix() => "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:" + actor;
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
        async Task<ResolutionResult> Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State().Tick;
            var wire = JsonSerializer.SerializeToElement(raw, Json); command = GameCommandJsonMapper.Map(wire);
            var intent = "held-tax-recast-" + journal.Entries.Count;
            async ValueTask<ResolutionResult> Send() => command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(player, intent, submitted, wire, default),
                ReadyCommand => await session.ReadyAsync(player, intent, wire, default),
                _ => await session.SubmitAsync(player, intent, command, wire, default) };
            var result = await Send(); Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var count = journal.Entries.Count; var duplicate = await Send();
            Assert.True(duplicate.Accepted); Assert.Equal(count, journal.Entries.Count);
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
            if (verifyColdRecoveryAfterEveryCommand) await Cold();
            return result;
        }
        async Task NextOwnTurn()
        {
            Assert.Equal(actor, State().TurnPlayerId);
            var otherStart = await Act(actor, new EndTurnCommand());
            await TurnSequenceTestDriver.Complete(otherStart,
                (player, command) => new ValueTask<ResolutionResult>(Act(player, command)));
            var ownStart = await Act(State().TurnPlayerId, new EndTurnCommand());
            await TurnSequenceTestDriver.Complete(ownStart,
                (player, command) => new ValueTask<ResolutionResult>(Act(player, command)));
            Assert.Equal(actor, State().TurnPlayerId);
            Assert.Equal(MatchPhases.Main, State().Phase);
        }
        async Task Drain()
        {
            // Each pass remains covered by accepted, duplicate and hidden-zone
            // assertions. A single restore after the window avoids replaying the
            // same action log for every priority/focus pass.
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
            Assert.True(closed, "The finite held-tax replay window did not close.");
            if (recover) await Cold();
        }
    }

    // Fixed submitted blueprint from the existing independent seed424242
    // alice/bob vectors, prepared before room creation. Runtime still shuffles.
    private static string[] MainCards() => [
        Parent, Parent, Gift, Pirate, Host, "OGN·012/298", "OGN·012/298", "OGN·012/298", Pirate, Gift, Host, Gift,
        "OGN·015/298", "OGN·015/298", "OGN·015/298", "OGN·018/298", "OGN·018/298", "OGN·018/298",
        "OGN·167/298", "OGN·167/298", Host, "OGN·167/298", "OGN·175/298", "OGN·175/298", "OGN·175/298", Parent,
        "OGN·176/298", "OGN·176/298", "OGN·176/298", "UNL-126/219", "UNL-126/219", "UNL-126/219",
        "UNL-129/219", "UNL-129/219", "UNL-129/219", "SFD·014/221", "SFD·014/221", Pirate, "SFD·014/221"
    ];
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
