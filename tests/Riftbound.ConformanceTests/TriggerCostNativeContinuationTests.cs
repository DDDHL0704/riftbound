using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TriggerCostNativeContinuationTests
{
    [Theory]
    [InlineData("vex", "UNL-193/219", true)]
    [InlineData("vex", "UNL-193/219", false)]
    [InlineData("renata", "SFD·201/221", true)]
    [InlineData("renata", "SFD·201/221", false)]
    [InlineData("hub", "SFD·214/221", true)]
    [InlineData("hub", "SFD·214/221", false)]
    public async Task NativeCostDecisionsRecoverAndReplay(string name, string no, bool accept)
    {
        var initial = OfficialTriggerCostTests.Position(no);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_TRIGGER_COST_EVIDENCE");
        var branch = name + (accept ? "-accept" : "-decline");
        var directory = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var usedNative = false;
        void Export(string name, object value) {
            if (directory is null) return; Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async ValueTask<ResolutionResult> Submit(string player, GameCommand command) {
            var result = await session.SubmitAsync(player, branch + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage); OfficialGraveyardRecastTests.Restore(result.State); return result;
        }
        var result = await Submit("P1", new PassPriorityCommand());
        var prompt = session.PromptFor("P1"); Export("prompt.json", prompt);
        Export("snapshot.json", result.Snapshots["P1"]);
        var command = OfficialTriggerCostTests.Choose(result.State, accept);
        var path = directory is null ? null : Path.Combine(directory, "command.json");
        if (path is not null && File.Exists(path)) {
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); command = GameCommandJsonMapper.Map(doc.RootElement);
            Assert.Equal(prompt.PromptId, doc.RootElement.GetProperty("promptId").GetString());
            Assert.Equal(result.State.Tick, doc.RootElement.GetProperty("snapshotTick").GetInt64()); usedNative = true;
        }
        result = await Submit("P1", command);
        if (accept) {
            Assert.Single(result.State.StackItems);
            Assert.Empty(result.State.PlayerZones["P1"].Hand);
            Assert.Equal(1, result.State.PlayerScores["P1"]);
        }
        result = await TurnSequenceTestDriver.Complete(result, Submit);
        Assert.Equal(accept && name == "vex" ? 2 : 1, result.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(accept && name == "hub" ? 2 : 1, result.State.PlayerScores["P1"]);
        Assert.Equal(accept && name == "renata" ? 1 : 0, result.State.CardObjects.Values.Count(c => c.Tags.Contains("金币")));
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Export("replay.json", new { usedNative, replay });
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
