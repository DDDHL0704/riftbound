using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DravenDeathAndWinOrderTests
{
    [Theory]
    [InlineData("SFD·148/221", 0)]
    [InlineData("SFD·148/221", 7)]
    [InlineData("SFD·148a/221", 0)]
    [InlineData("SFD·148a/221", 7)]
    public async Task SameControllerCannotReverseSuccessiveDeathAndResultInstructions(string printing, int score)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("DIES", printing, "P1", "BASE"); Put("WINS", printing, "P1", "BASE");
        Put("DRAGON", "OGN·142/298", "P2", "BATTLEFIELD", "FIELD");
        Put("FIELD", "OGN·278/298", "P2", "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        var initial = new MatchState("SAME-CONTROLLER-DEATH-WIN", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Base = ["DIES", "WINS"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "DRAGON"] } },
            cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = score, ["P2"] = score },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty }, seed: 424242)
            with { CardPlays = new(5, [], []) };
        var journal = new Journal(); var state = initial; var states = new List<MatchState>();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await Act("P1", new MoveUnitCommand("DIES", "BASE", "BATTLEFIELD:FIELD", [], ["DIES", "WINS"]));
        await Act("P1", new PassFocusCommand()); await Act("P2", new PassFocusCommand());
        await Act("P1", new DeclareBattleCommand("FIELD", ["DIES", "WINS"], ["DRAGON"], ["COMBAT_ASSIGNMENT"]));
        var battle = state.BattleState.BattleId!;
        await Act("P1", new AssignCombatDamageCommand(battle, "FIELD", [new("DIES", "DRAGON", 6), new("WINS", "DRAGON", 6)]));
        await Act("P2", new AssignCombatDamageCommand(battle, "FIELD", [new("DRAGON", "DIES", 6), new("DRAGON", "WINS", 4)]));
        Assert.Contains("DIES", state.PlayerZones["P1"].Graveyard);
        Assert.Contains("WINS", state.PlayerZones["P1"].Battlefields);
        Assert.Empty(state.TriggerQueue);
        Assert.All(state.StackItems, i => Assert.Equal("P1", i.ControllerId));
        var death = Assert.Single(state.StackItems);
        Assert.Equal("DIES", death.SourceObjectId); Assert.NotNull(death.DravenBattleDeathScore);
        Assert.Null(death.DravenBattleWinScore);
        Assert.Equal("CLEANUP_TRIGGERS", state.PendingBattleResult!.Stage);
        Assert.DoesNotContain(journal.Entries.SelectMany(e => e.Events), e => e.Kind is
            "BATTLE_RESULT_DETERMINED" or "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED");
        Assert.DoesNotContain(CommandTypes.OrderTriggers, session.PromptFor("P1").EnabledActions());
        await Act("P1", new OrderTriggersCommand(OrderedTriggerIds: [death.StackItemId]), false);
        var choice = state.PendingCardChoice!;
        await Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2"]));
        // CN 468.2 completes death/cleanup skills before the result task can create WINS's skill.
        for (var i = 0; state.Status == MatchStatuses.InProgress && state.StackItems.Any(s => s.DravenBattleDeathScore is not null); i++)
        { Assert.InRange(i, 0, 8); await Act(state.PriorityPlayerId!, new PassPriorityCommand()); }
        if (score == 0)
        {
            var win = Assert.Single(state.StackItems);
            Assert.Equal("WINS", win.SourceObjectId); Assert.NotNull(win.DravenBattleWinScore);
            Assert.Null(win.DravenBattleDeathScore);
            Assert.Equal("RESULT_TRIGGERS", state.PendingBattleResult!.Stage);
            Assert.Empty(state.TriggerQueue);
            Assert.DoesNotContain(CommandTypes.OrderTriggers, session.PromptFor("P1").EnabledActions());
        }
        for (var i = 0; state.Status == MatchStatuses.InProgress && state.StackItems.Count > 0; i++)
        { Assert.InRange(i, 0, 8); await Act(state.PriorityPlayerId!, new PassPriorityCommand()); }
        var events = journal.Entries.SelectMany(e => e.Events).ToArray();
        var skillScores = events.Where(e => e.Kind == "SCORE_GAINED"
            && e.Payload.GetValueOrDefault("reason") is string reason && reason.StartsWith("CAPTURED_DRAVEN_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(score == 7 ? 1 : 2, skillScores.Length);
        Assert.Equal("P2", skillScores[0].Payload["playerId"]);
        Assert.Equal("DIES", skillScores[0].Payload["sourceObjectId"]);
        if (score == 7)
        {
            Assert.Equal("P2", state.WinnerPlayerId); Assert.Equal(7, state.PlayerScores["P1"]); Assert.Equal(8, state.PlayerScores["P2"]);
            Assert.DoesNotContain(events, e => e.Kind is "BATTLE_RESULT_DETERMINED" or "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED");
        }
        else
        {
            Assert.Equal("P1", skillScores[1].Payload["playerId"]); Assert.Equal("WINS", skillScores[1].Payload["sourceObjectId"]);
            Assert.Equal(2, state.PlayerScores["P1"]); Assert.Equal(1, state.PlayerScores["P2"]);
            Assert.Null(state.WinnerPlayerId); Assert.Single(events, e => e.Kind == "BATTLE_CLOSED");
            var result = Assert.Single(events, e => e.Kind == "BATTLE_RESULT_DETERMINED");
            Assert.True(Array.IndexOf(events, skillScores[0]) < Array.IndexOf(events, result));
            Assert.True(Array.IndexOf(events, result) < Array.IndexOf(events, skillScores[1]));
        }
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var recordedEvents = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        for (var i = 0; i < commands.Length; i++)
        {
            var prefix = commands.Take(i + 1).ToArray();
            var sequence = commands[i].CompletedEventSequence;
            var prefixEvents = recordedEvents.Where(e => e.Sequence <= sequence).ToArray();
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(), states[i],
                new CoreRuleEngine(), default, prefixEvents);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            var at = states[i]; var snapshots = ResolutionResult.BuildSnapshots(at); var prompts = ResolutionResult.BuildPrompts(at);
            var views = at.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, at.Tick, sequence,
                snapshots[p], at.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(at.RoomId, at.Tick, sequence, [], at);
            Assert.Empty(MatchRecoveryValidator.Validate(at.RoomId, sequence, prefix, prefixEvents, views, at, at.Tick, spectator));
            // This explicit midgame frame validates the real prefix, not a full legal opening.
            var frame = new MatchRecoveryFrame(at.RoomId, at.Tick, sequence, prefix, prefixEvents, views, [], at, initial, spectator)
                { OpeningSeed = initial.Seed };
            var cold = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, json), json)!;
            var restored = MatchSession.Restore(cold, new CoreRuleEngine(), NoopMatchJournal.Instance);
            foreach (var player in at.Seats.Keys)
            {
                Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
                Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
            }
        }
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_DEATH_WIN_ORDER_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            using var stream = new GZipStream(File.Open(Path.Combine(output, printing.Replace('·', '-').Replace('/', '-') + "-" + score + ".json.gz"), FileMode.CreateNew), CompressionLevel.SmallestSize);
            JsonSerializer.Serialize(stream, new { printing, score, initial, states, entries = journal.Entries, commands,
                events = recordedEvents, originalPrefixReplayCount = commands.Length }, json);
        }

        async Task Act(string actor, GameCommand command, bool accepted = true)
        {
            var before = JsonSerializer.Serialize(state, json);
            var beforeSnapshot = JsonSerializer.Serialize(session.SnapshotFor(actor), json);
            var beforePrompt = JsonSerializer.Serialize(session.PromptFor(actor), json);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = state.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, json);
            var result = await session.SubmitAsync(actor, "same-controller-" + journal.Entries.Count, GameCommandJsonMapper.Map(wire), wire, default);
            Assert.Equal(accepted, result.Accepted);
            if (!accepted)
            {
                Assert.Equal(before, JsonSerializer.Serialize(result.State, json)); Assert.Empty(result.Events);
                Assert.Equal(beforeSnapshot, JsonSerializer.Serialize(session.SnapshotFor(actor), json));
                Assert.Equal(beforePrompt, JsonSerializer.Serialize(session.PromptFor(actor), json));
            }
            state = result.State; states.Add(state);
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
