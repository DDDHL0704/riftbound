using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PowerDamageDetachTests
{
    private const string Spell = "SFD·107/221", Weapon = "SFD·108/221";

    private static MatchState Position(bool two = false)
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", Spell, "P1", "HAND");
        s = RadGearTestPosition.Put(s, "GEAR", Weapon, "P1", "BASE");
        s = RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
        s = RadGearTestPosition.Put(s, "ENEMY", "OGN·034/298", "P2", "BASE");
        return two ? RadGearTestPosition.Put(s, "SECOND", Weapon, "P1", "BASE") : s;
    }

    private static async Task Attach(Driver game, string gear)
    {
        await game.Act("P1", new AssembleEquipmentCommand(gear, "ALLY", ["ASSEMBLE_ORANGE"]));
        await game.Top();
    }

    [Fact]
    public async Task AnUnarmedFirstTargetIsRejectedByTheSharedPreviewAndPlay()
    {
        var s = Position(); var command = new PlayCardCommand("SPELL", Spell, ["ALLY", "ENEMY"]);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command)).IsValid);
        await RadGearTestPosition.Reject(s, "P1", command);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DamageUsesCurrentArmedMightBeforeExactlyOneWeaponIsDetached(bool two)
    {
        var game = new Driver(Position(two)); var bare = game.State.CardObjects["ALLY"].Power;
        await Attach(game, "GEAR"); if (two) await Attach(game, "SECOND");
        var expected = bare + (two ? 2 : 1);
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY", "ENEMY"])); await game.Top();
        if (two)
        {
            var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("DETACH_ARMAMENT", pending.Request.Kind);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            Assert.Equal(expected, pending.Request.Preview!.CardObjects["ENEMY"].Damage);
            await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SECOND"]));
            await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["ENEMY"]));
            await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["SECOND"]));
        }
        Assert.Equal(expected, game.State.CardObjects["ENEMY"].Damage);
        Assert.Null(game.State.CardObjects[two ? "SECOND" : "GEAR"].AttachedToObjectId);
        if (two) Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        var events = game.Events().Where(e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")).ToArray();
        Assert.Single(events, e => e.Event.Kind == "DAMAGE_APPLIED"); Assert.Single(events, e => e.Event.Kind == "EQUIPMENT_DETACHED");
        Assert.True(Array.FindIndex(events, e => e.Event.Kind == "DAMAGE_APPLIED")
            < Array.FindIndex(events, e => e.Event.Kind == "EQUIPMENT_DETACHED"));
        await game.Replay();
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("ENEMY")]
    public async Task LosingOneTargetDoesNotReassignTheReferenceOrCancelAnIndependentDetach(string returned)
    {
        var state = returned == "ENEMY" ? RadGearTestPosition.Put(Position(), "OPPONENT-RETURN", "OGN·104/298", "P2", "HAND") : Position();
        var game = new Driver(state); await Attach(game, "GEAR");
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY", "ENEMY"]));
        if (returned == "ENEMY") await game.Act("P1", new PassPriorityCommand());
        await game.Act(returned == "ENEMY" ? "P2" : "P1", new PlayCardCommand(returned == "ENEMY" ? "OPPONENT-RETURN" : "RETURN", "OGN·104/298", [returned]));
        await game.Top(); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(returned == "ENEMY" ? 1 : 0, game.Events().Count(e => e.Event.Kind == "EQUIPMENT_DETACHED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")));
        await game.Replay();
    }

    [Fact]
    public async Task ReferencingAUnitsPowerStillDealsSpellDamageAndStillDetachesWhenPrevented()
    {
        var state = RadGearTestPosition.Put(Position(), "ENEMY", "VEN·025", "P2", "BASE");
        for (var i = 0; i < 7; i++) state = RadGearTestPosition.Put(state, "RUNE" + i, "OGN·007/298", "P2", "BASE");
        var game = new Driver(state); await Attach(game, "GEAR");
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY", "ENEMY"])); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId); await game.Replay();
    }

    // SFD judge FAQ 260114, p19: taking off the last weapon makes the first
    // target illegal; another weapon keeps it armed and supplies current might.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualTakeUpResponseRechecksWhetherTheFirstTargetRemainsArmed(bool two)
    {
        var state = RadGearTestPosition.Put(Position(two), "DETACH", "SFD·011/221", "P2", "HAND");
        var game = new Driver(state); await Attach(game, "GEAR");
        if (two) await Attach(game, "SECOND");
        var expected = two ? game.State.CardObjects["ALLY"].Power + 1 : 0;
        await game.Act("P1", new PlayCardCommand("SPELL", Spell, ["ALLY", "ENEMY"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("DETACH", "SFD·011/221", ["ALLY", "GEAR"]));
        await game.Top(); await game.Top();
        Assert.Equal(expected, game.State.CardObjects["ENEMY"].Damage);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        if (two) Assert.Null(game.State.CardObjects["SECOND"].AttachedToObjectId);
        Assert.Equal(two ? 1 : 0, game.Events().Count(e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")));
        await game.Replay();
    }
}
