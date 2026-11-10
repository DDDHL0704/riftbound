using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentOverkillDrawTests
{
    private const string Gear = "UNL-188/219";
    private static MatchState Position(int enemyMight = 1, int copies = 1, bool attached = true)
    {
        var state = LocalDestructionRecallTests.At(RadGearTestPosition.Create("OGN·275/298"), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = enemyMight },
            ["ALLY"] = state.CardObjects["ALLY"] with { Power = attached ? 1 : 4 } } };
        for (var i = 0; i < copies; i++) {
            var id = "GEAR" + i; state = RadGearTestPosition.Put(state, id, Gear, "P1", "BASE");
            if (attached) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { AttachedToObjectId = "ALLY" } } };
        }
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }
    private static async Task Battle(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        for (var step = 0; step < 4 && game.State.FocusPlayerId is { } player; step++)
            await game.Act(player, new PassFocusCommand());
        var battle = game.State.BattleState;
        Assert.True(battle.IsActive);
        await game.Act("P1", new DeclareBattleCommand("OTHER", battle.AttackerObjectIds,
            battle.DefenderObjectIds, ["COMBAT_ASSIGNMENT"]));
        for (var step = 0; step < 20 && game.State.BattleState.IsActive
            && game.State.PendingBattleResult is null; step++)
        {
            var prompt = game.State.Seats.Keys.Select(game.PromptFor).FirstOrDefault(prompt => prompt.Actionable
                && (prompt.Actions.Contains(CommandTypes.AssignCombatDamage) || prompt.Actions.Contains(CommandTypes.PassPriority)));
            if (prompt is null) break;
            battle = game.State.BattleState;
            GameCommand command = prompt.Actions.Contains(CommandTypes.AssignCombatDamage)
                ? new AssignCombatDamageCommand(battle.BattleId!, battle.BattlefieldObjectId!, CombatTestDriver.Assignments(prompt))
                : new PassPriorityCommand();
            await game.Act(prompt.PlayerId, command);
        }
        if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(
            OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }
    [Theory]
    [InlineData(1, true)] [InlineData(2, false)]
    public async Task NaturalConquestCapturesTheActualAssignedOverkillAtTheThreePointBoundary(int enemyMight, bool earnsDraw)
    {
        var game = new Driver(Position(enemyMight)); var top = game.State.PlayerZones["P1"].MainDeck[0];
        await Battle(game); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(earnsDraw ? 1 : 0, game.State.StackItems.Count); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        if (earnsDraw) {
            var skill = Assert.Single(game.State.StackItems); Assert.Equal(Gear, skill.CardNo);
            Assert.Equal(3, skill.UnitEvent!.AssignedOverkill); Assert.True(skill.UnitEvent.Origin!.ArmamentGranted);
            await game.Top(); Assert.Equal([top], game.State.PlayerZones["P1"].Hand);
        }
        await game.Replay();
    }
    [Fact]
    public async Task AnUnattachedGuardDoesNotGrantTheLowerBoxToAConqueror()
    {
        var game = new Driver(Position(attached: false)); await Battle(game);
        Assert.DoesNotContain(game.State.StackItems, s => s.CardNo == Gear); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }
    [Fact]
    public async Task TwoGuardsEarnTwoSeparateDrawsFromOneActualConquest()
    {
        var game = new Driver(Position(copies: 2)); var top = game.State.PlayerZones["P1"].MainDeck.Take(2).ToArray();
        await Battle(game); Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal(2, game.State.StackItems.Select(s => s.UnitEvent!.Origin!.Equipment.ObjectId).Distinct().Count());
        await game.Top(); await game.Top(); Assert.Equal(top, game.State.PlayerZones["P1"].Hand); await game.Replay();
    }
    [Fact]
    public async Task ReturningTheHostAfterConquestCannotEraseItsEarnedDraw()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        var top = game.State.PlayerZones["P1"].MainDeck[0]; await Battle(game);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Null(game.State.CardObjects["GEAR0"].AttachedToObjectId); await game.Top();
        Assert.Contains(top, game.State.PlayerZones["P1"].Hand); await game.Replay();
    }
    [Theory]
    [InlineData("amount")] [InlineData("erase")] [InlineData("duplicate")]
    public async Task RecoveryRejectsRewritingOrErasingTheActualConquestCondition(string mutation)
    {
        var game = new Driver(Position()); await Battle(game); var item = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = mutation switch {
            "erase" => [], "duplicate" => [item, item],
            _ => [item with { UnitEvent = item.UnitEvent! with { AssignedOverkill = 4 } }] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); await game.Replay();
    }
}
