using System.Reflection;
using System.Text.Json;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedDestructionReplacementInstanceTests
{
    [Theory]
    [InlineData("173", "SFD·173/221")]
    [InlineData("239", "SFD·239/221")]
    [InlineData("239-star", "SFD·239*/221")]
    public void FiniteCopiedDestructionQaKeepsUnplayedPrintedCardsAndStrictCurrentState(string suffix, string no)
    {
        var state = FiniteScenario("copied-local-destruction-soraka-" + suffix);
        Assert.Equal(RuleInputIdentity.Current, state.RuleInputs); Assert.Equal(424242, state.Seed); Assert.Equal(5, state.TurnNumber);
        Assert.Equal("P1", state.CardObjects["HOME"].ControllerId);
        Assert.Equal(no, state.CardObjects["S"].CardNo); Assert.Equal(4, state.CardObjects["S"].Power);
        foreach (var id in new[] { "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }) {
            Assert.Equal("OGN·210/298", state.CardObjects[id].CardNo); Assert.Equal(2, state.CardObjects[id].Power);
        }
        foreach (var id in new[] { "S", "FIELD-1", "FIELD-2", "BASE-1", "BASE-2", "G1", "SONG", "SONG-2", "GLASSES" }) {
            var card = state.CardObjects[id];
            Assert.Contains(id, state.PlayerZones["P1"].Hand); Assert.Equal(0, card.ObjectGeneration);
            Assert.False(card.IsEmpowered); Assert.Null(card.AttachedRuleText); Assert.Null(card.AttachmentUnitCopies);
            Assert.Null(card.AttachedToObjectId); Assert.Equal(0, card.AttachmentRevision);
            Assert.Empty(card.UntilEndOfTurnPowerModifiers);
        }
        Assert.Equal(21, state.PlayerZones["P2"].Base.Count);
        Assert.All(state.PlayerZones["P2"].Base, id => Assert.Contains(CardObjectTags.RuneCard, state.CardObjects[id].Tags));
        foreach (var player in new[] { "P1", "P2" }) {
            Assert.Equal(70, state.RunePools[player].Mana); Assert.Equal(6, state.RunePools[player].PowerByTrait.Count);
            Assert.All(state.RunePools[player].PowerByTrait.Values, power => Assert.Equal(10, power));
            Assert.Equal(2, state.PlayerZones[player].RuneDeck.Count);
        }
        ArmamentMightTests.PublicRecovery(state);
        var peer = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P2"], RecoveryJson);
        Assert.DoesNotContain("PRIVATE-P1", peer); Assert.DoesNotContain("PRIVATE-P2", peer); Assert.DoesNotContain("FIELD-1", peer);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C8_COPIED_DESTRUCTION_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "finite-" + suffix + ".json"), JsonSerializer.Serialize(state, RecoveryJson));
        }
    }

    [Fact]
    public void FiniteCopiedDestructionQaCannotImportAnArbitraryPosition()
    {
        var error = Assert.Throws<TargetInvocationException>(() => FiniteScenario("arbitrary-state-import"));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static MatchState FiniteScenario(string scenario)
    {
        var type = typeof(MatchSession); var seedType = type.GetNestedType("DevScenarioSeed", BindingFlags.NonPublic)!;
        var seed = Activator.CreateInstance(seedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: ["P1", "P2"], culture: null)!;
        var method = type.GetMethod("BuildCopiedLocalDestructionScenario", BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsType<MatchState>(method.Invoke(null, [Position("SFD·173/221", true), seed, scenario]));
    }
}
