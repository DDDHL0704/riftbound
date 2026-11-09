using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GaiBeastGroupMightTests
{
    internal const string Beast = "SFD·175/221";
    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "BEAST", Beast, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "FIELD-ALLY", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "PRIVATE-ALLY", "OGN·219/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 4, [RuneTrait.Blue] = 4 }) } };
    }

    [Fact]
    public async Task ActualPlayBuffsOnlyOtherCurrentFriendlyUnitsUntilTheTurnEnds()
    {
        var game = new Driver(Position()); var before = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new PlayCardCommand("BEAST", Beast, []));
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power);
        var skill = Assert.Single(game.State.StackItems); Assert.Empty(skill.TargetObjectIds);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        await game.Top();
        Assert.Equal(before + 2, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(before + 2, game.State.CardObjects["FIELD-ALLY"].Power);
        Assert.Equal(5, game.State.CardObjects["BEAST"].Power);
        Assert.Equal(before, game.State.CardObjects["ENEMY"].Power);
        Assert.Equal(before, game.State.CardObjects["PRIVATE-ALLY"].Power);
        Assert.Single(game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(before, game.State.CardObjects["FIELD-ALLY"].Power);
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers); await game.Replay();
    }

    [Fact]
    public async Task ARealSourceReturnLeavesItsIndependentGroupInstructionIntact()
    {
        var game = new Driver(Position()); var before = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new PlayCardCommand("BEAST", Beast, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["BEAST"])); await game.Top();
        Assert.Contains("BEAST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"BEAST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top();
        Assert.Equal(before + 2, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(before + 2, game.State.CardObjects["FIELD-ALLY"].Power);
        Assert.Equal(5, game.State.CardObjects["BEAST"].Power);
        foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            Assert.DoesNotContain("\"BEAST\"", JsonSerializer.Serialize(view));
        Assert.All(new[] { "ALLY", "FIELD-ALLY" }, id =>
            Assert.Equal("BEAST", Assert.Single(game.State.CardObjects[id].UntilEndOfTurnPowerModifiers).SourceObjectId));
        await game.Replay();
    }

    [Fact]
    public async Task ReturningABeneficiaryBeforeResolutionLeavesItsPrivateIncarnationUnmodified()
    {
        var game = new Driver(Position()); var before = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new PlayCardCommand("BEAST", Beast, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(before, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(before + 2, game.State.CardObjects["FIELD-ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task AnEmptyFriendlyGroupStillEarnsTheTargetlessSkillAndCannotInjectATarget()
    {
        var state = Position();
        foreach (var id in new[] { "ALLY", "FIELD-ALLY" }) state = RadUnitEffectTestFixture.Place(state, id, "P1", "HAND");
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("BEAST", Beast, ["ENEMY"]));
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("BEAST", Beast, []));
        var skill = Assert.Single(game.State.StackItems); Assert.Empty(skill.TargetObjectIds);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
            StackItems = [skill with { TargetObjectIds = ["ENEMY"], TargetGenerations = new Dictionary<string, long> { ["ENEMY"] = 0 } }] }));
        await game.Top(); Assert.Equal(5, game.State.CardObjects["BEAST"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"); await game.Replay();
    }
}
