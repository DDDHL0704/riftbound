using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ConquestTokenNativeTests
{
    [Theory]
    [InlineData("UNL-222/219", false)]
    [InlineData("UNL-222/219", true)]
    [InlineData("SFD·069/221", false)]
    [InlineData("SFD·069/221", true)]
    public async Task ProductionMovementFocusReactionAndGoldResponsesColdRecoverDeduplicateAndReplay(string card, bool reaction)
    {
        var initial = reaction ? ConquestTokenTriggerTests.ReactionPosition(card) : ConquestTokenTriggerTests.Position(card);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_CONQUEST_TOKEN_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, (card.StartsWith("UNL") ? "primary" : "alternate") + (reaction ? "-reaction" : "-plain"));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var steps = new List<string>(); var native = 0;
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        Export("initial.json", initial);
        await Submit("move", "P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await Submit("focus-0", "P1", new PassFocusCommand());
        await Submit("focus-1", "P2", new PassFocusCommand());
        Assert.Empty(ConquestTokenTriggerTests.Gold(current)); Assert.Single(current.StackItems);
        if (reaction) {
            await Submit("response", "P1", new PassPriorityCommand());
            await Submit("play", "P2", new PlayCardCommand("COUNTER", "OGN·133/298", [], OptionalCosts: []));
            for (var i = 0; i < 2; i++) await Submit("reaction-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Contains("UNIT", current.PlayerZones["P1"].Graveyard);
            Assert.Empty(ConquestTokenTriggerTests.Gold(current)); Assert.Single(current.StackItems);
        }
        for (var i = 0; i < 2; i++) await Submit("gold-pass-" + i, current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(current.StackItems); Assert.Empty(current.TriggerQueue);
        Assert.True(current.CardObjects[Assert.Single(ConquestTokenTriggerTests.Gold(current))].IsExhausted);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), current, engine, default, Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("receipt.json", new { card, reaction, native, commands = journal.Entries.Count, restores = steps.Count, events = Events().Length, replay });
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
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
