using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedReturnPlayPosition(bool empowered = false)
    {
        var state = HandDiscardObserverTests.Put(BorrowedBombPosition("RAD·115/167"), "TARGET", "VEN·086", "P1", "BASE");
        state = HandDiscardObserverTests.Put(state, "PLAY", "OGN·001/298", "P1", "HAND");
        state = HandDiscardObserverTests.Put(state, "EQUAL", "VEN·118", "P1", "HAND");
        state = HandDiscardObserverTests.Put(state, "LANTERN", "RAD·113/167", "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TARGET"] = state.CardObjects["TARGET"] with { IsEmpowered = empowered } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }) } };
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task BorrowedReturnPlayCapturesMightAndOffersOnlyLowerUnitsWithAllCostsWaived(bool decline, bool token, bool sourceLeaves)
    {
        var initial = BorrowedReturnPlayPosition();
        if (token)
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T01", out var factory));
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["TARGET"] = factory.CreateObject("TARGET", "P1", "P1") with { Power = 6, UntilEndOfTurnPowerModifier = 3 } } };
        }
        var id = TemplateId(initial, "免费打出战力更低");
        foreach (var illegal in new[] { "GEAR", "ENEMY", "BF", "GRAVE" })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, [illegal]));
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }) } },
            "P1", new ActivateAbilityCommand("HOST", id, ["TARGET"]));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["TARGET"]));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        if (sourceLeaves)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(5, pending.MaximumUnitPower);
        Assert.Equal(sourceLeaves ? new[] { "HOST", "PLAY" } : new[] { "PLAY" }, pending.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(!token, game.State.CardObjects.ContainsKey("TARGET"));
        var mana = game.State.RunePools["P1"].Mana;
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("EQUAL", "VEN·118", [], Destination: "BASE"));
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await game.Act("P1", decline ? new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", [])
            : new PlayCardCommand("PLAY", "OGN·001/298", [], Destination: "BASE"));
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(!decline, game.State.PlayerZones["P1"].Base.Contains("PLAY"));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnPlayFaqOffersTheEightMightLanternAfterGangplanksReplacementWithColdRecovery(bool borrowed)
    {
        var initial = BorrowedReturnPlayPosition(empowered: true);
        if (!borrowed) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand(borrowed ? "HOST" : "UNIT", borrowed
            ? TemplateId(initial, "免费打出战力更低") : P4ActivatedAbilityCatalog.TranspositionMageAbilityId, ["TARGET"]));
        await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones["P1"].Base);
        Assert.Equal(9, game.State.CardObjects["TARGET"].Power);
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(8, pending.MaximumUnitPower);
        Assert.Contains("LANTERN", pending.Sources.Keys);
        Assert.Contains("invalid revealed hand effect-play continuation", OfficialInsightAndSpellLockTests.Errors(game.State with {
            PendingEffectPlay = pending with { MaximumUnitPower = 9 } }));
        Assert.Contains("invalid revealed hand effect-play continuation", OfficialInsightAndSpellLockTests.Errors(game.State with {
            PendingEffectPlay = pending with { DestinationPolicy = "BASE" } }));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("LANTERN", "RAD·113/167", [], Destination: "BASE"));
        Assert.Contains("LANTERN", game.State.PlayerZones["P1"].Base);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedReturnPlaySkipsTheDependentPlayWhenAResponseReturnsItsTarget()
    {
        var initial = BorrowedReturnPlayPosition();
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "免费打出战力更低"), ["TARGET"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["TARGET"]));
        await game.Top();
        await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedReturnPlayCanReplayItsReturnedBorrowerWithAFreshGeneration()
    {
        var initial = BorrowedBombPosition("RAD·115/167");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = initial.CardObjects["HOST"].Power + 1, UntilEndOfTurnPowerModifier = 1 } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }) } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "免费打出战力更低"), ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PendingEffectPlay!.Sources.Keys);
        await game.Act("P1", new PlayCardCommand("HOST", "OGN·111/298", [], Destination: "BASE"));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration >= initial.CardObjects["HOST"].ObjectGeneration + 2);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
