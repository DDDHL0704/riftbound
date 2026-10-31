using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayCostTargetlessPreviewTests
{
    [Fact]
    public void MissingTargetReturnsKnownCostAndMarksTargetDependentCost()
    {
        var state = Position(new(0, 1)) with
        {
            UntilEndOfTurnEffects = ["RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:DRAKE"],
            PlayerZones = new Dictionary<string, PlayerZones>(Position(new(0, 1)).PlayerZones)
            {
                ["P2"] = PlayerZones.Empty with { Battlefields = ["SHIELD"] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(Position(new(0, 1)).CardObjects)
            {
                ["SHIELD"] = new("SHIELD", cardNo: "OGN·013/298", ownerId: "P2", controllerId: "P2", power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield])
            }
        };
        var command = new PlayCardCommand("CARD", "OGS·003/024", []);

        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));

        Assert.True(quote.IsValid, quote.Message);
        Assert.False(quote.CanPay);
        Assert.True(quote.RequiresTargetSelection);
        Assert.True(quote.TargetDependentCosts);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower);
        Assert.Null(quote.Cost.RemainingMana);
        Assert.Contains(quote.Cost.Adjustments, adjustment => adjustment.Mana < 0);
    }

    [Fact]
    public void InvalidTargetQuoteReturnsTheCommandRejectionReason()
    {
        var state = Position(new(20, 20));
        var command = new PlayCardCommand("CARD", "OGS·003/024", ["UNKNOWN"]);

        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));

        Assert.False(quote.IsValid);
        Assert.Null(quote.Cost);
        Assert.Equal(ErrorCodes.InvalidTarget, quote.ErrorCode);
        Assert.Contains("requires", quote.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("当前选择无法打出", quote.Message);
    }

    private static MatchState Position(RunePool pool)
        => PlayCostPreviewTests.Position(pool, "OGS·003/024");
}
