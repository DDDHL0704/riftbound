using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialDravenBattleWinScoreTests
{
    [Theory]
    [InlineData("SFD·148/221", "attacker-win")]
    [InlineData("SFD·148/221", "defender-win")]
    [InlineData("SFD·148/221", "repeat-defender-win")]
    [InlineData("SFD·148/221", "winning-attacker")]
    [InlineData("SFD·148/221", "winning-defender")]
    [InlineData("SFD·148/221", "defeated")]
    [InlineData("SFD·148/221", "mutual-destruction")]
    [InlineData("SFD·148/221", "nonparticipant")]
    [InlineData("SFD·148/221", "empty-conquest")]
    [InlineData("SFD·148a/221", "attacker-win")]
    [InlineData("SFD·148a/221", "defender-win")]
    [InlineData("SFD·148a/221", "repeat-defender-win")]
    [InlineData("SFD·148a/221", "winning-attacker")]
    [InlineData("SFD·148a/221", "winning-defender")]
    [InlineData("SFD·148a/221", "defeated")]
    [InlineData("SFD·148a/221", "mutual-destruction")]
    [InlineData("SFD·148a/221", "nonparticipant")]
    [InlineData("SFD·148a/221", "empty-conquest")]
    public async Task OnlyTheParticipatingDravenThatWinsBattleScoresBeforeConquest(string cardNo, string scenario)
    {
        // CN 468.3.c/468.4, API 33240 / 33241. The source is an ordinary printed
        // unit; no injected trigger, damage, combat result or private engine call.
        var defendingSource = scenario is "defender-win" or "repeat-defender-win" or "winning-defender";
        var winsMatch = scenario is "winning-attacker" or "winning-defender";
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
        if (scenario == "repeat-defender-win") Put("ATTACKER2", "OGN·175/298", "P1", "BASE");
        if (!empty && defender != "SOURCE")
            Put(defender, scenario == "defeated" ? "OGN·142/298"
                : scenario == "mutual-destruction" ? "OGN·125/298" : "OGN·175/298",
                "P2", "BATTLEFIELD", "FIELD");
        Put("DRAW", "OGN·219/298", sourcePlayer, "MAIN_DECK");
        Assert.Equal(6, cards["SOURCE"].Power);
        var caseId = cardNo.Replace('·', '-').Replace('/', '-') + "-" + scenario;
        var zones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = scenario == "nonparticipant" ? ["ATTACKER", "SOURCE"] : [attacker] },
            ["P2"] = PlayerZones.Empty with { Battlefields = empty ? ["FIELD"] : ["FIELD", defender] }
        };
        if (scenario == "repeat-defender-win") zones["P1"] = zones["P1"] with { Base = ["ATTACKER", "ATTACKER2"] };
        zones[sourcePlayer] = zones[sourcePlayer] with { MainDeck = ["DRAW"] };
        var initial = new MatchState("DRAVEN-BATTLE-SCORE-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int>
            {
                ["P1"] = winsMatch && sourcePlayer == "P1" ? 7 : 0,
                ["P2"] = winsMatch && sourcePlayer == "P2" ? 7 : 0
            },
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

        if (scenario == "repeat-defender-win")
        {
            // Two different enemy units attack the same surviving defender in
            // the same turn. No preinstalled usage marker or private hook.
            await Act("P1", new MoveUnitCommand("ATTACKER2", "BASE", "BATTLEFIELD:FIELD", []));
            for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++)
                await Act(state.FocusPlayerId, new PassFocusCommand());
            await Act("P1", new DeclareBattleCommand("FIELD", ["ATTACKER2"], ["SOURCE"], ["COMBAT_ASSIGNMENT"]));
            await SettleSkills();
        }

        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
            entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage)
            { RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((ev, i) =>
            new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, ev))).ToArray();
        for (var i = 0; i < commands.Length; i++)
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,
                commands.Take(i + 1).ToArray(), states[i], new CoreRuleEngine(), default,
                events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_BATTLE_SCORE_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            using var output = new GZipStream(File.Open(Path.Combine(evidence, caseId + ".json.gz"),
                FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(output, new { caseId, cardNo, scenario, initial, states,
                entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        Assert.Equal(winsMatch ? sourcePlayer : null, state.WinnerPlayerId);
        Assert.Equal(winsMatch ? MatchStatuses.Finished : MatchStatuses.InProgress, state.Status);
        Assert.Empty(state.StackItems);
        Assert.Empty(state.TriggerQueue);
        Assert.DoesNotContain(events, e => e.Event.Kind == "BURNOUT_APPLIED");
        if (!winsMatch) Assert.DoesNotContain(events, e => e.Event.Kind == "MATCH_WON");
        var expectedAbilityScore = winsMatch || scenario is "attacker-win" or "defender-win" or "repeat-defender-win" ? 1 : 0;
        var conquestScore = scenario is "attacker-win" or "nonparticipant" or "empty-conquest" ? 1 : 0;
        Assert.Equal(initial.PlayerScores[sourcePlayer] + expectedAbilityScore + conquestScore, state.PlayerScores[sourcePlayer]);
        // The separate combat-death ability remains an open consumer; this
        // assertion only specifies the surviving participant's win trigger.
        var abilityScores = events.Where(e => e.Event.Kind == "SCORE_GAINED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SOURCE")).ToArray();
        Assert.Equal(expectedAbilityScore, abilityScores.Length);
        Assert.DoesNotContain(events, e => e.Event.Kind == "CARD_DRAWN");
        Assert.Equal(new[] { "DRAW" }, state.PlayerZones[sourcePlayer].MainDeck);
        if (expectedAbilityScore == 1)
        {
            Assert.Equal(sourcePlayer, abilityScores[0].Event.Payload["playerId"]);
            Assert.Equal(1, abilityScores[0].Event.Payload["amount"]);
            Assert.Equal("FIELD", state.ObjectLocations["SOURCE"].BattlefieldObjectId);
            if (!defendingSource && !winsMatch)
                Assert.True(abilityScores[0].Sequence < Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Sequence);
        }
        if (winsMatch)
        {
            Assert.True(Assert.Single(abilityScores).Sequence < Assert.Single(events, e => e.Event.Kind == "MATCH_WON").Sequence);
            Assert.DoesNotContain(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        }
        if (scenario is "defeated" or "mutual-destruction")
            Assert.Contains("SOURCE", state.PlayerZones[sourcePlayer].Graveyard);
        if (scenario == "nonparticipant") Assert.Contains("SOURCE", state.PlayerZones[sourcePlayer].Base);

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
            var result = await session.SubmitAsync(player, "draven-battle-score-" + journal.Entries.Count,
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
