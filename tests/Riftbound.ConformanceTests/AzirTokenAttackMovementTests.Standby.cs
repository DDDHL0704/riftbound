using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    [Fact]
    public async Task ARealPastTurnStandbySwapMakesTheSkillUseAzirsNewBattlefield()
    {
        const string swap = "UNL-083/219";
        var initial = RadUnitEffectTestFixture.Place(Position("SFD·177/221"), "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "SWAP", swap, "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "TEMPLATE", "OGN·219/298", "P2", "BASE");
        var runes = Enumerable.Range(0, 15).Select(i => "RUNE-" + i).ToArray();
        for (var i = 0; i < runes.Length; i++)
            initial = RadGearTestPosition.Put(initial, runes[i], i < 7 ? "UNL-R06" : "UNL-R02", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new HideCardCommand("SWAP", swap, "BATTLEFIELD:BF", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        await game.TapRunes("P1", runes);
        foreach (var id in new[] { runes[0], runes[7], runes[1] })
            await game.Act("P1", new RecycleRuneCommand(id));
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Contains(CardObjectTags.Ephemeral, image.Tags);
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:BF", []));
        var tokens = await CreateTokens(game);
        await EnterAndAttack(game, "SFD·177/221"); await Select(game, tokens);
        var skill = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new RevealCardCommand("SWAP", swap, ["HOST", image.ObjectId],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK"));
        await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("OTHER", game.State.ObjectLocations[image.ObjectId].BattlefieldObjectId);
        Assert.Equal(skill, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        Assert.All(tokens, id => Assert.Equal("BF", game.State.ObjectLocations[id].BattlefieldObjectId));
        await game.ColdCheckpoint();
    }
}
