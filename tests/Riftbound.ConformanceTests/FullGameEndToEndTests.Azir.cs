using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Theory]
    [InlineData("SFD·050/221", false)]
    [InlineData("SFD·050a/221", true)]
    public async Task AuditedAzirMidgameMovesEquipmentAndReplaysThroughScoreVictory(string card, bool attach)
        => await RunAuditedAzirToScoreVictory(AzirMovementAuditTests.AzirPosition(azir: card), attach);

    [Fact]
    public async Task AuditedCopiedAzirConfirmsWeaponThenChoosesAttachmentAndReplaysThroughScoreVictory()
        => await RunAuditedAzirToScoreVictory(await AzirMovementNativeTests.Initial("copied-attach"), true, copied: true);

    private static async Task RunAuditedAzirToScoreVictory(MatchState initial, bool attach, bool copied = false)
    {
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
        var command = AzirMovementAuditTests.Command(attach);
        if (copied) command = command with { AbilityInstanceId = CopiedActivatedAbilityTests.Copy(initial).AbilityInstanceId };
        var activated = await session.SubmitAsync("P1", "azir-score-activate", command, RawCommand(command), default);
        AssertAccepted(activated);
        if (copied) {
            Assert.True(Assert.Single(activated.State.StackItems).AzirMovement!.CopiedInstance);
            var generation = initial.CardObjects["HOST"].ObjectGeneration;
            Assert.Contains(P4ActivatedAbilityCatalog.AzirSwiftSwapUsedThisTurnEffectId("P1", "HOST", generation, command.AbilityInstanceId), activated.State.UntilEndOfTurnEffects);
            Assert.DoesNotContain(P4ActivatedAbilityCatalog.AzirSwiftSwapUsedThisTurnEffectId("P1", "HOST", generation), activated.State.UntilEndOfTurnEffects);
        }
        foreach (var player in new[] { "P1", "P2" }) {
            var pass = new PassPriorityCommand();
            activated = await session.SubmitAsync(player, "azir-score-pass-" + player, pass, RawCommand(pass), default);
            AssertAccepted(activated); OfficialGraveyardRecastTests.Restore(activated.State); ArmamentMightTests.PublicRecovery(activated.State);
        }
        Assert.Equal("OTHER", activated.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", activated.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("TARGET", activated.State.CardObjects["GEAR"].AttachedToObjectId);
        activated = await AzirTestDriver.Finish(activated, attach, (player, choice) => session.SubmitAsync(player,
            "azir-score-attachment-choice", choice, RawCommand(choice), default));
        Assert.Equal(attach ? "HOST" : "TARGET", activated.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(attach ? "OTHER" : "F", activated.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Single(activated.State.StackItems, i => i.MovementObserver is not null);
        var result = await DriveBattleCloseToScoreVictoryAsync(session, activated, "azir-score");
        AssertScoreVictory(result); Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.EndTurn);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "SCORE_GAINED");
        Assert.Single(result.State.CardObjects.Values, c => c.Tags.Contains("金币"));
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initial, journal, result);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
