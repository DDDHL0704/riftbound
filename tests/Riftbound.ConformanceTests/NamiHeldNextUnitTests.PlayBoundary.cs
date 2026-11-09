using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Fact]
    public async Task AnEarnedHoldGrantAndASecondNamisPaidPlayStunKeepSeparateOriginalSkills()
    {
        var initial = RadGearTestPosition.Put(Position(), "FIRST", Nami, "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "GREEN", "UNL-R02", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "ENEMY", "OGN·219/298", "P2", "BASE");
        var game = new Driver(initial);
        await EarnHold(game); await game.Top(); await Fund(game);
        await game.Act("P1", new RecycleRuneCommand("GREEN"));
        var resources = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("FIRST", Nami, [], OptionalCosts: ["SPEND_POWER:1:GREEN"]));
        Assert.Equal(resources.Mana - 3, game.State.RunePools["P1"].Mana);
        Assert.Equal(resources.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal("FIRST", choice.SourceObjectId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.Single(game.State.StackItems, item => item.TurnUnitPlayBoon?.Unit.ObjectId == "FIRST");
        Assert.Single(game.State.StackItems, item => item.SourceObjectId == "FIRST" && item.SourceConfirmed);
        await game.ColdCheckpoint();
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        var use = Assert.Single(CoreRuleEngine.UnitEventTurnPlayGrantUses(game.State));
        Assert.Equal("HOST", use.Grant.Source.ObjectId);
        Assert.Equal("FIRST", use.Unit.ObjectId);
        await game.Act("P1", new PlayCardCommand("SECOND", Unit, []));
        Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["SECOND"]));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task MerelyPlayingNamiDoesNotCreateHerHoldGrant()
    {
        var initial = RadGearTestPosition.Put(Position(), "HOST", Nami, "P1", "HAND") with {
            ActivePlayerId = "P1", TurnPlayerId = "P1", RunePools = new Dictionary<string, RunePool> {
                ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        var game = new Driver(initial);
        await Fund(game);
        await game.Act("P1", new PlayCardCommand("HOST", Nami, []));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Empty(CoreRuleEngine.UnitEventTurnPlayGrants(game.State));
        Assert.DoesNotContain(game.Events(), record => record.Event.Kind == "UNIT_EVENT_TURN_PLAY_GRANT_CREATED");
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
