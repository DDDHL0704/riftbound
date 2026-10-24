using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattleWinCombinedTriggerTests
{
    public static IEnumerable<object[]> Combinations()
    {
        foreach (var legend in new[] { "SFD·185/221", "SFD·242/221", "UNL-201/219", "UNL-236/219", "UNL-236*/219" })
        foreach (var unit in new[] { "UNL-114/219", "SFD·123/221" })
        foreach (var unitFirst in new[] { true, false })
            yield return [legend, unit, unitFirst];
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public Task SameBattleResultLetsControllerOrderBothSkillsBeforeControl(
        string legendNo, string unitNo, bool unitFirst)
        => Run(legendNo, unitNo, unitFirst, controlResponse: false);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ConquestSkillWaitsForARealResponseBeforeTheBattleCloses(bool unitFirst)
        => Run("SFD·185/221", "UNL-114/219", unitFirst, controlResponse: true);

    private static async Task Run(string legendNo, string unitNo, bool unitFirst, bool controlResponse)
    {
        // CN 468.3-468.5 and simultaneous controller ordering. Both observers
        // are printed cards in a real battle; no trigger or result is injected.
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
            locations[id] = new(player, zone, field);
        }
        Put("UNIT", unitNo, "P1", "BASE");
        Put("LEGEND", legendNo, "P1", "LEGEND");
        Put("DEFENDER", "OGN·175/298", "P2", "BATTLEFIELD", "FIELD");
        Put("FIELD", controlResponse ? "OGN·289/298" : "OGN·296/298", "P2", "BATTLEFIELD", "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = "P2" };
        Put("DRAW1", controlResponse ? "SFD·034/221" : "OGN·219/298", "P1", "MAIN_DECK");
        Put("DRAW2", "OGN·219/298", "P1", "MAIN_DECK");
        var caseId = (legendNo + "-" + unitNo + "-" + unitFirst + "-" + controlResponse)
            .Replace('·', '-').Replace('/', '-').Replace('*', 's');
        var initial = new MatchState("COMBINED-WIN-" + caseId, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["UNIT"], LegendZone = ["LEGEND"], MainDeck = ["DRAW1", "DRAW2"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "DEFENDER"] }
            }, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            playerExperience: new Dictionary<string, int> { ["P1"] = 3, ["P2"] = 5 },
            runePools: new Dictionary<string, RunePool>
            {
                ["P1"] = controlResponse ? new(10, 10) : RunePool.Empty, ["P2"] = RunePool.Empty
            },
            seed: 424242) with { CardPlays = new(5, [], []) };
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var states = new List<MatchState>();
        var probes = new List<object>();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var state = initial;
        var intentOrdinal = 0;
        (string Player, string Intent, GameCommand Command, JsonElement Raw) last = default;

        await Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:FIELD", []));
        for (var i = 0; i < 4 && state.FocusPlayerId is not null; i++)
            await Act(state.FocusPlayerId, new PassFocusCommand());
        await Act("P1", new DeclareBattleCommand("FIELD", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(2, state.TriggerQueue.Count);
        Assert.All(state.TriggerQueue, t => Assert.Equal("P1", t.ControllerId));
        Assert.Equal(new[] { "LEGEND", "UNIT" }, state.TriggerQueue.Select(t => t.SourceObjectId).Order().ToArray());
        Assert.Empty(state.StackItems);
        Assert.Empty(state.PlayerZones["P1"].Hand);
        Assert.Equal(0, state.PlayerScores["P1"]);
        Assert.Equal(3, state.PlayerExperience["P1"]);
        Assert.Equal("P2", state.CardObjects["FIELD"].ControllerId);
        var sourceOrder = unitFirst ? new[] { "UNIT", "LEGEND" } : new[] { "LEGEND", "UNIT" };
        var triggerOrder = sourceOrder.Select(id => Assert.Single(state.TriggerQueue, t => t.SourceObjectId == id).TriggerId).ToArray();
        await Act("P2", new OrderTriggersCommand(OrderedTriggerIds: triggerOrder), accepted: false);
        await Act("P1", new OrderTriggersCommand(OrderedTriggerIds: [triggerOrder[0], triggerOrder[0]]), accepted: false);
        await Act("P1", new OrderTriggersCommand(OrderedTriggerIds: triggerOrder));
        var orderedState = JsonSerializer.Serialize(state, json);
        var orderedCount = journal.Entries.Count;
        Assert.NotNull(last.Player);
        Assert.NotNull(last.Intent);
        Assert.NotNull(last.Command);
        var duplicate = await session.SubmitAsync(last.Player, last.Intent, last.Command, last.Raw, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Equal(orderedState, JsonSerializer.Serialize(duplicate.State, json));
        Assert.Equal(orderedCount, journal.Entries.Count);
        probes.Add(new { kind = "duplicate-order-intent", accepted = duplicate.Accepted, journalUnchanged = true, stateUnchanged = true });

        Assert.Equal(2, state.StackItems.Count);
        Assert.Equal(sourceOrder[0], state.StackItems[^1].SourceObjectId);
        Assert.Empty(state.PlayerZones["P1"].Hand);
        Assert.Equal("P2", state.CardObjects["FIELD"].ControllerId);
        if (controlResponse)
        {
            // Result skills must finish first. Establishing control then creates
            // a separate ordinary conquest skill, still before battle closure.
            for (var i = 0; i < 8 && state.PlayerScores["P1"] == 0; i++)
                await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(1, state.PlayerScores["P1"]);
            Assert.Equal("P1", state.CardObjects["FIELD"].ControllerId);
            Assert.Equal(new[] { "DRAW1", "DRAW2" }, state.PlayerZones["P1"].Hand);
            Assert.Equal("FIELD", Assert.Single(state.StackItems).SourceObjectId);
            Assert.DoesNotContain(journal.Entries.SelectMany(e => e.Events), e => e.Kind is "RUNE_READY_SCHEDULED" or "BATTLE_CLOSED");
            await Act("P1", new PlayCardCommand("DRAW1", "SFD·034/221", ["UNIT"]));
            Assert.Equal(2, state.StackItems.Count);
            for (var i = 0; i < 6 && state.StackItems.Count > 1; i++)
                await Act(state.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal("FIELD", Assert.Single(state.StackItems).SourceObjectId);
            Assert.Contains("DRAW1", state.PlayerZones["P1"].Graveyard);
            Assert.DoesNotContain(journal.Entries.SelectMany(e => e.Events), e => e.Kind is "RUNE_READY_SCHEDULED" or "BATTLE_CLOSED");
        }
        for (var i = 0; i < 16 && state.StackItems.Count > 0; i++)
        {
            Assert.Null(state.PendingCardChoice);
            Assert.Null(state.PendingHandChoice);
            Assert.Null(state.PendingPayment);
            Assert.NotNull(state.PriorityPlayerId);
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
        }

        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
            e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick,
            e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
            { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        Assert.Equal(commands.Length, states.Count);
        for (var i = 0; i < commands.Length; i++)
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,
                commands.Take(i + 1).ToArray(), states[i], new CoreRuleEngine(), default,
                events.Take(journal.Entries.Take(i + 1).Sum(e => e.Events.Count)).ToArray());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_COMBINED_WIN_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            using var output = new GZipStream(File.Open(Path.Combine(evidence, caseId + ".json.gz"),
                FileMode.CreateNew), CompressionLevel.Optimal);
            JsonSerializer.Serialize(output, new { caseId, legendNo, unitNo, unitFirst, controlResponse, initial, states, probes,
                entries = journal.Entries, commands, events, originalPrefixReplayCount = commands.Length }, json);
        }

        var khazix = legendNo.StartsWith("UNL-", StringComparison.Ordinal);
        var expectedDraws = khazix ? 1 : 2;
        Assert.Equal(expectedDraws - (controlResponse ? 1 : 0), state.PlayerZones["P1"].Hand.Count);
        Assert.Equal(controlResponse ? new[] { "DRAW2" } : khazix ? ["DRAW1"] : ["DRAW1", "DRAW2"], state.PlayerZones["P1"].Hand);
        Assert.Equal(khazix ? 4 : 3, state.PlayerExperience["P1"]);
        Assert.Equal(5, state.PlayerExperience["P2"]);
        Assert.Equal(1, state.PlayerScores["P1"]);
        Assert.Equal(0, state.PlayerScores["P2"]);
        Assert.Equal("P1", state.CardObjects["FIELD"].ControllerId);
        Assert.Empty(state.StackItems);
        Assert.Empty(state.TriggerQueue);
        Assert.Null(state.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, state.Status);
        var resolved = events.Where(e => e.Event.Kind is "UNIT_TRIGGER_RESOLVED" or "LEGEND_TRIGGER_RESOLVED").ToArray();
        Assert.Equal(sourceOrder, resolved.Select(e => (string)(e.Event.Payload.GetValueOrDefault("legendObjectId")
            ?? e.Event.Payload["sourceObjectId"])!).ToArray());
        var conquest = Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        Assert.All(resolved, e => Assert.True(e.Sequence < conquest.Sequence));
        var draws = events.Where(e => e.Event.Kind == "CARD_DRAWN").ToArray();
        Assert.Equal(expectedDraws, draws.Length);
        Assert.All(draws, e => Assert.True(e.Sequence < conquest.Sequence));
        Assert.Equal(khazix ? 1 : 0, events.Count(e => e.Event.Kind == "EXPERIENCE_GAINED"));
        Assert.DoesNotContain(events, e => e.Event.Kind is "BURNOUT_APPLIED" or "MATCH_WON");
        if (controlResponse)
        {
            var scheduled = Assert.Single(events, e => e.Event.Kind == "RUNE_READY_SCHEDULED");
            Assert.True(conquest.Sequence < scheduled.Sequence);
            Assert.True(scheduled.Sequence < Assert.Single(events, e => e.Event.Kind == "BATTLE_CLOSED").Sequence);
            Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith("DELAYED_END_READY:", StringComparison.Ordinal));
        }

        async Task Act(string player, GameCommand command, bool accepted = true)
        {
            var before = JsonSerializer.Serialize(state, json);
            var beforeCount = journal.Entries.Count;
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var intent = "combined-win-" + intentOrdinal++;
            var payload = JsonSerializer.SerializeToElement(raw, json);
            var result = await session.SubmitAsync(player, intent, command, payload, default);
            Assert.Equal(accepted, result.Accepted);
            if (!accepted)
            {
                Assert.Equal(before, JsonSerializer.Serialize(result.State, json));
                probes.Add(new { kind = "rejected-order", player, command, error = result.ErrorCode, stateUnchanged = true });
            }
            if (journal.Entries.Count > beforeCount)
            {
                Assert.Equal(beforeCount + 1, journal.Entries.Count);
                states.Add(result.State);
            }
            else Assert.False(accepted, result.ErrorMessage);
            state = result.State;
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
