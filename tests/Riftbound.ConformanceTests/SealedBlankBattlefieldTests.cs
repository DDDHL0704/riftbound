using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SealedBlankBattlefieldTests
{
    [Fact]
    public void BlankBattlefieldLoadsThroughSharedPhysicalFactoryAndHasNoRulesText()
    {
        Assert.True(PrintedCardFactory.TryCreate("blank", "P1", SealedDeckRules.BlankBattlefieldCardNo, out var battlefield));
        Assert.Contains("CARD_TYPE:BATTLEFIELD", battlefield.Tags);
        Assert.Equal("P1", battlefield.OwnerId);
        Assert.Null(battlefield.ControllerId);
        Assert.Equal("", SealedDeckRules.BlankBattlefield.CardEffect);
        Assert.False(TokenObjectRules.IsToken(battlefield));
        Assert.True(PrintedCardFactory.TryRestoreOutsidePlay(battlefield with { ControllerId = "P2", Power = 9 }, "P1", out var restored));
        Assert.Null(restored.ControllerId);
        Assert.Equal(0, restored.Power);
        Assert.Equal(battlefield.CardNo, restored.CardNo);
    }

    [Fact]
    public async Task BlankBattlefieldUsesNormalConquestControlAndScoring()
    {
        Assert.True(PrintedCardFactory.TryCreate("BF", "P1", SealedDeckRules.BlankBattlefieldCardNo, out var battlefield));
        var state = new MatchState(roomId: "sealed-blank-conquest", tick: 0, turnNumber: 3, activePlayerId: "P2",
            seats: new Dictionary<string, string> { ["P1"] = "c1", ["P2"] = "c2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.SpellDuelOpen, focusPlayerId: "P2",
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Battlefields = ["BF", "UNIT"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["BF"] = battlefield,
                ["UNIT"] = new("UNIT", cardNo: "OGN·001/298", power: 5, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1") },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["BF"] = new("P1", "BATTLEFIELD", "BF"), ["UNIT"] = new("P1", "BATTLEFIELD", "BF") });
        var engine = new CoreRuleEngine();
        var first = await engine.ResolveAsync(state, new("focus-2", "P2", CommandTypes.PassFocus), new PassFocusCommand(), default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var closed = await engine.ResolveAsync(first.State, new("focus-1", "P1", CommandTypes.PassFocus), new PassFocusCommand(), default);
        Assert.True(closed.Accepted, closed.ErrorMessage);
        Assert.Equal("P1", closed.State.CardObjects["BF"].ControllerId);
        Assert.Equal(1, closed.State.PlayerScores["P1"]);
        Assert.Empty(closed.State.PendingTaskQueue.Tasks);
    }
}
