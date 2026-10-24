using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulConsumerNativeTests
{
    [Theory]
    [InlineData("draw")]
    [InlineData("discount")]
    public async Task ProductionPlayComposerUsesCurrentMightForDrawAndDiscountAndReplays(string branch)
    {
        var discounted = branch == "discount";
        var card = discounted ? "SFD·103/221" : "SFD·106/221";
        var initial = PowerfulConsumerMightTests.Hand(PowerfulConsumerMightTests.Equipped(), card);
        string[] targets = [];
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_POWERFUL_CONSUMER_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        JsonElement Raw(string name, object fallback) {
            if (dir is null || !File.Exists(Path.Combine(dir, name + "-command.json"))) return JsonSerializer.SerializeToElement(fallback, json);
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-command.json"))); return doc.RootElement.Clone();
        }
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        Export("snapshot.json", ResolutionResult.BuildSnapshots(initial)["P1"]);
        Export("selection.json", new { sourceObjectId = "SPELL", cardNo = card, targetObjectIds = targets });
        var prompt = session.PromptFor("P1"); Export("play-prompt.json", prompt);
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick, new("SPELL", card, targets));
        if (dir is not null && File.Exists(Path.Combine(dir, "play-preview.json")))
            request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir, "play-preview.json")), json)!;
        Assert.Equal("SPELL", request.Command.SourceObjectId); Assert.Equal(card, request.Command.CardNo); Assert.Equal(targets, request.Command.TargetObjectIds);
        var quote = engine.PreviewPlayCard(initial, "P1", request); Assert.True(quote.CanPay, quote.Message); Assert.Equal(discounted ? 5 : 2, quote.Cost!.Mana); Export("play-quote.json", quote);
        var raw = Raw("play", new { cmdType = "PLAY_CARD", sourceObjectId = "SPELL", cardNo = card, targetObjectIds = targets,
            promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var play = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Equal("SPELL", play.SourceObjectId); Assert.Equal(card, play.CardNo); Assert.Equal(targets, play.TargetObjectIds);
        var done = await session.SubmitAsync("P1", "play", play, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
        var duplicate = await session.SubmitAsync("P1", "play", play, raw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(duplicate.State)); Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync("P1", "stale-play", play, raw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
        foreach (var name in discounted ? Array.Empty<string>() : new[] { "pass-1", "pass-2" }) {
            var actor = done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            var passRaw = Raw(name, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            done = await session.SubmitAsync(actor, name, Assert.IsType<PassPriorityCommand>(GameCommandJsonMapper.Map(passRaw)), passRaw, default);
            Assert.True(done.Accepted, done.ErrorMessage);
        }
        Assert.Equal(6, ArmamentMightTests.Might(done.State));
        Assert.Equal(discounted ? 15 : 18, done.State.RunePools["P1"].Mana);
        Assert.Equal(discounted ? 0 : 1, done.State.PlayerZones["P1"].Hand.Count);
        if (discounted) {
            Assert.Contains("SPELL", done.State.PlayerZones["P1"].Base);
            Assert.True(done.State.CardObjects["SPELL"].IsExhausted);
        } else Assert.Contains("SPELL", done.State.PlayerZones["P1"].Graveyard);
        OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Equal(discounted ? 1 : 3, commands.Count(c => c.Accepted)); Assert.Single(commands, c => !c.Accepted);
        Export("replay.json", new { native = dir is not null && File.Exists(Path.Combine(dir, "play-command.json")), commands = commands.Length,
            replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
