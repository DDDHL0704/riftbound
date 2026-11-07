using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class FriendlySpellshieldEquipmentAuraTests
{
    private const string Soulguard = "OGN·063/298";
    private const string Monument = "SFD·104/221";
    private static MatchState Position(string no, string unit = "OGN·219/298", bool inHand = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", no, "P1", inHand ? "HAND" : "BASE");
        state = RadGearTestPosition.Put(state, "ALLY", unit, "P1", "BASE");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(unit, out var printedUnit));
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { Tags = state.CardObjects["ALLY"].Tags
                .Concat(printedUnit.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray() } };
        if (!inHand) {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var printedGear));
            cards["GEAR"] = cards["GEAR"] with { Tags = cards["GEAR"].Tags
                .Concat(printedGear.SourceEquipmentTags.Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray() };
        }
        state = state with { CardObjects = cards };
        return RadGearTestPosition.Put(state, "SPELL", "OGS·003/024", "P2", "HAND");
    }

    private static MatchState WithBoon(MatchState state, string id = "ALLY")
    {
        Assert.True(BoonRules.TryGrant(state.CardObjects[id], state.PlayerZones, state.CardObjects, out var unit));
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = unit } };
    }
    private static int Tax(MatchState state, string id = "ALLY")
        => CoreRuleEngine.SpellshieldPowerCostForTarget(state, "P2", id);

    private static async Task FinishPlay(Driver game)
    {
        for (var i = 0; game.State.StackItems.Count > 0; i++) {
            Assert.True(i < 4);
            await game.Top();
        }
    }

    [Theory]
    [InlineData(Soulguard)] [InlineData(Monument)]
    public async Task ActualEquipmentPlayAddsTheCurrentFriendlySpellshieldAura(string no)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(no, inHand: true), "OTHER_ALLY", "OGN·219/298", "P1", "BASE"));
        Assert.Equal(0, Tax(game.State));
        await game.Act("P1", new PlayCardCommand("GEAR", no, no == Soulguard ? ["ALLY"] : []));
        await FinishPlay(game);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Equal(no == Soulguard ? 1 : 0, game.State.CardObjects["ALLY"].BoonCount);
        Assert.Equal(1, Tax(game.State));
        Assert.Equal(no == Soulguard ? 0 : 1, Tax(game.State, "OTHER_ALLY"));
        Assert.Equal(0, Tax(game.State, "ENEMY"));
        Assert.Contains(game.State.ContinuousEffects, effect => effect.SourceObjectId == "GEAR"
            && effect.TargetObjectId == "ALLY" && effect.Layer == ContinuousEffectLayers.RuleText);
        Assert.DoesNotContain(CardResourceKeywordNames.Spellshield, game.State.CardObjects["ALLY"].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData(1, 0, 0, 1)] [InlineData(2, 0, 0, 1)]
    [InlineData(1, 1, 0, 1)] [InlineData(2, 2, 0, 2)]
    [InlineData(1, 0, 1, 1)] [InlineData(2, 0, 2, 2)]
    [InlineData(1, 2, 1, 3)] [InlineData(2, 2, 2, 4)]
    public async Task SoulguardOnlySuppliesMissingSpellshieldAndRealTargetPaymentsUseTheTotal(
        int copies, int printedWard, int monuments, int expectedTax)
    {
        var unit = printedWard switch { 1 => "OGN·161/298", 2 => "SFD·120/221", _ => "OGN·219/298" };
        var state = WithBoon(RadUnitEffectTestFixture.Place(Position(Soulguard, unit), "ALLY", "P1", "BATTLEFIELD", "BF"));
        for (var i = 1; i < copies; i++) state = RadGearTestPosition.Put(state, "GEAR" + i, Soulguard, "P1", "BASE");
        for (var i = 0; i < monuments; i++) state = RadGearTestPosition.Put(state, "MONUMENT" + i, Monument, "P1", "BASE");
        Assert.Equal(expectedTax, Tax(state));
        var conditionalEffects = state.ContinuousEffects.Where(effect => effect.TargetObjectId == "ALLY"
            && effect.SourceObjectId?.StartsWith("GEAR", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(printedWard + monuments == 0 ? 1 : 0, conditionalEffects.Length);
        state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(10, expectedTax - 1) } };
        await RadGearTestPosition.Reject(state, "P2", new PlayCardCommand("SPELL", "OGS·003/024", ["ALLY"]));
        var game = new Driver(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P2"] = new(10, expectedTax) } });
        await game.Act("P2", new PlayCardCommand("SPELL", "OGS·003/024", ["ALLY"]));
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event;
        Assert.Equal(expectedTax, paid.Payload["spellshieldTaxPower"]);
        Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
        await FinishPlay(game);
        await game.Replay();
    }

    [Fact]
    public async Task SoulguardLosesItsConditionalGrantWhenTheActualBoonCostConsumesTheLastBoon()
    {
        var game = new Driver(Position(Soulguard, "OGN·164/298", inHand: true));
        await game.Act("P1", new PlayCardCommand("GEAR", Soulguard, ["ALLY"]));
        await FinishPlay(game);
        Assert.Equal(1, Tax(game.State));
        await game.Act("P1", new ActivateAbilityCommand("ALLY", "SOURCE-BOON-MIGHT:0", []));
        Assert.Equal(0, game.State.CardObjects["ALLY"].BoonCount);
        Assert.Equal(0, Tax(game.State));
        Assert.DoesNotContain(game.State.ContinuousEffects, effect => effect.SourceObjectId == "GEAR"
            && effect.TargetObjectId == "ALLY");
        await game.Top();
        Assert.Equal(0, Tax(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(Soulguard)] [InlineData(Monument)]
    public async Task DestroyingTheSourceEndsTheAuraAfterTheDestructionInstruction(string no)
    {
        var initial = Position(no);
        if (no == Soulguard) initial = WithBoon(initial);
        var game = new Driver(RadGearTestPosition.Put(initial, "DESTROY", "SFD·005/221", "P1", "HAND"));
        Assert.Equal(1, Tax(game.State));
        await game.Act("P1", new PlayCardCommand("DESTROY", "SFD·005/221", ["GEAR"]));
        Assert.Equal(1, Tax(game.State));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, Tax(game.State));
        Assert.DoesNotContain(game.State.ContinuousEffects, effect => effect.SourceObjectId == "GEAR");
        await game.Replay();
    }

    [Fact]
    public async Task MonumentStopsProtectingUnitsWhenItExpiresAtItsControllersNextStartPhase()
    {
        var game = new Driver(Position(Monument, inHand: true));
        await game.Act("P1", new PlayCardCommand("GEAR", Monument, []));
        await FinishPlay(game);
        Assert.Equal(1, Tax(game.State));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(1, Tax(game.State));
        await game.Act("P2", new EndTurnCommand());
        Assert.Contains(game.State.StackItems, item => item.EphemeralStart is not null);
        Assert.Equal(1, Tax(game.State));
        await FinishPlay(game);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, Tax(game.State));
        await game.Replay();
    }
}
