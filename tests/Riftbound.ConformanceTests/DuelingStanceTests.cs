using Riftbound.Contracts;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;

namespace Riftbound.ConformanceTests;

public sealed class DuelingStanceTests
{
    private const string Stance = "OGN·046/298";
    private const string Rescue = "OGN·104/298";

    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 1)]
    public async Task BonusCountsOnlyFriendlyUnitsAtTheTargetsLocation(bool battlefield, bool allyHere, int expected)
    {
        var state = PrintedExhaustAbilityTests.Position("OGN·219/298");
        if (battlefield)
        {
            state = At(state, "HOST", "P1", "BATTLEFIELD", "F");
            state = At(state, "ALLY", "P1", "BATTLEFIELD", allyHere ? "F" : "OTHER");
            state = At(state, "ENEMY", "P2", "BATTLEFIELD", "F");
        }
        else if (!allyHere)
        {
            state = At(state, "ALLY", "P1", "BATTLEFIELD", "F");
        }

        var game = new Driver(AttackDamageDistributionTests.Spell(state, "STANCE", Stance, "P1"));
        await game.Act("P1", new PlayCardCommand("STANCE", Stance, ["HOST"]));
        await game.Top();
        Assert.Equal(expected, game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
    }

    [Theory]
    [InlineData("ALLY", 2)]
    [InlineData("HOST", 0)]
    public async Task BonusUsesBoardAtResolutionAndSkipsDepartedTarget(string recalled, int expected)
    {
        var state = PrintedExhaustAbilityTests.Position("OGN·219/298");
        state = AttackDamageDistributionTests.Spell(state, "STANCE", Stance, "P1");
        state = AttackDamageDistributionTests.Spell(state, "RESCUE", Rescue, "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("STANCE", Stance, ["HOST"]));
        if (game.State.PriorityPlayerId == "P2")
            await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RESCUE", Rescue, [recalled]));
        await game.Top();
        Assert.Contains(recalled, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(expected, game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        await game.Replay();
    }
}
