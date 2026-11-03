using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedDestructionPosition()
        => RadUnitEffectTestFixture.Place(BorrowedRecyclePosition("RAD·022/167"), "HOST", "P1", "BATTLEFIELD", "F");

    [Theory]
    [InlineData("随从", "GEAR", false)]
    [InlineData("随从", "DONOR", false)]
    [InlineData("机器人", "ALLY", false)]
    [InlineData("机器人", "HOST", false)]
    [InlineData("机器人", "HOST", true)]
    public async Task BorrowedDestructionCostsCanConsumeTheBorrowerOrDonorWithoutErasingTheTokenSkill(
        string family, string cost, bool token)
    {
        var initial = BorrowedDestructionPosition();
        if (token) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, initial.CardObjects["HOST"].ObjectGeneration,
                    P6TokenFactoryCatalog.ImageTokenCardNo, initial.CardObjects["HOST"].CardNo!)] } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "战力的" + family);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [], ["DESTROY_FRIENDLY_PERMANENT:" + cost]));
        if (token) Assert.False(game.State.CardObjects.ContainsKey(cost));
        else Assert.Contains(cost, game.State.PlayerZones["P1"].Graveyard);
        if (cost != "HOST") Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        Assert.Empty(BorrowedCreatedUnits(game.State));
        Assert.Equal(initial.RunePools["P1"], game.State.RunePools["P1"]);
        await game.Top();
        await FinishBorrowedTokenLocations(game);
        var created = Assert.Single(BorrowedCreatedUnits(game.State));
        Assert.Equal(family == "机器人" ? 3 : 1, created.Power);
        Assert.Contains(created.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("随从", "ALLY")]
    [InlineData("机器人", "GEAR")]
    public async Task BorrowedDestructionCostsRejectWrongTypesOwnershipCountsAndSourceLocation(string family, string wrongType)
    {
        var initial = BorrowedDestructionPosition();
        var id = TemplateId(initial, "战力的" + family);
        var good = "DESTROY_FRIENDLY_PERMANENT:" + (family == "随从" ? "GEAR" : "ALLY");
        async Task Reject(MatchState state, string[] costs, string[]? targets = null)
        {
            var hash = MatchStateHasher.Hash(state);
            var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-borrowed-destruction", "P1", CommandTypes.ActivateAbility),
                new ActivateAbilityCommand("HOST", id, targets ?? [], costs), default);
            Assert.False(result.Accepted);
            Assert.Empty(result.Events);
            Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        }
        await Reject(initial, []);
        await Reject(initial, [good, good]);
        foreach (var bad in new[] { wrongType, "ENEMY-G", "G1", "MISSING" })
            await Reject(initial, ["DESTROY_FRIENDLY_PERMANENT:" + bad]);
        await Reject(initial, [good], ["ALLY"]);
        await Reject(RadUnitEffectTestFixture.Place(initial, "HOST", "P1", "HAND"), [good]);
    }
}
