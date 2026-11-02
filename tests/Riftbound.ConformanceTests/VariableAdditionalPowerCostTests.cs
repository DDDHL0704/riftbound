using System.Reflection;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VariableAdditionalPowerCostTests
{
    private static readonly CardBehaviorDefinition Variable = new("TEST", "variable fee", 2, "TEST", 0, 0)
        { MayPayAnyAdditionalPower = true };

    private static (bool Accepted, object?[] Plan) Plan(CardBehaviorDefinition behavior, params string[] costs)
    {
        object?[] args = [VenUnitEntryBasicTests.Position("RAD·023/167"), "P1", costs, behavior,
            null, null, null, null, null, null, null, null, null, null, null];
        var accepted = (bool)typeof(CoreRuleEngine).GetMethod("TryBuildOptionalCostPlan",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        return (accepted, args);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(65)] [InlineData(int.MaxValue)]
    public void DeclaredQuantityBecomesTheAdditionalGenericFeeAndKeepsItsOriginalReceipt(int amount)
    {
        var id = VariableAdditionalPowerCostRules.Prefix + amount;
        var result = Plan(Variable, id);
        Assert.True(result.Accepted);
        Assert.Equal(amount, result.Plan[6]);
        Assert.Equal([id], Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Plan[4]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(result.Plan[7]));
    }

    [Fact]
    public void OmittedQuantityMeansZeroAndCombinesWithAnIndependentTypedFee()
    {
        Assert.True(Plan(Variable).Accepted);
        Assert.Equal(0, Plan(Variable).Plan[6]);
        var result = Plan(Variable with { SourceDrawAdditionalPowerCost = 1,
            SourceDrawAdditionalPowerTrait = RuneTrait.Green, SourceDrawCountIfOptionalPowerCostPaid = 1 },
            "ADDITIONAL_POWER:3", "SPEND_POWER:green:1");
        Assert.True(result.Accepted);
        Assert.Equal(3, result.Plan[6]);
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(result.Plan[7])[RuneTrait.Green]);
    }

    [Theory]
    [InlineData(2, true)] [InlineData(3, false)]
    public void OrdinaryPaymentAuthorityAcceptsOnlyResourcesAvailableForTheDeclaredFee(int amount, bool accepted)
    {
        var result = Plan(Variable, "ADDITIONAL_POWER:" + amount);
        Assert.True(result.Accepted);
        var pools = new Dictionary<string, RunePool> { ["P1"] = new(0, 2) };
        var payment = new PaymentCostRules.PaymentPlan("variable-payment", "PLAY_CARD", "P1",
            genericPowerCost: (int)result.Plan[6]!, totalPowerCost: (int)result.Plan[6]!,
            optionalCostIds: Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Plan[4]));
        var committed = PaymentCostRules.TryCommitPayment(payment, pools);
        Assert.Equal(accepted, committed.Accepted);
        Assert.Equal(2, pools["P1"].Power);
        Assert.Equal(accepted ? 2 - amount : 2, committed.RunePools["P1"].Power);
        if (accepted)
        {
            var audit = PaymentCostRules.BuildCostPaidPayload(payment, committed.RunePools, null, new Dictionary<string, object?>());
            Assert.Equal(amount, audit["totalPowerCost"]);
            Assert.Equal(["ADDITIONAL_POWER:" + amount], Assert.IsType<string[]>(audit["optionalCosts"]));
        }
    }

    [Theory]
    [InlineData("")] [InlineData("-1")] [InlineData("+1")] [InlineData("01")]
    [InlineData("2147483648")] [InlineData("red:1")] [InlineData("1.0")]
    [InlineData("CHOOSE_AMOUNT")] [InlineData(" 1")] [InlineData("1:0")]
    public void InvalidQuantitiesCannotEnterTheCostPlan(string amount)
        => Assert.False(Plan(Variable, "ADDITIONAL_POWER:" + amount).Accepted);

    [Theory]
    [InlineData("ADDITIONAL_POWER:1", "ADDITIONAL_POWER:1")]
    [InlineData("ADDITIONAL_POWER:1", "ADDITIONAL_POWER:2")]
    public void DuplicateDeclarationsCannotBeNormalizedIntoOnePayment(string first, string second)
        => Assert.False(Plan(Variable, first, second).Accepted);

    [Fact]
    public void AddingAnIndependentEchoFeeCannotOverflowThePaymentQuantity()
    {
        var echo = Variable with { CardNo = "SFD·182/221" };
        Assert.False(Plan(echo, "ADDITIONAL_POWER:2147483647", "ECHO").Accepted);
    }

    [Fact]
    public void AnIndependentTypedFeeCannotOverflowTheTotalPowerAmount()
    {
        var typed = Variable with { SourceDrawAdditionalPowerCost = 1,
            SourceDrawAdditionalPowerTrait = RuneTrait.Green, SourceDrawCountIfOptionalPowerCostPaid = 1 };
        Assert.False(Plan(typed, "ADDITIONAL_POWER:2147483647", "SPEND_POWER:green:1").Accepted);
    }

    [Theory]
    [InlineData("ADDITIONAL_POWER:0")]
    [InlineData("ADDITIONAL_POWER:2")]
    public async Task AnUnrelatedPrintedCardCannotOptIntoTheNewPaymentMechanism(string cost)
    {
        var state = VenUnitEntryBasicTests.Position("RAD·023/167");
        var before = MatchStateHasher.Hash(state);
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("unauthorized-variable", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", "RAD·023/167", ["OTHER"], OptionalCosts: [cost]), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

}
