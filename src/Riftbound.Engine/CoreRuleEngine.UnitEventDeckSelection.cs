using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitEventDeckSelectionSpec(int LookCount, string SelectedRequiredTag,
    IReadOnlyList<string> BoonTraits);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevealedDeckSelectionFact(string ParentStackItemId, ObjectBinding Card,
    string CardNo, long FaceRevision, string? TokenFactoryCardNo, long RevealedTick,
    IReadOnlyList<string> Traits);

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnitEventDeckSelectionSpec? DeckSelection { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RevealedDeckSelectionFact? RevealedDeckSelection { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventDeckSelectionEffect = "LOOK_REVEAL_DRAW_RECYCLE_THEN_TRAIT_BOON";
    internal const string DeckSelectionBoonCreatedEvent = "DECK_SELECTION_BOON_CREATED";

    private static bool TryUnitEventDeckChoiceBehavior(StackItemState item, out CardBehaviorDefinition behavior)
    {
        behavior = null!;
        if (item.EffectKind != UnitEventEffect
            || item.UnitEvent is not { RevealedDeckSelection: null } context
            || UnitEventSpec(context.CardNo) is not { Effect: UnitEventDeckSelectionEffect,
                DeckSelection: { LookCount: > 0 } selection }) return false;
        behavior = new(context.CardNo, "查看并选择单位", 0, UnitEventEffect, 0, 1,
            MinTargetCount: 0, MainDeckLookCount: selection.LookCount,
            MainDeckTargetRequiredTag: selection.SelectedRequiredTag,
            DrawsSelectedMainDeckTarget: true, RecyclesUnselectedMainDeckLookCards: true);
        return true;
    }

    private static StackResolutionResult ResolveUnitEventDeckSelection(MatchState state, StackItemState item)
    {
        if (item.DeckChoiceCompleted) return NoopStackResolutionResult(state);
        if (!TryUnitEventDeckChoiceBehavior(item, out var behavior))
            throw new InvalidOperationException("Invalid unit-event deck selection.");
        return BeginDeckChoice(state, item, behavior);
    }

    private static RevealedDeckSelectionFact? CaptureDeckSelectionReveal(MatchState state,
        StackItemState item, IReadOnlyDictionary<string, CardObjectState> cards, IReadOnlyList<string> selected)
    {
        if (item.UnitEvent is not { RevealedDeckSelection: null } context
            || UnitEventSpec(context.CardNo)?.DeckSelection is null || selected.Count == 0) return null;
        var chosen = cards[selected.Single()];
        return new(item.StackItemId, new(chosen.ObjectId, chosen.ObjectGeneration), chosen.CardNo!,
            chosen.CopyFaceTransitions?.Count ?? 0, chosen.TokenFactoryCardNo,
            RuleChoices.Value?.CompletionTick ?? state.Tick + 1,
            chosen.Tags.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private static void AppendDeckSelectionBoonEvent(StackItemState item,
        RevealedDeckSelectionFact? revealed, List<GameEvent> events)
    {
        if (revealed is null || item.UnitEvent is not { } root
            || UnitEventSpec(root.CardNo)?.DeckSelection is not { } selection
            || !selection.BoonTraits.Any(revealed.Traits.Contains)) return;
        var context = root with { ReflexiveTick = revealed.RevealedTick, RevealedDeckSelection = revealed };
        // The selected unit has really been revealed. A later observation can
        // consume it before the draw without cancelling this independent skill.
        events.Add(WithUnitEvents(new(DeckSelectionBoonCreatedEvent, "根据实际展示单位的属性创建独立增益技能",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                ["parentSkillHash"] = MatchStateHasher.HashValue(item.StackItemId) }),
            [new(item.SourceObjectId, item.ControllerId, context)]));
    }

    private static bool ValidDeckSelectionReveal(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec)
    {
        if (spec.Effect != UnitEventDeckSelectionEffect || spec.DeckSelection is not { } selection)
            return context.RevealedDeckSelection is null;
        if (context.RevealedDeckSelection is not { } fact) return context.ReflexiveTick is null;
        if (string.IsNullOrWhiteSpace(fact.ParentStackItemId)
            || fact.Card is not { ObjectId.Length: > 0, Generation: >= 0 }
            || fact.FaceRevision < 0 || fact.RevealedTick < context.CapturedTick || fact.RevealedTick > state.Tick
            || context.ReflexiveTick != fact.RevealedTick || context.Mode is not null || fact.Traits is null
            || !fact.Traits.SequenceEqual(fact.Traits.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            || !fact.Traits.Contains(selection.SelectedRequiredTag)
            || !selection.BoonTraits.Any(fact.Traits.Contains)
            || !PredictBindingMatches(state, fact.Card, fact.CardNo, fact.FaceRevision, fact.RevealedTick,
                fact.TokenFactoryCardNo)) return false;
        // Outside the battlefield, these revealed cards have their printed face.
        // Keep the complete trait fact bound to the original accepted child capture.
        return PrintedCardFactory.TryCreate("revealed-selection", "", fact.CardNo, out var printed)
            && fact.TokenFactoryCardNo is null
            && fact.Traits.SequenceEqual(printed.Tags.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }
}
