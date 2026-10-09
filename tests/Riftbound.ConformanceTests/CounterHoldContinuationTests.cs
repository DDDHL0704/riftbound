using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CounterHoldContinuationTests
{
    [Theory]
    [InlineData("counter")]
    [InlineData("ornn")]
    [InlineData("reinforcements")]
    public async Task ProductionCommandsRecoverAndReplayEveryContinuation(string scenario)
    {
        var state = scenario switch {
            "counter" => OfficialCounterRepeatTests.Position("OGN·064/298"),
            "ornn" => OfficialOrnnHoldTests.Position("SFD·058/221"),
            _ => OfficialReinforcementsTests.Position("SFD·125/221") };
        var engine = new CoreRuleEngine();
        var journal = new Journal();
        var session = new MatchSession(state, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_CONTINUATION_EVIDENCE");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value)
        {
            if (string.IsNullOrEmpty(root)) return;
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, name), JsonSerializer.Serialize(value, value.GetType(), json));
        }
        GameCommand NativeOrDefault(string name, GameCommand fallback)
        {
            if (string.IsNullOrEmpty(root) || !File.Exists(Path.Combine(root, name))) return fallback;
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name)));
            return GameCommandJsonMapper.Map(document.RootElement);
        }
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var result = await session.SubmitAsync(player, scenario + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(restored));
            var errors = MatchRecoveryValidator.Validate(restored.RoomId, 0, [], [], new Dictionary<string,RecoveredPlayerView>(), restored, restored.Tick);
            Assert.True(errors.Count == 0, string.Join("; ", errors));
            return result;
        }
        ResolutionResult result;
        if (scenario == "counter")
        {
            var command = new PlayCardCommand("C", "OGN·064/298", ["S1"], OptionalCosts:["ECHO"], RepeatChoices:[new("", ["S2"])]);
            Export("counter-candidate.json", ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.PlayCard));
            Export("counter-quote.json", engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)));
            result = await Submit("P1", NativeOrDefault("native-counter-command.json", command));
        }
        else result = await Submit("P1", scenario == "ornn" ? new PassPriorityCommand() : new PlayCardCommand("CARD", "OGN·062/298", []));
        for (var step=0;step<12;step++)
        {
            if (result.State.PendingCardChoice is { } choice)
            {
                Export("ornn-prompt.json", result.Prompts["P1"]);
                result = await Submit("P1", NativeOrDefault("native-ornn-command.json", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["E2"])));
            }
            else if (result.State.PendingEffectPlay is not null)
            {
                var command = new PlayCardCommand("U", "SFD·125/221", []);
                Export("reinforcements-prompt.json", result.Prompts["P1"]);
                Export("reinforcements-candidate.json", result.Prompts["P1"].Candidates!.Single(c => c.Action == CommandTypes.PlayCard));
                Export("reinforcements-quote.json", engine.PreviewPlayCard(result.State, "P1", PlayCostPreviewTests.Request(result.State, command)));
                result = await Submit("P1", NativeOrDefault("native-reinforcements-command.json", command));
            }
            else if (result.State.StackItems.Count > 0) result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
            else break;
        }
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingEffectPlay);
        Assert.Null(result.State.PendingCardChoice);
        if (scenario == "counter") Assert.Equal(["SPELL1","SPELL2"], result.State.PlayerZones["P2"].Graveyard);
        else if (scenario == "ornn") { Assert.Contains("E2", result.State.PlayerZones["P1"].Hand); Assert.Equal(MatchPhases.Main, result.State.Phase); }
        else Assert.Contains("U", result.State.PlayerZones["P1"].Base);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev,i) => new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors));
        Export(scenario + "-replay.json",replay);
    }

    [Fact]
    public async Task CounterTargetAlreadyRemovedCanRecoverButForgedBindingCannot()
    {
        var state = OfficialCounterRepeatTests.Position("OGN·064/298");
        var played = await new CoreRuleEngine().ResolveAsync(state,new("counter","P1",CommandTypes.PlayCard),new PlayCardCommand("C","OGN·064/298",["S1"]),default);
        Assert.True(played.Accepted,played.ErrorMessage);
        var gone = played.State with { StackItems=played.State.StackItems.Where(s=>s.StackItemId!="S1").ToArray(),
            PlayerZones=new Dictionary<string,PlayerZones>(played.State.PlayerZones) { ["P2"]=played.State.PlayerZones["P2"] with { Graveyard=["SPELL1"] } } };
        Assert.Empty(MatchRecoveryValidator.Validate(gone.RoomId,0,[],[],new Dictionary<string,RecoveredPlayerView>(),gone,gone.Tick));
        var stack = played.State.StackItems.ToArray();
        stack[^1] = stack[^1] with { TargetStackSources=new Dictionary<string,string> { ["S1"]="SPELL2" } };
        var forged=played.State with { StackItems=stack };
        Assert.Contains(MatchRecoveryValidator.Validate(forged.RoomId,0,[],[],new Dictionary<string,RecoveredPlayerView>(),forged,forged.Tick),e=>e.Contains("captured stack target identity"));
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; }=[];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry);return ValueTask.CompletedTask; }
    }
}
