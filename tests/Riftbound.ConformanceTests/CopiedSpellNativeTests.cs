using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class CopiedSpellNativeTests
{
    [Theory]
    [InlineData("one", 1)]
    [InlineData("two", 2)]
    [InlineData("detach", 1)]
    [InlineData("ready-one", 1)]
    [InlineData("ready-two", 2)]
    public async Task ProductionSpellOrderingAndEveryIndependentResponseReplay(string branch, int copies)
    {
        var readySource = branch.StartsWith("ready-", StringComparison.Ordinal);
        var spellCard = readySource ? "OGN·083/298" : "OGN·048/298";
        var initial = AttachedRuleTextTests.Position(readySource ? "UNL-005/219" : "VEN·183", 0);
        for (var i = 0; i < copies; i++) {
            var id = i == 0 ? "GEAR" : "SONG" + i;
            if (i > 0) initial = AttachedRuleTextTests.AddSong(initial, id);
            initial = (await AttachedRuleTextTests.Attach(initial, id)).State;
        }
        if (readySource) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "SPELL", spellCard);
        if (branch == "detach") initial = OfficialCopyIdentityTests.AddSpell(initial, "DETACH", "SFD·011/221");
        Restore(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_COPIED_SPELL_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var native = 0; var steps = new List<object>();
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task<ResolutionResult> Submit(string name, string actor, GameCommand fallback) {
            var prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[actor]);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(fallback, fallback.GetType(), options), options)!;
            if (fallback is OrderTriggersCommand order) {
                fields.Remove("triggerIds");
                fields["orderedTriggerIds"] = JsonSerializer.SerializeToElement(order.TriggerIds);
            }
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields); var command = fallback;
            var path = dir is null ? null : Path.Combine(dir, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var document = JsonDocument.Parse(File.ReadAllText(path)); raw = document.RootElement.Clone();
                command = GameCommandJsonMapper.Map(raw); native++;
            }
            Export(name + "-selection.json", fallback);
            if (fallback is PlayCardCommand play) {
                var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, current.Tick, play);
                Export(name + "-quote.json", engine.PreviewPlayCard(current, actor, request));
            }
            steps.Add(new { name, action = fallback.CmdType });
            var result = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(result.Accepted, result.ErrorMessage);
            var count = journal.Entries.Count;
            var duplicate = await session.SubmitAsync(actor, name, command, raw, default); Assert.True(duplicate.Accepted); Assert.Equal(count, journal.Entries.Count);
            current = result.State; Restore(current); ArmamentMightTests.PublicRecovery(current);
            if (name == "play") {
                var stale = await session.SubmitAsync(actor, "stale", command, raw, default); Assert.False(stale.Accepted);
                Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
            }
            return result;
        }
        async Task Passes(string prefix) {
            await Submit(prefix + "-1", current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(prefix + "-2", current.PriorityPlayerId!, new PassPriorityCommand());
        }
        await Submit("play", "P1", new PlayCardCommand("SPELL", spellCard, []));
        await Passes("spell-pass"); Assert.Equal(copies + 1, current.TriggerQueue.Count);
        var ids = current.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray();
        await Submit("order", "P1", new OrderTriggersCommand(ids));
        Assert.Equal(copies + 1, current.StackItems.Count);
        var origins = current.StackItems.Where(i => i.SpellContext!.Origin is not null).Select(i => i.SpellContext!.InstanceId).ToArray();
        if (branch == "detach") {
            await Submit("detach", "P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
            await Passes("detach-pass"); Assert.Null(current.CardObjects["GEAR"].AttachedRuleText);
            Assert.All(origins, id => Assert.Contains(current.StackItems, i => i.SpellContext?.InstanceId == id));
        }
        var skills = current.StackItems.Count;
        for (var i = 0; i < skills; i++) {
            await Passes("skill-" + i);
            if (readySource) Assert.False(current.CardObjects["HOST"].IsExhausted);
        }
        Assert.Empty(current.StackItems); Assert.Empty(current.TriggerQueue);
        Assert.Equal(readySource ? 7 : branch == "detach" ? 9 : copies == 2 ? 9 : 7, current.CardObjects["HOST"].Power);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Single(commands, c => !c.Accepted);
        if (native > 0) Assert.Equal(steps.Count, native);
        Export("steps.json", steps); Export("result-snapshot.json", ResolutionResult.BuildSnapshots(current)["P1"]);
        Export("replay.json", new { nativeCommands = native, accepted = steps.Count, staleRejected = 1, replay });
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
