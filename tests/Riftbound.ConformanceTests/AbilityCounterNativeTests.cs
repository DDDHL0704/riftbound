using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AbilityCounterNativeTests
{
    [Theory]
    [InlineData("azir")]
    [InlineData("copied")]
    [InlineData("play-trigger")]
    [InlineData("source-gone")]
    [InlineData("laurent-base")]
    public async Task ProductionCounterSelectionsPreserveIndependentSourcesAndReplay(string scenario)
    {
        var initial = scenario switch {
            "play-trigger" => await AbilityCounterAuditTests.PendingEntry(),
            "source-gone" => await AbilityCounterAuditTests.PendingSourceGone(),
            "laurent-base" => await AbilityCounterAuditTests.PendingLaurent(),
            _ => await AbilityCounterAuditTests.PendingAzir(copied: scenario == "copied")
        };
        Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var target = initial.StackItems.Single(); var root = Environment.GetEnvironmentVariable("RIFTBOUND_ABILITY_COUNTER_EVIDENCE");
        if (root is not null) root = Path.Combine(root, scenario);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string file, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, file), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task<ResolutionResult> Submit(string name, string player, GameCommand fallback) {
            var prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[player]);
            var raw = JsonSerializer.SerializeToElement(fallback, fallback.GetType(), options); var command = fallback;
            var path = root is null ? null : Path.Combine(root, name + "-command.json");
            if (path is not null && File.Exists(path)) {
                using var document = JsonDocument.Parse(File.ReadAllText(path)); raw = document.RootElement.Clone();
                Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString());
                Assert.Equal(current.Tick, raw.GetProperty("snapshotTick").GetInt64());
                command = GameCommandJsonMapper.Map(raw); native++;
            }
            var result = await session.SubmitAsync(player, scenario + "-" + name, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); current = result.State;
            Restore(current); ArmamentMightTests.PublicRecovery(current); return result;
        }
        var counter = scenario == "play-trigger" ? new PlayCardCommand("RESPONSE", "UNL-106/219", ["FRIEND", target.StackItemId])
            : scenario == "laurent-base" ? new PlayCardCommand("SPELL", "SFD·206/221", ["HOST", target.StackItemId])
            : new PlayCardCommand("RESPONSE", "SFD·045/221", [target.StackItemId]);
        Export("selection.json", new { sourceObjectId = counter.SourceObjectId, targetObjectIds = counter.TargetObjectIds, ability = scenario != "laurent-base" });
        var result = await Submit("counter", initial.PriorityPlayerId!, counter);
        Assert.Equal(2, result.State.StackItems.Count);
        result = await Submit("pass-1", result.State.PriorityPlayerId!, new PassPriorityCommand());
        result = await Submit("pass-2", result.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingCardChoice);
        var countered = Assert.Single(result.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
        if (scenario != "laurent-base") {
            Assert.Equal("ABILITY", countered.Payload["stackObjectKind"]); Assert.Equal(true, countered.Payload["abilitySourcePreserved"]);
            Assert.Equal(initial.ObjectLocations[target.SourceObjectId], result.State.ObjectLocations[target.SourceObjectId]);
            Assert.Equal(initial.CardObjects[target.SourceObjectId], result.State.CardObjects[target.SourceObjectId]);
            Assert.DoesNotContain(result.Events, e => e.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "ABILITY_RESOLVED" or "EQUIPMENT_ATTACHED");
        } else {
            Assert.Contains("RESPONSE", result.State.PlayerZones["P2"].Graveyard);
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(target.CardNo, out var spell));
            Assert.Equal(spell.ManaCost, result.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        }
        if (native > 0) Assert.Equal(journal.Entries.Count, native);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("result-snapshot.json", result.Snapshots[initial.PriorityPlayerId!]); Export("replay.json", new { nativeCommands = native, commands = commands.Length, replay });
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
