using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class TypedCardJournalProjectionAssertions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    internal static async Task Run(string name, MatchState initial, IMatchJournal journal,
        Func<RecoveredCommand[]> commands, Func<RecoveredEvent[]> events, PlayCardCommand cast,
        Action<MatchState, RecoveredEvent[]> assertFinal)
    {
        var tap = new Tap(journal); var session = new MatchSession(initial, new CoreRuleEngine(), tap);
        var state = initial; var frames = new List<MatchRecoveryFrame>();
        async Task Act(string player, GameCommand command)
        {
            var id = "typed-card-" + tap.Entries.Count;
            var result = await session.SubmitAsync(player, id, command, null, default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var rows = commands(); Assert.Equal(tap.Entries.Count, rows.Length);
            for (var i = 0; i < rows.Length; i++)
            {
                Assert.Null(tap.Entries[i].RawCommand); Assert.Null(rows[i].RawCommand);
                Assert.NotNull(tap.Entries[i].TypedCommand);
                Assert.Same(tap.Entries[i].TypedCommand, rows[i].TypedCommand);
                Assert.Equal(tap.Entries[i].RuleInputs, rows[i].RuleInputs);
            }
            var sequence = rows[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
                snapshots[p], state.Tick, sequence, prompts[p]));
            var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, rows, events(), views, [], state, initial,
                MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
            var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
            Assert.Empty(Validate(decoded));
            session = MatchSession.Restore(decoded, new CoreRuleEngine(), tap);
            foreach (var viewer in state.Seats.Keys)
            {
                Assert.Equal(MatchStateHasher.HashValue(snapshots[viewer]), MatchStateHasher.HashValue(session.SnapshotFor(viewer)));
                Assert.Equal(MatchStateHasher.HashValue(prompts[viewer]), MatchStateHasher.HashValue(session.PromptFor(viewer)));
            }
            var count = tap.Entries.Count; var hash = MatchStateHasher.Hash(state);
            var duplicate = await session.SubmitAsync(player, id, command, null, default);
            Assert.True(duplicate.Accepted); Assert.Empty(duplicate.Events); Assert.Equal(count, tap.Entries.Count);
            Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, decoded.Commands, state,
                new CoreRuleEngine(), default, decoded.Events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); frames.Add(decoded);
        }

        await Act("P1", cast);
        var input = Assert.IsType<TypedCommandInput>(commands()[0].TypedCommand);
        Assert.Equal(nameof(PlayCardCommand), input.Kind);
        Assert.Equal("SPELL", input.Value.GetProperty("sourceObjectId").GetString());
        Assert.Equal(cast.CardNo, input.Value.GetProperty("cardNo").GetString());
        Assert.Equal(cast.TargetObjectIds, input.Value.GetProperty("targetObjectIds").EnumerateArray().Select(x => Assert.IsType<string>(x.GetString())).ToArray());
        for (var i = 0; state.StackItems.Count > 0 && i < 16; i++)
            await Act(Assert.IsType<string>(state.PriorityPlayerId), new PassPriorityCommand());
        Assert.Empty(state.StackItems); Assert.Empty(state.TriggerQueue); Assert.Equal(TimingStates.NeutralOpen, state.TimingState);
        assertFinal(state, events());

        var terminal = frames[^1]; var missingRows = terminal.Commands.ToArray();
        missingRows[0] = missingRows[0] with { TypedCommand = null };
        var missing = terminal with { Commands = missingRows };
        var missingErrors = Validate(missing); Assert.NotEmpty(missingErrors);
        var store = new RawEnvelopeFrameStore(missing);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), tap, store);
        var restore = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(state.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, restore.Code);
        Assert.Equal(1, store.Loads);
        var missingReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, missingRows, state,
            new CoreRuleEngine(), default, terminal.Events);
        Assert.False(missingReplay.IsMatch); Assert.NotEmpty(missingReplay.Errors);

        var changedRows = terminal.Commands.ToArray();
        var changedValue = JsonNode.Parse(input.Value.GetRawText())!.AsObject();
        changedValue["targetObjectIds"] = new JsonArray("MISSING-TYPED-TARGET");
        changedRows[0] = changedRows[0] with { TypedCommand = input with { Value = JsonSerializer.SerializeToElement(changedValue, Json) } };
        var changedReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, changedRows, state,
            new CoreRuleEngine(), default, terminal.Events);
        Assert.False(changedReplay.IsMatch); Assert.NotEmpty(changedReplay.Errors);

        if (Environment.GetEnvironmentVariable("RIFTBOUND_TYPED_CARD_PROJECTION_EVIDENCE") is { Length: > 0 } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new
            {
                scope = "Explicit existing midgame fixture; actual typed-only commands and original event replay; no fabricated raw input",
                initial, frames, commands = commands(), events = events(), final = state,
                forgeries = new object[] { new { kind = "missing-input", missing, missingErrors, restoreCode = restore.Code, replay = missingReplay },
                    new { kind = "changed-target", commands = changedRows, replay = changedReplay } },
                runtime = new { baseDirectory = AppContext.BaseDirectory, ruleInputs = RuleInputIdentity.Current,
                    modules = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly,
                        typeof(GameCommand).Assembly, typeof(TypedCardJournalProjectionAssertions).Assembly }.Select(a => new
                        { path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() }) }
            }, Json));
        }
    }

    private static IReadOnlyList<string> Validate(MatchRecoveryFrame frame) => MatchRecoveryValidator.Validate(frame.RoomId,
        frame.LastEventSequence, frame.Commands, frame.Events, frame.PlayerViews, frame.AuthoritativeState,
        frame.CurrentTick, frame.SpectatorReplayFrame);

    private sealed class Tap(IMatchJournal inner) : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            await inner.RecordAsync(entry, token); Entries.Add(entry);
        }
    }
}
