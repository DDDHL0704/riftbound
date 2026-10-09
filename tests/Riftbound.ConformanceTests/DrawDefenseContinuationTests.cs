using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DrawDefenseContinuationTests
{
    [Theory]
    [InlineData("gem")]
    [InlineData("fan")]
    [InlineData("fan-decline")]
    public async Task NativeTargetCommandsRestoreAndReplayThroughBattleCompletion(string scenario)
    {
        var gem = scenario == "gem";
        var initial = gem ? OfficialDrawDefenseTests.Gem(0) : OfficialDrawDefenseTests.Fan();
        var journal = new Journal(); var engine = new CoreRuleEngine();
        var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_INSIGHT_SOURCE_EVIDENCE");
        JsonElement? suppliedRaw = null;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value)
        {
            if (string.IsNullOrEmpty(root)) return;
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var raw = suppliedRaw ?? JsonSerializer.SerializeToElement(command, command.GetType(), json);
            suppliedRaw = null;
            var result = await session.SubmitAsync(player, scenario+journal.Entries.Count, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); OfficialDrawDefenseTests.Restore(result.State); return result;
        }
        ResolutionResult result;
        if (gem)
        {
            result = await Submit("P1", new PlayCardCommand("C", "OGN·083/298", []));
            result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        else
        {
            result = await Submit("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
            result = await Submit("P1", new PassFocusCommand()); result = await Submit("P2", new PassFocusCommand());
            result = await Submit("P1", new DeclareBattleCommand("BF", ["A"], ["D"], ["COMBAT_ASSIGNMENT"]));
        }
        var choice = result.State.PendingCardChoice!;
        Export((gem ? "gem" : "fan") + "-prompt.json", result.Prompts[choice.PlayerId]);
        GameCommand command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, gem ? ["V"] : scenario == "fan" ? ["A"] : []);
        var path = string.IsNullOrEmpty(root) ? null : Path.Combine(root,"native-"+scenario+"-command.json");
        if (path is not null && File.Exists(path))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path)); command = GameCommandJsonMapper.Map(document.RootElement); suppliedRaw = document.RootElement.Clone();
        }
        result = await Submit(choice.PlayerId, command);
        for (var i=0; i<8 && result.State.PriorityPlayerId is { } player; i++) result = await Submit(player, new PassPriorityCommand());
        Assert.Empty(result.State.StackItems); Assert.Null(result.State.PendingCardChoice); Assert.False(result.State.BattleState.IsActive);
        if (gem) Assert.Equal(6, result.State.CardObjects["V"].Power);
        else if (scenario == "fan") Assert.Contains("A", result.State.PlayerZones["P1"].Base);
        var commands = journal.Entries.Select(e=>new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,
            e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e=>e.Events.Select((ev,i)=>new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export(scenario+"-replay.json", replay);
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
