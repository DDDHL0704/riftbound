using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

public sealed class ImposedMovementSourceTests
{
    [Theory]
    [InlineData("bound", 2)]
    [InlineData("friendly-gear", 1)]
    [InlineData("friendly-host", 1)]
    [InlineData("both-friendly", 0)]
    [InlineData("detached", 1)]
    [InlineData("wrong-host-generation", 1)]
    [InlineData("wrong-revision", 1)]
    [InlineData("wrong-field", 1)]
    [InlineData("face-down-gear", 1)]
    [InlineData("face-down-host", 0)]
    public async Task EffectiveImposersUseEachTextInstancesOwnControllerAndCurrentBinding(string boundary, int expected)
    {
        var initial = Position("standard") with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(1, 1) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Base = ["SONG"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["SONG"] = OfficialGarenConquestBattlefieldCountTests.Printed("SONG", "SFD·059/221", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SONG"] = new("P2", "BASE") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P2", new AssembleEquipmentCommand("SONG", "PATROL", ["ASSEMBLE_GREEN"]));
        await driver.Top();
        var current = driver.State;
        var cards = current.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = current.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        var zones = current.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        // These are explicit static source/binding boundary fixtures after a real
        // assemble. They do not certify native TAKE_CONTROL/HIDE/detach effects.
        if (boundary is "friendly-gear" or "both-friendly")
        {
            cards["SONG"] = cards["SONG"] with { ControllerId = "P1" };
            zones["P2"] = zones["P2"] with { Battlefields = zones["P2"].Battlefields.Where(id => id != "SONG").ToArray() };
            zones["P1"] = zones["P1"] with { Battlefields = zones["P1"].Battlefields.Append("SONG").ToArray() };
            locations["SONG"] = new("P1", "BATTLEFIELD", "FIELD2");
        }
        if (boundary is "friendly-host" or "both-friendly")
        {
            cards["PATROL"] = cards["PATROL"] with { ControllerId = "P1" };
            cards["FIELD2"] = cards["FIELD2"] with { ControllerId = "P1" };
            zones["P2"] = zones["P2"] with { Battlefields = zones["P2"].Battlefields.Where(id => id != "PATROL").ToArray() };
            zones["P1"] = zones["P1"] with { Battlefields = zones["P1"].Battlefields.Append("PATROL").ToArray() };
            locations["PATROL"] = new("P1", "BATTLEFIELD", "FIELD2");
        }
        if (boundary == "detached")
        {
            cards["SONG"] = cards["SONG"] with { AttachedToObjectId = null, AttachedRuleText = null };
            zones["P2"] = zones["P2"] with { Battlefields = zones["P2"].Battlefields.Where(id => id != "SONG").ToArray(), Base = ["SONG"] };
            locations["SONG"] = new("P2", "BASE");
        }
        if (boundary == "wrong-host-generation") cards["PATROL"] = cards["PATROL"] with { ObjectGeneration = cards["PATROL"].ObjectGeneration + 1 };
        if (boundary == "wrong-revision") cards["SONG"] = cards["SONG"] with { AttachmentRevision = cards["SONG"].AttachmentRevision + 1 };
        if (boundary == "wrong-field") locations["SONG"] = new("P2", "BATTLEFIELD", "FIELD1");
        if (boundary == "face-down-gear") cards["SONG"] = cards["SONG"] with { IsFaceDown = true };
        if (boundary == "face-down-host") cards["PATROL"] = cards["PATROL"] with { IsFaceDown = true };
        current = current with { ActivePlayerId = "P1", TurnPlayerId = "P1", PriorityPlayerId = null,
            RunePools = new Dictionary<string, RunePool>(current.RunePools) { ["P1"] = new(0, 2) },
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        var prompt = ResolutionResult.BuildPrompts(current)["P1"];
        var all = prompt.Candidates!;
        if (!all.Any(c => c.Action == CommandTypes.MoveUnit) && Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "source-boundary-" + boundary + ".json"), System.Text.Json.JsonSerializer.Serialize(new { current, prompt }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        }
        var candidate = all.Single(c => c.Action == CommandTypes.MoveUnit);
        var costs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(candidate.Metadata!["powerPerExtraUnitByDestination"]);
        Assert.Equal(expected, costs["BATTLEFIELD:FIELD2"]);
    }

    [Fact]
    public async Task OnlyUnitsThatCanActuallyExecuteTheEffectsGroupCountForItsCost()
    {
        var initial = Position("effect");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["D2"] = initial.CardObjects["D2"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer("P1")] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BATTLEFIELD:FIELD2"));
        await driver.Top();
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal("FIELD2", driver.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.Equal("FIELD1", driver.State.ObjectLocations["D2"].BattlefieldObjectId);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID");
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("restricted")]
    public async Task StandardGroupNeverSilentlyDropsOneOfItsDeclaredMovers(string boundary)
    {
        var initial = Position("standard");
        if (boundary == "restricted") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["B"] = initial.CardObjects["B"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer("P1")] } } };
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("invalid-standard-group", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: boundary == "duplicate" ? ["A", "A"] : ["A", "B"]), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task SimultaneousEffectMovementToBaseDoesNotPayABattlefieldImposedCost()
    {
        var initial = Position("effect"); var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BASE:P2"));
        await driver.Top(); Assert.Null(driver.State.PendingRuleChoice);
        Assert.All(new[] { "D", "D2" }, id => Assert.Equal("BASE", driver.State.ObjectLocations[id].Zone));
        Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }
}
