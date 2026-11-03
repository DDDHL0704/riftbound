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
        session = await AssertColdRecoveryAsync(Frame(initial, journal, state), engine, journal);
        var prefixCommands = journal.Entries.Count;
        state = await CreatedTokenReadySqlAcceptance.PrepareConfirmationAsync(state, Act);
        var ready = Assert.Single(state.StackItems, item => item.CreatedTokenReady is not null);
        Assert.Equal(input.GetProperty("expectedSourceObjectId").GetString(), ready.CreatedTokenReady!.Source.ObjectId);
        var tokens = input.GetProperty("expectedTokenObjectIds").EnumerateArray().Select(value => value.GetString()!).ToArray();
        var recipients = input.GetProperty("expectedReadyObjectIds").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal(tokens, ready.CreatedTokenReady.Units.Select(binding => binding.ObjectId));
        Assert.All(tokens, id => Assert.True(state.CardObjects[id].IsExhausted));
        if (tokens.Length > 0) Assert.True(journal.Entries.Count > prefixCommands, "Azir's simultaneous triggers must be ordered through an accepted command.");
        if (state.PendingPayment is { } payment)
            await Act(payment.PlayerId, new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
                [input.GetProperty("expectedPayYellow").GetBoolean() ? "PAY" : "DECLINE"]));
        else if (state.PendingCardChoice is { } readyChoice)
        {
            Assert.Equal("CREATED_TOKEN_READY", readyChoice.EffectKind);
            await Act(readyChoice.PlayerId, new ChooseCardsCommand(readyChoice.ChoiceId, readyChoice.ChoiceWindow, recipients));
        }
        Assert.All(tokens, id => Assert.True(state.CardObjects[id].IsExhausted));
        session = await AssertColdRecoveryAsync(Frame(initial, journal, state), engine, journal);
        for (var step = 0; step < 64 && (state.StackItems.Count > 0 || state.TriggerQueue.Count > 0); step++)
        {
            if (state.PendingCardChoice is { } remainingChoice)
            {
                Assert.Equal(0, remainingChoice.RequiredCount);
                Assert.NotEqual("CREATED_TOKEN_READY", remainingChoice.EffectKind);
                await Act(remainingChoice.PlayerId, new ChooseCardsCommand(remainingChoice.ChoiceId, remainingChoice.ChoiceWindow, []));
            }
            else if (state.PendingPayment is { } remainingPayment)
                await Act(remainingPayment.PlayerId, new PayCostCommand(remainingPayment.PaymentId, remainingPayment.PaymentWindow, ["DECLINE"]));
            else if (ResolutionResult.BuildPrompts(state).FirstOrDefault(pair =>
                pair.Value.EnabledActions().Contains(CommandTypes.OrderTriggers)).Key is { } actor)
                await Act(actor, new OrderTriggersCommand(
                    OrderedTriggerIds: state.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
            else await Act(state.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Empty(state.StackItems);
        Assert.Empty(state.TriggerQueue);
        Assert.All(tokens, id => Assert.Equal(!recipients.Contains(id), state.CardObjects[id].IsExhausted));
        await AssertColdRecoveryAsync(Frame(initial, journal, state), engine, journal);

        async Task<MatchState> Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), Json);
            var result = await session.SubmitAsync(actor, "created-ready-jsonb-" + journal.Entries.Count,
                command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            state = result.State;
            return state;
        }
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState state)
    {
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
        return new MatchRecoveryFrame(state.RoomId, state.Tick, last.CompletedEventSequence, commands, events,
            views, [], state, initial, MatchReplayRedactor.BuildSpectatorFrame(last)) { OpeningSeed = initial.Seed };
    }

    internal static async Task<IMatchSession> AssertColdRecoveryAsync(MatchRecoveryFrame original, CoreRuleEngine engine, IMatchJournal journal)
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
        return await new InMemoryMatchSessionRegistry(engine, journal, new Store(frame)).GetOrCreateAsync(room, default);
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
