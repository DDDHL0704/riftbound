using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenModalReactionAvailabilityTests
{
    [Theory]
    [InlineData("RETURN_FRIENDLY_UNIT", "ENEMY", "P2", "ALLY")]
    [InlineData("ENEMY_UNIT_POWER_MINUS_2", "ALLY", "P1", "ENEMY")]
    public async Task TheOnlyLegalModeRemainsPlayableWhenTheOtherHasNoUnitTarget(
        string mode, string absentTarget, string owner, string legalTarget)
    {
        var initial = VenModalReactionTests.Position();
        initial = initial with {
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
                Base = p.Value.Base.Where(id => id != absentTarget).ToArray(),
                Battlefields = p.Value.Battlefields.Where(id => id != absentTarget).ToArray(),
                Graveyard = p.Key == owner ? [..p.Value.Graveyard, absentTarget] : p.Value.Graveyard }),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                [absentTarget] = new(owner, "GRAVEYARD") } };
        Assert.All(initial.PlayerZones.Values, zones => {
            Assert.DoesNotContain(absentTarget, zones.Base); Assert.DoesNotContain(absentTarget, zones.Battlefields); });
        Assert.Equal("GRAVEYARD", initial.ObjectLocations[absentTarget].Zone);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
            r => Equals(r["sourceObjectId"], "SPELL"));
        Assert.Equal(mode, requirement["mode"]);
        Assert.Contains(candidate.Modes!, m => m.Id == mode);
        Assert.Equal(1, requirement["minTargetCount"]); Assert.Equal(1, requirement["maxTargetCount"]);
        var targetRows = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(requirement["targetChoicesByIndex"]);
        var target = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(targetRows["0"]));
        Assert.Equal(legalTarget, target.Id);
        var wrongMode = mode == "RETURN_FRIENDLY_UNIT" ? "ENEMY_UNIT_POWER_MINUS_2" : "RETURN_FRIENDLY_UNIT";
        await OptionalEntryTargetBoundaryTests.Reject(initial, "P1", new PlayCardCommand("SPELL", "VEN·052", [legalTarget], Mode: wrongMode));
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand(Assert.IsType<string>(requirement["sourceObjectId"]), "VEN·052",
            [target.Id], Mode: Assert.IsType<string>(requirement["mode"])));
        await driver.Top();
        if (mode == "RETURN_FRIENDLY_UNIT") Assert.Contains(legalTarget, driver.State.PlayerZones["P1"].Hand);
        else Assert.Equal(2, driver.State.CardObjects[legalTarget].Power);
        Assert.Contains(absentTarget, driver.State.PlayerZones[owner].Graveyard);
        await driver.Replay(); VenModalReactionTests.Export("single-legal-mode-" + mode, initial, driver);
    }
}
