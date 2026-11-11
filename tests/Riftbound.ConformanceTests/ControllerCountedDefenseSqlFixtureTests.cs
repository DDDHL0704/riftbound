using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ControllerCountedDefenseSqlFixtureTests
{
    [Theory]
    [MemberData(nameof(CurrentTeemoCountedDamageTests.Counts), MemberType = typeof(CurrentTeemoCountedDamageTests))]
    public async Task ExactSqlFixtureConfirmsDefenseBeforeCountingAndAddsBothBonusesOnce(string printing, int count)
    {
        var initial = ControllerDamageBonusSqlAcceptance.CreateInitialState("SQL-CONTROLLER-DEFENSE", printing, count);
        var deck = initial.PlayerZones["P2"].MainDeck;
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:FIELD"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(1, choice.RequiredCount);
        Assert.Equal(new[] { "ATTACKER" }, choice.LegalObjectIds);
        Assert.Equal(deck, game.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "MAIN_DECK_CARDS_REVEALED" or "DAMAGE_APPLIED" or "CARDS_RECYCLED");

        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ATTACKER"]));
        await game.Top();
        var reveal = Assert.Single(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        Assert.Equal(count, reveal.Event.Payload["damageAmount"]);
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED").ToArray();
        if (count == 0) Assert.Empty(damage);
        else Assert.Equal(count + 2, Assert.Single(damage).Event.Payload["damage"]);
        Assert.Equal(count == 0 ? 0 : count + 2, game.State.CardObjects["ATTACKER"].Damage);
        Assert.Equal("LOWER", game.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Equal(deck.Take(5).Order(), game.State.PlayerZones["P2"].MainDeck.Skip(1).Order());
        Assert.Equal(initial.RunePools, game.State.RunePools);
        await game.Replay();
    }

    [Fact]
    public async Task FormerFaerieAttackerQueuesIndependentMovementBeforeDefense()
    {
        var initial = ControllerDamageBonusSqlAcceptance.CreateInitialState("SQL-FORMER-ATTACKER", "OGN·121/298", 1);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ATTACKER"] = initial.CardObjects["ATTACKER"] with { CardNo = "SFD·125/221" } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:FIELD"));
        Assert.Contains(game.State.TriggerQueue, trigger => trigger.SourceObjectId == "ATTACKER");
        Assert.Contains(game.State.TriggerQueue, trigger => trigger.SourceObjectId == "DEFENDER");
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(CommandTypes.ChooseCards, game.PromptFor("P2").EnabledActions());
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "MAIN_DECK_CARDS_REVEALED" or "DAMAGE_APPLIED");
        await game.Replay();
    }
}
