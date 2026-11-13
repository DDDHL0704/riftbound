using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RisingDragonKickLevelTests
{
    private const string Kick = "UNL-038/219";
    private static MatchState Position(int experience)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", Kick, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "SECOND", "OGN·219/298", "P2", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P2", "HAND");
        state = RadGearTestPosition.Put(state, "POPPY", "UNL-178/219", "P1", "HAND");
        return state with { PlayerExperience = new Dictionary<string, int> { ["P1"] = experience, ["P2"] = 0 } };
    }
    private static PlayCardCommand Play(int experience, bool same = false)
        => new("SPELL", Kick, experience >= 6 ? ["ENEMY", same ? "ENEMY" : "SECOND"] : ["ENEMY"]);

    [Theory]
    [InlineData(0, false)] [InlineData(5, false)] [InlineData(6, false)] [InlineData(12, false)]
    [InlineData(0, true)] [InlineData(5, true)] [InlineData(6, true)] [InlineData(12, true)]
    public async Task TheLevelInstructionHasItsOwnEnemyTargetAndExecutesAfterOnlyTheFirstTargetsMovement(int experience, bool same)
    {
        var initial = Position(experience); var command = Play(experience, same);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.IsValid, quote.Message); var game = new Driver(initial);
        await game.Act("P1", command); Assert.Equal("BASE:P2", Assert.Single(game.State.StackItems).Destination);
        Assert.Equal(experience >= 6 ? 2 : 1, Assert.Single(game.State.StackItems).TargetObjectIds.Count);
        await game.Top(); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.Equal(1, game.State.CardObjects["ENEMY"].MovementCountThisTurn);
        Assert.Equal(0, game.State.CardObjects["SECOND"].MovementCountThisTurn);
        var stunned = same ? "ENEMY" : "SECOND";
        Assert.Equal(experience >= 6, game.State.CardObjects[stunned].UntilEndOfTurnEffects.Contains("STUNNED"));
        if (experience >= 6)
        {
            var events = game.Events().Select(entry => entry.Event).ToList();
            Assert.True(events.FindIndex(e => e.Kind == "UNIT_MOVED_TO_BASE")
                < events.FindIndex(e => e.Kind == "STATUS_EFFECT_APPLIED"));
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(5)] [InlineData(6)]
    public async Task TargetCountMustMatchTheCurrentlyActiveDependentInstruction(int experience)
    {
        var state = Position(experience);
        var wrong = new PlayCardCommand("SPELL", Kick, experience < 6 ? ["ENEMY", "SECOND"] : ["ENEMY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, wrong));
        Assert.False(quote.IsValid); await RadGearTestPosition.Reject(state, "P1", wrong);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PayingExperienceInARealPoppyResponseDisablesOnlyTheLaterStun(bool echo)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(6), "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = initial with { UntilEndOfTurnEffects = echo ? [EchoCostRules.GrantPrefix + "P1"] : [] };
        var game = new Driver(initial); await game.Act("P1", Play(6) with { OptionalCosts = echo ? ["ECHO"] : [] });
        await game.Act("P1", new PlayCardCommand("POPPY", "UNL-178/219", [], Destination: "BATTLEFIELD:BF",
            Mode: "AMBUSH", OptionalCosts: ["SPEND_EXPERIENCE:3"]));
        Assert.Equal(3, game.State.PlayerExperience["P1"]); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["SECOND"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExperienceEarnedByARealResponseCannotAddATargetToTheAlreadyConfirmedSpell(bool echo)
    {
        var state = RadGearTestPosition.Put(Position(5), "RHYTHM", "UNL-184/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "GAIN", "UNL-092/219", "P1", "BASE");
        state = state with { UntilEndOfTurnEffects = echo ? [EchoCostRules.GrantPrefix + "P1"] : [] };
        var game = new Driver(state);
        await game.Act("P1", Play(5) with { OptionalCosts = echo ? ["ECHO"] : [] });
        var kick = Assert.Single(game.State.StackItems);
        Assert.Single(kick.TargetObjectIds);
        await game.Act("P1", new PlayCardCommand("RHYTHM", "UNL-184/219", ["GAIN"]));
        await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("GAIN", "UNL-092/219", [], Destination: "BATTLEFIELD:OTHER"));
        for (var step = 0; game.State.StackItems[^1].StackItemId != kick.StackItemId; step++)
        {
            Assert.InRange(step, 0, 3);
            await game.Top();
        }
        Assert.Equal(6, game.State.PlayerExperience["P1"]);
        Assert.Equal(kick.TargetObjectIds, Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.All(new[] { "ENEMY", "SECOND" }, id =>
            Assert.DoesNotContain("STUNNED", game.State.CardObjects[id].UntilEndOfTurnEffects));
        await game.Replay();
    }

    [Theory]
    [InlineData("ENEMY")] [InlineData("SECOND")]
    public async Task AnActualReturnOfEitherTargetCannotCancelTheSeparateInstructionForTheOtherTarget(string returned)
    {
        var game = new Driver(Position(6)); await game.Act("P1", Play(6)); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top(); await game.Top();
        Assert.Contains(returned, game.State.PlayerZones["P2"].Hand);
        if (returned == "ENEMY") Assert.Contains("STUNNED", game.State.CardObjects["SECOND"].UntilEndOfTurnEffects);
        else
        {
            Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
            Assert.DoesNotContain("STUNNED", game.State.CardObjects["SECOND"].UntilEndOfTurnEffects);
        }
        await game.Replay();
    }

    [Fact]
    public async Task AProhibitedMoveDoesNotPreventTheIndependentLevelStun()
    {
        var state = Position(6); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = [MovementRestrictionRules.ForPlayer("P1")] } } };
        var game = new Driver(state); await game.Act("P1", Play(6)); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Contains("STUNNED", game.State.CardObjects["SECOND"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task TheCastingControllerSuppliesTheLevelEvenWhenThePhysicalSpellBelongsToTheOpponent()
    {
        var state = Position(6); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SPELL"] = state.CardObjects["SPELL"] with { OwnerId = "P2" } } };
        var game = new Driver(state); await game.Act("P1", Play(6)); await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["SECOND"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Fact]
    public async Task EchoRepeatsTheIndependentStatusWithoutInventingASecondMoveToTheSameLocation()
    {
        var game = new Driver(Position(6) with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] });
        await game.Act("P1", Play(6) with { OptionalCosts = ["ECHO"] }); await game.Top();
        Assert.Contains("STUNNED", game.State.CardObjects["SECOND"].UntilEndOfTurnEffects);
        Assert.Equal(1, game.State.CardObjects["ENEMY"].MovementCountThisTurn); await game.Replay();
    }

    [Fact]
    public async Task RecoveryCannotAddAnUndeclaredTargetToAnOriginalBelowLevelPlay()
    {
        var game = new Driver(Position(5)); await game.Act("P1", Play(5)); var item = Assert.Single(game.State.StackItems);
        var forged = item with { TargetObjectIds = ["ENEMY", "SECOND"], TargetGenerations =
            new Dictionary<string, long>(item.TargetGenerations!) { ["SECOND"] = game.State.CardObjects["SECOND"].ObjectGeneration } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = [forged] }));
        await game.Top(); await game.Replay();
    }
}
