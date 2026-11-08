using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PlayerDefenseMovementTests
{
    private const string Hound = "SFD·126/221";

    private static MatchState Position(string zone = "BASE", bool exhausted = false)
    {
        var state = RadUnitEffectTestFixture.Place(RadGearTestPosition.Create(), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "HOUND", Hound, "P1", zone == "OTHER" ? "BATTLEFIELD" : zone,
            zone == "OTHER" ? "OTHER" : null);
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return state with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOUND"] = state.CardObjects["HOUND"] with { IsExhausted = exhausted } } };
    }

    private static Task Challenge(Driver game)
        => game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));

    private static Task Confirm(Driver game, bool move = true)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("P1", choice.PlayerId); Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(["MOVE_SELF"], choice.LegalObjectIds);
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, move ? ["MOVE_SELF"] : []));
    }

    [Theory]
    [InlineData("BASE", false)] [InlineData("BASE", true)]
    [InlineData("OTHER", false)] [InlineData("OTHER", true)]
    public async Task APlayerDefendingAnywhereMayMoveAnActiveOrExhaustedHoundWithoutRoam(string zone, bool exhausted)
    {
        var game = new Driver(Position(zone, exhausted)); var pool = game.State.RunePools["P1"];
        await Challenge(game); Assert.Equal(zone == "OTHER" ? "OTHER" : null,
            game.State.ObjectLocations["HOUND"].BattlefieldObjectId);
        await Confirm(game); var skill = Assert.Single(game.State.StackItems);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
        Assert.Equal(UnitEventTimings.PlayerDefend, skill.UnitEvent!.Timing);
        Assert.Equal("BF", skill.UnitEvent.DefendedBattlefield!.ObjectId);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["HOUND"].BattlefieldObjectId);
        Assert.Equal(exhausted, game.State.CardObjects["HOUND"].IsExhausted);
        Assert.True(game.State.CardObjects["HOUND"].IsDefending); Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }

    [Fact]
    public async Task DecliningTheOptionalMovementRetiresTheEarnedSkillAndKeepsItsOriginalPosition()
    {
        var game = new Driver(Position()); await Challenge(game); await Confirm(game, false);
        Assert.Equal("BASE", game.State.ObjectLocations["HOUND"].Zone);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }

    [Theory]
    [InlineData("HAND")] [InlineData("GRAVEYARD")]
    public async Task ASourceOutsideTheFieldDoesNotObservePlayerDefense(string zone)
    {
        var game = new Driver(Position(zone)); await Challenge(game);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }

    [Fact]
    public async Task TheAttackersHoundDoesNotObserveTheOtherPlayersDefense()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "ALLY", "P1", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        var game = new Driver(state with { TurnPlayerId = "P1", ActivePlayerId = "P1" });
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyPresentHoundCannotMoveToTheSamePositionOrRetriggerThePlayersRole()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "HOUND", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await Challenge(game); await Confirm(game); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["HOUND"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
            && Equals(entry.Event.Payload.GetValueOrDefault("unitObjectId"), "HOUND"));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ARealResponseReturningTheSourceLeavesItsIndependentSkillButCannotMoveANewGeneration()
    {
        var game = new Driver(Position()); await Challenge(game); await Confirm(game);
        if (game.State.PriorityPlayerId != "P1")
            await game.Act(Assert.IsType<string>(game.State.PriorityPlayerId), new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOUND"])); await game.Top();
        Assert.Contains("HOUND", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("HOUND", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Contains("HOUND", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("duplicate")] [InlineData("battlefield")]
    public async Task RecoveryRequiresTheOriginalPlayerDefenseCaptureAndItsBattlefield(string change)
    {
        var game = new Driver(Position()); await Challenge(game); await Confirm(game);
        var item = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = change switch {
            "erase" => [], "duplicate" => [item, item],
            _ => [item with { UnitEvent = item.UnitEvent! with { DefendedBattlefield = new("OTHER", 0) } }] } };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged),
            error => error.Contains("observed unit event", StringComparison.Ordinal));
    }
}
