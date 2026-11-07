using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class NamedConquestActivationTests
{
    private static MatchState Position(bool borrowed = false, bool response = false)
    {
        var state = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·286/298"),
            "VAYNE", "OGN·035/298", "P1", "BATTLEFIELD", "F");
        for (var i = 0; i < 2; i++) state = RadGearTestPosition.Put(state, "PAYRUNE" + i, "VEN·R01", "P1", "BASE");
        if (borrowed) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["VAYNE"] = state.CardObjects["VAYNE"] with { OwnerId = "P2" } } };
        if (response) state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return state;
    }

    private static async Task OpenArena(Driver game)
    {
        await game.Act("P1", new PassPriorityCommand());
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; game.State.StackItems[^1].HeldContext?.Kind != "ACTIVATE_CONQUEST"; i++)
        {
            Assert.True(i < 6); await game.Top();
        }
    }

    private static async Task Pay(Driver game, bool accept)
    {
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal("P1", payment.PlayerId); Assert.Equal(1, payment.ManaCost);
        if (accept) await game.TapRunes("P1", ["PAYRUNE0", "PAYRUNE1"]);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
            [accept ? "SPEND_MANA:1" : "DECLINE"]));
    }

    [Theory]
    [InlineData(true, false)] [InlineData(false, false)] [InlineData(true, true)]
    public async Task HoldingTheArenaEarnsAnIndependentVaynePaymentAndReturnsToTheOwnerAfterResponses(bool accept, bool borrowed)
    {
        var game = new Driver(Position(borrowed)); await OpenArena(game);
        var parent = Assert.Single(game.State.StackItems, item => item.HeldContext?.Kind == "ACTIVATE_CONQUEST");
        await game.Top();
        var child = Assert.Single(game.State.StackItems, item => item.UnitConquestReturn is not null);
        Assert.Equal(parent.StackItemId, child.UnitConquestReturn!.Activation!.ParentStackItemId);
        Assert.Equal(new ObjectBinding("F", 0), child.UnitConquestReturn.Activation.Source);
        Assert.Equal(0, child.UnitConquestReturn.Occurrence);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await Pay(game, accept);
        Assert.Contains("VAYNE", game.State.PlayerZones["P1"].Battlefields);
        if (accept)
        {
            await game.Top();
            Assert.Contains("VAYNE", game.State.PlayerZones[borrowed ? "P2" : "P1"].Hand);
            Assert.Equal(1, game.State.CardObjects["VAYNE"].ObjectGeneration);
        }
        else Assert.DoesNotContain("VAYNE", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Fact]
    public async Task AReturnResponseAfterPaymentCannotReturnTheSourcesNewHandIncarnation()
    {
        var game = new Driver(Position(response: true)); await OpenArena(game); await game.Top(); await Pay(game, true);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["VAYNE"])); await game.Top();
        Assert.Contains("VAYNE", game.State.PlayerZones["P1"].Hand);
        var before = game.Events().Length; await game.Top();
        Assert.DoesNotContain(game.Events().Skip(before), entry => entry.Event.Kind == "UNIT_RETURNED_TO_HAND");
        Assert.Equal(1, game.State.CardObjects["VAYNE"].ObjectGeneration);
        await game.Replay();
    }

    [Fact]
    public async Task TheArenaChoosesTheUnitsPresentWhenItsOwnSkillResolves()
    {
        var game = new Driver(Position(response: true)); await OpenArena(game);
        await game.TapRunes("P1", ["PAYRUNE0"]);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["VAYNE"])); await game.Top();
        var before = game.Events().Length; await game.Top();
        Assert.DoesNotContain(game.Events().Skip(before), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "VAYNE"));
        Assert.Contains("VAYNE", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task SharedTokenReturnAndLegacyFamiliesActivateOnceWithoutInventingAConquestOrObservingTheGraveyard()
    {
        var state = RadGearTestPosition.Put(Position(), "TOKEN", "SFD·069/221", "P1", "BATTLEFIELD", "F");
        state = RadGearTestPosition.Put(state, "TREE", "UNL-029/219", "P1", "BATTLEFIELD", "F");
        state = RadGearTestPosition.Put(state, "ROCKET", "OGN·252/298", "P1", "GRAVEYARD");
        var game = new Driver(state); await OpenArena(game); var before = game.Events().Length; await game.Top();
        var queued = game.Events().Skip(before).Where(e => e.Event.Kind == "TRIGGER_QUEUED").ToArray();
        Assert.Single(queued, e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "TOKEN")
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), TriggerKinds.UnitConquestCreateDormantGold));
        Assert.Single(queued, e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "VAYNE"));
        Assert.Single(queued, e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "TREE"));
        Assert.DoesNotContain(queued, e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "ROCKET"));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Fact]
    public async Task HuntEarnsItsNormalHoldAndOneAdditionalNamedConquestWithoutARealConquest()
    {
        var state = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·286/298"),
            "HUNT", "UNL-100/219", "P1", "BATTLEFIELD", "F");
        // PrintedCardFactory restores the outside-play face. This midgame
        // position also needs the keyword tags materialized by playing it.
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-100/219", out var definition));
        var hunt = state.CardObjects["HUNT"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HUNT"] = hunt with { Tags = hunt.Tags.Concat(definition.SourceUnitTags!.Split('|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToArray() } } };
        var game = new Driver(state); await OpenArena(game); await game.Top();
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(6, game.State.PlayerExperience["P1"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Fact]
    public async Task CrimsonDoesNotRepeatLegacyConquestInstructionsActivatedByHolding()
    {
        var state = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·286/298"),
            "TREE", "UNL-029/219", "P1", "BATTLEFIELD", "F");
        var game = new Driver(state); await OpenArena(game); await game.Top(); await game.Top();
        var activated = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_CONQUEST_EFFECT_ACTIVATED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "TREE"));
        Assert.Equal("BATTLEFIELD_HELD", activated.Event.Payload["reason"]);
        Assert.Equal(1, game.State.CardObjects["TREE"].BoonCount);
        await game.Replay();
    }

    [Fact]
    public async Task AChildCannotInventItsParentActivationInAnOtherwiseConsistentColdFrame()
    {
        var initial = Position(); var game = new Driver(initial); await OpenArena(game); await game.Top();
        var child = Assert.Single(game.State.StackItems, item => item.UnitConquestReturn is not null);
        var context = child.UnitConquestReturn!;
        var forged = game.State with { StackItems = game.State.StackItems.Select(item => item.StackItemId == child.StackItemId
            ? item with { UnitConquestReturn = context with { Activation = context.Activation! with {
                ParentStackItemId = context.Activation.ParentStackItemId + ":not-recorded" } } } : item).ToArray() };
        var frame = GraveyardConquestReturnTests.Frame(initial, game, forged);
        Assert.Contains(frame.ValidationErrors, e => e.Contains("accepted named conquest activation history", StringComparison.Ordinal));
        await GraveyardConquestReturnTests.AssertForgedStoreCannotRestore(frame, "accepted named conquest activation history");
    }
}
