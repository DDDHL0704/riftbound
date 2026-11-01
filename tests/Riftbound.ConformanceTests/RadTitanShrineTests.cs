using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadTitanShrineTests
{
    private const string Shrine = "RAD·089/167";
    private const string Unit = "OGN·049/298";

    [Fact]
    public async Task OfficialEquipmentPaysThreeManaAndDoesNotObserveItsOwnEquipmentPlay()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Shrine);
        Assert.Equal(39589, card.Id); Assert.Equal(3, card.Energy); Assert.Null(card.ReturnEnergy);
        var game = new Driver(Put(Create(), "SHRINE", Shrine, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SHRINE", Shrine, []));
        Assert.Equal(27, game.State.RunePools["P1"].Mana);
        Assert.Contains("SHRINE", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects["SHRINE"].IsExhausted);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TheControllerPaysTheEquipmentExhaustCostBeforeResponsesAndDraw(string owner)
    {
        var game = new Driver(Position(owner));
        await Play(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(new[] { "SHRINE" }, choice.LegalObjectIds);
        Assert.False(game.State.CardObjects["SHRINE"].IsExhausted);
        var pay = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SHRINE"]);
        await Reject(game.State, "P2", pay);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", pay);
        Assert.True(game.State.CardObjects["SHRINE"].IsExhausted);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        Assert.NotNull(game.State.StackItems[0].TriggerCost);
        Assert.Contains(game.Events(), e => e.Event.Kind == "EQUIPMENT_EXHAUSTED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SHRINE"));
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.PlayerZones["P2"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task DecliningTheLeadingCostLeavesTheEquipmentReadyAndDoesNotDraw()
    {
        var game = new Driver(Position()); await Play(game);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.False(game.State.CardObjects["SHRINE"].IsExhausted);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "BASE", "OGN·049/298")]
    [InlineData("P2", "BASE", "OGN·049/298")]
    [InlineData("P1", "BATTLEFIELD", "OGN·049/298")]
    [InlineData("P2", "BATTLEFIELD", "OGN·049/298")]
    [InlineData("P2", "BATTLEFIELD", "OGN·192/298")]
    public async Task EqualOrGreaterUnitsAnywherePreventTheStrictPowerCondition(string controller, string zone, string blocker)
    {
        var state = Put(Position(), "BLOCKER", blocker, controller, zone, zone == "BATTLEFIELD" ? "BF" : null);
        Assert.True(state.CardObjects["BLOCKER"].Power >= state.CardObjects["NEW"].Power);
        var game = new Driver(state); await Play(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.False(game.State.CardObjects["SHRINE"].IsExhausted);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("BASE", "BASE", true)] [InlineData("BATTLEFIELD", "BASE", false)]
    [InlineData("BATTLEFIELD", "BATTLEFIELD", true)] [InlineData("BASE", "BATTLEFIELD", false)]
    public async Task TheEnteredUnitsCurrentAuraPowerIsComparedAtItsActualLocation(string auraZone, string unitZone, bool triggers)
    {
        var state = Put(Position(), "AURA", "OGS·013/024", "P1", auraZone, auraZone == "BATTLEFIELD" ? "BF" : null);
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("NEW", Unit, [], Destination: unitZone == "BASE" ? "BASE" : "BATTLEFIELD:BF"));
        Assert.Equal(triggers, game.State.PendingCardChoice is not null);
        if (triggers) { await Pay(game); await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand); }
        else Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("graveyard")] [InlineData("hand")]
    public async Task AnUnavailableSourceCannotPayOrDraw(string scenario)
    {
        var state = Position();
        if (scenario == "exhausted") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SHRINE"] = state.CardObjects["SHRINE"] with { IsExhausted = true } } };
        else state = RadUnitEffectTestFixture.Place(state, "SHRINE", "P1", scenario == "hand" ? "HAND" : "GRAVEYARD");
        var game = new Driver(state); await Play(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Fact]
    public async Task TheOpponentsStrongestUnitDoesNotTriggerYourEquipment()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "NEW", "P2", "HAND")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state); await game.Act("P2", new PlayCardCommand("NEW", Unit, []));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.False(game.State.CardObjects["SHRINE"].IsExhausted); await game.Replay();
    }

    [Fact]
    public Task AnUnaffordableUnitPlayCannotEarnTheTrigger()
    {
        var state = Position() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        return Reject(state, "P1", new PlayCardCommand("NEW", Unit, []));
    }

    [Fact]
    public async Task MultipleSourcesAreOrderedAndPayTheirOwnCostsIndependently()
    {
        var game = new Driver(Put(Position(), "SHRINE-2", Shrine, "P1", "BASE"));
        await Play(game);
        var ids = game.State.TriggerQueue.Select(t => t.TriggerId).ToArray(); Assert.Equal(2, ids.Length);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        for (var i = 0; i < 2; i++) {
            var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, choice.LegalObjectIds));
        }
        for (var resolved = 0; game.State.StackItems.Count > 0; resolved++) { Assert.InRange(resolved, 0, 2); await game.Top(); }
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.All(new[] { "SHRINE", "SHRINE-2" }, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        await game.Replay();
    }

    [Fact]
    public async Task ACreatedMinionCanBeTheOnlyAndThereforeStrongestUnit()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "ALLY", "P1", "GRAVEYARD");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "GRAVEYARD");
        state = RadUnitEffectTestFixture.Place(state, "NEW", "P1", "GRAVEYARD");
        var game = new Driver(Put(state, "FORGE", "OGN·212/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("FORGE", "OGN·212/298", [])); await game.Top();
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.NotNull(game.State.PendingCardChoice);
        await Pay(game); await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task APaidDrawSurvivesEquipmentDestructionDuringAnActualSpellDuelResponse()
    {
        var state = Put(Position(), "NEW", "UNL-120/219", "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        state = Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        state = Put(state, "DESTROY", "RAD·101/167", "P2", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.SpellDuelState.IsActive);
        await game.Act("P1", new PlayCardCommand("NEW", "UNL-120/219", [], Destination: "BATTLEFIELD:BF"));
        await Pay(game);
        if (game.State.PriorityPlayerId != "P2")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("DESTROY", "RAD·101/167", ["SHRINE"]));
        await game.Top();
        Assert.Contains("SHRINE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(game.State.StackItems, item => item.SourceObjectId == "SHRINE" && item.TriggerCost is not null);
        if (game.State.PriorityPlayerId != "P1")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["NEW"]));
        await game.Top();
        Assert.Contains("NEW", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand.Where(id => id.StartsWith("P1-D", StringComparison.Ordinal)));
        Assert.Empty(game.State.PlayerZones["P2"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("hand")] [InlineData("graveyard")] [InlineData("controller")]
    public async Task PaidDrawCheckpointsRetainTheCapturedControllerAfterSourceChanges(string change)
    {
        var capture = new Driver(Position()); await Play(capture); await Pay(capture);
        // These derived checkpoints verify structural recovery and resolution.
        // The fact above verifies actual response history and full replay.
        var state = RadUnitEffectTestFixture.Place(capture.State, "SHRINE", change == "controller" ? "P2" : "P1",
            change == "controller" ? "BASE" : change == "hand" ? "HAND" : "GRAVEYARD");
        if (change != "controller") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SHRINE"] = state.CardObjects["SHRINE"] with { ObjectGeneration = state.CardObjects["SHRINE"].ObjectGeneration + 1 } } };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var engine = new CoreRuleEngine();
        for (var passes = 0; state.StackItems.Count > 0; passes++)
        {
            Assert.InRange(passes, 0, 3);
            var result = await engine.ResolveAsync(state, new("paid-shrine-checkpoint-" + passes,
                state.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        }
        Assert.Single(state.PlayerZones["P1"].Hand.Where(id => id.StartsWith("P1-D", StringComparison.Ordinal)));
        Assert.Empty(state.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task ATokenBatchOnlyEarnsTheFirstStrictlyStrongestPlay()
    {
        var state = NoOtherUnits(Position());
        var game = new Driver(Put(state, "BATCH", "OGS·015/024", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("BATCH", "OGS·015/024", [])); await game.Top();
        Assert.Equal(4, game.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Single(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await Pay(game); await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task TheLegacyHeldEntryObservesItsFirstMinionAtThePlayInstruction()
    {
        var state = NoOtherUnits(Position());
        state = state with { Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "OGN·275/298", "P1") with { ControllerId = "P1" } } };
        var game = new Driver(state);
        await game.Act("P1", new PassPriorityCommand()); await game.Top();
        Assert.Single(game.Events().Where(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Equal("TRIGGER_COST_CONFIRMATION", game.State.PendingCardChoice!.ChoiceWindow);
        Assert.Equal("SHRINE", Assert.Single(game.State.StackItems).SourceObjectId);
        await Pay(game); await game.Top();
        Assert.True(game.State.CardObjects["SHRINE"].IsExhausted);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count); // Trigger draw, then normal turn draw.
        await game.Replay();
    }

    [Fact]
    public async Task ALastBreathBatchIncludesItsEarlierTokensInTheStrictComparison()
    {
        var state = Put(NoOtherUnits(Position()), "DEAD", "VEN·128", "P1", "BASE");
        state = Put(state, "KILL", "OGN·229/298", "P1", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 30, new Dictionary<string, int> { [RuneTrait.Yellow] = 10 }) } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("DEAD", "EMPOWER:0:0", [])); await game.Top();
        Assert.True(game.State.CardObjects["DEAD"].IsEmpowered);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["DEAD"])); await game.Top();
        Assert.Contains("DEAD", game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Single(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await Pay(game); await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsForgedCapturedSourceAndReceiptMetadata()
    {
        var game = new Driver(Position()); await Play(game); await Pay(game);
        var item = Assert.Single(game.State.StackItems);
        foreach (var context in new[] { item.UnitEvent! with { Timing = "START" },
            item.UnitEvent! with { SourceGeneration = 999 }, item.UnitEvent! with { CapturedTick = game.State.Tick + 1 } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { UnitEvent = context }] }), e => e.Contains("unit event"));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            TriggerCost = item.TriggerCost! with { Source = new("ALLY", 0) } }] }));
        await game.Top(); await game.Replay();
    }

    private static MatchState Position(string owner = "P1")
    {
        var state = Put(Create(), "SHRINE", Shrine, owner, "BASE");
        state = RadUnitEffectTestFixture.Place(state, "SHRINE", "P1", "BASE");
        return Put(state, "NEW", Unit, "P1", "HAND");
    }

    private static MatchState NoOtherUnits(MatchState state)
    {
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "GRAVEYARD");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "GRAVEYARD");
        return RadUnitEffectTestFixture.Place(state, "NEW", "P1", "GRAVEYARD");
    }

    private static Task Play(Driver game) => game.Act("P1", new PlayCardCommand("NEW", Unit, []));
    private static Task Pay(Driver game)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SHRINE"]));
    }
}
