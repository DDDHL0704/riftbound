using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ScarletRosePlayObserverTests
{
    private const string Rose = "UNL-109/219";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AUnitPlayMayPayExactlyOneManaBeforeAnIndependentExperienceSkill(bool accept)
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Rose));
        await game.Act("P1", new PlayCardCommand("BASIC", "OGN·219/298", []));
        var mana = game.State.RunePools["P1"].Mana;
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        await UnitPlayObserverTestPosition.PayRose(game, accept);
        Assert.Equal(mana - (accept ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        Assert.False(game.State.CardObjects["OBSERVER"].IsExhausted);
        if (accept) await game.Top();
        Assert.Equal(accept ? 1 : 0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P2")); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PlayingAnEquipmentOrASpellDoesNotEarnTheUnitPlayExperience(bool equipment)
    {
        var initial = UnitPlayObserverTestPosition.Create(Rose);
        if (equipment) initial = RadGearTestPosition.Put(initial, "SECOND-ROSE", Rose, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", equipment ? new PlayCardCommand("SECOND-ROSE", Rose, [])
            : new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        if (!equipment) await game.Top();
        Assert.Null(game.State.PendingPayment); Assert.Empty(game.State.StackItems);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1")); await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task OnlyTheCurrentControllerPaysAndGainsExperienceForItsUnitPlay(string controller)
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Rose, controller));
        await game.Act("P1", new PlayCardCommand("BASIC", "OGN·219/298", []));
        if (controller == "P1") { await UnitPlayObserverTestPosition.PayRose(game); await game.Top(); }
        else { Assert.Null(game.State.PendingPayment); Assert.Empty(game.State.StackItems); }
        Assert.Equal(controller == "P1" ? 1 : 0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P2")); await game.Replay();
    }

    [Fact]
    public async Task EachUnitTokenPlayedByOneActualSpellEarnsItsOwnOptionalPayment()
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Rose));
        await game.Act("P1", new PlayCardCommand("BURST", "UNL-069/219", [])); await game.Top();
        Assert.Equal(2, game.State.CardObjects.Values.Count(TokenObjectRules.IsToken));
        await UnitPlayObserverTestPosition.Order(game); var mana = game.State.RunePools["P1"].Mana;
        for (var i = 0; i < 2; i++) await UnitPlayObserverTestPosition.PayRose(game);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        for (var i = 0; i < 2; i++) await game.Top();
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Fact]
    public async Task RepeatedUnitPlaysCanEarnExperienceWithoutExhaustingTheRose()
    {
        var initial = RadGearTestPosition.Put(UnitPlayObserverTestPosition.Create(Rose), "SECOND", "OGN·219/298", "P1", "HAND");
        var game = new Driver(initial);
        foreach (var id in new[] { "BASIC", "SECOND" })
        {
            await game.Act("P1", new PlayCardCommand(id, "OGN·219/298", []));
            await UnitPlayObserverTestPosition.PayRose(game); await game.Top();
        }
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        Assert.False(game.State.CardObjects["OBSERVER"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task PaymentRejectsTheOtherPlayerAndUnrelatedDuelFactsCannotBeInjected()
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Rose));
        await game.Act("P1", new PlayCardCommand("BASIC", "OGN·219/298", []));
        var cost = game.State.PendingPayment!;
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(cost.PaymentId, cost.PaymentWindow, ["PAY"]));
        var skill = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
            StackItems = [skill with { UnitEvent = skill.UnitEvent! with { PlayedUnitDuringSpellDuel = true } }] }));
        await UnitPlayObserverTestPosition.PayRose(game); await game.Top(); await game.Replay();
    }
    [Theory]
    [InlineData("erase")] [InlineData("tick")]
    public async Task AcceptedPlayCaptureCannotBeErasedOrRetimedAfterPayment(string mutation)
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Rose));
        await game.Act("P1", new PlayCardCommand("BASIC", "OGN·219/298", []));
        await UnitPlayObserverTestPosition.PayRose(game);
        var skill = Assert.Single(game.State.StackItems);
        var changed = mutation == "erase" ? game.State with { StackItems = [] }
            : game.State with { StackItems = [skill with { UnitEvent = skill.UnitEvent! with {
                CapturedTick = skill.UnitEvent.CapturedTick + 1 } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, changed));
        await game.Top(); await game.Replay();
    }

}
