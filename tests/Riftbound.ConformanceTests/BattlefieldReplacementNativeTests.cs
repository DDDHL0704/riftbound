using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialBattlefieldReplacementTests;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldReplacementNativeTests
{
    [Theory]
    [InlineData("ivern", true)]
    [InlineData("ivern", false)]
    [InlineData("return", true)]
    [InlineData("return", false)]
    public async Task ProductionCreateAndReturnChoicesRestoreAndReplay(string name, bool accept)
    {
        var initial = name == "ivern" ? Position() : NextHold(WithoutLegend((await CreateBrush(Position())).State));
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_BATTLEFIELD_REPLACEMENT_EVIDENCE");
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
        ResolutionResult result;
        if (name == "ivern") {
            result = await Submit("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            result = await Submit("P1", new PassFocusCommand()); result = await Submit("P2", new PassFocusCommand());
        } else result = await Submit("P1", new PassPriorityCommand());
        var prompt = session.PromptFor("P1"); Export("prompt.json", prompt); Export("snapshot.json", result.Snapshots["P1"]);
        var pending = result.State.PendingCardChoice!;
        GameCommand command = new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, accept ? [name == "ivern" ? "LEGEND" : "BF"] : []);
        var path = directory is null ? null : Path.Combine(directory, "command.json");
        if (path is not null && File.Exists(path)) {
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); command = GameCommandJsonMapper.Map(doc.RootElement);
            Assert.Equal(prompt.PromptId, doc.RootElement.GetProperty("promptId").GetString());
            Assert.Equal(result.State.Tick, doc.RootElement.GetProperty("snapshotTick").GetInt64()); usedNative = true;
        }
        result = await Submit("P1", command);
        Assert.Equal(name == "ivern" ? "OGN·296/298" : "UNL·T03", result.State.CardObjects["BF"].CardNo);
        if (accept) for (var i = 0; i < 2; i++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(name == "ivern" == accept ? "UNL·T03" : "OGN·296/298", result.State.CardObjects["BF"].CardNo);
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingCardChoice);
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
