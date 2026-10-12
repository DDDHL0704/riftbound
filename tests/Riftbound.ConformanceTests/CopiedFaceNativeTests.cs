using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CopiedFaceNativeTests
{
    [Theory]
    [InlineData("image-diana", "VEN·183")]
    [InlineData("image-azir", "SFD·050/221")]
    public async Task ProductionCopyResponseAttachmentAndTextAcquisitionReplay(string branch, string face)
    {
        var initial = OfficialCopyIdentityTests.Position(face);
        initial = LocalDestructionRecallTests.At(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = new("GEAR", cardNo: "SFD·059/221", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) } }, "GEAR", "P1", "BASE");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "ATTACH", "SFD·011/221");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "NEXT", branch == "image-diana" ? "OGN·048/298" : "SFD·011/221");
        if (branch == "image-azir") initial = OfficialCopyIdentityTests.AddSpell(initial, "REATTACH", "SFD·011/221");
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_FACE_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var native = 0; var steps = new List<object>();
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task Submit(string name, string actor, GameCommand fallback) {
            var prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[actor]);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), options), options)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            var path = dir is null ? null : Path.Combine(dir, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var doc = JsonDocument.Parse(File.ReadAllText(path)); raw = doc.RootElement.Clone();
                command = GameCommandJsonMapper.Map(raw); native++;
            }
            Export(name + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) Export(name + "-quote.json", engine.PreviewPlayCard(current, actor,
                new("quote", prompt.PromptId!, current.Tick, play)));
            steps.Add(new { name, action = fallback.CmdType });
            var done = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
            var count = journal.Entries.Count;
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted);
            Assert.Equal(count, journal.Entries.Count);
            current = done.State; Restore(current); ArmamentMightTests.PublicRecovery(current);
            if (name == "copy-play") {
                var stale = await session.SubmitAsync(actor, "stale", command, raw, default); Assert.False(stale.Accepted);
                Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
            }
        }
        async Task Passes(string prefix) {
            await Submit(prefix + "-1", current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(prefix + "-2", current.PriorityPlayerId!, new PassPriorityCommand());
        }
        await Submit("copy-play", "P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"]));
        await Passes("image-entry");
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, current.CardObjects["C-TOKEN-001"].CardNo);
        await Submit("attach", "P1", new PlayCardCommand("ATTACH", "SFD·011/221", ["C-TOKEN-001", "GEAR"]));
        await Passes("attach-pass");
        var first = Assert.IsType<AttachedRuleTextState>(current.CardObjects["GEAR"].AttachedRuleText);
        await Passes("copy-pass");
        Assert.Equal(face, current.CardObjects["C-TOKEN-001"].CardNo);
        Assert.Equal(first, current.CardObjects["GEAR"].AttachedRuleText);
        if (branch == "image-diana") {
            await Submit("spell", "P1", new PlayCardCommand("NEXT", "OGN·048/298", []));
            await Passes("spell-pass"); Assert.Single(current.StackItems);
            Assert.Null(current.StackItems.Single().SpellContext!.Origin);
            await Passes("skill-pass"); Assert.Equal(5, current.CardObjects["C-TOKEN-001"].Power);
        } else {
            await Submit("detach", "P1", new PlayCardCommand("NEXT", "SFD·011/221", ["C-TOKEN-001", "GEAR"]));
            await Passes("detach-pass"); Assert.Null(current.CardObjects["GEAR"].AttachedRuleText);
            await Submit("reattach", "P1", new PlayCardCommand("REATTACH", "SFD·011/221", ["C-TOKEN-001", "GEAR"]));
            await Passes("reattach-pass");
            var next = current.CardObjects["GEAR"].AttachedRuleText!;
            Assert.Equal(face, next.CardNo); Assert.True(next.Revision > first.Revision);
        }
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Single(commands, c => !c.Accepted);
        if (native > 0) Assert.Equal(steps.Count, native);
        Export("steps.json", steps); Export("replay.json", new { nativeCommands = native, accepted = steps.Count, staleRejected = 1, replay });
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
