using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Theory]
    [InlineData("OGN·109/298")]
    [InlineData("SFD·050/221")]
    public async Task AuditedCopiedPrintedTextMidgameResolvesIndependentInstancesAndReplaysThroughScoreVictory(string card)
    {
        var initial = AttachedRuleTextTests.Position(card, card == "OGN·109/298" ? 7 : 0);
        initial = OfficialHoldSequenceTests.AddUnit(initial, "TARGET", "OGN·001/298", "F", "P1");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        var deck = Enumerable.Range(0, 12).Select(i => "P2-CD" + i).ToArray();
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
        var result = await AttachedRuleTextTests.Attach(initial, session: session);
        AssertAccepted(result); Assert.Equal(1, result.State.CardObjects["GEAR"].AttachedRuleText!.Revision);
        if (card == "SFD·050/221") {
            var power = result.State.RunePools["P1"].TotalPower;
            foreach (var copied in new[] { false, true }) {
                var command = copied ? CopiedActivatedAbilityTests.Copy(result.State) : CopiedActivatedAbilityTests.Native();
                result = await session.SubmitAsync("P1", "copied-score-activate-" + copied, command, RawCommand(command), default);
                AssertAccepted(result);
                Assert.Equal(copied, Assert.Single(result.State.StackItems).AzirMovement!.CopiedInstance);
                result = await ResolveDriverPendingEffectsAsync(session, result, "copied-score-response-" + copied);
                OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
            }
            Assert.Empty(CopiedActivatedAbilityTests.Requirements(result.State));
            Assert.Equal(power - 2, result.State.RunePools["P1"].TotalPower);
            Assert.Equal("BASE", result.State.ObjectLocations["HOST"].Zone);
            Assert.Equal("BASE", result.State.ObjectLocations["GEAR"].Zone);
            Assert.Equal("F", result.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        }
        result = await DriveBattleCloseToScoreVictoryAsync(session, result, "copied-score");
        AssertScoreVictory(result); Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.EndTurn);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "SCORE_GAINED");
        if (card == "OGN·109/298") {
            var recycles = journal.Entries.Where(e => e.CommandType == CommandTypes.ChooseCards
                && e.RawCommand!.Value.GetProperty("choiceWindow").GetString() == "GRAVEYARD_RECYCLE_EFFECT").ToArray();
            Assert.Equal([3, 3, 1], recycles.Select(e => e.RawCommand!.Value.GetProperty("chosenObjectIds").GetArrayLength()).ToArray());
            Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(initial.RngCursor + 2, result.State.RngCursor);
        }
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initial, journal, result);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
