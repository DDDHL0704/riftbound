using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DestructionGraveyardFamilyTests
{
    internal const string Phoenix = "OGN·037/298";
    internal const string Decree = "OGN·221/298";
    internal const string Guillotine = "OGN·254/298";
    internal const string Ray = "OGN·009/298";

    [Theory]
    [InlineData(Phoenix, TriggerKinds.UnitSpellDestroyPlayFromGraveyard)]
    [InlineData(Decree, TriggerKinds.SpellTurnUnitDamagedDestroy)]
    [InlineData(Guillotine, TriggerKinds.SpellNextDamageOrInspiredDestroy)]
    public async Task WholePrintedInstructionsParseAsFiniteSharedConsumers(string cardNo, string kind)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var printed = catalog.Cards.Single(c => c.CardNo == cardNo);
        var spec = Assert.Single(RuleTextParser.Parse(printed).Triggers);
        Assert.Equal(kind, spec.Kind);
        if (cardNo == Phoenix) { Assert.Equal(1, spec.ManaCost); Assert.Equal(1, spec.PowerCost); Assert.Equal("red", spec.PowerCostTrait); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualLethalSpellAndSelfDestructionOfferLeadingCostThenPlayOriginalPhoenix(bool self)
    {
        var initial = Position(self ? "BATTLEFIELD" : "GRAVEYARD", smallTarget: true);
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("RAY", Ray, [self ? "PHOENIX" : "TARGET"]));
        var killed = await run.Top();
        var death = Assert.Single(killed.Events, e => e.Kind == "UNIT_DESTROYED");
        var attribution = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
        Assert.True(attribution.FromSpell); Assert.Equal(Ray, attribution.CardNo); Assert.Equal("P1", attribution.ResponsiblePlayerId);
        Assert.Contains("PHOENIX", run.State.PlayerZones["P1"].Graveyard);
        var captured = Assert.Single(run.State.StackItems);
        Assert.NotNull(captured.GraveyardPlay); Assert.Equal(self ? 1 : 0, captured.GraveyardPlay.Source.Generation);
        await PayPhoenix(run);
        await run.Top();
        Assert.NotNull(run.State.PendingEffectPlay); Assert.Empty(run.State.StackItems);
        var pool = run.State.RunePools["P1"];
        await run.Act("P1", new PlayCardCommand("PHOENIX", Phoenix, []));
        Assert.Equal(pool, run.State.RunePools["P1"]);
        if (run.State.StackItems.Count > 0) await run.Top();
        Assert.Contains("PHOENIX", run.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("PHOENIX", run.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(self ? 2 : 1, run.State.CardObjects["PHOENIX"].ObjectGeneration);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task DecreeDamageQueuesIndependentDestroyBeforePhoenixWithOriginalSpellSource()
    {
        var initial = Position(); var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        Assert.Single(run.State.UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_DESTRUCTION|", StringComparison.Ordinal));
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"]));
        var damaged = await run.Top();
        Assert.DoesNotContain(damaged.Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Equal(3, run.State.CardObjects["TARGET"].Damage);
        Assert.Null(run.State.PendingPayment);
        var delayed = Assert.Single(run.State.StackItems);
        Assert.Equal(Decree, delayed.CardNo); Assert.Equal("DECREE", delayed.SourceObjectId);
        Assert.Equal(new ObjectBinding("TARGET", 0), delayed.DamageDestruction!.DamagedUnit);
        var destroyed = await run.Top();
        var death = Assert.Single(destroyed.Events, e => e.Kind == "UNIT_DESTROYED");
        var cause = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
        Assert.Equal(Decree, cause.CardNo); Assert.Equal("DECREE", cause.Source.ObjectId); Assert.True(cause.FromSpell);
        Assert.Equal(1, cause.Source.Generation); Assert.NotNull(run.State.PendingPayment);
        await PayPhoenix(run, false);
        Assert.Empty(run.State.StackItems); Assert.Contains("PHOENIX", run.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuillotineCapturesInspireAtConfirmationOrNextDamageCreatesIndependentAbility(bool inspired)
    {
        var initial = Position(); var run = new StandbyDefendObserverTests.Driver(initial);
        if (inspired) { await run.Act("P1", new PlayCardCommand("CHEAP", "OGN·210/298", [])); if (run.State.StackItems.Count > 0) await run.Top(); }
        await run.Act("P1", new PlayCardCommand("GUILLOTINE", Guillotine, ["TARGET"]));
        Assert.Equal(inspired, run.State.StackItems.Single().PlayedAfterAnotherCardThisTurn);
        var resolved = await run.Top();
        if (!inspired)
        {
            Assert.DoesNotContain(resolved.Events, e => e.Kind == "UNIT_DESTROYED");
            await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"]));
            var damage = await run.Top();
            Assert.DoesNotContain(damage.Events, e => e.Kind == "UNIT_DESTROYED");
            Assert.Equal(Guillotine, Assert.Single(run.State.StackItems).DamageDestruction!.CardNo);
            resolved = await run.Top();
        }
        var death = Assert.Single(resolved.Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Equal(Guillotine, Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]).CardNo);
        Assert.DoesNotContain("TARGET", run.State.PlayerZones["P2"].Base);
        await PayPhoenix(run, false);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    internal static async Task PayPhoenix(StandbyDefendObserverTests.Driver run, bool pay = true)
    {
        var cost = Assert.IsType<PendingPaymentState>(run.State.PendingPayment);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", cost.PaymentWindow);
        Assert.Equal(1, cost.ManaCost); Assert.Equal(1, cost.PowerCost); Assert.Equal(1, cost.PowerCostByTrait["red"]);
        Assert.Null(run.State.PriorityPlayerId);
        var before = run.State.RunePools["P1"];
        await run.Act("P1", new PayCostCommand(cost.PaymentId, cost.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        Assert.Equal(before.Mana - (pay ? 1 : 0), run.State.RunePools["P1"].Mana);
        Assert.Equal(before.PowerByTrait["red"] - (pay ? 1 : 0), run.State.RunePools["P1"].PowerByTrait["red"]);
        if (pay) { Assert.NotNull(run.State.PriorityPlayerId); Assert.NotNull(Assert.Single(run.State.StackItems).TriggerCost); }
    }

    internal static MatchState Position(string phoenixZone = "GRAVEYARD", bool smallTarget = false)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Add(string id, string no, string owner, string zone) {
            cards[id] = HweiMoveDiscardTypesTests.Card(id, no, owner); locations[id] = new(owner, zone, zone == "BATTLEFIELD" ? owner == "P1" ? "F" : "OTHER" : null); }
        Add("PHOENIX", Phoenix, "P1", phoenixZone);
        Add("TARGET", smallTarget ? "OGN·210/298" : "UNL-118/219", "P2", "BATTLEFIELD");
        Add("RAY", Ray, "P1", "HAND"); Add("RAY2", Ray, "P1", "HAND");
        Add("DECREE", Decree, "P1", "HAND"); Add("GUILLOTINE", Guillotine, "P1", "HAND");
        Add("CHEAP", "OGN·210/298", "P1", "HAND");
        foreach (var (id, owner, no) in new[] { ("F", "P1", "OGN·278/298"), ("OTHER", "P2", "OGN·294/298") }) {
            cards[id] = HweiMoveDiscardTypesTests.Card(id, no, owner); locations[id] = new(owner, "BATTLEFIELD", id); }
        var zones = new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with {
            Hand = ["RAY", "RAY2", "DECREE", "GUILLOTINE", "CHEAP"],
            Base = phoenixZone == "BASE" ? ["PHOENIX"] : [], Graveyard = phoenixZone == "GRAVEYARD" ? ["PHOENIX"] : [], Battlefields = phoenixZone == "BATTLEFIELD" ? ["F", "PHOENIX"] : ["F"] },
            ["P2"] = PlayerZones.Empty with { Base = [], Battlefields = ["OTHER", "TARGET"] } };
        foreach (var owner in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(0, 12).Select(i => owner + "-DECK-" + i).ToArray();
            foreach (var id in deck) Add(id, "OGN·096/298", owner, "MAIN_DECK");
            zones[owner] = zones[owner] with { MainDeck = deck };
        }
        return new("CONTROLLED-DESTRUCTION-GRAVEYARD", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            cardObjects: cards, objectLocations: locations, playerZones: zones,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30, new Dictionary<string, int> { ["red"] = 20, ["yellow"] = 10 }),
                ["P2"] = new(30, 30, new Dictionary<string, int> { ["red"] = 20, ["orange"] = 10 }) }) { CardPlays = new(5, [], []) };
    }
}
