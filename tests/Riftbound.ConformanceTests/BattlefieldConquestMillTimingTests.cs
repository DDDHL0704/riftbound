using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldConquestMillTimingTests
{
    [Theory]
    [InlineData("P1", 0)] [InlineData("P1", 1)] [InlineData("P1", 2)] [InlineData("P1", 3)]
    [InlineData("P2", 0)] [InlineData("P2", 1)] [InlineData("P2", 2)] [InlineData("P2", 3)]
    public async Task ConquestQueuesTheMillBeforeEitherPlayerCanRespond(string player, int available)
    {
        var initial = Position(player, available);
        AssertCanonicalComponent(initial);
        var result = await Conquer(initial, player);
        WriteDiagnostic($"pre-response-{player}-{available}", initial, result);
        Assert.Contains(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.DoesNotContain(result.Events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED");
        Assert.Equal(initial.PlayerZones[player].MainDeck, result.State.PlayerZones[player].MainDeck);
        Assert.Equal(initial.PlayerZones[player].Graveyard, result.State.PlayerZones[player].Graveyard);
        Assert.True(result.State.StackItems.Any(i => i.EffectKind == TriggerKinds.BattlefieldConquerMill && i.SourceObjectId == "BF")
            || result.State.TriggerQueue.Any(t => t.EffectKind == TriggerKinds.BattlefieldConquerMill && t.SourceObjectId == "BF"));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task BattlefieldWithoutTheAbilityDoesNotQueueOrMill(string player)
    {
        var initial = Position(player, 3);
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { CardNo = "OGN·296/298" } };
        initial = initial with { CardObjects = cards };
        var result = await Conquer(initial, player);
        Assert.Contains(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.DoesNotContain(result.Events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED");
        Assert.DoesNotContain(result.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerMill);
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.EffectKind == TriggerKinds.BattlefieldConquerMill);
        Assert.Equal(initial.PlayerZones[player].MainDeck, result.State.PlayerZones[player].MainDeck);
    }

    [Theory]
    [InlineData("P1", 0)] [InlineData("P1", 1)] [InlineData("P1", 2)] [InlineData("P1", 3)]
    [InlineData("P2", 0)] [InlineData("P2", 1)] [InlineData("P2", 2)] [InlineData("P2", 3)]
    public async Task ResponsesPrecedeMillAndShortDeckRecyclesTheCurrentGraveyard(string player, int available)
    {
        var before = await Conquer(Position(player, available), player);
        AssertCanonicalComponent(before.State);
        var opponent = player == "P1" ? "P2" : "P1";
        var first = await Act(before.State, before.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.DoesNotContain(first.Events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED");
        Assert.Equal(before.State.PlayerZones[player].MainDeck, first.State.PlayerZones[player].MainDeck);
        Assert.Equal(before.State.PlayerZones[player].Graveyard, first.State.PlayerZones[player].Graveyard);
        AssertCanonicalComponent(first.State);
        var resolved = await Act(first.State, first.State.PriorityPlayerId!, new PassPriorityCommand());
        WriteDiagnostic($"resolved-{player}-{available}", before.State, resolved);
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "CARD_DRAWN");
        Assert.Equal(2, resolved.Events.Where(e => e.Kind == "CARDS_MILLED")
            .Sum(e => Assert.IsType<int>(e.Payload["count"])));
        Assert.Equal(available < 2 ? 1 : 0, resolved.Events.Count(e => e.Kind == "BURNOUT_APPLIED"));
        Assert.Equal(before.State.PlayerScores[opponent] + (available < 2 ? 1 : 0), resolved.State.PlayerScores[opponent]);
        Assert.Equal(available == 0 ? 1 : available == 1 ? 2 : available - 2, resolved.State.PlayerZones[player].MainDeck.Count);
        Assert.Equal(available == 0 ? 2 : available == 1 ? 1 : 5 - available, resolved.State.PlayerZones[player].Graveyard.Count);
        Assert.Equal(before.State.RngCursor + (available < 2 ? 1 : 0), resolved.State.RngCursor);
        if (available == 1)
        {
            var kinds = resolved.Events.Select(e => e.Kind).ToArray();
            Assert.True(Array.IndexOf(kinds, "CARDS_MILLED") < Array.IndexOf(kinds, "BURNOUT_APPLIED"));
            Assert.Equal("DRAW1", Assert.IsAssignableFrom<IReadOnlyList<string>>(
                resolved.Events.First(e => e.Kind == "CARDS_MILLED").Payload["cardIds"])[0]);
            Assert.True(Array.LastIndexOf(kinds, "CARDS_MILLED") > Array.IndexOf(kinds, "BURNOUT_APPLIED"));
        }
        AssertCanonicalComponent(resolved.State);
    }

    [Fact]
    public async Task CapturedConquestMillRejectsForgedIdentityAndTick()
    {
        var pending = (await Conquer(Position("P1", 3), "P1")).State;
        var item = Assert.Single(pending.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerMill);
        var context = Assert.IsType<BattlefieldConquestMillContext>(item.BattlefieldConquestMill);
        AssertCanonicalComponent(pending);
        foreach (var altered in new[] {
            context with { CardNo = "OGN·296/298" }, context with { ControllerId = "P2" },
            context with { ConquestSourceObjectId = "RUNE" }, context with { BattlefieldGeneration = context.BattlefieldGeneration + 1 },
            context with { ConquestTick = context.ConquestTick - 1 } })
        {
            var forged = pending with { StackItems = pending.StackItems.Select(i => i == item
                ? i with { BattlefieldConquestMill = altered } : i).ToArray() };
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(forged));
        }
        var unrelatedInstruction = pending with { StackItems = pending.StackItems.Select(i => i == item
            ? i with { DamageAmount = 3 } : i).ToArray() };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(unrelatedInstruction));
    }

    [Fact]
    public async Task RecoveryRejectsForgedConquestHistoryAndExtraPublicContextFields()
    {
        var initial = Position("P1", 3);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        var state = driver.State;
        var sequence = driver.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id,
            state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var forgedEvents = driver.Events().Select(e => e.Event.Kind == "BATTLEFIELD_CONQUERED"
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) {
                ["sourceObjectId"] = "RUNE" } } } : e).ToArray();
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, driver.Commands(), forgedEvents,
            views, state, state.Tick, spectator);
        Assert.Contains(errors, e => e.Contains("not bound to accepted conquest history", StringComparison.Ordinal));
        var forgedQueueEvent = driver.Events().Select(e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), TriggerKinds.BattlefieldConquerMill)
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) {
                ["controllerId"] = "P2" } } } : e).ToArray();
        var queueErrors = MatchRecoveryValidator.Validate(state.RoomId, sequence, driver.Commands(), forgedQueueEvent,
            views, state, state.Tick, spectator);
        Assert.Contains(queueErrors, e => e.Contains("not bound to accepted conquest history", StringComparison.Ordinal));

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var item = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(snapshots["P2"].Stack.Single(), options))!;
        var publicContext = JsonSerializer.Deserialize<Dictionary<string, object?>>(item["battlefieldConquestMill"].GetRawText())!;
        publicContext["conquestSourceObjectId"] = "UNIT";
        item["battlefieldConquestMill"] = JsonSerializer.SerializeToElement(publicContext, options);
        var altered = new Dictionary<string, RecoveredPlayerView>(views) {
            ["P2"] = views["P2"] with { Snapshot = snapshots["P2"] with { Stack = [JsonSerializer.SerializeToElement(item, options)] } } };
        var publicErrors = MatchRecoveryValidator.Validate(state.RoomId, sequence, driver.Commands(), driver.Events(),
            altered, state, state.Tick, spectator);
        Assert.Contains(publicErrors, e => e.Contains("invalid public captured battlefield conquest mill", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExpiredPublicResolutionProjectionStillRequiresAcceptedBirthHistory()
    {
        var pending = (await Conquer(Position("P1", 3), "P1")).State;
        var prior = Assert.Single(pending.BattlefieldResolutions, r => r.Kind == "CONQUERED");
        // This is a synthetic retention-boundary component state, not a
        // claim that twelve later conquests were played in this fixture.
        var retained = Enumerable.Range(1, 12).Select(i => prior with {
            Tick = pending.Tick + i,
            ResolutionId = $"battlefield-result:{pending.Tick + i}:0:CONQUERED:BF" }).Reverse().ToArray();
        var expired = pending with { Tick = pending.Tick + 12, BattlefieldResolutions = retained };
        var componentErrors = MatchRecoveryValidator.ValidateComponentStateAndViews(expired);
        Assert.DoesNotContain(componentErrors, e => e == "invalid captured battlefield conquest mill stack");
        var fullErrors = MatchRecoveryValidator.Validate(expired.RoomId, 0, [], [],
            new Dictionary<string, RecoveredPlayerView>(), expired, expired.Tick);
        Assert.Contains(fullErrors, e => e.Contains("not bound to accepted conquest history", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1", 0)] [InlineData("P1", 1)] [InlineData("P1", 2)] [InlineData("P1", 3)]
    [InlineData("P2", 0)] [InlineData("P2", 1)] [InlineData("P2", 2)] [InlineData("P2", 3)]
    public async Task NativeConquestMillSurvivesEveryColdRestoreAndJournalReplay(string player, int available)
    {
        var initial = Position(player, available);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var other = player == "P1" ? "P2" : "P1";
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        var pending = await driver.Act(other, new PassFocusCommand());
        Assert.DoesNotContain(pending.Events, e => e.Kind is "CARDS_MILLED" or "BURNOUT_APPLIED");
        var done = await driver.Top();
        Assert.Equal(2, done.Events.Where(e => e.Kind == "CARDS_MILLED")
            .Sum(e => Assert.IsType<int>(e.Payload["count"])));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task OtherConquestSkillCanReplaceBattlefieldBeforeTheCapturedMillResolves()
    {
        var initial = Position("P1", 3);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: "UNL-195/219", ownerId: "P1", controllerId: "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        var ivern = Assert.Single(driver.State.TriggerQueue, t => t.EffectKind == "HOLD_IVERN");
        var mill = Assert.Single(driver.State.TriggerQueue, t => t.EffectKind == TriggerKinds.BattlefieldConquerMill);
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: [ivern.TriggerId, mill.TriggerId]));
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["LEGEND"]));
        var replaced = await driver.Top();
        Assert.Contains(replaced.Events, e => e.Kind == "BATTLEFIELD_REPLACED");
        Assert.Equal("UNL·T03", driver.State.CardObjects["BF"].CardNo);
        Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerMill);
        var done = await driver.Top();
        Assert.Contains(done.Events, e => e.Kind == "CARDS_MILLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task OpponentCanReturnConqueringUnitWhileMillWaitsWithoutLeakingItsCurrentPrivateIdentity()
    {
        var initial = Position("P1", 3);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["UNIT"] = initial.CardObjects["UNIT"] with { Power = 1 },
                ["GUST"] = new("GUST", cardNo: "OGN·169/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Hand = ["GUST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GUST"] = new("P2", "HAND") },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("GUST", "OGN·169/298", ["UNIT"]));
        await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerMill);
        var done = await driver.Top();
        Assert.Contains(done.Events, e => e.Kind == "CARDS_MILLED");
        Assert.Equal("HIDDEN", Assert.Single(done.Events, e => e.Kind == "BATTLEFIELD_TRIGGER_RESOLVED").Payload["sourceObjectId"]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalConquestPointIsReplacedOrWinsBeforeTheMillAccordingToOtherBattlefieldScore(bool otherScored)
    {
        var initial = Position("P1", 3);
        initial = initial with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Battlefields = ["OTHER"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["OTHER"] = new("OTHER", cardNo: "OGN·280/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER") },
            UntilEndOfTurnEffects = otherScored ? [BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P1")] : [] };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        var conquest = await driver.Act("P2", new PassFocusCommand());
        Assert.Contains(conquest.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.DoesNotContain(conquest.Events, e => e.Kind == "CARDS_MILLED");
        if (otherScored)
        {
            Assert.Equal(MatchStatuses.Finished, driver.State.Status);
            Assert.Equal("P1", driver.State.WinnerPlayerId);
            Assert.Equal(initial.PlayerZones["P1"].MainDeck, driver.State.PlayerZones["P1"].MainDeck);
        }
        else
        {
            Assert.Single(conquest.Events, e => e.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
            Assert.Equal(7, driver.State.PlayerScores["P1"]);
            Assert.Contains("DRAW1", driver.State.PlayerZones["P1"].Hand);
            Assert.Single(driver.State.StackItems, i => i.EffectKind == TriggerKinds.BattlefieldConquerMill);
            var done = await driver.Top();
            Assert.Contains(done.Events, e => e.Kind == "CARDS_MILLED");
            Assert.Equal(new[] { "DRAW2", "DRAW3" }, driver.State.PlayerZones["P1"].Graveyard);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task EmptyDeckAndGraveyardBurnsOutUntilOpponentWinsImmediately(string player)
    {
        var initial = Position(player, 0);
        var opponent = player == "P1" ? "P2" : "P1";
        var removed = new[] { "DRAW1", "DRAW2", "DRAW3" }.ToHashSet(StringComparer.Ordinal);
        initial = initial with {
            CardObjects = initial.CardObjects.Where(e => !removed.Contains(e.Key)).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(e => !removed.Contains(e.Key)).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Graveyard = [] } },
            PlayerScores = new Dictionary<string, int> { [player] = 0, [opponent] = 6 } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(opponent, new PassFocusCommand());
        var done = await driver.Top();
        Assert.Equal(2, done.Events.Count(e => e.Kind == "BURNOUT_APPLIED"));
        Assert.DoesNotContain(done.Events, e => e.Kind is "CARDS_MILLED" or "CARD_DRAWN");
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Equal(opponent, driver.State.WinnerPlayerId);
        Assert.Equal(8, driver.State.PlayerScores[opponent]);
        Assert.Single(done.Events, e => e.Kind == "MATCH_WON");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task OneCardAndEmptyGraveyardRecyclesTheCardAlreadyMilled(string player)
    {
        var initial = Position(player, 1);
        var other = player == "P1" ? "P2" : "P1";
        var removed = new[] { "DRAW2", "DRAW3" }.ToHashSet(StringComparer.Ordinal);
        initial = initial with {
            CardObjects = initial.CardObjects.Where(e => !removed.Contains(e.Key)).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(e => !removed.Contains(e.Key)).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Graveyard = [] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(other, new PassFocusCommand());
        var done = await driver.Top();
        var milled = done.Events.Where(e => e.Kind == "CARDS_MILLED").ToArray();
        Assert.Equal(2, milled.Length);
        Assert.All(milled, e => Assert.Equal(new[] { "DRAW1" }, Assert.IsAssignableFrom<IReadOnlyList<string>>(e.Payload["cardIds"])));
        Assert.Single(done.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Empty(driver.State.PlayerZones[player].MainDeck);
        Assert.Equal(new[] { "DRAW1" }, driver.State.PlayerZones[player].Graveyard);
        Assert.DoesNotContain(done.Events, e => e.Kind == "CARD_DRAWN");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("P1", 0, "DRAW1")] [InlineData("P1", 0, "DRAW2")]
    [InlineData("P2", 1, "DRAW1")] [InlineData("P2", 1, "DRAW2")]
    public async Task RecyclingAndMillingBackToTheOriginalZoneCreatesANewObject(string player, long seed, string cardId)
    {
        // Controlled position: one card in the main deck and one in the trash.
        var initial = Position(player, 1);
        var opponent = player == "P1" ? "P2" : "P1";
        initial = initial with {
            Seed = seed,
            CardObjects = initial.CardObjects.Where(e => e.Key != "DRAW3").ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(e => e.Key != "DRAW3").ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                [player] = initial.PlayerZones[player] with { Graveyard = ["DRAW2"] } },
            PlayerScores = new Dictionary<string, int> { [player] = 0, [opponent] = 5 } };
        AssertCanonicalComponent(initial);
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_SFD212_IDENTITY_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial,
            evidence is null ? null : Path.Combine(evidence, $"single-burnout-{player}-{cardId}"));
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(opponent, new PassFocusCommand());
        var resolved = await driver.Top();
        var milled = resolved.Events.Where(e => e.Kind == "CARDS_MILLED").ToArray();
        Assert.Equal(2, milled.Length);
        Assert.Equal(new[] { "DRAW1" }, Assert.IsAssignableFrom<IReadOnlyList<string>>(milled[0].Payload["cardIds"]));
        Assert.Equal(new[] { "DRAW2" }, Assert.IsAssignableFrom<IReadOnlyList<string>>(milled[1].Payload["cardIds"]));
        Assert.Single(resolved.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(6, driver.State.PlayerScores[opponent]);
        Assert.Equal(new[] { "DRAW1" }, driver.State.PlayerZones[player].MainDeck);
        Assert.Equal(new[] { "DRAW2" }, driver.State.PlayerZones[player].Graveyard);
        AssertCanonicalComponent(driver.State);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        driver.Export("identity-diagnostic.json", new { initial, final = driver.State,
            commands = driver.Commands(), events = driver.Events(), resolutionEvents = resolved.Events });
        Assert.NotEqual(initial.CardObjects[cardId].ObjectGeneration, driver.State.CardObjects[cardId].ObjectGeneration);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task ARegularMillChangesOnlyTheMovedCardsIdentities(string player)
    {
        var initial = Position(player, 3);
        var opponent = player == "P1" ? "P2" : "P1";
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(opponent, new PassFocusCommand());
        var resolved = await driver.Top();
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(new[] { "DRAW3" }, driver.State.PlayerZones[player].MainDeck);
        Assert.Equal(new[] { "DRAW1", "DRAW2" }, driver.State.PlayerZones[player].Graveyard);
        foreach (var id in new[] { "DRAW1", "DRAW2" })
            Assert.NotEqual(initial.CardObjects[id].ObjectGeneration, driver.State.CardObjects[id].ObjectGeneration);
        Assert.Equal(initial.CardObjects["DRAW3"].ObjectGeneration, driver.State.CardObjects["DRAW3"].ObjectGeneration);
        AssertCanonicalComponent(driver.State);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task FirstBurnoutCompletesTheMillBeforeNormalScoreVictory(string player)
    {
        var initial = Position(player, 1);
        var opponent = player == "P1" ? "P2" : "P1";
        initial = initial with { PlayerScores = new Dictionary<string, int> { [player] = 0, [opponent] = 7 } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act(player, new PassFocusCommand());
        await driver.Act(opponent, new PassFocusCommand());
        var done = await driver.Top();
        var kinds = done.Events.Select(e => e.Kind).ToArray();
        Assert.Single(done.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(2, done.Events.Where(e => e.Kind == "CARDS_MILLED")
            .Sum(e => Assert.IsType<int>(e.Payload["count"])));
        Assert.True(Array.LastIndexOf(kinds, "CARDS_MILLED") > Array.IndexOf(kinds, "BURNOUT_APPLIED"));
        Assert.True(Array.IndexOf(kinds, "MATCH_WON") > Array.LastIndexOf(kinds, "CARDS_MILLED"));
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Equal(opponent, driver.State.WinnerPlayerId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState Position(string player, int available)
    {
        var state = ConquestLifecycleRegressionTests.State("SFD·212/221", 3);
        var zones = state.PlayerZones.ToDictionary();
        var deck = new[] { "DRAW1", "DRAW2", "DRAW3" };
        zones["P1"] = zones["P1"] with { MainDeck = deck.Take(available).ToArray(), Graveyard = deck.Skip(available).ToArray() };
        var cards = state.CardObjects.ToDictionary();
        if (player == "P2")
        {
            static string? Swap(string? id) => id == "P1" ? "P2" : id == "P2" ? "P1" : id;
            cards = cards.ToDictionary(entry => entry.Key, entry => entry.Value with {
                OwnerId = Swap(entry.Value.OwnerId), ControllerId = Swap(entry.Value.ControllerId) });
            zones = new Dictionary<string, PlayerZones> { ["P1"] = zones["P2"], ["P2"] = zones["P1"] };
        }
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal);
        foreach (var (owner, area) in zones)
        {
            foreach (var id in area.Base) locations[id] = new(owner, "BASE");
            foreach (var id in area.Hand) locations[id] = new(owner, "HAND");
            foreach (var id in area.MainDeck) locations[id] = new(owner, "MAIN_DECK");
            foreach (var id in area.Graveyard) locations[id] = new(owner, "GRAVEYARD");
            foreach (var id in area.Battlefields) locations[id] = new(owner, "BATTLEFIELD", id);
        }
        Assert.Equal(cards.Keys.Order(StringComparer.Ordinal), locations.Keys.Order(StringComparer.Ordinal));
        return state with { RoomId = $"SFD212-{player}-{available}", ActivePlayerId = player, TurnPlayerId = player,
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
    }

    private static void AssertCanonicalComponent(MatchState state)
    {
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(restored));
    }

    private static async Task<ResolutionResult> Conquer(MatchState state, string player)
    {
        var other = player == "P1" ? "P2" : "P1";
        var moved = await Act(state, player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        var first = await Act(moved.State, player, new PassFocusCommand());
        return await Act(first.State, other, new PassFocusCommand());
    }

    private static async Task<ResolutionResult> Act(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent($"sfd212-{player}-{state.Tick}", player, command.CmdType), command, CancellationToken.None);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }

    private static void WriteDiagnostic(string name, MatchState initial, ResolutionResult result)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_SFD212_DIAGNOSTICS") is not { } folder) return;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(new {
            initial, result.Accepted, result.ErrorMessage, final = result.State, result.Events, result.Prompts
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
