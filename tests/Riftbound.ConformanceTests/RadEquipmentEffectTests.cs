using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadEquipmentEffectTests
{
    [Theory]
    [InlineData("RAD_022_CREATE_GOLD", null, "金币", 0)]
    [InlineData("RAD_022_DESTROY_EQUIPMENT_CREATE_MINION", "GEAR", "随从", 1)]
    [InlineData("RAD_022_DESTROY_UNIT_CREATE_ROBOT", "ALLY", "机械", 3)]
    public async Task SpinningTopPaysItsCostBeforeCreatingThePrintedToken(string ability, string? cost, string tokenTag, int power)
    {
        var state = Put(Create(), "SOURCE", "RAD·022/167", "P1", "BASE");
        state = Put(state, "GEAR", "RAD·002/167", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        if (cost is not null) await Reject(game.State, "P1", new ActivateAbilityCommand("SOURCE", ability, ["ENEMY"]));
        await game.Act("P1", new ActivateAbilityCommand("SOURCE", ability, cost is null ? [] : [cost]));
        Assert.True(game.State.CardObjects["SOURCE"].IsExhausted);
        if (cost is not null) Assert.Contains(cost, game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
        await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
        Assert.Contains(tokenTag, token.Tags);
        Assert.Equal(power, token.Power);
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
        if (cost is null) Assert.True(token.IsExhausted);
        await Reject(game.State, "P1", new ActivateAbilityCommand("SOURCE", ability, cost is null ? [] : [cost]));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WakeflowerBuffsAFriendlyUnitAndExhaustsAPowerfulUnitToDraw(bool alreadyExhausted)
    {
        var state = Put(Create(), "SOURCE", "RAD·035/167", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("SOURCE", "RAD·035/167", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Top();
        Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        state = game.State;
        if (alreadyExhausted) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
        var activation = new ActivateAbilityCommand("SOURCE", P4ActivatedAbilityCatalog.WakeflowerAbilityId, ["ALLY"]);
        if (alreadyExhausted) await Reject(state, "P1", activation);
        else {
            await game.Act("P1", activation);
            Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
            await game.Top();
            Assert.Equal(new[] { "P1-D0" }, game.State.PlayerZones["P1"].Hand);
            await game.Replay();
        }
    }

    [Fact]
    public async Task RadiantAltarDrawsCallsASleepingRuneAndQueuesItsSeparateExhaustTrigger()
    {
        var state = Put(Create(), "SOURCE", "RAD·046/167", "P1", "BASE");
        state = Put(state, "RUNE", "OGN·007/298", "P1", "RUNE_DECK");
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("SOURCE", P4ActivatedAbilityCatalog.RadiantAltarAbilityId, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top();
        Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        Assert.True(game.State.CardObjects["RUNE"].IsExhausted);
        await Reject(game.State, "P1", new ActivateAbilityCommand("SOURCE", P4ActivatedAbilityCatalog.RadiantAltarAbilityId, []));
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·002/167", "强攻")]
    [InlineData("RAD·044/167", "坚守2")]
    [InlineData("RAD·087/167", "游走")]
    public async Task DeployedEquipmentGrantsItsKeywordOnlyToFriendlyUnitsHere(string no, string keyword)
    {
        var state = Put(Create(), "SOURCE", no, "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        var auras = state.ContinuousEffects.Where(e => e.SourceObjectId == "SOURCE" && e.Layer == ContinuousEffectLayers.RuleText).ToArray();
        Assert.Single(auras); Assert.Equal("ALLY", auras[0].TargetObjectId);
        Assert.Contains(keyword, auras[0].EffectId);
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BASE");
        Assert.DoesNotContain(state.ContinuousEffects, e => e.SourceObjectId == "SOURCE");
        var hand = Put(Create(), "SOURCE", no, "P1", "HAND");
        await Reject(hand, "P1", new PlayCardCommand("SOURCE", no, [], Destination: "BASE"));
    }

    [Fact]
    public async Task ExorcismBarrierBuffsBothLocationsAndDestroysItselfAfterAUnitIsPlayed()
    {
        var state = Put(Create(), "SOURCE", "RAD·039/167", "P1", "BASE");
        state = Put(state, "SECOND", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "CARD", "OGN·219/298", "P1", "HAND");
        Assert.Equal(new[] { "ALLY", "SECOND" }, state.ContinuousEffects.Where(e => e.SourceObjectId == "SOURCE" && e.PowerDelta == 2).Select(e => e.TargetObjectId).Order());
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CARD", "OGN·219/298", []));
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Base);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.State.ContinuousEffects, e => e.SourceObjectId == "SOURCE");
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·142/298", true)]
    [InlineData("OGN·219/298", false)]
    public async Task TitanShrineExhaustsAndDrawsOnlyForAStrictlyGreaterUnit(string no, bool greater)
    {
        var state = Put(Create(), "SOURCE", "RAD·089/167", "P1", "BASE");
        state = Put(state, "CARD", no, "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CARD", no, []));
        if (greater) {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SOURCE"]));
        }
        Assert.Equal(greater, game.State.CardObjects["SOURCE"].IsExhausted);
        Assert.Equal(greater ? 1 : 0, game.State.StackItems.Count);
        if (greater) { await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand); }
        else Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResourceExtractorCreatesGoldOnlyAtItsControllersEndWhileControllingTheField(bool controls)
    {
        var state = Put(Create(), "SOURCE", "RAD·052/167", "P1", "BATTLEFIELD", "BF");
        if (!controls) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        var game = new Driver(state);
        await game.Act("P1", new EndTurnCommand());
        if (controls) { Assert.Single(game.State.StackItems); await game.Top(); }
        var gold = game.State.CardObjects.Values.Where(c => c.Tags.Contains("金币")).ToArray();
        Assert.Equal(controls ? 1 : 0, gold.Length);
        Assert.All(gold, c => { Assert.True(c.IsExhausted); Assert.Contains(c.ObjectId, game.State.PlayerZones["P1"].Base); });
        await game.Replay();
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task AmbushTrapAddsOneStandbySlotAndRejectsOverflow(bool trap, int capacity)
    {
        var state = Create();
        if (trap) state = Put(state, "SOURCE", "RAD·108/167", "P1", "BATTLEFIELD", "BF");
        foreach (var id in new[] { "S1", "S2", "S3" }) state = Put(state, id, "OGN·135/298", "P1", "HAND");
        var game = new Driver(state);
        for (var i = 1; i <= capacity; i++)
            await game.Act("P1", new HideCardCommand("S" + i, "OGN·135/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        await Reject(game.State, "P1", new HideCardCommand("S" + (capacity + 1), "OGN·135/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        await game.Replay();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GravityFieldChargesOncePerGroupAndStacksIndependentSources(int copies)
    {
        var state = Create() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        for (var i = 0; i < copies; i++) state = Put(state, "SOURCE" + i, "RAD·062/167", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "ENEMY2", "OGN·219/298", "P2", "BASE");
        var game = new Driver(state);
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", [], ["ENEMY", "ENEMY2"]));
        Assert.Equal(30 - copies, game.State.RunePools["P2"].Mana);
        Assert.Equal(30 - copies, game.State.RunePools["P2"].TotalPower);
        await game.Replay();
        state = state with { RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        await Reject(state, "P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", [], ["ENEMY", "ENEMY2"]));
    }
}
