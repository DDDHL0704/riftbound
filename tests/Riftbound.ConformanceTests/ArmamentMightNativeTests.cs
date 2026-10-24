using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class ArmamentMightNativeTests
{
    [Theory]
    [InlineData("long-sword", "SFD·022/221", "ASSEMBLE_RED")]
    [InlineData("world-atlas", "SFD·086/221", "ASSEMBLE_BLUE")]
    public async Task ProductionAssemblyProjectsCurrentMightAndReplays(string branch, string card, string cost)
    {
        var initial = ArmamentMightTests.Position(card);
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var evidenceRoot = Environment.GetEnvironmentVariable("RIFTBOUND_ARMAMENT_MIGHT_EVIDENCE");
        var root = evidenceRoot is null ? null : Path.Combine(evidenceRoot, branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command) {
            var result = await session.SubmitAsync(player, "armament-native-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage); Restore(result.State); ArmamentMightTests.PublicRecovery(result.State); return result;
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
        var result = new ResolutionResult(true,null,initial,[],ResolutionResult.BuildSnapshots(initial),ResolutionResult.BuildPrompts(initial));
        Assert.Equal(4,ArmamentMightTests.Might(initial));
        result = await Submit("P1",Native("assemble",result,"P1",new AssembleEquipmentCommand("GEAR","HOST",[cost])));
        Assert.Null(result.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(4,ArmamentMightTests.Might(result.State));
        result = await Submit("P1",Native("pass-1",result,"P1",new PassPriorityCommand()));
        Assert.Null(result.State.CardObjects["GEAR"].AttachedToObjectId);
        result = await Submit("P2",Native("pass-2",result,"P2",new PassPriorityCommand()));
        Assert.Equal("HOST",result.State.CardObjects["GEAR"].AttachedToObjectId);Assert.Equal(6,ArmamentMightTests.Might(result.State));
        Assert.Equal(4,result.State.CardObjects["HOST"].Power);
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
