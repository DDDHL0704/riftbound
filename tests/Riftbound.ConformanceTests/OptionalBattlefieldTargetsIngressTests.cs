using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalBattlefieldTargetsIngressTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly string[] Kinds = ["DECLARE_BATTLE"];
    private static readonly (string Name, string? Value, string? Extra)[] Malformed = [
        ("null", "null", null), ("object", "{}", null), ("number", "1", null),
        ("boolean", "true", null), ("scalar-string", "\"ECHO\"", null),
        ("null-element", "[null]", null), ("number-element", "[1]", null),
        ("empty-element", "[\"\"]", null), ("blank-element", "[\"  \"]", null),
        ("leading-space", "[\" ECHO\"]", null), ("trailing-space", "[\"ECHO \"]", null),
        ("mixed-elements", "[\"ECHO\",null]", null),
        ("duplicate-valid", "[]", ",\"battlefieldTargetObjectIds\":[]"),
        ("duplicate-last-valid", "null", ",\"battlefieldTargetObjectIds\":[]"),
        ("property-whitespace", null, ",\" battlefieldTargetObjectIds \":[]")
    ];

    public static IEnumerable<object[]> CommandKinds() => Kinds.Select(kind => new object[] { kind });
    public static IEnumerable<object[]> MalformedCases() => Kinds.SelectMany(kind =>
        Malformed.Select(row => new object[] { kind, row.Name }));
    public static IEnumerable<object[]> CanonicalCases() => Kinds.SelectMany(kind =>
        new[] { "omitted", "empty", "canonical-choice" }.Select(shape => new object[] { kind, shape }));
    public static IEnumerable<object[]> StatusCases() => Kinds.SelectMany(kind =>
        new[] { false, true }.Select(finished => new object[] { kind, finished }));

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public void TypedProducersOmitNullAndKeepExplicitCanonicalArrays(string kind)
    {
        foreach (var options in new[] { new JsonSerializerOptions(), Web })
        foreach (var targets in new IReadOnlyList<string>?[] { null, [], ["ECHO", "CASE-SENSITIVE"] })
        {
            var command = Typed(kind, targets);
            var wire = JsonSerializer.SerializeToElement(command, command.GetType(), options);
            var property = options.PropertyNamingPolicy is null ? "BattlefieldTargetObjectIds" : "battlefieldTargetObjectIds";
            if (targets is null) Assert.False(wire.TryGetProperty(property, out _));
            else Assert.Equal(targets, wire.GetProperty(property).EnumerateArray().Select(x => x.GetString()!).ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public async Task MalformedFieldNeverReachesEngineOrAcceptedOrRejectedJournal(string kind, string shape)
    {
        var setup = await OpeningPrefix();
        var raw = BadRaw(setup, kind, shape);
        var before = setup.Current.State;
        var beforeCalls = setup.Engine.Calls;
        var beforeJournal = JournalHash(setup.Journal);
        var result = await setup.Session.SubmitAsync(setup.Actor, "malformed-battlefield-targets",
            GameCommandJsonMapper.Map(raw), raw, default);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Same(before, result.State);
        Assert.Empty(result.Events);
        Assert.Equal(beforeCalls, setup.Engine.Calls);
        Assert.Equal(beforeJournal, JournalHash(setup.Journal));
        Assert.Equal(8, setup.Journal.Entries.Count);
        Assert.Empty(StructuralErrors(Frame(setup, result.State)));

        // Rejected rows are audited too. Persisting this request as a rejected
        // command would still poison recovery; keep that independent guard.
        var frame = Frame(setup, before);
        var rejected = new RecoveredCommand(setup.Actor, "forged-rejected-row", kind, raw,
            before.Tick, before.Tick, frame.LastEventSequence, frame.LastEventSequence, false, "rejected");
        Assert.Contains(StructuralErrors(frame with { Commands = frame.Commands.Append(rejected).ToArray() }),
            error => error.Contains("battlefieldTargetObjectIds", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public async Task CanonicalFieldsReachRealRulesAndColdRecoverOriginalJournal(string kind, string shape)
    {
        var setup = await OpeningPrefix();
        var value = shape switch { "omitted" => null, "empty" => "[]", _ => "[\"SHAPE_ONLY_CHOICE\"]" };
        var raw = Raw(setup, kind, value);
        var beforeCalls = setup.Engine.Calls;
        var result = await setup.Session.SubmitAsync(setup.Actor, "canonical-battlefield-targets",
            GameCommandJsonMapper.Map(raw), raw, default);
        Assert.Equal(beforeCalls + 1, setup.Engine.Calls);
        Assert.Equal(9, setup.Journal.Entries.Count);
        await AssertColdOriginalReplay(setup, result.State);
    }

    [Theory]
    [MemberData(nameof(StatusCases))]
    public async Task MalformedFieldPrecedesStalePromptAndFinishedStatusJournalPaths(string kind, bool finished)
    {
        var setup = await OpeningPrefix();
        if (finished)
        {
            var surrender = new SurrenderCommand();
            setup.Current = await setup.Session.SubmitAsync(setup.Actor, "finish-for-status-boundary", surrender,
                JsonSerializer.SerializeToElement(surrender, Web), default);
            Assert.True(setup.Current.Accepted);
            Assert.Equal(MatchStatuses.Finished, setup.Current.State.Status);
        }
        var rawNode = JsonNode.Parse(Raw(setup, kind, "null").GetRawText())!.AsObject();
        rawNode["snapshotTick"] = setup.Current.State.Tick - 1;
        rawNode["promptId"] = "expired-battlefield-targets-prompt";
        var raw = JsonSerializer.SerializeToElement(rawNode);
        var before = JournalHash(setup.Journal);
        var beforeCalls = setup.Engine.Calls;
        var result = await setup.Session.SubmitAsync(setup.Actor, "stale-malformed-battlefield-targets",
            GameCommandJsonMapper.Map(raw), raw, default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Same(setup.Current.State, result.State);
        Assert.Empty(result.Events);
        Assert.Equal(beforeCalls, setup.Engine.Calls);
        Assert.Equal(before, JournalHash(setup.Journal));
        await AssertColdOriginalReplay(setup, result.State);
    }

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public async Task ExistingIntentConflictKeepsPrecedenceAndOriginalHistory(string kind)
    {
        var setup = await OpeningPrefix();
        var canonical = Raw(setup, kind, "[]");
        setup.Current = await setup.Session.SubmitAsync(setup.Actor, "same-existing-intent",
            GameCommandJsonMapper.Map(canonical), canonical, default);
        var raw = Raw(setup, kind, "null");
        var before = JournalHash(setup.Journal);
        var beforeCalls = setup.Engine.Calls;
        var result = await setup.Session.SubmitAsync(setup.Actor, "same-existing-intent",
            GameCommandJsonMapper.Map(raw), raw, default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(beforeCalls, setup.Engine.Calls);
        Assert.Equal(before, JournalHash(setup.Journal));
        await AssertColdOriginalReplay(setup, setup.Current.State);
    }

    [Theory]
    [MemberData(nameof(CommandKinds))]
    public async Task CorrectedRequestCanReuseAnUnjournaledMalformedIntent(string kind)
    {
        var setup = await OpeningPrefix();
        var bad = Raw(setup, kind, "null");
        var rejected = await setup.Session.SubmitAsync(setup.Actor, "correctable-intent",
            GameCommandJsonMapper.Map(bad), bad, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, rejected.ErrorCode);
        Assert.Equal(8, setup.Journal.Entries.Count);
        var canonical = Raw(setup, kind, "[]");
        var beforeCalls = setup.Engine.Calls;
        var result = await setup.Session.SubmitAsync(setup.Actor, "correctable-intent",
            GameCommandJsonMapper.Map(canonical), canonical, default);
        Assert.Equal(beforeCalls + 1, setup.Engine.Calls);
        Assert.Equal(9, setup.Journal.Entries.Count);
        await AssertColdOriginalReplay(setup, result.State);
    }

    [Fact]
    public async Task IndependentRecoveryStillRejectsMalformedCanonicalRejectedBattleRows()
    {
        var setup = await OpeningPrefix();
        var canonical = Raw(setup, "DECLARE_BATTLE", "[]");
        var result = await setup.Session.SubmitAsync(setup.Actor, "canonical-battle-shape-control",
            GameCommandJsonMapper.Map(canonical), canonical, default);
        var frame = Frame(setup, result.State);
        Assert.Empty(StructuralErrors(frame));
        var before = JournalHash(setup.Journal);
        foreach (var shape in Malformed)
        {
            var commands = frame.Commands.ToArray();
            commands[^1] = commands[^1] with { RawCommand = BadRaw(setup, "DECLARE_BATTLE", shape.Name) };
            Assert.Contains(StructuralErrors(frame with { Commands = commands }),
                error => error.Contains("battlefieldTargetObjectIds", StringComparison.Ordinal));
        }
        Assert.Equal(before, JournalHash(setup.Journal));
    }

    private static GameCommand Typed(string kind, IReadOnlyList<string>? targets)
        => new DeclareBattleCommand("FIELD", [], [], OptionalCosts: [], BattlefieldTargetObjectIds: targets);

    private static JsonElement BadRaw(Setup setup, string kind, string name)
    {
        var shape = Malformed.Single(row => row.Name == name);
        return Raw(setup, kind, shape.Value, shape.Extra);
    }

    private static JsonElement Raw(Setup setup, string kind, string? value, string? extra = null)
    {
        var node = JsonNode.Parse(setup.PlannedPlay.GetRawText())!.AsObject();
        node["cmdType"] = kind;
        node["battlefieldId"] = setup.Current.State.PlayerZones[setup.Actor].Battlefields.First(id =>
            setup.Current.State.CardObjects[id].Tags.Contains("CARD_TYPE:BATTLEFIELD"));
        node["attackerObjectIds"] = new JsonArray();
        node["defenderObjectIds"] = new JsonArray();
        node.Remove("battlefieldTargetObjectIds");
        var text = node.ToJsonString();
        text = text[..^1] + (value is null ? "" : ",\"battlefieldTargetObjectIds\":" + value) + extra + "}";
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static async Task<Setup> OpeningPrefix()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "card-play-history-current-opening-20261017.json")));
        var fixture = document.RootElement;
        var initial = MatchReplayInitialStateBuilder.FromSeats(fixture.GetProperty("sourceRoomId").GetString()!,
            fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        Assert.Equal(0, fixture.GetProperty("openingSeed").GetInt64());
        Assert.Equal(0, initial.Seed);
        Assert.Empty(initial.CardObjects);
        var journal = new Journal();
        var engine = new CountingEngine();
        var session = new MatchSession(initial, engine, journal);
        ResolutionResult? current = null;
        var commands = fixture.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        Assert.Equal(9, commands.Length);
        foreach (var recorded in commands.Take(8))
        {
            var raw = recorded.GetProperty("rawCommand").Clone();
            var player = recorded.GetProperty("playerId").GetString()!;
            var intent = recorded.GetProperty("clientIntentId").GetString()!;
            var command = GameCommandJsonMapper.Map(raw);
            current = command switch
            {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(player, intent, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(player, intent, raw, default),
                _ => await session.SubmitAsync(player, intent, command, raw, default)
            };
            Assert.True(current.Accepted, current.ErrorMessage);
        }
        return new Setup(initial, session, engine, journal, current!,
            commands[^1].GetProperty("playerId").GetString()!, commands[^1].GetProperty("rawCommand").Clone());
    }

    private static async Task AssertColdOriginalReplay(Setup setup, MatchState state)
    {
        var frame = Frame(setup, state);
        Assert.Empty(StructuralErrors(frame));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        var restoredFrame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
        var writes = new Journal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, new Store(restoredFrame));
        var restored = await registry.GetOrCreateAsync(state.RoomId, default);
        foreach (var player in state.Seats.Keys)
            Assert.Equal(JsonSerializer.Serialize(setup.Session.SnapshotFor(player), Web),
                JsonSerializer.Serialize(restored.SnapshotFor(player), Web));
        Assert.Empty(writes.Entries);
    }

    private static MatchRecoveryFrame Frame(Setup setup, MatchState state) => new(setup.Initial.RoomId,
        state.Tick, setup.Journal.Entries[^1].CompletedEventSequence,
        setup.Journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
            { RuleInputs = e.RuleInputs }).ToArray(),
        setup.Journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray(),
        new Dictionary<string, RecoveredPlayerView>(), [], state, setup.Initial) { OpeningSeed = setup.Initial.Seed };

    private static IReadOnlyList<string> StructuralErrors(MatchRecoveryFrame frame) => MatchRecoveryValidator.Validate(
        frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events, frame.PlayerViews,
        frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static string JournalHash(Journal journal) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(journal.Entries, Web))));

    private sealed record Setup(MatchState Initial, MatchSession Session, CountingEngine Engine, Journal Journal,
        ResolutionResult CurrentValue, string Actor, JsonElement PlannedPlay)
    { public ResolutionResult Current { get; set; } = CurrentValue; }

    private sealed class CountingEngine : IRuleEngine
    {
        public int Calls { get; private set; }
        public ValueTask<ResolutionResult> ResolveAsync(MatchState state, PlayerIntent intent, GameCommand command, CancellationToken token)
        { Calls++; return new CoreRuleEngine().ResolveAsync(state, intent, command, token); }
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
