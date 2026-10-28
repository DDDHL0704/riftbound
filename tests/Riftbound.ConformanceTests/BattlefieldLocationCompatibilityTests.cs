using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// A normal, accepted movement is what creates the null self-location in the
// first two positions. The wrong-ID case is a synthetic malformed-state control.
public sealed class BattlefieldLocationCompatibilityTests
{
    [Theory]
    [InlineData("P1", "missing")] [InlineData("P1", "null")] [InlineData("P1", "self")]
    [InlineData("P2", "missing")] [InlineData("P2", "null")] [InlineData("P2", "self")]
    public async Task ConquestOfARealBattlefieldKeepsTheVaynePaymentWindow(string player, string location)
    {
        var initial = Position(location, player);
        var opponent = player == "P1" ? "P2" : "P1";
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_BATTLEFIELD_LOCATION_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, player + "-" + location));
        try
        {
            await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
            await driver.Act(player, new PassFocusCommand());
            var conquered = await driver.Act(opponent, new PassFocusCommand());
            Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Equal(location == "self" ? "BF" : null, driver.State.ObjectLocations["BF"].BattlefieldObjectId);
            Assert.Equal("BF", driver.State.ObjectLocations["UNIT"].BattlefieldObjectId);
            var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
            Assert.Equal("TRIGGER_PAYMENT", payment.PaymentWindow);
            Assert.Equal(new[] { "SPEND_MANA:1", "DECLINE" }, payment.LegalPaymentChoiceIds);
            await driver.Act(player, new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["SPEND_MANA:1"]));
            Assert.Contains("UNIT", driver.State.PlayerZones[player].Battlefields);
            await driver.Top();
            Assert.Contains("UNIT", driver.State.PlayerZones[player].Hand);
            await RideTheWindCurrentMovementTests.Replay(initial, driver);
        }
        finally
        {
            driver.Export("location-diagnostic.json", new { initial, final = driver.State,
                commands = driver.Commands(), events = driver.Events() });
        }
    }

    [Fact]
    public async Task AContradictoryNonemptyBattlefieldReferenceDoesNotIdentifyThatField()
    {
        // This malformed location is injected for the defensive consumer check;
        // the test does not assert that full recovery accepts or rejects it.
        var initial = Position("wrong");
        var moved = await OfficialGraveyardRecastTests.Act(initial, "P1",
            new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        var first = await OfficialGraveyardRecastTests.Act(moved.State, "P1", new PassFocusCommand());
        var conquered = await OfficialGraveyardRecastTests.Act(first.State, "P2", new PassFocusCommand());
        if (Environment.GetEnvironmentVariable("RIFTBOUND_BATTLEFIELD_LOCATION_EVIDENCE") is { } root)
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "wrong-id.json"), JsonSerializer.Serialize(new {
                initial, final = conquered.State, conquered.Events
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Equal("OTHER", conquered.State.ObjectLocations["BF"].BattlefieldObjectId);
        Assert.Null(conquered.State.PendingPayment);
        Assert.DoesNotContain(conquered.State.TriggerQueue, item => item.UnitConquestReturn is not null);
        Assert.DoesNotContain(conquered.State.StackItems, item => item.UnitConquestReturn is not null);
    }

    [Theory]
    [InlineData("missing", true)] [InlineData("null", true)] [InlineData("self", true)]
    [InlineData("wrong", false)] [InlineData("empty", false)] [InlineData("zone", false)]
    [InlineData("hidden", false)] [InlineData("not-battlefield", false)] [InlineData("not-in-zone", false)]
    public void SyntheticBattlefieldIdentityGuardKeepsItsExistingBoundaries(string scenario, bool expected)
    {
        var state = Position(scenario);
        if (scenario is "empty" or "zone") state = state with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["BF"] = scenario == "empty" ? new("P2", "BATTLEFIELD", "") : new("P2", "BASE", "BF") } };
        if (scenario is "hidden" or "not-battlefield") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BF"] = scenario == "hidden" ? state.CardObjects["BF"] with { IsFaceDown = true }
                    : state.CardObjects["BF"] with { Tags = [] } } };
        if (scenario == "not-in-zone") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = [] } } };
        Assert.Equal(expected, InvokeGuard("Battlefield", state, "BF") is not null);
    }

    [Theory]
    [InlineData("self", true)] [InlineData("missing", false)] [InlineData("null", false)]
    [InlineData("wrong", false)] [InlineData("zone", false)]
    public void SyntheticUnitLocationMustStillPointPreciselyToTheField(string scenario, bool expected)
    {
        var state = Position("self");
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        if (scenario == "missing") locations.Remove("UNIT");
        else locations["UNIT"] = new("P1", scenario == "zone" ? "BASE" : "BATTLEFIELD",
            scenario == "self" ? "BF" : scenario == "wrong" ? "OTHER" : null);
        state = state with { ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with {
                    Base = state.PlayerZones["P1"].Base.Where(id => id != "UNIT").ToArray(), Battlefields = ["UNIT"] } } };
        Assert.Equal(expected, InvokeGuard("AtUnit", state, "UNIT") is not null);
    }

    [Fact]
    public void ComponentRecoveryRejectsTheWrongPlayerForTheFieldLocation()
    {
        var state = Position("self");
        state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            ["BF"] = new("P1", "BATTLEFIELD", "BF") } };
        Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(state), error =>
            error.Contains("object location BF P1/BATTLEFIELD disagrees with player zones P2/BATTLEFIELD", StringComparison.Ordinal));
    }

    private static CardObjectState? InvokeGuard(string method, MatchState state, string objectId)
        => (CardObjectState?)typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.BattlefieldLocalRules")!
            .GetMethod(method)!.Invoke(null, [state, objectId]);

    private static MatchState Position(string location, string player = "P1")
    {
        var initial = ConquestLifecycleRegressionTests.State("OGN·278/298", 0);
        var cards = initial.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var zones = initial.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        var locations = initial.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        if (player == "P2")
        {
            static string? Swap(string? id) => id == "P1" ? "P2" : id == "P2" ? "P1" : id;
            cards = cards.ToDictionary(e => e.Key, e => e.Value with {
                OwnerId = Swap(e.Value.OwnerId), ControllerId = Swap(e.Value.ControllerId) });
            zones = new Dictionary<string, PlayerZones> { ["P1"] = zones["P2"], ["P2"] = zones["P1"] };
        }
        var owner = player == "P1" ? "P2" : "P1";
        if (location == "missing") locations.Remove("BF");
        else locations["BF"] = new(owner, "BATTLEFIELD", location switch {
            "self" => "BF", "wrong" => "OTHER", _ => null });
        cards["UNIT"] = cards["UNIT"] with { CardNo = "OGN·035/298" };
        return initial with {
            RoomId = "battlefield-location-" + player + "-" + location,
            ActivePlayerId = player, TurnPlayerId = player, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { [player] = new(1, 0), [owner] = RunePool.Empty }
        };
    }
}
