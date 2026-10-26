using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LoggedBattleHistoryScenarioTests
{
    [Theory]
    [InlineData("battle-history-deck", false, false)]
    [InlineData("battle-history-burnout", false, true)]
    [InlineData("battle-history-attacker-deck", true, false)]
    [InlineData("battle-history-attacker-burnout", true, true)]
    public async Task LoggedScenarioSetupAndOrdinaryCommandsReplayFromPersistedSeatOrigin(string scenario, bool attackerEmpty, bool defenderEmpty)
    {
        var initial = MatchReplayInitialStateBuilder.FromSeats("logged-battle-history-" + scenario,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var journal = new Journal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var current = await session.SeedScenarioAsync("P1", "setup", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(current.Accepted);
        OfficialGraveyardRecastTests.Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
        async Task<ResolutionResult> Act(string actor, GameCommand command)
        {
            var prompt = session.PromptFor(actor);
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), options), options)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.State.Tick);
            current = await session.SubmitAsync(actor, "step-" + journal.Entries.Count, command, JsonSerializer.SerializeToElement(fields), default);
            Assert.True(current.Accepted, current.ErrorMessage);
            OfficialGraveyardRecastTests.Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(current);
            return current;
        }
        async Task<ResolutionResult> Top()
        {
            await Act(current.State.PriorityPlayerId!, new PassPriorityCommand());
            return await Act(current.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        await Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = current.State.PendingCardChoice!;
        await Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
        await Act("P1", new PlayCardCommand("P1-SOUL", "SFD·163/221", ["HOST", "P1-ALLY"]));
        var removed = await Top(); Assert.Equal(attackerEmpty, removed.Events.Any(e => e.Kind == "BURNOUT_APPLIED"));
        if (current.State.PriorityPlayerId != "P2") await Act(current.State.PriorityPlayerId!, new PassPriorityCommand());
        await Act("P2", new PlayCardCommand("P2-SOUL", "SFD·163/221", ["ENEMY1", "P2-ALLY"]));
        removed = await Top(); Assert.Equal(defenderEmpty, removed.Events.Any(e => e.Kind == "BURNOUT_APPLIED"));
        await Top();
        var close = await Act(current.State.FocusPlayerId!, new PassFocusCommand());
        if (!close.Events.Any(e => e.Kind == "BATTLE_CLOSED")) close = await Act(current.State.FocusPlayerId!, new PassFocusCommand());
        Assert.Equal("NO_RESULT", Assert.Single(current.State.BattleResolutions).Kind);
        Assert.All(current.State.BattleResolutions[0].PublicObjectBindings!, b => Assert.Equal(0, b.Generation));
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        Assert.StartsWith("DEV_SEED_SCENARIO:", commands[0].CommandType);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current.State, new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
