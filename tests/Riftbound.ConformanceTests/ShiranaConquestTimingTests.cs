using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ShiranaConquestTimingTests
{
    [Theory]
    [InlineData("P1", false)] [InlineData("P1", true)]
    [InlineData("P2", false)] [InlineData("P2", true)]
    public async Task ShiranaOffersItsBoonCostBeforeResponsesAndDrawsOnlyAfterThem(string player, bool accept)
    {
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·282/298", boon: true);
        // The cost source is protected; choosing a cost does not target it.
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { GrantedTags = ["法盾2"],
                Tags = initial.CardObjects["ALLY"].Tags.Append("法盾2").Order(StringComparer.Ordinal).ToArray() } } };
        var trace = new ThreeBattlefieldTimingProbe.Trace(initial, $"shirana-{player}-{accept}");
        try
        {
            var conquered = await trace.Conquer(player);
            Assert.Contains(conquered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.DoesNotContain(conquered.Events, e => e.Kind is "BOON_CONSUMED" or "CARD_DRAWN");
            Assert.Contains(CardObjectTags.Boon, trace.State.CardObjects["UNIT"].Tags);
            Assert.Contains(CardObjectTags.Boon, trace.State.CardObjects["ALLY"].Tags);
            Assert.Equal(initial.PlayerZones[player].MainDeck, trace.State.PlayerZones[player].MainDeck);
            var choice = Assert.IsType<PendingCardChoiceState>(trace.State.PendingCardChoice);
            Assert.Equal(player, choice.PlayerId);

            await trace.Choose(accept, "ALLY", targetMustBeChosen: accept);
            if (!accept)
            {
                Assert.Contains(CardObjectTags.Boon, trace.State.CardObjects["ALLY"].Tags);
                Assert.DoesNotContain(trace.Events, e => e.Kind is "BOON_CONSUMED" or "CARD_DRAWN");
                Assert.DoesNotContain(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerConsumeBoonDraw);
                return;
            }
            Assert.DoesNotContain(CardObjectTags.Boon, trace.State.CardObjects["ALLY"].Tags);
            Assert.Contains(CardObjectTags.Boon, trace.State.CardObjects["UNIT"].Tags);
            Assert.Equal(3, trace.State.CardObjects["ALLY"].Power);
            Assert.Contains("法盾2", trace.State.CardObjects["ALLY"].Tags);
            Assert.Single(trace.Events, e => e.Kind == "BOON_CONSUMED");
            Assert.DoesNotContain(trace.Events, e => e.Kind == "CARD_DRAWN");
            var paid = Assert.Single(trace.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerConsumeBoonDraw);
            Assert.Empty(paid.TargetObjectIds); // The chosen boon is a cost, not an effect target.
            Assert.Null(trace.State.PendingPayment);
            Assert.Equal(initial.RunePools[player].Mana, trace.State.RunePools[player].Mana);
            Assert.Equal(initial.RunePools[player].TotalPower, trace.State.RunePools[player].TotalPower);
            await trace.ReactWithPaidGust(player, TriggerKinds.BattlefieldConquerConsumeBoonDraw);
            Assert.DoesNotContain(trace.Events, e => e.Kind == "CARD_DRAWN");
            var resolved = await trace.Top();
            Assert.Single(resolved.Events, e => e.Kind == "CARD_DRAWN");
            Assert.Contains("DRAW1", trace.State.PlayerZones[player].Hand);
            Assert.Single(trace.Events, e => e.Kind == "BOON_CONSUMED");
        }
        finally { trace.Save(); }
    }
}
