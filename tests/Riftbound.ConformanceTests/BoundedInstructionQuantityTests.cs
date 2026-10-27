using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;
using static Riftbound.ConformanceTests.BoundedResourceQuantityTests;

namespace Riftbound.ConformanceTests;

public sealed class BoundedInstructionQuantityTests
{
    [Theory]
    [InlineData("any", 0)] [InlineData("any", 1)] [InlineData("any", 63)]
    [InlineData("any", 64)] [InlineData("any", 65)] [InlineData("any", int.MaxValue)]
    [InlineData("red", 0)] [InlineData("red", 1)] [InlineData("red", 63)]
    [InlineData("red", 64)] [InlineData("red", 65)] [InlineData("red", int.MaxValue)]
    public async Task GenericAndSingleTraitMenusKeepTheirOwnSmallAndLargeBoundaries(string trait, int available)
    {
        var driver = await OpenInstruction(InstructionPosition(trait, available));
        var options = driver.State.PendingRuleChoice!.Request.Options;
        var prefix = trait == "any" ? "SPEND_POWER:" : "SPEND_POWER:red:";
        var count = trait == "any" ? (long)available + 1 : available;
        var expected = count > 64 ? new[] { prefix + "CHOOSE_AMOUNT" }
            : Enumerable.Range(trait == "any" ? 0 : 1, (int)count).Select(n => prefix + n).ToArray();
        var actual = options.Where(o => o.Id.StartsWith(prefix, StringComparison.Ordinal)
            && (trait != "any" || o.Id.Count(c => c == ':') == 1)).Select(o => o.Id).ToArray();
        if (count > 64 && trait == "any") expected = ["SPEND_POWER:0", .. expected];
        Assert.Equal(expected.Order(), actual.Order());
        Assert.Contains(options, o => o.Id == "SPEND_POWER:0");
    }

    [Theory]
    [InlineData("any", 0)] [InlineData("any", 1)] [InlineData("any", 63)]
    [InlineData("any", 64)] [InlineData("any", 65)] [InlineData("any", int.MaxValue)]
    [InlineData("red", 1)] [InlineData("red", 63)] [InlineData("red", 64)]
    [InlineData("red", 65)] [InlineData("red", int.MaxValue)]
    public async Task LargeInstructionQuantitiesRemainUnpaidUntilTheirExactChoiceFinishes(string trait, int amount)
    {
        // Empty chosen battlefield isolates payment from damage arithmetic; the
        // retained original S5 damage suites cover real damage/death consumers.
        var initial = InstructionPosition(trait, int.MaxValue); var driver = await OpenInstruction(initial);
        var marker = trait == "any" ? "SPEND_POWER:CHOOSE_AMOUNT" : "SPEND_POWER:red:CHOOSE_AMOUNT";
        await Decide(driver, marker);
        var decisions = await SelectQuantity(driver, amount, "INSTRUCTION_POWER_PAYMENT", () => {
            Assert.Equal(int.MaxValue, driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"].TotalPower);
            Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "COST_PAID"
                && e.Event.Payload.GetValueOrDefault("paymentWindow") as string == "INSTRUCTION_POWER_PAYMENT");
        });
        Assert.InRange(decisions, 1, 31); Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(int.MaxValue - amount, driver.State.RunePools["P1"].TotalPower);
        if (trait == "red") Assert.Equal(int.MaxValue - amount, driver.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        var cost = Assert.Single(driver.Events(), e => e.Event.Kind == "COST_PAID"
            && e.Event.Payload.GetValueOrDefault("paymentWindow") as string == "INSTRUCTION_POWER_PAYMENT");
        Assert.Equal(amount, cost.Event.Payload["power"]); Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task LargeSingleTraitPaymentPreservesUnrelatedPower()
    {
        var initial = WithPool(InstructionPosition("any", 0), new(1, 0, new Dictionary<string, int> { [RuneTrait.Red] = 65, [RuneTrait.Blue] = 7 }));
        var driver = await OpenInstruction(initial); await Decide(driver, "SPEND_POWER:red:CHOOSE_AMOUNT");
        await SelectQuantity(driver, 65, "INSTRUCTION_POWER_PAYMENT");
        Assert.Equal(7, driver.State.RunePools["P1"].TotalPower); Assert.Equal(7, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AConvertedLargeBalanceReturnsToABoundedInstructionMenu()
    {
        var initial = AddBase(WithPool(InstructionPosition("any", 0), new(int.MaxValue, 0)), "STELE", P4ActivatedAbilityCatalog.AncientSteleCardNo);
        var driver = await OpenInstruction(initial);
        var resource = Assert.Single(driver.State.PendingRuleChoice!.Request.Options, o => o.Id.StartsWith("RESOURCE:STELE:", StringComparison.Ordinal));
        await Decide(driver, resource.Id); await SelectQuantity(driver, 65, "RESOURCE_CONVERSION_AMOUNT");
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", driver.State.PendingRuleChoice!.Request.Kind);
        Assert.Contains(driver.State.PendingRuleChoice.Request.Options, o => o.Id == "SPEND_POWER:CHOOSE_AMOUNT");
        await Decide(driver, "SPEND_POWER:CHOOSE_AMOUNT"); await SelectQuantity(driver, 0, "INSTRUCTION_POWER_PAYMENT");
        Assert.Equal(65, driver.State.RunePools["P1"].TotalPower); Assert.Equal(int.MaxValue - 66, driver.State.RunePools["P1"].Mana);
        Assert.True(driver.State.CardObjects["STELE"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task EchoRecomputesTheSecondLargeRangeAfterTheFirstActualPayment()
    {
        var initial = WithPool(InstructionPosition("any", 65), new(2, 65));
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant([], "P1") };
        var driver = await OpenInstruction(initial, echo: true);
        await Decide(driver, "SPEND_POWER:CHOOSE_AMOUNT"); await SelectQuantity(driver, 1, "INSTRUCTION_POWER_PAYMENT");
        Assert.Equal(64, driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"].TotalPower);
        await Decide(driver, "SPEND_POWER:CHOOSE_AMOUNT"); await SelectQuantity(driver, 64, "INSTRUCTION_POWER_PAYMENT");
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        var paid = driver.Events().Where(e => e.Event.Kind == "COST_PAID"
            && e.Event.Payload.GetValueOrDefault("paymentWindow") as string == "INSTRUCTION_POWER_PAYMENT").ToArray();
        Assert.Equal(new[] { 1, 64 }, paid.Select(e => (int)e.Event.Payload["power"]!).ToArray());
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState InstructionPosition(string trait, int power)
    {
        var state = Position("standard");
        return state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = trait == "any" ? new(1, power)
                : new(1, 0, new Dictionary<string, int> { [RuneTrait.Red] = power }) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", "OGN·268/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
                Base = ["D", "D2"], Battlefields = state.PlayerZones["P2"].Battlefields.Where(id => id != "D" && id != "D2").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["D"] = new("P2", "BASE"), ["D2"] = new("P2", "BASE") } };
    }
    private static async Task<StandbyDefendObserverTests.Driver> OpenInstruction(MatchState initial, bool echo = false)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", Native(new PlayCardCommand("SPELL", "OGN·268/298", ["FIELD1"], OptionalCosts: echo ? ["ECHO"] : [],
            RepeatChoices: echo ? [new("", ["FIELD1"])] : null)));
        await driver.Top(); Assert.Equal("INSTRUCTION_POWER_PAYMENT", driver.State.PendingRuleChoice!.Request.Kind); return driver;
    }
}
