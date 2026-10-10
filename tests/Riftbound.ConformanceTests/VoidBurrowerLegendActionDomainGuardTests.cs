using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialLegendDeckPlayTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class VoidBurrowerLegendActionDomainGuardTests
{
    [Fact]
    public async Task VoidBurrowerConquerOffersUnitChoiceAndPaysItsCost()
    {
        var opened = await Open(Position());
        var done = await Act(opened.State, "P1", new PlayCardCommand("DECK0", "SFD·125/221", []));
        Assert.True(done.State.CardObjects["DECK0"].IsExhausted);
        Assert.Equal(16, done.State.RunePools["P1"].Mana);
        Assert.Equal(["TAIL", "DECK1"], done.State.PlayerZones["P1"].MainDeck);
        Restore(done.State);
    }
    [Fact]
    public async Task ReprintCanPlayEquipmentWhenNeitherRevealedCardIsAUnit()
    {
        var opened = await Open(Position("SFD·243/221", top: ["OGN·048/298", "SFD·042/221"]));
        Assert.Equal(2, opened.State.PendingEffectPlay!.Sources.Count);
        var done = await Act(opened.State, "P1", new PlayCardCommand("DECK1", "SFD·042/221", []));
        Assert.Contains("DECK1", done.State.PlayerZones["P1"].Base); Assert.Equal(["TAIL", "DECK0"], done.State.PlayerZones["P1"].MainDeck); Restore(done.State);
    }
    [Fact]
    public async Task ExhaustedLegendCapturesButDoesNotRevealOrPay()
    {
        var done = await ConquestLifecycleRegressionTests.Conquer(Position(exhausted: true));
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_QUEUED");
        Assert.DoesNotContain(done.Events, e => e.Kind is "CARDS_REVEALED" or "COST_PAID");
        Assert.Null(done.State.PendingEffectPlay); Assert.Empty(done.State.StackItems); Restore(done.State);
    }
    [Fact]
    public async Task OldConfirmationPromptAndConflictingIntentCannotMutateAcceptedMatch()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position());
        var journal = new Journal(); var session = new MatchSession(opened.State, new CoreRuleEngine(), journal);
        var choice = opened.State.PendingCardChoice!; var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LEGEND"]);
        var prompt = session.PromptFor("P1");
        var raw = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.ChooseCards, choiceId = choice.ChoiceId, choiceWindow = choice.ChoiceWindow,
            chosenObjectIds = new[] { "LEGEND" }, promptId = prompt.PromptId, snapshotTick = prompt.SnapshotTick });
        var paid = await session.SubmitAsync("P1", "paid", command, raw, default); Assert.True(paid.Accepted, paid.ErrorMessage);
        var hash = MatchStateHasher.Hash(paid.State);
        var old = await session.SubmitAsync("P1", "old", command, raw, default);
        var repeat = await session.SubmitAsync("P1", "old", command, raw, default);
        foreach (var result in new[] { old, repeat }) { Assert.False(result.Accepted); Assert.Equal(ErrorCodes.PromptExpired, result.ErrorCode); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); }
        var conflict = await session.SubmitAsync("P1", "old", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []),
            JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.ChooseCards, choiceId = choice.ChoiceId, choiceWindow = choice.ChoiceWindow, chosenObjectIds = Array.Empty<string>() }), default);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode); Assert.Equal(hash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(2, journal.Entries.Count); Restore(paid.State);
    }
    private sealed class Journal : IMatchJournal {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
