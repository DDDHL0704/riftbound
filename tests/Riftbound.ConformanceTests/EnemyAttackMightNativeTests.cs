using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EnemyAttackMightNativeTests
{
    [Theory]
    [InlineData("deflect")]
    [InlineData("equipment-might")]
    public async Task ProductionMoveAndResponseCommandsResolveAutomaticAttackTriggersAndReplay(string branch)
    {
        var equipped = branch == "equipment-might";
        var initial = EnemyAttackMightTests.Board(equipped ? "OGN·303*/298" : "OGN·255/298", equipped ? 0 : 3);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Tags = equipped ? [CardObjectTags.UnitCard] : [CardObjectTags.UnitCard, "法盾2"] },
            ["GEAR"] = initial.CardObjects["GEAR"] with { AttachedToObjectId = equipped ? "HOST" : null } } };
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_ENEMY_ATTACK_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        JsonElement Raw(string name, object fallback) {
            if (dir is null || !File.Exists(Path.Combine(dir, name + "-command.json"))) return JsonSerializer.SerializeToElement(fallback, json);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, name + "-command.json"))); return document.RootElement.Clone();
        }
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        Export("snapshot.json", ResolutionResult.BuildSnapshots(initial)["P1"]);
        var prompt = session.PromptFor("P1"); Export("move-prompt.json", prompt);
        var moveRaw = Raw("move", new { cmdType = "MOVE_UNIT", sourceObjectId = "HOST", origin = "BASE", destination = "BATTLEFIELD:F",
            optionalCosts = Array.Empty<string>(), promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var move = Assert.IsType<MoveUnitCommand>(GameCommandJsonMapper.Map(moveRaw));
        Assert.Equal("HOST", move.SourceObjectId); Assert.Contains(move.Origin, new[] { "", "BASE" }); Assert.Equal("BATTLEFIELD:F", move.Destination);
        var moved = await session.SubmitAsync("P1", "move", move, moveRaw, default); Assert.True(moved.Accepted, moved.ErrorMessage);
        Assert.Equal(equipped ? 0 : 3, moved.State.CardObjects["HOST"].Power);
        Assert.Null(moved.State.PendingCardChoice); Assert.Null(moved.State.PendingPayment);
        var trigger = Assert.Single(moved.State.StackItems); Assert.Equal("FOX", trigger.SourceObjectId);
        Assert.Empty(trigger.TargetObjectIds); Assert.Equal("HOST", trigger.FieldContext!.AffectedUnit!.ObjectId);
        var duplicate = await session.SubmitAsync("P1", "move", move, moveRaw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(moved.State), MatchStateHasher.Hash(duplicate.State));
        Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync("P1", "stale-move", move, moveRaw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(moved.State), MatchStateHasher.Hash(stale.State));
        var done = moved;
        foreach (var name in new[] { "pass-1", "pass-2" }) {
            var actor = done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            var raw = Raw(name, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            done = await session.SubmitAsync(actor, name, Assert.IsType<PassPriorityCommand>(GameCommandJsonMapper.Map(raw)), raw, default);
            Assert.True(done.Accepted, done.ErrorMessage);
        }
        Assert.Equal(equipped ? -1 : 2, done.State.CardObjects["HOST"].Power);
        Assert.Equal(equipped ? 2 : 3, Assert.Single(done.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers).SnapshotMight);
        Assert.Equal(equipped ? 1 : 2, ArmamentMightTests.Might(done.State)); Assert.Equal("P1", done.State.FocusPlayerId);
        Assert.Equal(initial.RunePools["P2"], done.State.RunePools["P2"]);
        OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(3, commands.Count(c => c.Accepted)); Assert.Single(commands, c => !c.Accepted);
        Export("replay.json", new { native = dir is not null && File.Exists(Path.Combine(dir, "move-command.json")), commands = commands.Length,
            replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
