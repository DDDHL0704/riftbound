using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedDestructionReplacementInstanceTests
{
    private static readonly JsonSerializerOptions RecoveryJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task OriginalCopiedPendingTranscriptRoundTripsAndColdContinuesWithExactPrivateViewsAndEvents()
    {
        var (initial, run) = await CopiedFrontier(false);
        var frame = RecoveryFrame(initial, run);
        Assert.Empty(RecoveryErrors(frame));
        var audit = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands,
            frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.True(audit.IsMatch, string.Join("; ", audit.Errors));
        var cold = MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in new[] { "P1", "P2" }) {
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)[player], RecoveryJson), JsonSerializer.Serialize(cold.SnapshotFor(player), RecoveryJson));
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildPrompts(run.State)[player], RecoveryJson), JsonSerializer.Serialize(cold.PromptFor(player), RecoveryJson));
        }
        var pending = run.State.PendingRuleChoice!;
        await AssertRejected(run.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [pending.Request.Options[0].Id]));
        await AssertRejected(run.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["LOCAL_RECALL:BASE-1:S"]));
        await Select(run, CopyChoice(run.State, "BASE-1", "S", "SONG"));
        var accepted = run.Commands()[^1]; var raw = accepted.RawCommand!.Value;
        var resumed = await cold.SubmitAsync(accepted.PlayerId, accepted.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(resumed.Accepted, resumed.ErrorMessage); AssertSaved(run, true);
        Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(resumed.State));
        Assert.Equal(JsonSerializer.Serialize(run.Events().Where(e => e.Sequence > frame.LastEventSequence).Select(e => e.Event), RecoveryJson),
            JsonSerializer.Serialize(resumed.Events, RecoveryJson));
        var duplicate = await cold.SubmitAsync(accepted.PlayerId, accepted.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(resumed.State), MatchStateHasher.Hash(duplicate.State));
        Export("pending-json-cold", initial, run, "SFD·173/221", true);
    }

    [Fact]
    public async Task PendingCopiedOptionsLabelsAnswersAndOriginCannotBeForgedBeforeAValidContinuation()
    {
        var (initial, run) = await CopiedFrontier(false);
        var pending = run.State.PendingRuleChoice!; var request = pending.Request;
        var first = request.Options[0]; var answer = pending.Answers[0];
        var forged = new[] {
            pending with { Request = request with { Options = [first with { Id = first.Id + ":STALE" }, ..request.Options.Skip(1)] } },
            pending with { Request = request with { Options = [first with { Label = first.Label + " FORGED" }, ..request.Options.Skip(1)] } },
            pending with { Request = request with { PlayerId = "P2" } },
            pending with { Request = request with { Options = [..request.Options, first] } },
            pending with { Answers = [answer with { RequestHash = "FORGED" }, ..pending.Answers.Skip(1)] },
            pending with { Answers = [answer with { OptionId = "LOCAL_RECALL:FIELD-1:S:ATTACHED:STALE:1:1" }, ..pending.Answers.Skip(1)] },
            pending with { Origin = pending.Origin with { CardObjects = new Dictionary<string, CardObjectState>(pending.Origin.CardObjects) {
                ["S"] = pending.Origin.CardObjects["S"] with { ObjectGeneration = pending.Origin.CardObjects["S"].ObjectGeneration + 1 } } } }
        };
        foreach (var value in forged) {
            var state = run.State with { PendingRuleChoice = value };
            var roundTrip = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, RecoveryJson), RecoveryJson)!;
            Assert.Contains("invalid replacement choice continuation", OfficialInsightAndSpellLockTests.Errors(roundTrip));
        }
        await Select(run, CopyChoice(run.State, "BASE-1", "S", "SONG")); AssertSaved(run, true); await run.Replay();
        Export("pending-forgery-controls", initial, run, "SFD·173/221", true);
    }

    [Fact]
    public async Task AShapeValidAlternativeInstanceTranscriptStillFailsTheUnchangedOriginalJournal()
    {
        var (initial, original) = await CopiedFrontier(false);
        var (_, alternate) = await CopiedFrontier(true);
        Assert.Equal(original.State.Tick, alternate.State.Tick);
        Assert.NotEqual(MatchStateHasher.Hash(original.State), MatchStateHasher.Hash(alternate.State));
        var commands = JsonSerializer.Serialize(original.Commands(), RecoveryJson);
        var frame = RecoveryFrame(initial, original, alternate.State);
        Assert.Empty(RecoveryErrors(frame));
        var genuine = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, original.Commands(), original.State, new CoreRuleEngine(), default, original.Events());
        Assert.True(genuine.IsMatch, string.Join("; ", genuine.Errors));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch);
        Assert.Equal(commands, JsonSerializer.Serialize(frame.Commands, RecoveryJson));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C8_COPIED_DESTRUCTION_EVIDENCE") is { Length: > 0 } root)
            File.WriteAllText(Path.Combine(root, "coherent-alternate-transcript.json"), JsonSerializer.Serialize(new { frame, replay, originalCommandsNeverRewritten = true }, RecoveryJson));
        await Select(original, CopyChoice(original.State, "BASE-1", "S", "SONG")); AssertSaved(original, true);
        await Select(alternate, "LOCAL_RECALL:BASE-1:S"); AssertSaved(alternate, true);
        await original.Replay(); await alternate.Replay();
    }

    private static async Task<(MatchState Initial, Driver Run)> CopiedFrontier(bool copiedFirst)
    {
        const string no = "SFD·173/221"; var initial = Position(no, true); var run = new Driver(initial);
        await SetUp(run, no, ["SONG"]); await DestroyAll(run);
        await Select(run, copiedFirst ? CopyChoice(run.State, "FIELD-1", "S", "SONG") : "LOCAL_RECALL:FIELD-1:S");
        await Select(run, "GEAR:S:G1");
        Assert.NotNull(run.State.PendingRuleChoice);
        return (initial, run);
    }
    private static MatchRecoveryFrame RecoveryFrame(MatchState initial, Driver run, MatchState? authority = null)
    {
        var state = authority ?? run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial, spectator) { OpeningSeed = initial.Seed };
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, RecoveryJson), RecoveryJson)!;
    }
    private static IReadOnlyList<string> RecoveryErrors(MatchRecoveryFrame frame)
        => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events, frame.PlayerViews,
            frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
}
