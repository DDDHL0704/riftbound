using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN190.6.c, 355.10.d, 383.1.b/383.2, 383.3 and the three printed CN texts.
public sealed partial class WorkshopBattlefieldUnitPlayTests
{
    private const string Workshop = "OGN·211/298", Plain = "OGN·219/298";
    private const string Spring = "UNL-215/219", Valley = "UNL-218/219";
    private const string WardInvader = "OGN·161/298";

    private static MatchState Position(string battlefield, string played = Plain, bool otherHere = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(battlefield), "BORN", played, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "NEXT", Plain, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        if (otherHere) state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        return state;
    }

    private static async Task Enter(Driver game, string no = Plain, string id = "BORN", string field = "BF")
    {
        await game.Act("P1", new PlayCardCommand(id, no, [], Destination: "BATTLEFIELD:" + field));
        if (!game.State.PlayerZones["P1"].Battlefields.Contains(id)) await game.Top();
        Assert.Contains(id, game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(field, game.State.ObjectLocations[id].BattlefieldObjectId);
    }

    private static async Task PayBoon(Driver game, bool pay = true)
    {
        Assert.Null(game.State.PendingCardChoice);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(1, payment.ManaCost); Assert.Equal(0, payment.PowerCost);
        Assert.Contains("支付 1 法力", payment.Reason); Assert.Contains("给予原单位增益", payment.Reason);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
    }

    private static async Task SelectReturn(Driver game, string[] units, string playedUnit = "BORN")
    {
        Assert.Null(game.State.PendingPayment);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("P1", choice.PlayerId); Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.DoesNotContain(playedUnit, choice.LegalObjectIds);
        Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, units));
        Assert.Null(game.State.PendingPayment); Assert.Null(game.State.PendingCardChoice);
    }

    private static GameEvent[] Boons(Driver game)
        => game.Events().Select(entry => entry.Event).Where(ev => ev.Kind == "BOON_GRANTED").ToArray();

    private static CardObjectState[] Minions(Driver game)
        => game.State.CardObjects.Values.Where(card => TokenObjectRules.IsToken(card)
            && P6TokenFactoryCatalog.TryGetByCardNo(card.TokenFactoryCardNo ?? "", out var factory)
            && factory.TokenFamilyName == "随从").ToArray();

    [Fact]
    public async Task WorkshopCreatesItsPrintedMinionAtItsBaseOnlyAfterItsOwnSkillGetsResponses()
    {
        var game = new Driver(Position("OGN·296/298", Workshop));
        await game.Act("P1", new PlayCardCommand("BORN", Workshop, []));
        Assert.Empty(Minions(game)); Assert.Single(game.State.StackItems);
        await game.Top();
        var minion = Assert.Single(Minions(game));
        Assert.Equal(1, minion.Power); Assert.Contains(minion.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", game.State.ObjectLocations[minion.ObjectId].Zone);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task WorkshopUsesItsActualBattlefieldAndDoesNotCreateAnExtraBaseMinion()
    {
        var game = new Driver(Position("OGN·296/298", Workshop));
        await Enter(game, Workshop); Assert.Empty(Minions(game));
        await game.Top(); var minion = Assert.Single(Minions(game));
        Assert.Contains(minion.ObjectId, game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("BF", game.State.ObjectLocations[minion.ObjectId].BattlefieldObjectId);
        Assert.DoesNotContain(minion.ObjectId, game.State.PlayerZones["P1"].Base);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ValleyPaysItsRealOptionalManaBeforeResponsesAndThenBoonsOnlyThePlayedUnit()
    {
        var game = new Driver(Position(Valley)); await Enter(game);
        var mana = game.State.RunePools["P1"].Mana;
        Assert.Empty(Boons(game)); await PayBoon(game);
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Empty(Boons(game)); Assert.Single(game.State.StackItems);
        Assert.Empty(game.State.StackItems[0].TargetObjectIds);
        await game.Top(); Assert.Single(Boons(game));
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["BORN"].Tags);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task DecliningValleyLeavesTheUnitWithoutBoonAndDoesNotSpendThePrintedMana()
    {
        var game = new Driver(Position(Valley)); await Enter(game);
        var mana = game.State.RunePools["P1"].Mana; await PayBoon(game, false);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Empty(Boons(game));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingPayment);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningThePlayedUnitDuringResponsesCannotBoonItsPrivateOrLaterIncarnation()
    {
        var game = new Driver(Position(Valley)); await Enter(game); await PayBoon(game);
        var generation = game.State.CardObjects["BORN"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["BORN"])); await game.Top();
        Assert.Contains("BORN", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["BORN"].ObjectGeneration > generation);
        await game.Top(); Assert.Empty(Boons(game));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("target"), InlineData("erase-payment"), InlineData("player"), InlineData("unit"), InlineData("origin")]
    public async Task ValleyCannotBorrowATargetOrLoseItsOriginalAcceptedPaymentAndCapture(string mutation)
    {
        var game = new Driver(Position(Valley)); await Enter(game); await PayBoon(game);
        var item = Assert.Single(game.State.StackItems); var context = item.FieldContext!;
        var changed = mutation switch {
            "target" => item with { TargetObjectIds = ["ALLY"], TargetGenerations = new Dictionary<string, long> {
                ["ALLY"] = game.State.CardObjects["ALLY"].ObjectGeneration } },
            "erase-payment" => item with { TriggerCost = null },
            "player" => item with { ControllerId = "P2" },
            "unit" => item with { FieldContext = context with { BattlefieldUnitPlay = context.BattlefieldUnitPlay! with {
                Unit = new("ALLY", game.State.CardObjects["ALLY"].ObjectGeneration) } } },
            _ => item with { FieldContext = context with { BattlefieldUnitPlay = null } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [changed] }));
        await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task NamedPlayingPlayerControlsValleyEvenOnAnEnemyFieldAndDoesNotPayWardForItsFixedReference()
    {
        var state = Position(Valley, WardInvader, false);
        state = RadUnitEffectTestFixture.Place(state, "BF", "P2", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        var game = new Driver(state); await Enter(game, WardInvader);
        var mana = game.State.RunePools["P1"].Mana; var power = game.State.RunePools["P1"].TotalPower;
        await PayBoon(game); Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["BORN"].Tags);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ENEMY"].Tags);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task WorkshopAndTheBattlefieldEarnIndependentSkillsInThePlayersChosenOrder(bool workshopFirst)
    {
        var game = new Driver(Position(Valley, Workshop)); await Enter(game, Workshop);
        Assert.Empty(Minions(game)); Assert.Empty(Boons(game));
        var workshop = Assert.Single(game.State.TriggerQueue, trigger => trigger.SourceObjectId == "BORN" && trigger.SourceCardNo == Workshop);
        var valley = Assert.Single(game.State.TriggerQueue, trigger => trigger.FieldContext?.Kind == CoreRuleEngine.BattlefieldEntryBoonKind);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: workshopFirst
            ? [workshop.TriggerId, valley.TriggerId] : [valley.TriggerId, workshop.TriggerId]));
        await PayBoon(game);
        if (!workshopFirst) { await game.Top(); Assert.Empty(Minions(game)); Assert.Single(Boons(game)); }
        await game.Top(); var minion = Assert.Single(Minions(game));
        Assert.Equal("BF", game.State.ObjectLocations[minion.ObjectId].BattlefieldObjectId);
        // The minion really is played here, so it earns a separate optional boon.
        await PayBoon(game); await game.Top();
        if (workshopFirst) await game.Top();
        Assert.Equal(2, Boons(game).Length);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[minion.ObjectId].Tags);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["BORN"].Tags);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task SpringUsesOneOptionalTargetChoiceWithoutInventingAZeroCostPayment()
    {
        var game = new Driver(Position(Spring)); await Enter(game);
        var mana = game.State.RunePools["P1"].Mana; var prefix = game.Events().Length;
        await SelectReturn(game, ["ALLY"]);
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Null(Assert.Single(game.State.StackItems).TriggerCost);
        Assert.DoesNotContain(game.Events().Skip(prefix), entry => entry.Event.Kind == "PAYMENT_WINDOW_OPENED");
        await game.Top(); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task DeclineOrHavingNoOtherUnitStillConsumesSpringsFirstNonTokenPlay(bool otherHere)
    {
        var game = new Driver(Position(Spring, otherHere: otherHere)); await Enter(game);
        if (otherHere) await SelectReturn(game, []);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey));
        await Enter(game, id: "NEXT");
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TRIGGER_QUEUED"
            && entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ARealTurnChangeExpiresSpringsFirstEventAndTheNextTurnCanEarnItAgain()
    {
        var state = Position(Spring);
        for (var i = 0; i < 6; i++) state = RadGearTestPosition.Put(state, "SPRING-RUNE-" + i, "UNL-R01", "P1", "BASE");
        var game = new Driver(state); await Enter(game); await SelectReturn(game, []);
        await JungleAmbushReadyEntryTests.FinishTurn(game); await JungleAmbushReadyEntryTests.FinishTurn(game);
        Assert.Equal("P1", game.State.TurnPlayerId);
        await game.TapRunes("P1", Enumerable.Range(0, 6).Select(i => "SPRING-RUNE-" + i));
        await Enter(game, id: "NEXT");
        Assert.Contains("BORN", game.State.PendingCardChoice!.LegalObjectIds);
        await SelectReturn(game, ["ALLY"], playedUnit: "NEXT"); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey)));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(Spring, "BASE"), InlineData(Spring, "OTHER"), InlineData(Valley, "BASE"), InlineData(Valley, "OTHER")]
    public async Task ABaseOrDifferentBattlefieldPlayCannotBorrowTheObservedBattlefieldsSkill(string fieldNo, string destination)
    {
        var state = Position(fieldNo);
        state = RadUnitEffectTestFixture.Place(state, "OTHER", "P1", "BATTLEFIELD", "OTHER");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("BORN", Plain, [],
            Destination: destination == "BASE" ? "BASE" : "BATTLEFIELD:OTHER"));
        if (!game.State.PlayerZones["P1"].Base.Contains("BORN") && !game.State.PlayerZones["P1"].Battlefields.Contains("BORN"))
            await game.Top();
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldUnitPlayCaptureHashKey));
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnActualTokenPlayDoesNotConsumeSpringsFirstNonTokenEvent()
    {
        var state = Position(Spring);
        state = RadGearTestPosition.Put(state, "NEST", "UNL-160/219", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("NEST", P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, [])); await game.Top();
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey));
        await Enter(game); Assert.NotNull(game.State.PendingCardChoice);
        await SelectReturn(game, ["ALLY"]); await game.Top(); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task BothPlayersShareTheFirstNonTokenEventAtOneBattlefieldInTheSameTurn()
    {
        var state = Position(Spring, WardInvader, false);
        state = RadUnitEffectTestFixture.Place(state, "BF", "P2", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "AMBUSH", "UNL-060/219", "P2", "HAND");
        var game = new Driver(state); await Enter(game, WardInvader);
        Assert.Null(game.State.PendingCardChoice); // The playing P1 has no other unit here.
        Assert.Single(game.Events(), entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey));
        // A real P1 spell opens the response window in which P2 may Ambush.
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("AMBUSH", "UNL-060/219", [], Destination: "BATTLEFIELD:BF"));
        if (!game.State.PlayerZones["P2"].Battlefields.Contains("AMBUSH")) await game.Top();
        Assert.Contains("AMBUSH", game.State.PlayerZones["P2"].Battlefields);
        Assert.Single(game.Events(), entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey));
        Assert.DoesNotContain(game.State.StackItems, item => item.FieldContext?.Kind == CoreRuleEngine.BattlefieldFirstEntryMoveKind);
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.FieldContext?.Kind == CoreRuleEngine.BattlefieldFirstEntryMoveKind);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningTheSelectedUnitDuringResponsesCannotMoveADifferentUnitInstead()
    {
        var game = new Driver(Position(Spring)); await Enter(game); await SelectReturn(game, ["ALLY"]);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        await game.Top(); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("BF", game.State.ObjectLocations["BORN"].BattlefieldObjectId);
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("retarget"), InlineData("erase-target"), InlineData("generation"), InlineData("cost")]
    public async Task ConfirmedSpringTargetsRemainBoundToTheOriginalAcceptedConfirmation(string mutation)
    {
        var state = RadGearTestPosition.Put(Position(Spring), "SECOND-ALLY", Plain, "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state); await Enter(game); await SelectReturn(game, ["ALLY"]);
        var item = Assert.Single(game.State.StackItems);
        var changed = mutation switch {
            "retarget" => item with { TargetObjectIds = ["SECOND-ALLY"], TargetGenerations =
                new Dictionary<string, long> { ["SECOND-ALLY"] = game.State.CardObjects["SECOND-ALLY"].ObjectGeneration } },
            "erase-target" => item with { TargetObjectIds = [], TargetGenerations = null },
            "generation" => item with { TargetGenerations = new Dictionary<string, long> { ["ALLY"] = 999 } },
            _ => item with { TriggerCost = new(new("BF", game.State.CardObjects["BF"].ObjectGeneration)) } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [changed] }));
        await game.Top(); await game.ColdCheckpoint();
    }

    [Theory, InlineData("erase"), InlineData("duplicate"), InlineData("player"), InlineData("birth"),
     InlineData("field-generation"), InlineData("unit-generation"), InlineData("origin"), InlineData("kind"),
     InlineData("first-use-erase"), InlineData("first-use-copy")]
    public async Task CapturesAndFirstUsesCannotBeForgedAgainstTheUnmodifiedOriginalJournal(string mutation)
    {
        var game = new Driver(Position(Spring)); await Enter(game);
        var item = Assert.Single(game.State.StackItems); var field = item.FieldContext!;
        var origin = field.BattlefieldUnitPlay!;
        var changed = item with { ControllerId = mutation == "player" ? "P2" : item.ControllerId,
            FieldContext = mutation switch {
                "birth" => field with { BattlefieldUnitPlay = origin with { CapturedTick = origin.CapturedTick + 1 } },
                "field-generation" => field with { SourceGeneration = 999 },
                "unit-generation" => field with { BattlefieldUnitPlay = origin with { Unit = origin.Unit with { Generation = 999 } } },
                "origin" => field with { BattlefieldUnitPlay = null },
                "kind" => field with { Kind = CoreRuleEngine.BattlefieldEntryBoonKind }, _ => field } };
        var forged = mutation switch {
            "erase" => game.State with { StackItems = [], PendingCardChoice = null },
            "duplicate" => game.State with { StackItems = [item, item with { StackItemId = item.StackItemId + "-copy" }] },
            "first-use-erase" => game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects
                .Where(value => !value.StartsWith(CoreRuleEngine.BattlefieldFirstEntryUsePrefix, StringComparison.Ordinal)).ToArray() },
            "first-use-copy" => game.State with { UntilEndOfTurnEffects = [..game.State.UntilEndOfTurnEffects,
                game.State.UntilEndOfTurnEffects.Single(value => value.StartsWith(CoreRuleEngine.BattlefieldFirstEntryUsePrefix, StringComparison.Ordinal))] },
            _ => game.State with { StackItems = [changed] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await SelectReturn(game, []); await game.ColdCheckpoint();
    }
    [Fact]
    public async Task ValleysFixedReferenceSurvivesARealControlTransferAndRecallInTheSameIncarnation()
    {
        var state = Position(Valley, "UNL-060/219", false);
        state = RadUnitEffectTestFixture.Place(state, "BF", "P2", "BATTLEFIELD", "BF");
        // Kai'Sa in this actual showdown gives P2's Swift Take Reaction.
        state = RadGearTestPosition.Put(state, "ENEMY", "RAD·063/167", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "TAKE", "OGN·203/298", "P2", "HAND");
        state = state with { RunePools = state.RunePools.ToDictionary(pair => pair.Key,
            _ => new RunePool(40, 20, new Dictionary<string, int> { ["green"] = 10, ["blue"] = 10 })) };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.SpellDuelState.IsActive);
        await Enter(game, "UNL-060/219"); await PayBoon(game);
        Assert.True(game.State.SpellDuelState.IsActive);
        var generation = game.State.CardObjects["BORN"].ObjectGeneration;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["BORN"])); await game.Top();
        Assert.Equal("P2", game.State.CardObjects["BORN"].ControllerId);
        Assert.Contains("BORN", game.State.PlayerZones["P2"].Base);
        Assert.Equal(generation, game.State.CardObjects["BORN"].ObjectGeneration);
        await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["BORN"].Tags);
        Assert.Empty(game.State.StackItems); await game.ColdCheckpoint();
    }

    [Fact]
    public async Task SpringRespectsARealMovementRestrictionWithoutRestoringItsAlreadyUsedFirstEvent()
    {
        var state = Position(Spring);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.AllPlayers] } } };
        var game = new Driver(state); await Enter(game);
        Assert.Contains("ALLY", game.State.PendingCardChoice!.LegalObjectIds);
        await SelectReturn(game, ["ALLY"]); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BASE");
        await Enter(game, id: "NEXT");
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Single(game.Events(), entry => entry.Event.Payload.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("capture"), InlineData("erase"), InlineData("targets"), InlineData("first-use")]
    public async Task RecoveryChecksTheSkillInsideTheOriginOfAnActualTokenLocationChoice(string mutation)
    {
        var state = RadGearTestPosition.Put(Position(Spring), "OTHER-ALLY", Plain, "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "BIRDS", "UNL-044/219", "P1", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(40, 10, new Dictionary<string, int> { ["green"] = 2 }) } };
        var game = new Driver(state); await Enter(game); await SelectReturn(game, ["ALLY"]);
        await game.Act("P1", new PlayCardCommand("BIRDS", "UNL-044/219", [], Mode: "CREATE_WARHAWKS"));
        await game.Top(); var pending = game.State.PendingRuleChoice!; Assert.NotNull(pending);
        Assert.Equal(CoreRuleEngine.UnitTokenDestinationChoiceKind, pending.Request.Kind);
        var origin = pending.Origin;
        var item = Assert.Single(origin.StackItems, skill => skill.FieldContext?.Kind == CoreRuleEngine.BattlefieldFirstEntryMoveKind);
        var context = item.FieldContext!;
        var changed = mutation == "capture" ? item with { FieldContext = context with {
            BattlefieldUnitPlay = context.BattlefieldUnitPlay! with { CapturedTick = context.BattlefieldUnitPlay.CapturedTick + 1 } } }
            : item with { TargetObjectIds = ["OTHER-ALLY"], TargetGenerations = new Dictionary<string, long> {
                ["OTHER-ALLY"] = origin.CardObjects["OTHER-ALLY"].ObjectGeneration } };
        var changedOrigin = mutation == "first-use" ? origin with { UntilEndOfTurnEffects = origin.UntilEndOfTurnEffects
            .Where(value => !value.StartsWith(CoreRuleEngine.BattlefieldFirstEntryUsePrefix, StringComparison.Ordinal)).ToArray() }
            : origin with { StackItems = mutation == "erase" ? origin.StackItems.Where(skill => skill.StackItemId != item.StackItemId).ToArray()
                : origin.StackItems.Select(skill => skill.StackItemId == item.StackItemId ? changed : skill).ToArray() };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
            PendingRuleChoice = pending with { Origin = changedOrigin } }));
        for (var guard = 0; game.State.PendingRuleChoice is { } choice; guard++) {
            Assert.True(guard < 4); Assert.Equal(CoreRuleEngine.UnitTokenDestinationChoiceKind, choice.Request.Kind);
            await game.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
        await game.Top(); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.ColdCheckpoint();
    }

}
