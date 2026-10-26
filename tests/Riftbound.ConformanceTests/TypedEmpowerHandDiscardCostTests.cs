using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TypedEmpowerHandDiscardCostTests
{
    // Exact CN VEN·110/id37807, fetched 2026-10-26. This is the printed
    // payment contract only; the empowered banish observer is separate.
    private const string Text = "{{强化}} — 弃置一张法术牌（支付此费用：强化我。仅在未强化时可用。）\n当我变为{{已强化}}时，放逐战场上一名不高于3{{S}}的敌方单位。";

    [Fact]
    public void TheExactPrintedSpellDiscardIsASupportedNonresourceEmpowerCost()
    {
        var cost = Cost();
        Assert.False(cost.SupportedResources);
        Assert.Equal("弃置一张法术牌", cost.AdditionalCost);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.False(cost.Exhaust);
        Assert.True(Assert.IsType<bool>(typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.EmpowerRules")!
            .GetMethod("SupportedCost", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [cost])));
    }

    [Fact]
    public void OnlyEligibleOwnPhysicalHandSpellsAreOffered()
    {
        var state = RequiredEmpowerCostTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FOREIGN"] = state.CardObjects["FOREIGN"] with { Tags = [CardObjectTags.SpellCard] },
            ["TOKEN"] = state.CardObjects["TOKEN"] with { Tags = [CardObjectTags.SpellCard] } } };
        var before = MatchStateHasher.Hash(state);
        Assert.Equal(new[] { "SPELL" }, Choices(state, Cost()));
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("SPELL", true)]
    [InlineData("UNIT", false)]
    [InlineData("EQUIPMENT", false)]
    public void PaymentMustUseOneSpellRatherThanAnOtherwiseEligibleHandCard(string id, bool expected)
    {
        var state = RequiredEmpowerCostTests.Position(); var before = MatchStateHasher.Hash(state);
        var (accepted, binding) = RequiredEmpowerCostTests.Select(state, ["DISCARD_HAND_CARD:" + id], Cost());
        Assert.Equal(expected, accepted);
        if (expected) Assert.Equal(new ObjectBinding("SPELL", 0), binding);
        else Assert.Null(binding);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Fact]
    public void NonspellCardsCannotKeepTheTypedCostAvailableWhenTheLastSpellIsMissing()
    {
        var state = RequiredEmpowerCostTests.Position();
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "SPELL").ToArray() } } };
        var before = MatchStateHasher.Hash(state);
        Assert.Empty(Choices(state, Cost()));
        var (accepted, binding) = RequiredEmpowerCostTests.Select(state, ["DISCARD_HAND_CARD:UNIT"], Cost());
        Assert.False(accepted); Assert.Null(binding);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Fact]
    public void MissingDuplicateMultipleAndForeignPaymentsStayInvalidForTheTypedCost()
    {
        var state = RequiredEmpowerCostTests.Position(); var before = MatchStateHasher.Hash(state);
        string[][] selections = [[], ["DISCARD_HAND_CARD:SPELL", "DISCARD_HAND_CARD:SPELL"],
            ["DISCARD_HAND_CARD:SPELL", "DISCARD_HAND_CARD:UNIT"], ["DISCARD_HAND_CARD:FOREIGN"],
            ["DISCARD_HAND_CARD:GRAVE"], ["DISCARD_HAND_CARD:FACE-DOWN"], ["DISCARD_HAND_CARD:RUNE"],
            ["DISCARD_HAND_CARD: SPELL"], ["RECYCLE_RUNE:SPELL"]];
        foreach (var selection in selections)
        {
            var (accepted, binding) = RequiredEmpowerCostTests.Select(state, selection, Cost());
            Assert.False(accepted); Assert.Null(binding);
            Assert.Equal(before, MatchStateHasher.Hash(state));
        }
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("generic-power")]
    [InlineData("typed-power")]
    [InlineData("exhaust")]
    public void ARecognizedDiscardPhraseDoesNotAuthorizeAnUnimplementedCombinedCost(string branch)
    {
        var cost = Cost();
        cost = branch switch {
            "mana" => cost with { Mana = 1 }, "generic-power" => cost with { Power = 1 },
            "typed-power" => cost with { TypedPower = new Dictionary<string, int> { [RuneTrait.Blue] = 1 } },
            _ => cost with { Exhaust = true } };
        var state = RequiredEmpowerCostTests.Position(); var before = MatchStateHasher.Hash(state);
        Assert.Empty(Choices(state, cost));
        var (accepted, binding) = RequiredEmpowerCostTests.Select(state, ["DISCARD_HAND_CARD:SPELL"], cost);
        Assert.False(accepted); Assert.Null(binding);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("弃置两张法术牌")]
    [InlineData("弃置一张单位牌")]
    [InlineData("弃置一张装备牌")]
    public void OtherPrintedCostShapesAreNotSilentlyTreatedAsOneHandSpell(string phrase)
    {
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse("{{强化}} — " + phrase)).Alternatives);
        Assert.False(Assert.IsType<bool>(typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.EmpowerRules")!
            .GetMethod("SupportedCost", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [cost])));
        Assert.Empty(Choices(RequiredEmpowerCostTests.Position(), cost));
    }

    private static IReadOnlyList<string> Choices(MatchState state, EmpowerCostSpec cost)
        => Assert.IsAssignableFrom<IReadOnlyList<string>>(typeof(CoreRuleEngine)
            .GetMethod("EmpowerHandDiscardCostChoices", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [state, "P1", "SOURCE", cost]));

    private static EmpowerCostSpec Cost()
        => Assert.Single(Assert.Single(EmpowerParser.Parse(Text)).Alternatives);
}
