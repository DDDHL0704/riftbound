using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.DestructionGraveyardFamilyTests;

namespace Riftbound.ConformanceTests;

public sealed class DestructionGraveyardLifecycleTests
{
    [Fact]
    public async Task GuillotineConfirmedWithoutInspireCannotUpgradeOrTriggerRetroactivelyAfterAResponseSpell()
    {
        var initial = Add(Position(), "RESPONSE-DAMAGE", "OGN·033/298", "P1", "HAND"); var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("GUILLOTINE", Guillotine, ["TARGET"]));
        Assert.False(Assert.Single(run.State.StackItems).PlayedAfterAnotherCardThisTurn);
        await run.Act("P1", new PlayCardCommand("RESPONSE-DAMAGE", "OGN·033/298", ["TARGET"])); await run.Top();
        var choice = run.State.PendingCardChoice!;
        await run.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(6, run.State.CardObjects["TARGET"].Damage);
        Assert.Null(Assert.Single(run.State.StackItems).DamageDestruction);
        await run.Top();
        Assert.Equal(6, run.State.CardObjects["TARGET"].Damage); Assert.Empty(run.State.StackItems);
        Assert.Single(run.State.UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_DESTRUCTION|", StringComparison.Ordinal));
        await run.Act("P1", new PlayCardCommand("RAY2", Ray, ["TARGET"])); await run.Top();
        Assert.Equal(Guillotine, Assert.Single(run.State.StackItems).DamageDestruction!.CardNo);
        await run.Top(); await PayPhoenix(run, false);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task OpponentsDamageTriggersOriginalDecreeControllerAndOnlyThatPlayersPhoenix()
    {
        var initial = Add(Add(Add(Position(), "WAITING-SPELL", "OGN·083/298", "P1", "HAND"), "OPPONENT-DAMAGE", "OGN·033/298", "P2", "HAND"),
            "OPPONENT-PHOENIX", Phoenix, "P2", "GRAVEYARD");
        initial = Add(initial, "OWN-ELDER", "UNL-118/219", "P1", "BASE");
        // The original fixture also has an opposing Ancient Dragon. Its static
        // lethal-damage permission would make the opponent's six damage lethal,
        // so it is absent from this explicitly nonlethal responsibility case.
        initial = initial with {
            CardObjects = initial.CardObjects.Where(p => p.Key != "TARGET").ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "TARGET").ToDictionary(),
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P2"
                ? p.Value with { Battlefields = p.Value.Battlefields.Where(id => id != "TARGET").ToArray() } : p.Value) };
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("WAITING-SPELL", "OGN·083/298", []));
        await run.Act("P1", new PassPriorityCommand());
        await run.Act("P2", new PlayCardCommand("OPPONENT-DAMAGE", "OGN·033/298", ["OWN-ELDER"])); await run.Top();
        var choice = run.State.PendingCardChoice!;
        var chosen = await run.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        var ability = run.State.StackItems.Last();
        Assert.True(ability.DamageDestruction is not null, System.Text.Json.JsonSerializer.Serialize(new {
            chosen.Events, run.State.UntilEndOfTurnEffects, run.State.StackItems, run.State.TriggerQueue }));
        Assert.Equal("P1", ability.ControllerId);
        var death = Assert.Single((await run.Top()).Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Equal("P1", death.Payload["destroyedByPlayerId"]);
        var c = run.State.StackItems.Last().GraveyardPlay!;
        Assert.Equal("P1", c.ControllerId); Assert.Equal("DECREE", c.Attribution.Source.ObjectId); Assert.Equal("P1", c.Attribution.ResponsiblePlayerId);
        Assert.DoesNotContain(run.State.TriggerQueue, t => t.GraveyardPlay?.ControllerId == "P2");
        Assert.DoesNotContain(run.State.StackItems, t => t.GraveyardPlay?.ControllerId == "P2");
        await PayPhoenix(run, false); await run.Top();
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task ActualCombatUnitDamageDoesNotBecomeSpellDestructionForGraveyardPhoenix()
    {
        var initial = Add(Position(), "ATTACKER", "OGN·210/298", "P1", "BASE");
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:OTHER"));
        await SacredProtectorTests.FinishCombat(run);
        var death = Assert.Single(run.Events(), e => e.Event.Kind == "UNIT_DESTROYED").Event;
        var cause = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
        Assert.False(cause.FromSpell); Assert.Equal("TARGET", cause.Source.ObjectId); Assert.Equal("P2", cause.ResponsiblePlayerId);
        Assert.Null(run.State.PendingPayment); Assert.Empty(run.State.TriggerQueue); Assert.Empty(run.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task ActualSimultaneousGraveyardEntrantsObserveOwnAndOtherSpellDestructions()
    {
        var initial = Add(Position("BATTLEFIELD"), "SECOND-PHOENIX", Phoenix, "P1", "BASE");
        initial = Add(initial, "RUINATION", "UNL-180/219", "P1", "HAND") with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(30, 0, new Dictionary<string, int> { ["yellow"] = 3, ["red"] = 6 }), ["P2"] = RunePool.Empty } };
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("RUINATION", "UNL-180/219", [])); var destroyed = await run.Top();
        Assert.Equal(3, destroyed.Events.Count(e => e.Kind == "UNIT_DESTROYED")); Assert.Equal(6, run.State.TriggerQueue.Count);
        foreach (var id in new[] { "PHOENIX", "SECOND-PHOENIX" }) {
            var earned = run.State.TriggerQueue.Where(t => t.SourceObjectId == id).ToArray(); Assert.Equal(3, earned.Length);
            Assert.All(earned, t => { Assert.Equal(new ObjectBinding(id, 1), t.GraveyardPlay!.Source); Assert.Equal("UNL-180/219", t.GraveyardPlay.Attribution.CardNo); });
            Assert.Equal(new[] { "PHOENIX", "SECOND-PHOENIX", "TARGET" }, earned.Select(t => t.GraveyardPlay!.Destroyed.ObjectId).Order(StringComparer.Ordinal));
        }
        await run.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        for (var i = 0; i < 6; i++) await PayPhoenix(run, false);
        Assert.Empty(run.State.StackItems); Assert.Null(run.State.PendingPayment);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task ActualDamagePreventionProducesNoDelayedTriggerAndNextRealDamageStillDoes()
    {
        var initial = Add(Position(), "COUNTERSTORM", "SFD·194/221", "P1", "HAND");
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("COUNTERSTORM", "SFD·194/221", ["TARGET"])); await run.Top();
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"])); var prevented = await run.Top();
        var damage = Assert.Single(prevented.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Equal(0, damage.Payload["damage"]); Assert.Equal(3, damage.Payload["preventedDamage"]);
        Assert.Equal(0, run.State.CardObjects["TARGET"].Damage); Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
        await run.Act("P1", new PlayCardCommand("RAY2", Ray, ["TARGET"])); await run.Top();
        Assert.Equal(Decree, Assert.Single(run.State.StackItems).DamageDestruction!.CardNo);
        await run.Top(); await PayPhoenix(run, false);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Theory]
    [InlineData("OGN·128/298")]
    [InlineData("OGS·008/024")]
    public async Task ActualNamedUnitDuelDamageRetainsUnitResponsibilityAndCannotTriggerPhoenix(string duel)
    {
        var initial = Add(Position(), "DUELLIST", "OGN·210/298", "P1", "BATTLEFIELD", "F");
        initial = Add(initial, "DUEL", duel, "P1", "HAND");
        initial = initial with { RunePools = initial.RunePools.ToDictionary(p => p.Key, p => p.Key == "P1"
            ? new RunePool(30, 30, new Dictionary<string, int> { ["red"] = 10, ["orange"] = 10, ["yellow"] = 10 }) : p.Value) };
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("DUEL", duel, ["DUELLIST", "TARGET"]));
        var resolved = await run.Top();
        var death = Assert.Single(resolved.Events, e => e.Kind == "UNIT_DESTROYED");
        var cause = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
        Assert.False(cause.FromSpell); Assert.Equal("TARGET", cause.Source.ObjectId);
        Assert.Equal("P2", cause.ResponsiblePlayerId); Assert.Equal("P2", death.Payload["destroyedByPlayerId"]);
        Assert.Null(run.State.PendingPayment); Assert.Empty(run.State.TriggerQueue); Assert.Empty(run.State.StackItems);
        Assert.Contains("PHOENIX", run.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task OpponentsActualLethalSpellDoesNotOfferYourGraveyardPhoenix()
    {
        var initial = Add(Position(), "VICTIM", "OGN·210/298", "P1", "BATTLEFIELD", "F");
        initial = Add(initial, "OPPONENT-RAY", Ray, "P2", "HAND") with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P2", new PlayCardCommand("OPPONENT-RAY", Ray, ["VICTIM"])); var killed = await run.Top();
        Assert.Equal("P2", Assert.IsType<DestructionAttribution>(Assert.Single(killed.Events, e => e.Kind == "UNIT_DESTROYED").Payload["destructionAttribution"]).ResponsiblePlayerId);
        Assert.Null(run.State.PendingPayment); Assert.Empty(run.State.StackItems); Assert.Empty(run.State.TriggerQueue);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task GlobalDecreeIncludesARealUnitPlayedAfterTheSpellResolved()
    {
        var initial = Add(Position(), "LATE", "OGN·010/298", "P1", "HAND");
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("LATE", "OGN·010/298", [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        await run.Act("P1", new MoveUnitCommand("LATE", "BASE", "BATTLEFIELD:F"));
        for (var i = 0; i < 4 && run.State.FocusPlayerId is { } focus; i++) await run.Act(focus, new PassFocusCommand());
        Assert.Contains("LATE", run.State.PlayerZones["P1"].Battlefields);
        var generation = run.State.CardObjects["LATE"].ObjectGeneration;
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["LATE"])); await run.Top();
        Assert.Equal(2, run.State.TriggerQueue.Count);
        var delayed = Assert.Single(run.State.TriggerQueue, t => t.DamageDestruction is not null);
        Assert.Equal(new ObjectBinding("LATE", generation), delayed.DamageDestruction!.DamagedUnit);
        Assert.Single(run.State.TriggerQueue, t => t.GraveyardPlay is not null);
        await run.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: run.State.TriggerQueue.OrderBy(t => t.GraveyardPlay is null ? 0 : 1).Select(t => t.TriggerId).ToArray()));
        // Payment confirmations are completed before either earned skill receives responses.
        await PayPhoenix(run, false); await run.Top();
        Assert.Equal(1, run.Events().Count(e => e.Event.Kind == "UNIT_DESTROYED"));
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoDecreesRetainBothOriginalSpellInstancesAndOnlyFirstAbilityCanDestroy(bool reverse)
    {
        var initial = Add(Position(), "SECOND-DECREE", Decree, "P1", "HAND");
        var run = new StandbyDefendObserverTests.Driver(initial);
        foreach (var id in new[] { "DECREE", "SECOND-DECREE" }) { await run.Act("P1", new PlayCardCommand(id, Decree, [])); await run.Top(); }
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"])); await run.Top();
        Assert.Equal(2, run.State.TriggerQueue.Count);
        var queue = run.State.TriggerQueue.OrderBy(t => t.SourceObjectId, StringComparer.Ordinal).ToArray();
        if (reverse) Array.Reverse(queue);
        await run.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queue.Select(t => t.TriggerId).ToArray()));
        var death = Assert.Single((await run.Top()).Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Equal(queue[0].SourceObjectId, Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]).Source.ObjectId);
        await PayPhoenix(run, false);
        Assert.DoesNotContain((await run.Top()).Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Equal(1, run.Events().Count(e => e.Event.Kind == "UNIT_DESTROYED"));
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task ReturningDamagedTargetDuringActualResponseMakesEarnedDecreeAbilityNoop()
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298", "P2", "HAND");
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("DECREE", Decree, [])); await run.Top();
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["TARGET"])); await run.Top();
        var bound = Assert.Single(run.State.StackItems).DamageDestruction!.DamagedUnit;
        if (run.State.PriorityPlayerId != "P2") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
        await run.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["TARGET"])); await run.Top();
        Assert.Contains("TARGET", run.State.PlayerZones["P2"].Hand); Assert.True(run.State.CardObjects["TARGET"].ObjectGeneration > bound!.Generation);
        var current = run.State.StackItems.Single();
        var forged = run.State with { StackItems = [current with { DamageDestruction = current.DamageDestruction! with {
            DamagedUnit = new("TARGET", run.State.CardObjects["TARGET"].ObjectGeneration) } }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(new(true, null, forged, [],
            ResolutionResult.BuildSnapshots(forged), ResolutionResult.BuildPrompts(forged))));
        Assert.DoesNotContain((await run.Top()).Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Null(run.State.PendingPayment); Assert.Empty(run.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Theory]
    [InlineData(Decree, "DECREE", false)]
    [InlineData(Guillotine, "GUILLOTINE", true)]
    public async Task BothInstructionsExpireAtTheActualEndOfTurn(string no, string id, bool targeted)
    {
        var initial = Add(Position(), "OPPONENT-RAY", Ray, "P2", "HAND");
        initial = Add(Add(initial, "P2-RED1", "OGN·007/298", "P2", "BASE"), "P2-RED2", "OGN·007/298", "P2", "BASE");
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand(id, no, targeted ? ["TARGET"] : [])); await run.Top();
        Assert.Single(run.State.UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_DESTRUCTION|", StringComparison.Ordinal));
        await run.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(run.State.UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_DESTRUCTION|", StringComparison.Ordinal));
        await run.Act("P2", new TapRuneCommand("P2-RED1"));
        await run.Act("P2", new RecycleRuneCommand("P2-RED2"));
        await run.Act("P2", new PlayCardCommand("OPPONENT-RAY", Ray, ["TARGET"])); await run.Top();
        Assert.Equal(3, run.State.CardObjects["TARGET"].Damage); Assert.Empty(run.State.StackItems); Assert.Null(run.State.PendingPayment);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    [Fact]
    public async Task ReturnedAndActuallyReplayedUnitCannotReuseGuillotinesOldTargetInTheSameTurn()
    {
        var initial = Add(Add(Position(), "OWN-UNIT", "OGN·010/298", "P1", "BATTLEFIELD", "F"), "RETURN", "OGN·104/298", "P1", "HAND");
        var run = new StandbyDefendObserverTests.Driver(initial);
        await run.Act("P1", new PlayCardCommand("GUILLOTINE", Guillotine, ["OWN-UNIT"])); await run.Top();
        await run.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["OWN-UNIT"])); await run.Top();
        Assert.DoesNotContain(run.State.UntilEndOfTurnEffects, e => e.StartsWith("DAMAGE_DESTRUCTION|", StringComparison.Ordinal));
        await run.Act("P1", new PlayCardCommand("OWN-UNIT", "OGN·010/298", [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        await run.Act("P1", new MoveUnitCommand("OWN-UNIT", "BASE", "BATTLEFIELD:F"));
        for (var i = 0; i < 4 && run.State.FocusPlayerId is { } focus; i++) await run.Act(focus, new PassFocusCommand());
        Assert.True(run.State.CardObjects["OWN-UNIT"].ObjectGeneration > 0);
        await run.Act("P1", new PlayCardCommand("RAY", Ray, ["OWN-UNIT"])); var death = Assert.Single((await run.Top()).Events, e => e.Kind == "UNIT_DESTROYED");
        Assert.Equal(Ray, Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]).CardNo);
        Assert.DoesNotContain(run.State.TriggerQueue, t => t.DamageDestruction is not null);
        Assert.Null(Assert.Single(run.State.StackItems).DamageDestruction); await PayPhoenix(run, false);
        Assert.Equal(initial.TurnNumber, run.State.TurnNumber);
        await RideTheWindCurrentMovementTests.Replay(initial, run);
    }

    internal static MatchState Add(MatchState state, string id, string no, string owner, string zone, string? field = null)
    {
        var zones = state.PlayerZones.ToDictionary(); var own = zones[owner];
        zones[owner] = zone switch { "HAND" => own with { Hand = own.Hand.Append(id).ToArray() },
            "GRAVEYARD" => own with { Graveyard = own.Graveyard.Append(id).ToArray() },
            "BATTLEFIELD" => own with { Battlefields = own.Battlefields.Append(id).ToArray() },
            _ => own with { Base = own.Base.Append(id).ToArray() } };
        return state with { PlayerZones = zones, CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = HweiMoveDiscardTypesTests.Card(id, no, owner) }, ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            [id] = new(owner, zone, field) } };
    }
}
