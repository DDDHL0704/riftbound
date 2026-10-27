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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothRefusalLayersRestoreBothViewsAndBothOriginalCachedReceipts(bool coordinated)
    {
        var (initial, journal, actor) = await Opening();
        var original = journal.Entries[^1].AuthoritativeState;
        var normal = WebFrame(Frame(initial, journal, original, original));
        Assert.Empty(normal.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(normal, new CoreRuleEngine(), default));
        var forged = ExtraRed(original, actor);
        var bad = WebFrame(Frame(initial, journal, forged, coordinated ? forged : original));
        var history = MatchStateHasher.HashValue(new { normal.Commands, normal.Events });
        var writes = new Journal(); var store = new Store(bad);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, store);
        var refusal = await Refusal(registry, initial.RoomId);
        await Export("layer-" + coordinated, new { coordinated, normal, bad, actualRefusal = refusal?.ToString(),
            oldLeafActionLogPredicateWouldPass = refusal?.Message.Contains("action-log audit failed", StringComparison.Ordinal) == true });
        var error = Assert.IsType<MatchSessionException>(refusal);
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        if (coordinated)
        {
            Assert.Empty(bad.ValidationErrors);
            Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
            Assert.Contains("replayed final state hash", error.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(bad.ValidationErrors, e => e.Contains("rune pool total power", StringComparison.Ordinal));
            Assert.Contains(bad.ValidationErrors, e => e.Contains("rune pool typed power", StringComparison.Ordinal));
            Assert.Contains("match recovery validation failed", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("action-log audit failed", error.Message, StringComparison.Ordinal);
        }
        Assert.Null(await registry.FindAsync(initial.RoomId, default));
        Assert.Empty(writes.Entries);
        Assert.Equal(history, MatchStateHasher.HashValue(new { bad.Commands, bad.Events }));
        await AssertOriginalRecovery(registry, store, normal, writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoordinatedExtraBlueAndSameTotalColorSwapRequireTheOriginalLog(bool sameTotal)
    {
        var (initial, journal, actor) = await Opening();
        var original = journal.Entries[^1].AuthoritativeState;
        var colors = new Dictionary<string, int>(original.RunePools[actor].PowerByTrait) { [RuneTrait.Blue] = 2 };
        if (sameTotal) colors.Remove(RuneTrait.Red);
        var forged = original with { RunePools = new Dictionary<string, RunePool>(original.RunePools)
            { [actor] = original.RunePools[actor] with { PowerByTrait = colors } } };
        Assert.Equal(2, original.RunePools[actor].TotalPower);
        Assert.Equal(sameTotal ? 2 : 3, forged.RunePools[actor].TotalPower);
        Assert.Equal(2, forged.RunePools[actor].PowerByTrait[RuneTrait.Blue]);
        var normal = WebFrame(Frame(initial, journal, original, original));
        var bad = WebFrame(Frame(initial, journal, forged, forged));
        Assert.Empty(bad.ValidationErrors);
        var history = MatchStateHasher.HashValue(new { normal.Commands, normal.Events });
        var writes = new Journal(); var store = new Store(bad);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, store);
        var refusal = await Refusal(registry, initial.RoomId);
        await Export("blue-" + sameTotal, new { sameTotal, normal, bad, actualRefusal = refusal?.ToString() });
        var error = Assert.IsType<MatchSessionException>(refusal);
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("replayed final state hash", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(initial.RoomId, default));
        Assert.Empty(writes.Entries);
        Assert.Equal(history, MatchStateHasher.HashValue(new { bad.Commands, bad.Events }));
        await AssertOriginalRecovery(registry, store, normal, writes);
    }

    private static async Task AssertOriginalRecovery(InMemoryMatchSessionRegistry registry, Store store,
        MatchRecoveryFrame normal, Journal writes)
    {
        store.Frame = normal;
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(normal, new CoreRuleEngine(), default));
        var recovered = await registry.GetOrCreateAsync(normal.RoomId, default);
        foreach (var player in normal.PlayerViews.Keys)
        {
            Assert.Equal(WebHash(normal.PlayerViews[player].Snapshot), WebHash(recovered.SnapshotFor(player)));
            Assert.Equal(WebHash(normal.PlayerViews[player].Prompt), WebHash(recovered.PromptFor(player)));
        }
        foreach (var command in normal.Commands.TakeLast(2))
        {
            Assert.Equal(CommandTypes.PlayCard, command.CommandType);
            var raw = Assert.IsType<JsonElement>(command.RawCommand);
            var duplicate = await recovered.SubmitAsync(command.PlayerId, command.ClientIntentId,
                GameCommandJsonMapper.Map(raw), raw, default);
            Assert.Equal(command.Accepted, duplicate.Accepted);
            Assert.Equal(command.ErrorCode, duplicate.ErrorCode);
            Assert.Equal(command.ErrorMessage, duplicate.ErrorMessage);
            Assert.Empty(duplicate.Events);
            Assert.Equal(MatchStateHasher.Hash(normal.AuthoritativeState!), MatchStateHasher.Hash(duplicate.State));
            Assert.Empty(writes.Entries);
        }
    }

    private static MatchRecoveryFrame WebFrame(MatchRecoveryFrame frame)
    {
        var restored = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
        Assert.Equal(restored.ValidationErrors, MatchRecoveryValidator.Validate(restored.RoomId, restored.LastEventSequence,
            restored.Commands, restored.Events, restored.PlayerViews, restored.AuthoritativeState,
            restored.CurrentTick, restored.SpectatorReplayFrame));
        return restored;
    }
    private static string WebHash(object? value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, Web));
    private static async Task<Exception?> Refusal(InMemoryMatchSessionRegistry registry, string room)
    {
        try { await registry.GetOrCreateAsync(room, default); return null; }
        catch (Exception error) { return error; }
    }
    private static Task Export(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_S3_SURPLUS_EVIDENCE") is not { Length: > 0 } folder)
            return Task.CompletedTask;
        Directory.CreateDirectory(folder);
        return File.WriteAllTextAsync(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(value, Web));
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
            entry.StartedTick, entry.CompletedTick, entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) { ErrorCode = entry.ErrorCode, RuleInputs = entry.RuleInputs }).ToArray();
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
