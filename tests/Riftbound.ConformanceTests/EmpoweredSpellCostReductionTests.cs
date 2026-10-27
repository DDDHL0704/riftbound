using System.Collections;
using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EmpoweredSpellCostReductionTests
{
    // Exact CN VEN·055/id37746. The consumer owns the card promotion and
    // actual empowered/copy/control/opening lifecycle; these test shared costs.
    private const string Text = "{{强化3}}（支付{{3}}：强化我。仅在未强化时可用。）\n{{已强化>}} 你的法术费用减少{{1}}和{{A}}，不得低于{{1}}。";

    [Fact]
    public void ThePrintedDependencyKeepsManaPowerAndItsOwnManaFloorSeparate()
    {
        var spec = Assert.Single(EmpowerParser.SpellCostReductions(Text));
        Assert.Equal(new EmpoweredSpellCostReductionSpec(1, 1, 1), spec);
        var empower = Assert.Single(Assert.Single(EmpowerParser.Parse(Text)).Alternatives);
        Assert.Equal(3, empower.Mana); Assert.Equal(0, empower.Power);
        Assert.True(empower.SupportedResources); Assert.Empty(empower.TypedPower);
        Assert.Equal(2, EmpowerParser.SpellCostReductions(Text + "\n" + Text).Count);
    }

    [Theory]
    [InlineData("你的法术费用减少{{1}}和{{A}}，不得低于{{1}}。")]
    [InlineData("{{已强化>}} 你的单位费用减少{{1}}和{{A}}，不得低于{{1}}。")]
    [InlineData("{{已强化>}} 你的法术费用减少{{1}}和{{蓝色}}，不得低于{{1}}。")]
    [InlineData("{{已强化>}} 你的法术费用减少{{1}}和{{A}}。")]
    public void OtherTextDoesNotInventThisConditionalCostProfile(string text)
        => Assert.Empty(EmpowerParser.SpellCostReductions(text));

    [Fact]
    public void OneGenericReductionCanRemoveEitherWardPowerOrThePrintedColoredPower()
    {
        var original = new Dictionary<string, int> { [RuneTrait.Blue] = 1 };
        var before = JsonSerializer.Serialize(original);
        var options = Reduce(1, original, 1);
        Assert.Equal(2, options.Count);
        var blue = Assert.Single(options, o => o.Id == "SPELL_POWER_REDUCTION:blue:1");
        Assert.Equal(1, blue.Generic); Assert.Empty(blue.Typed); Assert.Equal(1, blue.Reduction);
        var generic = Assert.Single(options, o => o.Id == "SPELL_POWER_REDUCTION:any:1");
        Assert.Equal(0, generic.Generic); Assert.Equal(1, generic.Typed[RuneTrait.Blue]);
        var redOnly = new RunePool(1, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 });
        Assert.Equal(0, PaymentCostRules.PowerDeficit(redOnly, blue.Generic, blue.Typed));
        Assert.Equal(1, PaymentCostRules.PowerDeficit(redOnly, generic.Generic, generic.Typed));
        Assert.Equal(before, JsonSerializer.Serialize(original));
    }

    [Fact]
    public void MultipleReductionsKeepAllColoredAndGenericDistributions()
    {
        var options = Reduce(1, new Dictionary<string, int> { [RuneTrait.Red] = 1, [RuneTrait.Blue] = 1 }, 2);
        Assert.Equal(3, options.Count);
        Assert.Equal(new[] { "SPELL_POWER_REDUCTION:any:1,blue:1", "SPELL_POWER_REDUCTION:any:1,red:1", "SPELL_POWER_REDUCTION:blue:1,red:1" },
            options.Select(o => o.Id).Order(StringComparer.Ordinal).ToArray());
        Assert.All(options, o => { Assert.Equal(1, o.Generic + o.Typed.Values.Sum()); Assert.Equal(2, o.Reduction); });
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(10)]
    public void AColoredCostMayReachZeroWithoutAColorInTheWallet(int reductions)
    {
        var option = Assert.Single(Reduce(0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }, reductions));
        Assert.Equal("SPELL_POWER_REDUCTION:blue:1", option.Id);
        Assert.Equal(0, option.Generic); Assert.Empty(option.Typed); Assert.Equal(1, option.Reduction);
        Assert.True(PaymentCostRules.CanPayPowerCost(RunePool.Empty, option.Generic, option.Typed));
    }

    [Fact]
    public void ExcessReductionCannotMakeAResourceNegativeOrRequireAZeroChoice()
    {
        var option = Assert.Single(Reduce(1, new Dictionary<string, int> { [RuneTrait.Blue] = 1, [RuneTrait.Red] = 1 }, 9));
        Assert.Equal(0, option.Generic); Assert.Empty(option.Typed); Assert.Equal(3, option.Reduction);
        var empty = Assert.Single(Reduce(0, new Dictionary<string, int>(), 9));
        Assert.Equal("", empty.Id); Assert.Equal(0, empty.Reduction);
    }

    [Fact]
    public void WithoutAReducerTheOriginalAllocationStaysUnchanged()
    {
        var typed = new Dictionary<string, int> { [RuneTrait.Blue] = 2 };
        var option = Assert.Single(Reduce(3, typed, 0));
        Assert.Equal("", option.Id); Assert.Equal(3, option.Generic);
        Assert.Equal(typed, option.Typed); Assert.Equal(0, option.Reduction);
    }

    [Theory]
    [InlineData("SPELL_POWER_REDUCTION:blue:1")]
    [InlineData("SPELL_POWER_REDUCTION:any:999")]
    public async Task AnInventedDiscountCannotAuthorizeARealSpellOrPreview(string choice)
    {
        var state = PlayCostPreviewTests.Position(new(20, 20), "OGS·003/024");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = PlayerZones.Empty with { Battlefields = ["ENEMY"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = new("ENEMY", cardNo: "OGN·025/298", ownerId: "P2", controllerId: "P2", power: 7, tags: [CardObjectTags.UnitCard]) } };
        var command = new PlayCardCommand("CARD", "OGS·003/024", ["ENEMY"]);
        var engine = new CoreRuleEngine(); var before = MatchStateHasher.Hash(state);
        var control = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(control.CanPay, control.Message); Assert.Null(control.Cost!.SpellPowerReductionChoices);
        var attempted = command with { OptionalCosts = [choice] };
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, attempted));
        Assert.False(quote.IsValid); Assert.False(quote.CanPay); Assert.Equal(ErrorCodes.InvalidTarget, quote.ErrorCode);
        var rejected = await engine.ResolveAsync(state, new("invented-spell-discount", "P1", CommandTypes.PlayCard), attempted, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Equal("符能减费分配不合法。", rejected.ErrorMessage); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(state)); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    private sealed record Price(string Id, int Generic, IReadOnlyDictionary<string, int> Typed, int Reduction);
    private static IReadOnlyList<Price> Reduce(int generic, IReadOnlyDictionary<string, int> typed, int reduction)
    {
        var values = Assert.IsAssignableFrom<IEnumerable>(typeof(CoreRuleEngine)
            .GetMethod("ReduceSpellPower", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [generic, typed, reduction]));
        return values.Cast<object>().Select(value => new Price(
            (string)value.GetType().GetProperty("ChoiceId")!.GetValue(value)!,
            (int)value.GetType().GetProperty("GenericPower")!.GetValue(value)!,
            (IReadOnlyDictionary<string, int>)value.GetType().GetProperty("TypedPower")!.GetValue(value)!,
            (int)value.GetType().GetProperty("Reduction")!.GetValue(value)!)).ToArray();
    }
}
