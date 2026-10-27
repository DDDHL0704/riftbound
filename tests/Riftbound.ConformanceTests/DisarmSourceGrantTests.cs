using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DisarmTriggerTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class DisarmSourceGrantTests
{
    private static MatchState AddPatrol(MatchState state, string id, string controller = "P1")
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: "RAD·055/167", power: 2, manaCost: 3, ownerId: controller,
                controllerId: controller, tags: [CardObjectTags.UnitCard, "COLOR:blue", "机械"]) } };
        return At(state, id, controller, "BASE");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task EachIndependentPatrolGrantsOneAndTheMechConfirmsOnlyOneCombinedSkill(int count)
    {
        var initial = await Position("RAD·055/167", 0);
        for (var i = 2; i <= count; i++) initial = AddPatrol(initial, "PATROL" + i);
        var attack = await Attack(initial);
        var item = Assert.Single(attack.State.StackItems);
        Assert.Empty(attack.State.TriggerQueue);
        Assert.Equal(count, item.FieldContext!.Disarm!.Contributions.Count);
        Assert.Equal(count, item.FieldContext.Disarm.Contributions.Sum(c => c.Amount));
        Assert.All(item.FieldContext.Disarm.Contributions, c => Assert.NotNull(c.AuraKey));
        var confirmed = await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1");
        var done = await AttackDamageDistributionTests.Top(confirmed.State);
        Assert.Equal(3 - count, Might(done.State)); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Assert.Single(done.Events, e => e.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Record("patrol-" + count, "attack", attack.State, attack.Events);
        Record("patrol-" + count, "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("opponent")]
    [InlineData("hand")]
    [InlineData("non-mech")]
    public async Task AGrantRequiresItsFriendlyMechAndAnOnFieldSource(string branch)
    {
        var initial = await Position(branch == "non-mech" ? "RAD·054/167" : "RAD·055/167", branch == "non-mech" ? 1 : 0);
        initial = AddPatrol(initial, "PATROL2", branch == "opponent" ? "P2" : "P1");
        if (branch == "hand") initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Base = initial.PlayerZones["P1"].Base.Where(id => id != "PATROL2").ToArray(),
                    Hand = [..initial.PlayerZones["P1"].Hand, "PATROL2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["PATROL2"] = new("P1", "HAND") } };
        var attack = await Attack(initial);
        Assert.Equal(1, Assert.Single(attack.State.StackItems).FieldContext!.Disarm!.Contributions.Sum(c => c.Amount));
        var done = await AttackDamageDistributionTests.Top((await AttackDamageDistributionTests.Confirm(attack.State, "ENEMY1")).State);
        Assert.Equal(2, Might(done.State)); Record("ineligible-" + branch, "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task DestroyingAGrantingPatrolAfterConfirmationKeepsTheAlreadyCreatedTotal()
    {
        var initial = AttackDamageDistributionTests.Spell(AddPatrol(await Position("RAD·055/167", 0), "PATROL2"),
            "SOUL", "SFD·163/221", "P1");
        var confirmed = await AttackDamageDistributionTests.Confirm((await Attack(initial)).State, "ENEMY1");
        var removed = await TopWithReceipt((await Act(confirmed.State, "P1",
            new PlayCardCommand("SOUL", "SFD·163/221", ["PATROL2", "HOST"]))).State, "grant-departure");
        Assert.Contains("PATROL2", removed.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, Assert.Single(removed.State.StackItems).FieldContext!.Disarm!.Contributions.Sum(c => c.Amount));
        Record("grant-departure", "response", removed.State, removed.Events);
        var done = await AttackDamageDistributionTests.Top(removed.State);
        Assert.Equal(1, Might(done.State)); Assert.Equal(0, done.State.CardObjects["ENEMY1"].Damage);
        Record("grant-departure", "done", done.State, done.Events);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
