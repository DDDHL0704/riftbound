using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SunkenTempleNativeTests
{
    [Theory]
    [InlineData("pay")] [InlineData("decline")] [InlineData("below")]
    public async Task ProductionMovementFocusCostAndResponseCommandsReplay(string branch)
    {
        var initial = OfficialInsightAndSpellLockTests.Restore(SunkenTempleMightTests.Position(power: branch == "below" ? 1 : 2));
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SUNKEN_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        var steps = branch switch { "pay" => new[] { "move", "focus-1", "focus-2", "cost", "pass-1", "pass-2" },
            "decline" => ["move", "focus-1", "focus-2", "cost"], _ => ["move", "focus-1", "focus-2"] };
        Export("steps.json", steps); Export("snapshot.json", ResolutionResult.BuildSnapshots(initial)["P1"]);
        var state = initial; ResolutionResult? done = null; var native = true;
        foreach (var name in steps) {
            var actor = name == "focus-2" ? "P2" : name.StartsWith("pass-") ? state.PriorityPlayerId! : "P1";
            var prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            GameCommand command = name switch {
                "move" => new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []),
                "focus-1" or "focus-2" => new PassFocusCommand(),
                "cost" => new PayCostCommand(state.PendingPayment!.PaymentId, state.PendingPayment.PaymentWindow, [branch == "pay" ? "PAY" : "DECLINE"]),
                _ => new PassPriorityCommand() };
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw.GetRawText(), json)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(state.Tick); raw = JsonSerializer.SerializeToElement(fields, json);
            var path = dir is null ? null : Path.Combine(dir, name + "-command.json");
            if (path is not null && File.Exists(path)) { using var doc = JsonDocument.Parse(File.ReadAllText(path)); raw = doc.RootElement.Clone(); }
            else native = false;
            command = GameCommandJsonMapper.Map(raw);
            done = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
            if (name == "move") {
                var duplicate = await session.SubmitAsync(actor, name, command, raw, default);
                Assert.True(duplicate.Accepted); Assert.Single(journal.Entries);
                var stale = await session.SubmitAsync(actor, "stale", command, raw, default);
                Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
            }
            state = done.State; OfficialGraveyardRecastTests.Restore(state); ArmamentMightTests.PublicRecovery(state);
            if (name == "focus-2") {
                Assert.Equal(branch != "below", state.PendingPayment is not null);
                Assert.Empty(state.PlayerZones["P1"].Hand);
                if (branch != "below") Assert.Equal(5, Assert.Single(state.StackItems).BattlefieldConquestMight!.PowerfulUnits[0].Might);
            }
            if (name == "cost") { Assert.Empty(state.PlayerZones["P1"].Hand); Assert.Equal(branch == "pay" ? 4 : 5, state.RunePools["P1"].Mana); }
        }
        Assert.Equal(branch == "pay" ? 1 : 0, state.PlayerZones["P1"].Hand.Count);
        Assert.Empty(state.StackItems); Assert.Null(state.PendingPayment);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, state, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Equal(steps.Length, commands.Count(c => c.Accepted));
        Export("replay.json", new { native, accepted = steps.Length, staleRejected = commands.Count(c => !c.Accepted), replay,
            finalHash = MatchStateHasher.Hash(state) }); Export("result-snapshot.json", done!.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
