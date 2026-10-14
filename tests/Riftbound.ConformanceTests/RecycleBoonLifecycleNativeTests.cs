using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RecycleBoonLifecycleNativeTests
{
    [Theory]
    [InlineData("OGN·235/298")]
    [InlineData("SFD·237/221")]
    [InlineData("SFD·237*/221")]
    public async Task ProductionCopyLookRecycleReturnAndSurvivingBoonColdRecoverDeduplicateAndReplay(string card)
    {
        var initial = RecycledCardBoonLifecycleTests.Position(card);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_RECYCLE_BOON_LIFECYCLE_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, card.StartsWith("OGN") ? "primary" : card.Contains("*") ? "promo" : "alternate");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var steps = new List<string>(); var native = 0;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        Export("initial.json", initial);
        await Submit("copy-play", "P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"], OptionalCosts: []));
        for (var i = 0; i < 2; i++) await Submit("copy-parent-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        for (var i = 0; i < 2; i++) await Submit("copy-reflexive-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(card, current.CardObjects["C-TOKEN-001"].CardNo);
        await Submit("look-play", "P1", new PlayCardCommand("LOOK", "UNL-063/219", ["TARGET"], OptionalCosts: []));
        for (var i = 0; i < 2; i++) await Submit("look-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        var insight = current.PendingCardChoice!;
        await Submit("recycle", "P1", new ChooseCardsCommand(insight.ChoiceId, insight.ChoiceWindow, [insight.LegalObjectIds[0]]));
        var target = current.PendingCardChoice!;
        await Submit("target", "P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ALLY"]));
        await Submit("leave-play", "P1", new PlayCardCommand("LEAVE", "OGN·104/298", ["C-TOKEN-001"], OptionalCosts: []));
        for (var i = 0; i < 2; i++) await Submit("leave-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.False(current.CardObjects.ContainsKey("C-TOKEN-001"));
        var surviving = Assert.Single(current.StackItems); Assert.True(surviving.FieldContext!.SourceCeasedToExist);
        Assert.Equal("C-TOKEN-001", surviving.FieldContext.RecycledBoon!.CeasedSource!.Source.ObjectId);
        for (var i = 0; i < 2; i++) await Submit("boon-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains(CardObjectTags.Boon, current.CardObjects["ALLY"].Tags); Assert.Empty(current.StackItems);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), current, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("receipt.json", new { card, native, commands = journal.Entries.Count, restores = steps.Count, events = Events().Length, replay });
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
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
