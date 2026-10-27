using System.Text.Json;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedPowerPoolTests
{
    public static IEnumerable<object[]> Purposes()
        => from spell in new[] { false, true }
           from purpose in new[] { PowerPaymentPurpose.Other, PowerPaymentPurpose.PlaySpell, PowerPaymentPurpose.Equipment }
           from colored in new[] { false, true } select new object[] { spell, purpose, colored };

    [Theory]
    [MemberData(nameof(Purposes))]
    public void OnlyThePermittedUseCanSpendTheRestrictedRainbowPower(bool spell, PowerPaymentPurpose purpose, bool colored)
    {
        var pool = new RunePool(0, 0, spellOnlyPower: spell ? 1 : 0, equipmentOnlyPower: spell ? 0 : 1);
        var expected = spell ? purpose == PowerPaymentPurpose.PlaySpell : purpose == PowerPaymentPurpose.Equipment;
        var plan = new PaymentCostRules.PaymentPlan("restricted-price", "TEST", "P1",
            genericPowerCost: colored ? 0 : 1, totalPowerCost: 1,
            powerCostByTrait: colored ? new Dictionary<string, int> { [RuneTrait.Green] = 1 } : null,
            powerPurpose: purpose);
        var before = JsonSerializer.Serialize(pool);
        var paid = PaymentCostRules.TryCommitPayment(plan, new Dictionary<string, RunePool> { ["P1"] = pool });
        Assert.Equal(expected, paid.Accepted);
        Assert.Equal(before, JsonSerializer.Serialize(pool));
        if (expected)
        {
            Assert.Equal(0, paid.RunePools["P1"].TotalStoredPower);
            Assert.Equal(0, paid.RunePools["P1"].Power); Assert.Empty(paid.RunePools["P1"].PowerByTrait);
        }
        else
        {
            Assert.Equal("INSUFFICIENT_COST", paid.ErrorCode); Assert.Same(pool, paid.RunePools["P1"]);
        }
    }

    [Fact]
    public void ASpellPaymentPreservesTheEquipmentBucketAndOrdinaryPower()
    {
        var initial = new RunePool(0, 1, spellOnlyPower: 1, equipmentOnlyPower: 1);
        var plan = new PaymentCostRules.PaymentPlan("spell-price", "PLAY_CARD", "P1", genericPowerCost: 1,
            totalPowerCost: 1, manaPurpose: ManaPaymentPurpose.PlaySpell);
        Assert.Equal(PowerPaymentPurpose.PlaySpell, plan.PowerPurpose);
        var result = PaymentCostRules.TryCommitPayment(plan, new Dictionary<string, RunePool> { ["P1"] = initial });
        Assert.True(result.Accepted); var remaining = result.RunePools["P1"];
        Assert.Equal(1, remaining.Power); Assert.Equal(0, remaining.SpellOnlyPower); Assert.Equal(1, remaining.EquipmentOnlyPower);
        Assert.Equal(2, remaining.TotalStoredPower);
    }

    [Fact]
    public void StateAndAllPublicResourceViewsRetainTheRestrictionThroughJsonRecovery()
    {
        var pool = new RunePool(2, 3, new Dictionary<string, int> { [RuneTrait.Green] = 1 },
            spellOnlyMana: 4, spellOnlyPower: 5, equipmentOnlyPower: 6);
        var initial = ImposedMovementContinuationTests.WithPool(ImposedMovementContinuationTests.Position("standard"), pool);
        var state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(initial))!;
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(state));
        Assert.Equal(pool, state.RunePools["P1"]); Assert.Equal(4, state.RunePools["P1"].TotalPower);
        Assert.Equal(15, state.RunePools["P1"].TotalStoredPower);
        foreach (var snapshot in ResolutionResult.BuildSnapshots(state).Values)
        {
            var view = JsonSerializer.SerializeToElement(snapshot.Players["P1"]).GetProperty("runePool");
            Assert.Equal(4, view.GetProperty("power").GetInt32());
            Assert.Equal(5, view.GetProperty("spellOnlyPower").GetInt32());
            Assert.Equal(6, view.GetProperty("equipmentOnlyPower").GetInt32());
        }
        ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData(0, -1, 0, false)] [InlineData(0, 0, -1, false)]
    [InlineData(0, int.MaxValue, 1, false)]
    [InlineData(int.MaxValue - 1, 1, 1, false)]
    [InlineData(int.MaxValue - 2, 1, 1, true)] [InlineData(0, 1, 1, true)]
    public void CombinedPowerCapacityPreservesInvalidInputForValidation(int ordinary, int spell, int equipment, bool supported)
    {
        var initial = new RunePool(0, ordinary, spellOnlyPower: spell, equipmentOnlyPower: equipment);
        var read = JsonSerializer.Deserialize<RunePool>(JsonSerializer.Serialize(initial))!;
        Assert.Equal(ordinary, read.Power); Assert.Equal(spell, read.SpellOnlyPower); Assert.Equal(equipment, read.EquipmentOnlyPower);
        Assert.Equal(supported, read.HasSupportedAmounts);
    }

    [Theory]
    [InlineData("spellOnlyPower", "\"1\"")] [InlineData("equipmentOnlyPower", "\"1\"")]
    [InlineData("spellOnlyPower", "null")] [InlineData("equipmentOnlyPower", "null")]
    [InlineData("spellOnlyPower", "2147483648")] [InlineData("equipmentOnlyPower", "2147483648")]
    public void ResourcePowerFieldsUseStrictIntegerJson(string field, string value)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RunePool>(
            "{\"mana\":0,\"power\":0,\"" + field + "\":" + value + "}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
