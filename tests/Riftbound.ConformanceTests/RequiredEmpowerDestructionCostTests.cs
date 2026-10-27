using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RequiredEmpowerDestructionCostTests
{
    private static EmpowerCostSpec Cost(string text = "{{强化}} — 摧毁一名友方单位")
        => Assert.Single(Assert.Single(EmpowerParser.Parse(text)).Alternatives);
    private static MethodInfo Method(string name) => typeof(CoreRuleEngine).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void TheRequiredChoiceIncludesSelfTokensAndControlledForeignOwnedUnitsButNoTargetsOrHiddenObjects()
    {
        var state = Position(); var before = MatchStateHasher.Hash(state);
        var choices = Assert.IsAssignableFrom<IReadOnlyList<string>>(Method("EmpowerFriendlyUnitDestructionCostChoices").Invoke(null, [state, "P1", Cost()]));
        Assert.Equal(new[] { "ALLY", "FOREIGN-OWNED", "TOKEN", "UNIT" }, choices);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("UNIT", 4)] [InlineData("ALLY", 2)] [InlineData("TOKEN", 0)] [InlineData("FOREIGN-OWNED", 1)]
    public void ExactlyOnePhysicalUnitSelectionCapturesTheOriginalGenerationWithoutChangingState(string id, long generation)
    {
        var state = Position(); var before = MatchStateHasher.Hash(state);
        var (accepted, binding) = Select(state, ["DESTROY_FRIENDLY_UNIT:" + id]);
        Assert.True(accepted); Assert.Equal(new ObjectBinding(id, generation), binding);
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("two")] [InlineData("enemy")]
    [InlineData("hand")] [InlineData("graveyard")] [InlineData("gear")] [InlineData("rune")]
    [InlineData("hidden")] [InlineData("unknown")] [InlineData("empty")] [InlineData("padded")]
    [InlineData("unrelated")]
    public void InvalidSelectionsCannotBecomeAFreeOrPartiallyPaidActivation(string kind)
    {
        var state = Position(); var before = MatchStateHasher.Hash(state);
        var id = kind switch { "enemy" => "ENEMY", "hand" => "HAND", "graveyard" => "GRAVE", "gear" => "GEAR",
            "rune" => "RUNE", "hidden" => "HIDDEN", "unknown" => "MISSING", _ => "ALLY" };
        string[] choices = kind switch {
            "missing" => [], "duplicate" => ["DESTROY_FRIENDLY_UNIT:ALLY", "DESTROY_FRIENDLY_UNIT:ALLY"],
            "two" => ["DESTROY_FRIENDLY_UNIT:ALLY", "DESTROY_FRIENDLY_UNIT:UNIT"],
            "empty" => ["DESTROY_FRIENDLY_UNIT:"], "padded" => ["DESTROY_FRIENDLY_UNIT: ALLY"],
            "unrelated" => ["DISCARD_HAND_CARD:ALLY"], _ => ["DESTROY_FRIENDLY_UNIT:" + id] };
        var (accepted, binding) = Select(state, choices);
        Assert.False(accepted); Assert.Null(binding); Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("{{强化}}")]
    [InlineData("{{强化}} — 摧毁两名友方单位")]
    [InlineData("{{强化}} — 摧毁一名友方单位，然后抽一张牌")]
    [InlineData("{{强化1}} — 摧毁一名友方单位")]
    public void MissingMultipleAndCompoundCostsDoNotBorrowTheSingleDestructionSelector(string text)
    {
        var (accepted, binding) = Select(Position(), ["DESTROY_FRIENDLY_UNIT:ALLY"], Cost(text));
        Assert.False(accepted); Assert.Null(binding);
    }

    [Fact]
    public async Task ARealSourceMayPayItselfAndTheSurvivingAbilityDoesNotEmpowerANewGeneration()
    {
        var game = new Driver(VenUnitEntryBasicTests.Position("VEN·124"));
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:UNIT"]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(new ObjectBinding("UNIT", generation), item.EmpowerActivation!.DestructionCostUnit);
        Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Contains("invalid captured empower activation", MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
            StackItems = [item with { EmpowerActivation = item.EmpowerActivation with { DestructionCostUnit = null } }] }));
        Assert.Contains("invalid captured empower activation", MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
            StackItems = [item with { EmpowerActivation = item.EmpowerActivation with { DestructionCostUnit = new("UNIT", generation + 2) } }] }));
        await game.Top(); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "OBJECT_EMPOWERED"); await game.Replay();
    }

    private static (bool Accepted, ObjectBinding? Binding) Select(MatchState state, IReadOnlyList<string> choices, EmpowerCostSpec? cost = null)
    {
        object?[] args = [state, "P1", cost ?? Cost(), choices, null];
        var accepted = Assert.IsType<bool>(Method("TrySelectEmpowerFriendlyUnitDestructionCost").Invoke(null, args));
        return (accepted, args[4] as ObjectBinding);
    }

    private static MatchState Position()
    {
        var cards = new Dictionary<string, CardObjectState>();
        foreach (var (id, generation) in new[] { ("UNIT", 4L), ("ALLY", 2L), ("FOREIGN-OWNED", 1L), ("TOKEN", 0L),
                     ("ENEMY", 0L), ("HAND", 0L), ("GRAVE", 0L), ("HIDDEN", 0L) })
            cards[id] = new(id, cardNo: id == "UNIT" ? "VEN·124" : "OGN·001/298", ownerId: id is "FOREIGN-OWNED" or "ENEMY" ? "P2" : "P1",
                controllerId: id == "ENEMY" ? "P2" : "P1", tags: [CardObjectTags.UnitCard]) { ObjectGeneration = generation, IsFaceDown = id == "HIDDEN" };
        cards["TOKEN"] = cards["TOKEN"] with { TokenFactoryCardNo = "OGN·016/298" };
        cards["GEAR"] = new("GEAR", cardNo: "SFD·022/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]);
        cards["RUNE"] = new("RUNE", cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard]);
        return new("REQUIRED-UNIT-COST", 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = ["UNIT", "ALLY", "FOREIGN-OWNED", "TOKEN", "GEAR", "RUNE", "HIDDEN"], Hand = ["HAND"], Graveyard = ["GRAVE"] },
                ["P2"] = PlayerZones.Empty with { Base = ["ENEMY"] } }, cardObjects: cards);
    }
}
