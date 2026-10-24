using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeckPlayPendingScenarioTests
{
    [Theory]
    [InlineData("reinforcements", "OGN·062/298", false)]
    [InlineData("void-rush", "SFD·188/221", false)]
    [InlineData("blind-fury", "OGN·025/298", false)]
    [InlineData("bright-future", "OGN·115/298", false)]
    [InlineData("reinforcements-observed", "OGN·062/298", true)]
    [InlineData("bright-future-observed", "OGN·115/298", true)]
    public async Task DeclaredControlledPositionUsesTheActualDeckSequenceAndRecordsItsExplicitSetup(string branch, string card, bool observed)
    {
        var journal = new Journal();
        var initial = MatchReplayInitialStateBuilder.FromSeats("deck-pending-" + branch,
            new Dictionary<string, string> { ["alice"] = "P1", ["bob"] = "P2" });
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var scenario = "deck-pending-" + branch;
        var seeded = await session.SeedScenarioAsync("alice", "controlled-deck-pending", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted); Assert.Equal(card, seeded.State.CardObjects["PARENT"].CardNo);
        Assert.Equal(observed ? DeckObservationPlayTests.Nocturne : "OGN·096/298", seeded.State.CardObjects["P1_SECOND"].CardNo);
        var current = seeded;
        async Task Act(string actor, GameCommand command)
        {
            var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.SerializeToNode(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            fields["promptId"] = prompt.PromptId; fields["snapshotTick"] = current.State.Tick;
            current = await session.SubmitAsync(actor, "input-" + journal.Entries.Count, command, JsonSerializer.SerializeToElement(fields), default);
            Assert.True(current.Accepted, current.ErrorMessage);
            OfficialGraveyardRecastTests.Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(current);
        }
        await Act("alice", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"]));
        await Act(current.State.PriorityPlayerId!, new PassPriorityCommand()); await Act(current.State.PriorityPlayerId!, new PassPriorityCommand());
        foreach (var id in DeckPlayPendingBeforeParentTests.FirstSelections(card).Select(id => id.Replace("_TOP", "_FIRST")))
        {
            var choice = current.State.PendingCardChoice!; await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
        }
        if (observed)
        {
            var choice = current.State.PendingRuleChoice!; await Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
        }
        var expected = DeckPlayPendingBeforeParentTests.FirstPendingOrder(card).Select(id => id.Replace("_TOP", "_FIRST")).ToArray();
        Assert.Equal(expected, current.State.StackItems.Where(s => s.DeckPlayPendingCard is not null).Select(s => s.SourceObjectId));
        Assert.Null(current.State.PendingEffectPlay); Assert.NotNull(current.State.PendingCardChoice);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(current.State));
        Assert.Equal("DEV_SEED_SCENARIO:" + scenario, journal.Entries[0].CommandType);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((value, index) => new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, value))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current.State, new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
