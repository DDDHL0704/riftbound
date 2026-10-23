using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CoherentSurplusForgeryRecoveryTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ChangingOnlyAuthoritativeSurplusFailsViewChecksBeforeTheActionLogAudit()
    {
        var (initial, journal, actor) = await Opening(); var original = journal.Entries[^1].AuthoritativeState;
        var bad = Frame(initial, journal, ExtraRed(original, actor), original);
        Assert.Contains(bad.ValidationErrors, error => error.Contains("rune pool total power", StringComparison.Ordinal));
        Assert.Contains(bad.ValidationErrors, error => error.Contains("rune pool typed power", StringComparison.Ordinal));
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new Journal(), new Store(bad));
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(initial.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.DoesNotContain("action-log audit failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CoordinatedSurplusViewsRemainShapeValidButOriginalHistoryRejectsThemWithoutCachingOrWriting()
    {
        var (initial, journal, actor) = await Opening(); var original = journal.Entries[^1].AuthoritativeState;
        Assert.Equal(1, original.RunePools[actor].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, original.RunePools[actor].PowerByTrait[RuneTrait.Blue]);
        var normal = Frame(initial, journal, original, original); Assert.Empty(normal.ValidationErrors);
        var forged = ExtraRed(original, actor); var bad = Frame(initial, journal, forged, forged);
        Assert.Empty(bad.ValidationErrors);
        foreach (var view in bad.PlayerViews.Values)
        {
            var pool = JsonSerializer.SerializeToElement(view.Snapshot!.Players[actor], Web).GetProperty("runePool");
            Assert.Equal(3, pool.GetProperty("power").GetInt32());
            Assert.Equal(0, pool.GetProperty("untypedPower").GetInt32());
            Assert.Equal(2, pool.GetProperty("powerByTrait").GetProperty("red").GetInt32());
            Assert.Equal(1, pool.GetProperty("powerByTrait").GetProperty("blue").GetInt32());
        }
        var historyHash = MatchStateHasher.HashValue(new { normal.Commands, normal.Events });
        var writes = new Journal(); var store = new Store(bad);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, store);
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(initial.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("replayed final state hash", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(initial.RoomId, default)); Assert.Empty(writes.Entries);
        Assert.Equal(historyHash, MatchStateHasher.HashValue(new { bad.Commands, bad.Events }));
        store.Frame = normal;
        var recovered = await registry.GetOrCreateAsync(initial.RoomId, default);
        var restoredPool = JsonSerializer.SerializeToElement(recovered.SnapshotFor(actor).Players[actor], Web).GetProperty("runePool");
        Assert.Equal(2, restoredPool.GetProperty("power").GetInt32());
        Assert.Equal(1, restoredPool.GetProperty("powerByTrait").GetProperty("red").GetInt32());
        Assert.Empty(writes.Entries);
    }

    private static MatchState ExtraRed(MatchState original, string actor)
        => original with { RunePools = new Dictionary<string, RunePool>(original.RunePools) {
            [actor] = original.RunePools[actor] with { PowerByTrait = new Dictionary<string, int>(original.RunePools[actor].PowerByTrait) { [RuneTrait.Red] = 2 } } } };

    private static async Task<(MatchState Initial, Journal Journal, string Actor)> Opening()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "card-play-history-current-opening-20261017.json")));
        var fixture = document.RootElement; Assert.Equal(0, fixture.GetProperty("openingSeed").GetInt64());
        var entries = fixture.GetProperty("acceptedCommands").EnumerateArray().ToArray(); Assert.Equal(9, entries.Length);
        var initial = MatchReplayInitialStateBuilder.FromSeats(fixture.GetProperty("sourceRoomId").GetString()!, fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        var journal = new Journal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        foreach (var entry in entries.Take(8))
        {
            var raw = entry.GetProperty("rawCommand").Clone(); var command = GameCommandJsonMapper.Map(raw);
            var player = entry.GetProperty("playerId").GetString()!; var id = entry.GetProperty("clientIntentId").GetString()!;
            var result = command switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(player, id, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(player, id, raw, default),
                _ => await session.SubmitAsync(player, id, command, raw, default) };
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        var actor = entries[^1].GetProperty("playerId").GetString()!;
        var node = JsonNode.Parse(entries[^1].GetProperty("rawCommand").GetRawText())!.AsObject();
        node["optionalCosts"] = new JsonArray(entries.Take(8).Where(entry => entry.GetProperty("rawCommand").GetProperty("cmdType").GetString() == CommandTypes.TapRune)
            .Reverse().Select(entry => JsonValue.Create("RECYCLE_RUNE:" + entry.GetProperty("rawCommand").GetProperty("sourceObjectId").GetString())).ToArray());
        node["promptId"] = session.PromptFor(actor).PromptId; node["snapshotTick"] = journal.Entries[^1].CompletedTick;
        var payload = JsonSerializer.SerializeToElement(node); var play = GameCommandJsonMapper.Map(payload);
        var accepted = await session.SubmitAsync(actor, "coherent-surplus-play", play, payload, default); Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var stale = await session.SubmitAsync(actor, "coherent-surplus-stale", play, payload, default);
        Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Empty(stale.Events);
        Assert.Equal(10, journal.Entries.Count); Assert.Equal(9, journal.Entries.Count(entry => entry.Accepted));
        Assert.Equal(32, journal.Entries.Sum(entry => entry.Events.Count));
        return (initial, journal, actor);
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState state, MatchState projectionState)
    {
        var last = journal.Entries[^1];
        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId, entry.CommandType, entry.RawCommand,
            entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) { RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((item, i) => new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, item))).ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(projectionState); var prompts = ResolutionResult.BuildPrompts(projectionState);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, state.Tick, last.CompletedEventSequence,
            snapshots[player], state.Tick, last.CompletedEventSequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, last.CompletedEventSequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, last.CompletedEventSequence, commands, events, views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, last.CompletedEventSequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public MatchRecoveryFrame Frame { get; set; } = frame;
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(Frame);
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
