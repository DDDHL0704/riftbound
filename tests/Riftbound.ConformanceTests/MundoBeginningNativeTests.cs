using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
namespace Riftbound.ConformanceTests;
public sealed class MundoBeginningNativeTests
{
    [Fact]
    public async Task ProductionThreeCardRecycleResumesAndReplays()
    {
        var initial = MundoBeginningTests.Position();
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MUNDO_EVIDENCE");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; var native = 0;
        void Export(string name, object value) {
            if (root is null) return; Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command) {
            var result = await session.SubmitAsync(player, "mundo-native-" + journal.Entries.Count, command,
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
        Assert.Single(result.State.StackItems);
        foreach (var name in new[]{"pass-1","pass-2"}) {
            var player=result.State.PriorityPlayerId!;
            result=await Submit(player,Native(name,result,player,new PassPriorityCommand()));
        }
        var choice=result.State.PendingCardChoice!;Assert.Equal(3,choice.RequiredCount);Assert.Equal(5,choice.LegalObjectIds.Count);
        result=await Submit("P1",Native("recycle",result,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["G2","G4","G5"])));
        Assert.Equal(new[]{"G1","G3"},result.State.PlayerZones["P1"].Graveyard);Assert.Equal(8,MundoBeginningTests.Might(result.State));
        Assert.Equal(MatchPhases.Main,result.State.Phase);Assert.Null(result.State.PendingCardChoice);Assert.Empty(result.State.StackItems);
        var commands = journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
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
