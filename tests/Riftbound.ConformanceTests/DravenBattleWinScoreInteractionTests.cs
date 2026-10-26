using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DravenBattleWinScoreInteractionTests
{
    private const string ScoreEffect = "CAPTURED_DRAVEN_FIRST_BATTLE_WIN_SCORE";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("SFD·148/221", true)]
    [InlineData("SFD·148/221", false)]
    [InlineData("SFD·148a/221", true)]
    [InlineData("SFD·148a/221", false)]
    public Task ChosenResultSkillOrderStopsAtTheNormalWinningPoint(string cardNo, bool scoreFirst)
        => WithEvidence(cardNo, "winning-order-" + scoreFirst, Position(cardNo, legend: true, score: 7), async run =>
        {
            await run.Battle("SOURCE", "DEFENDER");
            await run.Order(scoreFirst);
            await run.Settle();
            Assert.Equal("P1", run.State.WinnerPlayerId);
            Assert.Equal(8, run.State.PlayerScores["P1"]);
            Assert.Equal(scoreFirst ? 0 : 1, run.Events.Count(e => e.Kind == "CARD_DRAWN"));
            Assert.Equal(scoreFirst ? 0 : 1, run.State.PlayerZones["P1"].Hand.Count(id => id.StartsWith("DRAW-", StringComparison.Ordinal)));
            Assert.DoesNotContain(run.Events, e => e.Kind is "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED" or "BURNOUT_APPLIED");
            Assert.Equal("P2", run.State.CardObjects["FIELD"].ControllerId);
            Assert.Single(run.Events, e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), ScoreEffect));
            Assert.True(run.Events.FindIndex(e => e.Kind == "SCORE_GAINED") < run.Events.FindIndex(e => e.Kind == "MATCH_WON"));
            await run.Act("P1", new EndTurnCommand(), accepted: false);
            Assert.Single(run.Events, e => e.Kind == "MATCH_WON");
        });

    [Theory]
    [InlineData("SFD·148/221", 0)]
    [InlineData("SFD·148/221", 7)]
    [InlineData("SFD·148a/221", 0)]
    [InlineData("SFD·148a/221", 7)]
    public Task ActualReturnResponsePreservesTheEarnedPointAndItsCapturedController(string cardNo, int score)
        => WithEvidence(cardNo, "return-" + score, Position(cardNo, legend: true, score: score), async run =>
        {
            await run.Battle("SOURCE", "DEFENDER");
            await run.Order(scoreFirst: true);
            var birthGeneration = run.State.CardObjects["SOURCE"].ObjectGeneration;
            await run.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"]));
            var response = run.State.StackItems[^1].StackItemId;
            for (var i = 0; run.State.StackItems.Any(s => s.StackItemId == response); i++)
            {
                Assert.InRange(i, 0, 3);
                await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            }
            Assert.Contains("SOURCE", run.State.PlayerZones["P1"].Hand);
            Assert.True(run.State.CardObjects["SOURCE"].ObjectGeneration > birthGeneration);
            Assert.Equal(score, run.State.PlayerScores["P1"]);
            // Preserve the actual response state even while the consumer is not
            // wired. The final assertions still require the Draven skill.
            var publicView = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(run.State)["P2"], Json);
            var scoreViews = publicView.GetProperty("stack").EnumerateArray()
                .Where(s => s.GetProperty("cardNo").GetString() == cardNo).ToArray();
            run.Probes.Add(new { kind = "after-return-opponent-view", publicView });
            await run.Settle();
            Assert.Equal(score + 1, run.State.PlayerScores["P1"]);
            Assert.Equal(score == 7 ? "P1" : null, run.State.WinnerPlayerId);
            Assert.Equal("HIDDEN", Assert.Single(scoreViews).GetProperty("sourceObjectId").GetString());
            Assert.DoesNotContain(run.Events, e => e.Kind is "BATTLEFIELD_CONQUERED" or "BURNOUT_APPLIED");
            Assert.Single(run.Events, e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "SOURCE"));
            Assert.Equal(score == 7 ? 0 : 1, run.Events.Count(e => e.Kind == "CARD_DRAWN"));
            Assert.Equal(score == 7 ? 0 : 1, run.Events.Count(e => e.Kind == "BATTLE_CLOSED"));
        });

    [Theory]
    [InlineData("SFD·148/221")]
    [InlineData("SFD·148a/221")]
    public Task QuickCounterCannotCounterAnUntargetedScoreSkillOrConsumeResources(string cardNo)
        => WithEvidence(cardNo, "illegal-counter", Position(cardNo), async run =>
        {
            await run.Battle("SOURCE", "DEFENDER");
            var skill = Assert.Single(run.State.StackItems, s => s.EffectKind == ScoreEffect);
            Assert.Empty(skill.TargetObjectIds);
            Assert.Equal(0, run.State.PlayerScores["P1"]);
            Assert.Contains(run.State.UntilEndOfTurnEffects,
                marker => marker.StartsWith("DRAVEN_FIRST_BATTLE_WIN:", StringComparison.Ordinal));
            if (run.State.PriorityPlayerId == "P1") await run.Act("P1", new PassPriorityCommand());
            Assert.Equal("P2", run.State.PriorityPlayerId);
            await run.Act("P2", new PlayCardCommand("COUNTER", "SFD·045/221", [skill.StackItemId]), accepted: false);
            Assert.Contains("COUNTER", run.State.PlayerZones["P2"].Hand);
            await run.Settle();
            Assert.Equal(2, run.State.PlayerScores["P1"]); // Skill point, then ordinary conquest.
            Assert.DoesNotContain(run.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
            Assert.Single(run.Events, e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), ScoreEffect));
        });

    [Theory]
    [InlineData("SFD·148/221")]
    [InlineData("SFD·148a/221")]
    public Task TwoWinsInOneTurnAndOneAfterActualTurnChangesScoreOnlyTwice(string cardNo)
        => WithEvidence(cardNo, "three-battles-two-turns", Position(cardNo, defending: true), async run =>
        {
            await run.Battle("ATTACKER1", "SOURCE");
            await run.Settle();
            await run.Battle("ATTACKER2", "SOURCE");
            await run.Settle();
            var firstTurnScores = run.Events.Count(e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), ScoreEffect));
            var generation = run.State.CardObjects["SOURCE"].ObjectGeneration;
            await run.Act("P1", new EndTurnCommand());
            await run.Settle();
            Assert.Equal("P2", run.State.TurnPlayerId);
            await run.Act("P2", new EndTurnCommand());
            await run.Settle();
            Assert.Equal("P1", run.State.TurnPlayerId);
            Assert.Equal(7, run.State.TurnNumber);
            await run.Battle("ATTACKER3", "SOURCE");
            await run.Settle();
            Assert.Equal(generation, run.State.CardObjects["SOURCE"].ObjectGeneration);
            Assert.Equal(3, run.Events.Count(e => e.Kind == "BATTLE_CLOSED"));
            Assert.Equal(1, firstTurnScores);
            Assert.Equal(2, run.Events.Count(e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), ScoreEffect)));
            Assert.Null(run.State.WinnerPlayerId);
            Assert.DoesNotContain(run.Events, e => e.Kind is "BATTLEFIELD_CONQUERED" or "BURNOUT_APPLIED");
        });

    private static MatchState Position(string cardNo, bool legend = false, int score = 0, bool defending = false)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, field);
        }
        Put("SOURCE", cardNo, defending ? "P2" : "P1", defending ? "BATTLEFIELD" : "BASE", defending ? "FIELD" : null);
        Put("FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        if (defending)
            for (var i = 1; i <= 3; i++) Put("ATTACKER" + i, "OGN·175/298", "P1", "BASE");
        else Put("DEFENDER", "OGN·175/298", "P2", "BATTLEFIELD", "FIELD");
        Put("RETURN", "OGN·104/298", "P1", "HAND");
        Put("COUNTER", "SFD·045/221", "P2", "HAND");
        if (legend) Put("LEGEND", "SFD·185/221", "P1", "LEGEND");
        var zones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = defending ? ["ATTACKER1", "ATTACKER2", "ATTACKER3"] : ["SOURCE"],
                Hand = ["RETURN"], LegendZone = legend ? ["LEGEND"] : [] },
            ["P2"] = PlayerZones.Empty with { Battlefields = defending ? ["FIELD", "SOURCE"] : ["FIELD", "DEFENDER"], Hand = ["COUNTER"] }
        };
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 8).Select(i => "DRAW-" + player + "-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = zones[player] with { MainDeck = deck };
        }
        return new MatchState("DRAVEN-INTERACTION", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = score, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) },
            seed: 424242) with { CardPlays = new(5, [], []) };
    }

    private static async Task WithEvidence(string cardNo, string scenario, MatchState initial, Func<Run, Task> action)
    {
        var caseId = (cardNo + "-" + scenario).Replace('·', '-').Replace('/', '-');
        var run = new Run(initial with { RoomId = "DRAVEN-INTERACTION-" + caseId });
        string? failure = null;
        try { await action(run); }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; throw; }
        finally { await run.Save(cardNo, scenario, caseId, failure); }
    }

    private sealed class Run
    {
        private readonly MatchState initial;
        private readonly Journal journal = new();
        private MatchSession? session;
        private readonly List<MatchState> states = [];
        public MatchState State { get; private set; }
        public List<object> Probes { get; } = [];
        public List<GameEvent> Events => journal.Entries.SelectMany(e => e.Events).ToList();

        public Run(MatchState initial)
        {
            this.initial = initial;
            State = initial;
        }

        public async Task Act(string player, GameCommand command, bool accepted = true)
        {
            session ??= new(initial, new CoreRuleEngine(), journal);
            var before = JsonSerializer.Serialize(State, Json);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = State.Tick;
            if (!accepted && State.Status == MatchStatuses.Finished)
            {
                var count = journal.Entries.Count;
                string Views() => JsonSerializer.Serialize(State.Seats.Keys.OrderBy(x => x, StringComparer.Ordinal)
                    .Select(p => new { snapshot = session.SnapshotFor(p), prompt = session.PromptFor(p) }), Json);
                var beforeViews = Views();
                var error = await Assert.ThrowsAsync<MatchSessionException>(() => session.SubmitAsync(player,
                    "draven-interaction-" + count, command, JsonSerializer.SerializeToElement(raw, Json), default).AsTask());
                Assert.Equal(ErrorCodes.MatchFinished, error.Code);
                Assert.Equal(count, journal.Entries.Count);
                Assert.Equal(before, JsonSerializer.Serialize(State, Json));
                Assert.Equal(beforeViews, Views());
                Probes.Add(new { kind = "finished-session-rejection", player, command, error.Code,
                    stateAndBothViewsUnchanged = true, journalUnchanged = true });
                return;
            }
            var result = await session.SubmitAsync(player, "draven-interaction-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw, Json), default);
            State = result.State;
            states.Add(State);
            Assert.True(result.Accepted == accepted, result.ErrorMessage ?? "Unexpected accepted command");
            if (!accepted)
            {
                Assert.Equal(before, JsonSerializer.Serialize(State, Json));
                Probes.Add(new { kind = "rejected-input", player, command, result.ErrorMessage, stateUnchanged = true });
            }
        }

        public async Task Battle(string attacker, string defender)
        {
            await Act("P1", new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:FIELD", []));
            for (var i = 0; i < 4 && State.FocusPlayerId is not null; i++)
                await Act(State.FocusPlayerId, new PassFocusCommand());
            await Act("P1", new DeclareBattleCommand("FIELD", [attacker], [defender], ["COMBAT_ASSIGNMENT"]));
        }

        public async Task Order(bool scoreFirst)
        {
            if (State.TriggerQueue.Count > 1)
            {
                var ids = scoreFirst ? new[] { "SOURCE", "LEGEND" } : new[] { "LEGEND", "SOURCE" };
                await Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids
                    .Select(id => Assert.Single(State.TriggerQueue, t => t.SourceObjectId == id).TriggerId).ToArray()));
            }
        }

        public async Task Settle()
        {
            for (var i = 0; State.StackItems.Count > 0 && State.Status == MatchStatuses.InProgress; i++)
            {
                Assert.InRange(i, 0, 15);
                Assert.NotNull(State.PriorityPlayerId);
                await Act(State.PriorityPlayerId!, new PassPriorityCommand());
            }
        }

        public async Task Save(string cardNo, string scenario, string caseId, string? failure)
        {
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
                e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence,
                e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
                new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            Assert.Equal(commands.Length, states.Count);
            for (var i = 0; i < commands.Length; i++)
            {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(),
                    states[i], new CoreRuleEngine(), default,
                    events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            }
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_INTERACTION_EVIDENCE");
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(output, new { caseId, cardNo, scenario, initial, states, probes = Probes,
                entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, Json);
        }
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
