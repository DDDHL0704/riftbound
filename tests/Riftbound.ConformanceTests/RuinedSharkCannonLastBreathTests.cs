using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RuinedSharkCannonLastBreathTests
{
    private const string Shark = "UNL-067/219", Kill = "OGN·229/298";
    private static MatchState Position(string zone = "BASE", bool stolen = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "OTHER", "OGN·275/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "SHARK", Shark, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "TARGET", "OGN·088/298", "P2", zone, zone == "BATTLEFIELD" ? "OTHER" : null);
        state = RadGearTestPosition.Put(state, "KILL", Kill, "P1", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SHARK"] = state.CardObjects["SHARK"] with { OwnerId = stolen ? "P2" : "P1" } } };
    }
    private static async Task Destroy(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["SHARK"])); await game.Top();
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
    }
    private static Task Confirm(Driver game, string target)
    {
        var choice = game.State.PendingCardChoice!;
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
    }

    [Theory]
    [InlineData("BASE", false)] [InlineData("BATTLEFIELD", false)]
    [InlineData("BASE", true)] [InlineData("BATTLEFIELD", true)]
    public async Task TheDestroyedSourcesCurrentControllerConfirmsAnyEnemyUnitThenDealsFourAfterResponses(string zone, bool stolen)
    {
        var game = new Driver(Position(zone, stolen)); await Destroy(game);
        Assert.Contains("SHARK", game.State.PlayerZones[stolen ? "P2" : "P1"].Graveyard);
        Assert.Contains("TARGET", game.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("ALLY", game.State.PendingCardChoice.LegalObjectIds);
        Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
        await Confirm(game, "TARGET"); Assert.Single(game.State.StackItems);
        Assert.Equal(0, game.State.CardObjects["TARGET"].Damage);
        await game.Top(); Assert.Equal(4, game.State.CardObjects["TARGET"].Damage);
        Assert.Contains("TARGET", zone == "BASE" ? game.State.PlayerZones["P2"].Base : game.State.PlayerZones["P2"].Battlefields);
        await game.Replay();
    }

    [Fact]
    public async Task FriendlyOrMultipleTargetsAreAtomicRejections()
    {
        var game = new Driver(Position()); await Destroy(game); var choice = game.State.PendingCardChoice!;
        foreach (var ids in new[] { new[] { "ALLY" }, new[] { "TARGET", "ENEMY" } })
            await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["TARGET"]));
        await Confirm(game, "TARGET"); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AReturnedEnemyIsANewObjectAndCannotReceiveTheConfirmedDamage()
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P2", "HAND");
        var game = new Driver(initial); await Destroy(game); await Confirm(game, "TARGET");
        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["TARGET"])); await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Hand);
        var prefix = game.Events().Length; await game.Top();
        Assert.DoesNotContain(game.Events().Skip(prefix), entry => entry.Event.Kind == "DAMAGE_APPLIED");
        await game.Replay();
    }

    [Fact]
    public async Task ActualCopiedTextAndALocalAmplifierEarnFourSeparateLastBreaths()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "SHARK", "P1", "BATTLEFIELD", "BF");
        initial = LastBreathRepetitionTests.AddAmplifier(initial, "AMP", "P1", "BF");
        initial = RadGearTestPosition.Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "SHARK", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["SHARK"])); await game.Top();
        var contexts = game.State.TriggerQueue.Select(trigger => trigger.UnitEvent)
            .Concat(game.State.StackItems.Select(item => item.UnitEvent)).OfType<UnitEventContext>().Where(context => context.CardNo == Shark).ToArray();
        Assert.Equal(4, contexts.Length); Assert.Equal(2, contexts.Count(context => context.Origin is not null));
        Assert.All(contexts, context => Assert.NotNull(context.ArmamentDeath!.Repetition)); await game.Replay();
    }

    [Theory]
    [InlineData("death")] [InlineData("erase")]
    public async Task RecoveryBindsTheIndependentSkillToTheOriginalAcceptedDeath(string change)
    {
        var game = new Driver(Position()); await Destroy(game); await Confirm(game, "TARGET");
        var skill = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = change == "erase" ? [] : [skill with {
            UnitEvent = skill.UnitEvent! with { ArmamentDeath = null } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Top(); await game.Replay();
    }
}
