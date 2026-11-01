using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadWakebloomTests
{
    private const string Wakebloom = "RAD·035/167";
    private const string Skill = "WAKEBLOOM_EXHAUST_POWERFUL_DRAW";
    private const string UnitCost = "EXHAUST_FRIENDLY_UNIT:ALLY";

    [Fact]
    public async Task ActualPlayGrantsTemporaryMightAndThatMightCanPayThePrintedActivation()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Wakebloom);
        Assert.Equal(39528, card.Id); Assert.Equal(3, card.Energy);
        var game = new Driver(Put(Create(), "GEAR", Wakebloom, "P1", "HAND"));
        var originalPower = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new PlayCardCommand("GEAR", Wakebloom, []));
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]);
        await Reject(game.State, "P2", command);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Act("P1", command);
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(originalPower + 3, game.State.CardObjects["ALLY"].Power);
        await game.Act("P1", Activate());
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(originalPower, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD")]
    public async Task TheUnitAndEquipmentPayBeforeTheDrawAndAreNotSkillTargets(string zone)
    {
        var state = Position();
        if (zone == "BATTLEFIELD") state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", zone, "BF");
        var game = new Driver(state);
        Assert.Contains(UnitCost, JsonSerializer.Serialize(game.PromptFor("P1")));
        Assert.DoesNotContain(Skill, JsonSerializer.Serialize(game.PromptFor("P2")));
        await game.Act("P1", Activate());
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        var item = Assert.Single(game.State.StackItems);
        Assert.Empty(item.TargetObjectIds); Assert.Null(item.TargetGenerations);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_EXHAUSTED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY")
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "ABILITY_COST"));
        await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("enemy")] [InlineData("weak")]
    [InlineData("source")] [InlineData("double")] [InlineData("target")]
    public Task IllegalCostSelectionsCannotExhaustAnythingOrDraw(string scenario)
    {
        var state = Put(Position(), "WEAK", "OGN·219/298", "P1", "BASE");
        state = Put(state, "ENEMY", "OGN·049/298", "P2", "BASE");
        var costs = scenario switch {
            "missing" => Array.Empty<string>(), "enemy" => ["EXHAUST_FRIENDLY_UNIT:ENEMY"],
            "weak" => ["EXHAUST_FRIENDLY_UNIT:WEAK"], "source" => ["EXHAUST_FRIENDLY_UNIT:GEAR"],
            "double" => new[] { UnitCost, UnitCost }, _ => new[] { UnitCost }
        };
        return Reject(state, "P1", new ActivateAbilityCommand("GEAR", Skill,
            scenario == "target" ? ["ALLY"] : [], OptionalCosts: costs));
    }

    [Theory]
    [InlineData("unit")] [InlineData("source")] [InlineData("weak")]
    public async Task UnpayableExhaustCostsAreOmittedFromPromptsAndRejected(string scenario)
    {
        var state = scenario == "weak" ? Position("OGN·219/298") : Position();
        if (scenario != "weak") {
            var id = scenario == "unit" ? "ALLY" : "GEAR";
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { IsExhausted = true } } };
        }
        Assert.DoesNotContain(Skill, JsonSerializer.Serialize(new Driver(state).PromptFor("P1")));
        await Reject(state, "P1", Activate());
    }

    [Fact]
    public async Task TheCurrentStaticAuraPowerCanMakeAnOtherwiseWeakUnitPayable()
    {
        var state = Put(Position("OGN·219/298"), "WARD", "RAD·039/167", "P1", "BASE");
        var game = new Driver(state);
        Assert.Contains(UnitCost, JsonSerializer.Serialize(game.PromptFor("P1")));
        await game.Act("P1", Activate());
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task APowerfulRobotTokenCanPayTheSharedUnitCost()
    {
        var state = Put(Position("OGN·219/298"), "WARD", "RAD·039/167", "P1", "BASE");
        var robot = P6TokenFactoryCatalog.GetAll().First(f => f.TokenFamilyName == "机器人").CreateObject("ROBOT", "P1", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ROBOT"] = robot } };
        state = RadUnitEffectTestFixture.Place(state, "ROBOT", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", Skill, [], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:ROBOT"]));
        Assert.True(game.State.CardObjects["ROBOT"].IsExhausted);
        await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task ASourceDepartureCheckpointDoesNotUndoThePaidDraw()
    {
        var game = new Driver(Position()); await game.Act("P1", Activate());
        var state = RadUnitEffectTestFixture.Place(game.State, "GEAR", "P1", "GRAVEYARD");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { ObjectGeneration = state.CardObjects["GEAR"].ObjectGeneration + 1 } } };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var engine = new CoreRuleEngine();
        for (var i = 0; state.StackItems.Count > 0; i++) {
            Assert.InRange(i, 0, 3);
            var result = await engine.ResolveAsync(state, new("wakebloom-checkpoint-" + i, state.PriorityPlayerId!, CommandTypes.PassPriority),
                new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state)); FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        Assert.Single(state.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task AnOpponentCannotActivateTheEquipment()
        => await Reject(Position(), "P2", Activate());

    private static MatchState Position(string unit = "OGN·049/298")
        => Put(Put(Create(), "GEAR", Wakebloom, "P1", "BASE"), "ALLY", unit, "P1", "BASE");
    private static ActivateAbilityCommand Activate() => new("GEAR", Skill, [], OptionalCosts: [UnitCost]);
}
