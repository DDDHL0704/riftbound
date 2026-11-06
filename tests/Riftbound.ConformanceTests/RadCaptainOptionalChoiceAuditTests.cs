using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadCaptainOptionalChoiceAuditTests
{
    [Fact]
    public async Task CaptainMayDeclineAnEligibleEquipmentAndRecycleEveryLookedCard()
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·129/167");
        var first = initial.PlayerZones["P1"].MainDeck[0];
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [first] = OfficialGarenConquestBattlefieldCountTests.Printed(first, "SFD·022/221", "P1") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·129/167", []));
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Contains(first, choice.LegalObjectIds);
        var looked = choice.ContextObjectIds.Order(StringComparer.Ordinal).ToArray();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal(looked, game.State.PlayerZones["P1"].MainDeck.Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }
}
