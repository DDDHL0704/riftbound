using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendDiscardDrawContext(string AbilityId, ObjectBinding Source, string CardNo,
    string ControllerId, string StackItemId, long CapturedTick, long FaceRevision);

public sealed partial class CoreRuleEngine
{
    internal const string LegendDiscardDrawWindow = "LEGEND_DISCARD_DRAW";

    internal static bool IsLegendDiscardDrawEffect(string effect) => effect == LegendAbilityEffectKinds.DiscardDraw;

    private static bool ValidLegendDiscardDrawOrigin(MatchState state, LegendDiscardDrawContext context)
        => context.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && context.CapturedTick >= 0 && context.CapturedTick <= state.Tick && context.FaceRevision >= 0
            && state.Seats.ContainsKey(context.ControllerId)
            && context.StackItemId == $"STACK-{context.CapturedTick}-{context.Source.ObjectId}-LEGEND-DISCARD"
            && LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, context.CardNo)
            && TryGetLegendAbility(context.AbilityId, out var ability) && IsLegendDiscardDrawEffect(ability.EffectKind)
            && PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision, context.CapturedTick, null);

    internal static bool ValidLegendDiscardDraw(MatchState state, StackItemState item)
    {
        if (item.LegendDiscardDraw is not { } context) return !IsLegendDiscardDrawEffect(item.EffectKind);
        return IsLegendDiscardDrawEffect(item.EffectKind) && ValidLegendDiscardDrawOrigin(state, context)
            && context.Source.ObjectId == item.SourceObjectId && context.CardNo == item.CardNo
            && context.ControllerId == item.ControllerId && context.StackItemId == item.StackItemId
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
            && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed && item.Destination.Length == 0
            && item.TimingContext is TimingStates.NeutralOpen or TimingStates.SpellDuelOpen;
    }

    internal static bool ValidLegendDiscardDrawChoice(MatchState state)
    {
        var choice = state.PendingHandChoice;
        if (choice is null) return true;
        if (choice.LegendDiscardDraw is not { } context)
            return choice.EffectKind != LegendDiscardDrawWindow && choice.ChoiceWindow != LegendDiscardDrawWindow
                && choice.Reason != LegendDiscardDrawWindow;
        if (!ValidLegendDiscardDrawOrigin(state, context) || !state.PlayerZones.TryGetValue(context.ControllerId, out var zones)) return false;
        var legal = zones.Hand.Where(id => IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, context.ControllerId, id))
            .Order(StringComparer.Ordinal).ToArray();
        return choice.PlayerId == context.ControllerId && choice.SourceObjectId == context.Source.ObjectId
            && choice.ChoiceId == $"CHOICE-{context.StackItemId}" && choice.ChoiceWindow == LegendDiscardDrawWindow
            && choice.Reason == LegendDiscardDrawWindow && choice.EffectKind == LegendDiscardDrawWindow
            && choice.RequiredCount == 1 && choice.MaxCount == 1 && choice.DrawCount == 1 && legal.Length > 0
            && choice.LegalObjectIds.Order(StringComparer.Ordinal).SequenceEqual(legal)
            && choice.MovementObserver is null && choice.LastBreathDraw is null;
    }

    private static ResolutionResult QueueLegendDiscardDraw(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, List<GameEvent> events)
    {
        var source = state.CardObjects[sourceId];
        var tick = state.Tick + 1;
        var stackId = $"STACK-{tick}-{sourceId}-LEGEND-DISCARD";
        var item = new StackItemState(stackId, player, sourceId, ability.EffectKind, cardNo,
            timingContext: StackTimingContextForNewStackItem(state)) {
            LegendDiscardDraw = new(ability.AbilityId, new(sourceId, source.ObjectGeneration), cardNo, player,
                stackId, tick, source.CopyFaceTransitions?.Count ?? 0) };
        var next = state with { Tick = tick, ActivePlayerId = player,
            TimingState = state.TimingState == TimingStates.SpellDuelOpen ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed,
            PriorityPlayerId = player, PassedPriorityPlayerIds = [], StackItems = [..state.StackItems, item] };
        events.Add(new("STACK_ITEM_ADDED", "传奇弃牌抽牌技能加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = stackId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = Array.Empty<string>() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendDiscardDraw(MatchState state, StackItemState item)
    {
        if (!ValidLegendDiscardDraw(state, item)) throw new InvalidOperationException("Invalid legend discard ability.");
        return ResolveDiscardDrawStackItem(state, item, 1, 1, LegendDiscardDrawWindow, LegendDiscardDrawWindow,
            "LEGEND_ACTIVATED", activatedAbility: true);
    }
}
