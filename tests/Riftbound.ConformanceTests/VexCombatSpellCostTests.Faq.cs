using Riftbound.Contracts;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VexCombatSpellCostTests
{
    // Chinese Unleashed judge FAQ 260511: two attacking Vexes, Ezreal in
    // base and Bellows Breath with Echo leave exactly one mana to pay.
    [Fact]
    public async Task TheOfficialTwoVexEzrealAndBellowsEchoExamplePaysExactlyOneMana()
    {
        var initial = RadGearTestPosition.Put(Position(spell: "SFD·080/221"), "SECOND-VEX", Vex, "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "EZREAL", "SFD·149/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", [], ["HOST", "SECOND-VEX"]));
        Assert.All(new[] { "HOST", "SECOND-VEX" }, id => Assert.True(game.State.CardObjects[id].IsAttacking));
        var before = game.State.RunePools["P1"];
        var command = new PlayCardCommand(SpellObject, "SFD·080/221", ["ENEMY"],
            OptionalCosts: ["ECHO", "OPTIONAL_COST_REDUCTION:EZREAL:0:ECHO:MANA"]);
        var quote = Quote(game.State, "P1", command); Balanced(quote);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command);
        Assert.Equal(before.Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount);
        await game.Top(); await game.ColdCheckpoint();
    }
}
