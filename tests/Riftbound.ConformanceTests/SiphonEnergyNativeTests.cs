using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SiphonEnergyNativeTests
{
    [Theory]
    [InlineData("aura-floor")]
    [InlineData("enemy-only")]
    public async Task ProductionSelectedBattlefieldCommandsPayResolveAndReplay(string branch)
    {
        var initial = await SiphonEnergyTests.Board();
        if (branch == "aura-floor") {
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["D"] = initial.CardObjects["D"] with { Power = 1 } } };
            initial = LocalDestructionRecallTests.At(DeathObserverAuditTests.Source(initial, "OGS·013/024", "GAREN"),
                "GAREN", "P2", "BATTLEFIELD", "BF");
        }
        initial = OfficialInsightAndSpellLockTests.Restore(initial);
        string[] targets = [branch == "enemy-only" ? "BF2" : "BF"];
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_SIPHON_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        var prompt = session.PromptFor("P1"); Export("prompt.json", prompt);
        Export("snapshot.json", ResolutionResult.BuildSnapshots(initial)["P1"]);
        var expected = SiphonEnergyTests.Command(targets);
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick, expected);
        var native = dir is not null && File.Exists(Path.Combine(dir, "command.json"));
        if (native) request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!, "preview.json")), json)!;
        var quote = engine.PreviewPlayCard(initial, "P1", request);
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message); Export("quote.json", quote);
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "PLAY_CARD", sourceObjectId = "AOE",
            cardNo = SiphonEnergyTests.Spell, targetObjectIds = targets, promptId = prompt.PromptId, snapshotTick = initial.Tick });
        if (native) {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!, "command.json")));
            raw = document.RootElement.Clone();
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString());
            Assert.Equal(initial.Tick, raw.GetProperty("snapshotTick").GetInt64());
        }
        var command = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Equal(expected.SourceObjectId, command.SourceObjectId); Assert.Equal(expected.CardNo, command.CardNo);
        Assert.Equal(targets, command.TargetObjectIds); Assert.Equal(targets, request.Command.TargetObjectIds);
        var result = await session.SubmitAsync("P1", "native-siphon", command, raw, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var duplicate = await session.SubmitAsync("P1", "native-siphon", command, raw, default);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
        var stale = await session.SubmitAsync("P1", "stale-siphon", command, raw, default);
        Assert.False(stale.Accepted); Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(stale.State));
        for (var i = 0; i < 2; i++) {
            var pass = new PassPriorityCommand();
            result = await session.SubmitAsync(result.State.PriorityPlayerId!, "pass-" + i, pass,
                JsonSerializer.SerializeToElement(new { cmdType = "PASS_PRIORITY" }), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Equal(branch == "aura-floor" ? 0 : 4, result.State.CardObjects["D"].Power);
        Assert.Equal(branch == "enemy-only" ? 3 : 4, result.State.CardObjects["D2"].Power);
        Assert.Equal(4, result.State.CardObjects["D3"].Power);
        Assert.Equal(initial.CardObjects["A"].Power + (branch == "aura-floor" ? 1 : 0), result.State.CardObjects["A"].Power);
        Assert.Equal(18, result.State.RunePools["P1"].Mana); Assert.Equal(19, result.State.RunePools["P1"].TotalPower);
        var affected = branch == "aura-floor" ? "D" : "D2";
        Assert.Equal(branch == "aura-floor" ? 2 : 4, Assert.Single(result.State.CardObjects[affected].UntilEndOfTurnPowerModifiers).SnapshotMight);
        Assert.Empty(MinimumMightAuditTests.PublicErrors(result.State));
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
        Export("result-snapshot.json", result.Snapshots["P1"]);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
            e.CompletedTick, i, ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, result.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Export("replay.json", new { native, replay, commands = commands.Length,
            chosenTargets = targets, finalHash = MatchStateHasher.Hash(result.State) });
    }

    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
