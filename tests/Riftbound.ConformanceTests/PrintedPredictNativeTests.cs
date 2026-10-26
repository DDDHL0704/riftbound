using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PrintedPredictNativeTests
{
    [Theory]
    [InlineData("OGN·174/298", false)]
    [InlineData("OGN·174/298", true)]
    [InlineData("UNL-161/219", false)]
    [InlineData("UNL-161/219", true)]
    public async Task ProductionPrintedUnitEquipmentChoicesRecoverDeduplicateAndReplay(string card, bool recycle)
    {
        var initial = GrantedPredictTests.Hand(card) with { Tick = 0 };
        if (card == "UNL-161/219") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["MECH"] = initial.CardObjects["MECH"] with { Tags = [CardObjectTags.EquipmentCard] } } };
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_PRINTED_PREDICT_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, (card == "UNL-161/219" ? "equipment-" : "unit-") + (recycle ? "recycle" : "keep"));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var steps = new List<string>(); var native = 0;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        await Submit("play", "P1", new PlayCardCommand("MECH", card, [], OptionalCosts: []));
        for (var i = 0; i < 2; i++) await Submit("pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        var choice = Assert.IsType<PendingCardChoiceState>(current.PendingCardChoice);
        Assert.Equal([initial.PlayerZones["P1"].MainDeck[0]], choice.LegalObjectIds);
        await Submit("choose", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, recycle ? choice.LegalObjectIds : []));
        Assert.Empty(current.StackItems); Assert.Empty(current.TriggerQueue);
        var finalized = Assert.Single(current.CardPlays.Finalized);
        var resolved = Assert.Single(current.CardPlays.Resolved);
        Assert.Equal("MECH", finalized.Source.ObjectId); Assert.Equal(finalized.PlayId, resolved.PlayId);
        Assert.Equal(card == "UNL-161/219" ? 2 : 1, resolved.Types);
        Assert.Equal(recycle ? initial.PlayerZones["P1"].MainDeck.Skip(1).Append(initial.PlayerZones["P1"].MainDeck[0]) : initial.PlayerZones["P1"].MainDeck, current.PlayerZones["P1"].MainDeck);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), current, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("receipt.json", new { recycle, native, commands = journal.Entries.Count, restores = steps.Count, events = Events().Length,
            finalized = current.CardPlays.Finalized, resolved = current.CardPlays.Resolved, replay });
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
            current = OfficialInsightAndSpellLockTests.Restore(result.State); steps.Add(name);
            var count = journal.Entries.Count; Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var sequence = journal.Entries[^1].CompletedEventSequence;
            session = MatchSession.Restore(new(initial.RoomId, current.Tick, sequence, Commands(), Events(), new Dictionary<string, RecoveredPlayerView>(), [], current, initial), engine, journal);
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            var stale = await session.SubmitAsync(actor, "stale-" + name, command, raw, default);
            Assert.False(stale.Accepted); Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode); Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
