using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Exercise the remote SQL leaf's actual finish function without a database.
// The separate birth suite independently asserts the full original seat log.
public sealed class ImposedMovementAcceptanceFlowTests
{
    [Theory]
    [InlineData("standard-pay")] [InlineData("standard-decline")]
    [InlineData("effect-pay")] [InlineData("effect-decline")]
    public async Task TheActualAcceptanceLeafResumesTheUncommittedRangeAndCompletesTheLaterInstruction(string kind)
    {
        var room = "imposed-leaf-unit-" + kind; var journal = new Journal();
        var session = new MatchSession(room, new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
        var scenario = "imposed-movement-" + kind;
        var seeded = await session.SeedScenarioAsync("P1", "imposed-leaf-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        Assert.Equal(new[] { "ROOM_SEAT", "ROOM_SEAT", "DEV_SEED_SCENARIO:" + scenario }, journal.Entries.Select(e => e.CommandType));
        await Cold();
        if (kind.StartsWith("effect", StringComparison.Ordinal))
        {
            await Act("P1", new PlayCardCommand("QA-SPELL", "UNL-054/219", ["QA-D", "QA-D2"], Destination: "BATTLEFIELD:QA-FIELD2"));
            await Act("P1", new PassPriorityCommand()); await Act("P2", new PassPriorityCommand());
        }
        else await Act("P1", new MoveUnitCommand("QA-A", "BASE", "BATTLEFIELD:QA-FIELD2", SourceObjectIds: ["QA-A", "QA-B"]));
        await Decide(State().PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-LUX:", StringComparison.Ordinal)).Id);
        await Decide(State().PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-STELE:", StringComparison.Ordinal)).Id);
        await Decide(ImposedMovementSqlAcceptance.QuantityOption(State().PendingRuleChoice!.Request, 65));
        ImposedMovementSqlAcceptance.AssertPending(State(), "P1", "P2", kind);
        await ImposedMovementSqlAcceptance.FinishSequence(() => Task.FromResult(State()), "P1", "P2", kind, Act);
        ImposedMovementSqlAcceptance.AssertFinal(State(), "P1", kind);
        Assert.Equal(1, State().RunePools["P1"].Mana);
        Assert.Equal(kind.EndsWith("-pay", StringComparison.Ordinal) ? 64 : 65, State().RunePools["P1"].TotalPower);
        Assert.Equal("BASE", State().ObjectLocations["QA-RUNE"].Zone);
        var conversion = Assert.Single(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.Equal("QA-STELE", conversion.Payload["sourceObjectId"]); Assert.Equal(65, conversion.Payload["generatedPower"]);
        Assert.Single(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "MANA_GAINED");

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        async Task Decide(string option)
        {
            var request = State().PendingRuleChoice!.Request;
            await Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        async Task Act(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = State().Tick;
            var raw = JsonSerializer.SerializeToElement(node); command = GameCommandJsonMapper.Map(raw);
            var id = "imposed-leaf-" + journal.Entries.Count;
            var result = await session.SubmitAsync(player, id, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            await Cold(); var count = journal.Entries.Count;
            Assert.True((await session.SubmitAsync(player, id, command, raw, default)).Accepted);
            Assert.Equal(count, journal.Entries.Count);
        }
        async Task Cold()
        {
            var last = journal.Entries[^1]; var state = State();
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, last.CompletedEventSequence,
                snapshots[id], state.Tick, last.CompletedEventSequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(room, state.Tick, last.CompletedEventSequence, [], state);
            var errors = MatchRecoveryValidator.Validate(room, last.CompletedEventSequence, commands, events, views, state, state.Tick, spectator);
            Assert.Empty(errors);
            var initial = MatchState.Create(room) with { Seed = journal.Entries[0].OpeningSeed };
            var frame = new MatchRecoveryFrame(room, state.Tick, last.CompletedEventSequence, commands, events, views, errors, state, initial, spectator)
                { OpeningSeed = journal.Entries[0].OpeningSeed };
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
            session = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
        }
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
