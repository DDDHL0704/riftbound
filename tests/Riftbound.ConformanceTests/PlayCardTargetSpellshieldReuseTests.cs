using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayCardTargetSpellshieldReuseTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    public void RepeatedTargetsPayWardPerOccurrenceUnlessAnyTargetIsAtHaisu(
        bool useHaisu, int expectedMixedTax)
    {
        var state = GroupedTargetTests.OtherField(GroupedTargetTests.Group(2), "D2");
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["D"] = state.CardObjects["D"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] },
            ["D2"] = state.CardObjects["D2"] with { Tags = [CardObjectTags.UnitCard] }
        };
        if (useHaisu)
        {
            cards["BF2"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF2", "VEN·158", "P2")
                with { ControllerId = "P2" };
        }

        state = state with { CardObjects = cards, TurnNumber = 14, Tick = 500 };
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·029/298", out var spell));
        // The mana reduction makes the test callback observe each ordered selection.
        spell = spell with { TargetTraitsManaReductionTags = CardObjectTags.UnitCard };
        var taxBySelection = new Dictionary<(string, string), int>();

        ActionPromptBuilder.PlayCardLegalTargetSelections(state, "P1", spell, null,
            (targets, ward) =>
            {
                taxBySelection.Add((targets[0], targets[1]), ward);
                return true;
            });

        Assert.Equal(4, taxBySelection[("D", "D")]);
        Assert.Equal(expectedMixedTax, taxBySelection[("D", "D2")]);
        Assert.Equal(expectedMixedTax, taxBySelection[("D2", "D")]);
    }
}
