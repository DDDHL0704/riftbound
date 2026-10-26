using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TerminalFaceDownDisclosureTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task EitherConcessionShowsTheCurrentStandbyCardWithoutTurningItFaceUpOrOpeningHiddenZones(string player)
    {
        // CN129.4/422.4/425.1.a: face-down field cards are shown at game end;
        // showing a card does not move it or play/turn it face up.
        var initial = StandbySourceRulesTests.Position("OGN·057/298");
        var journal = new Journal(); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal);
        async Task<ResolutionResult> Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), Json);
            var result = await session.SubmitAsync(actor, "terminal-show-" + player + "-" + journal.Entries.Count,
                command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); return result;
        }
        var hidden = await Act("P1", new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.True(hidden.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.False(Objects(hidden.Snapshots["P2"], "P1").TryGetProperty("SHADOW", out _));
        Assert.False(Objects(ResolutionResult.BuildSpectatorSnapshot(hidden.State), "P1").TryGetProperty("SHADOW", out _));
        var originalCard = MatchStateHasher.HashValue(hidden.State.CardObjects["SHADOW"]);
        var originalLocation = MatchStateHasher.HashValue(hidden.State.ObjectLocations["SHADOW"]);
        var beforeSnapshot = JsonSerializer.Serialize(hidden.Snapshots, Json);
        var beforeCommands = JsonSerializer.Serialize(Commands(journal), Json);
        var beforeEvents = JsonSerializer.Serialize(Events(journal), Json);
        var ended = await Act(player, new SurrenderCommand());
        Assert.Equal(MatchStatuses.Finished, ended.State.Status);
        Assert.Equal(player == "P1" ? "P2" : "P1", ended.State.WinnerPlayerId);
        Assert.Equal("MATCH_WON", Assert.Single(ended.Events).Kind);
        Assert.Equal(originalCard, MatchStateHasher.HashValue(ended.State.CardObjects["SHADOW"]));
        Assert.Equal(originalLocation, MatchStateHasher.HashValue(ended.State.ObjectLocations["SHADOW"]));
        Assert.Equal(beforeSnapshot, JsonSerializer.Serialize(hidden.Snapshots, Json));
        Assert.Equal(beforeCommands, JsonSerializer.Serialize(Commands(journal).Take(1), Json));
        Assert.Equal(beforeEvents, JsonSerializer.Serialize(Events(journal).Take(hidden.Events.Count), Json));
        AssertPublicFaceDownCardAndHiddenZones(ended.State, ended.Snapshots);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(ended);
        var restoredState = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(ended.State, Json), Json)!;
        Assert.Equal(MatchStateHasher.Hash(ended.State), MatchStateHasher.Hash(restoredState));
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(restoredState));
        AssertPublicFaceDownCardAndHiddenZones(restoredState, ResolutionResult.BuildSnapshots(restoredState));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(journal), ended.State, engine, default, Events(journal));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        await AssertRestoredTerminalSession(initial, ended, journal, new SurrenderCommand());
        var before = MatchStateHasher.Hash(ended.State);
        var rejected = await engine.ResolveAsync(ended.State, new("post-end", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(MatchStateHasher.HashValue(ended.Snapshots), MatchStateHasher.HashValue(rejected.Snapshots));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task TheSharedFinishedProjectionUsesTheSameDisclosureForEitherWinner(string winner)
    {
        // Projection contract only; actual normal-victory execution is covered
        // separately from this explicit midgame fixture.
        var initial = StandbySourceRulesTests.Position("OGN·057/298");
        var hidden = await new CoreRuleEngine().ResolveAsync(initial, new("terminal-projection-hide", "P1", CommandTypes.HideCard),
            new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]), default);
        Assert.True(hidden.Accepted, hidden.ErrorMessage);
        var finished = hidden.State with { Status = MatchStatuses.Finished, WinnerPlayerId = winner };
        AssertPublicFaceDownCardAndHiddenZones(finished, ResolutionResult.BuildSnapshots(finished));
        Assert.Equal(MatchStateHasher.HashValue(hidden.State.CardObjects), MatchStateHasher.HashValue(finished.CardObjects));
        Assert.Equal(MatchStateHasher.HashValue(hidden.State.PlayerZones), MatchStateHasher.HashValue(finished.PlayerZones));
    }

    [Fact]
    public async Task ARealBurnoutVictoryAlsoShowsTheStandbyCardAndReplaysItsOriginalCommands()
    {
        var initial = StandbySourceRulesTests.Position("OGN·057/298");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        foreach (var id in initial.PlayerZones["P1"].MainDeck.Concat(initial.PlayerZones["P1"].RuneDeck))
        { cards.Remove(id); locations.Remove(id); }
        cards["TERMINAL-DRAW"] = OfficialGarenConquestBattlefieldCountTests.Printed("TERMINAL-DRAW", "OGN·047/298", "P1");
        cards["TERMINAL-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("TERMINAL-RUNE", "SFD·R03", "P1");
        locations["TERMINAL-DRAW"] = new("P1", "HAND"); locations["TERMINAL-RUNE"] = new("P1", "RUNE_DECK");
        // The two draws share one real burnout: its graveyard refill supplies
        // the requested cards before the normal victory check (CN321/323.1).
        string[] refill = ["TERMINAL-G0", "TERMINAL-G1", "TERMINAL-G2"];
        foreach (var id in refill)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·083/298", "P1");
            locations[id] = new("P1", "GRAVEYARD");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 3) },
            PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P1"] = 0, ["P2"] = 7 },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Hand = initial.PlayerZones["P1"].Hand.Append("TERMINAL-DRAW").ToArray(), MainDeck = [], RuneDeck = ["TERMINAL-RUNE"],
                Graveyard = initial.PlayerZones["P1"].Graveyard.Concat(refill).ToArray() } } };
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(initial));
        var journal = new Journal(); var engine = new CoreRuleEngine();
        var session = new MatchSession(initial, engine, journal);
        var state = initial;
        async Task<ResolutionResult> Act(GameCommand command)
        {
            var result = await session.SubmitAsync("P1", "terminal-burnout-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), Json), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State; return result;
        }
        var hidden = await Act(new HideCardCommand("SHADOW", "OGN·057/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.False(Objects(hidden.Snapshots["P2"], "P1").TryGetProperty("SHADOW", out _));
        var physicalCard = MatchStateHasher.HashValue(hidden.State.CardObjects["SHADOW"]);
        await Act(new PlayCardCommand("TERMINAL-DRAW", "OGN·047/298", []));
        ResolutionResult? ended = null;
        for (var step = 0; state.Status != MatchStatuses.Finished && step < 8; step++)
        {
            var result = await session.SubmitAsync(state.PriorityPlayerId!, "terminal-burnout-" + journal.Entries.Count,
                new PassPriorityCommand(), JsonSerializer.SerializeToElement(new PassPriorityCommand(), Json), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State; ended = result;
        }
        Assert.NotNull(ended); Assert.Equal(MatchStatuses.Finished, state.Status);
        Assert.Equal(8, state.PlayerScores["P2"]); Assert.Equal("P2", state.WinnerPlayerId);
        Assert.Single(Events(journal), e => e.Event.Kind == "BURNOUT_APPLIED");
        Assert.Single(Events(journal), e => e.Event.Kind == "MATCH_WON");
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state));
        Assert.Equal(physicalCard, MatchStateHasher.HashValue(state.CardObjects["SHADOW"]));
        AssertPublicFaceDownCardAndHiddenZones(state, ended.Snapshots);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(ended);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(journal), state, engine, default, Events(journal));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        await AssertRestoredTerminalSession(initial, ended, journal, new PassPriorityCommand());
    }

    private static async Task AssertRestoredTerminalSession(MatchState initial, ResolutionResult ended, Journal journal, GameCommand finalCommand)
    {
        var state = ended.State; var commands = Commands(journal); var events = Events(journal);
        var sequence = commands[^1].CompletedEventSequence; var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            ended.Snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
        Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator)
            { OpeningSeed = initial.Seed };
        var cold = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        var restored = MatchSession.Restore(cold, new CoreRuleEngine(), journal);
        var snapshots = state.Seats.Keys.ToDictionary(p => p, restored.SnapshotFor);
        AssertPublicFaceDownCardAndHiddenZones(state, snapshots);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(ended.Snapshots[player]), MatchStateHasher.HashValue(snapshots[player]));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
        var prefix = JsonSerializer.Serialize(journal.Entries, Json); var last = journal.Entries[^1];
        var duplicate = await restored.SubmitAsync(last.PlayerId, last.ClientIntentId, finalCommand, last.RawCommand, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage); Assert.Empty(duplicate.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(MatchStateHasher.HashValue(ended.Snapshots), MatchStateHasher.HashValue(duplicate.Snapshots));
        var late = await Assert.ThrowsAsync<MatchSessionException>(() => restored.SubmitAsync("P1", "restored-terminal-late",
            new PassPriorityCommand(), JsonSerializer.SerializeToElement(new PassPriorityCommand(), Json), default).AsTask());
        Assert.Equal(ErrorCodes.MatchFinished, late.Code);
        Assert.Equal(prefix, JsonSerializer.Serialize(journal.Entries, Json));
    }

    private static void AssertPublicFaceDownCardAndHiddenZones(MatchState state, IReadOnlyDictionary<string, SnapshotDto> snapshots)
    {
        foreach (var (viewer, snapshot) in snapshots.Append(new("__spectator__", ResolutionResult.BuildSpectatorSnapshot(state))))
        {
            Assert.True(Objects(snapshot, "P1").TryGetProperty("SHADOW", out var card),
                $"CN422.4 requires the finished view for {viewer} to show SHADOW.");
            Assert.Equal("OGN·057/298", card.GetProperty("cardNo").GetString());
            Assert.True(card.GetProperty("isFaceDown").GetBoolean());
            foreach (var (owner, zones) in state.PlayerZones)
            {
                var objects = Objects(snapshot, owner);
                foreach (var id in zones.MainDeck.Concat(zones.RuneDeck)) Assert.False(objects.TryGetProperty(id, out _));
                if (viewer != owner)
                    foreach (var id in zones.Hand) Assert.False(objects.TryGetProperty(id, out _));
            }
        }
    }

    private static JsonElement Objects(SnapshotDto snapshot, string player)
        => JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("players").GetProperty(player).GetProperty("objects");

    private static RecoveredCommand[] Commands(Journal journal) => journal.Entries.Select(e => new RecoveredCommand(
        e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick,
        e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
        { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();

    private static RecoveredEvent[] Events(Journal journal) => journal.Entries.SelectMany(e => e.Events.Select((value, i) =>
        new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, value))).ToArray();

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
