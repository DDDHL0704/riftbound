using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ConditionalEquipmentDeathObserverTests
{
    private const string Shrine = "OGN·072/298";
    private const string Helmet = "OGN·228/298";

    private static MatchState ShrinePosition(string actor = "P1", string victimController = "P2", bool exhausted = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", Shrine, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "VICTIM", "UNL-004/219", victimController,
            "BATTLEFIELD", victimController == "P1" ? "BF" : "OTHER");
        state = RadGearTestPosition.Put(state, "STUN", "OGN·050/298", actor, "HAND");
        state = RadGearTestPosition.Put(state, "DAMAGE", "OGS·003/024", actor, "HAND");
        return state with { TurnPlayerId = actor, ActivePlayerId = actor,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = exhausted } } };
    }

    private static async Task Kill(Driver game, string actor, bool stun = true)
    {
        if (stun) {
            await game.Act(actor, new PlayCardCommand("STUN", "OGN·050/298", ["VICTIM"]));
            await game.Top();
        }
        await game.Act(actor, new PlayCardCommand("DAMAGE", "OGS·003/024", ["VICTIM"]));
        await game.Top();
    }

    private static async Task Order(Driver game, string actor)
    {
        if (game.State.TriggerQueue.Count > 0)
            await game.Act(actor, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static DeathObserverContext[] Captured(MatchState state)
        => state.StackItems.Select(item => item.DeathObserver).Concat(state.TriggerQueue.Select(item => item.DeathObserver))
            .Where(context => context is not null).Cast<DeathObserverContext>().ToArray();

    private static async Task PayShrine(Driver game, string player, bool pay)
    {
        await Order(game, player);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(player, choice.PlayerId);
        Assert.Contains("GEAR", choice.LegalObjectIds);
        await game.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["GEAR"] : []));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ShrineExhaustsOnlyWhenItsControllerAcceptsAndDrawsAfterResponses(bool pay)
    {
        var game = new Driver(ShrinePosition());
        await Kill(game, "P1");
        var context = Assert.Single(Captured(game.State));
        Assert.True(context.Destroyed.WasStunned);
        Assert.Equal("P2", context.Destroyed.ControllerId);
        Assert.Equal("P1", context.Attribution!.ResponsiblePlayerId);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await PayShrine(game, "P1", pay);
        Assert.Equal(pay, game.State.CardObjects["GEAR"].IsExhausted);
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
        if (pay) await game.Top();
        Assert.Equal(hand + (pay ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "P2", false)]
    [InlineData("P1", "P1", true)]
    [InlineData("P2", "P2", true)]
    public async Task ShrineDoesNotObserveAnUnstunnedFriendlyOrOtherPlayersKill(string actor, string victim, bool stun)
    {
        var game = new Driver(ShrinePosition(actor, victim));
        await Kill(game, actor, stun);
        Assert.Empty(Captured(game.State));
        Assert.Null(game.State.PendingCardChoice);
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Fact]
    public async Task AnExhaustedShrineCannotPayAndItsOptionalDrawIsDiscarded()
    {
        var game = new Driver(ShrinePosition(exhausted: true));
        await Kill(game, "P1");
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Fact]
    public async Task ShrineFollowsItsCurrentControllerInsteadOfItsOwner()
    {
        var initial = RadUnitEffectTestFixture.Place(ShrinePosition("P2", "P1"), "GEAR", "P2", "BASE");
        var game = new Driver(initial);
        await Kill(game, "P2");
        Assert.Equal("P1", game.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("P2", Assert.Single(Captured(game.State)).Attribution!.ResponsiblePlayerId);
        await PayShrine(game, "P2", true);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P2"].Hand);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task NamedUnitDamageBelongsToItsControllerRatherThanTheDuelCaster()
    {
        var initial = RadGearTestPosition.Put(ShrinePosition("P2"), "DUEL", "OGN·128/298", "P2", "HAND");
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("STUN", "OGN·050/298", ["VICTIM"]));
        await game.Top();
        await game.Act("P2", new PlayCardCommand("DUEL", "OGN·128/298", ["VICTIM", "ALLY"]));
        await game.Top();
        var context = Assert.Single(Captured(game.State));
        Assert.Equal("P1", context.Attribution!.ResponsiblePlayerId);
        Assert.False(context.Attribution.FromSpell);
        await PayShrine(game, "P1", true);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ThePaidShrineDrawSurvivesTheAnimatedSourceReturningInResponse()
    {
        var initial = RadGearTestPosition.Put(ShrinePosition(), "RETURN", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "LEGEND", "RAD·145/167", "P1", "BASE");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Base = initial.PlayerZones["P1"].Base.Where(id => id != "LEGEND").ToArray(), LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        initial = OriannaEquipmentConsumerFixture.WithResources(initial);
        initial = RadGearTestPosition.Put(initial, "EXTRA-ORANGE", "UNL-R04", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "EXTRA-PURPLE", "UNL-R05", "P1", "BASE");
        var game = new Driver(initial);
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "GEAR");
        await game.Act("P1", new RecycleRuneCommand("EXTRA-ORANGE", ["EXTRA-ORANGE", "EXTRA-PURPLE"]));
        await Kill(game, "P1");
        await PayShrine(game, "P1", true);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["GEAR"]));
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].LegendEquipmentUnit);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    private static MatchState HelmetPosition(bool boon = true, bool friendly = true, bool two = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", Helmet, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "AOE", "OGN·133/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RECIPIENT2", "OGN·219/298", "P1", "BASE");
        foreach (var id in two ? new[] { "VICTIM", "VICTIM2" } : ["VICTIM"]) {
            var controller = friendly ? "P1" : "P2";
            state = RadGearTestPosition.Put(state, id, "UNL-004/219", controller, "BATTLEFIELD", friendly ? "BF" : "OTHER");
            if (!boon) continue;
            Assert.True(BoonRules.TryGrant(state.CardObjects[id], state.PlayerZones, state.CardObjects, out var unit));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = unit with { Damage = 1 } } };
        }
        return state;
    }

    private static async Task ChooseHelmet(Driver game, string id)
    {
        await Order(game, "P1");
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }

    [Fact]
    public async Task HelmetRequiresABoonedFriendlyDeathAndGrantsAnotherUnitARealBoon()
    {
        var game = new Driver(HelmetPosition());
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
        Assert.True(Assert.Single(Captured(game.State)).DestroyedHadBoon);
        await ChooseHelmet(game, "ALLY");
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ALLY"].BoonCount);
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, true)] [InlineData(true, false)]
    public async Task HelmetDoesNotObserveAnUnboonedOrEnemyUnit(bool boon, bool friendly)
    {
        var game = new Driver(HelmetPosition(boon, friendly));
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
        Assert.Empty(Captured(game.State));
        Assert.Equal(0, game.State.CardObjects["ALLY"].BoonCount);
        await game.Replay();
    }

    [Fact]
    public async Task TwoSimultaneousBoonedDeathsEarnTwoIndependentTargetedHelmetSkills()
    {
        var game = new Driver(HelmetPosition(two: true));
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
        Assert.Equal(2, Captured(game.State).Length);
        var recipients = new Queue<string>(["ALLY", "RECIPIENT2"]);
        for (var steps = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0; steps++) {
            Assert.True(steps < 10);
            if (game.State.TriggerQueue.Count > 0) await Order(game, "P1");
            else if (game.State.PendingCardChoice is not null) await ChooseHelmet(game, recipients.Dequeue());
            else await game.Top();
        }
        Assert.Empty(recipients);
        Assert.Equal(1, game.State.CardObjects["ALLY"].BoonCount);
        Assert.Equal(1, game.State.CardObjects["RECIPIENT2"].BoonCount);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "TRIGGER_RESOLVED"
            && Equals(entry.Event.Payload.GetValueOrDefault("observerKind"), TriggerKinds.FriendlyBoonedUnitDestroyedBoonOther)));
        await game.Replay();
    }

    [Fact]
    public async Task HelmetUsesTheDeadUnitsControllerAndKeepsItsDifferentOwner()
    {
        var initial = HelmetPosition();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["VICTIM"] = initial.CardObjects["VICTIM"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
        var context = Assert.Single(Captured(game.State));
        Assert.Equal("P1", context.Destroyed.ControllerId);
        Assert.Equal("P2", context.Destroyed.OwnerId);
        Assert.Contains("VICTIM", game.State.PlayerZones["P2"].Graveyard);
        await ChooseHelmet(game, "ALLY");
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["ALLY"].BoonCount);
        await game.Replay();
    }

    [Fact]
    public async Task HelmetCannotBoonTheNewIncarnationOfATargetReturnedInResponse()
    {
        var game = new Driver(RadGearTestPosition.Put(HelmetPosition(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
        await ChooseHelmet(game, "ALLY");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(0, game.State.CardObjects["ALLY"].BoonCount);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "BOON_GRANTED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecoveryRejectsForgedHelmetBoonConditions(bool extraAttribution)
    {
        var game = new Driver(HelmetPosition());
        await game.Act("P1", new PlayCardCommand("AOE", "OGN·133/298", []));
        await game.Top();
        await Order(game, "P1");
        var item = Assert.Single(game.State.StackItems); var context = item.DeathObserver!;
        var changed = extraAttribution ? context with {
            Attribution = new(new("AOE", 0), "OGN·133/298", "P1", "FORGED", "P1", true)
        } : context with { DestroyedHadBoon = false };
        var forged = game.State with { StackItems = [item with { DeathObserver = changed }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await game.Replay();
    }

    [Theory]
    [InlineData("stun")] [InlineData("responsible")] [InlineData("attribution")] [InlineData("extra-boon")]
    public async Task RecoveryRejectsForgedShrineDeathConditions(string forgery)
    {
        var game = new Driver(ShrinePosition());
        await Kill(game, "P1");
        await Order(game, "P1");
        var item = Assert.Single(game.State.StackItems); var context = item.DeathObserver!;
        var changed = forgery switch {
            "stun" => context with { Destroyed = context.Destroyed with { WasStunned = false } },
            "responsible" => context with { Attribution = context.Attribution! with { ResponsiblePlayerId = "P2" } },
            "attribution" => context with { Attribution = null },
            _ => context with { DestroyedHadBoon = true } };
        var forged = game.State with { StackItems = [item with { DeathObserver = changed }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await game.Replay();
    }
}
