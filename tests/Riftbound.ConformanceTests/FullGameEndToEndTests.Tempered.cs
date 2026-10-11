using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Theory]
    [InlineData("SFD·008/221")]
    [InlineData("SFD·119/221")]
    public async Task AuditedWeaponmasterMidgamePaysReferencedCostAndReplaysThroughScoreVictory(string card)
    {
        var initial = TemperedPlayTriggerAuditTests.PositionFor(card, "SFD·186/221");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "HOLDER", "OGN·001/298", "F", "P1");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        var deck = Enumerable.Range(0, 12).Select(i => "P2-WD" + i).ToArray();
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
        var play = new PlayCardCommand("HOST", card, []);
        var result = await session.SubmitAsync("P1", "weaponmaster-score-play", play, RawCommand(play), default);
        AssertAccepted(result); Assert.Contains("HOST", result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["GEAR"].AttachedToObjectId);
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        var choose = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]);
        result = await session.SubmitAsync("P1", "weaponmaster-score-confirm", choose, RawCommand(choose), default);
        AssertAccepted(result);
        foreach (var player in new[] { "P1", "P2" }) {
            var pass = new PassPriorityCommand();
            result = await session.SubmitAsync(player, "weaponmaster-score-pass-" + player, pass, RawCommand(pass), default);
            AssertAccepted(result); OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
        }
        Assert.Equal("HOST", result.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains(result.Events, e => e.Kind == "COST_PAID");
        Assert.DoesNotContain(journal.Entries.Where(e => e.CommandType == CommandTypes.PlayCard).SelectMany(e => e.Events), e => e.Kind == "EQUIPMENT_ATTACHED");
        result = await DriveBattleCloseToScoreVictoryAsync(session, result, "weaponmaster-score");
        AssertScoreVictory(result); Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.EndTurn);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "SCORE_GAINED");
        if (card == "SFD·119/221") Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.PayCost);
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initial, journal, result);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
