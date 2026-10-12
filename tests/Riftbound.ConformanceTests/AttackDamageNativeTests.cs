using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AttackDamageNativeTests
{
    [Theory]
    [InlineData("printed")]
    [InlineData("alternate")]
    [InlineData("copied")]
    public async Task ProductionChosenTargetsAndLaterDamageDistributionReplayWithEveryRecoveryView(string scenario)
    {
        var initial = (await AttackDamageDistributionTests.Attack(AttackDamageDistributionTests.Position(
            scenario == "alternate" ? "OGN·041a/298" : "OGN·041/298", copied: scenario == "copied"))).State;
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var current = initial; var root = Environment.GetEnvironmentVariable("RIFTBOUND_ATTACK_DAMAGE_EVIDENCE");
        if (root is not null) root = Path.Combine(root, scenario);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var steps = new List<object>(); var native = 0;
        void Export(string file, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, file), JsonSerializer.Serialize(value, value.GetType(), options));
        }
        async Task<ResolutionResult> Submit(string name, string player, GameCommand fallback) {
            var prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt);
            Export(name + "-snapshot.json", ResolutionResult.BuildSnapshots(current)[player]);
            var raw = JsonSerializer.SerializeToElement(fallback, fallback.GetType(), options); var command = fallback;
            steps.Add(new { name, player, command = raw });
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
        if (scenario == "copied") {
            await Submit("order", "P1", new OrderTriggersCommand(OrderedTriggerIds: Enumerable.Reverse(current.TriggerQueue.Select(t => t.TriggerId)).ToArray()));
            var first = current.PendingCardChoice!;
            await Submit("target-printed", "P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, ["ENEMY1"]));
        }
        var choice = current.PendingCardChoice!;
        var targets = scenario == "copied" ? new[] { "ENEMY2", "ENEMY3" } : new[] { "ENEMY1", "ENEMY2", "ENEMY3" };
        await Submit("targets", "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        Assert.Null(current.PendingCardChoice);
        await Submit("pass-1", current.PriorityPlayerId!, new PassPriorityCommand());
        await Submit("pass-2", current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal("EFFECT_DAMAGE_DISTRIBUTION", current.PendingCardChoice!.ChoiceWindow);
        Assert.All(targets, id => Assert.Equal(0, current.CardObjects[id].Damage));
        var result = await Submit("allocate", "P1", scenario == "copied"
            ? AttackDamageDistributionTests.Allocation(current, ("ENEMY2", 3), ("ENEMY3", 2))
            : AttackDamageDistributionTests.Allocation(current, ("ENEMY1", 2), ("ENEMY2", 2), ("ENEMY3", 1)));
        if (scenario == "copied") {
            Assert.Single(current.StackItems); Assert.Contains("ENEMY2", current.PlayerZones["P2"].Graveyard);
            await Submit("pass-3", current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit("pass-4", current.PriorityPlayerId!, new PassPriorityCommand());
            result = await Submit("allocate-printed", "P1", AttackDamageDistributionTests.Allocation(current, ("ENEMY1", 5)));
            Assert.Contains("ENEMY1", current.PlayerZones["P2"].Graveyard);
        } else Assert.Equal(new[] { 2, 2, 1 }, targets.Select(id => current.CardObjects[id].Damage));
        Assert.Empty(current.StackItems); Assert.Null(current.PendingCardChoice); Assert.Equal(9, current.CardObjects["HOST"].Power);
        Assert.DoesNotContain(journal.Entries.Where(e => e.CommandType == CommandTypes.AssignEffectDamage).SelectMany(e => e.Events), e => e.Kind == "PRIORITY_PASSED");
        if (native > 0) Assert.Equal(journal.Entries.Count, native);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("steps.json", steps); Export("result-snapshot.json", result.Snapshots["P1"]);
        Export("replay.json", new { nativeCommands = native, commands = commands.Length, replay });
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
