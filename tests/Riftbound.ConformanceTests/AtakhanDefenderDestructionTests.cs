using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AtakhanDefenderDestructionTests
{
    private const string Atakhan = "UNL-170/219";
    private static MatchState Position()
        => RadGearTestPosition.Put(AttackDamageDistributionTests.Position(Atakhan), "OUTSIDE", "OGN·219/298", "P2", "BASE");
    private static async Task Attack(Driver game)
        => await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));

    [Fact]
    public async Task AnActualAttackEarnsAnUntargetedSkillAndOnlyTheDefenderChoosesAUnitHereAtResolution()
    {
        var game = new Driver(Position()); await Attack(game);
        var skill = Assert.Single(game.State.StackItems); Assert.Empty(skill.TargetObjectIds);
        Assert.Equal("P2", skill.UnitEvent!.RequiredUnitDestruction!.DefendingPlayerId);
        Assert.Equal("F", skill.UnitEvent.RequiredUnitDestruction.Battlefield.ObjectId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"); await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P2", pending.Request.PlayerId);
        Assert.Equal(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, pending.Request.Options.Select(option => option.Id));
        await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["ENEMY1"]));
        foreach (var id in new[] { "HOST", "OUTSIDE" })
            await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [id]));
        await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY2");
        Assert.Contains("ENEMY2", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("OUTSIDE", game.State.PlayerZones["P2"].Base);
        var death = Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED");
        Assert.Equal("P2", death.Event.Payload["destroyedByPlayerId"]); await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task DeclaringCombatAfterTheEarnedSkillDoesNotCaptureTheAttackAgain(bool token)
    {
        var state = Position();
        if (token)
        {
            Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.ZaunMinionTokenCardNo, out var factory));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY2"] = factory.CreateObject("ENEMY2", "P2", "P2") } };
        }
        var game = new Driver(state); await Attack(game); await game.Top();
        await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY2");
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        var before = game.Events().Length;
        await game.Act("P1", new DeclareBattleCommand("F", ["HOST"], ["ENEMY1", "ENEMY3"], ["COMBAT_ASSIGNMENT"]));
        Assert.DoesNotContain(game.Events().Skip(before), entry => entry.Event.Kind == "TRIGGER_QUEUED");
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheAttackerDoesNotChangeTheCapturedDefenderOrBattlefield()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await Attack(game); await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand); await game.Top();
        await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY1");
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task ARealDefenderReturnResponseRemovesOnlyThatUnitFromTheLaterChoice()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P2", "HAND"));
        await Attack(game); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY1"])); await game.Top(); await game.Top();
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, option => option.Id == "ENEMY1");
        await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY3");
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Hand); Assert.Contains("ENEMY3", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task AProtectedDefenderCanBePickedWithoutBecomingAnEnemySkillTarget()
    {
        var state = RadGearTestPosition.Put(Position(), "ENEMY1", "UNL-147/219", "P2", "BATTLEFIELD", "F");
        var game = new Driver(state); await Attack(game); await game.Top();
        await HousecleaningChoiceTests.Choose(game, "P2", "ENEMY1");
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task BecomingADefenderDoesNotEarnTheAttackInstruction()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "ALLY", Atakhan, "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["ALLY"].IsDefending);
        Assert.DoesNotContain(game.State.StackItems, item => item.CardNo == Atakhan);
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.SourceCardNo == Atakhan); await game.Replay();
    }

    [Theory, InlineData("field"), InlineData("defender"), InlineData("origin"), InlineData("erase")]
    public async Task RecoveryRejectsChangingOrErasingTheOriginallyEarnedAttackInstruction(string mode)
    {
        var game = new Driver(Position()); await Attack(game); var item = Assert.Single(game.State.StackItems);
        var origin = item.UnitEvent!.RequiredUnitDestruction!;
        var forged = game.State with { StackItems = mode == "erase" ? [] : [item with { UnitEvent = item.UnitEvent with {
            RequiredUnitDestruction = mode == "origin" ? null : mode == "defender" ? origin with { DefendingPlayerId = "P1" }
                : origin with { Battlefield = new("OTHER", 0) } } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
    }
}
