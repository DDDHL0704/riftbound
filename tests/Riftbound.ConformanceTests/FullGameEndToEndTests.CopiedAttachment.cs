using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Theory]
    [InlineData("SFD·119/221", false)]
    [InlineData("SFD·119a/221", true)]
    public async Task AuditedCopiedAttachmentMidgameConfirmsBothDrawsAndReplaysThroughScoreVictory(string card, bool copyFirst)
    {
        var initial = CopiedAttachmentObserverTests.AddSword(AttachedRuleTextTests.Position(card, 0));
        initial = OfficialHoldSequenceTests.AddUnit(initial, "HOLDER", "OGN·001/298", "F", "P1");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        var deck = Enumerable.Range(0, 12).Select(i => "P2-CA" + i).ToArray();
        foreach (var id in deck) {
            cards[id] = new(id, cardNo: "OGN·001/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]);
            locations[id] = new("P2", "MAIN_DECK");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerScores = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 0 },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = deck }
            }
        };
        OfficialGraveyardRecastTests.Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var journal = new RecordingMatchJournal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var sequence = 0;
        async Task<ResolutionResult> Submit(GameCommand command, string player = "P1") {
            var next = await session.SubmitAsync(player, "copied-attachment-score-" + sequence++, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            AssertAccepted(next); OfficialGraveyardRecastTests.Restore(next.State); ArmamentMightTests.PublicRecovery(next.State); return next;
        }
        var result = await AttachedRuleTextTests.Attach(initial, session: session);
        Assert.Single(result.State.StackItems); Assert.False(result.State.StackItems[0].AttachmentObserver!.CopiedInstance);
        var payment = result.State.PendingPayment!;
        result = await Submit(new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        result = await EquipTestDriver.FinishAsync(await Submit(new AssembleEquipmentCommand("SWORD", "HOST", ["ASSEMBLE_RED"])), session);
        Assert.Equal(2, result.State.TriggerQueue.Count);
        var ids = result.State.TriggerQueue.OrderBy(i => i.AttachmentObserver!.CopiedInstance == copyFirst ? 0 : 1).Select(i => i.TriggerId).ToArray();
        result = await Submit(new OrderTriggersCommand(OrderedTriggerIds: ids));
        Assert.Equal(copyFirst, result.State.StackItems.Last().AttachmentObserver!.CopiedInstance);
        var mana = result.State.RunePools["P1"].Mana;
        for (var index = 0; index < 2; index++) {
            payment = result.State.PendingPayment!;
            result = await Submit(new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            Assert.Empty(result.State.PlayerZones["P1"].Hand);
            Assert.Equal(mana - index - 1, result.State.RunePools["P1"].Mana);
        }
        Assert.Null(result.State.PendingPayment);
        for (var index = 0; index < 2; index++) {
            result = await Submit(new PassPriorityCommand()); result = await Submit(new PassPriorityCommand(), "P2");
            Assert.Equal(index + 1, result.State.PlayerZones["P1"].Hand.Count);
        }
        Assert.Empty(result.State.StackItems);
        result = await DriveBattleCloseToScoreVictoryAsync(session, result, "copied-attachment-score-game");
        AssertScoreVictory(result); Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.EndTurn);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "SCORE_GAINED");
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initial, journal, result);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
