using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    [Theory, InlineData("SFD·177/221"), InlineData("SFD·177a/221")]
    public async Task AnActualAssembledSongEarnsASeparateChoiceAndIndependentAttackSkill(string no)
    {
        var initial = RadGearTestPosition.Put(Position(no), "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial); var tokens = await CreateTokens(game);
        await game.Act("P1", new PlayCardCommand("HOST", no, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(no, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var choices=0;
        while (game.State.PendingCardChoice is not null) await Select(game, tokens[choices++]);
        Assert.Equal(2, choices); Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, item => item.UnitEvent!.Origin is not null);
        Assert.Single(game.State.StackItems, item => item.UnitEvent!.Origin is null);
        Assert.All(tokens, id => Assert.Equal("BASE", game.State.ObjectLocations[id].Zone));
        await game.Top(); await game.Top();
        Assert.All(tokens, id => Assert.Equal("OTHER", game.State.ObjectLocations[id].BattlefieldObjectId));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnActualImageCopiesTheAttackTextAndItsCeasedSourceCannotSupplyALastKnownBattlefield(bool sourceLeaves)
    {
        var initial = RadGearTestPosition.Put(Position("SFD·177/221"), "TEMPLATE", "SFD·177/221", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); var tokens = await CreateTokens(game);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"])); await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal("SFD·177/221", image.CardNo);
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:OTHER", []));
        Assert.DoesNotContain(image.ObjectId, game.State.PendingCardChoice!.LegalObjectIds);
        await Select(game, tokens);
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(image.ObjectId, skill.SourceObjectId);
        if (sourceLeaves)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId])); await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
            Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        }
        await game.Top();
        Assert.All(tokens, id => Assert.Equal(sourceLeaves ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations[id].Zone));
        Assert.All(tokens, id => Assert.Equal(sourceLeaves ? null : "OTHER", game.State.ObjectLocations[id].BattlefieldObjectId));
        await game.ColdCheckpoint();
    }
}
