using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayCardTargetPaymentReuseTests
{
    private const string BellowsBreath = "SFD·080/221";

    [Theory]
    [InlineData(3, 1, 6)]
    [InlineData(12, 14, 500)]
    public void OrdinaryTargetCombinationsQuoteOnce(int targets, int turn, long tick)
    {
        var state = GroupedTargetTests.Group(targets) with { TurnNumber = turn, Tick = tick };
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(BellowsBreath, out var spell));

        var calls = 0;
        var selections = ActionPromptBuilder.PlayCardLegalTargetSelections(state, "P1", spell, null,
            (_, ward) =>
            {
                calls++;
                Assert.Equal(0, ward);
                return true;
            });

        Assert.Equal(1, calls);
        Assert.Contains(selections, selection => selection.SequenceEqual(new[] { "D", "D2", "D3" }));
        Assert.True(selections.Count >= 1 + targets + targets * (targets - 1) + targets * (targets - 1) * (targets - 2));
    }

    [Fact]
    public void DifferentSpellshieldTaxesAreQuotedIndependently()
    {
        var state = GroupedTargetTests.Group(8) with { TurnNumber = 14, Tick = 500 };
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["D"] = state.CardObjects["D"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] }
        };
        state = state with { CardObjects = cards };
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(BellowsBreath, out var spell));

        var quotedTaxes = new List<int>();
        var selections = ActionPromptBuilder.PlayCardLegalTargetSelections(state, "P1", spell, null,
            (_, ward) =>
            {
                quotedTaxes.Add(ward);
                return ward == 0;
            });

        Assert.Equal(2, quotedTaxes.Count);
        Assert.Contains(0, quotedTaxes);
        Assert.Contains(quotedTaxes, ward => ward > 0);
        Assert.Contains(selections, selection => selection.SequenceEqual(new[] { "D2", "D3", "D4" }));
        Assert.DoesNotContain(selections, selection => selection.Contains("D"));
    }

    [Fact]
    public void TargetDependentManaPricesAreNotShared()
    {
        var state = GroupedTargetTests.Group(8) with { TurnNumber = 14, Tick = 500 };
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(BellowsBreath, out var spell));
        spell = spell with { TargetTraitsManaReductionTags = CardObjectTags.UnitCard };
        var seen = new List<IReadOnlyList<string>>();

        var selections = ActionPromptBuilder.PlayCardLegalTargetSelections(state, "P1", spell, null,
            (targets, _) =>
            {
                seen.Add(targets);
                return targets.Contains("D");
            });

        Assert.Contains(seen, targets => targets.Contains("D"));
        Assert.Contains(seen, targets => !targets.Contains("D"));
        Assert.Equal(seen.Count(targets => targets.Contains("D")), selections.Count);
        Assert.All(selections, targets => Assert.Contains("D", targets));
    }
}
