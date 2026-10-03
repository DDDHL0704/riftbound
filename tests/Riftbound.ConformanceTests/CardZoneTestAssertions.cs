using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class CardZoneTestAssertions
{
    internal static void RetainedOutsidePlay(MatchState state, string objectId, string expectedZone)
    {
        Assert.True(state.CardObjects.TryGetValue(objectId, out var card), "The physical card must remain identifiable.");
        Assert.False(string.IsNullOrWhiteSpace(card.CardNo));
        Assert.Equal(expectedZone, state.ObjectLocations[objectId].Zone);
        Assert.Equal(card.OwnerId, state.ObjectLocations[objectId].PlayerId);
        Assert.Equal(card.OwnerId, card.ControllerId);
        Assert.All(state.PlayerZones.Values, zones =>
        {
            Assert.DoesNotContain(objectId, zones.Base);
            Assert.DoesNotContain(objectId, zones.Battlefields);
        });
        Assert.Equal(0, card.Damage);
        Assert.Equal(0, card.UntilEndOfTurnPowerModifier);
        Assert.Empty(card.UntilEndOfTurnEffects);
        Assert.Empty(card.UntilEndOfTurnPowerModifiers);
        Assert.Null(card.AttachedToObjectId);
        Assert.False(card.IsAttacking || card.IsDefending || card.IsExhausted || card.IsFaceDown);
    }
}
