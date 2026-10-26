using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HighestUnitMightNativeTests
{
    [Theory]
    [InlineData("equipped")] [InlineData("below")] [InlineData("missing-red")]
    public async Task ProductionQuoteConfirmationAndResponsesUseCurrentMightAndReplay(string branch)
    {
        var initial = OfficialInsightAndSpellLockTests.Restore(HighestUnitMightTests.Position(attached: branch != "below", red: branch == "missing-red" ? 0 : 1));
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_HIGHEST_MIGHT_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
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
        Export("selection.json", new { sourceObjectId = "SPELL", cardNo = "OGN·014/298", targetObjectIds = new[] { "HOST" } });
        var prompt = session.PromptFor("P1"); Export("play-prompt.json", prompt);
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Equal(branch != "below", candidate.Enabled);
        Assert.Equal(branch != "below", (candidate.Sources ?? []).Any(s => s.Id == "SPELL"));
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick, new("SPELL", "OGN·014/298", ["HOST"]));
        var nativeRequest = dir is not null && File.Exists(Path.Combine(dir, "play-preview.json"));
        if (nativeRequest) request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!, "play-preview.json")), json)!;
        Assert.Equal("SPELL", request.Command.SourceObjectId); Assert.Equal("OGN·014/298", request.Command.CardNo); Assert.Equal(new[] { "HOST" }, request.Command.TargetObjectIds);
        var before = MatchStateHasher.Hash(initial); var quote = engine.PreviewPlayCard(initial, "P1", request);
        Assert.Equal(branch == "equipped", quote.CanPay); Assert.Equal(branch == "below" ? 6 : 3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]); Assert.Equal(before, MatchStateHasher.Hash(initial)); Export("play-quote.json", quote);
        if (!quote.CanPay) {
            var rejected = await engine.ResolveAsync(initial, new("blocked", "P1", CommandTypes.PlayCard), request.Command, default);
            Assert.False(rejected.Accepted); Assert.Equal(before, MatchStateHasher.Hash(rejected.State)); Assert.Empty(rejected.Events); Assert.Empty(journal.Entries);
            Export("blocked.json", new { nativeRequest, quote.CanPay, mana = quote.Cost.Mana, power = quote.Cost.PowerByTrait, stateHash = before }); return;
        }
        var raw = Raw("play", new { cmdType = "PLAY_CARD", sourceObjectId = "SPELL", cardNo = "OGN·014/298", targetObjectIds = new[] { "HOST" }, promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var done = await session.SubmitAsync("P1", "play", Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(raw)), raw, default); Assert.True(done.Accepted, done.ErrorMessage);
        Assert.Equal(0, done.State.RunePools["P1"].Mana); Assert.Empty(done.State.RunePools["P1"].PowerByTrait);
        var duplicate = await session.SubmitAsync("P1", "play", GameCommandJsonMapper.Map(raw), raw, default); Assert.True(duplicate.Accepted); Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync("P1", "stale-play", GameCommandJsonMapper.Map(raw), raw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
        var nativeCommands = dir is not null && File.Exists(Path.Combine(dir, "play-command.json")) ? 1 : 0;
        foreach (var name in new[] { "pass-1", "pass-2", "last-breath-pass-1", "last-breath-pass-2" }) {
            var actor = done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            if (dir is not null && File.Exists(Path.Combine(dir, name + "-command.json"))) nativeCommands++;
            var passRaw = Raw(name, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            done = await session.SubmitAsync(actor, name, GameCommandJsonMapper.Map(passRaw), passRaw, default); Assert.True(done.Accepted, done.ErrorMessage);
            OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
            if (name == "pass-2") Assert.Equal(5, Assert.Single(done.State.StackItems).LastBreathMight!.Might);
        }
        Assert.Equal(2, done.State.PlayerZones["P1"].Hand.Count); Assert.Empty(done.State.StackItems);
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Graveyard);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Equal(5, commands.Count(c => c.Accepted)); Assert.Single(commands, c => !c.Accepted);
        Export("replay.json", new { nativeRequest, nativeCommands, accepted = 5, staleRejected = 1, replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
