using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class EmpowerNativeTests
{
    [Theory]
    [InlineData("reducer", "VEN·001", 0, 2, 0)]
    [InlineData("typed", "VEN·014", 0, 2, 1)]
    [InlineData("ward", "VEN·030", 0, 3, 0)]
    [InlineData("mana", "VEN·074", 0, 1, 0)]
    [InlineData("orange", "VEN·074", 1, 0, 1)]
    [InlineData("mixed", "VEN·074", 0, 1, 0)]
    public async Task ProductionPlayAndAbilityControlsPayThenResolveWithColdRecoveryAndReplay(
        string branch, string cardNo, int alternative, int mana, int power)
    {
        var initial = EmpowerTests.Position(cardNo);
        if (branch is "typed" or "orange") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string,int> { [branch == "typed" ? "red" : "orange"] = 1 }) } };
        if (branch == "reducer") initial = EmpowerTests.Runes(initial, 4);
        if (branch == "mixed") initial = LocalDestructionRecallTests.At(
            OfficialHoldSequenceTests.AddUnit(initial, "OTHER-EMPOWER", "VEN·014", "F", "P1"), "OTHER-EMPOWER", "P1", "BASE");
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_EMPOWER_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var engine = new CoreRuleEngine(); var journal = new Journal();
        var session = new MatchSession(initial, engine, journal); var current = initial;
        var steps = new List<object>(); var native = 0;
        JsonElement activationRaw = default; GameCommand? activationCommand = null;
        void Export(string name, object value) {
            if (dir is null) return;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task Submit(string name, string actor, GameCommand fallback) {
            var prompt = session.PromptFor(actor);
            Export(name + "-prompt.json", prompt); Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[actor]);
            Export(name + "-selection.json", fallback);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), options), options)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            var path = dir is null ? null : Path.Combine(dir, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var doc = JsonDocument.Parse(File.ReadAllText(path)); raw = doc.RootElement.Clone();
                command = GameCommandJsonMapper.Map(raw); native++;
            }
            if (fallback is PlayCardCommand play) Export(name + "-quote.json", engine.PreviewPlayCard(current, actor,
                new("quote", prompt.PromptId!, current.Tick, play)));
            steps.Add(new { name, action = fallback.CmdType });
            var done = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
            var entries = journal.Entries.Count;
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted);
            Assert.Equal(entries, journal.Entries.Count); current = done.State;
            Restore(current); ArmamentMightTests.PublicRecovery(current);
            if (name == "activate") {
                activationRaw = raw; activationCommand = command;
                var stale = await session.SubmitAsync(actor, "stale", command, raw, default);
                Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
            }
        }
        await Submit("play", "P1", new PlayCardCommand("HOST", cardNo, []));
        Assert.False(current.CardObjects["HOST"].IsEmpowered); Assert.Empty(current.StackItems);
        var before = current.RunePools["P1"];
        if (cardNo == "VEN·074") {
            var candidate = Assert.Single(session.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"])
                .Where(row => (string?)row["sourceObjectId"] == "HOST").ToArray();
            Assert.Equal(2, rows.Select(m => m["abilityLabel"]).Distinct().Count());
            Assert.Contains(rows, m => ((string)m["abilityLabel"]!).Contains("1法力"));
            Assert.Contains(rows, m => ((string)m["abilityLabel"]!).Contains("1橙色符能"));
        }
        await Submit("activate", "P1", new ActivateAbilityCommand("HOST", $"EMPOWER:0:{alternative}", [], []));
        Assert.Equal(before.Mana - mana, current.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - power, current.RunePools["P1"].TotalPower);
        Assert.False(current.CardObjects["HOST"].IsEmpowered); Assert.Empty(current.StackItems.Single().TargetObjectIds);
        current = OfficialInsightAndSpellLockTests.Restore(current);
        var previousCommands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
        var previousEvents = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
            e.CompletedTick, i, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(current); var prompts = ResolutionResult.BuildPrompts(current);
        var views = current.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, current.Tick, sequence,
            snapshots[id], current.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(current.RoomId, current.Tick, sequence, [], current);
        var errors = MatchRecoveryValidator.Validate(current.RoomId, sequence, previousCommands, previousEvents, views, current, current.Tick, spectator);
        Assert.Empty(errors);
        session = MatchSession.Restore(new(current.RoomId, current.Tick, sequence, previousCommands, previousEvents, views, errors,
            current, initial, spectator), new CoreRuleEngine(), journal);
        var entriesAfterRestore = journal.Entries.Count;
        Assert.True((await session.SubmitAsync("P1", "activate", activationCommand!, activationRaw, default)).Accepted);
        Assert.Equal(entriesAfterRestore, journal.Entries.Count);
        await Submit("pass-1", current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.False(current.CardObjects["HOST"].IsEmpowered);
        await Submit("pass-2", current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.True(current.CardObjects["HOST"].IsEmpowered); Assert.Empty(current.StackItems);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
            e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Single(commands, c => !c.Accepted);
        if (native > 0) Assert.Equal(steps.Count, native);
        Export("steps.json", steps); Export("replay.json", new { nativeCommands = native, accepted = steps.Count, staleRejected = 1,
            coldSessionRestored = true, dedupAfterColdRestore = true, replay });
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
