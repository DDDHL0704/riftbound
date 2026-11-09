using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasDeathObserverExhaustCost(StackItemState item)
        => item.DeathObserver is { } context && UnitDestroyedTriggerSpecRules.TryGetTrigger(context.CardNo,
            spec => spec.Kind == context.Kind && spec.ExhaustsSource == true && spec.Optional == true, out _);

    private static bool MatchingDeathObserverRelation(DeathObserverContext context, string controller, string source)
    {
        if (IsEnemySourceDeathKind(context.Kind))
            return ValidEnemySourceDeathRelation(context, controller);
        if (context.Kind == TriggerKinds.BattlefieldFirstNonTokenDestroyedCreateMinion)
            return ValidLocalFriendlyDeath(context, controller, source);
        if (context.Kind is TriggerKinds.UnitEnemyDestroyedHereOncePerTurnCallRune or TriggerKinds.UnitStunnedEnemyDestroyedHereDraw)
            return MatchingLocalDeath(context, controller)
                && (context.Kind != TriggerKinds.UnitStunnedEnemyDestroyedHereDraw || context.Destroyed.WasStunned);
        return context.SourceLocation is null && context.DestroyedLocation is null
            && (context.Kind == TriggerKinds.PlayerDestroysStunnedEnemyDraw
                ? !string.IsNullOrWhiteSpace(context.Destroyed.ControllerId) && context.Destroyed.ControllerId != controller
                : context.Destroyed.ControllerId == controller);
    }

    private static bool ValidAdditionalDeathObserverConditions(DeathObserverContext context, string controller)
    {
        if (context.Kind == TriggerKinds.PlayerDestroysStunnedEnemyDraw)
            return context.Destroyed.WasStunned && context.DestroyedHadBoon is null
                && context.Attribution is { Source.Generation: >= 0 } attribution
                && attribution.ResponsiblePlayerId == controller
                && UnitDestroyedTriggerSpecRules.TryGetTrigger(context.CardNo,
                    UnitDestroyedTriggerSpecRules.IsControllerStunnedEnemyDestroyedDrawTrigger, out _);
        if (context.Kind == TriggerKinds.FriendlyBoonedUnitDestroyedBoonOther)
            return context.DestroyedHadBoon == true && context.Attribution is null
                && UnitDestroyedTriggerSpecRules.TryGetTrigger(context.CardNo,
                    UnitDestroyedTriggerSpecRules.IsFriendlyBoonedUnitDestroyedBoonTrigger, out _);
        return context.DestroyedHadBoon is null && context.Attribution is null;
    }
}
