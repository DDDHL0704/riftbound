using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattleWinLateParticipantTests
{
    [Theory]
    [InlineData("UNL-114/219", false)]
    [InlineData("UNL-114/219", true)]
    [InlineData("SFD·123/221", false)]
    [InlineData("SFD·123/221", true)]
    public Task ReactionDefenderAfterTheResultPreservesEarnedDrawAndAllowsTheNextBattle(string cardNo, bool lateDefender)
        => Run(cardNo, lateDefender, lateAttacker: false);

    internal static async Task Run(string cardNo, bool lateDefender, bool lateAttacker)
    {
        // CN 323.2.a, 463, 468.3-468.7. Rengar really enters through PLAY_CARD
        // while the original winner's ordinary draw skill is on the stack.
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, field);
        }
        Put("SOURCE", cardNo, "P1", "BASE");
        Put("DEFENDER", "OGN·175/298", "P2", "BATTLEFIELD", "FIELD");
        Put("FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        Put("RENGAR", "SFD·025/221", lateAttacker ? "P1" : "P2", "HAND");
        Put("DRAW1", "OGN·219/298", "P1", "MAIN_DECK");
        Put("DRAW2", "OGN·219/298", "P1", "MAIN_DECK");
        var caseId = cardNo.Replace('·', '-').Replace('/', '-') + "-" + (lateAttacker ? "ATTACKER" : lateDefender.ToString());
        var initial = new MatchState("LATE-BATTLE-PARTICIPANT-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["SOURCE"], MainDeck = ["DRAW1", "DRAW2"], Hand = lateAttacker ? ["RENGAR"] : [] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "DEFENDER"], Hand = lateAttacker ? [] : ["RENGAR"] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = lateAttacker ? new(20, 20) : RunePool.Empty, ["P2"] = new(20, 20) },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var probes = new List<object>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var state = initial;
        string? failure = null;
        try
        {
            await Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
            await PassFocus();
            await Act("P1", new DeclareBattleCommand("FIELD", ["SOURCE"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
            Assert.Equal("P1", state.PendingBattleResult!.WinnerPlayerId);
            Assert.Equal("P2", state.CardObjects["FIELD"].ControllerId);
            Assert.Single(state.StackItems);
            Assert.DoesNotContain("DRAW1", state.PlayerZones["P1"].Hand);
            bool? entryDefending = null;
            bool? entryAttacking = null;
            if (lateDefender || lateAttacker)
            {
                var actor = lateAttacker ? "P1" : "P2";
                if (state.PriorityPlayerId != actor) await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                await Act(actor, new PlayCardCommand("RENGAR", "SFD·025/221", [], Destination: "BATTLEFIELD:FIELD"));
                Assert.Equal("FIELD", state.ObjectLocations["RENGAR"].BattlefieldObjectId);
                entryDefending = state.CardObjects["RENGAR"].IsDefending;
                entryAttacking = state.CardObjects["RENGAR"].IsAttacking;
                probes.Add(new { kind = "after-real-reaction-entry", entryDefending, entryAttacking,
                    state.PendingBattleResult, state.PendingTaskQueue, state.TimingState });
                Assert.Equal("P1", state.PendingBattleResult!.WinnerPlayerId);
                Assert.DoesNotContain("RENGAR", state.PendingBattleResult.DefenderObjectIds);
            }
            await Settle();
            Assert.Equal(new[] { "DRAW1" }, state.PlayerZones["P1"].Hand);
            Assert.Single(Events(), e => e.Kind == "BATTLE_CLOSED");
            Assert.Equal(lateDefender ? 0 : 1, state.PlayerScores["P1"]);
            Assert.Equal(lateDefender ? "P2" : "P1", state.CardObjects["FIELD"].ControllerId);
            if (lateDefender)
            {
                probes.Add(new { kind = "first-battle-closed", state.PendingTaskQueue, state.TimingState,
                    state.FocusPlayerId, state.UntilEndOfTurnEffects, state.CardObjects });
                Assert.DoesNotContain(Events(), e => e.Kind == "BATTLEFIELD_CONQUERED");
                // Both opposing units still occupy the same field. A second
                // battle is due without another move or a manually cleared marker.
                await PassFocus();
                await Act("P1", new DeclareBattleCommand("FIELD", ["SOURCE"], ["RENGAR"], ["COMBAT_ASSIGNMENT"]));
                await Settle();
                Assert.True(entryDefending, "The late defender must acquire its controller's battle role under CN 323.2.a.");
                Assert.Equal(new[] { "DRAW1", "DRAW2" }, state.PlayerZones["P1"].Hand);
                Assert.Equal(2, Events().Count(e => e.Kind == "BATTLE_CLOSED"));
                Assert.Equal(2, Events().Count(e => e.Kind == "BATTLE_RESULT_DETERMINED"));
                Assert.Contains("RENGAR", state.PlayerZones["P2"].Graveyard);
            }
            Assert.Equal(1, state.PlayerScores["P1"]);
            Assert.Single(Events(), e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Null(state.WinnerPlayerId);
            Assert.Null(state.PendingBattleResult);
            Assert.Empty(state.StackItems);
            Assert.DoesNotContain(Events(), e => e.Kind == "BURNOUT_APPLIED");
            if (lateAttacker)
            {
                Assert.True(entryAttacking);
                Assert.False(state.CardObjects["RENGAR"].IsAttacking);
                Assert.False(state.CardObjects["RENGAR"].IsDefending);
            }
        }
        catch (Exception e) { failure = e.GetType().Name + ": " + e.Message; throw; }
        finally
        {
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
                e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence,
                e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
                new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            Assert.Equal(commands.Length, states.Count);
            for (var i = 0; i < commands.Length; i++)
            {
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands.Take(i + 1).ToArray(), states[i],
                    new CoreRuleEngine(), default, events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
                Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            }
            var folder = Environment.GetEnvironmentVariable("RIFTBOUND_LATE_BATTLE_PARTICIPANT_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
                using var output = new GZipStream(File.Open(Path.Combine(folder, caseId + ".json.gz"), FileMode.CreateNew), CompressionLevel.Optimal);
                JsonSerializer.Serialize(output, new { caseId, cardNo, lateDefender, lateAttacker, initial, states, probes,
                    entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length, failure }, json);
            }
        }

        List<GameEvent> Events() => journal.Entries.SelectMany(e => e.Events).ToList();
        async Task PassFocus()
        {
            for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++) await Act(state.FocusPlayerId, new PassFocusCommand());
        }
        async Task Settle()
        {
            for (var i = 0; state.StackItems.Count > 0; i++)
            {
                Assert.InRange(i, 0, 11);
                Assert.NotNull(state.PriorityPlayerId);
                await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            }
        }
        async Task Act(string player, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "late-participant-" + journal.Entries.Count,
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
