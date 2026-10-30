using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadNextUnitPlayGrantTests
{
    private const string Spell = "RAD·154/167", Unit = "OGN·001/298";
    private const string Used = "TURN_UNIT_PLAY_GRANT_USED|";

    private static MatchState Position()
    {
        var state = TurnUnitPlayGrantsTests.Position(Spell);
        state = HandDiscardObserverTests.Put(state, "FOLLOW", Unit, "P1", "HAND");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        foreach (var id in new[] { "OTHER", "OPEN" }) {
            cards[id] = new(id, cardNo: "OGN·278/298", ownerId: "P2", controllerId: id == "OTHER" ? "P2" : null,
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag]);
            locations[id] = new("P2", "BATTLEFIELD", id);
            zones["P2"] = zones["P2"] with { Battlefields = zones["P2"].Battlefields.Append(id).Distinct(StringComparer.Ordinal).ToArray() };
        }
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    private static async Task Grant(Driver game, string source = "GRANT")
    {
        await game.Act("P1", new PlayCardCommand(source, Spell, [])); await game.Top();
    }

    private static async Task ChooseTokenLocation(Driver game, string destination)
    {
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("TOKEN_PLAY_LOCATION", choice.Request.Kind);
        await game.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [destination]));
    }

    private static async Task CloseShowdown(Driver game)
    {
        for (var step = 0; step < 12 && game.State.SpellDuelState.IsActive; step++) {
            if (game.State.StackItems.Count > 0) await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            else await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.False(game.State.SpellDuelState.IsActive);
    }

    [Fact]
    public async Task OfficialPrintingKeepsItsReactionAndNextUnitText()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Spell);
        Assert.Equal(39662, card.Id); Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy);
        Assert.Equal("最终抚慰", card.CardName); Assert.Equal("伊芙琳", card.Hero);
        Assert.Contains("下一名单位", card.CardEffect); Assert.Contains("正面朝下", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Spell, out var behavior));
        Assert.True(behavior.CanPlayDuringPriority); Assert.True(behavior.TurnUnitPlayGrantNextOnly);
        Assert.True(behavior.TurnUnitPlayGrantAnyBattlefield);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:F")] [InlineData("BATTLEFIELD:OTHER")] [InlineData("BATTLEFIELD:OPEN")]
    public async Task TheNextUnitCanEnterAnyBattlefieldOrBaseAndGetsOneRespondableBoon(string destination)
    {
        var game = new Driver(Position()); await Grant(game);
        await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: destination));
        var at = game.State.ObjectLocations["UNIT"];
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", at.Zone);
        if (destination != "BASE") Assert.Equal(destination[12..], at.BattlefieldObjectId);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.Single(game.State.StackItems, item => item.TurnUnitPlayBoon is not null);
        Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await CloseShowdown(game);
        await Reject(game.State, new("FOLLOW", Unit, [], Destination:
            destination == "BATTLEFIELD:OPEN" ? "BATTLEFIELD:OTHER" : "BATTLEFIELD:OPEN"));
        await game.Act("P1", new PlayCardCommand("FOLLOW", Unit, [], Destination: "BASE"));
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["FOLLOW"].Tags);
        Assert.DoesNotContain(game.State.StackItems, item => item.TurnUnitPlayBoon is not null);
        await game.Replay();
    }

    [Fact]
    public async Task InvalidLocationsAndAnUnpaidPlayDoNotConsumeTheGrant()
    {
        var game = new Driver(Position()); await Grant(game);
        await Reject(game.State, new("UNIT", Unit, [], Destination: "BATTLEFIELD:INVENTED"));
        var poor = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(poor, new("UNIT", Unit, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BASE")); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AnExplicitBattlefieldProhibitionStillRejectsTheGrantedPlay()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { CardNo = "SFD·216/221" } } };
        var game = new Driver(state); await Grant(game);
        await Reject(game.State, new("UNIT", Unit, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        await game.Replay();
    }

    [Fact]
    public async Task ARealFaceDownUnitCanUseTheNextUnitPermissionAtAnotherBattlefield()
    {
        var initial = HandDiscardObserverTests.Put(StandbySourceRulesTests.Position("OGN·135/298"), "GRANT", Spell, "P1", "HAND");
        foreach (var n in new[] { 1, 2 }) {
            var id = "NEXT-UNIT-MANA-" + n;
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [id] = new(id, cardNo: "UNL-R03", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:blue"]) },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new("P1", "BASE") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                    Base = initial.PlayerZones["P1"].Base.Append(id).ToArray() } } };
        }
        var game = new Driver(await StandbySourceRulesTests.ReadyShadow("OGN·135/298", initial));
        await Reject(game.State, new("SHADOW", "OGN·135/298", [], Destination: "BATTLEFIELD:OTHER"));
        foreach (var n in new[] { 1, 2 }) await game.Act("P1", new TapRuneCommand("NEXT-UNIT-MANA-" + n));
        await Grant(game);
        await game.Act("P1", new PlayCardCommand("SHADOW", "OGN·135/298", [], Destination: "BATTLEFIELD:OTHER"));
        Assert.False(game.State.CardObjects["SHADOW"].IsFaceDown);
        Assert.Equal("OTHER", game.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["SHADOW"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task AnUnusedPermissionExpiresBeforeTheNextTurn()
    {
        var game = new Driver(Position()); await Grant(game);
        await FaceDownPlayTestDriver.NextTurn(game);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith("TURN_UNIT_PLAY_GRANT", StringComparison.Ordinal));
        await FaceDownPlayTestDriver.NextTurn(game);
        await Reject(game.State, new("UNIT", Unit, [], Destination: "BATTLEFIELD:OTHER")); await game.Replay();
    }

    [Fact]
    public async Task SpellsDoNotConsumeTheNextUnitAndTheGrantDoesNotChangeUnitTiming()
    {
        var state = HandDiscardObserverTests.Put(Position(), "OTHER-SPELL", "OGN·048/298", "P1", "HAND");
        var game = new Driver(state); await Grant(game);
        await game.Act("P1", new PlayCardCommand("OTHER-SPELL", "OGN·048/298", []));
        await Reject(game.State, new("UNIT", Unit, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BASE")); await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MultipleGrantsObserveTheSameNextUnitAndNeverApplyToTheFollowingUnit(bool echo)
    {
        var state = HandDiscardObserverTests.Put(Position(), "GRANT2", Spell, "P1", "HAND");
        if (echo) state = state with { UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Append(EchoCostRules.GrantPrefix + "P1").ToArray() };
        var game = new Driver(state);
        if (echo) {
            await game.Act("P1", new PlayCardCommand("GRANT", Spell, [], OptionalCosts: ["ECHO"])); await game.Top();
        }
        else { await Grant(game); await Grant(game, "GRANT2"); }
        await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BASE"));
        Assert.Equal(2, game.State.TriggerQueue.Count); Assert.Equal(2, game.State.UntilEndOfTurnEffects.Count(e => e.StartsWith(Used, StringComparison.Ordinal)));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await game.Top(); await game.Top();
        Assert.Single(game.State.CardObjects["UNIT"].Tags, tag => tag == CardObjectTags.Boon);
        await game.Act("P1", new PlayCardCommand("FOLLOW", Unit, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task AnActuallyPlayedTokenConsumesTheGrantBeforeTheNextHandUnit()
    {
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "TOKEN-SPELL", "SFD·031/221", "P1", "HAND"));
        await Grant(game); await game.Act("P1", new PlayCardCommand("TOKEN-SPELL", "SFD·031/221", [])); await game.Top();
        await ChooseTokenLocation(game, "BASE");
        var token = Assert.Single(game.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.Single(game.State.StackItems, item => item.TurnUnitPlayBoon is not null); await game.Top();
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[token.ObjectId].Tags);
        await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:F")] [InlineData("BATTLEFIELD:OTHER")] [InlineData("BATTLEFIELD:OPEN")]
    public async Task AnEffectPlayedTokenSelectsItsRealEntryLocationBeforeAnyBoonOrBattle(string destination)
    {
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "TOKEN-SPELL", "SFD·031/221", "P1", "HAND"));
        await Grant(game); await game.Act("P1", new PlayCardCommand("TOKEN-SPELL", "SFD·031/221", [])); await game.Top();
        Assert.DoesNotContain(game.State.CardObjects.Values, card => card.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        var choice = game.State.PendingRuleChoice!;
        Assert.Equal(new[] { "BASE", "BATTLEFIELD:F", "BATTLEFIELD:OPEN", "BATTLEFIELD:OTHER" }, choice.Request.Options.Select(option => option.Id));
        await ChooseTokenLocation(game, destination);
        var token = Assert.Single(game.State.CardObjects.Values, card => card.Tags.Contains(CardObjectTags.SandSoldier));
        var location = game.State.ObjectLocations[token.ObjectId];
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", location.Zone);
        Assert.Equal(destination == "BASE" ? null : destination[12..], location.BattlefieldObjectId);
        var entry = Assert.Single(game.Events(), row => row.Event.Kind == "UNIT_TOKEN_CREATED").Event;
        Assert.Equal(location.Zone, entry.Payload["destinationZone"]);
        Assert.DoesNotContain(game.Events(), row => row.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[token.ObjectId].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task ATokenCannotSelectABannedBattlefieldOrAnInventedLocationOrBeChosenByTheOpponent()
    {
        var state = HandDiscardObserverTests.Put(Position(), "TOKEN-SPELL", "SFD·031/221", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { CardNo = "SFD·216/221" } } };
        var game = new Driver(state); await Grant(game);
        await game.Act("P1", new PlayCardCommand("TOKEN-SPELL", "SFD·031/221", [])); await game.Top();
        var choice = game.State.PendingRuleChoice!;
        Assert.DoesNotContain(choice.Request.Options, option => option.Id == "BATTLEFIELD:OTHER");
        foreach (var (player, destination) in new[] { ("P1", "BATTLEFIELD:OTHER"), ("P1", "BATTLEFIELD:INVENTED"), ("P2", "BASE") })
        {
            var hash = MatchStateHasher.Hash(game.State);
            var result = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-token-location", player, CommandTypes.PayCost),
                new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [destination]), default);
            Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        }
        await ChooseTokenLocation(game, "BASE"); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AnInstructionThatExplicitlyPlaysItsTokenToBaseKeepsThatRestriction()
    {
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "TOKEN-SPELL", "SFD·076/221", "P1", "HAND"));
        await Grant(game); await game.Act("P1", new PlayCardCommand("TOKEN-SPELL", "SFD·076/221", [])); await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == "SFD·T01");
        Assert.Equal("BASE", game.State.ObjectLocations[token.ObjectId].Zone);
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[token.ObjectId].Tags);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-082/219")] [InlineData("UNL-082a/219")]
    public async Task ACapturedMovementOriginAtBaseRemainsBindingWhileTheTokenConsumesTheGrant(string no)
    {
        var state = OfficialHoldSequenceTests.AddUnit(Position(), "LILLIA", no, "F", "P1");
        state = LocalDestructionRecallTests.At(state, "LILLIA", "P1", "BASE");
        var game = new Driver(state); await Grant(game);
        await game.Act("P1", new MoveUnitCommand("LILLIA", Destination: "BATTLEFIELD:OPEN"));
        Assert.Single(game.State.StackItems, item => item.MovementObserver is not null);
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == "UNL·T07");
        Assert.Equal(new ObjectLocationState("P1", "BASE"), game.State.ObjectLocations[token.ObjectId]);
        Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        await game.Top(); Assert.Contains(CardObjectTags.Boon, game.State.CardObjects[token.ObjectId].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task ATokenWithoutTheTemporaryGrantStillHasItsOrdinaryControlledBattlefieldChoice()
    {
        var state = HandDiscardObserverTests.Put(Position(), "TOKEN-SPELL", "SFD·031/221", "P1", "HAND");
        state = OfficialHoldSequenceTests.AddUnit(state, "LOCATION-ANCHOR", Unit, "F", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("TOKEN-SPELL", "SFD·031/221", [])); await game.Top();
        Assert.Equal(new[] { "BASE", "BATTLEFIELD:F" }, game.State.PendingRuleChoice!.Request.Options.Select(option => option.Id));
        var pending = game.State.PendingRuleChoice!;
        var forged = game.State with { PendingRuleChoice = pending with { Request = pending.Request with {
            Options = pending.Request.Options.Append(new("BATTLEFIELD:OTHER", "伪造的额外位置", ["OTHER"])).ToArray() } } };
        Assert.Contains("invalid replacement choice continuation", OfficialInsightAndSpellLockTests.Errors(forged));
        await ChooseTokenLocation(game, "BATTLEFIELD:F");
        var token = Assert.Single(game.State.CardObjects.Values, card => card.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.Equal("F", game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId);
        Assert.DoesNotContain(CardObjectTags.Boon, token.Tags); await game.Replay();
    }

    [Fact]
    public async Task EchoCannotReuseTheNextUnitPermissionForItsSecondToken()
    {
        var state = HandDiscardObserverTests.Put(Position(), "TOKEN-SPELL", "SFD·031/221", "P1", "HAND");
        state = OfficialHoldSequenceTests.AddUnit(state, "ENEMY-ANCHOR", Unit, "OTHER", "P2");
        state = OfficialHoldSequenceTests.AddUnit(state, "LOCATION-ANCHOR", Unit, "F", "P1");
        var game = new Driver(state); await Grant(game);
        await game.Act("P1", new PlayCardCommand("TOKEN-SPELL", "SFD·031/221", [], OptionalCosts: ["ECHO"])); await game.Top();
        await ChooseTokenLocation(game, "BATTLEFIELD:OTHER");
        var second = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("TOKEN_PLAY_LOCATION", second.Request.Kind);
        Assert.DoesNotContain(second.Request.Options, option => option.Id is "BATTLEFIELD:OTHER" or "BATTLEFIELD:OPEN");
        await ChooseTokenLocation(game, "BASE");
        var tokens = game.State.CardObjects.Values.Where(card => card.Tags.Contains(CardObjectTags.SandSoldier)).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.Single(game.State.StackItems, item => item.TurnUnitPlayBoon is not null);
        Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        await game.Top();
        Assert.Single(tokens, token => game.State.CardObjects[token.ObjectId].Tags.Contains(CardObjectTags.Boon));
        Assert.Contains(tokens, token => game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId == "OTHER"
            && game.State.CardObjects[token.ObjectId].Tags.Contains(CardObjectTags.Boon));
        await game.Replay();
    }

    [Fact]
    public async Task ReturningThePlayedUnitDuringResponsesDoesNotRestoreTheSpentGrantOrLeakItsHandIdentity()
    {
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await Grant(game); await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top(); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Act("P1", new PlayCardCommand("FOLLOW", Unit, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("future")] [InlineData("before-grant")] [InlineData("unit-generation")] [InlineData("card")] [InlineData("null-unit")]
    [InlineData("malformed")] [InlineData("duplicate")]
    public async Task RecoveryRejectsForgedNextUnitUseReceipts(string branch)
    {
        var game = new Driver(Position()); await Grant(game);
        await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BASE"));
        var marker = Assert.Single(game.State.UntilEndOfTurnEffects, effect => effect.StartsWith(Used, StringComparison.Ordinal));
        var use = JsonSerializer.Deserialize<TurnUnitPlayGrantUse>(Convert.FromBase64String(marker[Used.Length..]))!;
        var changed = branch switch {
            "future" => use with { CapturedTick = game.State.Tick + 1 },
            "before-grant" => use with { CapturedTick = 1 },
            "unit-generation" => use with { Unit = use.Unit with { Generation = use.Unit.Generation + 1 } },
            "card" => use with { UnitCardNo = "OGN·002/298" },
            "null-unit" => use with { Unit = null! },
            _ => use
        };
        var bad = Used + (branch == "malformed" ? "invalid-base64" : Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(changed)));
        var corrupted = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(e => e != marker)
            .Concat(branch == "duplicate" ? [marker, marker] : new[] { bad }).ToArray() };
        Assert.Contains("invalid temporary unit play grant", OfficialInsightAndSpellLockTests.Errors(corrupted));
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-next-unit", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
