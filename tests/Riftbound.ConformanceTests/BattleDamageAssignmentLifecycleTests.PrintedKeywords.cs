using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    private static MatchState PrintedKeywordBattlePosition(string sourceId, string cardNo)
    {
        var initial = BuildNaturalStartBattleState() with { Tick = 0,
            Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            UntilEndOfTurnEffects = [],
            RunePools = new Dictionary<string, RunePool> {
                ["P1"] = new(20, 20, new Dictionary<string, int> { ["green"] = 2, ["yellow"] = 2, ["blue"] = 2 }),
                ["P2"] = RunePool.Empty } };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [BattlefieldObjectId] = initial.CardObjects[BattlefieldObjectId] with { ControllerId = "P2" },
            [sourceId] = Printed(sourceId, cardNo, "P1"),
            ["DISCARD"] = Printed("DISCARD", "OGN·219/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Battlefields = [BattlefieldObjectId],
                    Base = sourceId == AttackerObjectId ? [] : [AttackerObjectId],
                    Hand = [sourceId, "DISCARD"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                [AttackerObjectId] = new("P1", sourceId == AttackerObjectId ? "HAND" : "BASE"),
                [sourceId] = new("P1", "HAND"), ["DISCARD"] = new("P1", "HAND") } };
        return AttackDamageDistributionTests.Spell(initial, "READY", "UNL-009/219", "P1");
    }

    private static async Task BeginPrintedKeywordBattle(Driver game, string sourceId)
    {
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", [sourceId]));
        await game.Top();
        var attackers = sourceId == AttackerObjectId ? new[] { AttackerObjectId } : [AttackerObjectId, sourceId];
        await game.Act("P1", new MoveUnitCommand(AttackerObjectId, "BASE", "BATTLEFIELD:" + BattlefieldObjectId, [], attackers));
        for (var i = 0; i < 4 && game.State.FocusPlayerId is { } focus; i++)
            await game.Act(focus, new PassFocusCommand());
        if (ResolutionResult.BuildPrompts(game.State)["P1"].EnabledActions().Contains(CommandTypes.DeclareBattle))
            await game.Act("P1", new DeclareBattleCommand(BattlefieldObjectId, attackers,
                [BulwarkDefenderObjectId, BackRowDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
    }

    [Theory]
    [InlineData("ARC-002/006")]
    [InlineData("SFD·173/221")]
    [InlineData("SFD·239/221")]
    [InlineData("SFD·239*/221")]
    public async Task PrintedBackRowPreventsBypassingTheOrdinaryDefender(string cardNo)
    {
        var game = new Driver(PrintedKeywordBattlePosition(SecondAttackerObjectId, cardNo));
        await game.Act("P1", new PlayCardCommand(SecondAttackerObjectId, cardNo, [], Destination: "BASE"));
        Assert.Contains(CardCombatKeywordNames.BackRow, game.State.CardObjects[SecondAttackerObjectId].Tags);
        var power = game.State.CardObjects[SecondAttackerObjectId].Power;
        await BeginPrintedKeywordBattle(game, SecondAttackerObjectId);
        await game.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 3),
                new(SecondAttackerObjectId, BackRowDefenderObjectId, power)]));
        var before = game.State;
        var bypass = new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, SecondAttackerObjectId, 2), new(BackRowDefenderObjectId, SecondAttackerObjectId, 1)]);
        var intent = new PlayerIntent("printed-back-row-bypass", "P2", CommandTypes.AssignCombatDamage);
        var rejected = await new CoreRuleEngine().ResolveAsync(before, intent, bypass, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
        // The identical allocation is valid without Back Row. This rules out
        // unrelated legality failures making the rejection assertion pass.
        var withoutKeyword = before with { CardObjects = new Dictionary<string, CardObjectState>(before.CardObjects) {
            [SecondAttackerObjectId] = before.CardObjects[SecondAttackerObjectId] with {
                Tags = before.CardObjects[SecondAttackerObjectId].Tags.Where(tag => tag != CardCombatKeywordNames.BackRow).ToArray() } } };
        var control = await new CoreRuleEngine().ResolveAsync(withoutKeyword, intent, bypass, default);
        Assert.True(control.Accepted, control.ErrorMessage);
        await game.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BackRowDefenderObjectId, AttackerObjectId, 1)]));
        var assigned = Assert.Single(game.Events(), entry => entry.Event.Kind == "COMBAT_DAMAGE_ASSIGNED").Event;
        var assignments = Assert.IsAssignableFrom<IReadOnlyList<CombatDamageAssignmentDto>>(assigned.Payload["assignments"]);
        Assert.DoesNotContain(assignments, assignment => assignment.TargetObjectId == SecondAttackerObjectId);
        Assert.Contains(SecondAttackerObjectId, game.State.PlayerZones["P1"].Battlefields);
        await game.Replay();
    }

    [Fact]
    public async Task PrintedChempunkToughHasFourAttackDamageFromItsTwoBaseAndAssaultTwo()
    {
        var game = new Driver(PrintedKeywordBattlePosition(AttackerObjectId, "OGN·003/298"));
        await game.Act("P1", new PlayCardCommand(AttackerObjectId, "OGN·003/298", ["DISCARD"],
            Destination: "BASE"));
        await game.Top();
        await BeginPrintedKeywordBattle(game, AttackerObjectId);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!,
            c => c.Action == CommandTypes.AssignCombatDamage);
        var pool = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(candidate.Metadata!["damagePool"]);
        Assert.Equal(4, pool[AttackerObjectId]);
        await game.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 2)]));
        await game.Replay();
    }
}
