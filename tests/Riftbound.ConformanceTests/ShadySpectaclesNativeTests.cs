using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class ShadySpectaclesNativeTests
{
    [Theory]
    [InlineData("refresh")]
    [InlineData("spell")]
    [InlineData("return")]
    public async Task ProductionEquipmentPlayCopyChoiceAndIndependentSkillsReplay(string branch)
    {
        var initial = ShadySpectaclesTests.Position(branch == "spell" ? "VEN·183" : "SFD·050/221");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { ManaCost = 4 } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Where(id => id != "GEAR").ToArray(), Hand = ["GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GEAR"] = new("P1", "HAND") } };
        if (branch != "return") initial = OfficialCopyIdentityTests.AddSpell(initial, "DETACH", "SFD·011/221");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "SPELL", branch == "return" ? "OGN·104/298" : "OGN·048/298");
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SHADY_EVIDENCE");
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
            if (fallback is OrderTriggersCommand order) { fields.Remove("triggerIds"); fields["orderedTriggerIds"] = JsonSerializer.SerializeToElement(order.TriggerIds); }
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            var path = dir is null ? null : Path.Combine(dir, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var document = JsonDocument.Parse(File.ReadAllText(path)); raw = document.RootElement.Clone();
                command = GameCommandJsonMapper.Map(raw); native++;
            }
            Export(name + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) Export(name + "-quote.json", engine.PreviewPlayCard(current, actor,
                new("quote", prompt.PromptId!, current.Tick, play)));
            steps.Add(new { name, action = fallback.CmdType });
            var done = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
            var count = journal.Entries.Count;
            Assert.True((await session.SubmitAsync(actor, name, command, raw, default)).Accepted); Assert.Equal(count, journal.Entries.Count);
            current = done.State; Restore(current); ArmamentMightTests.PublicRecovery(current);
            if (name == "play") {
                var stale = await session.SubmitAsync(actor, "stale", command, raw, default); Assert.False(stale.Accepted);
                Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
            }
        }
        async Task Passes(string prefix) {
            await Submit(prefix + "-1", current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(prefix + "-2", current.PriorityPlayerId!, new PassPriorityCommand());
        }
        async Task Equip(string prefix) {
            await Submit(prefix, "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"]));
            await Passes(prefix + "-pass");
            var pending = current.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", pending.Request.Kind);
            await Submit(prefix + "-copy", "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        }
        async Task Order(string name) {
            if (current.TriggerQueue.Count > 0) await Submit(name, "P1", new OrderTriggersCommand(current.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        }
        await Submit("play", "P1", new PlayCardCommand("GEAR", "VEN·137", []));
        Assert.Equal(16, current.RunePools["P1"].Mana); await Equip("equip");
        Assert.Equal(branch == "spell" ? "VEN·183" : "SFD·050/221", current.CardObjects["HOST"].CardNo);
        if (branch == "refresh") {
            var first = CopiedActivatedAbilityTests.Copy(current) with { TargetObjectIds = ["COPY-SOURCE"] };
            await Submit("activate", "P1", first); await Passes("activate-pass");
            await Submit("detach", "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await Passes("detach-pass");
            Assert.Equal("OGN·096/298", current.CardObjects["HOST"].CardNo); await Equip("reequip");
            var fresh = CopiedActivatedAbilityTests.Copy(current) with { TargetObjectIds = ["COPY-SOURCE"] };
            Assert.NotEqual(first.AbilityInstanceId, fresh.AbilityInstanceId);
            await Submit("fresh", "P1", fresh); await Passes("fresh-pass"); Assert.Empty(CopiedActivatedAbilityTests.Requirements(current));
        } else if (branch == "spell") {
            await Submit("spell", "P1", new PlayCardCommand("SPELL", "OGN·048/298", [])); await Passes("spell-pass"); await Order("spell-order");
            var captured = Assert.Single(current.StackItems, item => item.SourceObjectId == "HOST");
            await Submit("detach", "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await Passes("detach-pass"); await Order("detach-order");
            Assert.Contains(current.StackItems, item => item.StackItemId == captured.StackItemId);
            var skills = current.StackItems.Count; for (var i = 0; i < skills; i++) await Passes("skill-" + i);
            Assert.Equal(3, current.CardObjects["HOST"].Power); Assert.Equal(7, current.CardObjects["COPY-SOURCE"].Power);
        } else {
            await Submit("return", "P1", new PlayCardCommand("SPELL", "OGN·104/298", ["HOST"])); await Passes("return-pass");
            Assert.Equal("OGN·096/298", current.CardObjects["HOST"].CardNo); Assert.Contains("HOST", current.PlayerZones["P1"].Hand);
        }
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Single(commands, command => !command.Accepted);
        if (native > 0) Assert.Equal(steps.Count, native);
        Export("steps.json", steps); Export("replay.json", new { nativeCommands = native, accepted = steps.Count, staleRejected = 1, replay });
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
