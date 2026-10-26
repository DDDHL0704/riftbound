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
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var old = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1084, old.Length); Assert.Equal(1085, catalog.Cards.Count);
        var prior = FunctionalUnitBuilder.Build(old); var current = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(865, prior.Count); Assert.Equal(866, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
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
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            Assert.Equal(17, game.State.RunePools["P1"].Mana); Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
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
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
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
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate());
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
        var initial = Add(Position(), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: "BATTLEFIELD:BF")); await game.Act("P1", Activate()); await game.Top();
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
        var game = new Driver(initial);
        try
        {
            await game.Act("P2", new PlayCardCommand("TAKEN", No, [], Destination: "BATTLEFIELD:OTHER"));
            await game.Act("P2", new ActivateAbilityCommand("TAKEN", "EMPOWER:0:0", [])); await game.Top();
            await game.Act("P2", new EndTurnCommand());
            for (var i = 0; i < 8; i++) await game.Act("P1", new TapRuneCommand("CONTROL-RUNE-" + i));
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

    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static MatchState Position() => VenUnitEntryBasicTests.Position(No) with { RunePools = new Dictionary<string, RunePool> {
        ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
    private static async Task EnterReady(Driver game, bool empowered, string destination)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
        if (empowered) { await game.Act("P1", Activate()); await game.Top(); }
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
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
