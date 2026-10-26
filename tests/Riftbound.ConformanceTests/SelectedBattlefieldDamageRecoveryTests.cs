using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SelectedBattlefieldDamageRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> Responses()
    {
        foreach (var card in new[] { "OGN·268/298", "OGN·250/298", "UNL-072/219" })
        foreach (var gate in new[] { false, true })
        foreach (var returned in new[] { false, true }) yield return [card, gate, returned];
    }

    [Theory]
    [MemberData(nameof(Responses))]
    public async Task ActualResponsesAndColdPrefixesRetainChosenFieldAndCurrentRecipients(string card, bool gate, bool returned)
    {
        var run = await Open(card, gate);
        await run.Act("P1", Play(card));
        var original = Assert.Single(run.State.StackItems);
        Assert.Equal(Targets(card, "A"), original.TargetObjectIds);
        if (returned)
        {
            var actor = card == "OGN·250/298" ? "P1" : "P2";
            if (actor == "P2") await run.Act("P1", new PassPriorityCommand());
            await run.Act(actor, new PlayCardCommand(actor + "-RETURN", "OGN·104/298",
                [card == "OGN·250/298" ? "P1-BASE" : "P2-A1"]));
            var response = run.State.StackItems[^1].StackItemId;
            await run.Resolve(response);
            var target = card == "OGN·250/298" ? "P1-BASE" : "P2-A1";
            Assert.Contains(target, run.State.PlayerZones[actor].Hand);
            Assert.True(run.State.CardObjects[target].ObjectGeneration > 0);
            Assert.Equal(original.TargetObjectIds, Assert.Single(run.State.StackItems).TargetObjectIds);
            Assert.Equal(original.TargetGenerations, Assert.Single(run.State.StackItems).TargetGenerations);
        }
        await run.Resolve(original.StackItemId);
        var damage = run.Entries.SelectMany(e => e.Events).Where(e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")).ToArray();
        var bonus = 1 + (gate ? 1 : 0); // Annie once, then the recipient's current Void Gate once.
        if (card == "OGN·250/298" && returned)
        {
            Assert.Empty(damage); // A missing selected unit supplies no computable Might, even with bonuses.
            Assert.DoesNotContain(run.Entries.SelectMany(e => e.Events), e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        }
        else
        {
            Assert.Equal(returned ? 1 : 2, damage.Length);
            Assert.DoesNotContain(damage, e => Equals(e.Payload.GetValueOrDefault("targetObjectId"), "P2-B"));
            if (!returned) Assert.Equal((card == "OGN·268/298" ? 3 : 4) + bonus, run.State.CardObjects["P2-A1"].Damage);
            Assert.Equal((card == "UNL-072/219" ? 1 : card == "OGN·268/298" ? 3 : 4) + bonus,
                run.State.CardObjects["P2-A2"].Damage);
            if (card == "OGN·250/298")
            {
                Assert.Equal("A", run.State.ObjectLocations["P1-BASE"].BattlefieldObjectId);
                Assert.True(run.State.BattleState.IsActive);
                Assert.Equal("A", run.State.BattleState.BattlefieldObjectId);
                Assert.Equal("P1", run.State.FocusPlayerId);
            }
        }
        Assert.Equal(0, run.State.CardObjects["P2-B"].Damage);
        Assert.Equal(0, run.State.CardObjects["ANNIE"].Damage);
        Assert.Equal(0, run.State.CardObjects["P2-BASE"].Damage);
        Assert.Empty(run.State.StackItems);
        run.Save($"response-{card}-{gate}-{returned}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroPaidPowerStillModifiesTheDeclaredZeroDamageInstructionOnce(bool gate)
    {
        var run = await Open("OGN·268/298", gate);
        await run.Act("P1", new PlayCardCommand("SPELL", "OGN·268/298", ["A"]));
        var spell = Assert.Single(run.State.StackItems);
        Assert.Equal(0, spell.DamageAmount);
        await run.Resolve(spell.StackItemId, 0);
        Assert.Equal(gate ? 2 : 1, run.State.CardObjects["P2-A1"].Damage);
        Assert.Equal(gate ? 2 : 1, run.State.CardObjects["P2-A2"].Damage);
        Assert.Equal(0, run.State.CardObjects["P2-B"].Damage);
        run.Save($"zero-{gate}");
    }

    [Theory]
    [InlineData("OGN·268/298")]
    [InlineData("OGN·250/298")]
    [InlineData("UNL-072/219")]
    public async Task CoherentRetargetedStateAndViewsCannotReplaceOriginalChosenField(string card)
    {
        var run = await Open(card, false);
        await run.Act("P1", Play(card));
        var item = Assert.Single(run.State.StackItems);
        var targets = Targets(card, "B");
        var tampered = run.State with { StackItems = [item with { TargetObjectIds = targets,
            TargetGenerations = targets.ToDictionary(id => id, id => run.State.CardObjects[id].ObjectGeneration) }] };
        var frame = run.Frame(tampered);
        Assert.True(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Assert.NotEmpty(errors);
        run.Save("retarget-" + card, new { tampered, replayErrors = errors });
    }

    private static string[] Targets(string card, string field) => card switch
    {
        "OGN·250/298" => ["P1-BASE", field],
        "UNL-072/219" => [field, field == "A" ? "P2-A1" : "P2-B"],
        _ => [field]
    };

    private static PlayCardCommand Play(string card) => new("SPELL", card, Targets(card, "A"));

    private static async Task<Driver> Open(string card, bool gate)
    {
        var run = new Driver(MatchState.Create("N54-SELECTED-DAMAGE-RECOVERY") with { Seed = 424242 });
        var branch = card == "OGN·268/298" ? "bullet" : card == "OGN·250/298" ? "drop" : "crescent";
        await run.Prepare("selected-damage-" + branch + (gate ? "-gate" : ""));
        return run;
    }

    private sealed class Driver
    {
        private readonly MatchState initial;
        internal Driver(MatchState initial) { this.initial = initial; State = initial; }
        private readonly Journal journal = new();
        private MatchSession? session;
        internal MatchState State { get; private set; }
        internal IReadOnlyList<MatchJournalEntry> Entries => journal.Entries;
        internal async Task Act(string actor, GameCommand command)
        {
            session ??= new(initial, new CoreRuleEngine(), journal);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = State.Tick;
            var result = await session.SubmitAsync(actor, "selected-recovery-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(raw, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            State = result.State;
            await Cold();
        }
        internal async Task Prepare(string scenario)
        {
            session = new(initial, new CoreRuleEngine(), journal);
            await session.EnsurePlayerAsync("P1", default); State = journal.Entries[^1].AuthoritativeState; await Cold();
            await session.EnsurePlayerAsync("P2", default); State = journal.Entries[^1].AuthoritativeState; await Cold();
            var result = await session.SeedScenarioAsync("P1", "selected-damage-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
            Assert.True(result.Accepted, result.ErrorMessage); State = result.State; await Cold();
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
                Assert.Equal(JsonSerializer.Serialize(session.PromptFor(player), Json), JsonSerializer.Serialize(restored.PromptFor(player), Json));
            }
            session = restored;
        }

        internal async Task Resolve(string id, int instructionPower = 3)
        {
            for (var i = 0; State.StackItems.Any(s => s.StackItemId == id); i++)
            {
                Assert.InRange(i, 0, 4);
                if (State.PendingRuleChoice is { Request.Kind: "INSTRUCTION_POWER_PAYMENT" } payment)
                    await Act(payment.Request.PlayerId, new PayCostCommand(payment.Request.Id, "RULE_REPLACEMENT", ["SPEND_POWER:" + instructionPower]));
                else await Act(State.PriorityPlayerId!, new PassPriorityCommand());
            }
        }
        internal MatchRecoveryFrame Frame(MatchState? replacement = null)
        {
            var state = replacement ?? State; var sequence = journal.Entries[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence,
                snapshots[p], state.Tick, sequence, prompts[p]));
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
                e.RawCommand?.Clone(), e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
                e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(
                e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, journal.Entries[^1].Events, state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }
        internal void Save(string name, object? tamper = null)
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_SELECTED_DAMAGE_RECOVERY_EVIDENCE") is not { Length: > 0 } folder) return;
            Directory.CreateDirectory(folder);
            using var stream = new GZipStream(File.Open(Path.Combine(folder, name.Replace('·', '-').Replace('/', '-') + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(stream, new { initial, entries = Entries, frame = Frame(), prefixReplayAndColdCount = Entries.Count, tamper }, Json);
        }
    }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
