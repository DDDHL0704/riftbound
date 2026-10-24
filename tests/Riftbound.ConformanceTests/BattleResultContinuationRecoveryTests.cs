using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattleResultContinuationRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("UNL-114/219", "keep")]
    [InlineData("UNL-114/219", "return")]
    [InlineData("UNL-114/219", "destroy")]
    [InlineData("SFD·123/221", "keep")]
    [InlineData("SFD·123/221", "return")]
    [InlineData("SFD·123/221", "destroy")]
    public async Task CapturedWinnerDrawSurvivesRealResponseRemovalAndColdRestore(string cardNo, string response)
    {
        var run = await Open(cardNo);
        Assert.Equal("P2", run.State.CardObjects["FIELD"].ControllerId);
        Assert.Equal(0, run.State.PlayerScores["P1"]);
        Assert.True(run.State.CardObjects["SOURCE"].IsAttacking);
        Assert.Equal("RESULT_TRIGGERS", run.State.PendingBattleResult!.Stage);
        var removeSource = response != "keep";
        if (removeSource)
        {
            if (response == "return")
                await run.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
            else
            {
                await run.Act("P1", new PassPriorityCommand());
                await run.Act("P2", new PlayCardCommand("BARRAGE1", "OGN·127/298", []));
            }
            await Top();
            if (response == "destroy")
            {
                if (run.State.PriorityPlayerId == "P1") await run.Act("P1", new PassPriorityCommand());
                await run.Act("P2", new PlayCardCommand("BARRAGE2", "OGN·127/298", []));
                await Top();
                Assert.Contains("SOURCE", run.State.PlayerZones["P1"].Graveyard);
            }
            else
            {
                Assert.Contains("SOURCE", run.State.PlayerZones["P1"].Hand);
                var item = Assert.Single(run.State.StackItems);
                Assert.True(item.UnitBattleWinDraw!.Source.Generation < run.State.CardObjects["SOURCE"].ObjectGeneration);
                var publicView = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(run.State)["P2"], Json);
                var skill = publicView.GetProperty("stack").EnumerateArray().Single();
                Assert.Equal("HIDDEN", skill.GetProperty("sourceObjectId").GetString());
                Assert.Equal(cardNo, skill.GetProperty("cardNo").GetString());
            }
            Assert.NotNull(run.State.PendingBattleResult);
            Assert.Single(run.State.StackItems, item => item.UnitBattleWinDraw is not null);
        }
        async Task Top()
        {
            var spell = run.State.StackItems[^1].StackItemId;
            for (var i = 0; run.State.StackItems.Any(s => s.StackItemId == spell); i++)
            {
                Assert.InRange(i, 0, 3);
                await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            }
        }
        for (var i = 0; run.State.PendingBattleResult is not null; i++)
        {
            Assert.InRange(i, 0, 7);
            await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Contains("DRAW", run.State.PlayerZones["P1"].Hand);
        Assert.Empty(run.State.StackItems);
        Assert.Empty(run.State.TriggerQueue);
        Assert.Null(run.State.WinnerPlayerId);
        Assert.Equal(removeSource ? 0 : 1, run.State.PlayerScores["P1"]);
        var events = run.Frame().Events;
        var draw = Assert.Single(events, e => e.Event.Kind == "CARD_DRAWN");
        var closed = Assert.Single(events, e => e.Event.Kind == "BATTLE_CLOSED");
        Assert.True(draw.Sequence < closed.Sequence);
        if (removeSource) Assert.DoesNotContain(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        else Assert.True(draw.Sequence < Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Sequence);
        var battle = Assert.Single(run.State.BattleResolutions, b => b.Kind == "CLOSED");
        Assert.Equal("P1", battle.WinnerPlayerId);
        Assert.Contains("SOURCE", battle.SurvivingAttackerObjectIds);
        run.Save("response-" + cardNo.Replace('/', '-').Replace('·', '-') + "-" + response);
    }

    [Theory]
    [InlineData("SOURCE_GENERATION")]
    [InlineData("CARD_NO")]
    [InlineData("RESULT_TICK")]
    public async Task CoherentlyReprojectedCapturedSkillStillRequiresItsOriginalBirth(string attack)
    {
        var run = await Open("UNL-114/219");
        var item = Assert.Single(run.State.StackItems);
        var context = item.UnitBattleWinDraw!;
        var continuation = run.State.PendingBattleResult!;
        switch (attack)
        {
            case "SOURCE_GENERATION": context = context with { Source = context.Source with { Generation = context.Source.Generation + 1 } }; break;
            case "CARD_NO": context = context with { CardNo = "SFD·123/221" }; item = item with { CardNo = context.CardNo }; break;
            default: context = context with { ResultTick = context.ResultTick - 1 }; continuation = continuation with { ResultTick = context.ResultTick }; break;
        }
        var forged = run.State with { PendingBattleResult = continuation, StackItems = [item with { UnitBattleWinDraw = context }] };
        var frame = run.Frame(forged);
        // Independent current views agree. Only original accepted history
        // authorizes the historical source incarnation, printing and result tick.
        Assert.Empty(frame.ValidationErrors);
        var replayErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.NotEmpty(replayErrors);
        Assert.Contains(replayErrors, e => e.Contains("original", StringComparison.Ordinal));
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new Journal(), new Store(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(frame.RoomId, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        run.Save("coordinated-" + attack, new { forgedFrame = frame, replayErrors, error.Code });
    }

    [Theory]
    [InlineData("NO_CONTEXT")]
    [InlineData("NO_CONTINUATION")]
    [InlineData("ACTOR")]
    [InlineData("ROLE")]
    [InlineData("FIELD")]
    [InlineData("FUTURE")]
    public async Task InvalidCapturedBattleShapesAreRejectedBeforeRestore(string attack)
    {
        var run = await Open("UNL-114/219");
        var item = Assert.Single(run.State.StackItems);
        var c = item.UnitBattleWinDraw!;
        var forged = attack == "NO_CONTINUATION" ? run.State with { PendingBattleResult = null }
            : run.State with { StackItems = [item with { UnitBattleWinDraw = attack switch {
                "NO_CONTEXT" => null,
                "ACTOR" => c with { ControllerId = "P2" },
                "ROLE" => c with { Role = "DEFENDER" },
                "FIELD" => c with { BattlefieldId = "UNKNOWN" },
                _ => c with { ResultTick = run.State.Tick + 1 }
            } }] };
        var frame = run.Frame(forged);
        Assert.NotEmpty(frame.ValidationErrors);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new Journal(), new Store(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(frame.RoomId, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        run.Save("shape-" + attack, new { forgedFrame = frame, error.Code });
    }

    [Theory]
    [InlineData("UNL-114/219")]
    [InlineData("SFD·123/221")]
    public async Task DrawBurnoutNormalVictoryStopsBeforeControlAndBattleClose(string cardNo)
    {
        var run = await Open(cardNo, true);
        for (var i = 0; run.State.Status == MatchStatuses.InProgress; i++)
        {
            Assert.InRange(i, 0, 3);
            await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Equal("P2", run.State.WinnerPlayerId);
        Assert.Equal(8, run.State.PlayerScores["P2"]);
        Assert.Equal(0, run.State.PlayerScores["P1"]);
        Assert.NotNull(run.State.PendingBattleResult);
        Assert.Equal("P2", run.State.CardObjects["FIELD"].ControllerId);
        var events = run.Frame().Events;
        Assert.True(Assert.Single(events, e => e.Event.Kind == "CARD_DRAWN").Sequence
            < Assert.Single(events, e => e.Event.Kind == "MATCH_WON").Sequence);
        Assert.DoesNotContain(events, e => e.Event.Kind is "BATTLE_CLOSED" or "BATTLEFIELD_CONQUERED");
        run.Save("burnout-" + cardNo.Replace('/', '-').Replace('·', '-'));
    }

    private static async Task<Driver> Open(string cardNo, bool burnout = false)
    {
        var run = new Driver(MatchState.Create("BATTLE-RESULT-RECOVERY") with { Seed = 424242 });
        await run.Prepare("battle-result-" + (cardNo == "UNL-114/219" ? "nidalee" : "enforcer") + (burnout ? "-burnout" : ""));
        await run.Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
        for (var i = 0; i < 4 && run.State.FocusPlayerId is not null; i++) await run.Act(run.State.FocusPlayerId, new PassFocusCommand());
        await run.Act("P1", new DeclareBattleCommand("FIELD", ["SOURCE"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.NotNull(run.State.PendingBattleResult);
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
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, journal.Entries[^1].Events, state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }

        internal void Save(string name, object? tamper = null)
        {
            var output = Environment.GetEnvironmentVariable("RIFTBOUND_BATTLE_RESULT_RECOVERY_EVIDENCE");
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
