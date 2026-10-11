using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;
public sealed class MovementDiscardNativeTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NativeHandChoiceRestoresAndReplaysAfterSourceLeaves(bool sourceLeaves)
    {
        var moved = await MovementObserverAuditTests.Move(MovementDiscardTests.Position(), "BASE:P2");
        var s = moved.State;
        if (sourceLeaves) s = s with {
            CardObjects = s.CardObjects.Where(p => p.Key != "D").ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P2"] = s.PlayerZones["P2"] with { Base = s.PlayerZones["P2"].Base.Where(id => id != "D").ToArray() } },
            ObjectLocations = s.ObjectLocations.Where(p => p.Key != "D").ToDictionary() };
        var opened = await OfficialGraveyardRecastTests.Top(s);
        var initial = OfficialInsightAndSpellLockTests.Restore(opened.State);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var p = initial.PendingHandChoice!;
        var prompt = session.PromptFor("P2");
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MOVEMENT_DISCARD_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, sourceLeaves ? "source-gone" : "source-present");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) { if (dir is null) return; Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json)); }
        Export("hand-choice-prompt.json", prompt); Export("snapshot.json", opened.Snapshots["P2"]);
        GameCommand command = new ChooseHandCardsCommand(p.ChoiceId, p.ChoiceWindow, ["H2"]);
        var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
        var native = dir is not null && File.Exists(Path.Combine(dir, "native-hand-choice-command.json"));
        if (native) { using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!, "native-hand-choice-command.json"))); raw = doc.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString()); Assert.Equal(initial.Tick, raw.GetProperty("snapshotTick").GetInt64()); }
        var result = await session.SubmitAsync("P2", "native-discard", command, raw, default);
        Assert.True(result.Accepted, result.ErrorMessage); OfficialGraveyardRecastTests.Restore(result.State);
        Assert.Equal(new[] { "H1", "H3", "DRAW-TOP" }, result.State.PlayerZones["P2"].Hand);
        Assert.Contains("H2", result.State.PlayerZones["P2"].Graveyard);
        Assert.Null(result.State.PendingHandChoice);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev,i) => new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));
        Export("replay.json",new{native,replay,commands=commands.Length});
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
