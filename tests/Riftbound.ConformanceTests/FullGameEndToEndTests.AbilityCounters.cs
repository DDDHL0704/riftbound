using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditedCounteredAzirContinuesToScoreVictoryAndReplays(bool copied)
    {
        var initial = await AbilityCounterAuditTests.PendingAzir(copied: copied);
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        var deck = Enumerable.Range(0, 12).Select(i => "P2-D" + i).ToArray();
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
        var command = new PlayCardCommand("RESPONSE", "SFD·045/221", [initial.StackItems.Single().StackItemId]);
        var current = await session.SubmitAsync("P2", "ability-counter-game-cast", command, RawCommand(command), default);
        AssertAccepted(current); OfficialGraveyardRecastTests.Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
        for (var index = 0; index < 2; index++) {
            var pass = new PassPriorityCommand();
            current = await session.SubmitAsync(current.State.PriorityPlayerId!, "ability-counter-game-pass-" + index, pass, RawCommand(pass), default);
            AssertAccepted(current); OfficialGraveyardRecastTests.Restore(current.State); ArmamentMightTests.PublicRecovery(current.State);
        }
        Assert.Empty(current.State.StackItems); Assert.Null(current.State.PendingCardChoice);
        Assert.Equal("F", current.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("OTHER", current.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("TARGET", current.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(0, current.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.Equal(0, current.State.CardObjects["TARGET"].MovementCountThisTurn);
        Assert.DoesNotContain(current.State.CardObjects.Values, c => c.Tags.Contains("金币"));
        foreach (var marker in initial.UntilEndOfTurnEffects.Where(e => e.StartsWith("AZIR_SWIFT_SWAP_USED_THIS_TURN", StringComparison.Ordinal)))
            Assert.Contains(marker, current.State.UntilEndOfTurnEffects);
        var result = await DriveBattleCloseToScoreVictoryAsync(session, current, "ability-counter-game");
        AssertScoreVictory(result); Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.EndTurn);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "SCORE_GAINED");
        Assert.Single(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "STACK_ITEM_COUNTERED");
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initial, journal, result);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
