using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LeonaMightNativeTests
{
    [Theory]
    [InlineData("source-move")]
    [InlineData("stun-equipment")]
    public async Task ProductionMovePlayAndResponseControlsRecomputeTheLiveAuraAndReplay(string branch)
    {
        var stun = branch == "stun-equipment"; var target = stun ? "ENEMY1" : "HOST";
        var card = stun ? "OGN·262/298" : "OGS·011/024";
        var initial = LeonaStunnedMightTests.Board(stun ? "OGN·079a/298" : "OGN·079/298", stun ? 8 : 3, stunned: !stun);
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "SPELL", card, "P1");
        if (stun) {
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEAR"] = initial.CardObjects["GEAR"] with { OwnerId = "P2", ControllerId = "P2", AttachedToObjectId = "ENEMY1" } } };
            initial = LocalDestructionRecallTests.At(initial, "GEAR", "P2", "BATTLEFIELD", "F");
        }
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_LEONA_EVIDENCE"); var dir = root is null ? null : Path.Combine(root, branch);
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
        Export("selection.json", new { sourceObjectId = "SPELL", cardNo = card, targetObjectIds = new[] { target } });
        var prompt = session.PromptFor("P1"); Export("move-prompt.json", prompt);
        var moveRaw = Raw("move", new { cmdType = "MOVE_UNIT", sourceObjectId = "HOST", origin = "BASE", destination = "BATTLEFIELD:F",
            optionalCosts = Array.Empty<string>(), promptId = prompt.PromptId, snapshotTick = initial.Tick });
        var move = Assert.IsType<MoveUnitCommand>(GameCommandJsonMapper.Map(moveRaw));
        Assert.Equal("HOST", move.SourceObjectId); Assert.Contains(move.Origin, new[] { "", "BASE" }); Assert.Equal("BATTLEFIELD:F", move.Destination);
        var moved = await session.SubmitAsync("P1", "move", move, moveRaw, default); Assert.True(moved.Accepted, moved.ErrorMessage);
        Assert.Equal(stun ? 10 : 1, LeonaStunnedMightTests.Might(moved.State));
        var duplicate = await session.SubmitAsync("P1", "move", move, moveRaw, default);
        Assert.True(duplicate.Accepted); Assert.Equal(MatchStateHasher.Hash(moved.State), MatchStateHasher.Hash(duplicate.State)); Assert.Single(journal.Entries);
        var stale = await session.SubmitAsync("P1", "stale-move", move, moveRaw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(moved.State), MatchStateHasher.Hash(stale.State));
        prompt = session.PromptFor("P1"); Export("play-prompt.json", prompt);
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, moved.State.Tick, new("SPELL", card, [target]));
        if (dir is not null && File.Exists(Path.Combine(dir, "play-preview.json")))
            request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir, "play-preview.json")), json)!;
        Assert.Equal("SPELL", request.Command.SourceObjectId); Assert.Equal(card, request.Command.CardNo); Assert.Equal(new[] { target }, request.Command.TargetObjectIds);
        var quote = engine.PreviewPlayCard(moved.State, "P1", request); Assert.True(quote.CanPay, quote.Message); Export("play-quote.json", quote);
        var playRaw = Raw("play", new { cmdType = "PLAY_CARD", sourceObjectId = "SPELL", cardNo = card, targetObjectIds = new[] { target },
            promptId = prompt.PromptId, snapshotTick = moved.State.Tick });
        var play = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(playRaw));
        Assert.Equal("SPELL", play.SourceObjectId); Assert.Equal(card, play.CardNo); Assert.Equal(new[] { target }, play.TargetObjectIds);
        var done = await session.SubmitAsync("P1", "play", play, playRaw, default); Assert.True(done.Accepted, done.ErrorMessage);
        foreach (var name in new[] { "pass-1", "pass-2" }) {
            var actor = done.State.PriorityPlayerId!; prompt = session.PromptFor(actor); Export(name + "-prompt.json", prompt);
            var passRaw = Raw(name, new { cmdType = "PASS_PRIORITY", promptId = prompt.PromptId, snapshotTick = done.State.Tick });
            done = await session.SubmitAsync(actor, name, Assert.IsType<PassPriorityCommand>(GameCommandJsonMapper.Map(passRaw)), passRaw, default);
            Assert.True(done.Accepted, done.ErrorMessage);
        }
        Assert.Equal(stun ? 2 : 3, LeonaStunnedMightTests.Might(done.State)); Assert.Equal(stun ? 8 : 3, done.State.CardObjects["ENEMY1"].Power);
        Assert.Empty(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(stun ? 1 : 0, done.State.ContinuousEffects.Count(e => e.EffectKind == StaticAuraKinds.SameLocationEnemyFilteredPower));
        OfficialGraveyardRecastTests.Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, done.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors)); Assert.Equal(4, commands.Count(c => c.Accepted)); Assert.Single(commands, c => !c.Accepted);
        Export("replay.json", new { native = dir is not null && File.Exists(Path.Combine(dir, "play-command.json")), commands = commands.Length,
            replay, finalHash = MatchStateHasher.Hash(done.State) });
        Export("result-snapshot.json", done.Snapshots["P1"]);
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
