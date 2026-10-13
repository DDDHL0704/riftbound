using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TurnUnitPlayGrantsTests
{
    internal const string Rally = "SFD·166/221";
    internal const string Confront = "OGN·129/298";

    [Fact]
    public async Task RealConfrontMakesSubsequentlyPlayedUnitsEnterReady()
    {
        var initial = Position(Confront); var driver = Driver(initial, "confront-entry");
        await driver.Act("P1", new PlayCardCommand("GRANT", Confront, [])); await driver.Top();
        Assert.Equal(1, driver.Events().Where(e => e.Event.Kind == "CARD_DRAWN").Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
        var played = await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·001/298", [], Destination: "BASE"));
        Assert.False(driver.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains(played.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "UNIT"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task RealRallyCreatesAnIndependentBoonTriggerWhenAFriendlyUnitIsPlayed()
    {
        var initial = Position(Rally); var driver = Driver(initial, "rally-boon");
        await driver.Act("P1", new PlayCardCommand("GRANT", Rally, [])); await driver.Top();
        var played = await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·001/298", [], Destination: "BASE"));
        Assert.DoesNotContain(CardObjectTags.Boon, driver.State.CardObjects["UNIT"].Tags);
        Assert.Single(driver.State.StackItems);
        Assert.Contains(played.Events, e => e.Kind == "TRIGGER_QUEUED");
        await driver.Top();
        Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects["UNIT"].Tags);
        Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task ActuallyPlayedSandSoldierTokensUseTheSameGrantAtEntryOrAfterTheirTriggeredWindow(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "SANDCRAFT", "SFD·031/221", "P1");
        var driver = Driver(initial, card == Confront ? "confront-token" : "rally-token");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("SANDCRAFT", "SFD·031/221", [])); await driver.Top();
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        if (card == Confront) Assert.False(token.IsExhausted);
        else
        {
            Assert.DoesNotContain(CardObjectTags.Boon, token.Tags); Assert.Single(driver.State.StackItems);
            await driver.Top(); Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects[token.ObjectId].Tags);
            Assert.Equal(3, driver.State.CardObjects[token.ObjectId].Power);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task TwoRallySpellsProduceTwoCapturedTriggersButOnlyOneBoon()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Rally), "GRANT2", Rally, "P1");
        var driver = Driver(initial, "two-rallies");
        await driver.Act("P1", new PlayCardCommand("GRANT", Rally, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("GRANT2", Rally, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·001/298", [], Destination: "BASE"));
        Assert.Equal(2, driver.State.TriggerQueue.Count);
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await driver.Top(); await driver.Top();
        Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(1, driver.State.CardObjects["UNIT"].Tags.Count(t => t == CardObjectTags.Boon));
        Assert.Equal(1, driver.Events().Count(e => e.Event.Kind == "BOON_GRANTED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ReturningThePlayedUnitBeforeItsRallyTriggerPreventsBoonAndKeepsItsPrivateHandHidden()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Rally), "RETURN", "OGN·104/298", "P1");
        var driver = Driver(initial, "rally-returned-unit");
        await driver.Act("P1", new PlayCardCommand("GRANT", Rally, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·001/298", [], Destination: "BASE"));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        var done = await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(done.Events, e => e.Kind == "BOON_GRANTED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task RealEchoCreatesTwoIndependentGrantsAndDrawsTwice(string card)
    {
        var initial = Position(card) with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = Driver(initial, card == Confront ? "confront-echo" : "rally-echo");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [], OptionalCosts: ["ECHO"]));
        await driver.Top();
        Assert.Equal(2, driver.State.UntilEndOfTurnEffects.Count(e => e.StartsWith("TURN_UNIT_PLAY_GRANT|")));
        Assert.Equal(2, driver.Events().Where(e => e.Event.Kind == "CARD_DRAWN").Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
        await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·001/298", [], Destination: "BASE"));
        if (card == Confront) Assert.False(driver.State.CardObjects["UNIT"].IsExhausted);
        else
        {
            Assert.Equal(2, driver.State.TriggerQueue.Count);
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            await driver.Top(); await driver.Top(); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task ARealCounterPreventsBothTheGrantAndDraw(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "COUNTER", "OGN·064/298", "P2");
        var driver = Driver(initial, card == Confront ? "confront-countered" : "rally-countered");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, []));
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [Assert.Single(driver.State.StackItems).StackItemId]));
        await driver.Top();
        Assert.DoesNotContain(driver.State.UntilEndOfTurnEffects, e => e.StartsWith("TURN_UNIT_PLAY_GRANT|"));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "CARD_DRAWN" or "TURN_UNIT_PLAY_GRANT_CREATED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task AUnitActuallyPlayedFromTheGraveyardUsesTheGrant(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "LOYALTY", "UNL-168/219", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["UNIT"] = initial.CardObjects["UNIT"] with { CardNo = "OGN·096/298", Power = 1, ManaCost = 2 } }, ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray(), Graveyard = ["UNIT"] } } };
        var driver = Driver(initial, card == Confront ? "confront-graveyard-play" : "rally-graveyard-play");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("LOYALTY", "UNL-168/219", ["UNIT"])); await driver.Top();
        Assert.NotNull(driver.State.PendingEffectPlay);
        await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·096/298", []));
        if (card == Confront) Assert.False(driver.State.CardObjects["UNIT"].IsExhausted);
        else { Assert.Single(driver.State.StackItems); await driver.Top(); Assert.Equal(2, driver.State.CardObjects["UNIT"].Power); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task ExistingUnitsAndRealMovementDoNotCountAsPlayingAUnit(string card)
    {
        var initial = LocalDestructionRecallTests.At(Position(card), "HOST", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "PURSUIT", "SFD·184/221", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = true } } };
        var driver = Driver(initial, card == Confront ? "confront-movement-excluded" : "rally-movement-excluded");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        await driver.Act("P1", new PlayCardCommand("PURSUIT", "SFD·184/221", ["HOST"], Destination: "BATTLEFIELD:F")); await driver.Top();
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.TriggerQueue);
        Assert.DoesNotContain(CardObjectTags.Boon, driver.State.CardObjects["HOST"].Tags);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task RealBanishThenPlayToBattlefieldUsesTheNewIncarnationAndCurrentGrant(string card)
    {
        var initial = Position(card);
        initial = AttackDamageDistributionTests.Spell(initial, "HUNT", "UNL-184/219", "P1");
        var originalPower = initial.CardObjects["HOST"].Power;
        var driver = Driver(initial, card == Confront ? "confront-battlefield-play" : "rally-battlefield-play");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"])); await driver.Top();
        Assert.Equal("BATTLEFIELD", driver.State.ObjectLocations["HOST"].Zone);
        Assert.True(driver.State.CardObjects["HOST"].ObjectGeneration > initial.CardObjects["HOST"].ObjectGeneration);
        if (card == Confront) Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        else { Assert.Single(driver.State.StackItems); await driver.Top(); Assert.Equal(originalPower + 1, driver.State.CardObjects["HOST"].Power); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task PlayingAnOpponentsTopDeckUnitUsesThePlayersGrantDespiteDifferentOwnership(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "IMPULSE", "OGN·025/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["OPPONENT_TOP"] = new("OPPONENT_TOP", cardNo:"OGN·096/298", power:1, manaCost:2, ownerId:"P2", controllerId:"P2", tags:[CardObjectTags.UnitCard]) },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) { ["OPPONENT_TOP"] = new("P2","MAIN_DECK") },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with {
                MainDeck = initial.PlayerZones["P2"].MainDeck.Prepend("OPPONENT_TOP").ToArray() } } };
        var driver = Driver(initial, card == Confront ? "confront-opponent-owned-play" : "rally-opponent-owned-play");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("IMPULSE", "OGN·025/298", ["OPPONENT_TOP"])); await driver.Top();
        Assert.Equal("P2", driver.State.CardObjects["OPPONENT_TOP"].OwnerId);
        Assert.Equal("P1", driver.State.CardObjects["OPPONENT_TOP"].ControllerId);
        if (card == Confront) Assert.False(driver.State.CardObjects["OPPONENT_TOP"].IsExhausted);
        else { await driver.Top(); Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects["OPPONENT_TOP"].Tags); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ReturningTheRallySpellToPrivateHandKeepsItsIndependentQueuedAndStackedBoonTriggers()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Rally), "ANNIE", "OGS·010/024", "P1");
        var driver = Driver(initial, "rally-source-private-hand");
        await driver.Act("P1", new PlayCardCommand("GRANT", Rally, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("ANNIE", "OGS·010/024", [], Destination: "BASE"));
        for (var step = 0; step < 12 && driver.State.ObjectLocations["GRANT"].Zone != "HAND"; step++)
        {
            if (driver.State.TriggerQueue.Count > 0)
                await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (driver.State.PendingCardChoice is { } choice)
                await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GRANT"]));
            else if (driver.State.PriorityPlayerId is { } priority) await driver.Act(priority, new PassPriorityCommand());
            else Assert.Fail("Annie must expose its graveyard return choice or response window.");
        }
        Assert.Equal("HAND", driver.State.ObjectLocations["GRANT"].Zone);
        while (driver.State.StackItems.Count > 0) await driver.Top();
        await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·001/298", [], Destination: "BASE")); await driver.Top();
        Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects["UNIT"].Tags);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task TheOpponentsActualEffectPlayInTheSameTurnDoesNotUseOurGrant(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "FUTURE", "OGN·115/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["OWN_TOP"] = new("OWN_TOP", cardNo:"OGN·096/298", power:1, manaCost:2, ownerId:"P1", controllerId:"P1", tags:[CardObjectTags.UnitCard]),
            ["OPPONENT_TOP"] = new("OPPONENT_TOP", cardNo:"OGN·096/298", power:1, manaCost:2, ownerId:"P2", controllerId:"P2", tags:[CardObjectTags.UnitCard]) },
            ObjectLocations = new Dictionary<string,ObjectLocationState>(initial.ObjectLocations) { ["OWN_TOP"] = new("P1","MAIN_DECK"), ["OPPONENT_TOP"] = new("P2","MAIN_DECK") },
            PlayerZones = new Dictionary<string,PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                MainDeck = initial.PlayerZones["P1"].MainDeck.Take(1).Concat(["OWN_TOP"]).Concat(initial.PlayerZones["P1"].MainDeck.Skip(1)).ToArray() }, ["P2"] = initial.PlayerZones["P2"] with {
                MainDeck = initial.PlayerZones["P2"].MainDeck.Prepend("OPPONENT_TOP").ToArray() } } };
        var driver = Driver(initial, card == Confront ? "confront-opponent-excluded" : "rally-opponent-excluded");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("FUTURE", "OGN·115/298", ["OWN_TOP", "OPPONENT_TOP"])); var resolved = await driver.Top();
        Assert.Equal(initial.TurnNumber, driver.State.TurnNumber);
        Assert.Equal("BASE", driver.State.ObjectLocations["OPPONENT_TOP"].Zone);
        Assert.False(driver.State.CardObjects["OPPONENT_TOP"].IsExhausted); // Bright Future itself plays both units ready.
        var opponentEntry = Assert.Single(resolved.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "OPPONENT_TOP"));
        Assert.False(opponentEntry.Payload.ContainsKey("turnUnitPlayReady"));
        Assert.DoesNotContain(CardObjectTags.Boon, driver.State.CardObjects["OPPONENT_TOP"].Tags);
        if (card == Confront) Assert.False(driver.State.CardObjects["OWN_TOP"].IsExhausted);
        else { var trigger = Assert.Single(driver.State.StackItems); Assert.Equal("OWN_TOP", trigger.TurnUnitPlayBoon!.Unit.ObjectId); await driver.Top(); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task AnActualDestructionReplacementRecallIsNotAUnitPlay(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "RECALL", "OGS·020/024", "P1");
        initial = LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "F");
        initial = AttackDamageDistributionTests.Spell(initial, "DAMAGE", "UNL-007/219", "P1");
        initial = initial with { CardObjects = new Dictionary<string,CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { Power = 2 } } };
        var driver = Driver(initial, card == Confront ? "confront-recall-excluded" : "rally-recall-excluded");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("RECALL", "OGS·020/024", ["HOST"])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("DAMAGE", "UNL-007/219", ["HOST"]));
        for (var step = 0; step < 8 && driver.State.ObjectLocations["HOST"].Zone != "BASE"; step++)
            if (driver.State.PendingRuleChoice is { } choice)
                await driver.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["RECALL:HOST"]));
            else await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal("BASE", driver.State.ObjectLocations["HOST"].Zone);
        Assert.True(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain(CardObjectTags.Boon, driver.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain(driver.State.StackItems, s => s.EffectKind == "DELAYED_FRIENDLY_UNIT_PLAYED_BOON");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("controller")]
    [InlineData("card")]
    [InlineData("generation")]
    [InlineData("execution")]
    [InlineData("missing")]
    public async Task StackAndQueuedPublicAndSpectatorRecoveryRejectForgedBoonCaptures(string mutation)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Rally), "GRANT2", Rally, "P1");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("GRANT", Rally, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("GRANT2", Rally, [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("UNIT", "OGN·001/298", [], Destination: "BASE"));
        foreach (var queue in new[] { true, false })
        {
            if (!queue) await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            var state = driver.State;
            var c = queue ? state.TriggerQueue[0].TurnUnitPlayBoon! : state.StackItems[0].TurnUnitPlayBoon!;
            TurnUnitPlayBoonContext? bad = mutation switch {
                "source" => c with { Grant = c.Grant with { Source = new("HOST", 0) } },
                "target" => c with { Unit = new("HOST", 0) }, "controller" => c with { UnitControllerId = "P2" },
                "card" => c with { UnitCardNo = Confront }, "generation" => c with { Unit = c.Unit with { Generation = 999999 } },
                "execution" => c with { Grant = c.Grant with { ExecutionIndex = 999999 } }, _ => null };
            var corrupted = queue ? state with { TriggerQueue = state.TriggerQueue.Select((t,i) => i == 0 ? t with { TurnUnitPlayBoon = bad } : t).ToArray() }
                : state with { StackItems = state.StackItems.Select((t,i) => i == 0 ? t with { TurnUnitPlayBoon = bad } : t).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(corrupted), e => e.Contains("unit play boon"));
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
            foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
            {
                var rows = queue ? Assert.IsType<Dictionary<string,object?>>(snapshot.Timing)["triggerQueue"] as IEnumerable<object?> : snapshot.Stack;
                var row = Assert.IsType<Dictionary<string,object?>>(rows!.First()); var old = row["turnUnitPlayBoon"];
                row["turnUnitPlayBoon"] = bad;
                Assert.NotEmpty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
                row["turnUnitPlayBoon"] = old;
            }
        }
    }

    [Theory]
    [InlineData(Confront)]
    [InlineData(Rally)]
    public async Task TheGrantExpiresAtRealTurnEndBeforeTheOpponentsNextTurnPlay(string card)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(card), "ENEMY_UNIT", "OGN·001/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY_UNIT"] = initial.CardObjects["ENEMY_UNIT"] with { Power = 5, Tags = [CardObjectTags.UnitCard] } } };
        foreach (var id in Enumerable.Range(1,5).Select(i => "ENEMY-RUNE-" + i))
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [id] = new(id, cardNo: "UNL-R01", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new("P2", "BASE") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with {
                    Base = initial.PlayerZones["P2"].Base.Append(id).ToArray() } } };
        var driver = Driver(initial, card == Confront ? "confront-expiry" : "rally-expiry");
        await driver.Act("P1", new PlayCardCommand("GRANT", card, [])); await driver.Top();
        var next = await driver.Act("P1", new EndTurnCommand());
        await TurnSequenceTestDriver.Complete(next, (player, command) => new(driver.Act(player, command)));
        Assert.DoesNotContain(driver.State.UntilEndOfTurnEffects, e => e.StartsWith("TURN_UNIT_PLAY_GRANT|"));
        foreach (var rune in driver.State.PlayerZones["P2"].Base.Where(id => driver.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
            && !driver.State.CardObjects[id].IsExhausted).Take(5).ToArray()) await driver.Act("P2", new TapRuneCommand(rune));
        await driver.Act("P2", new PlayCardCommand("ENEMY_UNIT", "OGN·001/298", [], Destination: "BASE"));
        Assert.True(driver.State.CardObjects["ENEMY_UNIT"].IsExhausted);
        Assert.DoesNotContain(CardObjectTags.Boon, driver.State.CardObjects["ENEMY_UNIT"].Tags);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("turn")]
    [InlineData("controller")]
    [InlineData("source")]
    [InlineData("source-generation")]
    [InlineData("card")]
    [InlineData("stack")]
    [InlineData("execution")]
    [InlineData("kind")]
    [InlineData("duplicate")]
    public async Task RecoveryRejectsIndependentTemporaryPlayGrantProvenanceMutations(string mutation)
    {
        var driver = new StandbyDefendObserverTests.Driver(Position(Rally));
        await driver.Act("P1", new PlayCardCommand("GRANT", Rally, [])); await driver.Top();
        var state = driver.State; var marker = Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith("TURN_UNIT_PLAY_GRANT|"));
        var f = marker.Split('|'); string Encode(string v) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(v));
        if (mutation == "turn") f[1] = "999999";
        if (mutation == "controller") f[2] = Encode("FORGED");
        if (mutation == "source") f[3] = Encode("HOST");
        if (mutation == "source-generation") f[4] = "999999";
        if (mutation == "card") f[5] = Encode(Confront);
        if (mutation == "stack") f[6] = Encode("FORGED");
        if (mutation == "execution") f[7] = "-1";
        if (mutation == "kind") f[8] = "READY_ENTRY";
        var corrupted = state with { UntilEndOfTurnEffects = mutation == "duplicate" ? [..state.UntilEndOfTurnEffects, marker]
            : state.UntilEndOfTurnEffects.Select(e => e == marker ? string.Join('|', f) : e).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(corrupted), e => e.Contains("temporary unit play grant"));
    }

    internal static MatchState Position(string card)
    {
        var s = AttackDamageDistributionTests.Spell(SacredProtectorTests.Position(), "GRANT", card, "P1");
        s = AttackDamageDistributionTests.Spell(s, "UNIT", "OGN·001/298", "P1");
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["UNIT"] = s.CardObjects["UNIT"] with { Tags = [CardObjectTags.UnitCard], Power = 5 } },
            RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(30,30), ["P2"] = new(30,30) } };
    }

    internal static StandbyDefendObserverTests.Driver Driver(MatchState state, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_TURN_UNIT_GRANT_EVIDENCE");
        return new(state, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
