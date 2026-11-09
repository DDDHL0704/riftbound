using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task AnotherUnitEventLocksTheOriginalEnemyBeforeWardAndKeepsItAfterPayment(bool pay)
    {
        var initial = ArmamentGrantedCombatTriggerTests.Position();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·161/298", out var ward));
        var cards = initial.CardObjects.ToDictionary();
        foreach (var id in new[] { "ENEMY1", "ENEMY2" })
        {
            var printed = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·161/298", "P2");
            cards[id] = printed with { Tags = printed.Tags.Concat(ward.SourceUnitTags.Split('|')).Distinct().ToArray() };
        }
        var game = new Driver(initial with { CardObjects = cards });
        await ArmamentGrantedCombatTriggerTests.Attach(game);
        await ArmamentGrantedCombatTriggerTests.Battle(game, false);
        await ArmamentGrantedCombatTriggerTests.Choose(game, "ENEMY1");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.PowerCost);
        var item = Assert.Single(game.State.StackItems);
        Assert.Empty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State));
        var forged = item with { TargetObjectIds = ["ENEMY2"],
            TargetGenerations = new Dictionary<string, long> { ["ENEMY2"] = game.State.CardObjects["ENEMY2"].ObjectGeneration } };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [forged] }),
            error => error == "unit event targets differ from original accepted confirmation");
        var before = game.State.RunePools["P1"].TotalPower;
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(before - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        if (pay)
        {
            Assert.Equal(new[] { "ENEMY1" }, Assert.Single(game.State.StackItems).TargetObjectIds);
            await game.Top();
        }
        else Assert.Empty(game.State.StackItems);
        Assert.Equal(pay ? 2 : 0, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(0, game.State.CardObjects["ENEMY2"].Damage);
        await game.ColdCheckpoint();
    }
}
