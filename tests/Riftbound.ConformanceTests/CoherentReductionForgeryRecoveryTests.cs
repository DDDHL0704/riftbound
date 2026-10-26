using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CoherentReductionForgeryRecoveryTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StateOnlyManaMutationRetainsTheIndependentViewRejection(bool copied)
    {
        var setup = await Opening(copied);
        var original = setup.Journal.Entries[^1].AuthoritativeState;
        var bad = Frame(setup, ExtraMana(original, setup.Actor), original);
        Assert.Contains(bad.ValidationErrors, e => e.Contains("rune pool ordinary mana", StringComparison.Ordinal));
        var writes = new Journal(); var store = new Store(bad);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, store);
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(original.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("match recovery validation failed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(original.RoomId, default)); Assert.Empty(writes.Entries);
        store.Frame = Frame(setup, original, original);
        await registry.GetOrCreateAsync(original.RoomId, default);
        Assert.Empty(writes.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoordinatedViewsPassShapeButOriginalOptionalReductionHistoryRejectsTheExtraMana(bool copied)
    {
        var setup = await Opening(copied);
        var original = setup.Journal.Entries[^1].AuthoritativeState;
        var normal = Frame(setup, original, original); Assert.Empty(normal.ValidationErrors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(normal, new CoreRuleEngine(), default));
        var history = MatchStateHasher.HashValue(new { normal.Commands, normal.Events });
        var forged = ExtraMana(original, setup.Actor);
        var bad = Frame(setup, forged, forged); Assert.Empty(bad.ValidationErrors);
        foreach (var view in bad.PlayerViews.Values)
            Assert.Equal(1, JsonSerializer.SerializeToElement(view.Snapshot!.Players[setup.Actor], Web)
                .GetProperty("runePool").GetProperty("mana").GetInt32());
        var writes = new Journal(); var store = new Store(bad);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, store);
        var error = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(original.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("action-log audit failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("replayed final state hash", error.Message, StringComparison.Ordinal);
        Assert.Null(await registry.FindAsync(original.RoomId, default)); Assert.Empty(writes.Entries);
        Assert.Equal(history, MatchStateHasher.HashValue(new { bad.Commands, bad.Events }));
        store.Frame = normal;
        var recovered = await registry.GetOrCreateAsync(original.RoomId, default);
        foreach (var player in original.Seats.Keys)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_S4_FORGERY_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "original-views-" + copied + "-" + player + ".json"), JsonSerializer.Serialize(new {
                    expectedSnapshot = normal.PlayerViews[player].Snapshot, actualSnapshot = recovered.SnapshotFor(player),
                    expectedPrompt = normal.PlayerViews[player].Prompt, actualPrompt = recovered.PromptFor(player)
                }, Web));
            }
            Assert.Equal(ProtocolViewHash(normal.PlayerViews[player].Snapshot), ProtocolViewHash(recovered.SnapshotFor(player)));
            Assert.Equal(ProtocolViewHash(normal.PlayerViews[player].Prompt), ProtocolViewHash(recovered.PromptFor(player)));
        }
        var count = setup.Journal.Entries.Count;
        var cached = await recovered.SubmitAsync(setup.Actor, "coherent-reduction-paid", setup.Command, setup.Raw, default);
        Assert.True(cached.Accepted, cached.ErrorMessage);
        Assert.Equal(MatchStateHasher.Hash(original), MatchStateHasher.Hash(cached.State));
        Assert.Empty(writes.Entries); Assert.Equal(count, setup.Journal.Entries.Count);
    }

    private static string ProtocolViewHash(object? view)
        => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(view, Web));

    private static MatchState ExtraMana(MatchState state, string actor) => state with {
        RunePools = new Dictionary<string, RunePool>(state.RunePools) { [actor] = state.RunePools[actor] with { Mana = 1 } }
    };

    private static async Task<Setup> Opening(bool copied)
    {
        var initial = MatchReplayInitialStateBuilder.FromSeats("coherent-reduction-" + copied,
            new Dictionary<string, string> { ["fee-reduction-a"] = "P1", ["fee-reduction-b"] = "P2" });
        Assert.Empty(initial.CardObjects); Assert.Equal(0, initial.Seed);
        var journal = new Journal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var opening = copied ? await OptionalCostReductionCopiedOrdinaryOpening.BuildAsync(initial, session)
            : await OptionalCostReductionOrdinaryOpening.BuildAsync(initial, session, "SFD·149/221");
        var raw = Wire(opening.Command);
        var invalid = Wire(opening.ForgedCommand);
        var rejected = await session.SubmitAsync(opening.Actor, "coherent-reduction-bad-instance", opening.ForgedCommand, invalid, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(opening.State), MatchStateHasher.Hash(rejected.State));
        var paid = await session.SubmitAsync(opening.Actor, "coherent-reduction-paid", opening.Command, raw, default);
        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal(0, paid.State.RunePools[opening.Actor].Mana); Assert.Equal(0, paid.State.RunePools[opening.Actor].TotalPower);
        var stale = await session.SubmitAsync(opening.Actor, "coherent-reduction-stale", opening.Command, raw, default);
        Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Empty(stale.Events);
        Assert.Equal(opening.OpeningCommands + 3, journal.Entries.Count);
        Assert.Equal(copied ? 57 : 45, journal.Entries.Count);
        Assert.Equal(copied ? 348 : 291, journal.Entries.Sum(e => e.Events.Count));
        return new(initial, journal, opening.Actor, opening.Command, raw);

        JsonElement Wire(GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            node["promptId"] = session.PromptFor(opening.Actor).PromptId; node["snapshotTick"] = opening.State.Tick;
            return JsonSerializer.SerializeToElement(node);
        }
    }

    private static MatchRecoveryFrame Frame(Setup setup, MatchState state, MatchState projection)
    {
        var entries = setup.Journal.Entries; var last = entries[^1];
        var commands = entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(projection); var prompts = ResolutionResult.BuildPrompts(projection);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, last.CompletedEventSequence,
            snapshots[p], state.Tick, last.CompletedEventSequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, last.CompletedEventSequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, last.CompletedEventSequence, commands, events, views, state, state.Tick, spectator);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, last.CompletedEventSequence, commands, events, views, errors, state, setup.Initial, spectator)
            { OpeningSeed = setup.Initial.Seed };
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
    }

    private sealed record Setup(MatchState Initial, Journal Journal, string Actor, PlayCardCommand Command, JsonElement Raw);
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public MatchRecoveryFrame Frame { get; set; } = frame;
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(Frame);
    }
}
