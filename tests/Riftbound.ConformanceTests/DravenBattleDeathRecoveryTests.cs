using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DravenBattleDeathRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Death = "CAPTURED_DRAVEN_BATTLE_DEATH_OPPONENT_SCORE";

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, false, 7)]
    [InlineData(false, true, 0)]
    [InlineData(false, true, 7)]
    [InlineData(true, false, 0)]
    [InlineData(true, false, 7)]
    [InlineData(true, true, 0)]
    [InlineData(true, true, 7)]
    public async Task EveryOriginalPrefixRestoresOpponentSelectionAndNormalVictory(bool alternate, bool mutual, int score)
    {
        var run = await Open(alternate, mutual, score);
        await run.Settle();
        var scores = run.Events.Where(e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), Death)).ToArray();
        Assert.Equal(mutual && score == 0 ? 2 : 1, scores.Length);
        Assert.Equal(mutual ? "P1" : "P2", scores[0].Payload["playerId"]);
        if (score == 7)
        {
            Assert.Equal(mutual ? "P1" : "P2", run.State.WinnerPlayerId);
            Assert.DoesNotContain(run.Events, e => e.Kind is "BATTLE_CLOSED" or "BATTLEFIELD_CONQUERED");
            Assert.NotNull(run.State.PendingBattleResult);
        }
        else
        {
            Assert.Null(run.State.WinnerPlayerId);
            Assert.Null(run.State.PendingBattleResult);
            Assert.Single(run.Events, e => e.Kind == "BATTLE_CLOSED");
        }
        run.Save($"cold-{alternate}-{mutual}-{score}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterBattleResultConfirmsAfterEarlierDeathAndResolvesFirst(bool attackingDraven)
    {
        var run = await Open(false, false, 0, mixed: true, attackingDraven: attackingDraven);
        // CN 467.3 -> 468.1 -> 468.3: these are successive instructions even
        // though one accepted command performs them. APNAP cannot reverse them.
        Assert.Empty(run.State.TriggerQueue);
        Assert.Equal(Death, run.State.StackItems[0].EffectKind);
        Assert.Equal("CAPTURED_LEGEND_BATTLE_WIN_DRAW", run.State.StackItems[1].EffectKind);
        Assert.Equal(attackingDraven ? "P1" : "P2", run.State.PendingCardChoice!.PlayerId);
        await run.Settle();
        var events = run.Events.ToList();
        var draw = Assert.Single(events, e => e.Kind == "CARD_DRAWN");
        var deathScore = Assert.Single(events, e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), Death));
        Assert.True(events.IndexOf(draw) < events.IndexOf(deathScore));
        Assert.True(events.IndexOf(deathScore) < events.FindIndex(e => e.Kind == "BATTLE_CLOSED"));
        run.Save("mixed-instructions-" + attackingDraven);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecycledDeadIncarnationStaysPrivateWhileItsEarnedSkillRemains(bool alternate)
    {
        var run = await Open(alternate, false, 0);
        await run.Confirm();
        var context = Assert.Single(run.State.StackItems).DravenBattleDeathScore!;
        if (run.State.PriorityPlayerId == "P1") await run.Act("P1", new PassPriorityCommand());
        await run.Act("P2", new PlayCardCommand("DISPOSAL", "UNL-103/219", ["P1-UNIT"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
        var spell = run.State.StackItems[^1].StackItemId;
        for (var i = 0; run.State.StackItems.Any(s => s.StackItemId == spell); i++)
        { Assert.InRange(i, 0, 4); await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand()); }
        Assert.Contains("P1-UNIT", run.State.PlayerZones["P1"].MainDeck);
        Assert.True(run.State.CardObjects["P1-UNIT"].ObjectGeneration > context.Source.Generation);
        Assert.Equal(context, Assert.Single(run.State.StackItems).DravenBattleDeathScore);
        var view = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(run.State)["P2"], Json);
        Assert.Equal("HIDDEN", view.GetProperty("stack").EnumerateArray().Single().GetProperty("sourceObjectId").GetString());
        await run.Settle();
        Assert.Equal(1, run.State.PlayerScores["P2"]);
        run.Save("recycle-private-source-" + alternate);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReplacementChoiceCompletesBeforeTheDeathSkillIsBorn(bool alternate, bool pay)
    {
        var run = await Open(alternate, false, 0, altar: true);
        var request = run.State.PendingRuleChoice!.Request;
        Assert.Equal("P1", request.PlayerId);
        Assert.Empty(run.State.StackItems);
        await run.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "ALTAR:P1-UNIT:FIELD" : "DECLINE"]));
        if (pay)
        {
            Assert.Contains("P1-UNIT", run.State.PlayerZones["P1"].Base);
            Assert.DoesNotContain(run.Events, e => e.Kind == "TRIGGER_QUEUED" && Equals(e.Payload.GetValueOrDefault("effectKind"), Death));
        }
        else
        {
            Assert.Contains("P1-UNIT", run.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(run.State.Tick, Assert.Single(run.State.StackItems).DravenBattleDeathScore!.DeathTick);
            await run.Settle();
            Assert.Equal(1, run.State.PlayerScores["P2"]);
        }
        run.Save($"altar-{alternate}-{pay}");
    }

    [Theory]
    [InlineData("SELF")]
    [InlineData("UNKNOWN")]
    [InlineData("EMPTY")]
    [InlineData("DUPLICATE")]
    [InlineData("OTHER_CONTROLLER")]
    [InlineData("EARLY_PRIORITY")]
    public async Task InvalidOpponentIntentPreservesBothViewsAndOriginalHistory(string attack)
    {
        var run = await Open(false, false, 0);
        var c = run.State.PendingCardChoice!;
        var ids = attack switch { "SELF" => new[] { "P1" }, "UNKNOWN" => ["P3"], "EMPTY" => [], "DUPLICATE" => ["P2", "P2"], _ => ["P2"] };
        await run.Act(attack == "OTHER_CONTROLLER" ? "P2" : "P1", attack == "EARLY_PRIORITY"
            ? new PassPriorityCommand() : new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, ids), accepted: false);
        await run.Confirm();
        await run.DuplicateLast();
        await run.Settle();
        Assert.Equal(1, run.State.PlayerScores["P2"]);
        run.Save("reject-" + attack);
    }

    [Theory]
    [InlineData("GENERATION")]
    [InlineData("PRINTING")]
    [InlineData("FIELD")]
    [InlineData("ROLE")]
    [InlineData("DEATH_TICK")]
    [InlineData("DEATH_ID")]
    [InlineData("MISSING")]
    [InlineData("SELF_RECIPIENT")]
    [InlineData("UNCONFIRMED_RECIPIENT")]
    [InlineData("COORDINATED_BIRTH")]
    [InlineData("COORDINATED_DEATH")]
    public async Task ForgedCurrentContextsAndOriginalEventsCannotRestore(string attack)
    {
        var run = await Open(false, false, 0);
        await run.Confirm();
        var oldItem = Assert.Single(run.State.StackItems);
        var old = oldItem.DravenBattleDeathScore!;
        var changed = attack switch {
            "PRINTING" => old with { CardNo = "SFD·148a/221" },
            "FIELD" => old with { BattlefieldId = "NOT-THE-BATTLE" },
            "ROLE" => old with { Role = "DEFENDER" },
            "DEATH_TICK" => old with { DeathTick = old.DeathTick - 1 },
            "DEATH_ID" => old with { DeathId = "forged-death" },
            "SELF_RECIPIENT" => old with { OpponentId = "P1" },
            "UNCONFIRMED_RECIPIENT" => old with { OpponentId = null },
            _ => old with { Source = old.Source with { Generation = old.Source.Generation + 1 } }
        };
        var forged = run.State with { StackItems = [oldItem with {
            DravenBattleDeathScore = attack == "MISSING" ? null : changed, CardNo = changed.CardNo,
            StackItemId = $"ordered-draven-death-score-{changed.DeathId}-{changed.Source.ObjectId}" }] };
        var frame = run.Frame(forged);
        if (attack.StartsWith("COORDINATED", StringComparison.Ordinal))
            frame = frame with { Events = frame.Events.Select(e => e.Event.Kind == "TRIGGER_QUEUED"
                    && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), Death)
                ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload)
                    { ["dravenBattleDeathScore"] = changed with { OpponentId = null } } } }
                : attack == "COORDINATED_DEATH" && e.Event.Kind == "UNIT_DESTROYED"
                    && e.Event.Payload.GetValueOrDefault("destroyedUnitFact") is DestroyedUnitFact fact
                ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload)
                    { ["destroyedUnitFact"] = fact with { Generation = fact.Generation + 1 } } } }
                : e).ToArray() };
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.NotEmpty(errors);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new Journal(), new Store(frame));
        var exception = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(frame.RoomId, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, exception.Code);
        run.Save("forged-" + attack, new { frame, errors });
    }

    private static async Task<Driver> Open(bool alternate, bool mutual, int score, bool mixed = false, bool attackingDraven = true, bool altar = false)
    {
        var run = new Driver(MatchState.Create("DRAVEN-DEATH-RECOVERY") with { Seed = 424242 });
        var scenario = "draven-death-" + (altar ? alternate ? "altar-alt" : "altar" : mixed ? attackingDraven ? "mixed-attacker" : "mixed-defender"
            : (alternate ? "alt-" : "") + (mutual ? "mutual-" : "") + (score == 7 ? "winning" : "normal"));
        await run.Prepare(scenario);
        await run.Act("P1", new MoveUnitCommand("P1-UNIT", "BASE", "BATTLEFIELD:FIELD", []));
        await run.Act("P1", new PassFocusCommand());
        await run.Act("P2", new PassFocusCommand());
        await run.Act("P1", new DeclareBattleCommand("FIELD", ["P1-UNIT"], ["P2-UNIT"], ["COMBAT_ASSIGNMENT"]));
        return run;
    }

    private sealed class Driver
    {
        private readonly MatchState initial;
        private readonly Journal journal = new();
        private MatchSession session;
        private readonly List<MatchState> states = [];
        private (string Actor, string Intent, GameCommand Command, JsonElement Raw) last;
        internal MatchState State { get; private set; }
        internal IEnumerable<GameEvent> Events => journal.Entries.SelectMany(e => e.Events);
        internal Driver(MatchState initial) { this.initial = initial; State = initial; session = new(initial, new CoreRuleEngine(), journal); }

        internal async Task Act(string actor, GameCommand command, bool accepted = true)
        {
            var before = JsonSerializer.Serialize(State, Json);
            var snapshots = State.Seats.Keys.ToDictionary(p => p, p => JsonSerializer.Serialize(session.SnapshotFor(p), Json));
            var prompts = State.Seats.Keys.ToDictionary(p => p, p => JsonSerializer.Serialize(session.PromptFor(p), Json));
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, Json);
            command = GameCommandJsonMapper.Map(wire);
            last = (actor, "death-recovery-" + journal.Entries.Count, command, wire);
            var result = await session.SubmitAsync(actor, last.Intent, command, last.Raw, default);
            Assert.True(result.Accepted == accepted, result.ErrorMessage ?? "Unexpected accepted intent");
            if (!accepted)
            {
                Assert.Equal(before, JsonSerializer.Serialize(result.State, Json));
                foreach (var p in State.Seats.Keys)
                {
                    Assert.Equal(snapshots[p], JsonSerializer.Serialize(session.SnapshotFor(p), Json));
                    Assert.Equal(prompts[p], JsonSerializer.Serialize(session.PromptFor(p), Json));
                }
            }
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            State = result.State; states.Add(State);
            await Cold();
        }

        internal async Task Prepare(string scenario)
        {
            await session.EnsurePlayerAsync("P1", default); State = journal.Entries[^1].AuthoritativeState; states.Add(State); await Cold();
            await session.EnsurePlayerAsync("P2", default); State = journal.Entries[^1].AuthoritativeState; states.Add(State); await Cold();
            var result = await session.SeedScenarioAsync("P1", "original-death-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State; states.Add(State); await Cold();
        }

        private async Task Cold()
        {
            var frame = Frame();
            Assert.True(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            var replayEngine = new RecordingEngine();
            var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, replayEngine, default);
            if (errors.Count > 0) Save("failure-" + Guid.NewGuid().ToString("N"), new { errors, replayed = replayEngine.LastState });
            Assert.True(errors.Count == 0, string.Join("; ", errors));
            var restored = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
            foreach (var p in State.Seats.Keys)
            {
                Assert.Equal(JsonSerializer.Serialize(session.SnapshotFor(p), Json), JsonSerializer.Serialize(restored.SnapshotFor(p), Json));
                Assert.Equal(JsonSerializer.Serialize(session.PromptFor(p), Json), JsonSerializer.Serialize(restored.PromptFor(p), Json));
            }
            session = restored;
        }

        internal async Task Confirm()
        {
            var choice = State.PendingCardChoice!;
            Assert.NotNull(choice);
            await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.PlayerId == "P1" ? "P2" : "P1"]));
        }

        internal async Task DuplicateLast()
        {
            var count = journal.Entries.Count; var before = JsonSerializer.Serialize(State, Json);
            var duplicate = await session.SubmitAsync(last.Actor, last.Intent, last.Command, last.Raw, default);
            Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
            Assert.Equal(count, journal.Entries.Count); Assert.Equal(before, JsonSerializer.Serialize(duplicate.State, Json));
        }

        internal async Task Settle()
        {
            for (var i = 0; State.Status == MatchStatuses.InProgress
                && (State.StackItems.Count > 0 || State.TriggerQueue.Count > 0 || State.PendingCardChoice is not null); i++)
            {
                Assert.InRange(i, 0, 24);
                if (State.PendingCardChoice is not null) await Confirm();
                else if (State.TriggerQueue.Count > 0)
                {
                    var actor = State.Seats.Keys.Single(p => session.PromptFor(p).EnabledActions().Contains(CommandTypes.OrderTriggers));
                    var prompt = JsonSerializer.SerializeToElement(session.PromptFor(actor), Json);
                    var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("action").GetString() == CommandTypes.OrderTriggers && c.GetProperty("enabled").GetBoolean());
                    var ids = candidate.GetProperty("metadata").GetProperty("orderedTriggerIds").EnumerateArray().Select(x => x.GetString()!).ToArray();
                    await Act(actor, new OrderTriggersCommand(OrderedTriggerIds: ids));
                }
                else await Act(State.PriorityPlayerId!, new PassPriorityCommand());
            }
        }

        internal MatchRecoveryFrame Frame(MatchState? replacement = null)
        {
            var state = replacement ?? State; var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand?.Clone(),
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, journal.Entries[^1].Events, state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }

        internal void Save(string name, object? tamper = null)
        {
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_DEATH_RECOVERY_EVIDENCE");
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            using var stream = new GZipStream(File.Open(Path.Combine(folder, name + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(stream, new { initial, states, entries = journal.Entries, frame = Frame(),
                originalPrefixReplayAndColdRestoreCount = journal.Entries.Count, tamper }, Json);
        }
    }

    private sealed class RecordingEngine : IRuleEngine
    {
        internal MatchState? LastState { get; private set; }
        public async ValueTask<ResolutionResult> ResolveAsync(MatchState state, PlayerIntent intent, GameCommand command, CancellationToken token)
        {
            var result = await new CoreRuleEngine().ResolveAsync(state, intent, command, token);
            LastState = result.State; return result;
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
