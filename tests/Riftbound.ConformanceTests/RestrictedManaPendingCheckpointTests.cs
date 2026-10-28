using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// The original SQL acceptance failure occurs after this real resource action,
// while the enclosing instruction remains pending. This is a native QA birth,
// not a SQL, network, process-restart or ordinary-deck acceptance claim.
public sealed class RestrictedManaPendingCheckpointTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("ogs", "OGS·014/024")]
    [InlineData("sp6", "VEN·SP6")]
    public async Task OriginalBoardAndConfirmedChildStayDistinctAcrossColdReplay(string kind, string card)
    {
        var room = "restricted-pending-checkpoint-" + kind;
        var journal = new Journal();
        var session = new MatchSession(room, new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await Cold();
        await session.EnsurePlayerAsync("P2", default); await Cold();
        var scenario = "restricted-mana-lux-" + kind;
        var seeded = await session.SeedScenarioAsync("P1", "pending-qa-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage); await Cold();
        Assert.Equal(card, State().CardObjects["QA-LUX"].CardNo);
        await Act("P1", new ActivateAbilityCommand("QA-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await Act("P1", new PlayCardCommand("QA-FIRST", "OGN·268/298", ["QA-FIELD"]));
        await Act("P1", new PassPriorityCommand());
        await Act("P2", new ActivateAbilityCommand("QA-PEER-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await Act("P2", new PassPriorityCommand());
        var before = Frame(State());
        Assert.Equal(1, before.AuthoritativeState!.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(1, before.AuthoritativeState.PendingRuleChoice!.Origin.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(1, before.AuthoritativeState.PendingRuleChoice.Request.Preview!.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(2, before.Events.Count(e => e.Event.Kind == "MANA_GAINED"));
        var request = State().PendingRuleChoice!.Request;
        var resource = request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-SECOND-LUX:", StringComparison.Ordinal)).Id;
        await Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [resource]));
        var after = Frame(State());
        var pending = Assert.IsType<RuleChoiceContinuation>(after.AuthoritativeState!.PendingRuleChoice);
        Assert.Single(pending.Answers);
        Assert.Equal(1, pending.Origin.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(3, after.AuthoritativeState.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(3, pending.Request.Preview!.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(2, after.AuthoritativeState.RunePools["P2"].SpellOnlyMana);
        Assert.True(after.AuthoritativeState.CardObjects["QA-SECOND-LUX"].IsExhausted);
        var child = after.Commands[^1];
        Assert.Equal(CommandTypes.PayCost, child.CommandType);
        Assert.Equal(2, after.Events.Count(e => e.Sequence <= child.StartedEventSequence && e.Event.Kind == "MANA_GAINED"));
        Assert.Equal(1, after.Events.Count(e => e.Sequence > child.StartedEventSequence
            && e.Sequence <= child.CompletedEventSequence && e.Event.Kind == "MANA_GAINED"));
        Assert.Equal(3, after.Events.Count(e => e.Event.Kind == "MANA_GAINED"));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(after, new CoreRuleEngine(), default));
        var originalHistoryHash = MatchStateHasher.HashValue(after.Commands);
        var alteredOrigin = pending.Origin with { RunePools = new Dictionary<string, RunePool>(pending.Origin.RunePools) {
            ["P1"] = pending.Origin.RunePools["P1"] with { SpellOnlyMana = 2 } } };
        var forged = after.AuthoritativeState with { PendingRuleChoice = pending with { Origin = alteredOrigin } };
        var rejected = await MatchActionLogReplayer.VerifyFinalStateAsync(after.ReplayInitialState!, after.Commands, forged,
            new CoreRuleEngine(), default, after.Events);
        Assert.False(rejected.IsMatch);
        Assert.Equal(originalHistoryHash, MatchStateHasher.HashValue(after.Commands));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_S3_PENDING_EVIDENCE") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "native-pending-" + kind + ".json"), JsonSerializer.Serialize(new {
                scope = "Fresh native QA birth and cold replay; no SQL, network, process restart or full L3 claim.", before, after, rejected }, Json));
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = State().Tick;
            var wire = JsonSerializer.SerializeToElement(raw); var id = "pending-" + journal.Entries.Count;
            var result = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(result.Accepted, result.ErrorMessage); FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            await Cold();
            var count = journal.Entries.Count;
            var duplicate = await session.SubmitAsync(player, id, command, wire, default);
            Assert.True(duplicate.Accepted, duplicate.ErrorMessage); Assert.Equal(count, journal.Entries.Count);
        }
        MatchRecoveryFrame Frame(MatchState state)
        {
            var last = journal.Entries[^1];
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
                new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, last.CompletedEventSequence,
                snapshots[id], state.Tick, last.CompletedEventSequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(room, state.Tick, last.CompletedEventSequence, [], state);
            var errors = MatchRecoveryValidator.Validate(room, last.CompletedEventSequence, commands, events, views, state, state.Tick, spectator);
            var initial = MatchState.Create(room) with { Seed = journal.Entries[0].OpeningSeed };
            return new(room, state.Tick, last.CompletedEventSequence, commands, events, views, errors, state, initial, spectator)
                { OpeningSeed = journal.Entries[0].OpeningSeed };
        }
        async Task Cold()
        {
            var frame = Frame(State()); Assert.Empty(frame.ValidationErrors);
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
            session = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
        }
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
