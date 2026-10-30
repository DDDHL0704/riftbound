using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static IEnumerable<SourceStaticLimitsSpec> StaticLimitsFor(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source).Select(instance =>
            CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior) ? behavior.SourceStaticLimits : null)
            .OfType<SourceStaticLimitsSpec>();

    internal static bool PrintedPlayTurnAllowed(MatchState state, string player, CardBehaviorDefinition behavior)
        => behavior.SourceStaticLimits is not { UnplayableThroughControllerTurn: > 0 } limit
            || PlayerTurnOrdinal(state, player) > limit.UnplayableThroughControllerTurn;

    private static bool BulwarkIgnoredForTarget(MatchState state, string targetId)
    {
        if (!state.ObjectLocations.TryGetValue(targetId, out var at) || at.Zone != "BATTLEFIELD"
            || at.BattlefieldObjectId is null || !state.CardObjects.TryGetValue(targetId, out var target)) return false;
        var controller = EffectiveFieldControllerId(state.PlayerZones, targetId, target);
        return state.CardObjects.Values.Any(source => IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) is { } opponent && opponent != controller
            && state.ObjectLocations.TryGetValue(source.ObjectId, out var location)
            && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == at.BattlefieldObjectId
            && StaticLimitsFor(state, source).Any(spec => spec.CombatDamageIgnoresBulwarkHere));
    }

    private static int StageRuneCallLimit(MatchState state)
        => state.CardObjects.Values.Where(source => IsFaceUpNonStandbyUnit(source)
                && IsObjectOnField(state.PlayerZones, source.ObjectId)
                && state.ObjectLocations.TryGetValue(source.ObjectId, out var location) && location.Zone == "BATTLEFIELD")
            .SelectMany(source => StaticLimitsFor(state, source)).Select(spec => spec.BattlefieldStageRuneCallLimit)
            .Where(limit => limit > 0).DefaultIfEmpty(int.MaxValue).Min();

    private static bool EarlyBattlefieldScoreBecomesDraw(MatchState state, string player)
        => state.TurnPlayerId == player && PlayerTurnOrdinal(state, player) > 0 && state.CardObjects.Values.Any(source => IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && StaticLimitsFor(state, source).Any(spec => spec.BattlefieldScoreDrawThroughPlayerTurn > 0
                && PlayerTurnOrdinal(state, player) <= spec.BattlefieldScoreDrawThroughPlayerTurn));

    private static bool PreventedByRuneDamageImmunity(Dictionary<string, CardObjectState> cards,
        string controller, CardObjectState target, bool fromUnit)
    {
        if (fromUnit || RuleChoices.Value is not { ResolutionItem: not null, ResolutionState: { } state }
            || !IsFaceUpNonStandbyUnit(target) || !IsObjectOnField(state.PlayerZones, target.ObjectId)
            || EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target) is not { } owner || owner == controller) return false;
        var current = state with { CardObjects = cards };
        return StaticLimitsFor(current, target).Any(spec => spec.EnemySpellSkillDamageImmunityMinimumRunes > 0
            && EmpowerRules.ControlledRunes(current, owner) >= spec.EnemySpellSkillDamageImmunityMinimumRunes);
    }
}
