using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialBattlefieldControlTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("P2", false)]
    [InlineData("P1", true)]
    public async Task OrdinaryUnitDestinationRequiresControlInPromptAndAuthority(string? controller, bool allowed)
    {
        var state = DestinationState(controller);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var candidate = session.PromptFor("P1").Candidates!.Single(c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.Destinations!, c => c.Id == "BASE");
        Assert.Equal(allowed, candidate.Destinations!.Any(c => c.Id == "BATTLEFIELD:BF-1"));
        Assert.DoesNotContain(candidate.Destinations!, c => c.Id == "BATTLEFIELD:P1-MAIN");

        var result = await session.SubmitAsync("P1", "play", new PlayCardCommand(
            "UNIT", "SFD·125/221", [], Destination: "BATTLEFIELD:BF-1"), null, default);
        Assert.Equal(allowed, result.Accepted);
        if (!allowed)
        {
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Equal(state.Tick, result.State.Tick);
            Assert.Equal(state.RunePools["P1"], result.State.RunePools["P1"]);
            Assert.Contains("UNIT", result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);
        }
    }

    [Fact]
    public async Task LegacyMainDestinationCannotBypassPhysicalBattlefieldControl()
    {
        var state = DestinationState(null);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("spoof", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", "SFD·125/221", [], Destination: "BATTLEFIELD:P1-MAIN"), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(state.Tick, result.State.Tick);
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF-1")]
    public async Task OrdinaryUnitEntersExhausted(string destination)
    {
        var engine = new CoreRuleEngine();
        var state = DestinationState("P1");
        var result = await engine.ResolveAsync(state, new PlayerIntent("play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("UNIT", "SFD·125/221", [], Destination: destination), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var pass = 0; result.State.StackItems.Count > 0 && pass < 4; pass++)
        {
            result = await engine.ResolveAsync(result.State,
                new PlayerIntent("pass-" + pass, result.State.PriorityPlayerId!, CommandTypes.PassPriority),
                new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Empty(result.State.StackItems);
        Assert.True(result.State.CardObjects["UNIT"].IsExhausted);
    }

    private static MatchState DestinationState(string? controller) => new(
        roomId: "DESTINATION-CONTROL", tick: 0, turnNumber: 3, activePlayerId: "P1",
        seats: new Dictionary<string, string> { ["P1"] = "c1", ["P2"] = "c2" },
        status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
        phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
        runePools: new Dictionary<string, RunePool> { ["P1"] = new(10, 0), ["P2"] = RunePool.Empty },
        playerZones: new Dictionary<string, PlayerZones>
        {
            ["P1"] = PlayerZones.Empty with { Hand = ["UNIT"], Battlefields = ["BF-1"] },
            ["P2"] = PlayerZones.Empty
        },
        cardObjects: new Dictionary<string, CardObjectState>
        {
            ["UNIT"] = new("UNIT", cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["BF-1"] = new("BF-1", cardNo: "OGN·275/298", tags: ["CARD_TYPE:BATTLEFIELD"], ownerId: "P1", controllerId: controller)
        },
        objectLocations: new Dictionary<string, ObjectLocationState>
        {
            ["UNIT"] = new("P1", "HAND"), ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1")
        });

    [Fact]
    public async Task ClaimUsesUnitControllerEvenWhenStoredUnderOtherPlayersBattlefieldZone()
    {
        var original = DestinationState(null);
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["UNIT"] = cards["UNIT"] with { OwnerId = "P1", ControllerId = "P2" };
        var state = original with
        {
            CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Battlefields = ["BF-1", "UNIT"] },
                ["P2"] = PlayerZones.Empty
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["UNIT"] = new("P1", "BATTLEFIELD", "BF-1")
            },
            TimingState = TimingStates.SpellDuelOpen,
            ActivePlayerId = "P2",
            FocusPlayerId = "P2"
        };
        var engine = new CoreRuleEngine();
        var first = await engine.ResolveAsync(state, new PlayerIntent("focus-2", "P2", CommandTypes.PassFocus), new PassFocusCommand(), default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var closed = await engine.ResolveAsync(first.State, new PlayerIntent("focus-1", "P1", CommandTypes.PassFocus), new PassFocusCommand(), default);
        Assert.True(closed.Accepted, closed.ErrorMessage);
        Assert.Equal("P2", closed.State.CardObjects["BF-1"].ControllerId);
        Assert.Empty(closed.State.PendingTaskQueue.Tasks);
        Assert.Equal(1, closed.State.PlayerScores["P2"]);
    }

    [Fact]
    public async Task UnitsOnSeparateControlledBattlefieldsCannotFightEachOther()
    {
        var original = DestinationState("P1");
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["UNIT"] = cards["UNIT"] with { Power = 2 };
        cards["DEFENDER"] = cards["UNIT"] with { ObjectId = "DEFENDER", OwnerId = "P2", ControllerId = "P2" };
        cards["BF-2"] = cards["BF-1"] with { ObjectId = "BF-2", OwnerId = "P2", ControllerId = "P2" };
        var state = original with
        {
            CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Battlefields = ["BF-1", "UNIT"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["BF-2", "DEFENDER"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"), ["UNIT"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["BF-2"] = new("P2", "BATTLEFIELD", "BF-2"), ["DEFENDER"] = new("P2", "BATTLEFIELD", "BF-2")
            }
        };
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(state)["P1"].Candidates!,
            c => c.Action == CommandTypes.DeclareBattle && c.Enabled);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("cross-field-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand("BF-1", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]), default);
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task StandardMoveIntoEnemyUnitsStillOpensBattleWhenBothUnitsAreExhausted(string battlefieldOwner)
    {
        var original = DestinationState("P2");
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["BF-1"] = cards["BF-1"] with { OwnerId = battlefieldOwner };
        cards["UNIT"] = cards["UNIT"] with { Power = 2 };
        cards["DEFENDER"] = new("DEFENDER", power: 2, isExhausted: true,
            cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2");
        var state = original with
        {
            CardObjects = cards,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["UNIT"], Battlefields = battlefieldOwner == "P1" ? ["BF-1"] : [] },
                ["P2"] = PlayerZones.Empty with { Battlefields = battlefieldOwner == "P2" ? ["BF-1", "DEFENDER"] : ["DEFENDER"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["BF-1"] = new(battlefieldOwner, "BATTLEFIELD", "BF-1"), ["UNIT"] = new("P1", "BASE"),
                ["DEFENDER"] = new("P2", "BATTLEFIELD", "BF-1")
            }
        };
        var engine = new CoreRuleEngine();
        var current = await engine.ResolveAsync(state, new PlayerIntent("move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF-1", []), default);
        Assert.True(current.Accepted, current.ErrorMessage);
        Assert.True(current.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal("P1", current.State.FocusPlayerId);
        Assert.Equal("P1", current.State.PendingCleanupTasks.Single(task => task.Kind == "START_BATTLE").PlayerId);
        var recovered = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(current.State))!;
        Assert.Equal(MatchStateHasher.Hash(current.State), MatchStateHasher.Hash(recovered));
        Assert.Equal("P1", recovered.PendingCleanupTasks.Single(task => task.Kind == "START_BATTLE").PlayerId);
        for (var pass = 0; pass < 2; pass++)
        {
            current = await engine.ResolveAsync(current.State,
                new PlayerIntent("focus-" + pass, current.State.FocusPlayerId!, CommandTypes.PassFocus),
                new PassFocusCommand(), default);
            Assert.True(current.Accepted, current.ErrorMessage);
        }
        Assert.Contains(CommandTypes.DeclareBattle, current.Prompts["P1"].Actions);
        var battle = await engine.ResolveAsync(current.State,
            new PlayerIntent("battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand("BF-1", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]), default);
        Assert.True(battle.Accepted, battle.ErrorMessage);
        Assert.Contains(battle.Events, e => e.Kind == "BATTLE_DECLARED");
        Assert.DoesNotContain(battle.Events, e => e.Kind == "BATTLE_SKIPPED");
    }

    [Fact]
    public async Task ANewContestInTheSameTurnStartsANewSpellDuel()
    {
        var original = DestinationState(null);
        var state = original with
        {
            PlayerZones = new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["UNIT"], Battlefields = ["BF-1"] },
                ["P2"] = PlayerZones.Empty
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"), ["UNIT"] = new("P1", "BASE")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")]
        };
        var engine = new CoreRuleEngine();
        var moved = await engine.ResolveAsync(state, new PlayerIntent("move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF-1", []), default);
        Assert.True(moved.Accepted, moved.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, moved.State.TimingState);
        Assert.Contains(moved.Events, e => e.Kind == "SPELL_DUEL_STARTED");
        var current = moved;
        for (var pass = 0; pass < 2; pass++)
        {
            current = await engine.ResolveAsync(current.State,
                new PlayerIntent("focus-" + pass, current.State.FocusPlayerId!, CommandTypes.PassFocus),
                new PassFocusCommand(), default);
            Assert.True(current.Accepted, current.ErrorMessage);
        }
        Assert.Equal("P1", current.State.CardObjects["BF-1"].ControllerId);
        Assert.Empty(current.State.PendingTaskQueue.Tasks);
    }

    [Theory]
    [InlineData("SFD·209/221", 3, false)]
    [InlineData("SFD·209/221", 5, true)]
    [InlineData("OGN·290/298", 3, true)]
    public async Task BattlefieldTextDoesNotReplaceNormalHoldingAndDelayOnlyAffectsItsOwnScore(
        string specialCardNo, int previousTurn, bool specialScores)
    {
        var original = DestinationState("P2");
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["BF-1"] = cards["BF-1"] with { CardNo = specialCardNo };
        cards["BF-2"] = new("BF-2", cardNo: "OGN·275/298", tags: ["CARD_TYPE:BATTLEFIELD"], ownerId: "P1", controllerId: "P2");
        cards["UNIT"] = cards["UNIT"] with { ControllerId = "P2" };
        cards["UNIT-2"] = cards["UNIT"] with { ObjectId = "UNIT-2" };
        cards["DRAW"] = new("DRAW", cardNo: "OGS·003/024", ownerId: "P2", controllerId: "P2");
        var state = original with
        {
            TurnNumber = previousTurn,
            CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Battlefields = ["BF-1", "BF-2", "UNIT", "UNIT-2"] },
                ["P2"] = PlayerZones.Empty with { MainDeck = ["DRAW"] }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"), ["UNIT"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["BF-2"] = new("P1", "BATTLEFIELD", "BF-2"), ["UNIT-2"] = new("P1", "BATTLEFIELD", "BF-2"),
                ["DRAW"] = new("P2", "MAIN_DECK")
            }
        };
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("end", "P1", CommandTypes.EndTurn), new EndTurnCommand(), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        var scoredFields = result.Events.Where(e => e.Kind == "SCORE_GAINED")
            .Select(e => e.Payload.GetValueOrDefault("sourceObjectId")?.ToString()).ToArray();
        Assert.Contains("BF-2", scoredFields);
        Assert.Equal(specialScores, scoredFields.Contains("BF-1"));
        Assert.Equal(specialScores ? 2 : 1, result.State.PlayerScores["P2"]);
    }

    [Theory]
    [InlineData(TimingStates.NeutralClosed, false)]
    [InlineData(TimingStates.SpellDuelClosed, false)]
    [InlineData(TimingStates.SpellDuelOpen, false)]
    [InlineData(TimingStates.NeutralClosed, true)]
    public async Task ResourceReactionsRespectPriorityAndKeepTheWindow(string timing, bool recycle)
    {
        var original = DestinationState(null);
        var cards = original.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["RUNE"] = new("RUNE", cardNo: "OGN·007/298", tags: [CardObjectTags.RuneCard, "COLOR:red"], ownerId: "P1", controllerId: "P1");
        var open = timing == TimingStates.SpellDuelOpen;
        var state = original with
        {
            CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Base = ["RUNE"], Hand = ["UNIT"], Battlefields = ["BF-1"] },
                ["P2"] = PlayerZones.Empty
            },
            TimingState = timing,
            PriorityPlayerId = open ? null : "P1",
            FocusPlayerId = open ? "P1" : null,
            StackItems = open ? [] : [new StackItemState("STACK", "P2", "SPELL", "DAMAGE", "OGS·003/024", [])]
        };
        var action = recycle ? CommandTypes.RecycleRune : CommandTypes.TapRune;
        GameCommand command = recycle ? new RecycleRuneCommand("RUNE") : new TapRuneCommand("RUNE");
        var engine = new CoreRuleEngine();
        var rejected = await engine.ResolveAsync(state, new PlayerIntent("wrong-player", "P2", action), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(state.Tick, rejected.State.Tick);
        var result = await engine.ResolveAsync(state, new PlayerIntent("resource", "P1", action), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(timing, result.State.TimingState);
        Assert.Equal(state.PriorityPlayerId, result.State.PriorityPlayerId);
        Assert.Equal(state.FocusPlayerId, result.State.FocusPlayerId);
        Assert.Equal(state.StackItems, result.State.StackItems);
        if (recycle)
        {
            Assert.Contains("RUNE", result.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait["red"]);
        }
        else
        {
            Assert.True(result.State.CardObjects["RUNE"].IsExhausted);
            Assert.Equal(state.RunePools["P1"].Mana + 1, result.State.RunePools["P1"].Mana);
        }
        Assert.DoesNotContain(result.Events, e => e.Kind == "STACK_ITEM_RESOLVED");
    }

    [Fact]
    public async Task EmptyOpeningBattlefieldsHaveOwnersButNoControllersAndNeverScoreForPassingTurns()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog, OfficialDeckFormat.ChinaStandard20260724)[0].Decklist;
        var session = new MatchSession("EMPTY-OPENING-CONTROL", new CoreRuleEngine());
        foreach (var player in new[] { "P1", "P2" })
        {
            session.EnsurePlayer(player);
            var submitted = await session.SubmitDeckAsync(player, "deck-" + player,
                new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields),
                null, default);
            Assert.True(submitted.Accepted, submitted.ErrorMessage);
        }
        await session.ReadyAsync("P1", "ready-1", null, default);
        var result = await session.ReadyAsync("P2", "ready-2", null, default);
        AssertUncontrolled(result.State);
        foreach (var player in new[] { result.State.ActivePlayerId, result.State.OpeningSecondActionPlayerId! })
        {
            result = await session.SubmitAsync(player, "mulligan-" + player,
                new MulliganCommand([]), JsonSerializer.SerializeToElement(new { cmdType = "MULLIGAN", handObjectIds = Array.Empty<string>() }), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        for (var turn = 0; turn < 8; turn++)
        {
            result = await session.SubmitAsync(result.State.TurnPlayerId, "end-" + turn,
                new EndTurnCommand(), null, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            AssertUncontrolled(result.State);
            Assert.All(result.State.PlayerScores.Values, score => Assert.Equal(0, score));
            Assert.DoesNotContain(result.Events, e => e.Kind is "BATTLEFIELD_HELD" or "SCORE_GAINED" or "MATCH_WON");
        }
    }

    private static void AssertUncontrolled(MatchState state)
    {
        Assert.Equal(2, state.BattlefieldStates.Count);
        Assert.All(state.BattlefieldStates.Values, battlefield =>
        {
            Assert.Null(battlefield.ControllerId);
            Assert.Equal("UNCONTROLLED", battlefield.Status);
            var card = state.CardObjects[battlefield.BattlefieldObjectId];
            Assert.False(string.IsNullOrWhiteSpace(card.OwnerId));
            Assert.Null(card.ControllerId);
        });
    }
}
