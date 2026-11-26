using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RavenbloomDuelTests
{
    private const string Duel = "OGN·128/298";
    private const string Student = "OGN·103/298";
    private const string Retreat = "OGN·104/298";
    private const string Meditation = "OGN·048/298";

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task DuelDealsDamageBeforeTheStudentsRespondablePlayTrigger(bool spellDuel, bool onBattlefield, bool targetsStudent)
    {
        var game = new Driver(Position(onBattlefield)) { RecoverAfterEveryCommand = false };
        if (spellDuel)
            await game.Act("P1", new MoveUnitCommand("MOVER", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal(spellDuel ? PromptTypes.SpellDuelFocus : PromptTypes.MainAction, game.PromptFor("P1").View?.Type);

        var friendly = targetsStudent ? "UNIT" : "ALLY";
        await game.PlayQuoted("P1", new("DUEL", Duel, [friendly, "ENEMY"]));
        Assert.Equal("DUEL", Assert.Single(game.State.StackItems).SourceObjectId);
        Assert.Equal(2, StudentPower(game, "P1"));
        Assert.Equal(2, StudentPower(game, "P2"));

        // CN 420.4.a: the spell must finish resolving before it earns this trigger.
        await game.Top();
        AssertDuelDamage(game, friendly, targetsStudent ? 2 : 4);
        Assert.Equal(1, game.State.CardObjects[friendly].Damage);
        Assert.Equal(2, StudentPower(game, "P1"));
        AssertStudentTrigger(game);
        await game.ColdCheckpoint();

        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(3, StudentPower(game, "P1"));
        Assert.Equal(3, StudentPower(game, "P2"));
        var events = game.Events();
        var damage = Assert.Single(events, e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals("ENEMY", e.Event.Payload["targetObjectId"]));
        var trigger = Assert.Single(events, e => e.Event.Kind == "TRIGGER_RESOLVED"
            && Equals("UNIT", e.Event.Payload["sourceObjectId"]));
        Assert.True(damage.Sequence < trigger.Sequence);

        if (spellDuel)
        {
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.Equal(3, StudentPower(game, "P1"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(2, StudentPower(game, "P1"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DuelStillEarnsAStudentTriggerWhenAResponseRemovesTheTarget()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(onBattlefield: false), "RETREAT", Retreat, "P2");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.PlayQuoted("P1", new("DUEL", Duel, ["UNIT", "ENEMY"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.PlayQuoted("P2", new("RETREAT", Retreat, ["ENEMY"]));
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.Equal("DUEL", Assert.Single(game.State.StackItems).SourceObjectId);
        await game.Top();
        // CN 359.3.e.10: a spell still completes its play when its instructions have no effect.
        Assert.Contains("DUEL", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        AssertStudentTrigger(game);
        await game.Top();
        Assert.Equal(3, StudentPower(game, "P1"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DuelReadsTheStudentsMightAtResolutionAfterAnotherSpellCompletes()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(onBattlefield: false), "MEDITATION", Meditation, "P1");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.PlayQuoted("P1", new("DUEL", Duel, ["UNIT", "ENEMY"]));
        await game.PlayQuoted("P1", new("MEDITATION", Meditation, []));
        await game.Top();
        Assert.Equal("DUEL", game.State.StackItems[0].SourceObjectId);
        Assert.Equal("MEDITATION", game.State.StackItems[^1].SpellContext?.PlayedObjectId);
        await game.Top();
        Assert.Equal(3, StudentPower(game, "P1"));
        await game.Top();
        AssertDuelDamage(game, "UNIT", 3);
        AssertStudentTrigger(game);
        await game.Top();
        Assert.Equal(4, StudentPower(game, "P1"));
        await game.ColdCheckpoint();
    }

    private static void AssertDuelDamage(Driver game, string friendly, int damage)
    {
        var hits = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals("DUEL", e.Event.Payload["sourceObjectId"])).Select(e => e.Event.Payload).ToArray();
        Assert.Collection(hits, hit =>
        {
            Assert.Equal(friendly, hit["damageSourceObjectId"]);
            Assert.Equal("ENEMY", hit["targetObjectId"]);
            Assert.Equal(damage, hit["damage"]);
        }, hit =>
        {
            Assert.Equal("ENEMY", hit["damageSourceObjectId"]);
            Assert.Equal(friendly, hit["targetObjectId"]);
            Assert.Equal(1, hit["damage"]);
        });
    }

    private static void AssertStudentTrigger(Driver game)
    {
        var trigger = Assert.Single(game.State.StackItems);
        Assert.Equal("UNIT", trigger.SourceObjectId);
        Assert.Equal(TriggerKinds.UnitSpellPlayedPowerModifier, trigger.SpellContext?.Kind);
        Assert.Equal("DUEL", trigger.SpellContext?.PlayedObjectId);
        Assert.Equal("UNIT", JsonSerializer.SerializeToElement(Assert.Single(game.SnapshotFor("P2").Stack))
            .GetProperty("sourceObjectId").GetString());
        Assert.Equal(PromptTypes.StackPriority, game.PromptFor(game.State.PriorityPlayerId!).View?.Type);
    }

    private static int StudentPower(Driver game, string viewer)
        => JsonSerializer.SerializeToElement(game.SnapshotFor(viewer).Players["P1"])
            .GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();

    private static MatchState Position(bool onBattlefield)
    {
        var state = VenUnitEntryBasicTests.Position(Student);
        var cards = state.CardObjects.ToDictionary();
        cards["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "UNL-093/219", "P2");
        cards["MOVER"] = cards["ALLY"] with { ObjectId = "MOVER" };
        cards["OTHER"] = cards["OTHER"] with { ControllerId = null };
        var zones = state.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Hand = [], Base = [.. zones["P1"].Base, "MOVER"] };
        var locations = state.ObjectLocations.ToDictionary();
        locations["MOVER"] = new("P1", "BASE");
        state = state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        state = LocalDestructionRecallTests.At(state, "UNIT", "P1", onBattlefield ? "BATTLEFIELD" : "BASE", onBattlefield ? "BF" : null);
        return AttackDamageDistributionTests.Spell(state, "DUEL", Duel, "P1");
    }
}
