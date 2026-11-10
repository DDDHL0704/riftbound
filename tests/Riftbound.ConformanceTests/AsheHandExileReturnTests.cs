using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// UNL-169; CN355.9/355.10.a (opponent vs hidden choice), 425.1/425.3
// (current hand), 391.5/392/393 (independent, linked legal-zone lifetime).
public sealed class AsheHandExileReturnTests
{
    private const string Ashe = "UNL-169/219";

    private static MatchState Position(bool empty = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "ASHE", Ashe, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        if (!empty)
        {
            state = RadGearTestPosition.Put(state, "OP-A", "OGN·219/298", "P2", "HAND");
            state = RadGearTestPosition.Put(state, "OP-B", "OGN·048/298", "P2", "HAND");
        }
        return state;
    }

    private static Task Choose(Driver game, params string[] ids)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    private static async Task Open(Driver game, string destination = "BASE")
    {
        await game.Act("P1", new PlayCardCommand("ASHE", Ashe, [], Destination: destination));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT_EVENT_CHOSEN_OPPONENT", choice.ChoiceWindow);
        Assert.Equal(new[] { "P2" }, choice.LegalObjectIds);
        Assert.Null(game.State.PriorityPlayerId);
        Assert.DoesNotContain("OP-A", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await Choose(game, "P2");
        Assert.Equal("P2", Assert.Single(game.State.StackItems).UnitEvent!.ChosenOpponentId);
        Assert.NotNull(game.State.PriorityPlayerId);
    }

    private static async Task Exile(Driver game)
    {
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("REVEALED_HAND_EFFECT", choice.ChoiceWindow);
        Assert.Equal("P2", choice.HandContext!.OwnerId); Assert.Equal(1, choice.RequiredCount);
        Assert.Contains("OP-A", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await Choose(game, "OP-A");
        Assert.Contains("OP-A", game.State.PlayerZones["P2"].Banished);
        Assert.DoesNotContain("OP-A", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("OP-B", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        Assert.Empty(game.State.StackItems);
        var schedule = Assert.Single(game.State.ScheduledUnitEvents!);
        Assert.Equal("P2", schedule.Context.ChosenOpponentId);
        Assert.Equal(game.State.CardObjects["OP-A"].ObjectGeneration, schedule.Context.HandExile!.Card.Generation);
    }

    private static async Task Hold(Driver game)
    {
        await game.Act(game.State.TurnPlayerId, new EndTurnCommand());
        for (var i = 0; !game.State.StackItems.Any(item => item.UnitEvent?.DelayedReceipt is not null); i++)
        {
            Assert.InRange(i, 0, 8);
            Assert.Equal(MatchPhases.TurnStart, game.State.Phase);
            await game.Act(game.State.TurnPlayerId, new PassPriorityCommand());
        }
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_HELD"
            && Equals(entry.Event.Payload.GetValueOrDefault("playerId"), "P2"));
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ActualPlayConfirmsTheOpponentThenBanishesOneRevealedCardAndReturnsItOnlyAfterTheNextRealHold(string destination)
    {
        var game = new Driver(Position()); await Open(game, destination);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "HAND_REVEALED");
        await Exile(game);
        var generation = game.State.CardObjects["OP-A"].ObjectGeneration;
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DISCARDED");
        await Hold(game);
        Assert.Null(game.State.ScheduledUnitEvents);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", skill.ControllerId);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && Equals(entry.Event.Payload.GetValueOrDefault("triggeredByEventKind"), "BATTLEFIELD_HELD"));
        Assert.Contains("OP-A", game.State.PlayerZones["P2"].Banished);
        Assert.Null(game.State.PendingCardChoice);
        await game.Top();
        Assert.Contains("OP-A", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("OP-A", game.State.PlayerZones["P2"].Banished);
        Assert.Equal(generation + 1, game.State.CardObjects["OP-A"].ObjectGeneration);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "DELAYED_UNIT_EVENT_TRIGGERED");
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturningAsheBeforeOrAfterTheHandChoiceKeepsTheEarnedIndependentReturnAndPrivateSource(bool beforeChoice)
    {
        var game = new Driver(Position()); await Open(game);
        if (beforeChoice)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ASHE"])); await game.Top();
        }
        await Exile(game);
        if (!beforeChoice)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ASHE"])); await game.Top();
        }
        Assert.Contains("ASHE", game.State.PlayerZones["P1"].Hand);
        await Hold(game);
        Assert.DoesNotContain("\"ASHE\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var original = game.Events(); var originalHash = MatchStateHasher.HashValue(original);
        await game.Top();
        Assert.Contains("OP-A", game.State.PlayerZones["P2"].Hand);
        Assert.Equal("HIDDEN", Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_RETURNED_TO_HAND"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "OP-A")).Event.Payload["sourceObjectId"]);
        Assert.Equal(originalHash, MatchStateHasher.HashValue(game.Events().Take(original.Length).ToArray()));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnEmptyHandStillRevealsAndCompletesWithoutCreatingAnExileOrReturn()
    {
        var game = new Driver(Position(empty: true)); await Open(game); await game.Top();
        Assert.Equal(0, game.State.PendingCardChoice!.RequiredCount);
        await Choose(game);
        Assert.Null(game.State.ScheduledUnitEvents); Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "HAND_REVEALED");
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "CARD_BANISHED" or "DELAYED_UNIT_EVENT_CREATED");
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnActualDrawResponseChangesTheHandBeforeTheMandatoryReveal()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "DRAW", "UNL-103/219", "P2", "HAND"));
        await Open(game);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("DRAW", "UNL-103/219", [], Mode: "DRAW_1"));
        await game.Top();
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        var id = Assert.Single(game.State.PlayerZones["P2"].Hand, card => card is not ("OP-A" or "OP-B"));
        Assert.DoesNotContain(id, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await game.Top();
        Assert.Contains(id, game.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("DRAW", game.State.PendingCardChoice.LegalObjectIds);
        await Choose(game, id);
        Assert.Contains(id, game.State.PlayerZones["P2"].Banished);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ACounterRestrictedToFriendlyPermanentTargetsCannotCounterThisPlayerTargetedSkill()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "COUNTER", "SFD·045/221", "P2", "HAND"));
        await Open(game); var skill = Assert.Single(game.State.StackItems);
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await RadGearTestPosition.Reject(game.State, "P2",
            new PlayCardCommand("COUNTER", "SFD·045/221", [skill.StackItemId]));
        await game.Top(); await Choose(game, "OP-A");
        Assert.Single(game.State.ScheduledUnitEvents!);
        Assert.Contains("COUNTER", game.State.PlayerZones["P2"].Hand);
        Assert.Contains("OP-A", game.State.PlayerZones["P2"].Banished);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheRuleComponentUsesTheChosenThirdOpponentsHandAndKeepsTheOtherOpponentsCardsPrivate()
    {
        var initial = Position();
        initial = initial with {
            Seats = new Dictionary<string, string>(initial.Seats) { ["P3"] = "P3" },
            ReadyPlayerIds = [..initial.ReadyPlayerIds, "P3"],
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P3"] = PlayerZones.Empty },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P3"] = new(30, 30) },
            PlayerScores = new Dictionary<string, int>(initial.PlayerScores) { ["P3"] = 0 },
            PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) { ["P3"] = 0 } };
        initial = RadGearTestPosition.Put(initial, "THIRD", "OGN·219/298", "P3", "HAND");
        // The room codec currently accepts two seat labels. This component
        // fixture exercises the shared engine's chosen-player semantics without
        // presenting a three-seat frame as a supported persistence protocol.
        var state = initial; var engine = new CoreRuleEngine(); var ordinal = 0;
        async Task Act(string player, GameCommand command)
        {
            var result = await engine.ResolveAsync(state, new("third-opponent-" + ordinal++, player, command.CmdType), command, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result); state = result.State;
        }
        await Act("P1", new PlayCardCommand("ASHE", Ashe, []));
        Assert.Equal(new[] { "P2", "P3" }, state.PendingCardChoice!.LegalObjectIds);
        var opponent = state.PendingCardChoice;
        await Act("P1", new ChooseCardsCommand(opponent.ChoiceId, opponent.ChoiceWindow, ["P3"]));
        for (var passes = 0; state.PendingCardChoice is null; passes++)
        {
            Assert.InRange(passes, 0, 4);
            await Act(state.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Equal("P3", state.PendingCardChoice.HandContext!.OwnerId);
        Assert.Equal(new[] { "THIRD" }, state.PendingCardChoice.LegalObjectIds);
        var view = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state)["P1"]);
        Assert.Contains("THIRD", view); Assert.DoesNotContain("OP-A", view); Assert.DoesNotContain("OP-B", view);
        var hand = state.PendingCardChoice;
        await Act("P1", new ChooseCardsCommand(hand.ChoiceId, hand.ChoiceWindow, ["THIRD"]));
        Assert.Equal("P3", Assert.Single(state.ScheduledUnitEvents!).Context.ChosenOpponentId);
        Assert.Contains("THIRD", state.PlayerZones["P3"].Banished);
        Assert.Equal(new[] { "OP-A", "OP-B" }, state.PlayerZones["P2"].Hand);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealImageCopiedAfterEntryDoesNotRetroactivelyTriggerTheCopiedOnPlaySkill(bool sourceLeaves)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "ASHE", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["ASHE"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(Ashe, image.CardNo);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        if (sourceLeaves)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId]));
            await game.Top(); Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        }
        Assert.Equal(new[] { "OP-A", "OP-B" }, game.State.PlayerZones["P2"].Hand);
        Assert.Null(game.State.ScheduledUnitEvents);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "HAND_REVEALED" or "DELAYED_UNIT_EVENT_CREATED");
        Assert.DoesNotContain("OP-A", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("actor")] [InlineData("empty")] [InlineData("two")] [InlineData("own")] [InlineData("deck")]
    public async Task InvalidRevealedHandChoicesRejectAtomicallyAndLeaveTheRealChoiceUsable(string invalid)
    {
        var game = new Driver(Position()); await Open(game); await game.Top();
        var choice = game.State.PendingCardChoice!;
        var selected = invalid switch { "empty" => Array.Empty<string>(), "two" => ["OP-A", "OP-B"],
            "own" => ["RETURN"], "deck" => ["P2-D0"], _ => ["OP-A"] };
        await RadGearTestPosition.Reject(game.State, invalid == "actor" ? "P2" : "P1",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        await Choose(game, "OP-A"); await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("erase")] [InlineData("generation")] [InlineData("completed")]
    [InlineData("unconfirmed")] [InlineData("null-cards")]
    public async Task RecoveryAuthenticatesTheOriginalRevealedHandAndItsUnfinishedExecution(string change)
    {
        var game = new Driver(Position()); await Open(game); await game.Top();
        var choice = game.State.PendingCardChoice!; var top = game.State.StackItems.Single();
        var forged = game.State;
        if (change == "erase") forged = forged with { PendingCardChoice = null };
        if (change == "completed") forged = forged with { StackItems = [top with { CompletedHandExecutions = 1 }] };
        if (change == "unconfirmed") forged = forged with { StackItems = [top with {
            UnitEvent = top.UnitEvent! with { ChosenOpponentId = null, OpponentConfirmedAtTick = null } }] };
        if (change == "null-cards") forged = forged with { PendingCardChoice = choice with {
            HandContext = choice.HandContext! with { Cards = null! } } };
        if (change == "generation") forged = forged with {
            CardObjects = new Dictionary<string, CardObjectState>(forged.CardObjects) {
                ["OP-A"] = forged.CardObjects["OP-A"] with { ObjectGeneration = 10 } },
            PendingCardChoice = choice with { HandContext = choice.HandContext! with {
                Cards = new Dictionary<string, long>(choice.HandContext.Cards) { ["OP-A"] = 10 } } } };
        var history = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        Assert.Equal(history, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await Choose(game, "OP-A"); await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("erase")] [InlineData("duplicate")] [InlineData("birth")] [InlineData("generation")]
    public async Task RecoveryBindsThePendingReturnToTheOriginalAcceptedScheduleWithoutRewritingHistory(string change)
    {
        var game = new Driver(Position()); await Open(game); await Exile(game);
        var schedule = Assert.Single(game.State.ScheduledUnitEvents!);
        var changed = change == "birth" ? schedule with { Receipt = schedule.Receipt with { CreatedTick = schedule.Receipt.CreatedTick - 1 } }
            : change == "generation" ? schedule with { Context = schedule.Context with {
                HandExile = schedule.Context.HandExile! with { Card = schedule.Context.HandExile.Card with {
                    Generation = schedule.Context.HandExile.Card.Generation + 1 } } } } : schedule;
        var forged = game.State with { ScheduledUnitEvents = change == "erase" ? null
            : change == "duplicate" ? [schedule, schedule] : [changed] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task NeitherAMainTransitionNorTheCreatorsOwnActualHoldFiresTheNamedOpponentsDelay()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "OTHER", "P1", "BATTLEFIELD", "OTHER");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await Open(game); await Exile(game);
        var schedule = JsonSerializer.Serialize(Assert.Single(game.State.ScheduledUnitEvents!));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", game.State.TurnPlayerId); Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(schedule, JsonSerializer.Serialize(Assert.Single(game.State.ScheduledUnitEvents!)));
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_HELD"
            && Equals(entry.Event.Payload.GetValueOrDefault("playerId"), "P1"));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "DELAYED_UNIT_EVENT_TRIGGERED");
        Assert.Equal(schedule, JsonSerializer.Serialize(Assert.Single(game.State.ScheduledUnitEvents!)));
        Assert.Contains("OP-A", game.State.PlayerZones["P2"].Banished);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("erase")] [InlineData("duplicate")] [InlineData("no-receipt")]
    [InlineData("tick")] [InlineData("no-link")] [InlineData("coherent-generation")]
    public async Task TheFiredReturnAndItsRetirementRemainBoundToTheOriginalAcceptedBirth(string change)
    {
        var game = new Driver(Position()); await Open(game); await Exile(game); await Hold(game);
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var forgedContext = change switch {
            "no-receipt" => context with { DelayedReceipt = null },
            "tick" => context with { DelayedReceipt = context.DelayedReceipt! with { FiredTick = context.CapturedTick } },
            "no-link" => context with { HandExile = null },
            "coherent-generation" => context with { HandExile = context.HandExile! with {
                Card = context.HandExile.Card with { Generation = context.HandExile.Card.Generation + 1 } } },
            _ => context };
        var forged = game.State with { StackItems = change == "erase" ? []
            : change == "duplicate" ? [item, item with { StackItemId = item.StackItemId + "-clone" }]
            : [item with { UnitEvent = forgedContext }] };
        if (change == "coherent-generation") forged = forged with {
            CardObjects = new Dictionary<string, CardObjectState>(forged.CardObjects) {
                ["OP-A"] = forged.CardObjects["OP-A"] with {
                    ObjectGeneration = forgedContext.HandExile!.Card.Generation } } };
        var original = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        Assert.Equal(original, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.Top();
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [item] }));
        Assert.Contains("OP-A", game.State.PlayerZones["P2"].Hand);
        await game.ColdCheckpoint();
    }
}
