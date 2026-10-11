using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttachedRuleTextNativeTests
{
    [Theory]
    [InlineData("might")] [InlineData("azir")] [InlineData("reset")]
    public async Task ProductionPrintedTextAndInstanceSelectionsRecoverAndReplay(string scenario)
    {
        var initial = AttachedRuleTextTests.Position(scenario == "might" ? "OGN·109/298" : "SFD·050/221");
        if (scenario != "might") initial = At(OfficialHoldSequenceTests.AddUnit(initial, "TARGET", "SFD·130/221", "F", "P1"), "TARGET", "P1", "BASE");
        if (scenario == "reset") initial = AttachedRuleTextTests.WithSpell(initial, "P1", "SFD·011/221");
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var root = Environment.GetEnvironmentVariable("RIFTBOUND_ATTACHED_TEXT_EVIDENCE");
        if (root is not null) root = Path.Combine(root, scenario);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task<ResolutionResult> Submit(string name, string player, GameCommand fallback) {
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
            Restore(current); ArmamentMightTests.PublicRecovery(current); return result;
        }
        async Task Resolve(string prefix) {
            await Submit(prefix + "-pass-1", "P1", new PassPriorityCommand());
            await Submit(prefix + "-pass-2", "P2", new PassPriorityCommand());
        }
        await Submit("equip", "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        Assert.Null(current.CardObjects["GEAR"].AttachedRuleText);
        await Resolve("equip"); Assert.Equal(1, current.CardObjects["GEAR"].AttachedRuleText!.Revision);
        if (scenario == "might") Assert.Equal(8, ArmamentMightTests.Might(current));
        else {
            var originalPower = current.RunePools["P1"].TotalPower;
            await Submit("native", "P1", CopiedActivatedAbilityTests.Native()); await Resolve("native");
            var copy = CopiedActivatedAbilityTests.Copy(current);
            await Submit("copy", "P1", copy); await Resolve("copy");
            Assert.Empty(CopiedActivatedAbilityTests.Requirements(current));
            Assert.Equal(originalPower - 2, current.RunePools["P1"].TotalPower);
            if (scenario == "reset") {
                await Submit("detach", "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]));
                await Resolve("detach"); Assert.Null(current.CardObjects["GEAR"].AttachedRuleText);
                await Submit("reequip", "P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])); await Resolve("reequip");
                Assert.Equal(2, current.CardObjects["GEAR"].AttachedRuleText!.Revision);
                var fresh = CopiedActivatedAbilityTests.Copy(current); Assert.NotEqual(copy.AbilityInstanceId, fresh.AbilityInstanceId);
                await Submit("fresh", "P1", fresh); await Resolve("fresh"); Assert.Empty(CopiedActivatedAbilityTests.Requirements(current));
            }
        }
        if (native > 0) Assert.Equal(journal.Entries.Count, native);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
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
