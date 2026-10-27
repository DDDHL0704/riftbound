using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DianaDuelManaOpeningTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("UNL-197/219")] [InlineData("UNL-234/219")] [InlineData("UNL-234*/219")]
    public async Task RealRoomDeckOpeningAndResourceHistoryRejectCoherentManaRelabelMintAndLoss(string cardNo)
    {
        var room = "DIANA-DUEL-MANA-BIRTH-" + cardNo;
        var initial = MatchState.Create(room) with { Seed = 424242 };
        var journal = new Journal(); var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false);
        IMatchSession session = new MatchSession(initial, new CoreRuleEngine(), journal, NoopMatchPlayerStore.Instance, options);
        var forgeries = new List<object>(); OfficialDecklist? deck = null;
        try
        {
            var catalog = await OfficialCardCatalog.LoadDefaultAsync();
            var fillers = new[] { "OGN·084/298", "OGN·086/298", "OGN·087/298", "OGN·088/298", "OGN·091/298",
                "OGN·092/298", "OGN·096/298", "OGN·097/298", "OGN·100/298", "OGN·103/298", "OGN·106/298", "OGN·107/298", "OGN·116/298" };
            deck = new(cardNo, "UNL-149/219", fillers.SelectMany(no => Enumerable.Repeat(no, 3)).Append("UNL-149/219").ToArray(),
                Enumerable.Repeat("UNL-R04", 12).ToArray(), ["OGN·278/298", "OGN·294/298", "OGN·297/298"]);
            var legality = OfficialDeckValidator.Validate(deck, catalog);
            Assert.True(legality.IsValid, string.Join("; ", legality.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
            await session.EnsurePlayerAsync("P1", default); await Cold();
            await session.EnsurePlayerAsync("P2", default); await Cold();
            foreach (var player in new[] { "P1", "P2" })
                await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
            await Act(State().ActivePlayerId, new MulliganCommand([]));
            await Act(State().OpeningSecondActionPlayerId!, new MulliganCommand([]));
            if (State().ActivePlayerId == "P2") await Act("P2", new EndTurnCommand());
            Assert.Equal(MatchPhases.Main, State().Phase); Assert.Equal(TimingStates.NeutralOpen, State().TimingState);
            Assert.Equal("P1", State().ActivePlayerId); Assert.Equal(RunePool.Empty, State().RunePools["P1"]);
            var legend = Assert.Single(State().PlayerZones["P1"].LegendZone);
            Assert.Equal(cardNo, State().CardObjects[legend].CardNo);
            await Act("P1", new LegendActCommand(legend, LegendActionAbilityCatalog.DianaLegendAbilityId, [], []));
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, State().RunePools["P1"]);
            var prefixCommands = MatchStateHasher.HashValue(Commands()); var prefixEvents = MatchStateHasher.HashValue(Events());
            foreach (var shape in new[] { "relabel-ordinary", "mint", "lose" })
            {
                var pool = shape switch {
                    "relabel-ordinary" => new RunePool(1, 0),
                    "mint" => RunePool.Empty with { DuelOnlyMana = 2 },
                    _ => RunePool.Empty };
                var forged = State() with { RunePools = new Dictionary<string, RunePool>(State().RunePools) { ["P1"] = pool } };
                var frame = Frame(forged); Assert.Empty(frame.ValidationErrors);
                var writes = new Journal();
                var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, new Store(frame), NoopMatchPlayerStore.Instance, options);
                var failure = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(room, default));
                Assert.Equal(ErrorCodes.RecoveryInconsistent, failure.Code); Assert.Empty(writes.Entries);
                Assert.Equal(prefixCommands, MatchStateHasher.HashValue(Commands())); Assert.Equal(prefixEvents, MatchStateHasher.HashValue(Events()));
                Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, State().RunePools["P1"]);
                forgeries.Add(new { shape, structuralErrors = frame.ValidationErrors, rejectionCode = failure.Code,
                    rejection = failure.Message, originalCommandsHash = prefixCommands, originalEventsHash = prefixEvents });
            }
            await Cold();
            await Act("P1", new EndTurnCommand());
            Assert.All(State().RunePools.Values, pool => Assert.Equal(RunePool.Empty, pool));
            Assert.Equal("P2", State().ActivePlayerId); await Cold();
            Assert.Equal(MatchSession.RoomSeatCommandType, Commands()[0].CommandType);
            Assert.Equal(MatchSession.RoomSeatCommandType, Commands()[1].CommandType);
            Assert.DoesNotContain(Commands(), command => command.CommandType.StartsWith("DEV_SEED_SCENARIO", StringComparison.Ordinal));
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_DIANA_DUEL_MANA_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "canonical-opening-" + cardNo.Replace('/', '_').Replace('*', 'a') + ".json"),
                    JsonSerializer.Serialize(new { scope = "Real empty-room seats, independently validated 40/12/3 decks, Ready/Mulligan, native Diana production and end-turn clearing. Every command cold-loads via the production registry, deduplicates and audits the complete original history. Coherent forgery views agree structurally but must fail original-history audit. No DEV seed, SQL/network/process restart/full L2/L3 or whole-card claim.",
                        initial, deck, final = journal.Entries.LastOrDefault()?.AuthoritativeState, commands = Commands(), events = Events(), forgeries }, Json));
            }
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) {
                TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        MatchRecoveryFrame Frame(MatchState state)
        {
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence,
                snapshots[id], state.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(room, state.Tick, sequence, [], state);
            var errors = MatchRecoveryValidator.Validate(room, sequence, Commands(), Events(), views, state, state.Tick, spectator);
            return new(room, state.Tick, sequence, Commands(), Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }
        async Task Cold()
        {
            var frame = Frame(State()); Assert.Empty(frame.ValidationErrors);
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, new Store(frame), NoopMatchPlayerStore.Instance, options);
            session = await registry.GetOrCreateAsync(room, default);
            Assert.Equal(MatchStateHasher.HashValue(frame.PlayerViews["P1"].Snapshot), MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        }
        async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State().Tick;
            var wire = JsonSerializer.SerializeToElement(raw, Json); command = GameCommandJsonMapper.Map(wire);
            var intent = "diana-canonical-" + journal.Entries.Count;
            var result = await session.SubmitAsync(player, intent, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage); FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var count = journal.Entries.Count; var hash = MatchStateHasher.Hash(result.State);
            var duplicate = await session.SubmitAsync(player, intent, command, wire, default);
            Assert.True(duplicate.Accepted); Assert.Equal(count, journal.Entries.Count); Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            await Cold();
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
