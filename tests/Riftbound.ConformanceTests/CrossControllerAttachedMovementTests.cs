using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CrossControllerAttachedMovementTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnExplicitTransferredHostMovesItsGearWithoutChangingEitherController(bool gearControlledByMover)
    {
        var initial = Position(gearControlledByMover); var game = new Driver(initial);
        try
        {
            var gear = initial.CardObjects["GEAR"]; var unit = initial.CardObjects["HOST"];
            await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
            Assert.Equal("P1", game.State.CardObjects["HOST"].OwnerId);
            Assert.Equal("P2", game.State.CardObjects["HOST"].ControllerId);
            Assert.Equal(unit.ObjectGeneration, game.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal(gear, game.State.CardObjects["GEAR"]);
            Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "F"), game.State.ObjectLocations["HOST"]);
            Assert.Equal(game.State.ObjectLocations["HOST"], game.State.ObjectLocations["GEAR"]);
            Assert.Contains("HOST", game.State.PlayerZones["P2"].Battlefields);
            Assert.Contains("GEAR", game.State.PlayerZones["P2"].Battlefields);
            Assert.Single(game.Events(), e => e.Event.Kind == "EQUIPMENT_MOVED_WITH_UNIT");
            Assert.Equal(RunePool.Empty, game.State.RunePools["P2"]); await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_CROSS_CONTROL_MOVE_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "cross-control-" + gearControlledByMover + ".json"), JsonSerializer.Serialize(new {
                    scope = "Explicit normal-card midgame state for controller/location invariance; actual stolen-host acquisition is separately covered by the unchanged combat consumer. Native move, per-command privacy/recovery/dedup/original replay; no complete opening or full L2/L3.",
                    initial, state = game.State, commands = game.Commands(), events = game.Events()
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
        }
    }

    [Theory]
    [InlineData("host-controller")] [InlineData("host-owner")]
    [InlineData("gear-controller")] [InlineData("gear-owner")]
    [InlineData("gear-zone")] [InlineData("gear-face-down")]
    public async Task UnknownIdentityForeignHostOrMisplacedGearStillRejectsWithoutMutation(string mutation)
    {
        var state = Position(false); var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        switch (mutation)
        {
            case "host-controller": cards["HOST"] = cards["HOST"] with { ControllerId = "P1" }; break;
            case "host-owner": cards["HOST"] = cards["HOST"] with { OwnerId = null }; break;
            case "gear-controller": cards["GEAR"] = cards["GEAR"] with { ControllerId = null }; break;
            case "gear-owner": cards["GEAR"] = cards["GEAR"] with { OwnerId = null }; break;
            case "gear-face-down": cards["GEAR"] = cards["GEAR"] with { IsFaceDown = true }; break;
        }
        state = state with { CardObjects = cards };
        if (mutation == "gear-zone") state = state with {
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
                Base = p.Key == "P1" ? ["GEAR"] : ["HOST"] }),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR"] = new("P1", "BASE") } };
        // Malformed starting-state guards use independent Core rejection checks;
        // these are not claimed as recoverable complete game histories.
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("cross-control-negative-" + mutation, "P2", CommandTypes.MoveUnit),
            new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    private static MatchState Position(bool gearControlledByMover)
    {
        var state = ArmamentMightTests.Position() with { Tick = 0, TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var host = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "OGN·001/298", "P1") with { ControllerId = "P2" };
        var gear = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·022/221", "P1") with {
            ControllerId = gearControlledByMover ? "P2" : "P1", AttachedToObjectId = "HOST", IsExhausted = true };
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["HOST"] = host, ["GEAR"] = gear },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [] },
                ["P2"] = state.PlayerZones["P2"] with { Base = ["HOST", "GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["HOST"] = new("P2", "BASE"), ["GEAR"] = new("P2", "BASE") },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty }
        };
    }
}
