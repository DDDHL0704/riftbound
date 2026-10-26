using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TerminalDisclosureRecoveryGuardTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string StandbyId = "P1-FACEDOWN-OGN-TEEMO-PURPLE";
    private const string StandbyCardNo = "OGN·197/298";

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task CoordinatedFinishedStateCannotExposeAnUnfinishedStandbyThroughTheRegistry(string forgedWinner)
    {
        // Actual ROOM_SEAT records and the existing accepted DEV scenario,
        // including its engine HIDE_CARD preparation, retain a valid birth.
        // This is not certification of an ordinary deck/opening sequence.
        var room = "terminal-disclosure-registry-" + forgedWinner;
        var journal = new Journal(); var session = new MatchSession(room, new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        var seeded = await session.SeedScenarioAsync("P1", "original-scenario", "standby-reaction", null, default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        Assert.True(seeded.State.CardObjects[StandbyId].IsFaceDown);
        var initial = MatchState.Create(room) with { Seed = journal.Entries[0].OpeningSeed };
        var command = new PassPriorityCommand();
        var raw = JsonSerializer.SerializeToElement(command, Json);
        var hidden = await session.SubmitAsync("P1", "original-pass", command, raw, default);
        Assert.True(hidden.Accepted, hidden.ErrorMessage);
        Assert.Equal(MatchStatuses.InProgress, hidden.State.Status); Assert.Null(hidden.State.WinnerPlayerId);
        Assert.False(Objects(hidden.Snapshots["P2"]).TryGetProperty(StandbyId, out _));
        var original = Cold(Frame(initial, hidden.State, journal));
        Assert.False(Objects(original.SpectatorReplayFrame!.SpectatorSnapshot).TryGetProperty(StandbyId, out _));
        var originalAudit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(original, new CoreRuleEngine(), default);
        Assert.True(originalAudit.Count == 0, string.Join("\n", originalAudit));

        var forgedState = hidden.State with { Status = MatchStatuses.Finished, WinnerPlayerId = forgedWinner };
        var forged = Cold(Frame(initial, forgedState, journal));
        Assert.Equal(StandbyCardNo, Objects(forged.PlayerViews["P2"].Snapshot).GetProperty(StandbyId).GetProperty("cardNo").GetString());
        Assert.Equal(StandbyCardNo, Objects(forged.SpectatorReplayFrame!.SpectatorSnapshot).GetProperty(StandbyId).GetProperty("cardNo").GetString());
        Assert.Equal(JsonSerializer.Serialize(original.Commands, Json), JsonSerializer.Serialize(forged.Commands, Json));
        Assert.Equal(JsonSerializer.Serialize(original.Events, Json), JsonSerializer.Serialize(forged.Events, Json));
        Assert.NotEqual(MatchStateHasher.Hash(original.AuthoritativeState!), MatchStateHasher.Hash(forged.AuthoritativeState!));
        var shape = MatchRecoveryValidator.Validate(forged.RoomId, forged.LastEventSequence, forged.Commands,
            forged.Events, forged.PlayerViews, forged.AuthoritativeState, forged.CurrentTick, forged.SpectatorReplayFrame);
        Assert.True(shape.Count == 0, string.Join("\n", shape));
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default);
        Assert.Contains(audit, error => error.Contains("final-state hash mismatch", StringComparison.Ordinal));
        var before = JsonSerializer.Serialize(journal.Entries, Json);
        var store = new Store(forged); var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store);
        var failures = new List<string>();
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var failure = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(initial.RoomId, default));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, failure.Code);
            Assert.Contains("action-log audit failed", failure.Message);
            Assert.Contains("final-state hash mismatch", failure.Message);
            failures.Add(failure.Message);
            Assert.Equal(attempt, store.Loads); Assert.Null(await registry.FindAsync(initial.RoomId, default));
            Assert.Equal(before, JsonSerializer.Serialize(journal.Entries, Json));
        }
        store.Frame = original;
        var restored = await registry.GetOrCreateAsync(initial.RoomId, default);
        Assert.Equal(3, store.Loads);
        Assert.Same(restored, await registry.GetOrCreateAsync(initial.RoomId, default)); Assert.Equal(3, store.Loads);
        foreach (var player in initial.Seats.Keys)
            Assert.Equal(MatchStateHasher.HashValue(original.PlayerViews[player].Snapshot), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
        Assert.False(Objects(restored.SnapshotFor("P2")).TryGetProperty(StandbyId, out _));
        var duplicate = await restored.SubmitAsync("P1", "original-pass", command, raw, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage); Assert.Empty(duplicate.Events);
        Assert.Equal(MatchStateHasher.Hash(hidden.State), MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(before, JsonSerializer.Serialize(journal.Entries, Json));
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_RAW_ENVELOPE_EVIDENCE");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "terminal-forgery-" + forgedWinner + ".json"),
                JsonSerializer.Serialize(new { original, forged, shape, audit, failures, store.Loads, journalUnchanged = true }, Json));
        }
    }

    private static T Cold<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    private static JsonElement Objects(SnapshotDto snapshot)
        => JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("players").GetProperty("P1").GetProperty("objects");

    private static MatchRecoveryFrame Frame(MatchState initial, MatchState state, Journal journal)
    {
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
            { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((value, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, value))).ToArray();
        var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
            snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial, spectator) { OpeningSeed = initial.Seed };
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        internal MatchRecoveryFrame Frame { get; set; } = frame;
        internal int Loads { get; private set; }
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
        { Loads++; return ValueTask.FromResult<MatchRecoveryFrame?>(Frame); }
    }
}
