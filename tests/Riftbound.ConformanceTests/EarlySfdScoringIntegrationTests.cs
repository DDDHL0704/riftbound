using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EarlySfdScoringIntegrationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AConquestThatEarnsAnAliasedHoldSkillDoesNotDiscountTheGiant(bool preventPoints)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", "SFD·027/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GIANT", "SFD·055/221", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ARMAMENT", "SFD·030/221", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ARMAMENT"] = state.CardObjects["ARMAMENT"] with { AttachedToObjectId = "HOST" } } };
        if (preventPoints)
        {
            state = RadGearTestPosition.Put(state, "TIANNA-FIELD", "OGN·296/298", "P2", "BATTLEFIELD", "TIANNA-FIELD");
            state = RadGearTestPosition.Put(state, "TIANNA", "SFD·060/221", "P2", "BATTLEFIELD", "TIANNA-FIELD");
        }
        state = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        var expectedDraw = state.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        for (var pass = 0; game.State.SpellDuelState.IsActive && pass < 6; pass++)
            await game.Act(game.State.PriorityPlayerId ?? game.State.FocusPlayerId!, new PassFocusCommand());
        Assert.False(game.State.SpellDuelState.IsActive);
        Assert.Equal(preventPoints ? 0 : 1, game.State.PlayerScores["P1"]);
        Assert.Equal("HOLD_DRAW", Assert.Single(game.State.StackItems).EffectKind);
        Assert.Equal(new[] { "GIANT" }, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.All(expectedDraw, id => Assert.Contains(id, game.State.PlayerZones["P1"].Hand));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_HELD");
        Assert.Empty(game.State.BattlefieldHolds?.Entries ?? []);
        var command = new PlayCardCommand("GIANT", "SFD·055/221", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.Equal(10, quote.Cost!.Mana);
        Assert.Equal(3, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        await game.Replay();
    }
}
