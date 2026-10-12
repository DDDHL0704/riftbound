using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Theory]
    [InlineData("printed")]
    [InlineData("copied")]
    [InlineData("countered")]
    public async Task AuditedDistributedAttackConfirmsRespondsAllocatesAndReplaysThroughScoreVictory(string scenario)
    {
        var initial = AttackDamageDistributionTests.Position(copied: scenario == "copied");
        initial = LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·001/298", "F", "P1"), "ALLY", "P1", "BASE");
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
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = deck } } };
        OfficialGraveyardRecastTests.Restore(initial); ArmamentMightTests.PublicRecovery(initial);
        var journal = new RecordingMatchJournal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        var sequence = 0;
        async Task<ResolutionResult> Submit(MatchState state, string player, GameCommand command) {
            var result = await session.SubmitAsync(player, "attack-score-" + scenario + "-" + sequence++, command, RawCommand(command), default);
            AssertAccepted(result); OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
            return result;
        }
        var current = await Submit(initial, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        for (var index = 0; index < 4 && current.State.FocusPlayerId is not null; index++)
            current = await Submit(current.State, current.State.FocusPlayerId, new PassFocusCommand());
        current = await Submit(current.State, "P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY2", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        if (scenario == "copied") current = await Submit(current.State, "P1", new OrderTriggersCommand(
            OrderedTriggerIds: Enumerable.Reverse(current.State.TriggerQueue.Select(t => t.TriggerId)).ToArray()));
        while (current.State.PendingCardChoice is { ChoiceWindow: "TRIGGER_CONFIRMATION" } choice)
            current = await Submit(current.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                scenario == "countered" ? ["ENEMY1"] : ["ENEMY1", "ENEMY2", "ENEMY3"]));
        if (scenario == "countered") {
            var id = current.State.StackItems.Single().StackItemId;
            current = await Submit(current.State, "P1", new PassPriorityCommand());
            current = await Submit(current.State, "P2", new PlayCardCommand("RESPONSE", "UNL-106/219", ["ENEMY1", id]));
            for (var i = 0; i < 2; i++) current = await Submit(current.State, current.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Empty(current.State.StackItems); Assert.Null(current.State.PendingCardChoice);
            Assert.All(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, target => Assert.Equal(0, current.State.CardObjects[target].Damage));
            Assert.Equal("F", current.State.ObjectLocations["HOST"].BattlefieldObjectId);
            Assert.Single(current.Events, e => e.Kind == "STACK_ITEM_COUNTERED" && Equals(e.Payload["stackObjectKind"], "ABILITY"));
        } else {
            var count = scenario == "copied" ? 2 : 1;
            for (var i = 0; i < count; i++) {
                for (var pass = 0; pass < 2; pass++) current = await Submit(current.State, current.State.PriorityPlayerId!, new PassPriorityCommand());
                Assert.Equal("EFFECT_DAMAGE_DISTRIBUTION", current.State.PendingCardChoice!.ChoiceWindow);
                current = await Submit(current.State, "P1", AttackDamageDistributionTests.Allocation(current.State, ("ENEMY1", 2), ("ENEMY2", 2), ("ENEMY3", 1)));
            }
            Assert.Empty(current.State.StackItems); Assert.Equal(9, current.State.CardObjects["HOST"].Power);
        }
        var result = await DriveBattleCloseToScoreVictoryAsync(session, current, "attack-score-" + scenario);
        AssertScoreVictory(result); Assert.Equal("P1", result.State.WinnerPlayerId);
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "BATTLE_DECLARED");
        Assert.Contains(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "BATTLE_CLOSED");
        Assert.Contains(journal.Entries, e => e.CommandType == CommandTypes.EndTurn);
        Assert.Equal(scenario == "countered" ? 0 : scenario == "copied" ? 2 : 1,
            journal.Entries.Count(e => e.CommandType == CommandTypes.AssignEffectDamage));
        await AssertActionLogReplaysToFinalStateHashOnlyAsync(initial, journal, result);
        OfficialGraveyardRecastTests.Restore(result.State); ArmamentMightTests.PublicRecovery(result.State);
    }
}
