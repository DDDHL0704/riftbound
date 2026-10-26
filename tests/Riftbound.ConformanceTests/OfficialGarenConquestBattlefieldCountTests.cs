using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialGarenConquestBattlefieldCountTests
{
    // CN OGS·023/024: four units must be on the battlefield being conquered.
    // Both cases use the same printed cards, all owned and controlled by P1;
    // only the split between the target and the other battlefield changes.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 2)]
    public async Task GarenCountsOnlyUnitsAtTheConqueredBattlefieldAndOriginalActionsReplay(
        int movingCount, int expectedDraw)
    {
        var units = new[] { "U1", "U2", "U3", "U4" };
        var moving = units.Take(movingCount).ToArray();
        var other = units.Skip(movingCount).ToArray();
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null)
        {
            cards[id] = Printed(id, no, owner);
            locations[id] = new(owner, zone, field);
        }

        Put("GAREN", "OGS·023/024", "P1", "LEGEND");
        Put("TARGET", "OGN·296/298", "P2", "BATTLEFIELD", "TARGET");
        Put("OTHER", "OGN·280/298", "P1", "BATTLEFIELD", "OTHER");
        cards["OTHER"] = cards["OTHER"] with { ControllerId = "P1" };
        for (var i = 0; i < units.Length; i++)
            Put(units[i], i == 0 ? "OGN·142/298" : "OGN·219/298", "P1",
                i < movingCount ? "BASE" : "BATTLEFIELD", i < movingCount ? null : "OTHER");
        Put("DRAW1", "OGN·142/298", "P1", "MAIN_DECK");
        Put("DRAW2", "OGN·142/298", "P1", "MAIN_DECK");
        Assert.Equal(10, cards["U1"].Power);
        Assert.All(units.Skip(1), id => Assert.Equal(4, cards[id].Power));
        Assert.All(cards.Values.GroupBy(card => card.CardNo), copies => Assert.InRange(copies.Count(), 1, 3));

        var initial = new MatchState("GAREN-FIELD-COUNT-" + movingCount, 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with
                {
                    LegendZone = ["GAREN"], Base = moving,
                    Battlefields = new[] { "OTHER" }.Concat(other).ToArray(), MainDeck = ["DRAW1", "DRAW2"]
                },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["TARGET"] }
            },
            cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            seed: 424242) with { CardPlays = new(5, [], []) };
        Assert.Empty(initial.BattlefieldTasks);
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var state = initial;
        await Act("P1", new MoveUnitCommand(moving[0], "BASE", "BATTLEFIELD:TARGET", [], moving));
        Assert.Equal("P1", state.FocusPlayerId);
        await Act("P1", new PassFocusCommand());
        Assert.Equal("P2", state.FocusPlayerId);
        await Act("P2", new PassFocusCommand());

        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
            entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage)
            { TypedCommand = entry.TypedCommand, RuleInputs = entry.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((ev, i) =>
            new RecoveredEvent(entry.StartedEventSequence + i + 1, entry.CompletedTick, i, ev))).ToArray();
        Assert.Equal(3, commands.Length);
        Assert.All(commands, command => Assert.True(command.Accepted));
        Assert.Equal("P1", state.CardObjects["TARGET"].ControllerId);
        Assert.Equal(1, state.PlayerScores["P1"]);
        Assert.Equal(0, state.PlayerScores["P2"]);
        Assert.Null(state.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, state.Status);
        Assert.Equal(movingCount, units.Count(id => state.ObjectLocations[id].BattlefieldObjectId == "TARGET"));
        Assert.Equal(4 - movingCount, units.Count(id => state.ObjectLocations[id].BattlefieldObjectId == "OTHER"));
        Assert.All(units, id =>
        {
            Assert.Equal("P1", state.CardObjects[id].OwnerId);
            Assert.Equal("P1", state.CardObjects[id].ControllerId);
        });
        Assert.Equal("TARGET", Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED")
            .Event.Payload["battlefieldObjectId"]);
        Assert.DoesNotContain(events, e => e.Event.Kind is "BURNOUT_APPLIED" or "MATCH_WON" or "CARD_DISCARDED");

        // Replay the original accepted history even when the independent source expectation fails.
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, state,
            new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(expectedDraw, state.PlayerZones["P1"].Hand.Count);
        Assert.Equal(expectedDraw, events.Where(e => e.Event.Kind == "CARD_DRAWN")
            .Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
        Assert.Equal(2 - expectedDraw, state.PlayerZones["P1"].MainDeck.Count);
        var triggers = events.Where(e => e.Event.Kind == "LEGEND_TRIGGER_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("trigger"), "BATTLEFIELD_CONQUERED_DRAW_TWO")).ToArray();
        if (expectedDraw == 0) Assert.Empty(triggers);
        else
        {
            var trigger = Assert.Single(triggers).Event;
            Assert.Equal("TARGET", trigger.Payload["battlefieldId"]);
            Assert.Equal(4, trigger.Payload["controlledBattlefieldUnitCount"]);
            Assert.Equal("OGS·023/024", trigger.Payload["legendCardNo"]);
            Assert.Equal(new[] { "DRAW1", "DRAW2" }, state.PlayerZones["P1"].Hand);
        }

        async Task Act(string player, GameCommand command)
        {
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId;
            raw["snapshotTick"] = state.Tick;
            var result = await session.SubmitAsync(player, "garen-" + journal.Entries.Count,
                command, JsonSerializer.SerializeToElement(raw, json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            state = result.State;
        }
    }

    internal static CardObjectState Printed(string id, string cardNo, string owner)
    {
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        object?[] args = [id, owner, cardNo, null];
        Assert.True((bool)factory.GetMethod("TryCreate", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, args)!);
        return Assert.IsType<CardObjectState>(args[3]);
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
