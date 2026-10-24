using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattleLimitedRecontestTests
{
    public BattleLimitedRecontestTests(Xunit.Abstractions.ITestOutputHelper output)
        => BattleLimitedTestSupport.Runtime(output);

    [Fact]
    public async Task ResultResponseArrivalStartsAFreshSameFieldRoleAfterTheOldBattleExpires()
    {
        var initial = BattleLimitedTestSupport.Position("plain");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ATTACKER"] = BattleLimitedCleanupTests.Printed("ATTACKER", "SFD·110/221", "P2") };
        var zones = new Dictionary<string, PlayerZones>(initial.PlayerZones);
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
        void Put(string id, string no, string owner, string zone) {
            cards[id] = BattleLimitedCleanupTests.Printed(id, no, owner);
            locations[id] = new(owner, zone);
            zones[owner] = zone == "BASE" ? zones[owner] with { Base = [..zones[owner].Base, id] }
                : zone == "LEGEND" ? zones[owner] with { LegendZone = [..zones[owner].LegendZone, id] }
                : zones[owner] with { Hand = [..zones[owner].Hand, id] };
        }
        Put("LATE", "OGN·210/298", "P1", "HAND");
        Put("WIND", "OGN·173/298", "P1", "HAND");
        Put("P2-ORANGE", "UNL-R04", "P2", "BASE");
        Put("LEGEND", "SFD·185/221", "P2", "LEGEND");
        foreach (var i in Enumerable.Range(1, 3)) Put("P1-FUND-" + i, "UNL-R06", "P1", "BASE");
        initial = initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var run = new Driver(initial);
        try
        {
            await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME"));
            await run.Act("P1", new PlayCardCommand("LATE", "OGN·210/298", [], Destination: "BASE"));
            await run.Act("P1", new EndTurnCommand());
            var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
            await run.Act("P2", new TapRuneCommand(funding[0], funding));
            await run.Act("P2", new RecycleRuneCommand("P2-ORANGE"));
            await run.Act("P2", new PlayCardCommand("ATTACKER", "SFD·110/221", [], Destination: "BASE"));
            await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"])); await run.Top();
            await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", [])); await run.Top();
            Assert.Equal(6, run.State.CardObjects["ATTACKER"].Power);
            await BattleLimitedTestSupport.PassFocus(run); await BattleLimitedTestSupport.Declare(run);
            Assert.Equal("RESULT_TRIGGERS", run.State.PendingBattleResult!.Stage);
            if (run.State.PriorityPlayerId != "P1") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            var funds = Enumerable.Range(1, 3).Select(i => "P1-FUND-" + i).ToArray();
            await run.Act("P1", new TapRuneCommand(funds[0], funds));
            await run.Act("P1", new PlayCardCommand("WIND", "OGN·173/298", ["LATE"], Destination: "BATTLEFIELD:HOME"));
            await run.Top();
            await run.Top();
            Assert.Null(run.State.PendingBattleResult);
            Assert.True(run.State.SpellDuelState.IsActive);
            Assert.Equal(2, run.Events().Count(e => e.Event.Kind == "BATTLE_ROLES_ESTABLISHED"));
            Assert.Single(run.State.StackItems, i => i.SourceObjectId == "ATTACKER"
                && i.FieldContext?.Kind is "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND");
            Assert.Equal(3, run.State.CardObjects["ATTACKER"].Power);
            Assert.Single(run.State.UntilEndOfTurnEffects, e => e.StartsWith("BATTLE_LIMITED_SCOPE:", StringComparison.Ordinal));
            await run.Top();
            Assert.Equal(6, run.State.CardObjects["ATTACKER"].Power);
            Assert.Single(run.State.CardObjects["ATTACKER"].UntilEndOfTurnPowerModifiers);
            await run.Replay();
        }
        finally { BattleLimitedCleanupTests.Save("same-field-recontest", initial, run); }
    }
}
