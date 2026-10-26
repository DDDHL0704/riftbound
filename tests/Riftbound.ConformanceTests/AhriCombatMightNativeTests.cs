using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AhriCombatMightNativeTests
{
    [Theory]
    [InlineData("attack-aura")]
    [InlineData("defend-ven")]
    public async Task ProductionBattleRoleSelectionsResolveAndReplay(string branch)
    {
        var defend = branch == "defend-ven"; var player = defend ? "P2" : "P1"; var target = defend ? "HOST" : "ENEMY1";
        var initial = AhriCombatMightTests.Board(defend ? "VEN·SP3" : "OGN·119/298", defend);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { Power = defend ? 3 : 2 } } };
        if (!defend) initial = LocalDestructionRecallTests.At(DeathObserverAuditTests.Source(initial, "OGS·013/024", "GAREN"),
            "GAREN", "P2", "BATTLEFIELD", "F");
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_AHRI_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
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
        Export("selection.json", new { targetObjectIds = new[] { target }, sourceObjectId = defend ? "ENEMY1" : "HOST" });
        Export("snapshot.json", ResolutionResult.BuildSnapshots(initial)["P1"]);
        var prompt = session.PromptFor("P1"); Export("move-prompt.json", prompt);
        var moveRaw = Raw("move", new { cmdType = "MOVE_UNIT", sourceObjectId = "HOST", origin = "BASE", destination = "BATTLEFIELD:F",
            optionalCosts = Array.Empty<string>(), promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var move = Assert.IsType<MoveUnitCommand>(GameCommandJsonMapper.Map(moveRaw));
        Assert.Equal("HOST", move.SourceObjectId); Assert.Contains(move.Origin, new[] { "", "BASE" }); Assert.Equal("BATTLEFIELD:F", move.Destination);
        var moved = await session.SubmitAsync("P1", "move", move, moveRaw, default); Assert.True(moved.Accepted, moved.ErrorMessage);
        var choice = moved.State.PendingCardChoice!; Assert.NotNull(choice); Assert.Equal(player, choice.PlayerId);
        prompt = session.PromptFor(player); Export("choose-prompt.json", prompt);
        var raw = Raw("choose", new { cmdType = "CHOOSE_CARDS", choiceId = choice.ChoiceId, choiceWindow = choice.ChoiceWindow,
            chosenObjectIds = new[] { target }, promptId = prompt.PromptId, snapshotTick = moved.State.Tick });
        var command = Assert.IsType<ChooseCardsCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Equal(choice.ChoiceId, command.ChoiceId); Assert.Equal(choice.ChoiceWindow, command.ChoiceWindow);
        Assert.Equal(new[] { target }, command.ChosenObjectIds);
        var selected = await session.SubmitAsync(player, "choose", command, raw, default); Assert.True(selected.Accepted, selected.ErrorMessage);
        Assert.Equal(initial.CardObjects[target].Power, selected.State.CardObjects[target].Power);
        OfficialGraveyardRecastTests.Restore(selected.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(selected.State));
        var duplicate = await session.SubmitAsync(player, "choose", command, raw, default);
        Assert.Equal(MatchStateHasher.Hash(selected.State), MatchStateHasher.Hash(duplicate.State));
        var stale = await session.SubmitAsync(player, "stale-choose", command, raw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(selected.State), MatchStateHasher.Hash(stale.State));
        var done = selected;
        foreach (var name in new[] { "pass-1", "pass-2" }) {
            var actor = done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            var passRaw = Raw(name, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            done = await session.SubmitAsync(actor, name, Assert.IsType<PassPriorityCommand>(GameCommandJsonMapper.Map(passRaw)), passRaw, default);
            Assert.True(done.Accepted, done.ErrorMessage);
        }
        Assert.Equal(defend ? 1 : 0, done.State.CardObjects[target].Power);
        Assert.Equal(3, Assert.Single(done.State.CardObjects[target].UntilEndOfTurnPowerModifiers).SnapshotMight);
        Assert.Equal("P1", done.State.FocusPlayerId); OfficialGraveyardRecastTests.Restore(done.State);
        Assert.Empty(MinimumMightAuditTests.PublicErrors(done.State));
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("replay.json", new { native = dir is not null && File.Exists(Path.Combine(dir, "choose-command.json")), commands = commands.Length,
            replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots[player]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
