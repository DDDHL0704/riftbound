using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task AConditionalCounterCannotChooseEitherUntargetedEarnedSkill(bool beforeCreation)
    {
        var initial = RadGearTestPosition.Put(Position(), "COUNTER", "SFD·045/221", "P2", "HAND");
        for (var i = 0; i < 3; i++)
            initial = RadGearTestPosition.Put(initial, "COUNTER-FUNDS-" + i, "UNL-R02", "P2", "BASE");
        var game = new Driver(initial);
        await EarnHold(game);
        if (!beforeCreation)
        {
            await game.Top(); await Fund(game);
            await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        }
        var skill = Assert.Single(game.State.StackItems);
        Assert.Empty(skill.TargetObjectIds);
        Assert.Equal("P1", game.State.PriorityPlayerId);
        await game.Act("P1", new PassPriorityCommand());
        await game.TapRunes("P2", ["COUNTER-FUNDS-0", "COUNTER-FUNDS-1"]);
        await game.Act("P2", new RecycleRuneCommand("COUNTER-FUNDS-2"));
        Assert.Equal(2, game.State.RunePools["P2"].Mana);
        Assert.Equal(1, game.State.RunePools["P2"].PowerByTrait[RuneTrait.Green]);
        await RadGearTestPosition.Reject(game.State, "P2",
            new PlayCardCommand("COUNTER", "SFD·045/221", [skill.StackItemId]));
        if (beforeCreation)
        {
            await game.Top(); await Fund(game);
            await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        }
        await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Contains("COUNTER", game.State.PlayerZones["P2"].Hand);
        await game.ColdCheckpoint();
    }
}
