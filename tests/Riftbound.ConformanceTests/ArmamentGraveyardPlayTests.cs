using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentGraveyardPlayTests
{
    private const string Gear = "SFD·150/221", Unit = "OGN·041/298";

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
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static Task Confirm(Driver game, bool accept = true)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "DEAD", "SECOND-DEAD" }, choice.LegalObjectIds.Order(StringComparer.Ordinal));
        return game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["DEAD"] : []));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task ActualConquestOrHoldConfirmsAGraveyardTargetBeforeResponsesAndPaysBothBaseCosts(bool holding, bool decline)
    {
        var game = new Driver(Position(holding: holding));
        if (holding) {
            await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        } else await Conquer(game);
        Assert.Null(game.State.PendingEffectPlay); await Confirm(game, !decline);
        if (decline) Assert.Empty(game.State.StackItems);
        else {
            var skill = Assert.Single(game.State.StackItems); Assert.Equal(Gear, skill.UnitEvent!.CardNo);
            Assert.Equal(["DEAD"], skill.TargetObjectIds); Assert.Equal(0, skill.TargetGenerations!["DEAD"]);
            await game.Top(); var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.Equal(["DEAD"], pending.Sources.Keys);
            Assert.False(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
            var pool = game.State.RunePools["P1"]; var mana = game.State.CardObjects["DEAD"].ManaCost;
            await game.Act("P1", new PlayCardCommand("DEAD", Unit, [], Destination: "BASE"));
            Assert.Equal(pool.Mana - mana, game.State.RunePools["P1"].Mana);
            Assert.Equal(pool.TotalPower - 2, game.State.RunePools["P1"].TotalPower);
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
        var game = new Driver(Position()); await Conquer(game); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("ALLY", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("DEAD", Unit, [], Destination: "BASE"));
        Assert.Contains("DEAD", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task MissingManaOrPowerCannotWaiveTheConfirmedUnitsCostButThePaymentCanBeDeclined(bool mana)
    {
        var initial = Position(); initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = mana ? new(0, 30) : new(30, 0) } };
        var game = new Driver(initial); await Conquer(game); await Confirm(game); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("DEAD", Unit, [], Destination: "BASE"));
        await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("DEAD", game.State.PlayerZones["P1"].Graveyard); Assert.Null(game.State.PendingEffectPlay); await game.Replay();
    }

    [Theory]
    [InlineData("waiver")] [InlineData("different-source")] [InlineData("opponent-source")] [InlineData("extra-target")]
    public async Task RecoveryRequiresTheConfirmedGraveyardTargetAndNormalCostPolicy(string branch)
    {
        var game = new Driver(Position()); await Conquer(game); await Confirm(game); await game.Top();
        var pending = game.State.PendingEffectPlay!;
        var forged = game.State with { PendingEffectPlay = branch switch {
            "waiver" => pending with { IgnoreBaseMana = true },
            "different-source" => pending with { Sources = new Dictionary<string, long> { ["SECOND-DEAD"] = pending.Sources["DEAD"] } },
            "opponent-source" => pending with { Sources = new Dictionary<string, long> { ["THEIR-DEAD"] = 0 } },
            _ => pending with { Parent = pending.Parent with { TargetObjectIds = ["DEAD", "SECOND-DEAD"] } } } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
    }

    [Fact]
    public async Task ATargetPlayedByTheFirstSkillCannotBeReplacedWithAnotherCardForTheSecondSkill()
    {
        var initial = RadGearTestPosition.Put(Position(), "SECOND-GEAR", Gear, "P1", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SECOND-GEAR"] = initial.CardObjects["SECOND-GEAR"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(initial); await Conquer(game); await Confirm(game); await Confirm(game);
        Assert.Equal(2, game.State.StackItems.Count); await game.Top();
        await game.Act("P1", new PlayCardCommand("DEAD", Unit, [], Destination: "BASE"));
        Assert.Contains("DEAD", game.State.PlayerZones["P1"].Base); await game.Top();
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.Contains("SECOND-DEAD", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }
}
