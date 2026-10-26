using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class GuardianPendingChoiceOpeningRecoveryTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(true, "P1")]
    [InlineData(true, "P2")]
    [InlineData(false, "P1")]
    [InlineData(false, "P2")]
    public async Task LegalDoubleGuardianChoiceColdRecoversBeforeEitherPlayerSurrenders(bool rawPresent, string surrenderingPlayer)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "guardian-choice-opening.json")));
        var recipe = fixture.RootElement;
        var run = new Opening(recipe.GetProperty("seed").GetInt64());
        Assert.Equal(MatchStatuses.Seating, run.Initial.Status);
        Assert.Equal(MatchPhases.Room, run.Initial.Phase);
        Assert.Empty(run.Initial.CardObjects);
        var entries = recipe.GetProperty("openingIntents").EnumerateArray().ToArray();
        foreach (var entry in entries)
        {
            var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
            // This is a supported typed/wire equivalence. Before the repair,
            // the accepted raw journal replay captured [] instead of this null.
            if (command is DeclareBattleCommand battle)
                command = battle with { BattlefieldTargetObjectIds = null };
            await run.Submit(entry.GetProperty("playerId").GetString()!,
                entry.GetProperty("clientIntentId").GetString()!, command,
                command is not DeclareBattleCommand || rawPresent);
        }
        Assert.Equal(187, run.Entries.Count);
        var last = run.Entries[^1];
        Assert.Equal(rawPresent, last.RawCommand is not null);
        Assert.Equal(!rawPresent, last.TypedCommand is not null);
        var pending = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
        Assert.Equal("P1", pending.Request.PlayerId);
        Assert.Equal("P2", pending.Intent.PlayerId);
        Assert.Equal("DESTRUCTION_REPLACEMENT", pending.Request.Kind);
        var host = recipe.GetProperty("host").GetString()!;
        var gears = new[] { recipe.GetProperty("gear").GetString()!, recipe.GetProperty("gear2").GetString()! };
        Assert.Equal(2, pending.Request.Options.Count);
        Assert.All(gears, gear => {
            Assert.Contains(gear, run.State.PlayerZones["P1"].Battlefields);
            Assert.Equal(host, run.State.CardObjects[gear].AttachedToObjectId);
            Assert.Contains(pending.Request.Options, option => option.ObjectIds!.SequenceEqual(new[] { host, gear }));
        });
        Assert.Contains(host, run.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, run.State.CardObjects[host].Damage);
        Assert.Equal("P1", run.State.CardObjects[recipe.GetProperty("battlefield").GetString()!].ControllerId);
        Assert.Equal(MatchStateHasher.Hash(pending.Origin),
            MatchStateHasher.Hash(run.State with { Tick = pending.Origin.Tick, PendingRuleChoice = null }));

        var frame = run.ColdFrame();
        AssertNormal(frame);
        var originalCommands = JsonSerializer.Serialize(frame.Commands, Json);
        // The original accepted journal must still reject a shape-valid change
        // to its command checkpoint; no null/[] relaxation of state hashes.
        var alteredCommand = JsonSerializer.SerializeToNode(pending.Command)!.AsObject();
        alteredCommand.Remove("battlefieldTargetObjectIds");
        var forgedState = frame.AuthoritativeState! with {
            PendingRuleChoice = frame.AuthoritativeState.PendingRuleChoice! with {
                Command = JsonSerializer.SerializeToElement(alteredCommand) } };
        var forged = run.ColdFrame(forgedState);
        AssertNormal(forged);
        var rejectedRegistry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, new Store(forged));
        var rejection = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await rejectedRegistry.GetOrCreateAsync(forged.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, rejection.Code);
        Assert.Contains("replayed final state hash", rejection.Message, StringComparison.Ordinal);
        var forgedReplay = new[] { rejection.Message };
        Assert.Equal(originalCommands, JsonSerializer.Serialize(forged.Commands, Json));
        Assert.Equal(entries.Length, run.Entries.Count);

        // The production registry performs the mandatory original action-log
        // replay audit before restoring either this frame or the final frame.
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, new Store(frame));
        var cold = await registry.GetOrCreateAsync(frame.RoomId, default);
        AssertViews(frame, cold);
        Assert.Contains(cold.PromptFor(surrenderingPlayer).Candidates!,
            candidate => candidate.Enabled && candidate.Action == CommandTypes.Surrender);
        var beforeCards = JsonSerializer.SerializeToNode(frame.AuthoritativeState!.CardObjects, Json);
        var beforeScores = JsonSerializer.SerializeToNode(frame.AuthoritativeState.PlayerScores, Json);
        run.Session = cold;
        var ended = await run.Submit(surrenderingPlayer, "pending-guardian-surrender", new SurrenderCommand(), rawPresent);
        Assert.Equal(MatchStatuses.Finished, ended.State.Status);
        Assert.Equal(surrenderingPlayer == "P1" ? "P2" : "P1", ended.State.WinnerPlayerId);
        Assert.Null(ended.State.PendingRuleChoice);
        Assert.True(JsonNode.DeepEquals(beforeCards, JsonSerializer.SerializeToNode(ended.State.CardObjects, Json)));
        Assert.True(JsonNode.DeepEquals(beforeScores, JsonSerializer.SerializeToNode(ended.State.PlayerScores, Json)));
        Assert.Equal("MATCH_WON", Assert.Single(ended.Events).Kind);
        Assert.Equal(entries.Length + 1, run.Entries.Count);
        var surrenderRaw = run.Entries[^1].RawCommand;
        var duplicate = await cold.SubmitAsync(surrenderingPlayer, "pending-guardian-surrender",
            new SurrenderCommand(), surrenderRaw, default);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ended, Json), JsonSerializer.SerializeToNode(duplicate, Json)));
        Assert.Equal(entries.Length + 1, run.Entries.Count);
        var after = run.ColdFrame();
        AssertNormal(after);
        var finalRegistry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, new Store(after));
        AssertViews(after, await finalRegistry.GetOrCreateAsync(after.RoomId, default));
        Assert.Equal(originalCommands, JsonSerializer.Serialize(frame.Commands, Json));

        if (Environment.GetEnvironmentVariable("RIFTBOUND_PENDING_GUARDIAN_RECOVERY_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            await using var file = new FileStream(Path.Combine(folder, $"guardian-{rawPresent}-{surrenderingPlayer}.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
            await JsonSerializer.SerializeAsync(zip, new { frame, after, forgedReplay }, Json);
        }
        output.WriteLine("LEGAL_GUARDIAN_PENDING_RECOVERY " + JsonSerializer.Serialize(new {
            rawPresent, surrenderingPlayer, seed = run.Initial.Seed, pendingCommands = frame.Commands.Count,
            pendingEvents = frame.Events.Count, finalCommands = after.Commands.Count, finalEvents = after.Events.Count,
            ruleInputs = run.State.RuleInputs, originalJournalUnchanged = true,
            scope = "One legal double-Guardian choice and surrender path; not whole-card, SQL, API or L3 certification." }, Json));
    }

    private static IReadOnlyList<string> Errors(MatchRecoveryFrame frame)
        => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private static void AssertNormal(MatchRecoveryFrame frame)
    {
        Assert.Empty(Errors(frame));
        Assert.True(frame.IsConsistent);
    }

    private static void AssertViews(MatchRecoveryFrame frame, IMatchSession session)
    {
        foreach (var view in frame.PlayerViews)
        {
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(view.Value.Snapshot, Json),
                JsonSerializer.SerializeToNode(session.SnapshotFor(view.Key), Json)));
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(view.Value.Prompt, Json),
                JsonSerializer.SerializeToNode(session.PromptFor(view.Key), Json)));
        }
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private sealed class Opening : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        internal MatchState Initial { get; }
        internal MatchState State { get; private set; }
        internal IMatchSession Session { get; set; }
        internal Opening(long seed)
        {
            Initial = MatchReplayInitialStateBuilder.FromSeats("guardian-choice-opening",
                new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = seed };
            State = Initial;
            Session = new MatchSession(Initial, new CoreRuleEngine(), this);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            Entries.Add(entry);
            State = entry.AuthoritativeState;
            return ValueTask.CompletedTask;
        }
        internal async Task<ResolutionResult> Submit(string player, string intent, GameCommand command, bool rawPresent)
        {
            JsonElement? raw = null;
            if (rawPresent)
            {
                var fields = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
                fields["promptId"] = Session.PromptFor(player).PromptId;
                fields["snapshotTick"] = State.Tick;
                raw = JsonSerializer.SerializeToElement(fields, Json);
            }
            var result = command switch {
                SubmitDeckCommand deck => await Session.SubmitDeckAsync(player, intent, deck, raw, default),
                ReadyCommand => await Session.ReadyAsync(player, intent, raw, default),
                _ => await Session.SubmitAsync(player, intent, command, raw, default) };
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            State = result.State;
            return result;
        }
        internal MatchRecoveryFrame ColdFrame(MatchState? state = null)
        {
            state ??= State;
            var commands = Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
                entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
                entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) {
                    TypedCommand = entry.TypedCommand, RuleInputs = entry.RuleInputs }).ToArray();
            var events = Entries.SelectMany(entry => entry.Events.Select((ev, i) =>
                new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, ev))).ToArray();
            var sequence = Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state);
            var prompts = ResolutionResult.BuildPrompts(state);
            var views = Initial.Seats.Keys.ToDictionary(player => player, player =>
                new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(Initial.RoomId, state.Tick, sequence, [], state);
            var frame = new MatchRecoveryFrame(Initial.RoomId, state.Tick, sequence, commands, events, views, [],
                state, Initial, spectator) { OpeningSeed = Initial.Seed };
            return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        }
    }
}
