using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayCardTargetsIngressTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly (string Name, string? Value, string? Extra)[] Malformed = [
        ("missing", null, null), ("null", "null", null), ("object", "{}", null),
        ("number", "1", null), ("boolean", "true", null), ("scalar", "\"TARGET\"", null),
        ("null-element", "[null]", null), ("number-element", "[1]", null),
        ("empty-element", "[\"\"]", null), ("blank-element", "[\"  \"]", null),
        ("original-frontier", "[\"\",\"  \"]", null),
        ("leading-space", "[\" TARGET\"]", null), ("trailing-space", "[\"TARGET \"]", null),
        ("mixed-blank", "[\"TARGET\",\"\"]", null), ("mixed-null", "[\"TARGET\",null]", null),
        ("duplicate-valid-property", "[]", ",\"targetObjectIds\":[]"),
        ("duplicate-last-valid", "null", ",\"targetObjectIds\":[]"),
        ("duplicate-last-invalid", "[]", ",\"targetObjectIds\":null"),
        ("property-whitespace", null, ",\" targetObjectIds \":[]"),
        ("extra-whitespace-property", "[]", ",\" targetObjectIds \":[]")
    ];

    public static IEnumerable<object[]> MalformedCases() => Malformed.Select(x => new object[] { x.Name });

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public async Task MalformedRequiredTargetsNeverReachRulesJournalOrCacheAndTheOriginalIntentCanBeCorrected(string shape)
    {
        var setup = await OpeningPrefix();
        var raw = BadRaw(setup, shape);
        var intentId = "correctable-target-intent-" + shape;
        var before = Observe(setup);
        var result = await setup.Session.SubmitAsync(setup.Actor, intentId,
            GameCommandJsonMapper.Map(raw), raw, default);
        AssertRejectedAtIngress(setup, result, before);

        // Recovery audits rejected rows too. Do not replace that independent
        // check with a successful replay of a cleaned or rewritten command.
        var original = Frame(setup, result.State);
        var rejected = new RecoveredCommand(setup.Actor, "forged-rejected-target-row", CommandTypes.PlayCard, raw,
            result.State.Tick, result.State.Tick, original.LastEventSequence, original.LastEventSequence, false, "rejected");
        Assert.Contains(StructuralErrors(original with { Commands = original.Commands.Append(rejected).ToArray() }),
            e => e.Contains("targetObjectIds", StringComparison.Ordinal));

        var legal = setup.PlannedPlay;
        setup.Current = await setup.Session.SubmitAsync(setup.Actor, intentId,
            GameCommandJsonMapper.Map(legal), legal, default);
        Assert.True(setup.Current.Accepted, setup.Current.ErrorMessage);
        Assert.Equal(before.EngineCalls + 1, setup.Engine.Calls);
        Assert.Equal(9, setup.Journal.Entries.Count);
        Assert.Equal(legal.GetRawText(), setup.Journal.Entries[^1].RawCommand!.Value.GetRawText());
        var after = Observe(setup);
        Assert.Same(setup.Current, await setup.Session.SubmitAsync(setup.Actor, intentId,
            GameCommandJsonMapper.Map(legal), legal, default));
        Assert.Equal(after, Observe(setup));
        await AssertColdOriginalReplay(setup, intentId, legal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidTargetsPrecedeStaleAndFinishedJournalPaths(bool finished)
    {
        var setup = await OpeningPrefix();
        if (finished)
        {
            var command = new SurrenderCommand();
            setup.Current = await setup.Session.SubmitAsync(setup.Actor, "finish-before-invalid-targets", command,
                JsonSerializer.SerializeToElement(command, Web), default);
            Assert.True(setup.Current.Accepted);
            Assert.Equal(MatchStatuses.Finished, setup.Current.State.Status);
        }
        var node = JsonNode.Parse(BadRaw(setup, "original-frontier").GetRawText())!.AsObject();
        node["promptId"] = "expired-target-prompt";
        node["snapshotTick"] = setup.Current.State.Tick - 1;
        var raw = JsonSerializer.SerializeToElement(node);
        var before = Observe(setup);
        var result = await setup.Session.SubmitAsync(setup.Actor, "stale-invalid-targets",
            GameCommandJsonMapper.Map(raw), raw, default);
        AssertRejectedAtIngress(setup, result, before);
        await AssertColdOriginalReplay(setup);
    }

    [Fact]
    public async Task ExistingIntentConflictAndCachedOriginalResponseKeepPrecedence()
    {
        var setup = await OpeningPrefix();
        var original = setup.PlannedPlay;
        setup.Current = await setup.Session.SubmitAsync(setup.Actor, "existing-target-intent",
            GameCommandJsonMapper.Map(original), original, default);
        Assert.True(setup.Current.Accepted, setup.Current.ErrorMessage);
        var before = Observe(setup);
        var bad = BadRaw(setup, "original-frontier");
        var conflict = await setup.Session.SubmitAsync(setup.Actor, "existing-target-intent",
            GameCommandJsonMapper.Map(bad), bad, default);
        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(before, Observe(setup));
        Assert.Same(setup.Current, await setup.Session.SubmitAsync(setup.Actor, "existing-target-intent",
            GameCommandJsonMapper.Map(original), original, default));
        Assert.Equal(before, Observe(setup));
        await AssertColdOriginalReplay(setup, "existing-target-intent", original);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"TARGET\"]")]
    [InlineData("[\"second\",\"FIRST\"]")]
    [InlineData("[\"TARGET\",\"TARGET\"]")]
    public async Task CanonicalArraysKeepOrderCaseAndDuplicatesForTheExistingRules(string value)
    {
        var setup = await OpeningPrefix();
        var raw = Raw(setup, value);
        var expected = raw.GetProperty("targetObjectIds").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var calls = setup.Engine.Calls;
        setup.Current = await setup.Session.SubmitAsync(setup.Actor, "canonical-target-shape",
            GameCommandJsonMapper.Map(raw), raw, default);
        Assert.Equal(calls + 1, setup.Engine.Calls);
        Assert.Equal(expected, Assert.IsType<PlayCardCommand>(setup.Engine.LastCommand).TargetObjectIds);
        Assert.Equal(9, setup.Journal.Entries.Count);
        Assert.Equal(raw.GetRawText(), setup.Journal.Entries[^1].RawCommand!.Value.GetRawText());
        Assert.NotEqual(ErrorCodes.InvalidPayload, setup.Current.ErrorCode);
        if (expected.Length == 0) Assert.True(setup.Current.Accepted, setup.Current.ErrorMessage);
        await AssertColdOriginalReplay(setup);
    }

    [Theory]
    [InlineData("OGN·264/298")]
    [InlineData("VEN·103")]
    public async Task OriginalBlankTargetFrontiersAreRejectedBeforeTheCardSpecificRules(string cardNo)
    {
        // This is an explicit midgame ingress fixture, not a claimed SEATING
        // replay. VEN103 remains an unknown consumer on this independent branch.
        var initial = MidgamePosition(cardNo);
        var journal = new Journal();
        var engine = new CountingEngine();
        var session = new MatchSession(initial, engine, journal);
        var raw = JsonSerializer.SerializeToElement(new {
            cmdType = CommandTypes.PlayCard, sourceObjectId = "TARGET-SPELL", cardNo, targetObjectIds = new[] { "", "  " },
            optionalCosts = Array.Empty<string>(), promptId = session.PromptFor("P1").PromptId, snapshotTick = initial.Tick
        }, Web);
        Assert.Empty(Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(raw)).TargetObjectIds);
        var before = SessionHash(session, initial, journal);
        var result = await session.SubmitAsync("P1", "original-blank-frontier", GameCommandJsonMapper.Map(raw), raw, default);
        WriteEvidence("blank-" + (cardNo.StartsWith("OGN", StringComparison.Ordinal) ? "old-warfare" : "n15"),
            new { initial, raw, result.Accepted, result.ErrorCode, result.State, result.Events, journal.Entries, engine.Calls });
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Same(initial, result.State);
        Assert.Equal(0, engine.Calls);
        Assert.Empty(journal.Entries);
        Assert.Equal(before, SessionHash(session, initial, journal));
    }

    [Fact]
    public async Task OldWarfareStillAcceptsItsCanonicalEmptyTargetsAndPaysTwoManaAndOnePower()
    {
        var initial = MidgamePosition("OGN·264/298");
        var journal = new Journal();
        var engine = new CountingEngine();
        var session = new MatchSession(initial, engine, journal);
        var raw = JsonSerializer.SerializeToElement(new {
            cmdType = CommandTypes.PlayCard, sourceObjectId = "TARGET-SPELL", cardNo = "OGN·264/298", targetObjectIds = Array.Empty<string>(),
            optionalCosts = Array.Empty<string>(), promptId = session.PromptFor("P1").PromptId, snapshotTick = initial.Tick
        }, Web);
        var result = await session.SubmitAsync("P1", "old-warfare-valid-empty", GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(initial.RunePools["P1"].Mana - 2, result.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].Power - 1, result.State.RunePools["P1"].Power);
        Assert.Equal(1, engine.Calls);
        Assert.Equal(raw.GetRawText(), Assert.Single(journal.Entries).RawCommand!.Value.GetRawText());
        Assert.Equal("TARGET-SPELL", Assert.Single(result.State.StackItems).SourceObjectId);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        WriteEvidence("old-warfare-valid-empty", new { initial, result.State, journal.Entries });
    }

    private static MatchState MidgamePosition(string cardNo)
    {
        var initial = AttackDamageDistributionTests.Position();
        return initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = [..initial.PlayerZones["P1"].Hand, "TARGET-SPELL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["TARGET-SPELL"] = new("P1", "HAND") },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["TARGET-SPELL"] = new("TARGET-SPELL", cardNo: cardNo, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) } };
    }

    private static void AssertRejectedAtIngress(Setup setup, ResolutionResult result, Observation before)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Same(setup.Current.State, result.State);
        Assert.Empty(result.Events);
        Assert.Equal(before, Observe(setup));
        Assert.Empty(StructuralErrors(Frame(setup, result.State)));
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }

    private static JsonElement BadRaw(Setup setup, string name)
    {
        var row = Malformed.Single(x => x.Name == name);
        return Raw(setup, row.Value, row.Extra);
    }

    private static JsonElement Raw(Setup setup, string? value, string? extra = null)
    {
        var node = JsonNode.Parse(setup.PlannedPlay.GetRawText())!.AsObject();
        node.Remove("targetObjectIds");
        var text = node.ToJsonString();
        using var document = JsonDocument.Parse(text[..^1] +
            (value is null ? "" : ",\"targetObjectIds\":" + value) + extra + "}");
        return document.RootElement.Clone();
    }

    private static async Task<Setup> OpeningPrefix()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "card-play-history-current-opening-20261017.json")));
        var fixture = document.RootElement;
        var initial = MatchReplayInitialStateBuilder.FromSeats(fixture.GetProperty("sourceRoomId").GetString()!,
            fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        Assert.Equal(0, initial.Seed);
        Assert.Empty(initial.CardObjects);
        var journal = new Journal();
        var engine = new CountingEngine();
        var session = new MatchSession(initial, engine, journal);
        ResolutionResult? current = null;
        var commands = fixture.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        Assert.Equal(9, commands.Length);
        foreach (var row in commands.Take(8))
        {
            var raw = row.GetProperty("rawCommand").Clone();
            var player = row.GetProperty("playerId").GetString()!;
            var intent = row.GetProperty("clientIntentId").GetString()!;
            current = GameCommandJsonMapper.Map(raw) switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(player, intent, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(player, intent, raw, default),
                var command => await session.SubmitAsync(player, intent, command, raw, default)
            };
            Assert.True(current.Accepted, current.ErrorMessage);
        }
        return new(initial, session, engine, journal, current!, commands[^1].GetProperty("playerId").GetString()!,
            commands[^1].GetProperty("rawCommand").Clone());
    }

    private static async Task AssertColdOriginalReplay(Setup setup, string? cachedIntent = null, JsonElement? cachedRaw = null)
    {
        var frame = Frame(setup, setup.Current.State);
        Assert.Empty(StructuralErrors(frame));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        var restoredFrame = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
        var writes = new Journal();
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), writes, new Store(restoredFrame));
        var restored = await registry.GetOrCreateAsync(frame.RoomId, default);
        foreach (var player in frame.AuthoritativeState!.Seats.Keys)
        {
            Assert.Equal(JsonSerializer.Serialize(setup.Session.SnapshotFor(player), Web), JsonSerializer.Serialize(restored.SnapshotFor(player), Web));
            Assert.Equal(JsonSerializer.Serialize(setup.Session.PromptFor(player), Web), JsonSerializer.Serialize(restored.PromptFor(player), Web));
        }
        if (cachedIntent is not null && cachedRaw is { } raw)
        {
            var replay = await restored.SubmitAsync(setup.Actor, cachedIntent, GameCommandJsonMapper.Map(raw), raw, default);
            Assert.True(replay.Accepted, replay.ErrorMessage);
            Assert.Equal(MatchStateHasher.Hash(setup.Current.State), MatchStateHasher.Hash(replay.State));
        }
        Assert.Empty(writes.Entries);
        WriteEvidence("original-journal-" + (cachedIntent ?? setup.Journal.Entries[^1].ClientIntentId), frame);
    }

    private static MatchRecoveryFrame Frame(Setup setup, MatchState state) => new(setup.Initial.RoomId, state.Tick,
        setup.Journal.Entries[^1].CompletedEventSequence,
        setup.Journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs, ErrorCode = e.ErrorCode }).ToArray(),
        setup.Journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray(),
        new Dictionary<string, RecoveredPlayerView>(), [], state, setup.Initial) { OpeningSeed = setup.Initial.Seed };

    private static IReadOnlyList<string> StructuralErrors(MatchRecoveryFrame frame) => MatchRecoveryValidator.Validate(
        frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static Observation Observe(Setup setup) => new(setup.Engine.Calls, SessionHash(setup.Session, setup.Current.State, setup.Journal));
    private sealed record Observation(int EngineCalls, string Hash);

    private static string SessionHash(MatchSession session, MatchState state, Journal journal)
    {
        object? Field(string name) => typeof(MatchSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session);
        return MatchStateHasher.HashValue(new {
            state, journal.Entries, sequence = Field("lastEventSequence"), cache = Field("intentCache"),
            snapshots = state.Seats.Keys.Order().ToDictionary(id => id, session.SnapshotFor),
            prompts = state.Seats.Keys.Order().ToDictionary(id => id, session.PromptFor)
        });
    }

    private static void WriteEvidence(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_PLAY_TARGET_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(value, value.GetType(), Web));
    }

    private sealed record Setup(MatchState Initial, MatchSession Session, CountingEngine Engine, Journal Journal,
        ResolutionResult CurrentValue, string Actor, JsonElement PlannedPlay)
    { public ResolutionResult Current { get; set; } = CurrentValue; }

    private sealed class CountingEngine : IRuleEngine
    {
        public int Calls { get; private set; }
        public GameCommand? LastCommand { get; private set; }
        public ValueTask<ResolutionResult> ResolveAsync(MatchState state, PlayerIntent intent, GameCommand command, CancellationToken token)
        { Calls++; LastCommand = command; return new CoreRuleEngine().ResolveAsync(state, intent, command, token); }
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
