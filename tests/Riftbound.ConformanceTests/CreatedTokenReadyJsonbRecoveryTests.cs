using System.Text;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CreatedTokenReadyJsonbRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("protect-pay")]
    [InlineData("protect-decline")]
    [InlineData("rise-0")]
    [InlineData("rise-2")]
    public async Task UnconfirmedCreatedTokenTriggerSurvivesJsonbObjectKeyOrder(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "current-opening-20261017", "embedded-ready-" + name + ".json");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var input = fixture.RootElement;
        var room = input.GetProperty("sourceRoomId").GetString()!;
        var initial = MatchReplayInitialStateBuilder.FromSeats(room,
            input.GetProperty("seats").Deserialize<Dictionary<string, string>>(Json)!);
        var journal = new Journal();
        var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal);
        var state = initial;
        foreach (var entry in input.GetProperty("acceptedCommands").EnumerateArray())
        {
            var raw = entry.GetProperty("rawCommand");
            var actor = entry.GetProperty("playerId").GetString()!;
            var intent = entry.GetProperty("clientIntentId").GetString()!;
            var result = entry.GetProperty("commandType").GetString() switch {
                "SUBMIT_DECK" => await session.SubmitDeckAsync(actor, intent, raw.Deserialize<SubmitDeckCommand>(Json)!, raw, default),
                "READY" => await session.ReadyAsync(actor, intent, raw, default),
                _ => await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(raw), raw, default)
            };
            Assert.True(result.Accepted, result.ErrorMessage);
            state = result.State;
        }
        Assert.Contains(state.TriggerQueue.Select(item => item.CreatedTokenReady)
            .Concat(state.StackItems.Select(item => item.CreatedTokenReady)), context => context is not null);
        var last = journal.Entries[^1];
        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
            entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) {
                RuleInputs = entry.RuleInputs, TypedCommand = entry.TypedCommand, ErrorCode = entry.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((item, index) =>
            new RecoveredEvent(entry.StartedEventSequence + index + 1, entry.CompletedTick, index, item))).ToArray();
        var views = last.Snapshots.ToDictionary(pair => pair.Key, pair => new RecoveredPlayerView(pair.Key,
            last.CompletedTick, last.CompletedEventSequence, pair.Value, last.CompletedTick,
            last.CompletedEventSequence, last.Prompts[pair.Key]));
        var original = new MatchRecoveryFrame(room, state.Tick, last.CompletedEventSequence, commands, events,
            views, [], state, initial, MatchReplayRedactor.BuildSpectatorFrame(last)) { OpeningSeed = initial.Seed };
        await AssertColdRecoveryAsync(original, engine, journal);
    }

    internal static async Task AssertColdRecoveryAsync(MatchRecoveryFrame original, CoreRuleEngine engine, IMatchJournal journal)
    {
        var state = original.AuthoritativeState!;
        var room = original.RoomId;
        var frame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(
            Reorder(JsonSerializer.SerializeToElement(original, Json)), Json), Json)!;
        var before = JsonSerializer.SerializeToElement(state);
        var after = JsonSerializer.SerializeToElement(frame.AuthoritativeState!);
        var differences = before.EnumerateObject().Where(property =>
            MatchStateHasher.HashValue(property.Value) != MatchStateHasher.HashValue(after.GetProperty(property.Name)))
            .Select(property => property.Name).ToArray();
        Assert.True(differences.Length == 0, "State changed on JSONB roundtrip: " + string.Join(", ", differences));
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(frame.AuthoritativeState!));
        var errors = MatchRecoveryValidator.Validate(room, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default));
        frame = frame with { ValidationErrors = errors };
        Assert.NotNull(await new InMemoryMatchSessionRegistry(engine, journal, new Store(frame)).GetOrCreateAsync(room, default));
    }

    private static object Reorder(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Object => value.EnumerateObject().OrderBy(property => Encoding.UTF8.GetByteCount(property.Name))
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => Reorder(property.Value), StringComparer.Ordinal),
        JsonValueKind.Array => value.EnumerateArray().Select(Reorder).ToArray(),
        _ => value.Clone()
    };

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
