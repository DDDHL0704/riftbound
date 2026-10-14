using Riftbound.Contracts;
using System.Text.Json.Serialization;
namespace Riftbound.Engine;

public sealed record BeginningRecycleContext(string CardNo, long SourceGeneration, int Count, bool Completed = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    internal const string BeginningRecycleEffect = "CAPTURED_BEGINNING_GRAVEYARD_RECYCLE";
    internal const string GraveyardRecycleWindow = "GRAVEYARD_RECYCLE_EFFECT";
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> BeginningRecycleFamilies = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["OGN·109/298"]));
    private static int BeginningRecycleCount(string? cardNo)
        => BeginningRecycleFamilies.Value["OGN·109/298"].Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), StringComparer.Ordinal) ? 3 : 0;

    private static MatchState QueueBeginningRecycleAbilities(MatchState state, List<GameEvent> events)
    {
        var queue = state.TriggerQueue.ToList();
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (source.ControllerId != state.TurnPlayerId || source.IsFaceDown || !source.Tags.Contains(CardObjectTags.UnitCard)
                || !IsObjectOnField(state.PlayerZones, source.ObjectId)
                || !IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, source.ObjectId)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                var count = BeginningRecycleCount(instance.CardNo);
                if (count == 0) continue;
                var suffix = instance.Origin is null ? "" : "-" + instance.Id;
                var trigger = new TriggerQueueItemState($"beginning-recycle-{state.TurnNumber}-{state.Tick}-{source.ObjectId}{suffix}",
                    source.ControllerId, source.ObjectId, BeginningRecycleEffect, "TURN_START_BEGAN")
                    { SourceCardNo = instance.CardNo, BeginningRecycle = new(instance.CardNo, source.ObjectGeneration, count, Origin: instance.Origin) };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        return state with { TriggerQueue = queue };
    }

    internal static bool ValidBeginningRecycle(MatchState state, BeginningRecycleContext? context, string effect,
        string controller, string source, string? cardNo = null)
        => context is null ? effect != BeginningRecycleEffect
            : effect == BeginningRecycleEffect && !context.Completed && context.SourceGeneration >= 0
                && !string.IsNullOrWhiteSpace(source) && context.Count > 0 && BeginningRecycleCount(context.CardNo) == context.Count
                && (cardNo is null || cardNo == context.CardNo) && controller == state.TurnPlayerId
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.SourceGeneration, context.CardNo)
                && state.Phase == MatchPhases.TurnStart && state.TurnStartStep == "SCORE";

    private static StackResolutionResult BeginGraveyardRecycle(MatchState state, StackItemState item)
    {
        var context = item.BeginningRecycle!;
        if (context.Completed) return NoopStackResolutionResult(state);
        var legal = state.PlayerZones[item.ControllerId].Graveyard.Order(StringComparer.Ordinal).ToArray();
        var count = Math.Min(context.Count, legal.Length);
        if (count == 0) return NoopStackResolutionResult(state);
        var choice = new PendingCardChoiceState($"RECYCLE-{item.StackItemId}", GraveyardRecycleWindow,
            item.ControllerId, count, count, legal, legal, $"从废牌堆选择 {count} 张牌回收（不足三张则全部回收）",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
        return NoopStackResolutionResult(state) with { StackItems = state.StackItems, PendingCardChoice = choice,
            Events = [new("CARD_CHOICE_REQUESTED", "结算中选择回收的废牌", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow, ["requiredCount"] = count })] };
    }

    internal static bool ValidGraveyardRecycleChoice(MatchState state, PendingCardChoiceState choice)
    {
        var top = state.StackItems.LastOrDefault();
        if (top?.BeginningRecycle is not { } context || choice.ChoiceWindow != GraveyardRecycleWindow
            || !ValidBeginningRecycle(state, context, top.EffectKind, top.ControllerId, top.SourceObjectId, top.CardNo)
            || choice.DeckContext is not null || choice.HandContext is not null
            || choice.ResolvingStackItemId != top.StackItemId || choice.ChoiceId != $"RECYCLE-{top.StackItemId}"
            || choice.PlayerId != top.ControllerId || choice.SourceObjectId != top.SourceObjectId || choice.EffectKind != top.EffectKind) return false;
        var legal = state.PlayerZones[top.ControllerId].Graveyard.Order(StringComparer.Ordinal).ToArray();
        var count = Math.Min(context.Count, legal.Length);
        return count > 0 && choice.RequiredCount == count && choice.MaxCount == count
            && choice.LegalObjectIds.SequenceEqual(legal) && choice.ContextObjectIds.SequenceEqual(legal);
    }

    private static ResolutionResult ResolveGraveyardRecycleChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidGraveyardRecycleChoice(state, choice))
            return RejectWithCorePrompts(state, "回收选择上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Last();
        var zones = NormalizeZonesForSeats(state);
        var recycled = RecycleGraveyardCards(state, zones, state.CardObjects, item.ControllerId, item.SourceObjectId, selected.Order(StringComparer.Ordinal).ToArray(), "BEGINNING_EFFECT");
        var nextItem = item with { BeginningRecycle = item.BeginningRecycle! with { Completed = true } };
        var next = state with { PendingCardChoice = null, PlayerZones = zones, RngCursor = recycled.RngCursor,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(nextItem).ToArray() };
        var resumed = ResolvePassPriority(next, new("graveyard-recycle-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
        return resumed with { Events = recycled.Events.Append(new GameEvent("CARD_CHOICE_RESOLVED", "废牌回收选择完成",
            new Dictionary<string, object?> { ["choiceId"] = choice.ChoiceId, ["playerId"] = item.ControllerId, ["chosenCount"] = selected.Count }))
            .Concat(resumed.Events).ToArray() };
    }
}
