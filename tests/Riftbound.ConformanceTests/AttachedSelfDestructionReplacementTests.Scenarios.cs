using System.Reflection;
using System.Text.Json;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    [Theory]
    [InlineData("single", "OGN·210/298")]
    [InlineData("double", "OGN·210/298")]
    [InlineData("lethal-last-breath", "OGN·096/298")]
    [InlineData("control-group", "SFD·173/221")]
    public void FiniteGuardianQaKeepsAllActionsUnplayedAndCurrentStateStrict(string suffix, string hostNo)
    {
        var state = GuardianScenario("guardian-angel-" + suffix);
        Assert.Equal(RuleInputIdentity.Current, state.RuleInputs); Assert.Equal(424242, state.Seed); Assert.Equal(5, state.TurnNumber);
        Assert.Equal("P1", state.CardObjects["HOME"].ControllerId); Assert.Equal(hostNo, state.CardObjects["HOST"].CardNo);
        foreach (var id in new[] { "HOST", "ANGEL", "ANGEL-2", "SONG", "READY", "KILL", "TAKE", "RAY" }) {
            var card = state.CardObjects[id]; var owner = id is "KILL" or "TAKE" or "RAY" ? "P2" : "P1";
            Assert.Contains(id, state.PlayerZones[owner].Hand); Assert.Equal(new ObjectLocationState(owner, "HAND"), state.ObjectLocations[id]);
            Assert.Equal(0, card.ObjectGeneration); Assert.False(card.IsEmpowered); Assert.Null(card.AttachedToObjectId);
            Assert.Null(card.AttachedRuleText); Assert.Null(card.AttachmentUnitCopies); Assert.Equal(0, card.AttachmentRevision);
        }
        Assert.Equal(22, state.PlayerZones["P2"].Base.Count);
        Assert.All(state.PlayerZones["P2"].Base, id => Assert.Contains(CardObjectTags.RuneCard, state.CardObjects[id].Tags));
        Assert.Equal(70, state.RunePools["P1"].Mana); Assert.Equal(6, state.RunePools["P1"].PowerByTrait.Count);
        Assert.Equal(RunePool.Empty, state.RunePools["P2"]);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var view = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P2"], Json);
        Assert.DoesNotContain("PRIVATE-P1", view); Assert.DoesNotContain("PRIVATE-P2", view); Assert.DoesNotContain("ANGEL", view);
    }

    [Fact]
    public void FiniteGuardianQaRejectsEveryUnlistedScenario()
    {
        var error = Assert.Throws<TargetInvocationException>(() => GuardianScenario("guardian-angel-arbitrary-import"));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static MatchState GuardianScenario(string scenario)
    {
        var type = typeof(MatchSession); var seedType = type.GetNestedType("DevScenarioSeed", BindingFlags.NonPublic)!;
        var seed = Activator.CreateInstance(seedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: ["P1", "P2"], culture: null)!;
        var method = type.GetMethod("BuildAttachedSelfDestructionScenario", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsType<MatchState>(method.Invoke(null, [Position(), seed, scenario]));
    }
}
