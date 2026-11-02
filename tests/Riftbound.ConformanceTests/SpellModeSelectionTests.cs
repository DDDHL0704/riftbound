using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SpellModeSelectionTests
{
    // Declaration prototypes only. RAD 152 is deliberately not admitted before
    // its resolution, replacement, private choice and recovery paths are wired.
    private const string Card = "RAD·152/167";
    private static readonly SpellModeSetSpec Modes = new(Card, 2, [
        new(Card, "炫音返场", 3, "ENCORE_DAMAGE", 2, 1,
            Mode: "DAMAGE", TargetScope: CardTargetScopes.AnyUnit),
        new(Card, "炫音返场", 3, "ENCORE_MINIONS", 0, 0,
            Mode: "MINIONS", CreatedBaseUnitTokenCount: 2, CreatedBaseUnitTokenPower: 1,
            CreatedBaseUnitTokenName: "随从", CreatedBaseUnitTokenTags: CardObjectTags.UnitCard),
        new(Card, "炫音返场", 3, "ENCORE_MIGHT", 0, 2,
            Mode: "MIGHT", MinTargetCount: 0, TargetScope: CardTargetScopes.AnyUnit, PowerModifierAmount: 1),
        new(Card, "炫音返场", 3, "ENCORE_LOOK", 0, 1,
            Mode: "LOOK", TargetScope: CardTargetScopes.FriendlyMainDeckCard, MainDeckLookCount: 2,
            DrawsSelectedMainDeckTarget: true, RecyclesUnselectedMainDeckLookCards: true)
    ]);
    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position("OGN·104/298");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { CardNo = Card } } };
    }
    private static IReadOnlyList<DeclaredSpellMode> Capture(MatchState state, params SpellModeChoice[] choices)
    {
        var before = MatchStateHasher.Hash(state);
        Assert.True(CoreRuleEngine.TryCaptureSpellModes(state, "P1", "UNIT", Modes, choices, out var plan, out var error), error);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        return plan;
    }

    [Fact]
    public void SelectedModesKeepTheirOwnTargetsAndCaptureCurrentGenerations()
    {
        var state = Position();
        var targets = new[] { "ALLY" };
        var plan = Capture(state, new("MIGHT", ["ALLY", "ENEMY"]), new("DAMAGE", targets));
        Assert.Equal(new[] { "DAMAGE", "MIGHT" }, plan.Select(p => p.Mode));
        Assert.Equal(new[] { "ALLY" }, plan[0].Execution.TargetObjectIds);
        Assert.Equal(new[] { "ALLY", "ENEMY" }, plan[1].Execution.TargetObjectIds);
        Assert.Equal(state.CardObjects["ALLY"].ObjectGeneration, plan[0].Execution.TargetGenerations["ALLY"]);
        targets[0] = "GEAR";
        Assert.Equal("ALLY", plan[0].Execution.TargetObjectIds[0]);
        var restored = JsonSerializer.Deserialize<DeclaredSpellMode[]>(JsonSerializer.Serialize(plan))!;
        Assert.Equal("ALLY", restored[0].Execution.TargetObjectIds[0]);
        Assert.Equal(plan[0].Execution.TargetGenerations["ALLY"], restored[0].Execution.TargetGenerations["ALLY"]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void UpToTwoMightTargetsDoNotImposeTargetsOnTheTokenMode(int count)
    {
        var plan = Capture(Position(), new("MINIONS", []), new("MIGHT", new[] { "ALLY", "ENEMY" }.Take(count).ToArray()));
        Assert.Empty(plan[0].Execution.TargetObjectIds);
        Assert.Equal(count, plan[1].Execution.TargetObjectIds.Count);
    }

    [Fact]
    public void PrivateDeckSelectionIsDeferredUntilResolution()
    {
        var plan = Capture(Position(), new("LOOK", []), new("MINIONS", []));
        Assert.Equal(new[] { "MINIONS", "LOOK" }, plan.Select(p => p.Mode));
        Assert.All(plan, mode => Assert.Empty(mode.Execution.TargetObjectIds));
    }

    [Theory]
    [InlineData("count")] [InlineData("duplicate-mode")] [InlineData("unknown-mode")]
    [InlineData("missing-target")] [InlineData("equipment")] [InlineData("duplicate-target")]
    [InlineData("too-many-targets")] [InlineData("private-target")] [InlineData("wrong-controller")]
    public void InvalidDeclarationsAreRejectedWithoutChangingState(string kind)
    {
        var state = Position();
        SpellModeChoice[] choices = kind switch {
            "count" => [new("MINIONS", [])],
            "duplicate-mode" => [new("MINIONS", []), new("MINIONS", [])],
            "unknown-mode" => [new("MINIONS", []), new("OTHER", [])],
            "missing-target" => [new("MINIONS", []), new("DAMAGE", [])],
            "equipment" => [new("MINIONS", []), new("DAMAGE", ["GEAR"])],
            "duplicate-target" => [new("MINIONS", []), new("MIGHT", ["ALLY", "ALLY"])],
            "too-many-targets" => [new("MINIONS", []), new("MIGHT", ["ALLY", "ENEMY", "GEAR"])],
            "private-target" => [new("MINIONS", []), new("LOOK", ["P1-DRAW-1"])],
            _ => [new("MINIONS", []), new("LOOK", [])]
        };
        var before = MatchStateHasher.Hash(state);
        Assert.False(CoreRuleEngine.TryCaptureSpellModes(state, kind == "wrong-controller" ? "P2" : "P1",
            "UNIT", Modes, choices, out var plan, out var error));
        Assert.Empty(plan); Assert.NotEmpty(error); Assert.Equal(before, MatchStateHasher.Hash(state));
    }
}
