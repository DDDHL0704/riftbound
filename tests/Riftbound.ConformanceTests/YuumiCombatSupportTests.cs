using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class YuumiCombatSupportTests
{
    private const string Yuumi = "UNL-056/219";
    private const string Bulwark = "COMBAT_KEYWORD:壁垒:1";

    private static MatchState Position(bool defending = false, bool companion = true)
    {
        var state = RadGearTestPosition.Create();
        state = RadGearTestPosition.Put(state, "OTHER", "OGN·296/298", "P1", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "BF", "OGN·296/298", defending ? "P1" : "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "HOST", Yuumi, "P1", defending ? "BATTLEFIELD" : "BASE", defending ? "BF" : null);
        state = RadGearTestPosition.Put(state, "ALLY", "OGN·219/298", "P1", companion && defending ? "BATTLEFIELD" : "BASE", companion && defending ? "BF" : null);
        state = RadGearTestPosition.Put(state, "ENEMY", "OGN·001/298", "P2", defending ? "BASE" : "BATTLEFIELD", defending ? null : "BF");
        state = RadGearTestPosition.Put(state, "AWAY", "OGN·219/298", "P1", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·022/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return state with { TurnPlayerId = defending ? "P2" : "P1", ActivePlayerId = defending ? "P2" : "P1" };
    }

    private static Task Challenge(Driver game, bool defending = false, bool companion = true)
        => game.Act(defending ? "P2" : "P1", new MoveUnitCommand(defending ? "ENEMY" : "HOST", "BASE", "BATTLEFIELD:BF", [],
            defending ? ["ENEMY"] : companion ? ["HOST", "ALLY"] : ["HOST"]));

    private static async Task Confirm(Driver game)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("P1", choice.PlayerId);
        Assert.Equal(new[] { "ALLY" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task BothBattleRolesConfirmAnotherFriendlyHereThenGrantMightAndBulwarkAfterResponses(bool defending)
    {
        var game = new Driver(Position(defending));
        await Challenge(game, defending);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await Confirm(game);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(Bulwark, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("ATTACK_OR_DEFEND", skill.UnitEvent!.Timing);
        Assert.Null(skill.TriggerCost);
        Assert.Equal(new[] { "ALLY" }, skill.TargetObjectIds);
        await game.Top();
        Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        Assert.Contains(Bulwark, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(1, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task SelectionRejectsSelfEnemiesEquipmentOtherLocationsAndTheOtherPlayerAtomically()
    {
        var game = new Driver(Position()); await Challenge(game);
        var choice = game.State.PendingCardChoice!;
        foreach (var target in new[] { "HOST", "ENEMY", "GEAR", "AWAY", "UNKNOWN" })
            await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY", "ALLY"]));
        await Confirm(game); await game.Top(); await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task NoOtherFriendlyHereMeansNoTargetRatherThanBuffingYuumiOrAUnitElsewhere(bool defending)
    {
        var game = new Driver(Position(defending, false)); await Challenge(game, defending, false);
        Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent is not null);
        Assert.Equal(1, game.State.CardObjects["HOST"].Power);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory, InlineData("HOST"), InlineData("ALLY")]
    public async Task ARealResponseReturningTheSourceOrTargetMakesHereIllegalAndDoesNotModifyTheHandIncarnation(string returned)
    {
        var game = new Driver(Position()); await Challenge(game); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations[returned].Zone);
        var handHash = MatchStateHasher.HashValue(game.State.CardObjects[returned]);
        await game.Top();
        Assert.Equal(handHash, MatchStateHasher.HashValue(game.State.CardObjects[returned]));
        Assert.DoesNotContain(Bulwark, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task AUnitOwnedByTheOpponentIsFriendlyWhenControlledByTheSkillController()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { OwnerId = "P2" } } };
        var game = new Driver(initial); await Challenge(game); await Confirm(game); await game.Top();
        Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        Assert.Contains(Bulwark, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }

    [Fact]
    public async Task AnActualSongAssemblyCopiesTheTriggerAndBothApplicationsUseTheSharedGrant()
    {
        var initial = RadGearTestPosition.Put(Position(), "GEAR", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        await Challenge(game);
        var triggers = game.State.TriggerQueue;
        Assert.Equal(2, triggers.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: triggers.Select(t => t.TriggerId).ToArray()));
        await Confirm(game); await Confirm(game);
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top(); await game.Top();
        Assert.Equal(10, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(2, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(e => e.StartsWith("COMBAT_KEYWORD:壁垒:")));
        await game.Replay();
    }

    private static async Task CloseBattle(Driver game)
    {
        for (var step = 0; !game.Events().Any(e => e.Event.Kind == "BATTLE_CLOSED"); step++)
        {
            Assert.InRange(step, 0, 20);
            var assign = game.State.Seats.Keys.Select(game.PromptFor).FirstOrDefault(p => p.EnabledActions().Contains(CommandTypes.AssignCombatDamage));
            if (assign is not null)
            {
                if (assign.PlayerId == "P2")
                {
                    var metadata = JsonSerializer.SerializeToElement(assign.View!.Metadata);
                    Assert.Contains("HOST", metadata.GetProperty("allLegalTargets").GetProperty("ENEMY").EnumerateArray().Select(v => v.GetString()));
                    Assert.Equal(0, metadata.GetProperty("targetPriority").GetProperty("ALLY").GetInt32());
                    Assert.Equal(1, metadata.GetProperty("targetPriority").GetProperty("HOST").GetInt32());
                    var pool = metadata.GetProperty("assignableDamagePool").GetProperty("ENEMY").GetInt32();
                    await RadGearTestPosition.Reject(game.State, "P2", new AssignCombatDamageCommand(
                        game.State.BattleState.BattleId!, "BF", [new("ENEMY", "HOST", pool)]));
                }
                await game.Act(assign.PlayerId, new AssignCombatDamageCommand(game.State.BattleState.BattleId!, "BF", CombatTestDriver.Assignments(assign)));
            }
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else
            {
                var actor = game.State.TurnPlayerId;
                var attackers = game.State.CardObjects.Values.Where(c => c.IsAttacking).Select(c => c.ObjectId).ToArray();
                var defenders = game.State.CardObjects.Values.Where(c => c.IsDefending).Select(c => c.ObjectId).ToArray();
                await game.Act(actor, new DeclareBattleCommand("BF", attackers, defenders, ["COMBAT_ASSIGNMENT"]));
            }
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task TheGrantedBulwarkControlsRealDamageAssignmentAndBothBonusesLastUntilTurnEnd(bool defending)
    {
        var game = new Driver(Position(defending)); await Challenge(game, defending); await Confirm(game); await game.Top();
        await CloseBattle(game);
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && e.Event.Payload.ContainsKey("combatRole")
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "ENEMY")).ToArray();
        Assert.NotEmpty(damage);
        Assert.All(damage, e => Assert.Equal("ALLY", e.Event.Payload["targetObjectId"]));
        Assert.Equal(7, game.State.CardObjects["ALLY"].Power);
        Assert.Contains(Bulwark, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Act(game.State.TurnPlayerId, new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(Bulwark, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }
}
