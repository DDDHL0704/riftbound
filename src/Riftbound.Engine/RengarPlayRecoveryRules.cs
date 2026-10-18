using System.Text.RegularExpressions;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal static class RengarPlayRecoveryRules
{
    // Independent of the trigger producer and legend-identity registry.
    private static readonly Lazy<IReadOnlySet<string>> PrintedSources = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(c => Regex.Replace(c.CardEffect, @"\s+", "") == "当你打出一名单位时，让一名单位本回合内{{S}}+1。")
            .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));
    private static readonly Lazy<IReadOnlySet<string>> PrintedUnits = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(c => c.CardCategoryNameList.Any(n => n.Contains("单位", StringComparison.Ordinal)))
            .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));

    internal static bool Valid(MatchState state, UnitPlayMightContext? context, string effect,
        string source, string controller, string? cardNo = null)
    {
        if (context is null) return effect != CoreRuleEngine.UnitPlayMightEffect;
        if (!(effect == CoreRuleEngine.UnitPlayMightEffect && PrintedSources.Value.Contains(context.CardNo)
            && (cardNo is null || cardNo == context.CardNo) && state.Seats.ContainsKey(controller)
            && context.SourceGeneration >= 0 && context.CapturedTick > 0 && context.CapturedTick <= state.Tick
            && state.CardObjects.TryGetValue(source, out var legend) && legend.OwnerId == controller
            && legend.ObjectGeneration >= context.SourceGeneration
            && (legend.ObjectGeneration != context.SourceGeneration || legend.CardNo == context.CardNo)
            && context.PlayedUnit is { Generation: >= 0 } unit && !string.IsNullOrWhiteSpace(unit.ObjectId) && unit.ObjectId != source
            && !string.IsNullOrWhiteSpace(context.PlayedCardNo)
            && (context.RequestedTarget is null || context.RequestedTarget is { Generation: >= 0 } requested
                && !string.IsNullOrWhiteSpace(requested.ObjectId)))) return false;
        var binding = context.PlayedUnit;
        if (state.CardObjects.TryGetValue(binding.ObjectId, out var played) && played.ObjectGeneration < binding.Generation) return false;
        if (context.PlayedUnitTokenFactoryCardNo is { } factory)
            return P6TokenFactoryCatalog.IsUnitTokenFactory(factory)
                && (context.PlayedCardNo == factory || PrintedUnits.Value.Contains(context.PlayedCardNo))
                && (played is null || played.ObjectGeneration != binding.Generation
                    || TokenObjectRules.IsToken(played) && (played.TokenFactoryCardNo ?? played.CardNo) == factory
                        && played.CardNo == context.PlayedCardNo);
        return state.CardPlays.Resolved.Any(p => p.Source == binding && p.CardNo == context.PlayedCardNo
            && p.PlayerId == controller && p.Tick == context.CapturedTick && (p.Types & 1) != 0);
    }

    internal static bool ValidStack(MatchState state, StackItemState item)
        => Valid(state, item.UnitPlayMight, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            && (item.UnitPlayMight is null || !item.SourceConfirmed && !item.PermanentPlayTargetsPending
                && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && item.OptionalCosts.Count == 0
                && (item.TargetGenerations is null ? item.TargetObjectIds.Count == 0 && item.UnitPlayMight.TargetTokenFactoryCardNo is null && state.PriorityPlayerId is null
                        && (state.PendingCardChoice is not null || state.PendingPayment is not null || state.TriggerQueue.Count > 0)
                    : item.TargetObjectIds.Count == 1 && item.TargetGenerations.Count == 1
                        && item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0
                        && ValidTargetWitness(state, item)));

    internal static bool ValidQueued(MatchState state, TriggerQueueItemState trigger)
        => Valid(state, trigger.UnitPlayMight, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo)
            && (trigger.UnitPlayMight is null || trigger.SourceCardNo == trigger.UnitPlayMight.CardNo
                && trigger.EnqueuedAtTick == trigger.UnitPlayMight.CapturedTick
                && trigger.TriggeredByEventKind is "UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED");

    private static bool ValidTargetWitness(MatchState state, StackItemState item)
    {
        var factory = item.UnitPlayMight!.TargetTokenFactoryCardNo;
        if (factory is not null && !P6TokenFactoryCatalog.IsUnitTokenFactory(factory)) return false;
        if (!state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target)) return factory is not null;
        return target.ObjectGeneration != item.TargetGenerations![target.ObjectId]
            || factory == (TokenObjectRules.IsToken(target) ? target.TokenFactoryCardNo ?? target.CardNo : null);
    }
}
