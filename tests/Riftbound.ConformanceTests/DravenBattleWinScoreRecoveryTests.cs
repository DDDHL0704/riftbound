using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DravenBattleWinScoreRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Effect = "CAPTURED_DRAVEN_FIRST_BATTLE_WIN_SCORE";

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(false, 0, true)]
    [InlineData(false, 7, false)]
    [InlineData(false, 7, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 0, true)]
    [InlineData(true, 7, false)]
    [InlineData(true, 7, true)]
    public async Task OriginalEarnedScoreAndFirstOccurrenceSurviveEveryColdPrefix(bool alternate, int score, bool returned)
    {
        var run = await Open(alternate, score == 7);
        var earned = Assert.Single(run.State.StackItems).DravenBattleWinScore!;
        var marker = Marker(earned);
        Assert.Contains(marker, run.State.UntilEndOfTurnEffects);
        Assert.Equal(score, run.State.PlayerScores["P1"]);
        Assert.Equal("P2", run.State.CardObjects["FIELD"].ControllerId);
        if (returned)
        {
            await run.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
            var spell = run.State.StackItems[^1].StackItemId;
            for (var i = 0; run.State.StackItems.Any(s => s.StackItemId == spell); i++)
            {
                Assert.InRange(i, 0, 3);
                await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            }
            Assert.Contains("SOURCE", run.State.PlayerZones["P1"].Hand);
            Assert.True(run.State.CardObjects["SOURCE"].ObjectGeneration > earned.Source.Generation);
            Assert.Equal(earned, Assert.Single(run.State.StackItems).DravenBattleWinScore);
            var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(run.State)["P2"], Json);
            var skill = view.GetProperty("stack").EnumerateArray().Single();
            Assert.Equal("HIDDEN", skill.GetProperty("sourceObjectId").GetString());
            Assert.Equal(alternate ? "SFD·148a/221" : "SFD·148/221", skill.GetProperty("cardNo").GetString());
        }
        for (var i = 0; run.State.Status == MatchStatuses.InProgress && run.State.PriorityPlayerId is not null; i++)
        {
            Assert.InRange(i, 0, 5);
            await run.Act(run.State.PriorityPlayerId, new PassPriorityCommand());
        }
        Assert.Equal(score == 7 ? 8 : returned ? 1 : 2, run.State.PlayerScores["P1"]);
        Assert.Equal(score == 7 ? "P1" : null, run.State.WinnerPlayerId);
        Assert.Contains(marker, run.State.UntilEndOfTurnEffects);
        var events = run.Frame().Events;
        Assert.Single(events, e => e.Event.Kind == "SCORE_GAINED"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), Effect));
        Assert.DoesNotContain(events, e => e.Event.Kind == "CARD_DRAWN");
        if (score == 7)
        {
            Assert.NotNull(run.State.PendingBattleResult);
            Assert.DoesNotContain(events, e => e.Event.Kind is "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED");
        }
        else
        {
            Assert.Null(run.State.PendingBattleResult);
            Assert.Single(events, e => e.Event.Kind == "BATTLE_CLOSED");
            Assert.Equal(returned ? 0 : 1, events.Count(e => e.Event.Kind == "BATTLEFIELD_CONQUERED"));
        }
        run.Save($"cold-{alternate}-{score}-{returned}");
    }

    [Theory]
    [InlineData("GENERATION")]
    [InlineData("PRINTING")]
    [InlineData("RESULT_TICK")]
    public async Task CoordinatedScoreContextAndCurrentViewsCannotReplaceOriginalTriggerBirth(string attack)
    {
        var run = await Open(false);
        var item = Assert.Single(run.State.StackItems);
        var old = item.DravenBattleWinScore!;
        var context = attack switch {
            "GENERATION" => old with { Source = old.Source with { Generation = old.Source.Generation + 1 } },
            "PRINTING" => old with { CardNo = "SFD·148a/221" },
            _ => old with { ResultTick = old.ResultTick - 1 }
        };
        var forged = run.State with {
            StackItems = [item with { CardNo = context.CardNo, DravenBattleWinScore = context }],
            UntilEndOfTurnEffects = run.State.UntilEndOfTurnEffects.Select(x => x == Marker(old) ? Marker(context) : x).ToArray(),
            PendingBattleResult = run.State.PendingBattleResult! with { ResultTick = context.ResultTick }
        };
        var frame = run.Frame(forged);
        Assert.Empty(frame.ValidationErrors);
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.Contains(errors, e => e.Contains("original", StringComparison.Ordinal));
        await Refused(frame);
        run.Save("coordinated-" + attack, new { forgedFrame = frame, errors });
    }

    [Theory]
    [InlineData("MISSING_MARKER")]
    [InlineData("WRONG_TURN")]
    [InlineData("MISSING_CONTEXT")]
    [InlineData("TWO_CONTEXTS")]
    public async Task InvalidFirstOccurrenceAndCapturedSkillShapesCannotRestore(string attack)
    {
        var run = await Open(false);
        var item = Assert.Single(run.State.StackItems);
        var c = item.DravenBattleWinScore!;
        var forged = attack == "MISSING_MARKER" ? run.State with {
            UntilEndOfTurnEffects = run.State.UntilEndOfTurnEffects.Where(x => x != Marker(c)).ToArray()
        } : run.State with { StackItems = [attack switch {
            "MISSING_CONTEXT" => item with { DravenBattleWinScore = null },
            "WRONG_TURN" => item with { DravenBattleWinScore = c with { TurnNumber = c.TurnNumber + 1 } },
            _ => item with { UnitBattleWinDraw = new(c.Source, "UNL-114/219", c.ControllerId, c.BattlefieldId, c.ResultTick, c.Role) }
        }] };
        var frame = run.Frame(forged);
        Assert.NotEmpty(frame.ValidationErrors);
        await Refused(frame);
        run.Save("shape-" + attack, new { forgedFrame = frame });
    }

    private static string Marker(DravenBattleWinScoreContext c)
        => $"DRAVEN_FIRST_BATTLE_WIN:{c.TurnNumber}:{c.Source.Generation}:{c.Source.ObjectId}";

    private static async Task Refused(MatchRecoveryFrame frame)
    {
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new Journal(), new Store(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(frame.RoomId, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
    }

    private static async Task<Driver> Open(bool alternate, bool winning = false)
    {
        var run = new Driver(MatchState.Create("DRAVEN-SCORE-RECOVERY") with { Seed = 424242 });
        await run.Prepare("battle-result-draven" + (alternate ? "-alt" : "") + (winning ? "-winning" : ""));
        await run.Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
        for (var i = 0; i < 4 && run.State.FocusPlayerId is not null; i++) await run.Act(run.State.FocusPlayerId, new PassFocusCommand());
        await run.Act("P1", new DeclareBattleCommand("FIELD", ["SOURCE"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.NotNull(Assert.Single(run.State.StackItems).DravenBattleWinScore);
        return run;
    }

    private sealed class Driver
    {
        private readonly MatchState initial;
        private readonly Journal journal = new();
        private MatchSession? session;
        private readonly List<MatchState> states = [];
        internal MatchState State { get; private set; }
        internal Driver(MatchState initial) { this.initial = initial; State = initial; }
        internal async Task Act(string actor, GameCommand command)
        {
            session ??= new(initial, new CoreRuleEngine(), journal);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            var result = await session.SubmitAsync(actor, "battle-result-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            State = result.State; states.Add(State);
            await Cold();
        }

        internal async Task Prepare(string scenario)
        {
            session = new(initial, new CoreRuleEngine(), journal);
            await session.EnsurePlayerAsync("P1", default); State = journal.Entries[^1].AuthoritativeState; states.Add(State); await Cold();
            await session.EnsurePlayerAsync("P2", default); State = journal.Entries[^1].AuthoritativeState; states.Add(State); await Cold();
            var result = await session.SeedScenarioAsync("P1", "original-battle-result-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State; states.Add(State); await Cold();
        }

        private async Task Cold()
        {
            var frame = Frame();
            Assert.True(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
            Assert.True(errors.Count == 0, string.Join("; ", errors));
            var restored = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
            foreach (var player in State.Seats.Keys)
            {
                Assert.Equal(JsonSerializer.Serialize(session!.SnapshotFor(player), Json), JsonSerializer.Serialize(restored.SnapshotFor(player), Json));
                Assert.Equal(JsonSerializer.Serialize(session!.PromptFor(player), Json), JsonSerializer.Serialize(restored.PromptFor(player), Json));
            }
            session = restored;
        }

        internal MatchRecoveryFrame Frame(MatchState? replacement = null)
        {
            var state = replacement ?? State;
            var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, journal.Entries[^1].Events, state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }

        internal void Save(string name, object? tamper = null)
        {
            var output = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_SCORE_RECOVERY_EVIDENCE");
            if (string.IsNullOrWhiteSpace(output)) return;
            Directory.CreateDirectory(output);
            using var stream = new GZipStream(File.Open(Path.Combine(output, name + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(stream, new { initial, states, entries = journal.Entries, frame = Frame(),
                originalPrefixReplayAndColdRestoreCount = journal.Entries.Count, tamper }, Json);
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
