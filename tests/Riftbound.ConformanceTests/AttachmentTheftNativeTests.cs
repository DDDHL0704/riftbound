using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttachmentTheftNativeTests
{
    [Theory]
    [InlineData("mode")]
    [InlineData("return")]
    [InlineData("destroy")]
    public async Task ProductionExtraCostCopyChoiceAndChangedFaceExitRecoverAndReplay(string branch)
    {
        var initial = AttachmentTheftCopyTests.Sword(await AttachmentTheftCopyTests.Position());
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with {
                    Base = initial.PlayerZones["P2"].Base.Where(id => id != "P2-SWORD").ToArray(),
                    Hand = [..initial.PlayerZones["P2"].Hand, "P2-SWORD"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["P2-SWORD"] = initial.CardObjects["P2-SWORD"] with { ManaCost = 1 } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["P2-SWORD"] = new("P2", "HAND") }
        };
        if (branch != "mode") initial = AttackDamageDistributionTests.Spell(initial, "LEAVE",
            branch == "return" ? "OGN·104/298" : "OGN·229/298", "P2");
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_THEFT_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
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
            if (name == "steal") {
                var stale = await session.SubmitAsync(actor, "stale", command, raw, default); Assert.False(stale.Accepted);
                Assert.Equal(MatchStateHasher.Hash(current), MatchStateHasher.Hash(stale.State));
            }
        }
        async Task Passes(string prefix) {
            await Submit(prefix + "-1", current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(prefix + "-2", current.PriorityPlayerId!, new PassPriorityCommand());
        }
        await Submit("steal", "P2", new PlayCardCommand("AKSHAN", "SFD·109/221", [],
            OptionalCosts: ["AKSHAN_STEAL_EQUIPMENT:GEAR"]));
        Assert.Equal(16, current.RunePools["P2"].Mana); Assert.Equal(18, current.RunePools["P2"].PowerByTrait["orange"]);
        await Passes("steal-pass");
        var pending = current.PendingRuleChoice!;
        Assert.Equal("P2", pending.Request.PlayerId);
        Assert.Equal("OGN·096/298", pending.Request.Preview!.CardObjects["HOST"].CardNo);
        await Submit("copy", "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["COPY:P2-COPY"]));
        Assert.Equal("SFD·049/221", current.CardObjects["AKSHAN"].CardNo);
        Assert.Empty(current.StackItems); Assert.Empty(current.TriggerQueue); Assert.Null(current.PendingCardChoice);
        if (branch == "mode") {
            await Submit("sword-play", "P2", new PlayCardCommand("P2-SWORD", "SFD·022/221", ["AKSHAN"]));
            await Passes("sword-play-pass");
            Assert.Equal("AKSHAN", current.CardObjects["P2-SWORD"].AttachedToObjectId);
            Assert.Single(current.StackItems);
            var choice = current.PendingCardChoice!;
            await Submit("mode", "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GRANT_BOON"]));
            choice = current.PendingCardChoice!;
            await Submit("target", "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["AKSHAN"]));
            Assert.DoesNotContain(CardObjectTags.Boon, current.CardObjects["AKSHAN"].Tags);
            await Passes("mode-pass"); Assert.Contains(CardObjectTags.Boon, current.CardObjects["AKSHAN"].Tags);
            Assert.Equal(5, current.CardObjects["AKSHAN"].Power);
        } else {
            await Submit("leave", "P2", new PlayCardCommand("LEAVE", branch == "return" ? "OGN·104/298" : "OGN·229/298", ["AKSHAN"]));
            await Passes("leave-pass");
            Assert.Contains("AKSHAN", branch == "return" ? current.PlayerZones["P2"].Hand : current.PlayerZones["P2"].Graveyard);
            Assert.Equal("SFD·109/221", current.CardObjects["AKSHAN"].CardNo);
            Assert.Equal("P1", current.CardObjects["GEAR"].ControllerId); Assert.Null(current.CardObjects["GEAR"].AttachedToObjectId);
            Assert.Contains("GEAR", current.PlayerZones["P1"].Base);
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
