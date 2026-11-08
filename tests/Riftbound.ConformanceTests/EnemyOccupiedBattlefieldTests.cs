using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EnemyOccupiedBattlefieldTests
{
    private const string Spell = "RAD·007/167";
    private static MatchState Position()
        => RadGearTestPosition.Put(RadGearTestPosition.Create(), "CAST", Spell, "P1", "HAND");

    [Theory]
    [InlineData("BF")] [InlineData("OTHER")]
    public async Task EnemyOccupationAllowsEitherPhysicalOwnersBattlefield(string field)
    {
        var s = RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "BATTLEFIELD", field);
        var command = new PlayCardCommand("CAST", Spell, [field]);
        var quote = new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
        var prompt = ResolutionResult.BuildPrompts(s)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.Targets ?? [], target => target.Id == field);
        var game = new Driver(s); await game.Act("P1", command); await game.Top();
        var tokens = game.State.CardObjects.Values.Where(card => TokenObjectRules.IsToken(card)
            && card.Tags.Contains(CardObjectTags.UnitCard)).ToArray();
        Assert.Equal(3, tokens.Length);
        Assert.All(tokens, card => Assert.Equal(field, game.State.ObjectLocations[card.ObjectId].BattlefieldObjectId));
        await game.Replay();
    }

    [Theory]
    [InlineData("empty")] [InlineData("friendly")] [InlineData("unit")]
    public async Task EmptyFriendlyOccupiedAndOrdinaryUnitObjectsCannotSupplyTheBattlefieldTarget(string condition)
    {
        var s = Position();
        if (condition == "friendly") s = RadUnitEffectTestFixture.Place(s, "ALLY", "P1", "BATTLEFIELD", "OTHER");
        if (condition == "unit") s = RadUnitEffectTestFixture.Place(s, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var command = new PlayCardCommand("CAST", Spell, [condition == "unit" ? "ENEMY" : "OTHER"]);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command)).IsValid);
        await RadGearTestPosition.Reject(s, "P1", command);
    }
}
