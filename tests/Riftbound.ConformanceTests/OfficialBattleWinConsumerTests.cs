using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialBattleWinConsumerTests
{
    [Theory]
    [InlineData("UNL-114/219", "attacker-win")]
    [InlineData("UNL-114/219", "defender-win")]
    [InlineData("UNL-114/219", "defeated")]
    [InlineData("UNL-114/219", "mutual-destruction")]
    [InlineData("UNL-114/219", "nonparticipant")]
    [InlineData("UNL-114/219", "empty-conquest")]
    [InlineData("SFD·123/221", "attacker-win")]
    [InlineData("SFD·123/221", "defender-win")]
    [InlineData("SFD·123/221", "defeated")]
    [InlineData("SFD·123/221", "mutual-destruction")]
    [InlineData("SFD·123/221", "nonparticipant")]
    [InlineData("SFD·123/221", "empty-conquest")]
    public async Task OnlyTheParticipatingUnitThatInheritsBattleVictoryDraws(string cardNo, string scenario)
    {
        // CN 468.3.c, API 34653 / 33213. The source is an ordinary printed
        // unit; no injected trigger, damage, combat result or private engine call.
        var defendingSource = scenario == "defender-win";
        var empty = scenario == "empty-conquest";
        var sourcePlayer = defendingSource ? "P2" : "P1";
        var attacker = defendingSource || scenario == "nonparticipant" ? "ATTACKER" : "SOURCE";
        var defender = defendingSource ? "SOURCE" : "DEFENDER";
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("SOURCE", cardNo, sourcePlayer, defendingSource ? "BATTLEFIELD" : "BASE", defendingSource ? "FIELD" : null);
        Put("FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "FIELD");
        if (!empty) cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        if (attacker != "SOURCE")
            Put(attacker, defendingSource ? "OGN·175/298" : "OGN·142/298", "P1", "BASE");
        if (!empty && defender != "SOURCE")
            Put(defender, scenario == "defeated" ? "OGN·142/298"
                : scenario == "mutual-destruction" ? "OGN·219/298" : "OGN·175/298",
                "P2", "BATTLEFIELD", "FIELD");
        Put("DRAW", "OGN·219/298", sourcePlayer, "MAIN_DECK");
        Assert.Equal(4, cards["SOURCE"].Power);
        var caseId = cardNo.Replace('·', '-').Replace('/', '-') + "-" + scenario;
        var zones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = scenario == "nonparticipant" ? ["ATTACKER", "SOURCE"] : [attacker] },
            ["P2"] = PlayerZones.Empty with { Battlefields = empty ? ["FIELD"] : ["FIELD", defender] }
        };
        zones[sourcePlayer] = zones[sourcePlayer] with { MainDeck = ["DRAW"] };
        var initial = new MatchState("UNIT-BATTLE-WIN-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var state = initial;
        await Act("P1", new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:FIELD", []));
        await SettleSkills();
        for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++)
            await Act(state.FocusPlayerId, new PassFocusCommand());
        if (!empty)
            await Act("P1", new DeclareBattleCommand("FIELD", [attacker], [defender], ["COMBAT_ASSIGNMENT"]));
        await SettleSkills();

        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
            entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage)
            { ErrorCode = entry.ErrorCode, RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((ev, i) =>
            new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, ev))).ToArray();
        for (var i = 0; i < commands.Length; i++)
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,
                commands.Take(i + 1).ToArray(), states[i], new CoreRuleEngine(), default,
                events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_UNIT_BATTLE_WIN_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            using var output = new GZipStream(File.Open(Path.Combine(evidence, caseId + ".json.gz"),
                FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(output, new { caseId, cardNo, scenario, initial, states,
                entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        Assert.Null(state.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, state.Status);
        Assert.Empty(state.StackItems);
        Assert.Empty(state.TriggerQueue);
        Assert.DoesNotContain(events, e => e.Event.Kind is "BURNOUT_APPLIED" or "MATCH_WON");
        var expectedDraw = scenario is "attacker-win" or "defender-win" ? 1 : 0;
        Assert.Equal(expectedDraw, state.PlayerZones[sourcePlayer].Hand.Count);
        var draws = events.Where(e => e.Event.Kind == "CARD_DRAWN").ToArray();
        Assert.Equal(expectedDraw, draws.Length);
        if (expectedDraw == 1)
        {
            Assert.Equal(new[] { "DRAW" }, state.PlayerZones[sourcePlayer].Hand);
            Assert.Equal(sourcePlayer, draws[0].Event.Payload["playerId"]);
            Assert.Equal("FIELD", state.ObjectLocations["SOURCE"].BattlefieldObjectId);
            Assert.Equal("BATTLEFIELD", state.ObjectLocations["SOURCE"].Zone);
            if (!defendingSource)
                Assert.True(draws[0].Sequence < Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Sequence);
        }
        else
        {
            Assert.Equal(new[] { "DRAW" }, state.PlayerZones[sourcePlayer].MainDeck);
            if (scenario is "defeated" or "mutual-destruction")
                Assert.Contains("SOURCE", state.PlayerZones[sourcePlayer].Graveyard);
            if (scenario == "nonparticipant") Assert.Contains("SOURCE", state.PlayerZones[sourcePlayer].Base);
            if (empty) Assert.Equal("P1", state.CardObjects["FIELD"].ControllerId);
        }

        async Task SettleSkills()
        {
            for (var i = 0; i < 12 && state.StackItems.Count > 0; i++)
            {
                Assert.Null(state.PendingCardChoice);
                Assert.Null(state.PendingHandChoice);
                Assert.Null(state.PendingPayment);
                Assert.NotNull(state.PriorityPlayerId);
                await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            }
            Assert.Empty(state.StackItems);
        }

        async Task Act(string player, GameCommand command)
        {
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "unit-battle-win-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw, json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            state = result.State;
            states.Add(state);
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
