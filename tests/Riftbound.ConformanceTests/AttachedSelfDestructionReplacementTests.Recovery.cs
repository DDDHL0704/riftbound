using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    [Fact]
    public async Task OriginalMultiGuardianPendingJsonColdRestoresAndCommitsOnlyTheChosenBoundSource()
    {
        var (initial, run) = await OpenTwoGuardians(); var frame = GuardianFrame(initial, run);
        Assert.Empty(GuardianErrors(frame));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var cold = MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in new[] { "P1", "P2" }) {
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State)[player], Json), JsonSerializer.Serialize(cold.SnapshotFor(player), Json));
            Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildPrompts(run.State)[player], Json), JsonSerializer.Serialize(cold.PromptFor(player), Json));
        }
        var request = run.State.PendingRuleChoice!.Request; var option = request.Options.Single(o => o.ObjectIds!.Contains("ANGEL-2"));
        await run.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));
        var accepted = run.Commands()[^1]; var raw = accepted.RawCommand!.Value;
        var resumed = await cold.SubmitAsync(accepted.PlayerId, accepted.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(resumed.Accepted, resumed.ErrorMessage); AssertHostRecalled(run, "P1");
        Assert.Equal(MatchStateHasher.Hash(run.State), MatchStateHasher.Hash(resumed.State));
        Assert.Contains("ANGEL-2", run.State.PlayerZones["P1"].Graveyard); Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Base);
        var duplicate = await cold.SubmitAsync(accepted.PlayerId, accepted.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(resumed.State), MatchStateHasher.Hash(duplicate.State));
        AssertHostEventSource(run, "P1"); Export(initial, run, true, "pending-json-cold-bound-source");
    }

    [Fact]
    public async Task PendingGuardianChoicesCannotForgeTheOriginalOptionsLabelsOrCapturedBinding()
    {
        var (initial, run) = await OpenTwoGuardians(); var p = run.State.PendingRuleChoice!; var first = p.Request.Options[0];
        var variants = new[] {
            p with { Request = p.Request with { Options = [first with { Id = first.Id + ":WRONG-GENERATION" }, ..p.Request.Options.Skip(1)] } },
            p with { Request = p.Request with { Options = [first with { Label = first.Label + " FORGED" }, ..p.Request.Options.Skip(1)] } },
            p with { Request = p.Request with { PlayerId = "P2" } },
            p with { Request = p.Request with { Options = [..p.Request.Options, first] } },
            p with { Origin = p.Origin with { CardObjects = new Dictionary<string, CardObjectState>(p.Origin.CardObjects) {
                ["ANGEL"] = p.Origin.CardObjects["ANGEL"] with { ObjectGeneration = p.Origin.CardObjects["ANGEL"].ObjectGeneration + 1 } } } },
            p with { Origin = p.Origin with { CardObjects = new Dictionary<string, CardObjectState>(p.Origin.CardObjects) {
                ["ANGEL"] = p.Origin.CardObjects["ANGEL"] with { AttachedToObjectId = "ANGEL-2" } } } },
            p with { Origin = p.Origin with { CardObjects = new Dictionary<string, CardObjectState>(p.Origin.CardObjects) {
                ["ANGEL"] = p.Origin.CardObjects["ANGEL"] with { AttachmentRevision = 1 } } } }
        };
        foreach (var pending in variants) {
            var state = run.State with { PendingRuleChoice = pending };
            var roundTrip = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, Json), Json)!;
            Assert.Contains("invalid replacement choice continuation", OfficialInsightAndSpellLockTests.Errors(roundTrip));
        }
        await Choose(run, first.Id); AssertHostRecalled(run, "P1"); await run.Replay();
        Export(initial, run, true, "pending-structural-binding-forgeries");
    }

    [Fact]
    public async Task AShapeValidGuardianGenerationForgeryStillFailsTheUnchangedOriginalJournal()
    {
        var (initial, run) = await OpenTwoGuardians(); var p = run.State.PendingRuleChoice!;
        var changed = run.State.CardObjects["ANGEL"] with { ObjectGeneration = run.State.CardObjects["ANGEL"].ObjectGeneration + 1 };
        var cards = new Dictionary<string, CardObjectState>(run.State.CardObjects) { ["ANGEL"] = changed };
        var forged = run.State with { CardObjects = cards, PendingRuleChoice = p with { Origin = p.Origin with {
            CardObjects = new Dictionary<string, CardObjectState>(p.Origin.CardObjects) { ["ANGEL"] = changed } } } };
        var frame = GuardianFrame(initial, run, forged); Assert.Empty(GuardianErrors(frame));
        var originalCommands = JsonSerializer.Serialize(run.Commands(), Json);
        var genuine = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, run.Commands(), run.State, new CoreRuleEngine(), default, run.Events());
        Assert.True(genuine.IsMatch, string.Join("; ", genuine.Errors));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, new CoreRuleEngine(), default, frame.Events);
        Assert.False(replay.IsMatch); Assert.Equal(originalCommands, JsonSerializer.Serialize(frame.Commands, Json));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C9_ATTACHED_REPLACEMENT_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "coherent-guardian-generation-forgery.json"), JsonSerializer.Serialize(new { frame, replay, originalCommandsNeverRewritten = true }, Json));
        }
        await Choose(run, run.State.PendingRuleChoice!.Request.Options[0].Id); AssertHostRecalled(run, "P1"); await run.Replay();
    }

    private static async Task<(MatchState Initial, Driver Run)> OpenTwoGuardians()
    {
        var initial = AddInitial(Position(), "ANGEL-2", "SFD·051/221", "P1"); var run = new Driver(initial);
        await HostAndAngel(run);
        await run.Act("P1", new PlayCardCommand("ANGEL-2", "SFD·051/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("ANGEL-2", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
        await FundYellow(run, 2); await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        Assert.Equal(2, run.State.PendingRuleChoice!.Request.Options.Count); return (initial, run);
    }
    private static MatchRecoveryFrame GuardianFrame(MatchState initial, Driver run, MatchState? authority = null)
    {
        var state = authority ?? run.State; var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views, [], state, initial, spectator) { OpeningSeed = initial.Seed };
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
    }
    private static IReadOnlyList<string> GuardianErrors(MatchRecoveryFrame frame)
        => MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events, frame.PlayerViews,
            frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame);
}
