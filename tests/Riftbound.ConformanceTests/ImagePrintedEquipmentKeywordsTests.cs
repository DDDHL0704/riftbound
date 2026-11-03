using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Factory boundaries for CN 479.1.b.1 / 479.1.c. Actual reflexive resolution
// and Orianna's continuous type effect are separate consumers of these values.
public sealed class ImagePrintedEquipmentKeywordsTests
{
    [Theory]
    [InlineData("RAD·062/167", "部署", false)]
    [InlineData("RAD·062/167", "部署", true)]
    [InlineData("RAD·123/167", "灵便", false)]
    [InlineData("RAD·123/167", "灵便", true)]
    [InlineData("SFD·059/221", "武装", false)]
    [InlineData("SFD·059/221", "武装", true)]
    public void ImageKeepsPrintedEquipmentKeywordsAndDropsSubsequentCharacteristics(
        string cardNo, string keyword, bool grantsEphemeral)
    {
        Assert.True(PrintedCardFactory.TryCreate("SOURCE", "P2", cardNo, out var printed));
        var source = printed with {
            ControllerId = "P2", Power = 99, ManaCost = 99, Damage = 3,
            IsExhausted = true, IsAttacking = true, UntilEndOfTurnPowerModifier = 11,
            UntilEndOfTurnEffects = ["STUNNED"], AttachedToObjectId = "OTHER",
            Tags = [..printed.Tags, CardObjectTags.UnitCard, "游走"],
            GrantedTags = [CardObjectTags.UnitCard, "游走"] };
        var image = CopyCharacteristics.CreateImage("IMAGE", "P1", source, grantsEphemeral);

        Assert.Equal("IMAGE", image.ObjectId);
        Assert.Equal(cardNo, image.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, image.TokenFactoryCardNo);
        Assert.True(TokenObjectRules.IsToken(image));
        Assert.Equal("P1", image.OwnerId); Assert.Equal("P1", image.ControllerId);
        Assert.Equal(printed.ManaCost, image.ManaCost); Assert.Equal(printed.Power, image.Power);
        Assert.Contains(CardObjectTags.EquipmentCard, image.Tags);
        Assert.Contains(keyword, image.Tags);
        Assert.DoesNotContain(CardObjectTags.UnitCard, image.Tags);
        Assert.DoesNotContain("游走", image.Tags);
        Assert.Empty(image.GrantedTags ?? []);
        Assert.Equal(grantsEphemeral, image.Tags.Contains(CardObjectTags.Ephemeral));
        Assert.False(image.IsExhausted); Assert.False(image.IsAttacking);
        Assert.Equal(0, image.Damage); Assert.Equal(0, image.UntilEndOfTurnPowerModifier);
        Assert.Empty(image.UntilEndOfTurnEffects); Assert.Null(image.AttachedToObjectId);
        Assert.Equal(99, source.Power); Assert.Equal(3, source.Damage);
    }

    [Theory]
    [InlineData("RAD·062/167", "部署")]
    [InlineData("RAD·123/167", "灵便")]
    [InlineData("SFD·059/221", "武装")]
    public void AnotherImageKeepsTheCopiedEquipmentFaceAndItsPrintedKeywords(string cardNo, string keyword)
    {
        Assert.True(PrintedCardFactory.TryCreate("SOURCE", "P2", cardNo, out var source));
        var first = CopyCharacteristics.CreateImage("FIRST", "P2", source, grantsEphemeral: false);
        var modified = first with { Tags = [CardObjectTags.UnitCard, "游走"],
            GrantedTags = ["游走"], Power = 99, ManaCost = 99 };
        var second = CopyCharacteristics.CreateImage("SECOND", "P1", modified, grantsEphemeral: false);

        Assert.Equal(cardNo, second.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, second.TokenFactoryCardNo);
        Assert.Equal(source.Power, second.Power); Assert.Equal(source.ManaCost, second.ManaCost);
        Assert.Contains(CardObjectTags.EquipmentCard, second.Tags);
        Assert.Contains(keyword, second.Tags);
        Assert.DoesNotContain(CardObjectTags.UnitCard, second.Tags);
        Assert.DoesNotContain("游走", second.Tags);
    }

    [Fact]
    public void ClaimedEquipmentKeywordsCannotSubstituteForAnAuthoritativeFace()
    {
        var source = new CardObjectState("SOURCE", cardNo: "UNKNOWN-PRINT", ownerId: "P2", controllerId: "P2",
            tags: [CardObjectTags.EquipmentCard, "部署", "灵便", "武装"]);
        Assert.Throws<InvalidOperationException>(() => CopyCharacteristics.CreateImage("IMAGE", "P1", source));
    }
}
