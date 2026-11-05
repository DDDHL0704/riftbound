using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadZiggsActivationContinuityTests
{
    private const string Ability = "RAD_112_EXHAUST_PLAY_BOMB";

    private static async Task<(Driver Game, string Source)> Capture(bool attached)
    {
        var state = VenUnitEntryBasicTests.Position("UNL-200/219");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "RAD·112/167", "P1"),
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1") } };
        state = OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298");
        state = OfficialCopyIdentityTests.AddSpell(state, "SOUL", "SFD·163/221");
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "UNL-200/219", ["ALLY"]));
        await game.Top();
        await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal("RAD·112/167", image.CardNo);
        Assert.False(image.IsExhausted);
        Assert.Empty(game.State.StackItems);
        if (attached)
        {
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", image.ObjectId, ["ASSEMBLE_GREEN"]));
            await game.Top();
            Assert.Equal("RAD·112/167", game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        }
        var mana = game.State.RunePools["P1"].Mana;
        var command = new ActivateAbilityCommand(image.ObjectId, Ability, ["BF"]) {
            AbilityInstanceId = attached ? $"ATTACHED:GEAR:0:{game.State.CardObjects["GEAR"].AttachmentRevision}" : null };
        await game.Act("P1", command);
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects[image.ObjectId].IsExhausted);
        var context = Assert.Single(game.State.StackItems).SimpleActivation!;
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, context.SourceTokenFactoryCardNo);
        Assert.Equal(attached, context.Origin is not null);
        Assert.DoesNotContain(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        return (game, image.ObjectId);
    }

    private static async Task ResolveBomb(Driver game)
    {
        var resources = MatchStateHasher.HashValue(game.State.RunePools);
        var firstEvent = game.Events().Length;
        await game.Top();
        var bomb = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo);
        Assert.Equal("P1", bomb.OwnerId);
        Assert.Equal("P1", bomb.ControllerId);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), game.State.ObjectLocations[bomb.ObjectId]);
        Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
        Assert.Single(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "EQUIPMENT_TOKEN_CREATED");
        Assert.Equal(resources, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task CapturedNativeAndSongSkillsPlayOneBombAfterTheirRealImageSourceReturnsOrDies(
        bool attached, bool destroy)
    {
        var (game, source) = await Capture(attached);
        var skill = Assert.Single(game.State.StackItems);
        await game.Act("P1", destroy
            ? new PlayCardCommand("SOUL", "SFD·163/221", [source, "ALLY"])
            : new PlayCardCommand("RETURN", "OGN·104/298", [source]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(source));
        Assert.False(game.State.ObjectLocations.ContainsKey(source));
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        var remaining = Assert.Single(game.State.StackItems);
        Assert.Equal(skill.StackItemId, remaining.StackItemId);
        var receipt = Assert.IsType<CeasedSimpleActivationSource>(remaining.SimpleActivation!.CeasedSource);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, receipt.TokenFactoryCardNo);
        Assert.True(receipt.DepartedTick > remaining.SimpleActivation.AcquiredTick);
        await ResolveBomb(game);
        Assert.False(game.State.CardObjects.ContainsKey(source));
        foreach (var zones in game.State.PlayerZones.Values)
            Assert.DoesNotContain(source, zones.Base.Concat(zones.Battlefields).Concat(zones.Hand)
                .Concat(zones.MainDeck).Concat(zones.Graveyard).Concat(zones.Banished));
    }

    [Fact]
    public async Task CapturedSongSkillPlaysOneBombAfterRealDetachmentWithoutReadyingItsSource()
    {
        var (game, source) = await Capture(attached: true);
        var skill = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", [source, "GEAR"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        await ResolveBomb(game);
        Assert.True(game.State.CardObjects[source].IsExhausted);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
    }
}
