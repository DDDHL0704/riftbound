using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulSacrificeNativeTests
{
    [Theory]
    [InlineData("equipped")]
    [InlineData("token")]
    public async Task ProductionMightyCostSelectionAndSeparateResponsesReplay(string branch)
    {
        var initial = PowerfulSacrificeCostTests.Equipped(); var target = "HOST";
        if (branch == "token") {
            initial = (await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.Position("SFD·167/221"))).State;
            target = "C-TOKEN-001";
            initial = LocalDestructionRecallTests.At(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEAR"] = new("GEAR", cardNo: "SFD·056/221", ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], attachedToObjectId: target) } }, "GEAR", "P1", "BASE");
            initial = PowerfulConsumerMightTests.Hand(initial, PowerfulSacrificeCostTests.Card);
            var runes = new[] { "R1", "R2", "R3" };
            var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
            var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
            foreach (var id in runes) {
                cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"]);
                locations[id] = new("P1", "RUNE_DECK");
            }
            initial = initial with { CardObjects = cards, ObjectLocations = locations,
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = initial.PlayerZones["P1"] with { RuneDeck = runes } } };
        }
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var cost = "DESTROY_FRIENDLY_POWERFUL_UNIT:" + target;
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SACRIFICE_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
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
        Export("selection.json", new { sourceObjectId = "SPELL", cardNo = PowerfulSacrificeCostTests.Card, targetObjectIds = Array.Empty<string>(), optionalCosts = new[] { cost } });
        var prompt = session.PromptFor("P1"); Export("play-prompt.json", prompt);
        var missing = engine.PreviewPlayCard(initial, "P1", new("missing", prompt.PromptId!, initial.Tick,
            new("SPELL", PowerfulSacrificeCostTests.Card, [])));
        Assert.False(missing.CanPay); Export("missing-cost-quote.json", missing);
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick,
            new("SPELL", PowerfulSacrificeCostTests.Card, [], OptionalCosts: [cost]));
        if (dir is not null && File.Exists(Path.Combine(dir, "play-preview.json")))
            request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir, "play-preview.json")), json)!;
        Assert.Empty(request.Command.TargetObjectIds); Assert.Equal(new[] { cost }, request.Command.OptionalCosts);
        var quote = engine.PreviewPlayCard(initial, "P1", request);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana); Export("play-quote.json", quote);
        var raw = Raw("play", new { cmdType = "PLAY_CARD", sourceObjectId = "SPELL", cardNo = PowerfulSacrificeCostTests.Card,
            targetObjectIds = Array.Empty<string>(), optionalCosts = new[] { cost }, promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var play = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Empty(play.TargetObjectIds); Assert.Equal(new[] { cost }, play.OptionalCosts);
        var done = await session.SubmitAsync("P1", "play", play, raw, default); Assert.True(done.Accepted, done.ErrorMessage);
        Assert.Equal(19, done.State.RunePools["P1"].Mana); Assert.Empty(done.State.PlayerZones["P1"].Hand);
        Assert.Equal(5, done.State.StackItems[^1].LastBreathMight!.Might);
        OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        var duplicate = await session.SubmitAsync("P1", "play", play, raw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(duplicate.State)); Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync("P1", "stale-play", play, raw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(stale.State));
        foreach (var name in new[] { "last-breath-pass-1", "last-breath-pass-2", "spell-pass-1", "spell-pass-2" }) {
            var actor = done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            var passRaw = Raw(name, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            done = await session.SubmitAsync(actor, name, Assert.IsType<PassPriorityCommand>(GameCommandJsonMapper.Map(passRaw)), passRaw, default);
            Assert.True(done.Accepted, done.ErrorMessage);
            if (name == "last-breath-pass-2") {
                Assert.Equal(2, done.State.PlayerZones["P1"].Hand.Count); Assert.Equal(3, done.State.PlayerZones["P1"].RuneDeck.Count);
                Assert.Single(done.State.StackItems); OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
            }
        }
        Assert.Equal(4, done.State.PlayerZones["P1"].Hand.Count); Assert.Equal(2, done.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.True(done.State.CardObjects["R1"].IsExhausted);
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue);
        Assert.Equal(branch != "token", done.State.CardObjects.ContainsKey(target));
        OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Equal(5, commands.Count(c => c.Accepted)); Assert.Single(commands, c => !c.Accepted);
        Export("replay.json", new { native = dir is not null && File.Exists(Path.Combine(dir, "play-command.json")), commands = commands.Length,
            replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
