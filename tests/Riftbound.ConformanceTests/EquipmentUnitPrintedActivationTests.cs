using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EquipmentUnitPrintedActivationTests
{
    private static MatchState Position(string no)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", no, "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "READY", "OGN·146/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "COST", "OGN·219/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "NEXT", "OGN·001/298", "P1", "HAND");
        return OriannaEquipmentConsumerFixture.WithResources(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } });
    }

    private static bool Offered(MatchState state, string ability)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(candidate => candidate.Enabled && candidate.Action == CommandTypes.ActivateAbility
                && candidate.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(candidate => (IEnumerable<IReadOnlyDictionary<string, object?>>)candidate.Metadata!["sourceRequirements"]!)
            .Any(row => Equals(row.GetValueOrDefault("sourceObjectId"), "GEAR")
                && Equals(row.GetValueOrDefault("abilityId"), ability));

    // CN 178.1–178.2: becoming a unit retains its equipment text and permissions.
    [Theory]
    [InlineData("OGN·021/298", "SUN_DISC_NEXT_UNIT_READY")]
    [InlineData("OGN·023/298", P4ActivatedAbilityCatalog.MysteriousWeaponRecallAbilityId)]
    public async Task AnAnimatedEquipmentOnABattlefieldRetainsItsPrintedActivation(string no, string ability)
    {
        var game = new Driver(Position(no));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "GEAR");
        var generation = game.State.CardObjects["GEAR"].ObjectGeneration;
        await game.Act("P1", new MoveUnitCommand("GEAR", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["GEAR"]));
        await game.Top();
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations["GEAR"]);
        Assert.Contains(CardObjectTags.EquipmentCard, game.State.CardObjects["GEAR"].Tags);
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["GEAR"].Tags);
        Assert.True(Offered(game.State, ability));
        var targets = no == "OGN·023/298" ? new[] { "ALLY" } : [];
        var costs = no == "OGN·023/298" ? new[] { "DISCARD_HAND_CARD:COST" } : [];
        await RadGearTestPosition.Reject(game.State, "P2", new ActivateAbilityCommand("GEAR", ability, targets, costs));
        if (costs.Length > 0)
            await RadGearTestPosition.Reject(game.State, "P1", new ActivateAbilityCommand("GEAR", ability, targets));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", ability, targets, costs));
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.Equal(new ObjectBinding("GEAR", generation), Assert.Single(game.State.StackItems).SimpleActivation!.Source);
        if (costs.Length > 0) Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        if (no == "OGN·023/298")
            Assert.Contains(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects,
                effect => effect.StartsWith(CoreRuleEngine.MysteriousWeaponRecallPrefix, StringComparison.Ordinal));
        else {
            await game.Act("P1", new PlayCardCommand("NEXT", "OGN·001/298", [], Destination: "BASE"));
            Assert.False(game.State.CardObjects["NEXT"].IsExhausted);
        }
        await game.Replay();
    }
}
