using System.Reflection;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattleWinnerPhaseScenarioTests
{
    [Theory]
    [InlineData("plain")] [InlineData("guardians")] [InlineData("final-conquest")]
    [InlineData("result-burnout")] [InlineData("defender")] [InlineData("skipped-damage")] [InlineData("control-observer")]
    public void FiniteWinnerQaHasNoPreplayedCombatantOrEarnedBattleResult(string mode)
    {
        var state = Build(mode); Assert.Equal(424242, state.Seed); Assert.Equal(5, state.TurnNumber);
        Assert.Equal(RuleInputIdentity.Current, state.RuleInputs); Assert.Null(state.PendingBattleResult); Assert.False(state.BattleState.IsActive);
        foreach (var id in new[] { "HOST", "ATTACKER", "READY", "ANGEL", "ANGEL-2" }) {
            var owner = id is "ATTACKER" or "READY" ? "P2" : "P1"; var card = state.CardObjects[id];
            Assert.Contains(id, state.PlayerZones[owner].Hand); Assert.Equal(new ObjectLocationState(owner, "HAND"), state.ObjectLocations[id]);
            Assert.Equal(0, card.ObjectGeneration); Assert.Equal(0, card.Damage); Assert.Null(card.AttachedToObjectId);
            Assert.False(card.IsAttacking); Assert.False(card.IsDefending); Assert.False(card.IsEmpowered);
        }
        Assert.Equal(mode == "final-conquest" ? "P2" : null, state.CardObjects["OTHER"].ControllerId);
        if (mode == "final-conquest") {
            Assert.Contains("OTHER-KEEPER", state.PlayerZones["P2"].Battlefields);
            Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "OTHER"), state.ObjectLocations["OTHER-KEEPER"]);
            Assert.Equal(6, state.PlayerScores["P2"]); Assert.Empty(state.UntilEndOfTurnEffects);
        }
        Assert.Equal(mode == "result-burnout" ? 1 : 7, state.PlayerZones["P2"].MainDeck.Count);
        Assert.Equal(mode == "result-burnout" ? 7 : 0, state.PlayerScores["P1"]);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Fact]
    public void FiniteWinnerQaRejectsUnlistedStateImports()
    {
        var error = Assert.Throws<TargetInvocationException>(() => Build("arbitrary-import"));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    internal static MatchState Build(string mode)
    {
        var type = typeof(MatchSession); var seedType = type.GetNestedType("DevScenarioSeed", BindingFlags.NonPublic)!;
        var seed = Activator.CreateInstance(seedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, ["P1", "P2"], null)!;
        var current = new MatchState("C10-WINNER-" + mode, 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, seed: 424242);
        return (MatchState)type.GetMethod("BuildBattleWinnerPhaseScenario", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [current, seed, "battle-winner-phase-" + mode])!;
    }
}
