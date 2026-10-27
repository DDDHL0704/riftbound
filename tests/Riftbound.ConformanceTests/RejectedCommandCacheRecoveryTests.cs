using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class RejectedCommandCacheRecoveryTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("stale-ready", ErrorCodes.PromptExpired)]
    [InlineData("invalid-deck", ErrorCodes.InvalidDeck)]
    [InlineData("stale-submit", ErrorCodes.PromptExpired)]
    public async Task OrdinaryRoomKeepsTheOriginalRejectedCodeAfterNativeRegistryRecovery(string branch, string code)
    {
        var run = await Run.Create("rejected-code-" + branch);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        Attempt? rejected = branch switch
        {
            "stale-ready" => await run.Reject(branch, run.Wire(new ReadyCommand(), stale: true), code),
            "invalid-deck" => await run.Reject(branch, run.Wire(command with { MainDeck = [] }), code),
            _ => null
        };
        await run.Act("deck-p1", command);
        await run.Act("deck-p2", command, "P2");
        await run.Act("ready-p1", new ReadyCommand());
        await run.Act("ready-p2", new ReadyCommand(), "P2");
        if (branch == "stale-submit")
            rejected = await run.Reject(branch, run.Wire(new PassPriorityCommand(), stale: true), code);
        await run.VerifyCold(Assert.IsType<Attempt>(rejected));
    }

    [Fact]
    public async Task FiniteQaInstructionKeepsItsRejectedCodeAfterResourceGainContinuationAndNativeRegistryRecovery()
    {
        var run = await Run.Create("rejected-code-finite-qa");
        var seed = await run.Session.SeedScenarioAsync("P1", "fixed-qa-seed", "typed-power-payment-mixed-recycle",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "typed-power-payment-mixed-recycle" }), default);
        Assert.True(seed.Accepted, seed.ErrorMessage);
        await run.Act("play", new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"]));
        await run.Act("pass-1", new PassPriorityCommand(), run.State.PriorityPlayerId!);
        await run.Act("pass-2", new PassPriorityCommand(), run.State.PriorityPlayerId!);
        var pending = run.State.PendingRuleChoice; Assert.NotNull(pending);
        var request = pending.Request;
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", request.Kind);
        var attempt = await run.Reject("invalid-instruction", run.Wire(
            new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:99"])), ErrorCodes.InvalidTarget);
        var before = run.State.RunePools["P1"].TotalPower;
        await run.Act("recycle", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["RECYCLE_RUNE:P1-RUNE-RED-PARTIAL-PAYMENT-001"]));
        request = run.State.PendingRuleChoice!.Request;
        Assert.Equal(before + 1, request.Preview!.RunePools["P1"].TotalPower);
        await run.Act("zero", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:0"]));
        Assert.Null(run.State.PendingRuleChoice);
        Assert.Equal(before + 1, run.State.RunePools["P1"].TotalPower);
        await run.VerifyCold(attempt);
    }

    private sealed record Attempt(string Intent, JsonElement Raw, ResolutionResult Original);

    private sealed class Run(string name) : IMatchJournal
    {
        public IMatchSession Session { get; private set; } = null!;
        private readonly RecordedPlayers players = new();
        private List<MatchJournalEntry> Entries { get; } = [];
        public MatchState State => Entries[^1].AuthoritativeState;

        public static async Task<Run> Create(string name)
        {
            var run = new Run(name);
            run.Session = new MatchSession(name, new CoreRuleEngine(), run, run.players);
            await run.Session.EnsurePlayerAsync("P1", default);
            await run.Session.EnsurePlayerAsync("P2", default);
            Assert.Equal(new[] { "ROOM_SEAT", "ROOM_SEAT" }, run.Entries.Select(e => e.CommandType));
            return run;
        }

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }

        public IReadOnlyList<MatchJournalEntry> JournalEntries => Entries;

        public MatchRecoveryFrame Frame()
        {
            var last = Entries[^1];
            var commands = Entries.Select(entry => JsonSerializer.Deserialize<RecoveredCommand>(JsonSerializer.Serialize(entry, Web), Web)!).ToArray();
            var events = Entries.SelectMany(entry => entry.Events.Select((ev, i) => new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, ev))).ToArray();
            var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key, last.CompletedTick, last.CompletedEventSequence,
                p.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[p.Key]));
            var initial = MatchState.Create(name) with { Seed = Entries[0].OpeningSeed };
            return new(name, last.CompletedTick, last.CompletedEventSequence, commands, events, views, [], last.AuthoritativeState, initial,
                MatchReplayRedactor.BuildSpectatorFrame(name, last.CompletedTick, last.CompletedEventSequence, events.Select(e => e.Event).ToArray(), last.AuthoritativeState))
                { OpeningSeed = Entries[0].OpeningSeed };
        }

        public ValueTask<IMatchSession> Registry(MatchRecoveryFrame frame)
        {
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, new FrameStore(frame), players);
            return registry.GetOrCreateAsync(name, default);
        }

        public JsonElement Wire(GameCommand command, string player = "P1", bool stale = false)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            raw["promptId"] = Session.PromptFor(player).PromptId;
            raw["snapshotTick"] = stale ? 0 : State.Tick;
            return JsonSerializer.SerializeToElement(raw);
        }

        public async Task Act(string intent, GameCommand command, string player = "P1")
        {
            var result = await Send(Session, player, intent, Wire(command, player));
            Assert.True(result.Accepted, result.ErrorMessage);
        }

        public async Task<Attempt> Reject(string intent, JsonElement raw, string expectedCode)
        {
            var count = Entries.Count; var hash = MatchStateHasher.Hash(State);
            var tick = State.Tick; var sequence = Entries[^1].CompletedEventSequence;
            var first = await Send(Session, "P1", intent, raw);
            Assert.False(first.Accepted); Assert.Equal(expectedCode, first.ErrorCode); Assert.Empty(first.Events);
            Assert.Equal(count + 1, Entries.Count); Assert.False(Entries[^1].Accepted);
            Assert.Equal(hash, MatchStateHasher.Hash(first.State)); Assert.Equal(tick, State.Tick);
            Assert.Equal(sequence, Entries[^1].CompletedEventSequence);
            Assert.Equal(ViewHash(ResolutionResult.BuildSnapshots(State)), ViewHash(first.Snapshots));
            Assert.Equal(ViewHash(ResolutionResult.BuildPrompts(State)), ViewHash(first.Prompts));
            var warm = await Send(Session, "P1", intent, raw);
            Assert.False(warm.Accepted); Assert.Equal(first.ErrorCode, warm.ErrorCode); Assert.Equal(first.ErrorMessage, warm.ErrorMessage);
            Assert.Empty(warm.Events); Assert.Equal(count + 1, Entries.Count);
            Assert.Equal(ErrorCodes.ClientIntentConflict, await Conflict(Session, intent, Changed(raw)));
            Assert.Equal(count + 1, Entries.Count);
            var attempt = new Attempt(intent, raw.Clone(), first);
            Save("original-rejection", new { attempt, journal = Entries.ToArray() });
            return attempt;
        }

        public async Task VerifyCold(Attempt attempt)
        {
            var last = Entries[^1]; var journalText = JsonSerializer.Serialize(Entries, Web);
            // Copy the actual serialized journal fields, including any required result metadata.
            // Do not derive or manufacture an error code in the recovery fixture.
            var commands = Entries.Select(entry => JsonSerializer.Deserialize<RecoveredCommand>(
                JsonSerializer.Serialize(entry, Web), Web)!).ToArray();
            var events = Entries.SelectMany(entry => entry.Events.Select((ev, i) => new RecoveredEvent(
                entry.StartedEventSequence + i + 1, entry.CompletedTick, i, ev))).ToArray();
            var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key,
                last.CompletedTick, last.CompletedEventSequence, p.Value, last.CompletedTick,
                last.CompletedEventSequence, last.Prompts[p.Key]));
            var initial = MatchState.Create(name) with { Seed = Entries[0].OpeningSeed };
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(name, last.CompletedTick, last.CompletedEventSequence,
                events.Select(e => e.Event).ToArray(), last.AuthoritativeState);
            var frame = new MatchRecoveryFrame(name, last.CompletedTick, last.CompletedEventSequence, commands, events,
                views, [], last.AuthoritativeState, initial, spectator) { OpeningSeed = Entries[0].OpeningSeed };
            var loaded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Web), Web)!;
            var errors = MatchRecoveryValidator.Validate(name, loaded.LastEventSequence, loaded.Commands, loaded.Events,
                loaded.PlayerViews, loaded.AuthoritativeState, loaded.CurrentTick, loaded.SpectatorReplayFrame);
            Assert.Empty(errors);
            loaded = loaded with { ValidationErrors = errors };
            var auditErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(loaded, new CoreRuleEngine(), default);
            Assert.Empty(auditErrors);
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, loaded.Commands, State, new CoreRuleEngine(), default, loaded.Events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            var store = new FrameStore(loaded);
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, store, players);
            var cold = await registry.GetOrCreateAsync(name, default);
            Assert.Equal(1, store.Loads);
            var count = Entries.Count; var currentHash = MatchStateHasher.Hash(State);
            var result = await Send(cold, "P1", attempt.Intent, attempt.Raw);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(attempt.Original.ErrorMessage, result.ErrorMessage);
            Assert.Equal(count, Entries.Count); Assert.Equal(currentHash, MatchStateHasher.Hash(result.State));
            Assert.Equal(ViewHash(ResolutionResult.BuildSnapshots(State)), ViewHash(result.Snapshots));
            Assert.Equal(ViewHash(ResolutionResult.BuildPrompts(State)), ViewHash(result.Prompts));
            Assert.Equal(ErrorCodes.ClientIntentConflict, await Conflict(cold, attempt.Intent, Changed(attempt.Raw)));
            Assert.Equal(count, Entries.Count);
            var accepted = commands.Last(c => c.Accepted);
            var duplicate = await Send(cold, accepted.PlayerId, accepted.ClientIntentId, accepted.RawCommand!.Value);
            Assert.True(duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(count, Entries.Count);
            Assert.Equal(currentHash, MatchStateHasher.Hash(duplicate.State));
            Assert.Equal(journalText, JsonSerializer.Serialize(Entries, Web));
            Save("cold-code-observation", new { firstCode = attempt.Original.ErrorCode, actualCode = result.ErrorCode,
                exactCodePreserved = attempt.Original.ErrorCode == result.ErrorCode, result, commands, events,
                frame = loaded, structuralErrors = errors, auditErrors, replay,
                originalJournalUnchanged = true, noDuplicateWritesOrDoubleConsumption = true, actualNativeRegistryUsed = true });
            Assert.Equal(attempt.Original.ErrorCode, result.ErrorCode);
        }

        public void Save(string label, object value)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_REJECTED_CACHE_EVIDENCE") is not { Length: > 0 } root) return;
            var path = Path.Combine(root, name); Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, label + ".json"), JsonSerializer.Serialize(value, value.GetType(), Web));
            var assemblies = new[] { typeof(RejectedCommandCacheRecoveryTests).Assembly, typeof(CoreRuleEngine).Assembly,
                typeof(GameCommand).Assembly, typeof(OfficialCardCatalog).Assembly }.Select(a => new { name = a.GetName().Name,
                    path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                    informationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray();
            File.WriteAllText(Path.Combine(path, "runtime.json"), JsonSerializer.Serialize(new { assemblies, ruleInputs = RuleInputIdentity.Current }, Web));
        }
    }

    private static ValueTask<ResolutionResult> Send(IMatchSession session, string player, string intent, JsonElement raw)
    {
        var command = GameCommandJsonMapper.Map(raw);
        return command switch {
            ReadyCommand => session.ReadyAsync(player, intent, raw, default),
            SubmitDeckCommand deck => session.SubmitDeckAsync(player, intent, deck, raw, default),
            _ => session.SubmitAsync(player, intent, command, raw, default)
        };
    }

    private static async Task<string?> Conflict(IMatchSession session, string intent, JsonElement raw)
    {
        try { return (await Send(session, "P1", intent, raw)).ErrorCode; }
        catch (MatchSessionException ex) { return ex.Code; }
    }

    private static JsonElement Changed(JsonElement original)
    {
        var raw = JsonNode.Parse(original.GetRawText())!.AsObject(); raw["probeConflict"] = true;
        return JsonSerializer.SerializeToElement(raw);
    }
    private static string ViewHash(object value)
        => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));

    private sealed class FrameStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public int Loads { get; private set; }
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
        {
            Assert.Equal(frame.RoomId, roomId); Loads++;
            return ValueTask.FromResult<MatchRecoveryFrame?>(frame);
        }
    }
    private sealed class RecordedPlayers : IMatchPlayerStore
    {
        private readonly Dictionary<(string Room, string Player), string> hashes = [];
        public ValueTask SavePlayerSessionAsync(string room, string player, string seat, string hash, CancellationToken token, long openingSeed = 0)
        { hashes[(room, player)] = hash; return ValueTask.CompletedTask; }
        public async ValueTask CommitRoomSeatAsync(MatchJournalEntry entry, string hash, IMatchJournal journal, CancellationToken token)
        { await SavePlayerSessionAsync(entry.RoomId, entry.PlayerId, entry.AuthoritativeState.Seats[entry.PlayerId], hash, token, entry.OpeningSeed); await journal.RecordAsync(entry, token); }
        public ValueTask<bool> HasReconnectTokenHashAsync(string room, string player, string hash, CancellationToken token)
            => ValueTask.FromResult(hashes.TryGetValue((room, player), out var stored) && stored == hash);
    }

}
