using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VexCombatSpellCostTests
{
    [Fact]
    public async Task TheFriendlyCombatDiscountChangesJhinQualificationAfterTheCombinedEchoPayment()
    {
        var initial = RadGearTestPosition.Put(Position(spell: "UNL-061/219"), "JHIN", "UNL-089/219", "P1", "HAND");
        var game = new Driver(initial); await Challenge(game);
        var command = new PlayCardCommand(SpellObject, "UNL-061/219", [], OptionalCosts: ["ECHO"]);
        var quote = Quote(game.State, "P1", command); Balanced(quote);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command);
        Assert.Equal(3, Assert.Single(game.State.CardPlays.Finalized, entry => entry.Source.ObjectId == SpellObject).PaidMana);
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount);
        await game.Top();
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ActualCopiedEnemyCombatTaxMakesOneSpellQualifyForJhinEvenOnTheOpponentsTurn()
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR", "SFD·059/221", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "OPP-JHIN", "UNL-089a/219", "P2", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        await Challenge(game); await Focus(game, "P2");
        var command = Cast("OPP-SPELL", "HOST");
        var quote = Quote(game.State, "P2", command); Balanced(quote);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower); Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Blue]);
        await game.Act("P2", command);
        var payment = Assert.Single(game.State.CardPlays.Finalized, entry => entry.Source.ObjectId == "OPP-SPELL");
        Assert.Equal("P2", payment.PlayerId); Assert.Equal(4, payment.PaidMana);
        Assert.Equal("P1", game.State.TurnPlayerId);
        Assert.True(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P2", 4));
        Assert.False(CoreRuleEngine.ControllerConfirmedSpellWithPaidMana(game.State, "P1", 4));
        await game.Top(); await game.ColdCheckpoint();
    }
}
