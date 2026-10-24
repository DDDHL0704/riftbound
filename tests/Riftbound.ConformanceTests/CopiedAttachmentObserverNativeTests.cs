using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CopiedAttachmentObserverNativeTests
{
    [Theory]
    [InlineData("native-first")] [InlineData("copy-first")] [InlineData("decline-copy")] [InlineData("detach")]
    public async Task ProductionOrderingPaymentsResponsesAndDetachRecoverAndReplay(string scenario)
    {
        var initial = CopiedAttachmentObserverTests.AddSword(AttachedRuleTextTests.Position("SFD·119/221", 0));
        if (scenario == "detach") initial = AttachedRuleTextTests.WithSpell(initial, "P1", "SFD·011/221");
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var root = Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_ATTACHMENT_EVIDENCE");
        if (root is not null) root = Path.Combine(root, scenario);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task Submit(string name, string player, GameCommand fallback) {
            var prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[player]); var command = fallback;
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), options);
            var path = root is null ? null : Path.Combine(root, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var doc = JsonDocument.Parse(File.ReadAllText(path)); raw = doc.RootElement.Clone();
                Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString());
                Assert.Equal(current.Tick, raw.GetProperty("snapshotTick").GetInt64());
                command = GameCommandJsonMapper.Map(raw); native++;
            }
            var result = await session.SubmitAsync(player, scenario + "-" + journal.Entries.Count, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); current = result.State;
            OfficialGraveyardRecastTests.Restore(current); ArmamentMightTests.PublicRecovery(current);
        }
        async Task Resolve(string prefix) {
            await Submit(prefix + "-pass-1", "P1", new PassPriorityCommand());
            await Submit(prefix + "-pass-2", "P2", new PassPriorityCommand());
        }
        async Task Confirm(string name, string choice) {
            var p = current.PendingPayment!;
            await Submit(name, "P1", new PayCostCommand(p.PaymentId, p.PaymentWindow, [choice]));
        }
        await Submit("song", "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])); await Resolve("song");
        Assert.Single(current.StackItems); Assert.False(current.StackItems[0].AttachmentObserver!.CopiedInstance);
        await Confirm("song-decline", "DECLINE");
        await Submit("sword", "P1", new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"])); await Resolve("sword");
        Assert.Equal(2, current.TriggerQueue.Count);
        var copiedFirst = scenario is "copy-first" or "detach";
        var ids = current.TriggerQueue.OrderBy(i => i.AttachmentObserver!.CopiedInstance == copiedFirst ? 0 : 1).Select(i => i.TriggerId).ToArray();
        await Submit("order", "P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        Assert.Equal(copiedFirst, current.StackItems.Last().AttachmentObserver!.CopiedInstance);
        var mana = current.RunePools["P1"].Mana; var hand = current.PlayerZones["P1"].Hand.Count;
        if (scenario == "decline-copy") Assert.True(current.StackItems.Single(i => i.StackItemId == current.PendingPayment!.ResolvingStackItemId).AttachmentObserver!.CopiedInstance);
        await Confirm("confirm-1", scenario == "decline-copy" ? "DECLINE" : "PAY");
        Assert.Equal(hand, current.PlayerZones["P1"].Hand.Count); Assert.Null(current.PriorityPlayerId);
        await Confirm("confirm-2", "PAY"); Assert.Null(current.PendingPayment);
        Assert.Equal(mana - (scenario == "decline-copy" ? 1 : 2), current.RunePools["P1"].Mana);
        if (scenario == "detach") {
            await Submit("detach", "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"])); await Resolve("detach");
            Assert.Null(current.CardObjects["GEAR"].AttachedRuleText); Assert.Equal(2, current.StackItems.Count);
        }
        await Resolve("resolve-1");
        if (scenario != "decline-copy") { Assert.Single(current.StackItems); await Resolve("resolve-2"); }
        Assert.Empty(current.StackItems);
        Assert.Equal(hand + (scenario == "decline-copy" ? 1 : 2), current.PlayerZones["P1"].Hand.Count);
        if (native > 0) Assert.Equal(journal.Entries.Count, native);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("result-snapshot.json", ResolutionResult.BuildSnapshots(current)["P1"]);
        Export("replay.json", new { nativeCommands = native, commands = commands.Length, replay });
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
