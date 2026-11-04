using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class BlastConeMovementFaqTests
{
    private const string Cone = "UNL-133/219";
    private const string StunKind = "MOVED_ENEMY_OPTIONAL_EXHAUST_STUN";

    private static MatchState Position(string enemy = "OGN·219/298", bool empowered = false, bool enemyAtField = false)
    {
        var state = Put(Create(), "CONE", Cone, "P1", "HAND");
        state = Put(state, "ENEMY", enemy, "P2", enemyAtField ? "BATTLEFIELD" : "BASE", enemyAtField ? "BF" : null);
        state = Put(state, "STUN", "UNL-042/219", "P1", "HAND");
        state = Put(state, "RETURN", "OGN·104/298", "P2", "HAND");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { IsEmpowered = empowered } } };
    }

    private static async Task OpenMovement(Driver game, bool move = true, string destination = "BATTLEFIELD:BF")
    {
        var originalZone = game.State.ObjectLocations["ENEMY"].Zone;
        await game.Act("P1", new PlayCardCommand("CONE", Cone, []));
        for (var i = 0; i < 12; i++)
        {
            if (game.State.PendingRuleChoice is { } rule)
            {
                Assert.Equal("MOVEMENT_DESTINATION", rule.Request.Kind);
                await game.Act("P1", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [destination]));
                var skill = Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == Cone);
                Assert.Equal(destination, skill.Destination);
                Assert.Equal(originalZone, game.State.ObjectLocations["ENEMY"].Zone);
                Assert.NotNull(game.State.PriorityPlayerId);
            }
            else if (game.State.PendingCardChoice is { ChoiceWindow: "TRIGGER_COST_CONFIRMATION" }) return;
            else if (game.State.PendingCardChoice is { } choice)
            {
                Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, move ? ["ENEMY"] : []));
            }
            else if (game.State.StackItems.Count > 0) await game.Top();
            else return;
        }
        Assert.Fail("The finite play/movement sequence did not reach its stun decision.");
    }

    private static async Task PayStun(Driver game, bool pay)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(new[] { "CONE" }, choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["CONE"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["CONE"] : []));
    }

    [Theory]
    [InlineData("VEN·086")]
    [InlineData("VEN·181")]
    public async Task NonTargetedStunBypassesGangplankAndLaterGoAwayCannotReplaceAnAlreadyStunnedAction(string printing)
    {
        var game = new Driver(Position(printing, true, enemyAtField: true));
        var might = game.State.CardObjects["ENEMY"].Power;
        await Reject(game.State, "P1", new PlayCardCommand("CONE", Cone, ["ENEMY"]));
        await OpenMovement(game, destination: "BASE:P2");
        Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        await PayStun(game, true);
        var skill = Assert.Single(game.State.StackItems, item => item.MovementObserver?.Kind == StunKind);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
        Assert.NotNull(skill.TriggerCost); Assert.True(game.State.CardObjects["CONE"].IsExhausted);
        await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(might, game.State.CardObjects["ENEMY"].Power);
        Assert.Empty(game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers);
        // Go Away is an ordinary spell. Close the movement's empty-field duel
        // through real focus passes before playing it in the main open window.
        for (var passes = 0; game.State.FocusPlayerId is { } focus; passes++)
        {
            Assert.True(passes < 2);
            await game.Act(focus, new PassFocusCommand());
        }
        Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
        await game.Act("P1", new PlayCardCommand("STUN", "UNL-042/219", ["ENEMY"])); await game.Top();
        Assert.Empty(game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(might, game.State.CardObjects["ENEMY"].Power);
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalPlayMovementAndOptionalStunAreSeparateDecisions(bool move)
    {
        var game = new Driver(Position());
        await OpenMovement(game, move);
        if (move) await PayStun(game, false);
        Assert.Equal(move ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task ARealReturnBeforeResolutionDoesNotStunTheCardsNewGeneration()
    {
        var game = new Driver(Position()); await OpenMovement(game); await PayStun(game, true);
        var generation = game.State.CardObjects["ENEMY"].ObjectGeneration;
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.True(game.State.CardObjects["ENEMY"].ObjectGeneration > generation);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("unit-controller")]
    [InlineData("source-revision")]
    [InlineData("future-tick")]
    [InlineData("target")]
    [InlineData("receipt-generation")]
    public async Task RestoreRejectsForgedMovementIdentityAndPayment(string change)
    {
        var game = new Driver(Position()); await OpenMovement(game); await PayStun(game, true);
        var item = Assert.Single(game.State.StackItems, item => item.MovementObserver?.Kind == StunKind);
        var context = item.MovementObserver!;
        var forged = change switch {
            "actor" => item with { MovementObserver = context with { Unit = context.Unit with { MovingPlayerId = "P2" } } },
            "unit-controller" => item with { MovementObserver = context with { Unit = context.Unit with { ControllerId = "P1" } } },
            "source-revision" => item with { MovementObserver = context with { SourceFaceRevision = -1 } },
            "future-tick" => item with { MovementObserver = context with { CapturedTick = game.State.Tick + 1 } },
            "target" => item with { TargetObjectIds = ["ENEMY"], TargetGenerations = new Dictionary<string, long> { ["ENEMY"] = context.Unit.Generation } },
            _ => item with { TriggerCost = item.TriggerCost! with { Source = new("CONE", 999) } }
        };
        var changed = game.State with { StackItems = game.State.StackItems.Select(i => i.StackItemId == item.StackItemId ? forged : i).ToArray() };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(changed));
        await game.Replay();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASeparateSpellMovementTriggersOnlyWhenTheConeCanPayItsOwnExhaustion(bool exhausted)
    {
        var initial = Put(Position(), "CONE", Cone, "P1", "BASE");
        initial = Put(initial, "MOVE", "OGN·258/298", "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["CONE"] = initial.CardObjects["CONE"] with { IsExhausted = exhausted } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("MOVE", "OGN·258/298", ["ENEMY"], Destination: "BATTLEFIELD:BF"));
        await game.Top();
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        if (!exhausted) { await PayStun(game, true); await game.Top(); }
        else Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(!exhausted, game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.True(game.State.CardObjects["CONE"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "ALLY", "BF")]
    [InlineData("P2", "ENEMY", "OTHER")]
    public async Task MovingYourOwnUnitsDoesNotTriggerEitherPlayersCone(string actor, string unit, string field)
    {
        var initial = Put(Position(), "CONE", Cone, "P1", "BASE") with {
            TurnPlayerId = actor, ActivePlayerId = actor, PriorityPlayerId = null };
        initial = Put(initial, "MOVE", "OGN·173/298", actor, "HAND");
        var game = new Driver(initial);
        await game.Act(actor, new PlayCardCommand("MOVE", "OGN·173/298", [unit], Destination: "BATTLEFIELD:" + field));
        await game.Top();
        Assert.Equal(field, game.State.ObjectLocations[unit].BattlefieldObjectId);
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.MovementObserver?.Kind == StunKind);
        Assert.DoesNotContain(game.State.StackItems, item => item.MovementObserver?.Kind == StunKind);
        Assert.Null(game.State.PendingCardChoice);
        Assert.False(game.State.CardObjects["CONE"].IsExhausted);
        await game.Replay();
    }
}
