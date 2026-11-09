using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN824: the spell's dependent instruction uses its controller's current XP.
public sealed class PracticalExperienceLevelTests
{
    private const string Spell = "UNL-031/219";
    private static MatchState Position(int experience)
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", Spell, "P1", "HAND");
        s = RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
        s = RadGearTestPosition.Put(s, "POPPY", "UNL-178/219", "P1", "HAND");
        s = RadUnitEffectTestFixture.Place(s, "ALLY", "P1", "BATTLEFIELD", "BF");
        return s with { PlayerExperience = new Dictionary<string, int> { ["P1"] = experience, ["P2"] = 0 },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["ALLY"] = s.CardObjects["ALLY"] with { IsExhausted = true } } };
    }

    [Theory]
    [InlineData(0, false)] [InlineData(5, false)] [InlineData(6, false)] [InlineData(11, false)]
    [InlineData(0, true)] [InlineData(5, true)] [InlineData(6, true)] [InlineData(11, true)]
    public async Task EitherPlayersUnitReceivesOneOrThreeInsteadOfBothAndTheBuffExpires(int experience, bool enemy)
    {
        var game = new Driver(Position(experience)); var target = enemy ? "ENEMY" : "ALLY";
        var power = game.State.CardObjects[target].Power;
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, [target]));
        Assert.Equal(power, game.State.CardObjects[target].Power); await game.Top();
        Assert.Equal(power + (experience >= 6 ? 3 : 1), game.State.CardObjects[target].Power);
        Assert.Equal(experience, game.State.PlayerExperience["P1"]);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(power, game.State.CardObjects[target].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task SpendingExperienceInARealResponseChangesTheInstructionButNotAnAlreadyResolvedBuff(bool before)
    {
        var game = new Driver(Position(6)); var power = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY"]));
        if (!before) await game.Top();
        await game.Act("P1", new PlayCardCommand("POPPY", "UNL-178/219", [], Destination: "BATTLEFIELD:BF",
            Mode: "AMBUSH", OptionalCosts: ["SPEND_EXPERIENCE:3"]));
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        if (before) await game.Top();
        Assert.Equal(power + (before ? 1 : 3), game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task ASpellBorrowedFromTheOtherOwnerUsesItsCastingControllersExperience()
    {
        var s = Position(6); s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["SPELL"] = s.CardObjects["SPELL"] with { OwnerId = "P2" } } };
        var game = new Driver(s); var power = s.CardObjects["ALLY"].Power;
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY"])); await game.Top();
        Assert.Equal(power + 3, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task ARealReturnResponseDoesNotBuffTheTargetsPrivateNewIncarnation()
    {
        var game = new Driver(Position(6)); await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        var returned = MatchStateHasher.HashValue(game.State.CardObjects["ALLY"]);
        await game.Top(); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(returned, MatchStateHasher.HashValue(game.State.CardObjects["ALLY"])); await game.Replay();
    }
}
