using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AttachmentModeNativeTests
{
    [Theory]
    [InlineData("ready")] [InlineData("channel")] [InlineData("boon")]
    [InlineData("copied")] [InlineData("detach")] [InlineData("source-leaves")]
    public async Task ProductionModesTargetsIndependentCopiesAndResponsesRecoverAndReplay(string scenario)
    {
        var initial = CopiedAttachmentObserverTests.AddSword(AttachmentModeTests.Position());
        if (scenario == "detach") initial = AttachedRuleTextTests.WithSpell(initial, "P1", "SFD·011/221");
        if (scenario == "source-leaves") {
            initial = AttachedRuleTextTests.WithSpell(initial, "P1", "SFD·163/221");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["FRIEND"] = new("FRIEND", cardNo: "OGN·001/298", ownerId: "P1", controllerId: "P1", power: 2, tags: [CardObjectTags.UnitCard]) } };
            initial = LocalDestructionRecallTests.At(initial, "FRIEND", "P1", "BASE");
        }
        OfficialGraveyardRecastTests.Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var root = Environment.GetEnvironmentVariable("RIFTBOUND_ATTACHMENT_MODE_EVIDENCE");
        if (root is not null) root = Path.Combine(root, scenario);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        var steps = new List<object>();
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task Submit(string name, string player, GameCommand fallback, string? source = null, string[]? targets = null,
            string[]? costs = null, bool reverseTriggers = false) {
            var prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[player]);
            steps.Add(new { name, action = fallback.CmdType, source, targets = targets ?? [], costs = costs ?? [], reverseTriggers });
            var command = fallback; var raw = JsonSerializer.SerializeToElement(command, command.GetType(), options);
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
        async Task Resolve(string name) {
            await Submit(name + "-pass-1", current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(name + "-pass-2", current.PriorityPlayerId!, new PassPriorityCommand());
        }
        async Task Choose(string name, params string[] ids) {
            var choice = current.PendingCardChoice!;
            if (choice.ChoiceWindow == "ATTACHMENT_MODE_CONFIRMATION") {
                var candidate = session.PromptFor("P1").Candidates!.Single(c => c.Action == CommandTypes.ChooseCards);
                var labels = JsonSerializer.SerializeToElement(candidate.Metadata, options).GetProperty("cardChoices");
                Assert.Equal(choice.LegalObjectIds.Count, labels.GetArrayLength());
                Assert.All(labels.EnumerateArray(), target => Assert.DoesNotContain('_', target.GetProperty("label").GetString()!));
            }
            await Submit(name, choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids), targets: ids);
        }
        await Submit("song", "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]), "GEAR", ["HOST"], ["ASSEMBLE_GREEN"]);
        await Resolve("song"); Assert.Single(current.StackItems);
        var mode = scenario == "ready" ? "READY_RUNES" : scenario is "boon" or "source-leaves" ? "GRANT_BOON" : "CHANNEL_RUNE";
        await Choose("mode-1", mode);
        if (mode == "READY_RUNES") await Choose("targets-1", "MR1", "MR2");
        if (mode == "GRANT_BOON") await Choose("targets-1", "HOST");
        if (scenario == "source-leaves") {
            Assert.Equal(new[] { "HOST" }, current.StackItems[0].TargetObjectIds);
            await Submit("destroy", "P1", new PlayCardCommand("SPELL", "SFD·163/221", ["HOST", "FRIEND"]), "SPELL", ["HOST", "FRIEND"]);
            await Resolve("destroy"); Assert.Contains("HOST", current.PlayerZones["P1"].Graveyard);
            await Resolve("resolve-1"); Assert.DoesNotContain(CardObjectTags.Boon, current.CardObjects["HOST"].Tags);
            Assert.Single(current.RuleTextModeUses!.Uses);
        } else {
            await Resolve("resolve-1");
            if (scenario is "copied" or "detach") {
                await Submit("sword", "P1", new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"]), "SWORD", ["HOST"], ["ASSEMBLE_RED"]);
                await Resolve("sword"); Assert.Equal(2, current.TriggerQueue.Count);
                var ids = current.TriggerQueue.OrderBy(i => i.AttachmentObserver!.CopiedInstance ? 0 : 1).Select(i => i.TriggerId).ToArray();
                await Submit("order", "P1", new OrderTriggersCommand(OrderedTriggerIds: ids), reverseTriggers: true);
                Assert.False(current.StackItems.Single(i => i.StackItemId == current.PendingCardChoice!.ResolvingStackItemId).AttachmentObserver!.CopiedInstance);
                Assert.DoesNotContain("CHANNEL_RUNE", current.PendingCardChoice!.LegalObjectIds);
                await Choose("mode-2", "GRANT_BOON"); await Choose("targets-2", "HOST");
                Assert.True(current.StackItems.Single(i => i.StackItemId == current.PendingCardChoice!.ResolvingStackItemId).AttachmentObserver!.CopiedInstance);
                await Choose("mode-3", "READY_RUNES"); await Choose("targets-3", "MR1", "MR2");
                Assert.True(current.CardObjects["MR1"].IsExhausted); Assert.DoesNotContain(CardObjectTags.Boon, current.CardObjects["HOST"].Tags);
                Assert.Equal(3, current.RuleTextModeUses!.Uses.Count);
                if (scenario == "detach") {
                    await Submit("detach", "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]), "SPELL", ["HOST", "GEAR"]);
                    await Resolve("detach"); Assert.Null(current.CardObjects["GEAR"].AttachedRuleText);
                }
                await Resolve("resolve-2"); Assert.Single(current.StackItems); Assert.False(current.CardObjects["MR1"].IsExhausted);
                await Resolve("resolve-3"); Assert.Contains(CardObjectTags.Boon, current.CardObjects["HOST"].Tags);
            } else if (scenario == "ready") { Assert.False(current.CardObjects["MR1"].IsExhausted); Assert.False(current.CardObjects["MR2"].IsExhausted); }
            else if (scenario == "boon") Assert.Contains(CardObjectTags.Boon, current.CardObjects["HOST"].Tags);
            else { Assert.Contains("RD1", current.PlayerZones["P1"].Base); Assert.True(current.CardObjects["RD1"].IsExhausted); }
        }
        Assert.Empty(current.StackItems);
        if (native > 0) Assert.Equal(journal.Entries.Count, native);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("result-snapshot.json", ResolutionResult.BuildSnapshots(current)["P1"]);
        Export("replay.json", new { nativeCommands = native, commands = commands.Length, replay });
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
