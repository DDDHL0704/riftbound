using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PowerfulPlayedLegendTests
{
    private static async Task<ResolutionResult> Open(string legend)
    {
        var state = OfficialCopyIdentityTests.AddSpell(ArmamentMightTests.Position(), "PLAYED", "SFD·175/221");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["PLAYED"] = state.CardObjects["PLAYED"] with { Tags = [CardObjectTags.UnitCard] },
                ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: "P1", tags: ["CARD_TYPE:LEGEND"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        state = PrintedCostFixture.Add(state, "P1", "SFD·175/221");
        var played = await Act(state, "P1", new PlayCardCommand("PLAYED", "SFD·175/221", []));
        return await Top(played.State);
    }

    [Theory]
    [InlineData("OGN·249/298")]
    [InlineData("FND-249/298")]
    public async Task PlayingMightyOffersAChoiceBeforeExhaustingOrCalling(string legend)
    {
        var opened = await Open(legend);
        Assert.Contains("PLAYED", opened.State.PlayerZones["P1"].Base);
        Assert.Equal(3, opened.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(1, choice.MaxCount);
        Assert.DoesNotContain(opened.Events, e => e.Kind is "RUNES_CALLED" or "LEGEND_EXHAUSTED");
        Restore(opened.State); ArmamentMightTests.PublicRecovery(opened.State);
    }

    [Theory]
    [InlineData("OGN·249/298", true)] [InlineData("OGN·249/298", false)]
    [InlineData("FND-249/298", true)] [InlineData("FND-249/298", false)]
    public async Task ChoiceCanBeDeclinedOrPaidBeforeTheResponseWindow(string legend, bool accept)
    {
        var opened = await Open(legend);
        var choice = Assert.IsType<PendingCardChoiceState>(opened.State.PendingCardChoice);
        var selected = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["LEGEND"] : []));
        Assert.Equal(accept, selected.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(3, selected.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(selected.Events, e => e.Kind == "RUNES_CALLED");
        Restore(selected.State); ArmamentMightTests.PublicRecovery(selected.State);
        if (!accept) { Assert.Empty(selected.State.StackItems); return; }
        Assert.Single(selected.State.StackItems);
        var resolved = await Top(selected.State);
        Assert.Equal(2, resolved.State.PlayerZones["P1"].RuneDeck.Count);
        var rune = Assert.Single(resolved.Events, e => e.Kind == "RUNES_CALLED");
        var id = Assert.Single(Assert.IsType<string[]>(rune.Payload["runeObjectIds"]));
        Assert.Contains(id, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[id].IsExhausted);
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }
}
