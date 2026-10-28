using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class LegendEmpowerOtherTests
{
    private static MatchState SpellPosition(string actor, bool targetAlreadyEmpowered = false)
    {
        var state = VenFlowCombatKeywordTests.Position(false);
        state = state with {
            TurnPlayerId = actor, ActivePlayerId = actor,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: "VEN·151", ownerId: "P1", controllerId: "P1", tags: ["传奇"]),
                ["ALLY"] = state.CardObjects["ALLY"] with { IsEmpowered = targetAlreadyEmpowered } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                [actor] = new(20, 0, new Dictionary<string, int> { ["green"] = 10 }) } };
        return AttackDamageDistributionTests.Spell(state, "STATUS", "VEN·035", actor);
    }

    [Theory]
    [InlineData("VEN·151")]
    [InlineData("VEN·195")]
    [InlineData("VEN·195*")]
    [InlineData("VEN·153")]
    [InlineData("VEN·196")]
    [InlineData("VEN·196*")]
    public async Task EmpoweringAnotherObjectQueuesAnIndependentLegendSkill(string cardNo)
    {
        var state = LegendAttachmentTests.PositionFor(cardNo);
        var printed = (await EmpowerTests.Enter()).CardObjects["HOST"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { CardNo = "VEN·074", ManaCost = printed.ManaCost, Power = printed.Power } } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.NotNull(Assert.Single(game.State.StackItems).EmpowerOtherObserver);
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "ENEMY", true)]
    [InlineData("P2", "ALLY", false)]
    public async Task ThePlayerPerformingTheActionDeterminesWhichLegendObservesIt(string actor, string target, bool shouldTrigger)
    {
        var state = SpellPosition(actor);
        var cast = await Act(state, actor, new PlayCardCommand("STATUS", "VEN·035", [target], Mode: "EMPOWER_THEN_REMOVE"));
        var resolved = await Top(cast.State);
        Assert.True(resolved.State.CardObjects[target].IsEmpowered);
        Assert.Equal(shouldTrigger, resolved.State.StackItems.Any(item => item.EmpowerOtherObserver is not null));
        Assert.False(resolved.State.CardObjects["LEGEND"].IsEmpowered);
        if (shouldTrigger)
        {
            var done = await Top(resolved.State);
            Assert.True(done.State.CardObjects["LEGEND"].IsEmpowered);
        }
    }

    [Fact]
    public async Task AttemptingToEmpowerAnAlreadyEmpoweredObjectDoesNotTrigger()
    {
        var state = SpellPosition("P1", targetAlreadyEmpowered: true);
        var cast = await Act(state, "P1", new PlayCardCommand("STATUS", "VEN·035", ["ALLY"], Mode: "EMPOWER_THEN_REMOVE"));
        var done = await Top(cast.State);
        Assert.True(done.State.CardObjects["ALLY"].IsEmpowered);
        Assert.False(done.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.DoesNotContain(done.State.StackItems, item => item.EmpowerOtherObserver is not null);
        Assert.DoesNotContain(done.State.TriggerQueue, item => item.EmpowerOtherObserver is not null);
    }

    [Fact]
    public async Task EmpoweringAnotherLegendTriggersOnceButItsOwnEmpowermentDoesNotLoop()
    {
        var state = LegendAttachmentTests.PositionFor("VEN·139");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND", "OBSERVER"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OBSERVER"] = new("OBSERVER", cardNo: "VEN·151", ownerId: "P1", controllerId: "P1", tags: ["传奇"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["OBSERVER"] = new("P1", "LEGEND") } };
        var game = new VenBasicFlowSpellTests.Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("LEGEND", "EMPOWER:0:0", []));
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.NotNull(Assert.Single(game.State.StackItems).EmpowerOtherObserver);
        await game.Top();
        Assert.True(game.State.CardObjects["OBSERVER"].IsEmpowered);
        Assert.DoesNotContain(game.State.StackItems, item => item.EmpowerOtherObserver is not null);
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.EmpowerOtherObserver is not null);
        await game.Replay();
    }

    [Theory]
    [InlineData("context")]
    [InlineData("card")]
    [InlineData("controller")]
    [InlineData("same-object")]
    [InlineData("target-generation")]
    public async Task RecoveryRejectsForgedEmpowerObserver(string change)
    {
        var state = LegendAttachmentTests.PositionFor("VEN·151");
        var printed = (await EmpowerTests.Enter()).CardObjects["HOST"];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { CardNo = "VEN·074", ManaCost = printed.ManaCost, Power = printed.Power } } };
        var activated = await Act(state, "P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        var resolved = await Top(activated.State);
        var item = Assert.Single(resolved.State.StackItems);
        var context = item.EmpowerOtherObserver!;
        var forged = item with { EmpowerOtherObserver = change switch {
            "context" => null,
            "card" => context with { CardNo = "FORGED" },
            "controller" => context with { ControllerId = "P2" },
            "same-object" => context with { Observed = context.Source },
            _ => context with { Observed = context.Observed with { Generation = context.Observed.Generation + 1 } } } };
        Assert.Contains("invalid empower observer stack", OfficialInsightAndSpellLockTests.Errors(resolved.State with { StackItems = [forged] }));
    }

    [Fact]
    public async Task EarnedObserverDoesNotRevealItsSourceAfterThatObjectReturnsToHand()
    {
        var state = LegendAttachmentTests.PositionFor("VEN·151");
        var printed = (await EmpowerTests.Enter()).CardObjects["HOST"];
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = [], Battlefields = [..state.PlayerZones["P1"].Battlefields, "LEGEND"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { CardNo = "VEN·074", ManaCost = printed.ManaCost, Power = printed.Power },
                ["LEGEND"] = state.CardObjects["LEGEND"] with { Tags = [CardObjectTags.UnitCard] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "BATTLEFIELD", "F") } };
        state = EquipChainTests.Response(state, "OGN·169/298", "P2");
        var activated = await Act(state, "P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        var triggered = await Top(activated.State);
        Assert.NotNull(Assert.Single(triggered.State.StackItems).EmpowerOtherObserver);
        var passed = await Act(triggered.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["LEGEND"]));
        var returned = await Top(response.State);
        Assert.Contains("LEGEND", returned.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("LEGEND", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(returned.State)["P2"].Stack));
        ArmamentMightTests.PublicRecovery(returned.State);
        var done = await Top(returned.State);
        Assert.DoesNotContain("\"LEGEND\"", JsonSerializer.Serialize(done.Events));
        Assert.False(done.State.CardObjects["LEGEND"].IsEmpowered);
    }
}
