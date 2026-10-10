using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MultitypeEquipmentNativeTests
{
    [Theory]
    [InlineData("destroy", "OGN·229/298")]
    [InlineData("power", "OGN·095/298")]
    [InlineData("damage", "UNL-073/219")]
    public async Task NativeUnitSelectionPaymentAndResolutionReplay(string branch, string spell)
    {
        var initial = OfficialInsightAndSpellLockTests.Restore(MultitypeEquipmentTests.PositionFor(spell));
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MULTITYPE_EVIDENCE");
        var dir = root is null ? null : Path.Combine(root, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) {
            if (dir is null) return; Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        var prompt = session.PromptFor("P1"); Export("prompt.json", prompt);
        Export("snapshot.json", ResolutionResult.BuildSnapshots(initial)["P1"]);
        var expected = new PlayCardCommand("AOE", spell, ["G1"]);
        var request = new PlayCostPreviewRequestDto("quote", prompt.PromptId!, initial.Tick, expected);
        var native = dir is not null && File.Exists(Path.Combine(dir, "command.json"));
        if (native) request = JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(Path.Combine(dir!, "preview.json")), json)!;
        var quote = engine.PreviewPlayCard(initial, "P1", request);
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message); Export("quote.json", quote);
        GameCommand command = expected;
        var raw = JsonSerializer.SerializeToElement(new { cmdType="PLAY_CARD", sourceObjectId="AOE", cardNo=spell, targetObjectIds=new[]{"G1"}, promptId=prompt.PromptId, snapshotTick=initial.Tick });
        if (native) {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!, "command.json")));
            raw = document.RootElement.Clone(); command = GameCommandJsonMapper.Map(raw);
            Assert.Equal(prompt.PromptId, raw.GetProperty("promptId").GetString());
            Assert.Equal(initial.Tick, raw.GetProperty("snapshotTick").GetInt64());
        }
        var play = Assert.IsType<PlayCardCommand>(command);
        Assert.Equal("AOE", play.SourceObjectId); Assert.Equal(spell, play.CardNo); Assert.Equal(["G1"], play.TargetObjectIds);
        Assert.Equal(play.SourceObjectId, request.Command.SourceObjectId); Assert.Equal(play.TargetObjectIds, request.Command.TargetObjectIds);
        var result = await session.SubmitAsync("P1", "native-play", command, raw, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var duplicate = await session.SubmitAsync("P1", "native-play", command, raw, default);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(duplicate.State));
        for (var i=0;i<2;i++) {
            var pass = new PassPriorityCommand();
            result = await session.SubmitAsync(result.State.PriorityPlayerId!, "pass-"+i, pass,
                JsonSerializer.SerializeToElement(new { cmdType="PASS_PRIORITY" }), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        if (branch == "destroy") Assert.Contains("G1",result.State.PlayerZones["P2"].Graveyard);
        if (branch == "power") Assert.Equal(4,result.State.CardObjects["G1"].Power);
        if (branch == "damage") Assert.Equal(3,result.State.CardObjects["G1"].Damage);
        OfficialGraveyardRecastTests.Restore(result.State);
        Export("result-snapshot.json", result.Snapshots["P1"]);
        var commands=journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events=journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay=await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("replay.json",new {native,replay,commands=commands.Length});
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) {Entries.Add(entry);return ValueTask.CompletedTask;}
    }
}
