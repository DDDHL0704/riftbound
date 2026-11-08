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
        var game = new Driver(state); await OpenArena(game); await game.Top();
        var captured = Assert.Single(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "TREE"));
        Assert.Equal("BATTLEFIELD_HELD", captured.Event.Payload["triggeredByEventKind"]);
        Assert.Single(game.State.StackItems);
        Assert.Equal(0, game.State.CardObjects["TREE"].BoonCount);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["TREE"]));
        await game.Top();
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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealRecastBetweenArenaCopiesGrantsAnOptionalConquestReturn(bool accept)
    {
        var initial = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·286/298"),
            "HOST", "OGN·001/298", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "PURSUIT", RuthlessPursuitTests.Card, "P1", "GRAVEYARD");
        initial = RadGearTestPosition.Put(initial, "BLUE", "UNL-087/219", "P1", "BATTLEFIELD", "F");
        initial = RadGearTestPosition.Put(initial, "KAISA", "OGN·112/298", "P1", "BATTLEFIELD", "F");
        initial = RadGearTestPosition.Put(initial, "RUNE", "VEN·R01", "P1", "BASE");
        initial = initial with { PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P1"] = 2 } };
        var game = new Driver(initial); await OpenArena(game);
        Assert.Equal(2, game.State.StackItems.Count(item => item.HeldContext?.Kind == "ACTIVATE_CONQUEST"));
        // Closed beginning-step chains permit reactions, not an ordinary Swift
        // response. Kai'Sa's actual effect-play permission casts the movement
        // between the two Arena skills earned through Blue Sentinel.
        await game.Top(); await game.Top();
        Assert.Equal(["PURSUIT"], game.State.PendingEffectPlay!.Sources.Keys);
        await game.Act("P1", new RecycleRuneCommand("RUNE"));
        await game.Act("P1", new PlayCardCommand("PURSUIT", RuthlessPursuitTests.Card, ["HOST"], Destination: "BATTLEFIELD:F"));
        await game.Top();
        Assert.Contains(game.State.CardObjects["HOST"].UntilEndOfTurnEffects, e => e.StartsWith("GRANTED_CONQUEST_RETURN_BASE|"));
        var parent = Assert.Single(game.State.StackItems, item => item.HeldContext?.Kind == "ACTIVATE_CONQUEST");
        await game.Top();
        for (var i = 0; game.State.PendingCardChoice?.EffectKind != "UNIT_GRANTED_CONQUEST_RETURN_BASE"; i++)
        {
            Assert.True(i < 5, "The newly granted return did not reach confirmation.");
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else await game.Top();
        }
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT_GRANTED_CONQUEST_RETURN_BASE", choice.EffectKind);
        var skill = Assert.Single(game.State.StackItems, item => item.UnitConquest is not null);
        Assert.Equal(parent.StackItemId, skill.UnitConquest!.Activation!.ParentStackItemId);
        var forged = game.State with { StackItems = game.State.StackItems.Select(item => item.StackItemId == skill.StackItemId
            ? item with { UnitConquest = item.UnitConquest! with { Activation = null } } : item).ToArray() };
        var frame = GraveyardConquestReturnTests.Frame(initial, game, forged);
        Assert.Contains(frame.ValidationErrors, e => e.Contains("named conquest activation", StringComparison.Ordinal));
        await GraveyardConquestReturnTests.AssertForgedStoreCannotRestore(frame, "named conquest activation");
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["HOST"] : []));
        Assert.Equal("F", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        if (accept)
            for (var i = 0; game.State.StackItems.Any(item => item.StackItemId == skill.StackItemId); i++)
            {
                Assert.True(i < 4); await game.Top();
            }
        Assert.Equal(accept ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(accept ? 2 : 1, game.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    private static MatchState KaisaPosition(int score = 2)
    {
        var initial = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·286/298"),
            "KAISA", "OGN·112/298", "P1", "BATTLEFIELD", "F");
        initial = RadGearTestPosition.Put(initial, "SPELL", "OGN·048/298", "P1", "GRAVEYARD");
        return initial with { PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P1"] = score } };
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)]
    public async Task NamedRecastChecksTheCurrentScoreAfterHoldingAndRecyclesTheRealSpell(int score, bool eligible)
    {
        var game = new Driver(KaisaPosition(score)); await OpenArena(game); await game.Top();
        var skill = Assert.Single(game.State.StackItems, item => item.RecastContext is not null);
        Assert.NotNull(skill.RecastContext!.Activation);
        Assert.Equal(score + 1, game.State.PlayerScores["P1"]);
        await game.Top();
        if (eligible)
        {
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.Equal(["SPELL"], pending.Sources.Keys);
            await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
            var cast = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "SPELL");
            Assert.Equal(0, cast.PlayCost!.PaidMana);
            await game.Top();
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].MainDeck);
        }
        else
        {
            Assert.Null(game.State.PendingEffectPlay);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        }
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Fact]
    public async Task NamedActivationDoesNotSatisfyTheSeparateUncontrolledBattlefieldCondition()
    {
        var initial = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·286/298"),
            "YONE", "SFD·233/221", "P1", "BATTLEFIELD", "F");
        initial = RadGearTestPosition.Put(initial, "ENEMY", "OGN·001/298", "P2", "BASE");
        var game = new Driver(initial); await OpenArena(game); await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "UNIT_CONQUEST_SOURCE_POWER_DAMAGE"));
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await game.Replay();
    }

    [Theory]
    [InlineData("parent")] [InlineData("remove")] [InlineData("fieldGeneration")] [InlineData("unitGeneration")]
    [InlineData("eraseAndRename")]
    public async Task TokenRecoveryRejectsAChangedOrRemovedNamedOrigin(string mutation)
    {
        var initial = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·286/298"),
            "TOKEN", "SFD·069/221", "P1", "BATTLEFIELD", "F");
        var game = new Driver(initial); await OpenArena(game); await game.Top();
        var skill = Assert.Single(game.State.StackItems, item => item.ConquestToken is not null);
        var context = skill.ConquestToken!;
        var changed = mutation switch {
            "parent" => context with { Activation = context.Activation! with { ParentStackItemId = "not-accepted" } },
            "remove" or "eraseAndRename" => context with { Activation = null },
            "fieldGeneration" => context with { Activation = context.Activation! with { Source = new("F", 1) } },
            _ => context with { SourceGeneration = context.SourceGeneration + 1 } };
        var forged = game.State with { StackItems = game.State.StackItems.Select(item => item.StackItemId == skill.StackItemId
            ? item with { ConquestToken = changed,
                StackItemId = mutation == "eraseAndRename" ? "ordered-forged-conquest" : item.StackItemId } : item).ToArray() };
        var frame = GraveyardConquestReturnTests.Frame(initial, game, forged);
        Assert.Contains(frame.ValidationErrors, e => e.Contains("named conquest activation", StringComparison.Ordinal));
        await GraveyardConquestReturnTests.AssertForgedStoreCannotRestore(frame, "named conquest activation");
    }

    [Fact]
    public async Task PendingRecastCannotDropItsNamedParentWhenItLeavesTheStack()
    {
        var initial = KaisaPosition(); var game = new Driver(initial); await OpenArena(game); await game.Top(); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        var forged = game.State with { PendingEffectPlay = pending with {
            Parent = pending.Parent with { RecastContext = pending.Parent.RecastContext! with { Activation = null } } } };
        var frame = GraveyardConquestReturnTests.Frame(initial, game, forged);
        Assert.Contains(frame.ValidationErrors, e => e.Contains("named conquest activation", StringComparison.Ordinal));
        await GraveyardConquestReturnTests.AssertForgedStoreCannotRestore(frame, "named conquest activation");
    }
}
