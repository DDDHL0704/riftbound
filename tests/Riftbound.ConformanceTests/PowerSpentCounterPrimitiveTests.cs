using System.Text.Json;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PowerSpentCounterPrimitiveTests
{
    [Theory]
    [InlineData(0, 0)] [InlineData(1, 0)] [InlineData(0, 1)] [InlineData(1, 1)]
    public void OnlyTheActuallyDebitedPowerIsAddedToTheAdoptedResult(int generic, int red)
    {
        var original = new RunePool(0, 3, new Dictionary<string, int> { [RuneTrait.Red] = 3 }, paidPowerThisTurn: 4);
        var paid = PaymentCostRules.PayPowerCost(original, generic,
            new Dictionary<string, int> { [RuneTrait.Red] = red }, PowerPaymentPurpose.Other);
        Assert.Equal(4, original.PaidPowerThisTurn);
        Assert.Equal(4 + generic + red, paid.PaidPowerThisTurn);
        Assert.Equal(original.TotalPower - generic - red, paid.TotalPower);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void RestrictedAndTraitPowerCountForBothDefaultAndExplicitSourceSelection(bool explicitSources)
    {
        var original = new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }, equipmentOnlyPower: 2);
        var paid = PaymentCostRules.PayPowerCost(original, 2,
            new Dictionary<string, int> { [RuneTrait.Red] = 1 }, PowerPaymentPurpose.PlayEquipment,
            explicitSources ? new Dictionary<string, int> { ["EQUIPMENT_ONLY"] = 2 } : null);
        Assert.Equal(3, paid.PaidPowerThisTurn);
        Assert.Equal(0, paid.TotalStoredPower);
        Assert.Equal(0, original.PaidPowerThisTurn);
        Assert.Equal(3, original.TotalStoredPower);
    }

    [Fact]
    public void DiscardedQuotesDoNotSpendAndIncomeDoesNotEraseAnAdoptedDebit()
    {
        var original = new RunePool(0, 2);
        var quote = PaymentCostRules.PayPowerCost(original, 1, new Dictionary<string, int>(), PowerPaymentPurpose.Other);
        var secondQuote = PaymentCostRules.PayPowerCost(original, 1, new Dictionary<string, int>(), PowerPaymentPurpose.Other);
        Assert.Equal(0, original.PaidPowerThisTurn);
        Assert.Equal(quote, secondQuote);
        var adoptedWithIncome = quote with { Power = quote.Power + 1 };
        Assert.Equal(original.TotalPower, adoptedWithIncome.TotalPower);
        Assert.Equal(1, adoptedWithIncome.PaidPowerThisTurn);
        var secondPayment = PaymentCostRules.PayPowerCost(adoptedWithIncome, 1,
            new Dictionary<string, int>(), PowerPaymentPurpose.Other);
        Assert.Equal(2, secondPayment.PaidPowerThisTurn);
        Assert.Equal(1, adoptedWithIncome.PaidPowerThisTurn);
    }

    [Fact]
    public void TheSharedLegacyRuneCostAdapterRetainsTheAdoptedDebit()
    {
        var pools = new Dictionary<string, RunePool> { ["P1"] = new(3, 3, paidPowerThisTurn: 1) };
        var paid = PaymentCostRules.PayRuneCosts(pools, "P1", 2, 2);
        Assert.Equal(1, paid["P1"].Mana);
        Assert.Equal(1, paid["P1"].Power);
        Assert.Equal(3, paid["P1"].PaidPowerThisTurn);
        Assert.Equal(1, pools["P1"].PaidPowerThisTurn);
    }

    [Fact]
    public void BalanceEqualityPreservesItsContractWhileSerializedHashesRetainTheDebitHistory()
    {
        var paid = new RunePool(0, 0, paidPowerThisTurn: 2);
        var restored = JsonSerializer.Deserialize<RunePool>(JsonSerializer.Serialize(paid));
        Assert.Equal(paid, restored);
        Assert.Equal(2, restored!.PaidPowerThisTurn);
        Assert.Equal(RunePool.Empty, paid);
        Assert.Equal(RunePool.Empty.GetHashCode(), paid.GetHashCode());
        Assert.NotEqual(MatchStateHasher.HashValue(RunePool.Empty), MatchStateHasher.HashValue(paid));
        Assert.Equal(0, RunePool.Empty.PaidPowerThisTurn);
    }

    [Fact]
    public void InvalidOrUnrepresentableCountersCannotBecomeAValidPaymentResult()
    {
        Assert.False(new RunePool(0, 1, paidPowerThisTurn: -1).HasSupportedAmounts);
        var maximum = new RunePool(0, 1, paidPowerThisTurn: long.MaxValue);
        Assert.False(PaymentCostRules.CanPayPowerCost(maximum, 1, new Dictionary<string, int>()));
        Assert.True(PaymentCostRules.CanPayPowerCost(maximum, 0, new Dictionary<string, int>()));
        Assert.Throws<ResourceNumericLimitException>(() => PaymentCostRules.PayPowerCost(maximum, 1,
            new Dictionary<string, int>(), PowerPaymentPurpose.Other));
        Assert.Equal(long.MaxValue, maximum.PaidPowerThisTurn);
        Assert.Equal(1, maximum.Power);
    }

    [Fact]
    public void TheTurnCounterCanAccumulateMoreThanOneMaximumWalletWithoutNarrowing()
    {
        var first = new RunePool(0, int.MaxValue, paidPowerThisTurn: int.MaxValue);
        Assert.True(PaymentCostRules.CanPayPowerCost(first, int.MaxValue, new Dictionary<string, int>()));
        var paid = PaymentCostRules.PayPowerCost(first, int.MaxValue, new Dictionary<string, int>(), PowerPaymentPurpose.Other);
        Assert.Equal(2L * int.MaxValue, paid.PaidPowerThisTurn);
        Assert.Equal(0, paid.Power);
        var restored = JsonSerializer.Deserialize<RunePool>(JsonSerializer.Serialize(paid));
        Assert.Equal(paid.PaidPowerThisTurn, restored!.PaidPowerThisTurn);
        Assert.Equal(MatchStateHasher.HashValue(paid), MatchStateHasher.HashValue(restored));
    }

}
