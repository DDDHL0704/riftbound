using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleWinnerPhaseBoundaryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TwoActuallyPlayedSurvivingAttackersRetainTheirDeclaredConquestSourceOrder(bool reverse)
    {
        var initial = ReplaceHand(BattleWinnerPhaseScenarioTests.Build("plain"), "ATTACKER", "OGN·210/298", "P2");
        foreach (var (id, no) in new[] { ("ATTACKER-2", "OGN·210/298"), ("READY-2", "OGN·146/298") })
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P2") },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new("P2", "HAND") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Hand = [..initial.PlayerZones["P2"].Hand, id] } } };
        var run = new Driver(initial);
        try {
            await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
            await run.Act("P1", new EndTurnCommand());
            var runes = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray(); await run.Act("P2", new TapRuneCommand(runes[0], runes));
            foreach (var (unit, ready) in new[] { ("ATTACKER", "READY"), ("ATTACKER-2", "READY-2") }) {
                await run.Act("P2", new PlayCardCommand(unit, "OGN·210/298", [], Destination: "BASE"));
                await run.Act("P2", new PlayCardCommand(ready, "OGN·146/298", [unit])); await run.Top();
            }
            await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", [], ["ATTACKER", "ATTACKER-2"])); await PassFocus(run);
            var attackers = reverse ? new[] { "ATTACKER-2", "ATTACKER" } : ["ATTACKER", "ATTACKER-2"];
            await run.Act("P2", new DeclareBattleCommand("HOME", attackers, ["HOST"], ["COMBAT_ASSIGNMENT"]));
            await run.Act("P2", new AssignCombatDamageCommand("battle:HOME", "HOME", [new("ATTACKER", "HOST", 3), new("ATTACKER-2", "HOST", 3)]));
            await run.Act("P1", new AssignCombatDamageCommand("battle:HOME", "HOME", [new("HOST", "ATTACKER", 2)]));
            Assert.Equal(attackers[0], Assert.Single(run.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Event.Payload["sourceObjectId"]);
            Assert.All(attackers, id => Assert.Contains(id, run.State.PlayerZones["P2"].Battlefields));
            Assert.Equal(1, run.State.PlayerScores["P2"]); Assert.Null(run.State.PendingBattleResult); await run.Replay();
        }
        finally { Save("actual-two-conquerors-declared-order-" + reverse, initial, run); }
    }
}
