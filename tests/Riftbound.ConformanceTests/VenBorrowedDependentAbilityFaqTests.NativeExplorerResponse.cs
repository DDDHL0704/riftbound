using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    // Core 337.2 / 338.1.a.4: drawing cards does not qualify for the
    // immediate resolution exception granted to resource abilities.
    [Theory]
    [InlineData("SFD·199/221")]
    [InlineData("SFD·248/221")]
    public async Task NativeExplorerReactionKeepsAnIndependentResponseWindowBeforeDrawing(string donor)
    {
        var initial = ExplorerReactionPosition(donor);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("EXPLORER-TARGET-1", "OGN·004/298", ["ENEMY"]));
        await game.Top();
        await CloseExplorerDuel(game);
        await game.Act("P1", new PlayCardCommand("EXPLORER-TARGET-2", "OGN·004/298", ["ENEMY"]));
        Assert.Contains("EZREAL_ENEMY_TARGETS_THIS_TURN:P1:2", game.State.UntilEndOfTurnEffects);
        var before = game.State;
        var secondSpell = Assert.Single(before.StackItems).StackItemId;
        await game.Act("P1", new LegendActCommand("UNIT", LegendActionAbilityCatalog.EzrealLegendAbilityId, [], []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(before.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(before.PlayerZones["P1"].Hand, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.StackItems.Count);
        var item = game.State.StackItems[^1];
        var context = Assert.IsType<LegendDiscardDrawContext>(item.LegendDiscardDraw);
        foreach (var forged in new[] { item with { LegendDiscardDraw = null },
            item with { EffectKind = "LEGEND_DISCARD_DRAW" }, item with { TimingContext = "FORGED" },
            item with { LegendDiscardDraw = context with { CardNo = "VEN·143" } } })
            Assert.Contains("invalid captured legend discard effect", OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [game.State.StackItems[0], forged] }));
        Assert.Contains("invalid captured legend discard effect", OfficialInsightAndSpellLockTests.Errors(game.State with {
            UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(e => !e.StartsWith("EZREAL_ENEMY_TARGETS_THIS_TURN:", StringComparison.Ordinal)).ToArray() }));
        await game.Top();
        Assert.Contains(before.PlayerZones["P1"].MainDeck[0], game.State.PlayerZones["P1"].Hand);
        Assert.Equal(secondSpell, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        await CloseExplorerDuel(game);
        await game.Replay();
    }
    [Theory]
    [InlineData("SFD·199/221", false)]
    [InlineData("SFD·248/221", false)]
    [InlineData("SFD·199/221", true)]
    [InlineData("SFD·248/221", true)]
    public async Task NativeExplorerReactionCanStartAChainAtOpenPriorityOrDuelFocus(string donor, bool duel)
    {
        var initial = ExplorerReactionPosition(donor);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        foreach (var spell in new[] { "EXPLORER-TARGET-1", "EXPLORER-TARGET-2" }) {
            await game.Act("P1", new PlayCardCommand(spell, "OGN·004/298", ["ENEMY"]));
            await game.Top();
            await CloseExplorerDuel(game);
        }
        if (duel) {
            await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
            Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
            Assert.Equal("P1", game.State.FocusPlayerId);
        }
        var before = game.State;
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(before)["P1"].Candidates ?? [], c => c.Action == "LEGEND_ACT");
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "UNIT"));
        Assert.False(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        await RadGearTestPosition.Reject(before, "P2", new LegendActCommand("UNIT", LegendActionAbilityCatalog.EzrealLegendAbilityId, [], []));
        await game.Act("P1", new LegendActCommand("UNIT", LegendActionAbilityCatalog.EzrealLegendAbilityId, [], []));
        Assert.Equal(before.PlayerZones["P1"].Hand, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(duel ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed, game.State.TimingState);
        Assert.NotNull(Assert.Single(game.State.StackItems).LegendDiscardDraw);
        await game.Top();
        Assert.Contains(before.PlayerZones["P1"].MainDeck[0], game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.PendingHandChoice);
        await game.Replay();
    }

}
