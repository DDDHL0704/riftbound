using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentGraveyardPlayTests
{
    private const string Gear = "SFD·150/221", Unit = "OGN·219/298";

    private static MatchState Position(bool attached = true, bool holding = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "GEAR", Gear, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "DEAD", Unit, "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "SECOND-DEAD", "OGN·001/298", "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "DEAD-GEAR", "OGN·023/298", "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "THEIR-DEAD", Unit, "P2", "GRAVEYARD");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        if (holding) state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        return attached ? state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } } : state;
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task ActualConquestOrHoldEarnsAnOptionalPaidPlayFromItsControllersGraveyard(bool holding, bool decline)
    {
        var game = new Driver(Position(holding: holding));
        if (holding) {
            await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        } else await Conquer(game);
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(Gear, skill.UnitEvent!.CardNo);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(game.State.PendingEffectPlay);
        await game.Top(); var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(new[] { "DEAD", "SECOND-DEAD" }, pending.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.False(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
        Assert.Empty(pending.Parent.TargetObjectIds);
        if (decline) await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        else {
            var pool = game.State.RunePools["P1"]; var mana = game.State.CardObjects["DEAD"].ManaCost;
            await game.Act("P1", new PlayCardCommand("DEAD", Unit, [], Destination: "BASE"));
            Assert.Equal(pool.Mana - mana, game.State.RunePools["P1"].Mana);
            Assert.Contains("DEAD", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["DEAD"].IsExhausted);
        }
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task AnUnattachedWeaponDoesNotOfferItsLowerBoxOnAnotherUnitsConquest()
    {
        var game = new Driver(Position(false)); await Conquer(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingEffectPlay); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheHostAfterConquestKeepsTheIndependentPlayPermissionAndMasksItsSource()
    {
        var game = new Driver(Position()); await Conquer(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("ALLY", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("DEAD", Unit, [], Destination: "BASE"));
        Assert.Contains("DEAD", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task NoManaCannotWaiveTheUnitsCostOrConsumeThePermissionButItCanBeDeclined()
    {
        var initial = Position(); initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = RunePool.Empty } };
        var game = new Driver(initial); await Conquer(game); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("DEAD", Unit, [], Destination: "BASE"));
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("DEAD", game.State.PlayerZones["P1"].Graveyard); Assert.Null(game.State.PendingEffectPlay); await game.Replay();
    }

    [Theory]
    [InlineData("waiver")] [InlineData("remove-source")] [InlineData("opponent-source")] [InlineData("extra-target")]
    public async Task RecoveryBindsThePermissionToTheOriginalSourcesWithoutInventingTargetsOrFreeCosts(string branch)
    {
        var game = new Driver(Position()); await Conquer(game); await game.Top();
        var pending = game.State.PendingEffectPlay!;
        var forged = game.State with { PendingEffectPlay = branch switch {
            "waiver" => pending with { IgnoreBaseMana = true },
            "remove-source" => pending with { Sources = new Dictionary<string, long> { ["DEAD"] = pending.Sources["DEAD"] } },
            "opponent-source" => pending with { Sources = new Dictionary<string, long> { ["THEIR-DEAD"] = 0 } },
            _ => pending with { Parent = pending.Parent with { TargetObjectIds = ["DEAD"] } } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
    }
}
