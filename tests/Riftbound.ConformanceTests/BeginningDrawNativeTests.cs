using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class BeginningDrawNativeTests
{
    [Fact]
    public async Task ProductionReactionReducesHandBeforeJinxResolutionAndReplays()
    {
        var initial = OfficialHoldSequenceTests.AddUnit(BeginningDrawTests.Jinx(1), "E", "OGN·001/298", "F", "P1");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["FLASH"] = new("FLASH", cardNo: "OGS·011/024", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Hand = [..initial.PlayerZones["P1"].Hand, "FLASH"] } },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) { ["FLASH"] = new("P1", "HAND") } };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_BEGINNING_DRAW_EVIDENCE");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command) {
            var result = await session.SubmitAsync(player, "beginning-draw-native-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage); Restore(result.State); return result;
        }
        GameCommand Native(string name, ResolutionResult state, string player, GameCommand fallback) {
            var prompt = session.PromptFor(player); Export(name + "-prompt.json", prompt); Export(name + "-snapshot.json", state.Snapshots[player]);
            var path = root is null ? null : Path.Combine(root, name + "-command.json");
            if (path is null || !File.Exists(path)) return fallback;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(prompt.PromptId, doc.RootElement.GetProperty("promptId").GetString());
            Assert.Equal(state.State.Tick, doc.RootElement.GetProperty("snapshotTick").GetInt64()); native++;
            return GameCommandJsonMapper.Map(doc.RootElement);
        }
        var result = await Submit("P1", new PassPriorityCommand());
        Assert.Single(result.State.StackItems); Assert.Equal(2, result.State.PlayerZones["P1"].Hand.Count);
        var play = new PlayCardCommand("FLASH", "OGS·011/024", ["E"]);
        var prompt = session.PromptFor("P1"); var previewPath = root is null ? null : Path.Combine(root,"reaction-preview.json");
        var request = previewPath is not null && File.Exists(previewPath)
            ? JsonSerializer.Deserialize<PlayCostPreviewRequestDto>(File.ReadAllText(previewPath),json)!
            : new("reaction",prompt.PromptId!,result.State.Tick,play);
        Assert.Equal("FLASH", request.Command.SourceObjectId); Assert.Equal(new[]{"E"},request.Command.TargetObjectIds);
        var quote = engine.PreviewPlayCard(result.State,"P1",request);
        Assert.True(quote.IsValid && quote.CanPay,quote.Message); Export("reaction-quote.json",quote);
        result = await Submit("P1",Native("reaction",result,"P1",play));
        Assert.Equal(2,result.State.StackItems.Count);
        foreach (var name in new[]{"pass-reaction-1","pass-reaction-2"}) {
            var player = result.State.PriorityPlayerId!;
            result = await Submit(player,Native(name,result,player,new PassPriorityCommand()));
        }
        Assert.Contains("E",result.State.PlayerZones["P1"].Base); Assert.Single(result.State.StackItems); Assert.Single(result.State.PlayerZones["P1"].Hand);
        foreach (var name in new[]{"pass-jinx-1","pass-jinx-2"}) {
            var player = result.State.PriorityPlayerId!;
            result = await Submit(player,Native(name,result,player,new PassPriorityCommand()));
        }
        Assert.Contains("E",result.State.PlayerZones["P1"].Base); Assert.Equal(3,result.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(MatchPhases.Main,result.State.Phase); Assert.Empty(result.State.StackItems);
        var commands = journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage) { ErrorCode = e.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("result-snapshot.json",result.Snapshots["P1"]);
        Export("replay.json",new {nativeCommands=native,commands=commands.Length,replay});
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries {get;}=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry);return ValueTask.CompletedTask; }
    }
}
