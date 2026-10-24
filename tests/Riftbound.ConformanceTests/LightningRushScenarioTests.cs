using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LightningRushScenarioTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("flow")]
    [InlineData("flow-empty")]
    [InlineData("flow-echo")]
    [InlineData("observed")]
    [InlineData("flow-counter")]
    [InlineData("flow-return-counter")]
    [InlineData("flow-control")]
    public async Task ControlledPositionUsesRealCatalogCardsAndRetainsItsExplicitSetupInTheOriginalReplay(string branch)
    {
        var initial = MatchReplayInitialStateBuilder.FromSeats("declared-lightning-" + branch,
            new Dictionary<string, string> { ["alice"] = "P1", ["bob"] = "P2" });
        var journal = new Journal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var scene = "lightning-rush-" + branch;
        var setup = await session.SeedScenarioAsync("alice", "explicit-lightning-setup", scene,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scene }), default);
        Assert.True(setup.Accepted, setup.ErrorMessage);
        Assert.Equal(2026102301, setup.State.Seed);
        Assert.Equal(LightningRushDeckDrawFlowTests.LightningRush, setup.State.CardObjects["PARENT"].CardNo);
        Assert.Contains(CardObjectTags.SpellCard, setup.State.CardObjects["PARENT"].Tags);
        Assert.Equal(branch.StartsWith("flow", StringComparison.Ordinal) ? "GRAVEYARD" : "HAND", setup.State.ObjectLocations["PARENT"].Zone);
        Assert.Equal(branch == "flow-empty" ? 0 : 7, setup.State.PlayerZones["alice"].MainDeck.Count);
        if (branch == "observed") Assert.Equal(DeckObservationPlayTests.Nocturne, setup.State.CardObjects["P1_FIRST"].CardNo);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(setup.State)); ArmamentMightTests.PublicRecovery(setup.State);
        var entry = Assert.Single(journal.Entries); Assert.Equal("DEV_SEED_SCENARIO:" + scene, entry.CommandType);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((value, index) => new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, value))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, setup.State, new CoreRuleEngine(), default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
