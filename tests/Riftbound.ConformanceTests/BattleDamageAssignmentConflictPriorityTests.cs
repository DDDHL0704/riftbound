using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AttackerMayAssignTheMixedKeywordTargetFirstOrLast(bool mixedFirst)
    {
        var initial = MixedDefenderPosition();
        var engine = new CoreRuleEngine();
        var opened = await DeclareAssignmentBattleAsync(initial, engine);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        CombatDamageAssignmentDto[] assignments = mixedFirst
            ? [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 3)]
            : [new(AttackerObjectId, BackRowDefenderObjectId, 4), new(AttackerObjectId, BulwarkDefenderObjectId, 1)];
        var command = new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, assignments);
        var result = await engine.ResolveAsync(opened.State,
            new PlayerIntent($"conflicting-attacker-{mixedFirst}", "P1", CommandTypes.AssignCombatDamage), command, CancellationToken.None);
        WriteConflictingAssignmentDiagnostic($"attacker-{mixedFirst}", initial, opened.State, command, result);
        Assert.True(result.Accepted, result.ErrorMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DefenderMayAssignTheMixedKeywordTargetFirstOrLast(bool mixedFirst)
    {
        var initial = MixedAttackerPosition();
        var engine = new CoreRuleEngine();
        var opened = await engine.ResolveAsync(initial,
            new PlayerIntent("conflicting-defender-declare", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId, SecondAttackerObjectId],
                [BulwarkDefenderObjectId, BackRowDefenderObjectId], ["COMBAT_ASSIGNMENT"]), CancellationToken.None);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        var first = await engine.ResolveAsync(opened.State,
            new PlayerIntent("conflicting-defender-attacker-step", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
                [new(AttackerObjectId, BulwarkDefenderObjectId, 2),
                 new(SecondAttackerObjectId, BulwarkDefenderObjectId, 2),
                 new(SecondAttackerObjectId, BackRowDefenderObjectId, 2)]), CancellationToken.None);
        Assert.True(first.Accepted, first.ErrorMessage);
        CombatDamageAssignmentDto[] assignments = mixedFirst
            ? [new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BulwarkDefenderObjectId, SecondAttackerObjectId, 2),
               new(BackRowDefenderObjectId, SecondAttackerObjectId, 1)]
            : [new(BulwarkDefenderObjectId, SecondAttackerObjectId, 4), new(BackRowDefenderObjectId, AttackerObjectId, 1)];
        var command = new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, assignments);
        var result = await engine.ResolveAsync(first.State,
            new PlayerIntent($"conflicting-defender-{mixedFirst}", "P2", CommandTypes.AssignCombatDamage), command, CancellationToken.None);
        WriteConflictingAssignmentDiagnostic($"defender-{mixedFirst}", initial, first.State, command, result);
        Assert.True(result.Accepted, result.ErrorMessage);
    }

    [Fact]
    public async Task TwoMixedTargetsMaySatisfyDifferentRequirements()
    {
        const string secondMixed = "P2-SECOND-MIXED";
        var defenders = new[] { BulwarkDefenderObjectId, BackRowDefenderObjectId, secondMixed };
        var initial = WithRecoverySeats(BuildNaturalStartBattleState(defenderObjectIds: defenders));
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects, StringComparer.Ordinal)
        {
            [AttackerObjectId] = initial.CardObjects[AttackerObjectId] with { Power = 3 },
            [BulwarkDefenderObjectId] = MixedKeywords(initial.CardObjects[BulwarkDefenderObjectId], 2),
            [BackRowDefenderObjectId] = initial.CardObjects[BackRowDefenderObjectId] with { Power = 3, Tags = [CardObjectTags.UnitCard] },
            [secondMixed] = MixedKeywords(Unit(secondMixed, "P2", power: 1), 1)
        };
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations, StringComparer.Ordinal)
        {
            [secondMixed] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        initial = initial with { CardObjects = cards, ObjectLocations = locations };
        var engine = new CoreRuleEngine();
        var opened = await DeclareAssignmentBattleAsync(initial, engine, defenders);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        var command = new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, secondMixed, 1), new(AttackerObjectId, BackRowDefenderObjectId, 2)]);
        var result = await engine.ResolveAsync(opened.State,
            new PlayerIntent("conflicting-independent-targets", "P1", CommandTypes.AssignCombatDamage), command, CancellationToken.None);
        WriteConflictingAssignmentDiagnostic("independent-targets", initial, opened.State, command, result);
        Assert.True(result.Accepted, result.ErrorMessage);
    }

    [Fact]
    public async Task MixedTargetCannotReceiveDamageWhenNeitherFirstNorLastRequirementCanBeMet()
    {
        const string otherNormal = "P2-OTHER-NORMAL";
        var defenders = new[] { BulwarkDefenderObjectId, BackRowDefenderObjectId, otherNormal };
        var initial = WithRecoverySeats(BuildNaturalStartBattleState(defenderObjectIds: defenders));
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects, StringComparer.Ordinal)
        {
            [AttackerObjectId] = initial.CardObjects[AttackerObjectId] with { Power = 2 },
            [BulwarkDefenderObjectId] = MixedKeywords(initial.CardObjects[BulwarkDefenderObjectId], 2),
            [BackRowDefenderObjectId] = initial.CardObjects[BackRowDefenderObjectId] with { Power = 1, Tags = [CardObjectTags.UnitCard] },
            [otherNormal] = Unit(otherNormal, "P2", power: 1)
        };
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations, StringComparer.Ordinal)
        {
            [otherNormal] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        initial = initial with { CardObjects = cards, ObjectLocations = locations };
        var engine = new CoreRuleEngine();
        var opened = await DeclareAssignmentBattleAsync(initial, engine, defenders);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        var command = new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
            [new(AttackerObjectId, BackRowDefenderObjectId, 1), new(AttackerObjectId, BulwarkDefenderObjectId, 1)]);
        var result = await engine.ResolveAsync(opened.State,
            new PlayerIntent("conflicting-no-valid-order", "P1", CommandTypes.AssignCombatDamage), command, CancellationToken.None);
        WriteConflictingAssignmentDiagnostic("no-valid-order", initial, opened.State, command, result);
        AssertRejectedNoMutation(opened.State, result, ErrorCodes.InvalidPayload);
    }

    private static MatchState MixedDefenderPosition()
    {
        var state = WithRecoverySeats(BuildNaturalStartBattleState());
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [BulwarkDefenderObjectId] = MixedKeywords(state.CardObjects[BulwarkDefenderObjectId], 2),
            [BackRowDefenderObjectId] = state.CardObjects[BackRowDefenderObjectId] with { Power = 4, Tags = [CardObjectTags.UnitCard] }
        };
        return state with { CardObjects = cards };
    }

    private static MatchState MixedAttackerPosition()
    {
        var state = WithRecoverySeats(BuildNaturalStartBattleState(includeSecondAttacker: true));
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [AttackerObjectId] = MixedKeywords(state.CardObjects[AttackerObjectId], 2),
            [SecondAttackerObjectId] = state.CardObjects[SecondAttackerObjectId] with { Power = 4, Tags = [CardObjectTags.UnitCard] },
            [BulwarkDefenderObjectId] = state.CardObjects[BulwarkDefenderObjectId] with { Power = 4 }
        };
        return state with { CardObjects = cards };
    }

    private static CardObjectState MixedKeywords(CardObjectState card, int power)
        => card with { Power = power, Tags = new[] { CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark, CardCombatKeywordNames.BackRow }
            .OrderBy(tag => tag, StringComparer.Ordinal).ToArray() };

    private static MatchState WithRecoverySeats(MatchState state)
        => state with { Seats = new Dictionary<string, string>(StringComparer.Ordinal) { ["P1"] = "P1", ["P2"] = "P2" } };

    private static void WriteConflictingAssignmentDiagnostic(string name, MatchState initial, MatchState before,
        AssignCombatDamageCommand command, ResolutionResult result)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_CONTRADICTORY_PRIORITY_DIAGNOSTICS") is not { } folder) return;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(new {
            initial, before, command, result.Accepted, result.ErrorMessage, result.ErrorCode,
            final = result.State, result.Events, result.Prompts }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
