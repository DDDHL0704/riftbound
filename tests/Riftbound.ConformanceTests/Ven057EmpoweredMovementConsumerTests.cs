using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven057EmpoweredMovementConsumerTests
{
    private const string No = "VEN·057";
    private const string Text = "{{强化3}}（支付{{3}}：强化我。仅在未强化时可用。）\n{{已强化>}} 当我移动时，抽一张牌。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactPrintingPreservesEveryPriorCatalogGroupAndMember()
    {
        var latestCatalog = await OfficialCardCatalog.LoadDefaultAsync();
        var catalog = latestCatalog with { Total = 1088, Cards = latestCatalog.Cards.Where(c => c.CardNo != "VEN·124").ToArray() };
        var controlCards = catalog.Cards.Where(c => c.CardNo is not ("VEN·128" or "VEN·078")).ToArray();
        var old = controlCards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1085, old.Length); Assert.Equal(1086, controlCards.Length);
        var prior = FunctionalUnitBuilder.Build(old); var current = FunctionalUnitBuilder.Build(controlCards);
        Assert.Equal(866, prior.Count); Assert.Equal(867, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
        var cumulativePriorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1087, cumulativePriorCards.Length); Assert.Equal(1088, catalog.Cards.Count);
        var cumulativePrior = FunctionalUnitBuilder.Build(cumulativePriorCards); var cumulative = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(868, cumulativePrior.Count); Assert.Equal(869, cumulative.Count);
        foreach (var unit in cumulativePrior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        foreach (var unit in current) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        Assert.Equal(1089, latestCatalog.Cards.Count);
        var latestUnits = FunctionalUnitBuilder.Build(latestCatalog.Cards);
        Assert.Equal(870, latestUnits.Count);
        foreach (var unit in FunctionalUnitBuilder.Build(catalog.Cards))
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(unit), System.Text.Json.JsonSerializer.Serialize(Assert.Single(latestUnits, c => c.Id == unit.Id)));
        Assert.Equal("VEN·124", Assert.Single(Assert.Single(latestUnits, c => c.Cards.Any(card => card.CardNo == "VEN·124")).Cards).CardNo);
    }

    [Fact]
    public async Task ExactCnPrintingSeparatesBluePlayCostFromManaEmpowerAndDependentMovementDraw()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37748, card.Id); Assert.Equal("秘密线人", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.Equal([RuneTrait.Blue], card.CardColorList); Assert.Equal(["unit"], card.CardCategoryList); Assert.Equal("诺克萨斯", card.Region); Assert.Empty(card.Tag);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower); Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust);
        Assert.Equal(0, EmpowerParser.Self(card.CardEffect).Might); Assert.Empty(EmpowerParser.Self(card.CardEffect).Keywords);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(3, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower); Assert.Equal(1, PrintedPowerCostRules.ForCard(No).Amount);
    }

    [Fact]
    public async Task ActualPlaySpendsOneBlueAndTheIndependentEmpowerSpendsOnlyThreeMana()
    {
        var initial = Position(); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            Assert.Equal(17, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate()); Assert.Equal(14, game.State.RunePools["P1"].Mana);
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds); Assert.Equal(3, item.EmpowerActivation!.PaidMana);
            Assert.Equal(0, Draws(game, "P1")); await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(4, Might(game.State, "UNIT")); Assert.Equal(0, Draws(game, "P1")); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-play-and-empower", initial, game); }
    }

    [Theory]
    [InlineData(false, "BASE")] [InlineData(true, "BASE")]
    [InlineData(false, "BATTLEFIELD:BF")] [InlineData(true, "BATTLEFIELD:BF")]
    public async Task ActualMovementDrawsOnlyWhenItsSourceIsEmpoweredAtTheMovementEvent(bool empowered, string origin)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await EnterReady(game, empowered, origin); Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
            var draws = Draws(game, "P1"); var hand = game.State.PlayerZones["P1"].Hand.Count; var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var destination = origin == "BASE" ? "BATTLEFIELD:BF" : "BASE";
            await game.Act("P1", new MoveUnitCommand("UNIT", origin == "BASE" ? "BASE" : "BATTLEFIELD", destination, []));
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.Equal(1, game.State.CardObjects["UNIT"].MovementCountThisTurn);
            Assert.Equal(draws, Draws(game, "P1")); Assert.Equal(empowered ? 1 : 0, SourceEffects(game, "UNIT"));
            if (empowered)
            {
                Assert.Single(game.State.StackItems, item => item.SourceObjectId == "UNIT");
                await game.Top(); Assert.Equal(draws + 1, Draws(game, "P1")); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
            }
            await FinishWindow(game); Assert.Equal(draws + (empowered ? 1 : 0), Draws(game, "P1"));
            Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-move-" + empowered + "-" + origin.Replace(':', '-'), initial, game); }
    }

    [Theory]
    [InlineData("P1", "ALLY", "BF")] [InlineData("P2", "ENEMY", "OTHER")]
    public async Task AnotherFriendlyOrEnemyUnitsActualMoveDoesNotTriggerThisSource(string mover, string unit, string field)
    {
        var initial = Position(); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate()); await game.Top();
            if (mover == "P2") await game.Act("P1", new EndTurnCommand());
            var draws = Draws(game, "P1"); await game.Act(mover, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field, []));
            Assert.Equal(0, SourceEffects(game, "UNIT")); await FinishWindow(game); Assert.Equal(draws, Draws(game, "P1"));
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(0, game.State.CardObjects["UNIT"].MovementCountThisTurn);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("other-unit-move-" + mover, initial, game); }
    }

    [Fact]
    public async Task ActualReturnBeforeEmpowerResolvesDoesNotRefundManaOrEmpowerTheReplayedIncarnation()
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate());
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.Equal(13, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Top(); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(13, game.State.RunePools["P1"].Mana);
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(10, game.State.RunePools["P1"].Mana); Assert.Equal(0, SourceEffects(game, "UNIT"));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("return-before-empower", initial, game); }
    }

    [Fact]
    public async Task AnActuallyEarnedMoveDrawSurvivesItsSourceReturningAndLosingEmpowerment()
    {
        var initial = Add(Add(Position(), "RETURN", "OGN·104/298"), "REACTION-RUNE", "UNL-R03", "P1", "BASE"); var game = new Driver(initial);
        try
        {
            await EnterReady(game, true, "BASE"); await game.Act("P1", new TapRuneCommand("REACTION-RUNE"));
            var draws = Draws(game, "P1"); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", [])); Assert.Equal(1, SourceEffects(game, "UNIT"));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(draws, Draws(game, "P1")); await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal(draws, Draws(game, "P1")); Assert.Equal(1, SourceEffects(game, "UNIT"));
            await game.Top(); Assert.Equal(draws + 1, Draws(game, "P1")); await FinishWindow(game);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Replay(); Restore(game.State);
        }
        finally { await Save("earned-move-draw-source-return", initial, game); }
    }

    [Fact]
    public async Task AnActualReturnToHandIsAFieldDepartureRatherThanAMoveOrADraw()
    {
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BATTLEFIELD:BF")); await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.Act("P1", Activate()); await game.Top();
            var draws = Draws(game, "P1"); await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED"); Assert.Equal(draws, Draws(game, "P1")); Assert.Equal(0, SourceEffects(game, "UNIT"));
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind.StartsWith("UNIT_MOVED", StringComparison.Ordinal));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-return-is-not-move", initial, game); }
    }

    [Fact]
    public async Task ActualControlAndRecallPreserveEmpowermentWithoutCreatingAMoveTrigger()
    {
        var initial = Add(Add(Position(), "TAKEN", No, "P2"), "TAKE", "OGN·203/298") with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(0, 0), ["P2"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        for (var i = 0; i < 8; i++) initial = Add(initial, "CONTROL-RUNE-" + i, "UNL-R05", "P1", "BASE");
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P2", new PlayCardCommand("TAKEN", No, [], Destination: "BATTLEFIELD:OTHER"));
            await game.Act("P2", new ActivateAbilityCommand("TAKEN", "EMPOWER:0:0", [])); await game.Top();
            await game.Act("P2", new EndTurnCommand());
            await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            await game.TapRunes("P1", Enumerable.Range(0, 8).Select(i => "CONTROL-RUNE-" + i));
            for (var i = 0; i < 3; i++) await game.Act("P1", new RecycleRuneCommand("CONTROL-RUNE-" + i));
            var draws = Draws(game, "P1"); await game.Act("P1", new PlayCardCommand("TAKE", "OGN·203/298", ["TAKEN"])); await game.Top();
            Assert.Equal("P1", game.State.CardObjects["TAKEN"].ControllerId); Assert.Equal("P2", game.State.CardObjects["TAKEN"].OwnerId);
            Assert.Equal("BASE", game.State.ObjectLocations["TAKEN"].Zone); Assert.True(game.State.CardObjects["TAKEN"].IsEmpowered);
            Assert.Equal(0, game.State.CardObjects["TAKEN"].MovementCountThisTurn); Assert.Equal(0, SourceEffects(game, "TAKEN"));
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED"); Assert.Equal(draws, Draws(game, "P1")); await game.Replay(); Restore(game.State);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind.StartsWith("UNIT_MOVED", StringComparison.Ordinal));
        }
        finally { await Save("actual-control-recall-is-not-move", initial, game); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TwoActualMovesInOneTurnHaveNoOncePerTurnDrawLimit(bool empowered)
    {
        var initial = Add(Add(Position(), "READY-A", "UNL-009/219"), "READY-B", "UNL-009/219"); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            if (empowered) { await game.Act("P1", Activate()); await game.Top(); }
            await game.ColdCheckpoint(); game.RecoverAfterEveryCommand = true;
            var turn = game.State.TurnNumber; var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var draws = Draws(game, "P1");
            for (var move = 1; move <= 2; move++)
            {
                var mana = game.State.RunePools["P1"].Mana;
                await game.Act("P1", new PlayCardCommand(move == 1 ? "READY-A" : "READY-B", "UNL-009/219", ["UNIT"]));
                Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana); await game.Top();
                Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
                var hand = game.State.PlayerZones["P1"].Hand.Count; var before = Draws(game, "P1");
                await game.Act("P1", new MoveUnitCommand("UNIT", move == 1 ? "BASE" : "BATTLEFIELD", move == 1 ? "BATTLEFIELD:BF" : "BASE", []));
                Assert.Equal(turn, game.State.TurnNumber); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
                Assert.Equal(move, game.State.CardObjects["UNIT"].MovementCountThisTurn); Assert.Equal(before, Draws(game, "P1"));
                Assert.Equal(empowered ? 1 : 0, SourceEffects(game, "UNIT"));
                if (empowered) { await game.Top(); Assert.Equal(before + 1, Draws(game, "P1")); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count); }
                await FinishWindow(game);
                Assert.Equal(draws + (empowered ? move : 0), Draws(game, "P1")); Assert.Equal(empowered, game.State.CardObjects["UNIT"].IsEmpowered);
            }
            Assert.Equal(2, game.Events().Count(e => e.Event.Kind.StartsWith("UNIT_MOVED", StringComparison.Ordinal)));
            Assert.Equal("BASE", game.State.ObjectLocations["UNIT"].Zone); await game.Replay(); Restore(game.State);
        }
        finally { await Save("same-turn-two-moves-" + empowered, initial, game); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task CanonicalOpeningPaysRealBluePowerAndColdRecoversItsEarnedMoveDraw(string actor)
    {
        var game = await VenEmpowerCanonicalOpeningTests.Open(actor, "lillia-lowcurve", "UNL-R03", [No, No, No]);
        try
        {
            await game.WaitFor(actor, 6, No); await game.Tap(actor);
            await game.Act(actor, new RecycleRuneCommand(game.Runes(actor).First()));
            Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Blue]);
            var unit = game.Hand(actor, No).First(); var mana = game.State.RunePools[actor].Mana;
            await game.Act(actor, new PlayCardCommand(unit, No, []));
            Assert.Equal(mana - 3, game.State.RunePools[actor].Mana); Assert.Equal(0, game.State.RunePools[actor].TotalPower);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.Might(actor, unit)); await game.ColdCheckpoint();
            await game.Act(actor, new ActivateAbilityCommand(unit, "EMPOWER:0:0", []));
            Assert.Equal(mana - 6, game.State.RunePools[actor].Mana); Assert.Equal(3, Assert.Single(game.State.StackItems).EmpowerActivation!.PaidMana);
            Assert.False(game.State.CardObjects[unit].IsEmpowered); await game.ColdCheckpoint();
            var pool = game.State.RunePools[actor];
            var refunded = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { [actor] = pool with { Mana = pool.Mana + 3 } } };
            await game.RejectCoordinatedForgery("refunded-three-mana-after-actual-empower-payment", refunded);
            await game.Top(); Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Equal(4, game.Might(actor, unit)); await game.ColdCheckpoint();
            await game.Act(actor, new EndTurnCommand()); await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            Assert.Equal(actor, game.State.TurnPlayerId); Assert.False(game.State.CardObjects[unit].IsExhausted);
            var field = game.State.PlayerZones[actor].Battlefields.First(game.State.BattlefieldStates.ContainsKey);
            var hand = game.State.PlayerZones[actor].Hand.Count; var deck = game.State.PlayerZones[actor].MainDeck.Count;
            var generation = game.State.CardObjects[unit].ObjectGeneration;
            await game.Act(actor, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field, []));
            Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration); Assert.Equal(1, game.State.CardObjects[unit].MovementCountThisTurn);
            Assert.Equal(hand, game.State.PlayerZones[actor].Hand.Count); Assert.Equal(deck, game.State.PlayerZones[actor].MainDeck.Count);
            Assert.Single(game.State.StackItems, item => item.SourceObjectId == unit); await game.ColdCheckpoint();
            await game.Top(); Assert.Equal(hand + 1, game.State.PlayerZones[actor].Hand.Count); Assert.Equal(deck - 1, game.State.PlayerZones[actor].MainDeck.Count);
            await FinishWindow(game); Assert.True(game.State.CardObjects[unit].IsEmpowered); await game.ColdCheckpoint();
        }
        finally { await game.Save("ven057-movement-" + actor); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task AnActuallyTakenEmpoweredUnitsMoveDrawsForItsCurrentControllerRatherThanItsOwner(string thief)
    {
        var owner = thief == "P1" ? "P2" : "P1";
        var initial = Add(Add(Add(Position(), "TAKEN", No, owner), "TAKE", "OGN·203/298", thief), "READY", "UNL-009/219", thief) with {
            TurnPlayerId = owner, ActivePlayerId = owner,
            RunePools = new Dictionary<string, RunePool> { [owner] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }), [thief] = new(0, 0) } };
        for (var i = 0; i < 10; i++) initial = Add(initial, "CONTROL-RUNE-" + i, "UNL-R05", thief, "BASE");
        var game = new Driver(initial);
        try
        {
            await game.Act(owner, new PlayCardCommand("TAKEN", No, [], Destination: "BATTLEFIELD:" + (owner == "P1" ? "BF" : "OTHER")));
            await game.Act(owner, new ActivateAbilityCommand("TAKEN", "EMPOWER:0:0", [])); await game.Top(); await game.Act(owner, new EndTurnCommand());
            await game.TapRunes(thief, Enumerable.Range(0, 10).Select(i => "CONTROL-RUNE-" + i));
            for (var i = 0; i < 3; i++) await game.Act(thief, new RecycleRuneCommand("CONTROL-RUNE-" + i));
            await game.Act(thief, new PlayCardCommand("TAKE", "OGN·203/298", ["TAKEN"])); await game.Top();
            Assert.Equal(2, game.State.RunePools[thief].Mana); Assert.Equal(0, game.State.RunePools[thief].TotalPower);
            Assert.Equal(owner, game.State.CardObjects["TAKEN"].OwnerId); Assert.Equal(thief, game.State.CardObjects["TAKEN"].ControllerId);
            Assert.True(game.State.CardObjects["TAKEN"].IsEmpowered); Assert.Equal("BASE", game.State.ObjectLocations["TAKEN"].Zone);
            Assert.Equal(0, SourceEffects(game, "TAKEN")); Assert.DoesNotContain(game.Events(), e => e.Event.Kind.StartsWith("UNIT_MOVED", StringComparison.Ordinal));
            await game.Act(thief, new PlayCardCommand("READY", "UNL-009/219", ["TAKEN"])); await game.Top();
            Assert.Equal(0, game.State.RunePools[thief].Mana); Assert.False(game.State.CardObjects["TAKEN"].IsExhausted);
            var draws = Draws(game, thief); var ownerDraws = Draws(game, owner); var hand = game.State.PlayerZones[thief].Hand.Count;
            var generation = game.State.CardObjects["TAKEN"].ObjectGeneration;
            await game.Act(thief, new MoveUnitCommand("TAKEN", "BASE", "BATTLEFIELD:" + (thief == "P1" ? "BF" : "OTHER"), []));
            Assert.Equal(generation, game.State.CardObjects["TAKEN"].ObjectGeneration); Assert.Equal(1, game.State.CardObjects["TAKEN"].MovementCountThisTurn);
            Assert.Equal(draws, Draws(game, thief)); Assert.Equal(ownerDraws, Draws(game, owner)); Assert.Equal(1, SourceEffects(game, "TAKEN"));
            Assert.Equal(thief, Assert.Single(game.State.StackItems, item => item.SourceObjectId == "TAKEN").ControllerId);
            await game.Top(); Assert.Equal(draws + 1, Draws(game, thief)); Assert.Equal(ownerDraws, Draws(game, owner));
            Assert.Equal(hand + 1, game.State.PlayerZones[thief].Hand.Count); Assert.Equal(owner, game.State.CardObjects["TAKEN"].OwnerId);
            await FinishWindow(game); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-controlled-source-move-" + thief, initial, game); }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task ActuallyCopiedEmpowerPaysOnlyManaAndSurvivesDetachmentWithoutCopyingTheOriginalsStatus(bool empowerOriginal, bool detachBeforeResolution)
    {
        var initial = Add(ShadySpectaclesTests.Position(No), "DETACH", "SFD·011/221") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            var originalFace = game.State.CardObjects["HOST"].CardNo; var originalMight = Might(game.State, "HOST");
            if (empowerOriginal) { await game.Act("P1", new ActivateAbilityCommand("COPY-SOURCE", "EMPOWER:0:0", [])); await game.Top(); }
            Assert.Equal(empowerOriginal, game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
            var copy = game.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", copy.Request.Kind);
            await game.Act("P1", new PayCostCommand(copy.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
            Assert.Equal(No, game.State.CardObjects["HOST"].CardNo); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(4, Might(game.State, "HOST")); Assert.Equal(empowerOriginal, game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            var requirement = ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!.Where(c => c.Action == CommandTypes.ActivateAbility)
                .SelectMany(c => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(c.Metadata!["sourceRequirements"]))
                .Single(row => (string?)row["sourceObjectId"] == "HOST" && (string?)row["abilityId"] == "EMPOWER:0:0");
            var mana = game.State.RunePools["P1"].Mana; var blue = game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue];
            var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []) with { AbilityInstanceId = Assert.IsType<string>(requirement["abilityInstanceId"]) });
            Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana); Assert.Equal(blue, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            var pending = Assert.Single(game.State.StackItems); Assert.Equal(3, pending.EmpowerActivation!.PaidMana);
            Assert.Equal("GEAR", pending.EmpowerActivation.Origin!.Equipment.ObjectId); Assert.Equal("HOST", pending.EmpowerActivation.Source.ObjectId);
            Assert.False(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(0, Draws(game, "P1"));
            if (detachBeforeResolution)
            {
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
                Assert.Equal(originalFace, game.State.CardObjects["HOST"].CardNo); Assert.Equal(originalMight, Might(game.State, "HOST"));
                Assert.Equal(pending.StackItemId, Assert.Single(game.State.StackItems).StackItemId); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            }
            var paidMana = game.State.RunePools["P1"].Mana;
            await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(paidMana, game.State.RunePools["P1"].Mana);
            Assert.Equal(empowerOriginal, game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            Assert.Equal(detachBeforeResolution ? 1 : 0, Draws(game, "P1")); // SFD011 itself draws one on resolution.
            if (!detachBeforeResolution)
            {
                Assert.Equal(4, Might(game.State, "HOST"));
                await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
            }
            Assert.Equal(originalFace, game.State.CardObjects["HOST"].CardNo); Assert.Equal(originalMight, Might(game.State, "HOST"));
            Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
            Assert.Equal(1, Draws(game, "P1")); var drawsBeforeMove = Draws(game, "P1");
            await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
            Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn); Assert.Equal(0, SourceEffects(game, "HOST"));
            Assert.Equal(0, SourceEffects(game, "COPY-SOURCE")); await FinishWindow(game); Assert.Equal(drawsBeforeMove, Draws(game, "P1"));
            Assert.Single(game.Events(), e => e.Event.Kind.StartsWith("UNIT_MOVED", StringComparison.Ordinal));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("copied-empower-original-" + empowerOriginal + "-detach-before-" + detachBeforeResolution, initial, game); }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task AnActualCopiedMoveChecksTheHostsEmpowermentRatherThanTheOriginals(bool empowerOriginal, bool empowerHost)
    {
        var initial = ShadySpectaclesTests.Position(No) with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            if (empowerOriginal) { await game.Act("P1", new ActivateAbilityCommand("COPY-SOURCE", "EMPOWER:0:0", [])); await game.Top(); }
            await CopyActually(game); Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
            if (empowerHost) { await game.Act("P1", CopiedEmpower(game)); await game.Top(); }
            Assert.Equal(empowerOriginal, game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            Assert.Equal(empowerHost, game.State.CardObjects["HOST"].IsEmpowered);
            var draws = Draws(game, "P1"); var hand = game.State.PlayerZones["P1"].Hand.Count;
            var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration); Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn);
            Assert.Equal(draws, Draws(game, "P1")); Assert.Equal(0, SourceEffects(game, "COPY-SOURCE"));
            Assert.Equal(empowerHost ? 1 : 0, SourceEffects(game, "HOST"));
            if (empowerHost)
            {
                var skill = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "HOST");
                Assert.Equal(No, skill.CardNo); Assert.NotNull(skill.MovementObserver);
                var context = JsonSerializer.SerializeToElement(skill.MovementObserver, Json);
                Assert.Equal("GEAR", context.GetProperty("origin").GetProperty("equipment").GetProperty("objectId").GetString());
                await game.Top(); Assert.Equal(draws + 1, Draws(game, "P1")); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
            }
            await FinishWindow(game); Assert.Equal(draws + (empowerHost ? 1 : 0), Draws(game, "P1"));
            Assert.Equal(empowerOriginal, game.State.CardObjects["COPY-SOURCE"].IsEmpowered); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-copied-move-original-" + empowerOriginal + "-host-" + empowerHost, initial, game); }
    }

    [Fact]
    public async Task AnActuallyEarnedCopiedMoveDrawSurvivesTheCopyBeingRemovedBeforeResolution()
    {
        var initial = Add(ShadySpectaclesTests.Position(No), "DETACH", "SFD·011/221") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1, [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            var physicalFace = game.State.CardObjects["HOST"].CardNo; await CopyActually(game);
            await game.Act("P1", CopiedEmpower(game)); await game.Top(); Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            var draws = Draws(game, "P1"); var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
            var earned = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "HOST"); Assert.NotNull(earned.MovementObserver);
            Assert.Equal(draws, Draws(game, "P1"));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            var hand = game.State.PlayerZones["P1"].Hand.Count;
            await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"])); await game.Top();
            Assert.Equal(physicalFace, game.State.CardObjects["HOST"].CardNo); Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered); Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal(draws + 1, Draws(game, "P1")); // SFD011's own draw, before the independently earned skill.
            Assert.Equal(earned.StackItemId, Assert.Single(game.State.StackItems, item => item.SourceObjectId == "HOST").StackItemId);
            await game.Top(); Assert.Equal(draws + 2, Draws(game, "P1")); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
            Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn); Assert.False(game.State.CardObjects["COPY-SOURCE"].IsEmpowered);
            await FinishWindow(game); await game.Replay(); Restore(game.State);
        }
        finally { await Save("earned-copied-move-then-actual-detach", initial, game); }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task OneActualEmpoweredMoveEarnsOneIndependentDrawForEveryPrintedAndAttachedTextInstance(int copies)
    {
        var initial = Add(EmpowerTests.Position(No), "READY", "UNL-009/219") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(60, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1, [RuneTrait.Green] = 2 }), ["P2"] = new(20, 20) } };
        if (copies == 2) initial = AttachedRuleTextTests.AddSong(initial, "SONG2");
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("HOST", No, []));
            for (var i = 0; i < copies; i++)
            {
                var gear = i == 0 ? "GEAR" : "SONG2";
                await game.Act("P1", new AssembleEquipmentCommand(gear, "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal("HOST", game.State.CardObjects[gear].AttachedToObjectId);
            }
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [])); await game.Top();
            await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["HOST"])); await game.Top();
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered); Assert.False(game.State.CardObjects["HOST"].IsExhausted);
            Assert.Equal(4, Might(game.State, "HOST")); Assert.Equal(0, Draws(game, "P1"));
            var hand = game.State.PlayerZones["P1"].Hand.Count; var generation = game.State.CardObjects["HOST"].ObjectGeneration;
            await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
            Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration); Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn);
            Assert.Equal(copies + 1, SourceEffects(game, "HOST")); Assert.Equal(0, Draws(game, "P1"));
            if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
            var skills = game.State.StackItems.Where(item => item.SourceObjectId == "HOST").ToArray(); Assert.Equal(copies + 1, skills.Length);
            Assert.Equal(copies + 1, skills.Select(item => item.StackItemId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(skills, item => { Assert.Equal(No, item.CardNo); Assert.NotNull(item.MovementObserver); });
            var origins = skills.Select(item => JsonSerializer.SerializeToElement(item.MovementObserver, Json))
                .Where(context => context.TryGetProperty("origin", out var origin) && origin.ValueKind == JsonValueKind.Object).ToArray();
            Assert.Equal(copies, origins.Length);
            Assert.Equal(copies, origins.Select(context => context.GetProperty("origin").GetProperty("equipment").GetProperty("objectId").GetString()).Distinct(StringComparer.Ordinal).Count());
            for (var i = 0; i <= copies; i++)
            {
                await game.Top(); Assert.Equal(i + 1, Draws(game, "P1")); Assert.Equal(hand + i + 1, game.State.PlayerZones["P1"].Hand.Count);
                Assert.Equal(copies - i, SourceEffects(game, "HOST")); Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn);
            }
            await FinishWindow(game); Assert.Equal(copies + 1, Draws(game, "P1"));
            Assert.Single(game.Events(), e => e.Event.Kind.StartsWith("UNIT_MOVED", StringComparison.Ordinal)); await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-empowered-movement-text-instances-" + copies, initial, game); }
    }

    private static async Task CopyActually(Driver game)
    {
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_YELLOW"])); await game.Top();
        var choice = game.State.PendingRuleChoice!; Assert.Equal("ATTACHMENT_COPY_SELECTION", choice.Request.Kind);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal(No, game.State.CardObjects["HOST"].CardNo); Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
    }
    private static ActivateAbilityCommand CopiedEmpower(Driver game)
    {
        var row = ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!.Where(candidate => candidate.Action == CommandTypes.ActivateAbility)
            .SelectMany(candidate => Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]))
            .Single(requirement => (string?)requirement["sourceObjectId"] == "HOST" && (string?)requirement["abilityId"] == "EMPOWER:0:0");
        return new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []) with { AbilityInstanceId = Assert.IsType<string>(row["abilityInstanceId"]) };
    }

    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static MatchState Position() => VenUnitEntryBasicTests.Position(No) with { RunePools = new Dictionary<string, RunePool> {
        ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
    private static async Task EnterReady(Driver game, bool empowered, string destination)
    {
        var recover = game.RecoverAfterEveryCommand;
        game.RecoverAfterEveryCommand = false;
        await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
        if (empowered) { await game.Act("P1", Activate()); await game.Top(); }
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await game.ColdCheckpoint();
        game.RecoverAfterEveryCommand = recover;
    }
    private static async Task FinishWindow(Driver game)
    {
        for (var i = 0; game.State.TimingState != TimingStates.NeutralOpen; i++)
        {
            Assert.InRange(i, 0, 11);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else Assert.Fail("The actual movement did not expose the next ordinary response step.");
        }
    }
    private static async Task FinishWindow(VenEmpowerCanonicalOpeningTests.CanonicalGame game)
    {
        for (var i = 0; game.State.TimingState != TimingStates.NeutralOpen; i++)
        {
            Assert.InRange(i, 0, 11);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else Assert.Fail("The actual canonical movement did not expose the next ordinary response step.");
        }
    }
    private static int SourceEffects(Driver game, string source)
        => game.State.TriggerQueue.Count(i => i.SourceObjectId == source) + game.State.StackItems.Count(i => i.SourceObjectId == source);
    private static int Draws(Driver game, string player)
        => game.Events().Count(e => e.Event.Kind == "CARD_DRAWN" && (string?)e.Event.Payload.GetValueOrDefault("playerId") == player);
    private static int Might(MatchState state, string id)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1", string zone = "HAND") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone == "BASE" ? state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] }
            : state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static void Restore(MatchState state) => Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(OfficialInsightAndSpellLockTests.Restore(state)));
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN057_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact CN VEN057 explicit midgame actual resource Empower and movement consumers; recall/return are distinct actions. Source applicability, final L2/L3 and whole-card certification remain OPEN",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
