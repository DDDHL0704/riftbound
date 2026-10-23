using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class EmpoweredChosenActionReplacementTests
{
    [Theory]
    [InlineData("empowered-target-action-replacement", false)]
    [InlineData("empowered-target-action-overflow", true)]
    public void ApprovedFiniteSeedShapesRemainStrictAndKeepTheirExplicitQaBoundary(string scenario, bool boundary)
    {
        var state = BuildFiniteReplacementScenario(scenario);
        Assert.Equal(RuleInputIdentity.Current, state.RuleInputs); Assert.Equal(424242, state.Seed); Assert.Equal(5, state.TurnNumber);
        Assert.Equal("P1", state.CardObjects["HOME"].ControllerId); Assert.Equal("P2", state.CardObjects["OTHER"].ControllerId);
        foreach (var player in new[] { "P1", "P2" }) {
            Assert.Equal(70, state.RunePools[player].Mana);
            Assert.Equal(6, state.RunePools[player].PowerByTrait.Count);
            Assert.All(state.RunePools[player].PowerByTrait.Values, power => Assert.Equal(10, power));
            Assert.Equal(2, state.PlayerZones[player].RuneDeck.Count);
        }
        foreach (var (id, no) in new[] { ("PLANK-A", "VEN·086"), ("PLANK-B", "VEN·181") }) {
            var unit = state.CardObjects[id]; Assert.Equal(no, unit.CardNo); Assert.Equal(0, unit.ObjectGeneration);
            Assert.Equal(boundary, unit.IsEmpowered); Assert.Equal(boundary ? int.MaxValue - 1 : 6, unit.Power);
            Assert.Equal(boundary, state.PlayerZones["P1"].Battlefields.Contains(id));
            Assert.Equal(!boundary, state.PlayerZones["P1"].Hand.Contains(id));
            Assert.Empty(unit.UntilEndOfTurnPowerModifiers);
        }
        // Strict current-state and both-view recovery validation, not a claim
        // that this direct factory invocation is a logged ROOM_SEAT birth.
        ArmamentMightTests.PublicRecovery(state);
        var peer = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P2"], Json);
        Assert.DoesNotContain("PRIVATE-P1", peer); Assert.DoesNotContain("PRIVATE-P2", peer);
        if (!boundary) Assert.DoesNotContain("PLANK-A", peer);
    }

    [Theory]
    [InlineData("PLANK-A", "VEN·086")] [InlineData("PLANK-B", "VEN·181")]
    public async Task FiniteOrdinaryQaStillRequiresRealPlayAndPaidEmpower(string id, string no)
    {
        var run = new Driver(BuildFiniteReplacementScenario("empowered-target-action-replacement"), "finite-qa-real-play-" + no);
        await run.Act("P1", new PlayCardCommand(id, no, [], Destination: "BATTLEFIELD:HOME"));
        Assert.Equal(64, run.State.RunePools["P1"].Mana); Assert.Equal(6, run.State.CardObjects[id].Power);
        Assert.False(run.State.CardObjects[id].IsEmpowered);
        await run.Act("P1", new ActivateAbilityCommand(id, "EMPOWER:0:0", []));
        Assert.Equal(8, run.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]); await run.Top();
        Assert.True(run.State.CardObjects[id].IsEmpowered); await run.Cold();
    }

    [Fact]
    public void FiniteQaBuilderCannotAcceptAnArbitraryScenarioIdentifier()
    {
        var error = Assert.Throws<TargetInvocationException>(() => BuildFiniteReplacementScenario("arbitrary-state-import"));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static MatchState BuildFiniteReplacementScenario(string scenario)
    {
        var type = typeof(MatchSession);
        var seedType = type.GetNestedType("DevScenarioSeed", BindingFlags.NonPublic)!;
        var seed = Activator.CreateInstance(seedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: ["P1", "P2"], culture: null)!;
        var method = type.GetMethod("BuildEmpoweredTargetReplacementScenario", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsType<MatchState>(method.Invoke(null, [Position("VEN·086", "P1", "OGN·050/298"), seed, scenario]));
    }
}
