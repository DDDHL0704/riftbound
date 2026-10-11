using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AuditedAttachmentModesMidgameConfirmsTargetsAndReplaysThroughScoreVictory(bool copied)
    {
        var initial = CopiedAttachmentObserverTests.AddSword(AttachmentModeTests.Position());
        initial = OfficialHoldSequenceTests.AddUnit(initial, "HOLDER", "OGN·001/298", "F", "P1");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        var deck = Enumerable.Range(0, 12).Select(i => "P2-AM" + i).ToArray();
        foreach (var id in deck) {
            cards[id] = new(id, cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P2", "MAIN_DECK");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerScores = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 0 },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = deck } } };
        OfficialGraveyardRecastTests.Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var journal = new RecordingMatchJournal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal); var sequence = 0;
        async Task<ResolutionResult> Submit(GameCommand command, string player = "P1") {
            var next = await session.SubmitAsync(player, "attachment-mode-score-" + sequence++, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            AssertAccepted(next); OfficialGraveyardRecastTests.Restore(next.State); ArmamentMightTests.PublicRecovery(next.State); return next;
        }
        async Task<ResolutionResult> Choose(MatchState state, params string[] ids) {
            var choice = state.PendingCardChoice!;
            return await Submit(new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids), choice.PlayerId);
        }
        var result = await AttachedRuleTextTests.Attach(initial, session: session);
        result = await Choose(result.State, "CHANNEL_RUNE");
        Assert.Contains("RD1", result.State.PlayerZones["P1"].RuneDeck);
        result = await Submit(new PassPriorityCommand()); result = await Submit(new PassPriorityCommand(), "P2");
        Assert.Contains("RD1", result.State.PlayerZones["P1"].Base); Assert.True(result.State.CardObjects["RD1"].IsExhausted);
        if (copied) {
            result = await EquipTestDriver.FinishAsync(await Submit(new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"])), session);
            Assert.Equal(2, result.State.TriggerQueue.Count);
            result = await Submit(new OrderTriggersCommand(OrderedTriggerIds: result.State.TriggerQueue
                .OrderBy(i => i.AttachmentObserver!.CopiedInstance ? 0 : 1).Select(i => i.TriggerId).ToArray()));
            Assert.DoesNotContain("CHANNEL_RUNE", result.State.PendingCardChoice!.LegalObjectIds);
            result = await Choose(result.State, "GRANT_BOON"); result = await Choose(result.State, "HOST");
            Assert.Contains("CHANNEL_RUNE", result.State.PendingCardChoice!.LegalObjectIds);
            result = await Choose(result.State, "READY_RUNES"); result = await Choose(result.State, "MR1", "MR2");
            Assert.True(result.State.CardObjects["MR1"].IsExhausted); Assert.DoesNotContain(CardObjectTags.Boon, result.State.CardObjects["HOST"].Tags);
            Assert.Equal(3, result.State.RuleTextModeUses!.Uses.Count);
            for (var i = 0; i < 2; i++) {
                result = await Submit(new PassPriorityCommand()); result = await Submit(new PassPriorityCommand(), "P2");
            }
            Assert.False(result.State.CardObjects["MR1"].IsExhausted); Assert.Contains(CardObjectTags.Boon, result.State.CardObjects["HOST"].Tags);
        }
        Assert.Empty(result.State.StackItems);
        var turn = result.State.TurnNumber;
        result = await DriveBattleCloseToScoreVictoryAsync(session, result, "attachment-mode-score-game");
        AssertScoreVictory(result); Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.True(result.State.TurnNumber > turn); Assert.Null(result.State.RuleTextModeUses);
        Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.EndTurn);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "SCORE_GAINED");
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initial, journal, result);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
