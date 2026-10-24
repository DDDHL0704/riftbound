using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuneRecycleDestinationNativeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ProductionBattleRuneReturnAndOptionalCostColdRecoverDeduplicateAndReplay(int runeCount)
    {
        var initial = RuneRecycleDestinationTests.ConquestPosition(runeCount, exhausted: true) with { Tick = 0 };
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_RUNE_DESTINATION_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, runeCount == 1 ? "single" : "multiple");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var steps = new List<string>(); var native = 0;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        Export("initial.json", initial);
        await Submit("battle", "P1", RuneRecycleDestinationTests.Battle());
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, current.PlayerZones["P1"].MainDeck);
        Assert.Equal(new[] { "BOTTOM", "RUNE" }, current.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", current.ObjectLocations["RUNE"].Zone); Assert.False(current.CardObjects["RUNE"].IsExhausted);
        var choice = Assert.IsType<PendingCardChoiceState>(current.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        await Submit("choose", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.False(current.CardObjects["SIVIR"].IsExhausted); Assert.Empty(current.StackItems); Assert.Empty(current.TriggerQueue);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), current, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("receipt.json", new { runeCount, native, commands = journal.Entries.Count, restores = steps.Count, events = Events().Length, replay });
        if (native > 0) Assert.Equal(steps.Count, native);
        async Task Submit(string name, string actor, GameCommand fallback) {
            var prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt); Export(name + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) Export(name + "-quote.json", engine.PreviewPlayCard(current, actor, new("quote", prompt.PromptId!, current.Tick, play)));
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            if (dir is not null && File.Exists(Path.Combine(dir, name + "-command.json"))) {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-command.json")));
                raw = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw); native++;
            }
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString()); Assert.Equal(current.Tick, raw.GetProperty("snapshotTick").GetInt64());
            var result = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(result.Accepted, result.ErrorMessage);
            Export(name + "-snapshot.json", result.Snapshots[actor]);
            current = OfficialInsightAndSpellLockTests.Restore(result.State); steps.Add(name);
            var count = journal.Entries.Count; Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var sequence = journal.Entries[^1].CompletedEventSequence;
            session = MatchSession.Restore(new(initial.RoomId, current.Tick, sequence, Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], current, initial), engine, journal);
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var stale = await session.SubmitAsync(actor, "stale-" + name, command, raw, default);
            Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
