using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DravenBattleWinMultiSourceTests
{
    private const string ScoreEffect = "CAPTURED_DRAVEN_FIRST_BATTLE_WIN_SCORE";

    public static IEnumerable<object[]> Cases()
    {
        foreach (var cardNo in new[] { "SFD·148/221", "SFD·148a/221" })
        foreach (var initialScore in new[] { 0, 7 })
        foreach (var scorePosition in new[] { 0, 1, 2 })
            yield return [cardNo, initialScore, scorePosition];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ThreeCapturedResultSkillsRespectChosenOrderAndNormalVictory(
        string cardNo, int initialScore, int scorePosition)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, field);
        }
        Put("DRAVEN", cardNo, "P1", "BASE");
        Put("NIDALEE", "UNL-114/219", "P1", "BASE");
        Put("LEGEND", "SFD·185/221", "P1", "LEGEND");
        Put("DEFENDER", "OGN·175/298", "P2", "BATTLEFIELD", "FIELD");
        Put("FIELD", "OGN·278/298", "P2", "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        var deck = Enumerable.Range(1, 4).Select(i => "PRIVATE-DRAW-" + i).ToArray();
        foreach (var id in deck) Put(id, "OGN·219/298", "P1", "MAIN_DECK");
        var caseId = (cardNo + "-" + initialScore + "-" + scorePosition).Replace('·', '-').Replace('/', '-');
        var initial = new MatchState("MULTI-WIN-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["DRAVEN", "NIDALEE"], LegendZone = ["LEGEND"], MainDeck = deck },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "DEFENDER"] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = initialScore, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var state = initial;
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var probes = new List<object>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string? failure = null;
        (string Player, string Intent, GameCommand Command, JsonElement Raw) last = default;
        try
        {
            await Act("P1", new MoveUnitCommand("DRAVEN", "BASE", "BATTLEFIELD:FIELD", [], ["DRAVEN", "NIDALEE"]));
            await Act("P1", new PassFocusCommand());
            await Act("P2", new PassFocusCommand());
            await Act("P1", new DeclareBattleCommand("FIELD", ["DRAVEN", "NIDALEE"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
            Assert.Contains(CommandTypes.AssignCombatDamage, session.PromptFor("P1").EnabledActions());
            var battleId = state.BattleState.BattleId!;
            await Act("P1", new AssignCombatDamageCommand(battleId, "FIELD", [new("DRAVEN", "DEFENDER", 6), new("NIDALEE", "DEFENDER", 4)]));
            await Act("P2", new AssignCombatDamageCommand(battleId, "FIELD", [new("DEFENDER", "NIDALEE", 3)]));
            Assert.Equal(new[] { "DRAVEN", "LEGEND", "NIDALEE" }, state.TriggerQueue.Select(t => t.SourceObjectId).Order().ToArray());
            Assert.All(state.TriggerQueue, t => Assert.Equal("P1", t.ControllerId));
            Assert.Empty(state.StackItems);
            Assert.Contains("DRAVEN", state.PlayerZones["P1"].Battlefields);
            Assert.Contains("NIDALEE", state.PlayerZones["P1"].Battlefields);
            Assert.Contains("DEFENDER", state.PlayerZones["P2"].Graveyard);
            Assert.Equal(initialScore, state.PlayerScores["P1"]);
            Assert.Equal("P2", state.CardObjects["FIELD"].ControllerId);
            Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith("DRAVEN_FIRST_BATTLE_WIN:", StringComparison.Ordinal));
            var order = new List<string> { "NIDALEE", "LEGEND" };
            order.Insert(scorePosition, "DRAVEN");
            var ids = order.Select(id => Assert.Single(state.TriggerQueue, t => t.SourceObjectId == id).TriggerId).ToArray();
            await Act("P2", new OrderTriggersCommand(OrderedTriggerIds: ids), false);
            await Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids.Take(2).ToArray()), false);
            await Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
            Assert.Equal(order[0], state.StackItems[^1].SourceObjectId);
            var ordered = JsonSerializer.Serialize(state, json);
            var journalCount = journal.Entries.Count;
            Assert.NotNull(last.Player);
            Assert.NotNull(last.Intent);
            Assert.NotNull(last.Command);
            var duplicate = await session.SubmitAsync(last.Player, last.Intent, last.Command, last.Raw, default);
            Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
            Assert.Equal(ordered, JsonSerializer.Serialize(duplicate.State, json));
            Assert.Equal(journalCount, journal.Entries.Count);
            probes.Add(new { kind = "idempotent-order", stateAndJournalUnchanged = true });
            for (var i = 0; state.Status == MatchStatuses.InProgress && state.StackItems.Count > 0; i++)
            {
                Assert.InRange(i, 0, 15);
                Assert.NotNull(state.PriorityPlayerId);
                await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            }
            var events = journal.Entries.SelectMany(e => e.Events).ToList();
            var expectedDraws = initialScore == 7 ? scorePosition : 2;
            var resolved = events.Where(e => e.Kind is "UNIT_TRIGGER_RESOLVED" or "LEGEND_TRIGGER_RESOLVED").ToArray();
            var expectedOrder = initialScore == 7 ? order.Take(scorePosition + 1) : order;
            Assert.Equal(expectedOrder, resolved.Select(e => (string)(e.Payload.GetValueOrDefault("legendObjectId") ?? e.Payload["sourceObjectId"])!));
            Assert.Equal(expectedDraws, events.Count(e => e.Kind == "CARD_DRAWN"));
            Assert.Equal(deck.Take(expectedDraws), state.PlayerZones["P1"].Hand);
            Assert.Single(events, e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), ScoreEffect));
            Assert.DoesNotContain(events, e => e.Kind == "BURNOUT_APPLIED");
            Assert.DoesNotContain("PRIVATE-DRAW-", JsonSerializer.Serialize(session.SnapshotFor("P2"), json));
            probes.Add(new { kind = "final-opponent-projection", snapshot = session.SnapshotFor("P2"), prompt = session.PromptFor("P2") });
            if (initialScore == 7)
            {
                Assert.Equal("P1", state.WinnerPlayerId);
                Assert.Equal(8, state.PlayerScores["P1"]);
                Assert.Equal("P2", state.CardObjects["FIELD"].ControllerId);
                Assert.DoesNotContain(events, e => e.Kind is "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED");
                Assert.True(events.FindIndex(e => e.Kind == "SCORE_GAINED") < events.FindIndex(e => e.Kind == "MATCH_WON"));
            }
            else
            {
                Assert.Null(state.WinnerPlayerId);
                Assert.Equal(2, state.PlayerScores["P1"]);
                Assert.Equal("P1", state.CardObjects["FIELD"].ControllerId);
                var conquest = Assert.Single(events, e => e.Kind == "BATTLEFIELD_CONQUERED");
                Assert.All(resolved, e => Assert.True(events.IndexOf(e) < events.IndexOf(conquest)));
                Assert.Single(events, e => e.Kind == "BATTLE_CLOSED");
                Assert.Empty(state.StackItems);
            }
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; throw; }
        finally
        {
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
                e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
                e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
                e.CompletedTick, i, ev))).ToArray();
            Assert.Equal(commands.Length, states.Count);
            for (var i = 0; i < commands.Length; i++)
            {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(), states[i],
                    new CoreRuleEngine(), default, events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            }
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_MULTI_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
                JsonSerializer.Serialize(output, new { caseId, cardNo, initialScore, scorePosition, initial, states, probes,
                    entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, json);
            }
        }

        async Task Act(string player, GameCommand command, bool accepted = true)
        {
            var before = JsonSerializer.Serialize(state, json);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var intent = "multi-win-" + journal.Entries.Count;
            var payload = JsonSerializer.SerializeToElement(raw, json);
            var result = await session.SubmitAsync(player, intent, command, payload, default);
            Assert.Equal(accepted, result.Accepted);
            if (!accepted)
            {
                Assert.Equal(before, JsonSerializer.Serialize(result.State, json));
                Assert.Empty(result.Events);
                probes.Add(new { kind = "rejected-order", player, command, error = result.ErrorCode, stateUnchanged = true });
            }
            state = result.State;
            states.Add(state);
            last = (player, intent, command, payload);
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
