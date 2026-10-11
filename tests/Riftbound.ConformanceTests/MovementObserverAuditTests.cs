using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ChosenMovementTests;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MovementObserverAuditTests
{
    internal static MatchState Position(string unit = "SFD·130/221", bool tavern = false)
    {
        var state = Board(1);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["D"] = state.CardObjects["D"] with { CardNo = unit, IsExhausted = false, Damage = 0, OwnerId = "P1" },
            ["BF"] = state.CardObjects["BF"] with { CardNo = tavern ? "OGN·277/298" : "OGN·275/298" } } };
    }
    internal static async Task<ResolutionResult> Move(MatchState state, string destination = "BATTLEFIELD:BF2")
        => await Top((await Act(state, "P1", Command(destination, "D"))).State);
    private static string[] Gold(MatchState state, string player = "P2")
        => state.PlayerZones[player].Base.Where(id => state.CardObjects[id].Tags.Contains("金币")).ToArray();

    [Theory]
    [InlineData("BASE:P2")] [InlineData("BATTLEFIELD:BF2")]
    public async Task OpponentMovingTreasureHunterCreatesResponsiveSkillForItsController(string destination)
    {
        var moved = await Move(Position(), destination);
        Assert.Empty(Gold(moved.State));
        var skill = Assert.Single(moved.State.StackItems);
        Assert.Equal("P2", skill.ControllerId); Assert.Equal("P1", skill.MovementObserver!.Unit.MovingPlayerId);
        Assert.Equal("P2", skill.MovementObserver.Unit.ControllerId); Assert.Equal("BF", skill.MovementObserver.Unit.Origin.BattlefieldObjectId);
        Assert.DoesNotContain(moved.Events, e => e.Kind.StartsWith("INTERNAL_")); Restore(moved.State);
        var resolved = await Top(moved.State); var gold = Assert.Single(Gold(resolved.State));
        Assert.True(resolved.State.CardObjects[gold].IsExhausted); Assert.Equal("P2", resolved.State.CardObjects[gold].OwnerId);
        Assert.Empty(Gold(resolved.State, "P1")); Restore(resolved.State);
    }

    [Theory]
    [InlineData("UNL-022/219")] [InlineData("UNL-022a/219")]
    public async Task OpponentMovingJhinImmediatelyCreditsJhinController(string no)
    {
        var initial = Position(no); var moved = await Move(initial);
        Assert.Equal(initial.RunePools["P2"].Mana + 1, moved.State.RunePools["P2"].Mana); Assert.Equal(initial.RunePools["P2"].Power + 1, moved.State.RunePools["P2"].Power);
        Assert.Empty(moved.State.StackItems); Assert.Empty(moved.State.TriggerQueue); Restore(moved.State);
    }

    [Theory]
    [InlineData("BASE:P2")] [InlineData("BATTLEFIELD:BF2")]
    public async Task TavernObservesEveryUnitLeavingItsExactLocationAfterResponse(string destination)
    {
        var moved = await Move(Position("UNL-008/219", true), destination);
        var skill = Assert.Single(moved.State.StackItems); Assert.Equal("BF", skill.SourceObjectId);
        Assert.Equal(2, moved.State.CardObjects["D"].Power); Restore(moved.State);
        var resolved = await Top(moved.State); Assert.Equal(3, resolved.State.CardObjects["D"].Power); Restore(resolved.State);
    }

    [Fact]
    public async Task TavernAtUnrelatedFieldDoesNotObserveMovement()
    {
        var state = Position("UNL-008/219", true);
        state = At(state, "D", "P2", "BATTLEFIELD", "BF2");
        var moved = await Move(state, "BASE:P2"); Assert.Empty(moved.State.StackItems); Assert.Empty(moved.State.TriggerQueue);
        Assert.Equal(2, moved.State.CardObjects["D"].Power); Restore(moved.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EarnedGoldSurvivesSourceLeavingOrChangingControl(bool changedControl)
    {
        var moved = await Move(Position(), "BASE:P2"); var state = moved.State;
        if (changedControl) state = At(state, "D", "P1", "BASE");
        else state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Base = state.PlayerZones["P2"].Base.Where(id => id != "D").ToArray(), Graveyard = [..state.PlayerZones["P2"].Graveyard, "D"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["D"] = new("P2", "GRAVEYARD") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["D"] = state.CardObjects["D"] with { ObjectGeneration = 1, MovementCountThisTurn = 0 } } };
        Restore(state); var resolved = await Top(state); Assert.Single(Gold(resolved.State)); Assert.Empty(Gold(resolved.State, "P1")); Restore(resolved.State);
    }

    [Fact]
    public async Task TavernCannotBuffANewIncarnationOfTheMovedUnit()
    {
        var moved = await Move(Position("UNL-008/219", true));
        var state = moved.State with { CardObjects = new Dictionary<string, CardObjectState>(moved.State.CardObjects) {
            ["D"] = moved.State.CardObjects["D"] with { ObjectGeneration = 1 } } };
        var resolved = await Top(state); Assert.Equal(2, resolved.State.CardObjects["D"].Power); Restore(resolved.State);
    }

    [Fact]
    public async Task SimultaneousMovementKeepsEveryGoldAndTavernTriggerAvailableForOrdering()
    {
        var state = Position(tavern: true); state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["D2"] = state.CardObjects["D"] with { ObjectId = "D2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Battlefields = [..state.PlayerZones["P2"].Battlefields, "D2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["D2"] = new("P2", "BATTLEFIELD", "BF") } };
        var moved = await Top((await Act(state, "P1", Command("BASE:P2", "D", "D2"))).State);
        Assert.Equal(4, moved.State.TriggerQueue.Count); Assert.Empty(Gold(moved.State));
        Assert.True(OfficialInsightAndSpellLockTests.Errors(moved.State).Count == 0, string.Join("; ", OfficialInsightAndSpellLockTests.Errors(moved.State))); Restore(moved.State);
        var ordered = await Act(moved.State, moved.State.TriggerQueue[0].ControllerId,
            new OrderTriggersCommand(OrderedTriggerIds: moved.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        var current = ordered;
        while (current.State.StackItems.Count > 0) current = await Top(current.State);
        Assert.Equal(2, Gold(current.State).Length); Assert.Equal(3, current.State.CardObjects["D"].Power); Assert.Equal(3, current.State.CardObjects["D2"].Power); Restore(current.State);
    }
    [Fact]
    public async Task MovingOutOfAnAuraCanKillTheHunterBeforeItsGoldSkillResolves()
    {
        var state = Position();
        state = At(DeathObserverAuditTests.Source(state, "OGS·013/024", "GAREN"), "GAREN", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["D"] = state.CardObjects["D"] with { Power = 1, Damage = 1 } } };
        var moved = await Move(state, "BASE:P2");
        Assert.Contains("D", moved.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(Gold(moved.State)); Assert.Single(moved.State.StackItems); Restore(moved.State);
        var resolved = await Top(moved.State); Assert.Single(Gold(resolved.State)); Restore(resolved.State);
    }

    [Theory]
    [InlineData("missing")] [InlineData("source")] [InlineData("generation")] [InlineData("no-move")]
    public async Task RecoveryRejectsMissingOrForgedMovementFacts(string kind)
    {
        var moved = await Move(Position(), "BASE:P2"); var skill = Assert.Single(moved.State.StackItems);
        var context = skill.MovementObserver!;
        context = kind switch {
            "source" => context with { CardNo = "OGN·001/298" },
            "generation" => context with { Unit = context.Unit with { Generation = -1 } },
            "no-move" => context with { Unit = context.Unit with { Destination = context.Unit.Origin } }, _ => context };
        var forged = moved.State with { StackItems = [skill with { MovementObserver = kind == "missing" ? null : context }] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), error => error.Contains("movement observer"));
    }

    [Fact]
    public async Task DamageAndBanishReplacementDoNotCountAsMovement()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["AOE"] = state.CardObjects["AOE"] with { CardNo = "UNL-007/219" } } };
        var done = await Top((await Act(state, "P1", new PlayCardCommand("AOE", "UNL-007/219", ["D"]))).State);
        Assert.Empty(Gold(done.State)); Assert.Empty(done.State.StackItems);
        Assert.DoesNotContain(done.Events, e => e.Payload.GetValueOrDefault("observerKind") as string == TriggerKinds.UnitMovedCreateDormantGold);
        Restore(done.State);
    }

}
