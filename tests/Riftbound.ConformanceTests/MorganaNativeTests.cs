using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MorganaNativeTests
{
    [Theory]
    [InlineData("base")] [InlineData("ambush")]
    [InlineData("ward-pay")] [InlineData("ward-decline")]
    public async Task ProductionMarkedDamageUnitAbilityAndResponsesReplay(string branch)
    {
        var cardNo = branch is "ambush" or "ward-decline" ? "VEN·186" : "VEN·017";
        var initial = VendettaAmbushTests.Position(cardNo);
        if (branch == "ambush") {
            initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "F");
            initial = await AmbushPermissionTests.Response(initial);
        }
        if (branch.StartsWith("ward", StringComparison.Ordinal)) {
            initial = LocalDestructionRecallTests.At(initial, "HOST", "P2", "BASE");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2", ControllerId = "P2", Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        }
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var destination = branch == "ambush" ? "BATTLEFIELD:F" : "BASE";
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MORGANA_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
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
        Export("selection.json", new { sourceObjectId = "UNIT", cardNo = cardNo, targetObjectIds = targets, destination });
        var prompt = session.PromptFor("P1"); Export("play-prompt.json", prompt);
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick, new("UNIT", cardNo, targets, Destination: destination));
        var nativeRequest = dir is not null && File.Exists(Path.Combine(dir, "play-preview.json"));
        if (nativeRequest) request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!, "play-preview.json")), json)!;
        Assert.Equal(targets, request.Command.TargetObjectIds);
        var quote = engine.PreviewPlayCard(initial, "P1", request); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(5, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]); Export("play-quote.json", quote);
        var raw = Raw("play", new { cmdType = "PLAY_CARD", sourceObjectId = "UNIT", cardNo = cardNo, targetObjectIds = targets, destination,
            promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var play = GameCommandJsonMapper.Map(raw); var done = await session.SubmitAsync("P1", "play", play, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
        var duplicate = await session.SubmitAsync("P1", "play", play, raw, default); Assert.True(duplicate.Accepted); Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync("P1", "stale", play, raw, default); Assert.False(stale.Accepted);
        Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
        Assert.Equal(2, done.State.CardObjects["HOST"].Damage);
        Assert.True(done.State.StackItems.Single(i => i.SourceObjectId == "UNIT").SourceConfirmed);
        var steps = branch switch {
            "ambush" => new[] { "target-choice", "skill-pass-1", "skill-pass-2", "older-pass-1", "older-pass-2" },
            "ward-pay" => ["target-choice", "ward-cost", "skill-pass-1", "skill-pass-2"],
            "ward-decline" => ["target-choice", "ward-cost"],
            _ => ["target-choice", "skill-pass-1", "skill-pass-2"]
        };
        Export("steps.json", steps); var native = dir is not null && File.Exists(Path.Combine(dir, "play-command.json")) ? 1 : 0;
        foreach (var name in steps) {
            var actor = done.State.PendingPayment?.PlayerId ?? done.State.PendingCardChoice?.PlayerId ?? done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            GameCommand command = name == "target-choice"
                ? new ChooseCardsCommand(done.State.PendingCardChoice!.ChoiceId, done.State.PendingCardChoice.ChoiceWindow, ["HOST"])
                : name == "ward-cost" ? new PayCostCommand(done.State.PendingPayment!.PaymentId, done.State.PendingPayment.PaymentWindow, [branch == "ward-pay" ? "PAY" : "DECLINE"])
                : new PassPriorityCommand();
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json), json)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(done.State.Tick);
            var nextRaw = Raw(name, fields); if (dir is not null && File.Exists(Path.Combine(dir, name + "-command.json"))) native++;
            done = await session.SubmitAsync(actor, name, GameCommandJsonMapper.Map(nextRaw), nextRaw, default); Assert.True(done.Accepted, done.ErrorMessage);
            Assert.DoesNotContain(done.Events, e => e.Kind.StartsWith("INTERNAL_", StringComparison.Ordinal));
            OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
            if (name == "skill-pass-2") Assert.Equal(4, done.State.CardObjects["HOST"].Damage);
        }
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingPayment);
        Assert.Equal(branch == "ward-decline" ? 2 : 4, done.State.CardObjects["HOST"].Damage);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
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
