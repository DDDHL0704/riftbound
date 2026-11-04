using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadMordekaiserCombatFaqTests
{
    // DOC-17 p9: simultaneous lethal combat damage does not remove the cost unit early.
    [Theory]
    [InlineData("RAD·020/167")]
    [InlineData("RAD·020a/167")]
    public async Task SimultaneousLethalCombatCanSacrificeTheFriendlyMinionToRecallTheHero(string no)
    {
        var initial = DeathAndDuelLifecycleTests.State();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var minion));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["A"] = OfficialGarenConquestBattlefieldCountTests.Printed("A", "UNL-035/219", "P1"),
            ["D"] = OfficialGarenConquestBattlefieldCountTests.Printed("D", no, "P2"),
            ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "OGN·278/298", "P2") with { ControllerId = "P2" },
            ["MINION"] = minion.CreateObject("MINION", "P2", "P2") } };
        initial = RadUnitEffectTestFixture.Place(initial, "MINION", "P2", "BATTLEFIELD", "BF");
        Assert.Equal(6, initial.CardObjects["A"].Power);
        Assert.Equal(5, initial.CardObjects["D"].Power);
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        await game.Act("P1", new DeclareBattleCommand("BF", ["A"], ["D", "MINION"], ["COMBAT_ASSIGNMENT"]));
        for (var step = 0; game.State.PendingRuleChoice is null && game.State.BattleState.IsActive; step++)
        {
            Assert.InRange(step, 0, 11);
            var prompt = Assert.Single(ResolutionResult.BuildPrompts(game.State).Values,
                p => p.Actionable && (p.Actions.Contains(CommandTypes.AssignCombatDamage) || p.Actions.Contains(CommandTypes.PassPriority)));
            GameCommand command = prompt.Actions.Contains(CommandTypes.AssignCombatDamage)
                ? new AssignCombatDamageCommand(game.State.BattleState.BattleId!, "BF", CombatTestDriver.Assignments(prompt))
                : new PassPriorityCommand();
            await game.Act(prompt.PlayerId, command);
        }
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("P2", request.PlayerId);
        const string option = "SACRIFICE_RECALL:D:MINION";
        Assert.Contains(request.Options, entry => entry.Id == option);
        await game.ColdCheckpoint();
        var pay = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("wrong-faq-combat-player", "P1", pay.CmdType), pay, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await game.Act("P2", pay);
        for (var step = 0; game.State.PendingBattleResult is not null; step++)
        {
            Assert.InRange(step, 0, 7);
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("D", game.State.PlayerZones["P2"].Base);
        Assert.True(game.State.CardObjects["D"].IsExhausted);
        Assert.Equal(0, game.State.CardObjects["D"].Damage);
        Assert.Equal(0, game.State.CardObjects["D"].ObjectGeneration);
        Assert.DoesNotContain("MINION", game.State.CardObjects.Keys);
        var history = Assert.Single(game.State.BattleResolutions);
        Assert.Contains(history.PublicObjectBindings!, b => b.ObjectId == "MINION" && b.Generation == 0);
        var forgedHistory = history with { PublicObjectBindings = history.PublicObjectBindings!
            .Append(new ObjectBinding("FORGED-MINION", 0)).ToArray() };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { BattleResolutions = [forgedHistory] }),
            error => error.Contains("captured public battle history reference"));
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "D")
            && Convert.ToInt32(e.Event.Payload["damage"]) == 5);
        Assert.Contains(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "MINION")
            && Convert.ToInt32(e.Event.Payload["damage"]) == 1);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "D"));
        await game.ColdCheckpoint(); await game.Replay();
    }
}
