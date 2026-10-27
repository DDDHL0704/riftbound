using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SurrenderResourceProgressTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const string RedRune = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
    private const string BlueRune = "P1-RUNE-BLUE-EXTRA-PAYMENT-001";
    private const string Target = "P2-BULLET-TIME-UNIT-001";

    [Theory]
    [InlineData("UNPAID", "P1")]
    [InlineData("UNPAID", "P2")]
    [InlineData("RESOURCE", "P1")]
    [InlineData("RESOURCE", "P2")]
    [InlineData("DRAFT", "P1")]
    [InlineData("DRAFT", "P2")]
    [InlineData("PAID", "P1")]
    [InlineData("PAID", "P2")]
    public async Task ConcedeRetainsCompletedResourcesWithoutChargingAnUnconfirmedInstruction(string stage, string player)
    {
        // CN430.2/430.2.a/430.3: resources settle while the parent is paused.
        // CN650/651.1: either player may concede at that pause. This is the
        // original finite QA source command, not a legal-deck full-game proof.
        var run = await Run.Open();
        var resourceEntries = new List<MatchJournalEntry>();
        if (stage != "UNPAID")
        {
            await run.Choose("RECYCLE_RUNE:" + RedRune);
            resourceEntries.Add(run.Entries[^1]);
        }
        if (stage == "DRAFT")
        {
            await run.Choose("RECYCLE_RUNE:" + BlueRune);
            resourceEntries.Add(run.Entries[^1]);
            await run.Choose("SPEND_POWER:ALLOCATE");
            for (var step = 0; ; step++)
            {
                Assert.InRange(step, 0, 6);
                var request = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice).Request;
                if (request.Options.Any(o => o.Id == "CONFIRM_INSTRUCTION_POWER_ALLOCATION")) break;
                var choices = request.Options.Where(o => o.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)).ToArray();
                Assert.NotEmpty(choices);
                var amount = choices[0].Id.Contains("%3ATOTAL", StringComparison.Ordinal) ? 2 : 1;
                var option = Assert.Single(choices, o => {
                    var bounds = o.Id.Split(':');
                    return int.Parse(bounds[^2]) <= amount && amount <= int.Parse(bounds[^1]);
                });
                await run.Choose(option.Id);
            }
        }
        if (stage == "PAID") await run.Choose("SPEND_POWER:1");

        var expectedPower = stage == "DRAFT" ? 3 : stage == "RESOURCE" ? 2 : 1;
        var expectedDeckCount = stage == "UNPAID" ? 1 : stage == "DRAFT" ? 3 : 2;
        var expectedDamage = stage == "PAID" ? 1 : 0;
        AssertPool(run.Session.SnapshotFor("P1"), expectedPower, expectedDeckCount);
        var before = run.ColdFrame();
        await run.Cold(before);
        AssertPool(run.Session.SnapshotFor("P1"), expectedPower, expectedDeckCount);
        foreach (var entry in resourceEntries)
        {
            var raw = entry.RawCommand!.Value;
            var count = run.Entries.Count;
            var hash = MatchStateHasher.Hash(run.State);
            var duplicate = await run.Session.SubmitAsync(entry.PlayerId, entry.ClientIntentId, GameCommandJsonMapper.Map(raw), raw, default);
            Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
            Assert.Empty(duplicate.Events);
            Assert.Equal(count, run.Entries.Count);
            Assert.Equal(hash, MatchStateHasher.Hash(duplicate.State));
        }
        var commands = JsonSerializer.Serialize(before.Commands, Json);
        var events = JsonSerializer.Serialize(before.Events, Json);
        var ended = await run.Act(player, new SurrenderCommand());
        Assert.Equal(MatchStatuses.Finished, ended.State.Status);
        Assert.Equal(player == "P1" ? "P2" : "P1", ended.State.WinnerPlayerId);
        Assert.Equal(expectedPower, ended.State.RunePools["P1"].TotalPower);
        Assert.Equal(expectedDeckCount, ended.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(stage != "UNPAID", ended.State.PlayerZones["P1"].RuneDeck.Contains(RedRune));
        Assert.Equal(stage == "UNPAID", ended.State.PlayerZones["P1"].Base.Contains(RedRune));
        Assert.Equal(expectedDamage, ended.State.CardObjects[Target].Damage);
        AssertPool(ended.Snapshots["P1"], expectedPower, expectedDeckCount);

        var allEvents = run.Events();
        var recycled = allEvents.Where(e => e.Event.Kind == "RUNE_RECYCLED").ToArray();
        Assert.Equal(resourceEntries.Count, recycled.Length);
        foreach (var entry in resourceEntries)
            Assert.Single(recycled, e => e.Sequence > entry.StartedEventSequence && e.Sequence <= entry.CompletedEventSequence);
        var instructionPayments = allEvents.Where(e => e.Event.Kind == "COST_PAID"
            && Text(e.Event, "paymentWindow") == "INSTRUCTION_POWER_PAYMENT").ToArray();
        Assert.Equal(stage == "PAID" ? 1 : 0, instructionPayments.Length);
        Assert.Equal("MATCH_WON", Assert.Single(ended.Events).Kind);
        Assert.Equal(commands, JsonSerializer.Serialize(run.ColdFrame().Commands.Take(before.Commands.Count), Json));
        Assert.Equal(events, JsonSerializer.Serialize(run.ColdFrame().Events.Take(before.Events.Count), Json));

        var after = run.ColdFrame();
        var countAfter = run.Entries.Count;
        var surrender = run.Entries[^1];
        var duplicateEnd = await run.Session.SubmitAsync(player, surrender.ClientIntentId, new SurrenderCommand(), surrender.RawCommand, default);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ended, Json), JsonSerializer.SerializeToNode(duplicateEnd, Json)));
        Assert.Equal(countAfter, run.Entries.Count);
        await run.Cold(after);
        var recoveredDuplicate = await run.Session.SubmitAsync(player, surrender.ClientIntentId, new SurrenderCommand(), surrender.RawCommand, default);
        Assert.True(recoveredDuplicate.Accepted); Assert.Empty(recoveredDuplicate.Events);
        Assert.Equal(MatchStateHasher.Hash(after.AuthoritativeState!), MatchStateHasher.Hash(recoveredDuplicate.State));
        Assert.Equal(countAfter, run.Entries.Count);

        if (stage == "RESOURCE")
        {
            // Coherent views cannot turn the completed resource operation back
            // into its old origin balance while keeping its original history.
            var pools = new Dictionary<string, RunePool>(ended.State.RunePools) { ["P1"] = ended.State.RunePools["P1"] with {
                Power = 0, PowerByTrait = new Dictionary<string, int> { [RuneTrait.Red] = 1 } } };
            var forged = run.ColdFrame(ended.State with { RunePools = pools });
            Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default));
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, new Store(forged));
            var rejection = await Assert.ThrowsAsync<MatchSessionException>(async () => await registry.GetOrCreateAsync(forged.RoomId, default));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, rejection.Code);
            Assert.Equal(commands, JsonSerializer.Serialize(forged.Commands.Take(before.Commands.Count), Json));
        }
    }

    private static string? Text(GameEvent e, string key)
        => JsonSerializer.SerializeToElement(e.Payload, Json).TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static void AssertPool(SnapshotDto snapshot, int power, int deckCount)
    {
        var player = JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("players").GetProperty("P1");
        Assert.Equal(power, player.GetProperty("runePool").GetProperty("power").GetInt32());
        Assert.Equal(deckCount, player.GetProperty("zones").GetProperty("runeDeckCount").GetInt32());
    }

    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private sealed class Run : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        internal IMatchSession Session { get; private set; } = null!;
        internal MatchState State => Entries[^1].AuthoritativeState;
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
        internal static async Task<Run> Open()
        {
            var run = new Run();
            var session = new MatchSession("finite-resource-surrender", new CoreRuleEngine(), run);
            run.Session = session;
            await session.EnsurePlayerAsync("P1", default); await session.EnsurePlayerAsync("P2", default);
            var seeded = await session.SeedScenarioAsync("P1", "resource-surrender-source", "typed-power-payment-mixed-recycle",
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "typed-power-payment-mixed-recycle" }), default);
            Assert.True(seeded.Accepted, seeded.ErrorMessage);
            await run.Act("P1", new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", ["N54-SELECTED-FIELD"]));
            await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            return run;
        }
        internal Task<ResolutionResult> Choose(string option)
        {
            var request = Assert.IsType<RuleChoiceContinuation>(State.PendingRuleChoice).Request;
            return Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        internal async Task<ResolutionResult> Act(string player, GameCommand command)
        {
            var fields = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            fields["promptId"] = Session.PromptFor(player).PromptId; fields["snapshotTick"] = State.Tick;
            var result = await Session.SubmitAsync(player, "resource-surrender-" + Entries.Count, command, JsonSerializer.SerializeToElement(fields, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            return result;
        }
        internal RecoveredEvent[] Events() => Entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        internal MatchRecoveryFrame ColdFrame(MatchState? state = null)
        {
            var last = Entries[^1]; state ??= last.AuthoritativeState;
            var commands = Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs, ErrorCode = e.ErrorCode }).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = snapshots.ToDictionary(e => e.Key, e => new RecoveredPlayerView(e.Key, state.Tick, last.CompletedEventSequence,
                e.Value, state.Tick, last.CompletedEventSequence, prompts[e.Key]));
            var frame = new MatchRecoveryFrame(last.RoomId, state.Tick, last.CompletedEventSequence, commands, Events(), views, [],
                state, MatchState.Create(last.RoomId) with { Seed = Entries[0].OpeningSeed }) { OpeningSeed = Entries[0].OpeningSeed };
            return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        }
        internal async Task Cold(MatchRecoveryFrame frame)
        {
            Assert.Empty(MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
                frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick));
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), this, new Store(frame));
            Session = await registry.GetOrCreateAsync(frame.RoomId, default);
            foreach (var view in frame.PlayerViews)
            {
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(view.Value.Snapshot, Json), JsonSerializer.SerializeToNode(Session.SnapshotFor(view.Key), Json)));
                Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(view.Value.Prompt, Json), JsonSerializer.SerializeToNode(Session.PromptFor(view.Key), Json)));
            }
        }
    }
}
