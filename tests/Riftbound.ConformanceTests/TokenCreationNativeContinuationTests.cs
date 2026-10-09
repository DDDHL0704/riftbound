using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TokenCreationNativeContinuationTests
{
    [Theory]
    [InlineData("accept")]
    [InlineData("decline")]
    public async Task CostThenCopyNativeCommandsRestoreAndReplay(string branch)
    {
        var initial = OfficialLeblancCreationTests.Position(1);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_TOKEN_CREATION_EVIDENCE");
        var directory = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var usedNative = new List<string>();
        var current = initial;
        void Export(string name, object value) {
            if (directory is null) return; Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command, JsonElement? raw = null) {
            var result = await session.SubmitAsync(player, branch + journal.Entries.Count, command,
                raw ?? JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage); OfficialGraveyardRecastTests.Restore(result.State); current = result.State; return result;
        }
        async Task<ResolutionResult> Select(string stage, params string[] ids) {
            var prompt = session.PromptFor("P1"); Export(stage + "-prompt.json", prompt);
            var pending = current.PendingCardChoice!;
            GameCommand command = new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ids);
            JsonElement? raw = null;
            var path = directory is null ? null : Path.Combine(directory, stage + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var doc = JsonDocument.Parse(File.ReadAllText(path)); raw = doc.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw.Value);
                Assert.Equal(prompt.PromptId, raw.Value.GetProperty("promptId").GetString());
                Assert.Equal(current.Tick, raw.Value.GetProperty("snapshotTick").GetInt64()); usedNative.Add(stage);
            }
            return await Submit("P1", command, raw);
        }
        var result = await Submit("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        result = await Submit("P1", new PassFocusCommand()); result = await Submit("P2", new PassFocusCommand());
        result = await Select("cost", branch == "accept" ? ["H2"] : []);
        if (branch == "accept") {
            Assert.Empty(OfficialTokenReplacementTests.Tokens(result.State));
            for (var i = 0; i < 2; i++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            var pending = result.State.PendingCardChoice!;
            result = await Submit("P1", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["Z1"]));
            Export("copy-snapshot.json", result.Snapshots["P1"]);
            Assert.All(OfficialTokenReplacementTests.Tokens(result.State), t => Assert.Equal(0, t.Power));
            result = await Select("copy", "UNIT");
            for (var i = 0; i < 2; i++) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(2, OfficialTokenReplacementTests.Tokens(result.State).Length);
            Assert.All(OfficialTokenReplacementTests.Tokens(result.State), t => { Assert.Equal(1, t.Power); Assert.Contains(CardObjectTags.Ephemeral, t.Tags); });
            Assert.Equal(["H2"], result.State.PlayerZones["P1"].Graveyard);
        }
        else { Assert.Empty(OfficialTokenReplacementTests.Tokens(result.State)); Assert.False(result.State.CardObjects["LEGEND"].IsExhausted); }
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
