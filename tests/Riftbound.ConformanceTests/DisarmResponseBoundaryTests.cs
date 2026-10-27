using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DisarmTriggerTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class DisarmResponseBoundaryTests
{
    [Fact]
    public async Task TheAlreadyTriggeredAttackDoesNotTriggerAgainWhenCombatStarts()
    {
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(await Position("RAD·054/167", 1))).State, "ENEMY1");
        var done = await AttackDamageDistributionTests.Top(confirmed.State);
        var closed = await AttackDamageDistributionTests.Top(done.State);
        var combat = await Act(closed.State, "P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1"], ["COMBAT_ASSIGNMENT"]));
        Record("combat-start", "done", combat.State, combat.Events);
        Assert.Empty(combat.State.TriggerQueue); Assert.Empty(combat.State.StackItems);
        Assert.DoesNotContain(combat.Events, e => e.Kind is "TRIGGER_QUEUED" or "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.False(combat.State.BattleState.IsActive);
        Assert.Contains(combat.Events, e => e.Kind == "BATTLE_CLOSED");
        Restore(combat.State); ArmamentMightTests.PublicRecovery(combat.State);
    }

    [Theory]
    [InlineData("HOST", "P1")]
    [InlineData("ENEMY1", "P2")]
    public async Task ActualReturnToHandKeepsHistoricalSourceAndTargetGenerations(string id, string owner)
    {
        var initial = AttackDamageDistributionTests.Spell(await Position("RAD·054/167", 1), "RETURN", "OGN·169/298", "P2");
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(initial)).State, "ENEMY1");
        var passed = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var returned = await TopWithReceipt((await Act(passed.State, "P2",
            new PlayCardCommand("RETURN", "OGN·169/298", [id]))).State, "return-" + id);
        Assert.Contains(id, returned.State.PlayerZones[owner].Hand);
        Assert.Equal(initial.CardObjects[id].ObjectGeneration + 1, returned.State.CardObjects[id].ObjectGeneration);
        Record("return-" + id, "response", returned.State, returned.Events);
        var done = await AttackDamageDistributionTests.Top(returned.State);
        Assert.Equal(3, done.State.CardObjects["ENEMY1"].Power);
        Assert.Empty(done.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Record("return-" + id, "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task AnActualCounterStopsTheSkillWithoutRemovingItsSource()
    {
        var initial = AttackDamageDistributionTests.Spell(await Position("RAD·054/167", 1), "COUNTER", "SFD·045/221", "P2");
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(initial)).State, "ENEMY1");
        var passed = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var done = await AttackDamageDistributionTests.Top((await Act(passed.State, "P2",
            new PlayCardCommand("COUNTER", "SFD·045/221", [confirmed.State.StackItems.Single().StackItemId]))).State);
        Assert.Empty(done.State.StackItems); Assert.Equal(3, Might(done.State));
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
        Record("counter", "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
