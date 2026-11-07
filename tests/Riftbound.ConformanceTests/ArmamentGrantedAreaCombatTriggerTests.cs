using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Fixture = Riftbound.ConformanceTests.ArmamentGrantedCombatTriggerTests;

namespace Riftbound.ConformanceTests;

// SFD·190 lower box grants the host an untargeted attack/defend skill.
// CN718.3 / 718.5.e–g / 719.1; 383; 359.3.f.2; 403; 418.
public sealed class ArmamentGrantedAreaCombatTriggerTests
{
    private const string Cloak = "SFD·190/221";

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task NativeCloakPlayAndAssemblyDamageEveryEnemyHereWithoutTargetingOrSpellshieldPayment(bool defend, bool bonus)
    {
        var state = Fixture.Position(defend, Cloak);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·161/298", out var ward));
        var enemy = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY1", "OGN·161/298", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY1"] = enemy with { Tags = enemy.Tags.Concat(ward.SourceUnitTags.Split('|')).Distinct().ToArray() } } };
        if (bonus) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["F"] = OfficialGarenConquestBattlefieldCountTests.Printed("F", "OGN·296/298", "P1") with { ControllerId = defend ? "P1" : "P2" } } };
        state = RadGearTestPosition.Put(state, "REMOTE-F", "OGN·278/298", "P2", "BATTLEFIELD", "REMOTE-F");
        state = RadGearTestPosition.Put(state, "REMOTE", "OGN·082/298", "P2", "BATTLEFIELD", "REMOTE-F");
        if (defend) state = RadUnitEffectTestFixture.Place(state, "ENEMY2", "P2", "BATTLEFIELD", "REMOTE-F");
        var game = new Driver(state);
        await Fixture.Attach(game, cardNo: Cloak, cost: "ASSEMBLE_ANY_POWER");
        await Fixture.Battle(game, defend);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(Cloak, skill.CardNo); Assert.Equal("HOST", skill.SourceObjectId);
        Assert.Empty(skill.TargetObjectIds); Assert.Null(skill.TargetGenerations);
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Top();
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(bonus ? 3 : 2, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(defend ? 0 : bonus ? 3 : 2, game.State.CardObjects["ENEMY2"].Damage);
        Assert.Equal(0, game.State.CardObjects["REMOTE"].Damage);
        Assert.Equal(0, game.State.CardObjects["HOST"].Damage);
        Assert.Equal(defend ? 1 : 2, game.Events().Count(e => e.Event.Kind == "DAMAGE_APPLIED"));
        await game.Replay();
    }

    [Fact]
    public async Task UnattachedCloakDoesNotGrantItsLowerTextToTheUnit()
    {
        var game = new Driver(Fixture.Position(gear: Cloak));
        await game.Act("P1", new PlayCardCommand("GEAR", Cloak, [], Destination: "BASE"));
        await Fixture.Battle(game, false);
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent?.Origin?.ArmamentGranted == true);
        Assert.DoesNotContain(game.State.TriggerQueue, i => i.UnitEvent?.Origin?.ArmamentGranted == true);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await game.Replay();
    }

    [Fact]
    public async Task BowAndCloakKeepTheirIndependentTargetedAndUntargetedInstructions()
    {
        var state = RadGearTestPosition.Put(Fixture.Position(gear: Cloak), "BOW", "SFD·016/221", "P1", "HAND");
        var game = new Driver(state);
        await Fixture.Attach(game, cardNo: Cloak, cost: "ASSEMBLE_ANY_POWER");
        await Fixture.Attach(game, "BOW"); await Fixture.Battle(game, false);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal("SFD·016/221", game.State.StackItems.Single(i => i.StackItemId == game.State.PendingCardChoice!.ResolvingStackItemId).CardNo);
        await Fixture.Choose(game, "ENEMY1");
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top(); await game.Top();
        Assert.Equal(4, game.State.CardObjects["ENEMY1"].Damage);
        Assert.Equal(2, game.State.CardObjects["ENEMY2"].Damage);
        Assert.Equal(3, game.Events().Count(e => e.Event.Kind == "DAMAGE_APPLIED"));
        await game.Replay();
    }

    [Fact]
    public async Task ADepartedCloakHostKeepsItsEarnedIndependentSkillButCannotSupplyHereOrLeakPrivateIds()
    {
        var state = AttackDamageDistributionTests.Spell(Fixture.Position(gear: Cloak), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Fixture.Attach(game, cardNo: Cloak, cost: "ASSEMBLE_ANY_POWER");
        await Fixture.Battle(game, false);
        var captured = Assert.Single(game.State.StackItems).UnitEvent!.Origin;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["HOST"].Zone);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(captured, Assert.Single(game.State.StackItems).UnitEvent!.Origin);
        Assert.Empty(Fixture.Errors(game, game.State));
        var view = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack);
        Assert.DoesNotContain("HOST", view); Assert.DoesNotContain("armamentHostCardNo", view);
        var sequence = game.Commands().Last().CompletedEventSequence;
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
        Assert.DoesNotContain("HOST", JsonSerializer.Serialize(spectator.SpectatorSnapshot.Stack));
        var snapshots = ResolutionResult.BuildSnapshots(game.State);
        var leaked = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshots["P2"].Stack));
        leaked["sourceObjectId"] = "HOST"; leaked["sourceVisibility"] = "VISIBLE";
        var forgedViews = game.State.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, game.State.Tick, sequence, snapshots[player], game.State.Tick,
                sequence, ResolutionResult.BuildPrompts(game.State)[player]));
        var errors = MatchRecoveryValidator.Validate(game.State.RoomId, sequence, game.Commands(), game.Events(),
            forgedViews, game.State, game.State.Tick, spectator);
        Assert.Contains(errors, e => e.Contains("sourceObjectId disagrees", StringComparison.Ordinal));
        await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); Assert.Equal(0, game.State.CardObjects["ENEMY2"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task RecoveryCannotEraseTheCloakGrantOrInventTargetsForItsUntargetedInstruction()
    {
        var game = new Driver(Fixture.Position(gear: Cloak));
        await Fixture.Attach(game, cardNo: Cloak, cost: "ASSEMBLE_ANY_POWER"); await Fixture.Battle(game, false);
        var item = Assert.Single(game.State.StackItems);
        Assert.Empty(Fixture.Errors(game, game.State));
        var erased = game.State with { StackItems = [item with { UnitEvent = item.UnitEvent! with {
            Origin = item.UnitEvent.Origin! with { ArmamentGranted = false } } }] };
        Assert.Contains(Fixture.Errors(game, erased), e => e.Contains("original accepted grant event", StringComparison.Ordinal));
        var targeted = game.State with { StackItems = [item with { TargetObjectIds = ["ENEMY1"],
            TargetGenerations = new Dictionary<string, long> { ["ENEMY1"] = game.State.CardObjects["ENEMY1"].ObjectGeneration } }] };
        Assert.NotEmpty(Fixture.Errors(game, targeted));
    }

    [Theory]
    [InlineData("SFD·016/221")] [InlineData(Cloak)]
    public async Task TheHostControlsTheGrantedSkillEvenWhenItsArmamentHasADifferentController(string cardNo)
    {
        var state = Fixture.Position(true, cardNo);
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P2", "BATTLEFIELD", "F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = "P2", ControllerId = "P2", AttachedToObjectId = "HOST" } } };
        var game = new Driver(state); await Fixture.Battle(game, true);
        if (cardNo == "SFD·016/221") await Fixture.Choose(game, "ENEMY1");
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", skill.ControllerId); Assert.Equal("HOST", skill.SourceObjectId);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("GEAR", skill.UnitEvent!.Origin!.Equipment.ObjectId);
        await game.Top(); Assert.Equal(2, game.State.CardObjects["ENEMY1"].Damage);
        await game.Replay();
    }
}
