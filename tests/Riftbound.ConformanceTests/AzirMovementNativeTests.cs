using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ArmamentMightTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AzirMovementNativeTests
{
    [Theory]
    [InlineData("same-location")]
    [InlineData("move-attach")]
    [InlineData("move-decline")]
    [InlineData("opponent-focus")]
    public async Task ProductionMovementAndChosenArmamentCommandsReplayWithExactEventsAndRecovery(string scenario)
    {
        var initial = scenario == "opponent-focus" ? await AzirMovementAuditTests.OpponentFocusPosition()
            : AzirMovementAuditTests.AzirPosition(scenario == "same-location");
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var root = Environment.GetEnvironmentVariable("RIFTBOUND_AZIR_MOVEMENT_EVIDENCE");
        if (root is not null) root = Path.Combine(root, scenario);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task<ResolutionResult> Submit(string name, string player, GameCommand fallback) {
            var prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[player]); var command = fallback;
            var path = root is null ? null : Path.Combine(root, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                Assert.Equal(prompt.PromptId, doc.RootElement.GetProperty("promptId").GetString());
                Assert.Equal(current.Tick, doc.RootElement.GetProperty("snapshotTick").GetInt64());
                command = GameCommandJsonMapper.Map(doc.RootElement); native++;
            }
            var result = await session.SubmitAsync(player, scenario + "-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), options), default);
            Assert.True(result.Accepted, result.ErrorMessage); current = result.State; Restore(current); PublicRecovery(current); return result;
        }
        var result = await Submit("activate", "P1", AzirMovementAuditTests.Command(scenario != "move-decline"));
        Assert.Equal("TARGET", result.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.DoesNotContain(result.Events, e => e.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "EQUIPMENT_ATTACHED");
        result = await Submit("move-pass-1", "P1", new PassPriorityCommand());
        result = await Submit("move-pass-2", "P2", new PassPriorityCommand());
        Assert.Equal(scenario == "move-decline" ? "TARGET" : "HOST", result.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(scenario == "same-location" ? "F" : "OTHER", result.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(result.State.ObjectLocations[scenario == "move-decline" ? "TARGET" : "HOST"], result.State.ObjectLocations["GEAR"]);
        if (scenario == "same-location") {
            Assert.Empty(result.State.StackItems); Assert.DoesNotContain(result.Events, e => e.Kind == "TRIGGER_QUEUED");
            Assert.Equal(0, result.State.CardObjects["TARGET"].MovementCountThisTurn);
        } else {
            Assert.Single(result.State.StackItems, i => i.MovementObserver is not null);
            Assert.DoesNotContain(result.State.CardObjects.Values, c => c.Tags.Contains("金币"));
            result = await Submit("gold-pass-1", "P1", new PassPriorityCommand());
            result = await Submit("gold-pass-2", "P2", new PassPriorityCommand());
            var gold = Assert.Single(result.State.CardObjects.Values, c => c.Tags.Contains("金币"));
            Assert.True(gold.IsExhausted); Assert.Equal("P1", gold.ControllerId);
        }
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("result-snapshot.json", result.Snapshots["P1"]); Export("replay.json", new { nativeCommands = native, commands = commands.Length, replay });
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
