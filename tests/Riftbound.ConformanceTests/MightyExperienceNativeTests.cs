using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MightyExperienceNativeTests
{
    [Theory]
    [InlineData("pay")] [InlineData("decline")]
    public async Task ProductionExperienceThresholdMightyConfirmationAndResponsesReplay(string branch)
    {
        var initial = OfficialInsightAndSpellLockTests.Restore(MightyExperienceFrameTests.Envoy(MightyExperienceFrameTests.Hunter()));
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MIGHTY_EXPERIENCE_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
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
        var targets = Array.Empty<string>();
        Export("selection.json", new { sourceObjectId = "ENVOY", cardNo = "UNL-092/219", targetObjectIds = targets });
        var prompt = session.PromptFor("P1"); Export("play-prompt.json", prompt);
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick, new("ENVOY", "UNL-092/219", targets));
        var nativeRequest = dir is not null && File.Exists(Path.Combine(dir, "play-preview.json"));
        if (nativeRequest) request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!, "play-preview.json")), json)!;
        Assert.Equal(targets, request.Command.TargetObjectIds);
        var quote = engine.PreviewPlayCard(initial, "P1", request); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana); Assert.Empty(quote.Cost.PowerByTrait); Export("play-quote.json", quote);
        var raw = Raw("play", new { cmdType = "PLAY_CARD", sourceObjectId = "ENVOY", cardNo = "UNL-092/219", targetObjectIds = targets,
            promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var play = GameCommandJsonMapper.Map(raw); var done = await session.SubmitAsync("P1", "play", play, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
        var duplicate = await session.SubmitAsync("P1", "play", play, raw, default); Assert.True(duplicate.Accepted); Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync("P1", "stale", play, raw, default); Assert.False(stale.Accepted);
        Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
        var steps = branch == "pay" ? new[] { "spell-pass-1", "spell-pass-2", "cost", "skill-pass-1", "skill-pass-2" }
            : new[] { "spell-pass-1", "spell-pass-2", "cost" };
        Export("steps.json", steps); var native = dir is not null && File.Exists(Path.Combine(dir, "play-command.json")) ? 1 : 0;
        foreach (var name in steps) {
            var actor = done.State.PendingPayment?.PlayerId ?? done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            GameCommand command = name == "cost" ? new PayCostCommand(done.State.PendingPayment!.PaymentId, done.State.PendingPayment.PaymentWindow,
                [branch == "pay" ? "SPEND_POWER:yellow:1" : "DECLINE"]) : new PassPriorityCommand();
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json), json)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(done.State.Tick);
            var nextRaw = Raw(name, fields); if (dir is not null && File.Exists(Path.Combine(dir, name + "-command.json"))) native++;
            done = await session.SubmitAsync(actor, name, GameCommandJsonMapper.Map(nextRaw), nextRaw, default); Assert.True(done.Accepted, done.ErrorMessage);
            Assert.DoesNotContain(done.Events, e => e.Kind.StartsWith("INTERNAL_", StringComparison.Ordinal));
            OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
            if (name == "spell-pass-2") {
                Assert.Equal(5, ArmamentMightTests.Might(done.State));
                Assert.True(done.State.CardObjects["HOST"].IsExhausted); Assert.Equal(6, done.State.PlayerExperience["P1"]);
                Assert.NotNull(done.State.PendingPayment);
                { var c = Assert.Single(done.State.StackItems).MightySkill!; Assert.Equal(4, c.BeforeMight); Assert.Equal(5, c.AfterMight); }
            }
        }
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingPayment);
        Assert.Equal(branch == "pay" ? 2 : 3, done.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Equal(steps.Length + 1, commands.Count(c => c.Accepted)); Assert.Single(commands, c => !c.Accepted);
        Export("replay.json", new { nativeRequest, nativeCommands = native, accepted = steps.Length + 1, staleRejected = 1, replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
