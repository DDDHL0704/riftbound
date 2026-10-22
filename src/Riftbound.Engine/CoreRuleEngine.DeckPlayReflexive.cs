using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeckPlayReflexiveContext(string ParentStackItemId, ObjectBinding Source, string CardNo,
    int ExecutionIndex, int RepeatCount, string TimingContext, DeckPlayQueuedCard? Selected,
    bool Confirmed = false, ObjectBinding? Played = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeckPlayReflexiveView([property: JsonRequired] int ExecutionIndex, [property: JsonRequired] bool Confirmed);

public sealed partial class CoreRuleEngine
{
    internal const string DeckPlayReflexiveEffect = "DECK_PLAY_REFLEXIVE_EMPOWER";

    private static (MatchState State, IReadOnlyList<GameEvent> Events) CreateDeckPlayReflexive(
        MatchState state, StackItemState parent, DeckPlaySequenceContext sequence)
    {
        // The optional instruction precedes the colon. Its decision belongs to
        // the parent; the newly created ability confirms only after it finishes.
        var decision = RuleChoices.Value!.Choose(parent.ControllerId, "是否创建强化所选卡牌的内嵌技能？",
            [new("CREATE", "创建强化技能"), new("DECLINE", "不创建强化技能")], kind: "DECK_REFLEXIVE_CREATE",
            preview: state with { StackItems = state.StackItems.Select(s => s.StackItemId == parent.StackItemId ? parent : s).ToArray() });
        if (decision == "DECLINE") return (state, []);
        var selected = sequence.Queue.SingleOrDefault(q => q.ExecutionIndex == sequence.ExecutionIndex);
        var context = new DeckPlayReflexiveContext(parent.StackItemId, sequence.Source, parent.CardNo!,
            sequence.ExecutionIndex, parent.EffectRepeatCount, parent.TimingContext, selected);
        var item = DeckPlayReflexiveItem(parent.ControllerId, context);
        return (state with { StackItems = state.StackItems.Append(item).ToArray() },
            [new("STACK_ITEM_PENDING", "创建独立的内嵌强化技能，等待来源完成", new Dictionary<string, object?> {
                ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = item.SourceObjectId,
                ["cardNo"] = item.CardNo, ["controllerId"] = item.ControllerId,
                ["parentStackItemId"] = parent.StackItemId, ["effectKind"] = DeckPlayReflexiveEffect,
                ["executionIndex"] = sequence.ExecutionIndex })]);
    }

    private static StackItemState DeckPlayReflexiveItem(string controller, DeckPlayReflexiveContext c)
        => new($"DECK-REFLEXIVE:{c.ParentStackItemId}:{c.ExecutionIndex}", controller, c.Source.ObjectId,
            DeckPlayReflexiveEffect, c.CardNo, timingContext: c.TimingContext) { DeckPlayReflexive = c };

    private static bool IsUnconfirmedDeckInstruction(StackItemState item)
        => item.DeckPlayPendingCard is not null || item.ObservedDeckPlay is not null
            || item.DeckPlayReflexive is { Confirmed: false };

    private static ResolutionResult AdvanceDeckPlayReflexives(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var stack = result.State.StackItems.ToArray(); var events = result.Events.ToList();
        var blocked = false;
        for (var index = 0; index < stack.Length; index++)
        {
            var item = stack[index];
            if (item.DeckPlayReflexive is { Confirmed: false } c && !blocked
                && !stack.Any(s => s.StackItemId == c.ParentStackItemId))
            {
                stack[index] = item with { DeckPlayReflexive = c with { Confirmed = true } };
                events.Add(new("STACK_ITEM_ADDED", "确认独立的内嵌强化技能", new Dictionary<string, object?> {
                    ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = item.SourceObjectId,
                    ["effectKind"] = item.EffectKind, ["controllerId"] = item.ControllerId }));
            }
            if (IsUnconfirmedDeckInstruction(stack[index])) blocked = true;
        }
        if (events.Count == result.Events.Count) return result;
        var next = result.State with { StackItems = stack };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static ResolutionResult RecordDeckPlayReflexiveCard(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || before.PendingEffectPlay?.Parent.DeckPlaySequence is not { } sequence
            || sequence.PlayIndex < 0 || sequence.PlayIndex >= sequence.Queue.Count) return result;
        var selected = sequence.Queue[sequence.PlayIndex]; var binding = selected.Card;
        if (!result.State.CardObjects.TryGetValue(binding.ObjectId, out var card)
            || card.ObjectGeneration != binding.Generation + 1 || !IsObjectOnField(result.State.PlayerZones, binding.ObjectId)) return result;
        var next = result.State with { StackItems = result.State.StackItems.Select(item => item.DeckPlayReflexive is { } c
            && c.ParentStackItemId == before.PendingEffectPlay.Parent.StackItemId && c.Selected == selected
                ? item with { DeckPlayReflexive = c with { Played = new(binding.ObjectId, card.ObjectGeneration) } } : item).ToArray() };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static StackResolutionResult ResolveDeckPlayReflexive(MatchState state, StackItemState item)
    {
        if (!ValidDeckPlayReflexive(state, item) || !item.DeckPlayReflexive!.Confirmed)
            throw new InvalidOperationException("Invalid deck-play reflexive ability.");
        return item.DeckPlayReflexive.Played is { } played ? ApplyEmpowerAction(state, played) : NoopStackResolutionResult(state);
    }

    internal static bool ValidDeckPlayReflexive(MatchState state, StackItemState item)
    {
        if (item.DeckPlayReflexive is not { } c) return item.EffectKind != DeckPlayReflexiveEffect;
        if (c.Source is null || string.IsNullOrWhiteSpace(c.Source.ObjectId) || string.IsNullOrWhiteSpace(c.CardNo)
            || string.IsNullOrWhiteSpace(c.ParentStackItemId) || c.Source.Generation < 0
            || c.ExecutionIndex < 0 || c.ExecutionIndex >= c.RepeatCount || c.RepeatCount < 1
            || !CardBehaviorRegistry.TryGetByCardNo(c.CardNo, out var definition) || !definition.DeckPlayOptionalReflexiveEmpower
            || !state.Seats.ContainsKey(item.ControllerId) || !state.CardObjects.TryGetValue(c.Source.ObjectId, out var source)
            || source.ObjectGeneration < c.Source.Generation || c.TimingContext is not ("" or TimingStates.NeutralOpen or TimingStates.NeutralClosed or TimingStates.SpellDuelOpen)
            || MatchStateHasher.HashValue(item) != MatchStateHasher.HashValue(DeckPlayReflexiveItem(item.ControllerId, c))) return false;
        var parent = state.StackItems.FirstOrDefault(s => s.StackItemId == c.ParentStackItemId);
        var pendingParent = (state.PendingEffectPlay?.Parent.ObservedPlayPermission?.Suspended ?? state.PendingEffectPlay)?.Parent;
        var boundParent = parent ?? (pendingParent?.StackItemId == c.ParentStackItemId ? pendingParent : null);
        if (boundParent is not null && (boundParent.DeckPlaySequence is not { } sequence || !ValidDeckPlaySequence(state, boundParent)
            || sequence.Source != c.Source || sequence.ControllerId != item.ControllerId || boundParent.CardNo != c.CardNo
            || boundParent.EffectRepeatCount != c.RepeatCount || sequence.ExecutionIndex < c.ExecutionIndex
            || c.Selected != sequence.Queue.SingleOrDefault(q => q.ExecutionIndex == c.ExecutionIndex)
            || c.ExecutionIndex == sequence.ExecutionIndex && sequence.SelectionIndex != sequence.Players.Count
            || parent is not null && (c.Confirmed || c.Played is not null))) return false;
        if (c.Selected is { } selected && (selected.Card is null || string.IsNullOrWhiteSpace(selected.Card.ObjectId)
            || string.IsNullOrWhiteSpace(selected.CardNo) || selected.ExecutionIndex != c.ExecutionIndex
            || selected.OwnerId != item.ControllerId || selected.BeforeBanishGeneration < 0
            || selected.Card.Generation < 2 || selected.BeforeBanishGeneration != selected.Card.Generation - 2
            || !CardBehaviorRegistry.TryGetByCardNo(selected.CardNo, out var permanent)
            || !permanent.PlaysSourceToBaseAsUnit && !permanent.PlaysSourceToBaseAsEquipment
            || !state.CardObjects.TryGetValue(selected.Card.ObjectId, out var card) || card.ObjectGeneration < selected.Card.Generation)) return false;
        if (c.Played is { } played && (c.Selected?.Card is not { } selectedBinding
            || played.ObjectId != selectedBinding.ObjectId || played.Generation < 1 || selectedBinding.Generation != played.Generation - 1
            || !state.CardObjects.TryGetValue(played.ObjectId, out var current) || current.ObjectGeneration < played.Generation)) return false;
        var siblings = state.StackItems.Where(s => s.DeckPlayReflexive?.ParentStackItemId == c.ParentStackItemId).ToArray();
        var executions = siblings.Select(s => s.DeckPlayReflexive!.ExecutionIndex).ToArray();
        if (!executions.SequenceEqual(executions.Distinct().Order())) return false;
        var index = state.StackItems.ToList().FindIndex(s => s.StackItemId == item.StackItemId);
        for (var otherIndex = 0; otherIndex < state.StackItems.Count; otherIndex++)
        {
            var other = state.StackItems[otherIndex]; int? execution = null;
            if (other.ObservedDeckPlay?.Instruction is { } observation && observation.Parent.StackItemId == c.ParentStackItemId)
                execution = observation.ExecutionIndex;
            if (other.DeckPlayPendingCard is { } pendingCard && pendingCard.ParentStackItemId == c.ParentStackItemId
                && boundParent?.DeckPlaySequence is { } queue && pendingCard.OrderIndex >= 0 && pendingCard.OrderIndex < queue.Queue.Count)
                execution = queue.Queue[pendingCard.OrderIndex].ExecutionIndex;
            if (execution is { } round && (round <= c.ExecutionIndex ? otherIndex > index : otherIndex < index)) return false;
        }
        var priorPending = state.StackItems.TakeWhile(s => s.StackItemId != item.StackItemId).Any(IsUnconfirmedDeckInstruction);
        return c.Confirmed ? parent is null && !priorPending : parent is not null || priorPending;
    }
}
