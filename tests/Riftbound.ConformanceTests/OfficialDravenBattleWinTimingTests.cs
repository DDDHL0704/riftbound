using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialDravenBattleWinTimingTests
{
    // CN SFD 185/242 and 468.3-468.5: battle-result skills finish before
    // establishing control/conquest. Taking an empty field is not a battle win.
    [Theory]
    [InlineData(null, true)]
    [InlineData("SFD·185/221", true)]
    [InlineData("SFD·242/221", true)]
    [InlineData(null, false)]
    [InlineData("SFD·185/221", false)]
    [InlineData("SFD·242/221", false)]
    public async Task BattleWinDrawPrecedesConquestAndEmptyConquestDoesNotTrigger(
        string? legendNo, bool defended)
        => await Run(legendNo, defended);

    [Theory]
    [InlineData(null)]
    [InlineData("SFD·185/221")]
    [InlineData("SFD·242/221")]
    public async Task ResponseSpellRemovingDefenderStillResolvesBattleWinBeforeControl(string? legendNo)
        => await Run(legendNo, true, removeDefender: true);

    private static async Task Run(string? legendNo, bool defended, bool removeDefender = false)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }
        Put("ATTACKER", "OGN·142/298", "P1", "BASE");
        Put("FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "FIELD");
        if (defended)
        {
            Put("DEFENDER", "OGN·219/298", "P2", "BATTLEFIELD", "FIELD");
            cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        }
        Put("DRAW", "OGN·219/298", "P1", "MAIN_DECK");
        if (removeDefender) Put("SPARK", "OGS·022/024", "P1", "HAND");
        if (legendNo is not null) Put("LEGEND", legendNo, "P1", "LEGEND");
        Assert.Equal(10, cards["ATTACKER"].Power);
        if (defended) Assert.Equal(4, cards["DEFENDER"].Power);
        var caseId = (legendNo?.Replace('·', '-').Replace('/', '-') ?? "no-legend")
            + (removeDefender ? "-response-removal" : defended ? "-combat" : "-empty");
        var initial = new MatchState("DRAVEN-ORDER-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["ATTACKER"], MainDeck = ["DRAW"],
                    Hand = removeDefender ? ["SPARK"] : [],
                    LegendZone = legendNo is null ? [] : ["LEGEND"]
                },
                ["P2"] = PlayerZones.Empty with { Battlefields = defended ? ["FIELD", "DEFENDER"] : ["FIELD"] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool>
            {
                // A controlled mid-turn position with resources already generated.
                ["P1"] = removeDefender ? new(10, 10) : RunePool.Empty, ["P2"] = RunePool.Empty
            },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var state = initial;
        await Act("P1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:FIELD", []));
        if (removeDefender)
        {
            Assert.Equal(TimingStates.SpellDuelOpen, state.TimingState);
            await Act("P1", new PlayCardCommand("SPARK", "OGS·022/024", ["DEFENDER"]));
            for (var i = 0; i < 4 && state.StackItems.Count > 0; i++)
                await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Empty(state.StackItems);
            Assert.Contains("DEFENDER", state.PlayerZones["P2"].Graveyard);
            Assert.Contains("SPARK", state.PlayerZones["P1"].Graveyard);
        }
        for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++)
            await Act(state.FocusPlayerId, new PassFocusCommand());
        if (defended && !removeDefender)
            await Act("P1", new DeclareBattleCommand("FIELD", ["ATTACKER"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));

        // CN 468.4: use the real response window opened by the captured skill.
        // The original red histories stay in the delivered evidence archives.
        var resultResponseInputs = 0;
        while (state.PendingBattleResult is not null && state.Status == MatchStatuses.InProgress)
        {
            Assert.NotNull(state.PriorityPlayerId);
            Assert.InRange(resultResponseInputs++, 0, 11);
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
        }

        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
            entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage)
            { ErrorCode = entry.ErrorCode, RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((ev, i) =>
            new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, ev))).ToArray();
        // Keep the original action history and verify every prefix before the
        // independent source-order assertion, including on a red source case.
        var replayErrors = new List<string>();
        for (var i = 0; i < commands.Length; i++)
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,
                commands.Take(i + 1).ToArray(), states[i], new CoreRuleEngine(), default,
                events.Take(journal.Entries.Take(i + 1).Sum(entry => entry.Events.Count)).ToArray());
            replayErrors.AddRange(replay.Errors);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_DRAVEN_ORDER_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            using var output = new GZipStream(File.Open(Path.Combine(evidence, caseId + ".json.gz"),
                FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(output, new { caseId, legendNo, defended, removeDefender, initial, states,
                entries = journal.Entries, commands, events, replayErrors,
                sourceExpectation = "Battle-result draw completes before control/conquest; empty conquest does not trigger" },
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        Assert.All(commands, command => Assert.True(command.Accepted));
        Assert.Equal((removeDefender ? 6 : defended ? 4 : 3) + resultResponseInputs, commands.Length);
        Assert.Equal(defended && legendNo is not null ? 2 : 0, resultResponseInputs);
        Assert.Equal("P1", state.CardObjects["FIELD"].ControllerId);
        Assert.Equal(1, state.PlayerScores["P1"]);
        Assert.Equal(0, state.PlayerScores["P2"]);
        Assert.Null(state.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, state.Status);
        Assert.False(state.BattleState.IsActive);
        Assert.Equal("FIELD", state.ObjectLocations["ATTACKER"].BattlefieldObjectId);
        if (defended) Assert.Contains("DEFENDER", state.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(events, e => e.Event.Kind is "BURNOUT_APPLIED" or "MATCH_WON" or "CARD_DISCARDED");
        var conquest = Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        var triggers = events.Where(e => e.Event.Kind == "LEGEND_TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("trigger"), "BATTLE_WON_DRAW_ONE")).ToArray();
        var draws = events.Where(e => e.Event.Kind == "CARD_DRAWN").ToArray();
        if (defended && legendNo is not null)
        {
            var trigger = Assert.Single(triggers);
            var draw = Assert.Single(draws);
            Assert.Equal(legendNo, trigger.Event.Payload["legendCardNo"]);
            Assert.Equal(new[] { "DRAW" }, state.PlayerZones["P1"].Hand);
            Assert.Empty(state.PlayerZones["P1"].MainDeck);
            Assert.True(trigger.Sequence < conquest.Sequence && draw.Sequence < conquest.Sequence,
                $"CN 468.3-468.5: Draven trigger/draw must precede conquest; actual trigger={trigger.Sequence}, draw={draw.Sequence}, conquest={conquest.Sequence}");
        }
        else
        {
            Assert.Empty(triggers);
            Assert.Empty(draws);
            Assert.Empty(state.PlayerZones["P1"].Hand);
            Assert.Equal(new[] { "DRAW" }, state.PlayerZones["P1"].MainDeck);
        }

        async Task Act(string player, GameCommand command)
        {
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "draven-" + journal.Entries.Count,
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
