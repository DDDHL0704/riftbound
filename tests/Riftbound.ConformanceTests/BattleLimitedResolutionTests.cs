using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Current CN383.4e-f /433.1 /466.2c /468.7c; canonical real commands,
// original driver current-version cold/duplicate and full action replay retained.
public sealed class BattleLimitedResolutionTests
{
    public BattleLimitedResolutionTests(Xunit.Abstractions.ITestOutputHelper output)
        => BattleLimitedTestSupport.Runtime(output);

    [Theory]
    [InlineData("fiora-snapshot-late-ally")]
    [InlineData("steadfast-any-unit")]
    [InlineData("fiora-real-close")]
    public async Task CurrentMightSnapshotAndBattleDuration(string mode)
    {
        var initial = Position(mode); var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
        await run.Act("P1", new EndTurnCommand());
        var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(funding[0], funding));
        await run.Act("P2", new RecycleRuneCommand("P2-ORANGE"));
        if (mode == "fiora-snapshot-late-ally") {
            await run.Act("P2", new RecycleRuneCommand("P2-PURPLE"));
            await run.Act("P2", new PlayCardCommand("ALLY", "OGN·210/298", [], Destination: "BASE"));
        }
        var attacker = mode == "steadfast-any-unit" ? "OGN·210/298" : "SFD·110/221";
        await run.Act("P2", new PlayCardCommand("ATTACKER", attacker, [], Destination: "BASE"));
        await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"])); await run.Top();
        await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", []));
        if (mode != "steadfast-any-unit")
            Assert.Single(run.State.StackItems, item => item.FieldContext?.Kind == "BATTLE_DOUBLE_SELF_ATTACK");
        if (mode == "steadfast-any-unit") {
            var choice = Assert.IsType<PendingCardChoiceState>(run.State.PendingCardChoice);
            Assert.Contains("ATTACKER", choice.LegalObjectIds);
            Assert.Equal("P1", choice.PlayerId);
            await run.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ATTACKER"]));
            await run.Top();
            Assert.Contains(run.State.CardObjects["ATTACKER"].UntilEndOfTurnEffects, x => x.StartsWith("COMBAT_KEYWORD:坚守2:"));
            Assert.Contains(run.State.ContinuousEffects, x => x.TargetObjectId == "ATTACKER" && x.Duration == "UNTIL_END_OF_BATTLE"
                && x.Layer == "RULE_TEXT" && x.SourceObjectId == "HOME");
            Assert.Equal(2, run.State.CardObjects["ATTACKER"].Power);
        }
        else {
            Assert.Null(run.State.PendingCardChoice); await run.Top();
            Assert.Equal(6, run.State.CardObjects["ATTACKER"].Power);
            var modifier = Assert.Single(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers);
            Assert.Equal(3, modifier.SnapshotMight); Assert.Equal(3, modifier.PowerDelta); Assert.Equal("UNTIL_END_OF_BATTLE", modifier.Duration);
            if (mode == "fiora-snapshot-late-ally") {
                if (run.State.FocusPlayerId != "P2") await run.Act(run.State.FocusPlayerId!, new PassFocusCommand());
                await run.Act("P2", new PlayCardCommand("WIND", "OGN·173/298", ["ALLY"], Destination: "BATTLEFIELD:HOME")); await run.Top();
                Assert.True(run.State.CardObjects["ALLY"].IsAttacking); Assert.Equal(6, run.State.CardObjects["ATTACKER"].Power);
                Assert.Equal(modifier, Assert.Single(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers));
            }
            else {
                for (var i = 0; run.State.FocusPlayerId is not null; i++) { Assert.InRange(i, 0, 3); await run.Act(run.State.FocusPlayerId, new PassFocusCommand()); }
                await run.Act("P2", new DeclareBattleCommand("HOME", ["ATTACKER"], ["HOST"], ["COMBAT_ASSIGNMENT"]));
                for (var i = 0; run.State.PendingBattleResult is not null; i++) {
                    Assert.InRange(i, 0, 7); await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
                }
                Assert.Contains(run.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
                Assert.Equal(3, run.State.CardObjects["ATTACKER"].Power);
                Assert.Empty(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers);
                Assert.DoesNotContain(run.State.UntilEndOfTurnEffects, x => x.StartsWith("BATTLE_LIMITED_") || x.StartsWith("BATTLE_ROLE_"));
                Assert.DoesNotContain(run.State.CardObjects.Values.SelectMany(c => c.UntilEndOfTurnEffects), x => x.StartsWith("BATTLE_LIMITED_"));
            }
        }
        await run.Replay();
    }

    private static MatchState Position(string mode)
    {
        var state = BattleLimitedTestSupport.Position("plain");
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones);
        cards["HOME"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOME", mode == "steadfast-any-unit" ? "OGN·279/298" : "OGN·278/298", "P1") with { ControllerId = "P1" };
        cards["ATTACKER"] = OfficialGarenConquestBattlefieldCountTests.Printed("ATTACKER", mode == "steadfast-any-unit" ? "OGN·210/298" : "SFD·110/221", "P2");
        void Put(string id, string no, string zone) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P2"); locations[id] = new("P2", zone);
            zones["P2"] = zone == "BASE" ? zones["P2"] with { Base = [..zones["P2"].Base, id] }
                : zones["P2"] with { Hand = [..zones["P2"].Hand, id] };
        }
        Put("P2-ORANGE", "UNL-R04", "BASE");
        if (mode == "fiora-snapshot-late-ally") { Put("P2-PURPLE", "UNL-R05", "BASE"); Put("ALLY", "OGN·210/298", "HAND"); Put("WIND", "OGN·173/298", "HAND"); }
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
