using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PreservesMovementCapture { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string MoveEnemyToArrivalBattlefieldEffect = "MOVE_ENEMY_TO_ARRIVAL_BATTLEFIELD";
    internal const string MoveWeakerEnemyHereToAnotherBattlefieldEffect = "MOVE_WEAKER_ENEMY_HERE_TO_ANOTHER_BATTLEFIELD";

    private static bool IsDeclaredUnitEventMovementEffect(UnitEventContext context)
        => SelectedUnitEventSpec(context)?.Effect is "MOVE_ENEMY" or MoveWeakerEnemyHereToAnotherBattlefieldEffect;

    internal static bool RequiresUnitEventMovementDeclarationHistory(StackItemState item)
        => item.UnitEvent is { } context && SelectedUnitEventSpec(context)?.PreservesMovementCapture == true
            && IsDeclaredUnitEventMovementEffect(context) && item.TargetGenerations is not null && item.Destination.Length > 0;

    internal static string DeclaredUnitEventMovementHash(StackItemState item)
        => MatchStateHasher.HashValue(new { item.StackItemId, item.Destination, item.TargetObjectIds, item.TargetGenerations });

    private static IEnumerable<RuleChoiceOption> UnitEventMovementDestinations(MatchState state,
        StackItemState item, string target)
    {
        var here = CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration);
        var otherBattlefield = SelectedUnitEventSpec(item.UnitEvent)?.Effect == MoveWeakerEnemyHereToAnotherBattlefieldEffect;
        return ChosenMovementDestinations(state, item.ControllerId)
            .Where(choice => IsUnitMovementDestination(state, target, choice.Id)
                && (!otherBattlefield || choice.Id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
                    && choice.Id[12..] != here?.BattlefieldObjectId))
            .Select(choice => new RuleChoiceOption(choice.Id, choice.Label));
    }

    private static bool IsWeakerEnemyHereMovementTarget(MatchState state, StackItemState item, CardObjectState target)
        => IsFaceUpNonStandbyUnit(target)
            && EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target) != item.ControllerId
            && UnitEventTargetIsHere(state, item, target.ObjectId)
            && CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration) is not null
            && ResolveCurrentFieldUnitPower(state, target) < ResolveCurrentFieldUnitPower(state, state.CardObjects[item.SourceObjectId])
            && UnitEventMovementDestinations(state, item, target.ObjectId).Any();
}
