using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DravenMutualDeathScoreTests
{
    private const string DeathEffect = "CAPTURED_DRAVEN_BATTLE_DEATH_OPPONENT_SCORE";

    public static IEnumerable<object[]> Cases()
    {
        foreach (var p1Card in new[] { "SFD·148/221", "SFD·148a/221" })
        foreach (var p2Card in new[] { "SFD·148/221", "SFD·148a/221" })
        foreach (var turnPlayer in new[] { "P1", "P2" })
        foreach (var initialScore in new[] { 0, 7 })
            yield return [p1Card, p2Card, turnPlayer, initialScore];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BothRealDeathsKeepControllerOrderEvenWithoutABattleWinner(
        string p1Card, string p2Card, string turnPlayer, int initialScore)
    {
        // CN 383.3.d.1 puts the turn player's simultaneous skill on the chain
        // before the other player's skill. The latter therefore resolves first.
        // CN 323.1 checks normal victory after that first opponent-score skill.
        var opponent = turnPlayer == "P1" ? "P2" : "P1";
        var attacker = turnPlayer + "-DRAVEN";
        var defender = opponent + "-DRAVEN";
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        foreach (var (player, no) in new[] { ("P1", p1Card), ("P2", p2Card) })
        {
            var id = player + "-DRAVEN";
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = player == turnPlayer ? new(player, "BASE") : new(player, "BATTLEFIELD", "FIELD");
        }
        cards["FIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD", "OGN·278/298", opponent)
            with { ControllerId = opponent };
        locations["FIELD"] = new(opponent, "BATTLEFIELD", "FIELD");
        var caseId = (p1Card + "-" + p2Card + "-" + turnPlayer + "-" + initialScore).Replace('·', '-').Replace('/', '-');
        var initial = new MatchState("MUTUAL-DRAVEN-" + caseId, 0, 5, turnPlayer,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: turnPlayer,
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                [turnPlayer] = PlayerZones.Empty with { Base = [attacker] },
                [opponent] = PlayerZones.Empty with { Battlefields = ["FIELD", defender] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = initialScore, ["P2"] = initialScore },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var state = initial;
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        string? failure = null;
        try
        {
            await Act(turnPlayer, new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:FIELD", []));
            await Act(turnPlayer, new PassFocusCommand());
            await Act(opponent, new PassFocusCommand());
            await Act(turnPlayer, new DeclareBattleCommand("FIELD", [attacker], [defender], ["COMBAT_ASSIGNMENT"]));
            Assert.Contains("P1-DRAVEN", state.PlayerZones["P1"].Graveyard);
            Assert.Contains("P2-DRAVEN", state.PlayerZones["P2"].Graveyard);
            Assert.True(state.CardObjects["P1-DRAVEN"].ObjectGeneration > 0);
            Assert.True(state.CardObjects["P2-DRAVEN"].ObjectGeneration > 0);
            for (var i = 0; state.Status == MatchStatuses.InProgress
                && (state.StackItems.Count > 0 || state.TriggerQueue.Count > 0 || state.PendingCardChoice is not null); i++)
            {
                Assert.InRange(i, 0, 19);
                Assert.Null(state.PendingPayment);
                Assert.Null(state.PendingHandChoice);
                if (state.PendingCardChoice is { } choice)
                {
                    var recipient = choice.PlayerId == "P1" ? "P2" : "P1";
                    Assert.Contains(recipient, choice.LegalObjectIds);
                    await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [recipient]));
                }
                else if (state.TriggerQueue.Count > 0)
                {
                    var orderers = new[] { "P1", "P2" }.Where(p => session.PromptFor(p).EnabledActions().Contains(CommandTypes.OrderTriggers)).ToArray();
                    var player = Assert.Single(orderers);
                    var prompt = JsonSerializer.SerializeToElement(session.PromptFor(player), json);
                    var native = prompt.GetProperty("candidates").EnumerateArray().Single(c =>
                        c.GetProperty("action").GetString() == CommandTypes.OrderTriggers && c.GetProperty("enabled").GetBoolean());
                    var ids = native.GetProperty("metadata").GetProperty("orderedTriggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
                    await Act(player, new OrderTriggersCommand(OrderedTriggerIds: ids));
                }
                else
                {
                    Assert.NotNull(state.PriorityPlayerId);
                    await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                }
            }
            var events = journal.Entries.SelectMany(e => e.Events).ToList();
            Assert.Equal(new[] { "P1-DRAVEN", "P2-DRAVEN" }, events.Where(e => e.Kind == "UNIT_DESTROYED")
                .Select(e => (string)e.Payload["targetObjectId"]!).Order().ToArray());
            var scores = events.Where(e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), DeathEffect)).ToArray();
            Assert.Equal(initialScore == 7 ? 1 : 2, scores.Length);
            Assert.Equal(turnPlayer, scores[0].Payload["playerId"]);
            Assert.Equal(defender, scores[0].Payload["sourceObjectId"]);
            Assert.Equal(1, scores[0].Payload["amount"]);
            Assert.DoesNotContain(events, e => e.Kind == "BATTLEFIELD_CONQUERED" || e.Kind == "BURNOUT_APPLIED"
                || Equals(e.Payload.GetValueOrDefault("reason"), "CAPTURED_DRAVEN_FIRST_BATTLE_WIN_SCORE"));
            if (initialScore == 7)
            {
                Assert.Equal(turnPlayer, state.WinnerPlayerId);
                Assert.Equal(8, state.PlayerScores[turnPlayer]);
                Assert.Equal(7, state.PlayerScores[opponent]);
                Assert.Equal(opponent, state.CardObjects["FIELD"].ControllerId);
                Assert.DoesNotContain(events, e => e.Kind == "BATTLE_CLOSED");
                Assert.True(events.IndexOf(scores[0]) < events.FindIndex(e => e.Kind == "MATCH_WON"));
            }
            else
            {
                Assert.Null(state.WinnerPlayerId);
                Assert.Equal(1, state.PlayerScores["P1"]);
                Assert.Equal(1, state.PlayerScores["P2"]);
                Assert.Equal(opponent, scores[1].Payload["playerId"]);
                Assert.Equal(attacker, scores[1].Payload["sourceObjectId"]);
                Assert.Null(state.CardObjects["FIELD"].ControllerId);
                var closed = Assert.Single(events, e => e.Kind == "BATTLE_CLOSED");
                Assert.All(scores, e => Assert.True(events.IndexOf(e) < events.IndexOf(closed)));
                Assert.Contains(events, e => e.Kind == "BATTLE_NO_RESULT");
            }
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; throw; }
        finally
        {
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
                e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
                e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1,
                e.CompletedTick, i, ev))).ToArray();
            Assert.Equal(commands.Length, states.Count);
            for (var i = 0; i < commands.Length; i++)
            {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(), states[i],
                    new CoreRuleEngine(), default, events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            }
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_MUTUAL_DRAVEN_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
                JsonSerializer.Serialize(output, new { caseId, p1Card, p2Card, turnPlayer, initialScore, initial, states,
                    entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, json);
            }
        }

        async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "mutual-draven-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw, json), default);
            state = result.State;
            states.Add(state);
            Assert.True(result.Accepted, result.ErrorMessage);
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
