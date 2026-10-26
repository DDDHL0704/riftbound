using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RequiredEmpowerCostTests
{
    [Fact]
    public void ARecognizedHandDiscardRemainsANonresourceCostAndUnknownCostsStayClosed()
    {
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse("{{强化}} — 弃置一张手牌")).Alternatives);
        Assert.False(cost.SupportedResources); Assert.Equal("弃置一张手牌", cost.AdditionalCost);
        Assert.True(Supported(cost)); Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        foreach (var text in new[] { "{{强化}}", "{{强化}} — 弃置两张手牌", "{{强化}} — 弃置一张法术牌", "{{强化}} — 摧毁一名友方单位" })
            Assert.False(Supported(Assert.Single(Assert.Single(EmpowerParser.Parse(text)).Alternatives)));
        Assert.True(Supported(Assert.Single(Assert.Single(EmpowerParser.Parse("{{强化2红色}}")).Alternatives)));
    }

    [Fact]
    public void OnlyEligibleOwnPhysicalHandCardsAreCostChoices()
    {
        var state = Position(); var before = MatchStateHasher.Hash(state);
        var method = Method("EmpowerHandDiscardCostChoices");
        var choices = Assert.IsAssignableFrom<IReadOnlyList<string>>(method.Invoke(null, [state, "P1", "SOURCE"]));
        Assert.Equal(new[] { "UNIT", "SPELL", "EQUIPMENT" }, choices);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(method.Invoke(null, [state, "P2", "SOURCE"])));
    }

    [Theory]
    [InlineData("UNIT", 3)] [InlineData("SPELL", 0)] [InlineData("EQUIPMENT", 0)]
    public void ExactlyOneChosenCardCapturesItsOriginalGenerationWithoutMutatingAnything(string id, long generation)
    {
        var state = Position(); var before = MatchStateHasher.Hash(state);
        var (accepted, paid) = Select(state, ["DISCARD_HAND_CARD:" + id]);
        Assert.True(accepted); Assert.Equal(new ObjectBinding(id, generation), paid);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("two")]
    [InlineData("foreign")] [InlineData("base")] [InlineData("graveyard")]
    [InlineData("rune")] [InlineData("unknown")] [InlineData("missing-object")]
    [InlineData("source")] [InlineData("face-down")] [InlineData("token")]
    [InlineData("unknown-type")] [InlineData("empty")] [InlineData("padded-id")]
    [InlineData("unrelated")]
    public void MissingDuplicateForeignWrongZoneAndNoncardSelectionsCannotPay(string branch)
    {
        var state = Position(); var before = MatchStateHasher.Hash(state);
        string[] choices = branch switch {
            "missing" => [], "duplicate" => ["DISCARD_HAND_CARD:UNIT", "DISCARD_HAND_CARD:UNIT"],
            "two" => ["DISCARD_HAND_CARD:UNIT", "DISCARD_HAND_CARD:SPELL"],
            "foreign" => ["DISCARD_HAND_CARD:FOREIGN"], "base" => ["DISCARD_HAND_CARD:BASE"],
            "graveyard" => ["DISCARD_HAND_CARD:GRAVE"], "rune" => ["DISCARD_HAND_CARD:RUNE"],
            "unknown" => ["DISCARD_HAND_CARD:UNKNOWN"], "missing-object" => ["DISCARD_HAND_CARD:MISSING"],
            "source" => ["DISCARD_HAND_CARD:SOURCE"], "face-down" => ["DISCARD_HAND_CARD:FACE-DOWN"],
            "token" => ["DISCARD_HAND_CARD:TOKEN"], "unknown-type" => ["DISCARD_HAND_CARD:UNTYPED"],
            "empty" => ["DISCARD_HAND_CARD:"], "padded-id" => ["DISCARD_HAND_CARD: UNIT"],
            _ => ["RECYCLE_RUNE:UNIT"] };
        var (accepted, paid) = Select(state, choices);
        Assert.False(accepted); Assert.Null(paid); Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Fact]
    public async Task ResourceOnlyEmpowerDoesNotAcceptAnInventedHandPaymentWitness()
    {
        var entered = await EmpowerTests.Enter("VEN·001");
        var activated = await EmpowerTests.Activate(entered); Assert.True(activated.Accepted, activated.ErrorMessage);
        var item = Assert.Single(activated.State.StackItems); Assert.NotNull(item.EmpowerActivation);
        Assert.Null(item.EmpowerActivation.DiscardedHandCard);
        Assert.DoesNotContain("discardedHandCard", JsonSerializer.Serialize(item.EmpowerActivation, new JsonSerializerOptions(JsonSerializerDefaults.Web)), StringComparison.Ordinal);
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(activated.State));
        var forged = activated.State with { StackItems = [item with {
            EmpowerActivation = item.EmpowerActivation with { DiscardedHandCard = new("FORGED", 0) } }] };
        Assert.Contains("invalid captured empower activation", MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
    }

    private static bool Supported(EmpowerCostSpec cost)
        => Assert.IsType<bool>(typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.EmpowerRules")!
            .GetMethod("SupportedCost", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [cost]));
    private static MethodInfo Method(string name) => typeof(CoreRuleEngine).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
    private static (bool Accepted, ObjectBinding? Paid) Select(MatchState state, IReadOnlyList<string> choices)
    {
        object?[] args = [state, "P1", "SOURCE", choices, null];
        var accepted = Assert.IsType<bool>(Method("TrySelectEmpowerHandDiscardCost").Invoke(null, args));
        return (accepted, (ObjectBinding?)args[4]);
    }
    private static MatchState Position()
    {
        CardObjectState Card(string id, string tag, string actor = "P1") => new(id, tags: [tag], ownerId: actor, controllerId: actor);
        var cards = new Dictionary<string, CardObjectState> {
            ["SOURCE"] = Card("SOURCE", CardObjectTags.UnitCard),
            ["UNIT"] = Card("UNIT", CardObjectTags.UnitCard) with { ObjectGeneration = 3 },
            ["SPELL"] = Card("SPELL", CardObjectTags.SpellCard), ["EQUIPMENT"] = Card("EQUIPMENT", CardObjectTags.EquipmentCard),
            ["FOREIGN"] = Card("FOREIGN", CardObjectTags.UnitCard, "P2"), ["BASE"] = Card("BASE", CardObjectTags.UnitCard),
            ["GRAVE"] = Card("GRAVE", CardObjectTags.SpellCard), ["RUNE"] = Card("RUNE", CardObjectTags.RuneCard),
            ["FACE-DOWN"] = Card("FACE-DOWN", CardObjectTags.SpellCard) with { IsFaceDown = true },
            ["TOKEN"] = Card("TOKEN", CardObjectTags.UnitCard) with { TokenFactoryCardNo = "OGN·016/298" },
            ["UNTYPED"] = new("UNTYPED", ownerId: "P1", controllerId: "P1") };
        return new("required-empower-cost", 0, 1, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            cardObjects: cards, playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = ["SOURCE", "BASE"], Graveyard = ["GRAVE"],
                    Hand = ["UNIT", "SPELL", "EQUIPMENT", "FOREIGN", "RUNE", "MISSING", "FACE-DOWN", "TOKEN", "UNTYPED"] },
                ["P2"] = PlayerZones.Empty });
    }
}
