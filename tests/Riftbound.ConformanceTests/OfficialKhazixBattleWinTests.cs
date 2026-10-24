using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialKhazixBattleWinTests
{
    public static IEnumerable<object?[]> BattleCases()
    {
        foreach (var card in new string?[] { null, "UNL-201/219", "UNL-236/219", "UNL-236*/219" })
        foreach (var scenario in new[] { "attacker-win", "defender-win", "defeated", "mutual-destruction", "empty-conquest", "response-removal" })
            yield return [card, scenario];
    }

    [Theory]
    [MemberData(nameof(BattleCases))]
    public async Task OnlyTheBattleWinnersLegendGainsExperienceBeforeControl(string? cardNo, string scenario)
    {
        // CN 468.3-468.5, official API 34754/34799/34800: the player's
        // battle victory triggers this legend. An empty conquest is not a battle.
        var defending = scenario == "defender-win";
        var empty = scenario == "empty-conquest";
        var removeDefender = scenario == "response-removal";
        var legendPlayer = defending ? "P2" : "P1";
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("ATTACKER", defending || scenario is "defeated" or "mutual-destruction"
            ? "OGN·219/298" : "OGN·142/298", "P1", "BASE");
        Put("FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "FIELD");
        if (!empty)
        {
            Put("DEFENDER", defending || scenario == "defeated" ? "OGN·142/298" : "OGN·219/298",
                "P2", "BATTLEFIELD", "FIELD");
            cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        }
        if (cardNo is not null) Put("LEGEND", cardNo, legendPlayer, "LEGEND");
        if (removeDefender) Put("SPARK", "OGS·022/024", "P1", "HAND");
        var zones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Base = ["ATTACKER"], Hand = removeDefender ? ["SPARK"] : [] },
            ["P2"] = PlayerZones.Empty with { Battlefields = empty ? ["FIELD"] : ["FIELD", "DEFENDER"] }
        };
        zones[legendPlayer] = zones[legendPlayer] with { LegendZone = cardNo is null ? [] : ["LEGEND"] };
        var caseId = (cardNo?.Replace('/', '-').Replace('*', 's') ?? "no-legend") + "-" + scenario;
        var initial = new MatchState("KHAZIX-BATTLE-WIN-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            playerExperience: new Dictionary<string, int> { ["P1"] = 3, ["P2"] = 5 },
            runePools: new Dictionary<string, RunePool>
            {
                ["P1"] = removeDefender ? new(10, 10) : RunePool.Empty, ["P2"] = RunePool.Empty
            }, seed: 424242) with { CardPlays = new(5, [], []) };
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var state = initial;
        await Act("P1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:FIELD", []));
        if (removeDefender)
        {
            await Act("P1", new PlayCardCommand("SPARK", "OGS·022/024", ["DEFENDER"]));
            await SettleSkills();
            Assert.Contains("DEFENDER", state.PlayerZones["P2"].Graveyard);
        }
        for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++)
            await Act(state.FocusPlayerId, new PassFocusCommand());
        if (!empty && !removeDefender)
            await Act("P1", new DeclareBattleCommand("FIELD", ["ATTACKER"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        await SettleSkills();

        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
            e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick,
            e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
            { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        for (var i = 0; i < commands.Length; i++)
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,
                commands.Take(i + 1).ToArray(), states[i], new CoreRuleEngine(), default,
                events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_KHAZIX_BATTLE_WIN_EVIDENCE");
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
        Assert.DoesNotContain(events, e => e.Event.Kind is "BURNOUT_APPLIED" or "MATCH_WON" or "CARD_DRAWN");
        var expectedGain = cardNo is not null && (scenario is "attacker-win" or "defender-win" or "response-removal") ? 1 : 0;
        Assert.Equal(initial.PlayerExperience[legendPlayer] + expectedGain, state.PlayerExperience[legendPlayer]);
        var other = legendPlayer == "P1" ? "P2" : "P1";
        Assert.Equal(initial.PlayerExperience[other], state.PlayerExperience[other]);
        var gains = events.Where(e => e.Event.Kind == "EXPERIENCE_GAINED").ToArray();
        Assert.Equal(expectedGain, gains.Length);
        if (expectedGain == 1)
        {
            Assert.Equal(legendPlayer, gains[0].Event.Payload["playerId"]);
            Assert.Equal("LEGEND", gains[0].Event.Payload["sourceObjectId"]);
            Assert.Equal(cardNo, gains[0].Event.Payload["cardNo"]);
            Assert.Contains("GAINED_EXPERIENCE_THIS_TURN:" + legendPlayer, state.UntilEndOfTurnEffects);
            if (!defending)
                Assert.True(gains[0].Sequence < Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Sequence);
        }
        if (scenario == "mutual-destruction")
        {
            Assert.Contains("ATTACKER", state.PlayerZones["P1"].Graveyard);
            Assert.Contains("DEFENDER", state.PlayerZones["P2"].Graveyard);
        }

        async Task SettleSkills()
        {
            for (var i = 0; i < 16 && state.StackItems.Count > 0; i++)
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
            var result = await session.SubmitAsync(player, "khazix-battle-win-" + journal.Entries.Count,
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
