using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SpellCompletionContinuationTests
{
    [Theory]
    [InlineData("exile", true)]
    [InlineData("exile", false)]
    [InlineData("hall", true)]
    [InlineData("hall", false)]
    public async Task TriggerChoiceRestoresAndReplaysThroughProductionCommand(string scenario, bool accept)
    {
        var exile = scenario == "exile";
        var initial = exile ? OfficialSpellCompletionTests.Position("OGN·083/298", "UNL-181/219", false)
            : ConformanceFixtureRunnerTests.BattlefieldSpellPowerBonusState();
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SPELL_COMPLETION_EVIDENCE");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value)
        {
            if (root is null) return;
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command, JsonElement? raw = null)
        {
            var result = await session.SubmitAsync(player, scenario + journal.Entries.Count, command,
                raw ?? JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            OfficialSpellCompletionTests.Restore(result.State);
            return result;
        }
        var result = await Submit("P1", exile ? new PlayCardCommand("C", "OGN·083/298", [])
            : new PlayCardCommand("P1-SPELL-SAVAGE-STRENGTH", "SFD·034/221", ["P1-BATTLEFIELD-ALLY"]));
        for (var i = 0; i < 2; i++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        var choice = result.State.PendingCardChoice!;
        Assert.Equal("SPELL_TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        var prompt = session.PromptFor(choice.PlayerId);
        Export(scenario + "-prompt.json", prompt);
        GameCommand command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? [exile ? "C" : "P1-BATTLEFIELD-ALLY"] : []);
        JsonElement? supplied = null;
        var branch = scenario + (accept ? "-accept" : "-decline");
        var nativePath = root is null ? null : Path.Combine(root, "native-" + branch + "-command.json");
        if (nativePath is not null && File.Exists(nativePath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(nativePath));
            supplied = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(supplied.Value);
            Assert.Equal(prompt.PromptId, supplied.Value.GetProperty("promptId").GetString());
            Assert.Equal(result.State.Tick, supplied.Value.GetProperty("snapshotTick").GetInt64());
        }
        result = await Submit(choice.PlayerId, command, supplied);
        if (accept)
        {
            Assert.Single(result.State.StackItems);
            for (var i = 0; i < 2; i++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingCardChoice);
        if (exile) Assert.Equal(accept, result.State.PlayerZones["P1"].Banished.Contains("C"));
        else Assert.Equal(accept ? 5 : 4, result.State.CardObjects["P1-BATTLEFIELD-ALLY"].Power);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export(branch + "-replay.json", new { nativeCommandUsed = supplied.HasValue, replay });
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
