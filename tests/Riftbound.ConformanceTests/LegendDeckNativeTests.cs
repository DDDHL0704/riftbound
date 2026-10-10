using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialLegendDeckPlayTests;

namespace Riftbound.ConformanceTests;

public sealed class LegendDeckNativeTests
{
    [Theory]
    [InlineData("deck-unit", "SFD·125/221")]
    [InlineData("deck-equipment", "SFD·042/221")]
    [InlineData("deck-spell", "OGN·048/298")]
    [InlineData("deck-decline", "SFD·125/221")]
    [InlineData("cost-accept", "SFD·125/221")]
    [InlineData("cost-decline", "SFD·125/221")]
    public async Task NativeDeckChoiceRestoresAndReplays(string branch, string no)
    {
        var cost = branch.StartsWith("cost"); var decline = branch.EndsWith("decline");
        var opened = cost ? await ConquestLifecycleRegressionTests.Conquer(Position()) : await Open(Position(top: ["SFD·125/221", no]));
        var initial = opened.State; var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_LEGEND_DECK_EVIDENCE");
        var directory = root is null ? null : Path.Combine(root, branch); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string file, object value) { if (directory is null) return; Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, file), JsonSerializer.Serialize(value, value.GetType(), json)); }
        var prompt = session.PromptFor("P1"); Export("prompt.json", prompt); Export("snapshot.json", opened.Snapshots["P1"]);
        GameCommand command = cost ? new ChooseCardsCommand(initial.PendingCardChoice!.ChoiceId, initial.PendingCardChoice.ChoiceWindow, decline ? [] : ["LEGEND"])
            : decline ? new ChooseCardsCommand(initial.PendingEffectPlay!.ChoiceId, "EFFECT_PLAY", []) : new PlayCardCommand("DECK1", no, []);
        if (command is PlayCardCommand play) {
            Export("candidate.json", prompt.Candidates!.Single(c => c.Action == CommandTypes.PlayCard));
            var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, play)); Assert.True(quote.CanPay, quote.Message); Export("quote.json", quote);
        }
        var path = directory is null ? null : Path.Combine(directory, "native-command.json"); var usedNative = path is not null && File.Exists(path);
        var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
        if (usedNative) {
            using var doc = JsonDocument.Parse(File.ReadAllText(path!)); raw = doc.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString()); Assert.Equal(initial.Tick, raw.GetProperty("snapshotTick").GetInt64());
        }
        var result = await session.SubmitAsync("P1", branch, command, raw, default); Assert.True(result.Accepted, result.ErrorMessage); OfficialGraveyardRecastTests.Restore(result.State);
        if (!decline) for (var i = 0; i < 2; i++) {
            if (result.State.PriorityPlayerId is null) break;
            var pass = new PassPriorityCommand(); result = await session.SubmitAsync(result.State.PriorityPlayerId, branch + "-" + i, pass, JsonSerializer.SerializeToElement(pass, json), default);
            Assert.True(result.Accepted, result.ErrorMessage); OfficialGraveyardRecastTests.Restore(result.State);
        }
        if (cost) Assert.Equal(!decline, result.State.PendingEffectPlay is not null);
        else { Assert.Null(result.State.PendingEffectPlay); Assert.Equal(decline ? 3 : branch == "deck-spell" ? 1 : 2, result.State.PlayerZones["P1"].MainDeck.Count); }
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev,i) => new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("replay.json",new { usedNative, replay });
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
