using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattleCleanupRecallHistoryTests
{
    private const string RecallReason = "BATTLE_CLEANUP_ATTACKER_RECALL";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualCleanupRecallKeepsItsCauseThroughCloseColdRecoveryAndOriginalReplay(bool attacking)
    {
        var (initial, run) = await Open(attacking, 0, "positive");
        var h = Assert.Single(run.State.BattleResolutions);
        Assert.Equal("NO_RESULT", h.Kind); Assert.Equal(RecallReason, h.Reason); Assert.Null(h.WinnerPlayerId);
        Assert.Empty(h.SurvivingAttackerObjectIds); Assert.Single(h.SurvivingDefenderObjectIds);
        Assert.Contains(attacking ? "HOST" : "ENEMY", run.State.PlayerZones[attacking ? "P1" : "P2"].Base);
        var noResult = Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_NO_RESULT");
        Assert.Equal(RecallReason, noResult.Event.Payload["reason"]);
        var frame = Frame(initial, run);
        Assert.Empty(Errors(frame));
        var cold = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        var session = MatchSession.Restore(cold, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(run.State)["P1"]),
            MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        await SacredProtectorTests.Replay(initial, run);
    }

    [Fact]
    public async Task ACoherentFormerWinnerCannotInventAnAcceptedCleanupRecall()
    {
        var (initial, run) = await Open(true, 1, "winner-control");
        var original = Assert.Single(run.State.BattleResolutions); Assert.Equal("CLOSED", original.Kind);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_RECALLED_TO_BASE"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), RecallReason));
        var commands = JsonSerializer.Serialize(run.Commands(), Json); var events = JsonSerializer.Serialize(run.Events(), Json);
        var h = original with { Kind = "NO_RESULT", Reason = RecallReason, WinnerPlayerId = null,
            ResolutionId = $"battle-result:{original.Tick}:NO_RESULT:{original.BattlefieldId}:{original.AttackerObjectIds[0]}",
            RelatedEventKinds = original.RelatedEventKinds.Append("BATTLE_NO_RESULT").Distinct().ToArray() };
        var state = run.State with { BattleResolutions = [h] };
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state));
        await Reject(Frame(initial, run, state));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), state,
            new CoreRuleEngine(), default, run.Events());
        Assert.False(replay.IsMatch);
        Assert.Equal(commands, JsonSerializer.Serialize(run.Commands(), Json)); Assert.Equal(events, JsonSerializer.Serialize(run.Events(), Json));
        await SacredProtectorTests.Replay(initial, run);
    }

    [Fact]
    public async Task CoherentEmptyHistoryCannotAuthorizeARealRecallRecord()
    {
        var (initial, run) = await Open(true, 0, "empty-journal");
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(run.State));
        Assert.Empty(Errors(Frame(initial, run)));
        await Reject(Frame(initial, run, eraseJournal: true));
        await SacredProtectorTests.Replay(initial, run);
    }

    [Fact]
    public async Task AResponseReturningAnEarlierRoleUnitCannotAddItToTheLaterCombatHistory()
    {
        var initial = AttackDamageDistributionTests.Spell(SacredProtectorTests.DefenderPosition(1),
            "RESPONSE", "OGN·104/298", "P1");
        var run = SacredProtectorTests.Driver(initial, "returned-companion-history");
        await run.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        if (run.State.FocusPlayerId != "P1") await run.Act(run.State.FocusPlayerId!, new PassFocusCommand());
        await run.Act("P1", new PlayCardCommand("RESPONSE", "OGN·104/298", ["FRIEND"]));
        await run.Top(); await SacredProtectorTests.FinishCombat(run, 0);
        var state = run.State; var h = Assert.Single(state.BattleResolutions);
        Assert.Equal("NO_RESULT", h.Kind); Assert.Equal(RecallReason, h.Reason);
        Assert.Equal(new[] { "ENEMY" }, h.AttackerObjectIds); Assert.Equal(new[] { "HOST" }, h.DefenderObjectIds);
        Assert.Contains("FRIEND", state.PlayerZones["P1"].Hand); Assert.Equal(1, state.CardObjects["FRIEND"].ObjectGeneration);
        Assert.Equal(new[] { "ENEMY", "HOST" }, h.PublicObjectBindings!.Select(b => b.ObjectId).Order().ToArray());
        Assert.Contains(run.Events(), e => e.Event.Kind == "BATTLE_ROLES_ESTABLISHED"
            && JsonSerializer.Serialize(e.Event.Payload, Json).Contains("FRIEND", StringComparison.Ordinal));
        var frame = Frame(initial, run); Assert.Empty(Errors(frame));
        var cold = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        var restored = MatchSession.Restore(cold, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.Equal(MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(state)["P1"]),
            MatchStateHasher.HashValue(restored.SnapshotFor("P1")));
        var originalCommands = JsonSerializer.Serialize(run.Commands(), Json); var originalEvents = JsonSerializer.Serialize(run.Events(), Json);
        foreach (var generation in new long[] { 0, 1 })
        {
            var forged = state with { BattleResolutions = [h with {
                DefenderObjectIds = generation == 1 ? h.DefenderObjectIds.Append("FRIEND").ToArray() : h.DefenderObjectIds,
                PublicObjectBindings = h.PublicObjectBindings!.Append(new ObjectBinding("FRIEND", generation)).ToArray() }] };
            Assert.Contains("invalid captured public battle history reference", MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
            await Reject(Frame(initial, run, forged));
        }
        Assert.Equal(originalCommands, JsonSerializer.Serialize(run.Commands(), Json)); Assert.Equal(originalEvents, JsonSerializer.Serialize(run.Events(), Json));
        await SacredProtectorTests.Replay(initial, run);
    }

    private static async Task Reject(MatchRecoveryFrame frame)
    {
        Assert.Contains(Errors(frame), e => e.Contains("closed cleanup-recall history", StringComparison.Ordinal));
        var replayErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.Contains(replayErrors, e => e.Contains("closed cleanup-recall history", StringComparison.Ordinal));
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance, new Store(frame));
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(frame.RoomId, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Contains("closed cleanup-recall history", error.Message, StringComparison.Ordinal);
    }

    private static async Task<(MatchState Initial, Driver Run)> Open(bool attacking, int friends, string key)
    {
        // Same explicit midgame position and real commands as the original consumer.
        var initial = SacredProtectorTests.Position();
        for (var i = 0; i < friends; i++) initial = SacredProtectorTests.Unit(initial, "FRIEND-" + i, "P1");
        initial = SacredProtectorTests.Unit(initial, "ENEMY", "P2");
        if (attacking) {
            for (var i = 0; i < friends; i++) initial = LocalDestructionRecallTests.At(initial, "FRIEND-" + i, "P1", "BASE");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        }
        else {
            initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "F");
            initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
            initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        }
        var run = SacredProtectorTests.Driver(initial, "recall-history-" + key + "-" + attacking);
        try {
            await run.Act(attacking ? "P1" : "P2", new MoveUnitCommand(attacking ? "HOST" : "ENEMY", "BASE", "BATTLEFIELD:F", [],
                SourceObjectIds: attacking ? new[] { "HOST" }.Concat(Enumerable.Range(0, friends).Select(i => "FRIEND-" + i)).ToArray() : null));
            await SacredProtectorTests.FinishCombat(run, friends == 1 ? 6 : 0);
            return (initial, run);
        }
        finally {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_C12_RECALL_HISTORY_EVIDENCE") is { Length: > 0 } output) {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, key + "-" + attacking + ".json"), JsonSerializer.Serialize(new {
                    initial, final = run.State, commands = run.Commands(), events = run.Events(),
                    errors = Errors(Frame(initial, run)) }, new JsonSerializerOptions(Json) { WriteIndented = true }));
            }
        }
    }

    private static IReadOnlyList<string> Errors(MatchRecoveryFrame f) => MatchRecoveryValidator.Validate(f.RoomId,
        f.LastEventSequence, f.Commands, f.Events, f.PlayerViews, f.AuthoritativeState, f.CurrentTick, f.SpectatorReplayFrame);

    private static MatchRecoveryFrame Frame(MatchState initial, Driver run, MatchState? authority = null, bool eraseJournal = false)
    {
        var state = authority ?? run.State; var commands = eraseJournal ? [] : run.Commands(); var events = eraseJournal ? [] : run.Events();
        var sequence = commands.Length == 0 ? 0 : commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        return new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)) { OpeningSeed = initial.Seed };
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }
}
