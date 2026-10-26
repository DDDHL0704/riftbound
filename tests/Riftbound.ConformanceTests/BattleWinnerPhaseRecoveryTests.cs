using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattleWinnerPhaseRecoveryTests
{
    public BattleWinnerPhaseRecoveryTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        var engine = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={engine.Location}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(engine.Location))).ToLowerInvariant()}; MVID={engine.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ARealEmptyResultWinnerRetainsTheTerminalControlCutoffAcrossJsonColdAndDuplicate()
    {
        var (initial, run) = await WinningConquest(); var frame = Frame(initial, run);
        Assert.Equal(MatchStatuses.Finished, run.State.Status); Assert.Equal("P2", run.State.WinnerPlayerId);
        Assert.Equal("CONTROL_TRIGGERS", run.State.PendingBattleResult!.Stage); Assert.Empty(Errors(frame));
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind is "TRIGGER_QUEUED" or "BATTLE_CLOSED");
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var journal = new ColdJournal(); var cold = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
        foreach (var player in new[] { "P1", "P2" }) {
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)[player], Json), JsonSerializer.Serialize(cold.SnapshotFor(player), Json));
            Assert.False(cold.PromptFor(player).Actionable);
        }
        var last = frame.Commands[^1]; var raw = last.RawCommand!.Value;
        var duplicate = await cold.SubmitAsync(last.PlayerId, last.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(duplicate.State));
        var ended = new EndTurnCommand(); var node = JsonSerializer.SerializeToNode(ended, Json)!.AsObject();
        node["promptId"] = cold.PromptFor("P2").PromptId; node["snapshotTick"] = run.State.Tick;
        var wire = JsonSerializer.SerializeToElement(node);
        var rejected = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await cold.SubmitAsync("P2", "c10-terminal-new-action", ended, wire, default));
        Assert.Equal(ErrorCodes.MatchFinished, rejected.Code); Assert.Empty(journal.Entries);
        Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)["P2"], Json), JsonSerializer.Serialize(cold.SnapshotFor("P2"), Json));
        BattleWinnerPhaseBoundaryTests.Save("terminal-empty-result-cold", initial, run);
    }

    [Fact]
    public async Task EmptyResultTerminalShapesStillBindTheOriginalWinnerTickAndPublicObjects()
    {
        var (initial, run) = await WinningConquest(); var p = run.State.PendingBattleResult!;
        var variants = new[] { p with { WinnerPlayerId = "P1" }, p with { ResultTick = run.State.Tick + 1 },
            p with { PublicObjectBindings = [new ObjectBinding("FORGED-OBJECT", 1)] } };
        foreach (var pending in variants) {
            var forged = run.State with { PendingBattleResult = pending }; var frame = Frame(initial, run, forged);
            Assert.Contains("invalid captured battle result continuation", Errors(frame));
        }
        await run.Replay(); BattleWinnerPhaseBoundaryTests.Save("terminal-empty-result-structural-forgeries", initial, run);
    }

    [Fact]
    public async Task ACoherentlyProjectedEarlierStageCannotReplaceTheOriginalAcceptedEmptyResultJournal()
    {
        var (initial, run) = await WinningConquest(); var forged = run.State with {
            PendingBattleResult = run.State.PendingBattleResult! with { Stage = "RESULT_TRIGGERS" } };
        var frame = Frame(initial, run, forged); Assert.Empty(Errors(frame));
        var originalCommands = JsonSerializer.Serialize(run.Commands(), Json);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch); Assert.Equal(originalCommands, JsonSerializer.Serialize(frame.Commands, Json));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C10_GUARDIAN_COMBAT_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "coherent-terminal-phase-forgery.json"), JsonSerializer.Serialize(new {
                frame, replay, originalHistoryNeverRewritten = true, scope = "Current-shape-consistent reprojected earlier stage; original explicit-initial action log rejects." }, Json));
        }
        await run.Replay();
    }

    private static async Task<(MatchState, Driver)> WinningConquest()
    {
        var (initial, run) = await BattleWinnerPhaseBoundaryTests.Open("final-conquest");
        Assert.Equal(7, run.State.PlayerScores["P2"]); await BattleWinnerPhaseBoundaryTests.PassFocus(run);
        await BattleWinnerPhaseBoundaryTests.Declare(run); await BattleWinnerPhaseBoundaryTests.ChooseGuardian(run);
        Assert.Equal(8, run.State.PlayerScores["P2"]); return (initial, run);
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Driver run, MatchState? authority = null)
    {
        var state = authority ?? run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial, spectator) { OpeningSeed = initial.Seed };
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
    }
    private static IReadOnlyList<string> Errors(MatchRecoveryFrame frame) => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence,
        frame.Commands, frame.Events, frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);

    private sealed class ColdJournal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
