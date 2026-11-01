using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string RadShowcaseRuneChoiceWindow = "RAD_SHOWCASE_RUNES";

    private static bool IsRadShowcaseRuneTarget(MatchState state, string playerId, string objectId)
        => EndRuneTargets(state, playerId).Contains(objectId, StringComparer.Ordinal);

    private static string[] RadShowcaseRuneTargets(MatchState state, string playerId)
        => EndRuneTargets(state, playerId);

    private static PendingCardChoiceState RadShowcaseRuneChoice(MatchState state, StackItemState item)
    {
        var legal = RadShowcaseRuneTargets(state, item.ControllerId);
        return new(
            $"RAD-SHOWCASE-RUNES:{item.StackItemId}",
            RadShowcaseRuneChoiceWindow,
            item.ControllerId,
            0,
            Math.Min(legal.Length, 2),
            legal,
            legal,
            "选择至多两枚符文变为活跃状态；不选则继续结算。",
            item.SourceObjectId,
            item.EffectKind)
        {
            ResolvingStackItemId = item.StackItemId
        };
    }

    internal static bool ValidRadShowcaseRuneChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(s => s.StackItemId == choice.ResolvingStackItemId);
        if (item is null || item.CardNo != "RAD·032/167" || item.EffectKind != "RAD_032_SHOW_EQUIPMENT_POWER_PLUS_TWO"
            || item.Showcase is null || item.ShowcaseRuneTargets is not null
            || !ValidShowcase(state, item)) return false;
        var expected = RadShowcaseRuneChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId
            && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId
            && choice.RequiredCount == 0
            && choice.MaxCount == expected.MaxCount
            && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveRadShowcaseRuneChoice(
        MatchState state,
        PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        if (!ValidRadShowcaseRuneChoice(state, choice)
            || selected.Count > 2
            || selected.Distinct(StringComparer.Ordinal).Count() != selected.Count
            || selected.Any(id => !choice.LegalObjectIds.Contains(id, StringComparer.Ordinal)))
            return RejectWithCorePrompts(state, "战前仪式的符文选择已失效。", ErrorCodes.InvalidTarget);

        var item = state.StackItems.Single(s => s.StackItemId == choice.ResolvingStackItemId);
        var targets = selected.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal);
        var next = state with
        {
            Tick = state.Tick + 1,
            PendingCardChoice = null,
            ActivePlayerId = item.ControllerId,
            PriorityPlayerId = item.ControllerId,
            PassedPriorityPlayerIds = [],
            StackItems = state.StackItems.Select(s => s.StackItemId == item.StackItemId
                ? s with { ShowcaseRuneTargets = targets }
                : s).ToArray()
        };
        return new(true, null, next, [new GameEvent(
            "RAD_SHOWCASE_RUNES_CONFIRMED",
            "战前仪式的符文目标已确认",
            new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId,
                ["playerId"] = item.ControllerId,
                ["objectIds"] = selected.ToArray()
            })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    internal static bool ValidRadShowcaseRuneTargets(MatchState state, StackItemState item)
    {
        if (item.ShowcaseRuneTargets is null)
            return item.CardNo != "RAD·032/167" || item.Showcase is null || !item.SourceConfirmed
                || state.PendingCardChoice is { ChoiceWindow: RadShowcaseRuneChoiceWindow }
                    && state.PendingCardChoice.ResolvingStackItemId == item.StackItemId;
        if (item.CardNo != "RAD·032/167" || item.EffectKind != "RAD_032_SHOW_EQUIPMENT_POWER_PLUS_TWO"
            || item.Showcase is null || !ValidShowcase(state, item)
            || item.ShowcaseRuneTargets.Count > 2
            || item.ShowcaseRuneTargets.Keys.Distinct(StringComparer.Ordinal).Count() != item.ShowcaseRuneTargets.Count)
            return false;
        return item.ShowcaseRuneTargets.All(pair => pair.Value >= 0
            && state.CardObjects.TryGetValue(pair.Key, out var rune)
            && rune.ObjectGeneration >= pair.Value
            && IsRadShowcaseRuneTarget(state, item.ControllerId, pair.Key));
    }

    private static StackItemState ResolveRadShowcaseRuneTargets(
        MatchState state,
        StackItemState item,
        CardBehaviorDefinition behavior,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        List<GameEvent> events)
    {
        if (behavior.RuneReadyCountIfShowcase <= 0 || item.Showcase is null || item.ShowcaseRuneTargets is null)
            return item;
        var current = state with { PlayerZones = playerZones, CardObjects = cardObjects };
        foreach (var (objectId, generation) in item.ShowcaseRuneTargets)
        {
            if (!IsRadShowcaseRuneTarget(current, item.ControllerId, objectId)
                || !cardObjects.TryGetValue(objectId, out var rune)
                || rune.ObjectGeneration < generation)
                continue;
            if (rune.IsExhausted)
                events.Add(new GameEvent(
                    "RUNE_READIED",
                    "战前仪式让选择的符文变为活跃状态",
                    new Dictionary<string, object?> {
                        ["playerId"] = item.ControllerId,
                        ["sourceObjectId"] = item.SourceObjectId,
                        ["objectIds"] = new[] { objectId },
                        ["count"] = 1
                    }));
            cardObjects[objectId] = rune with { IsExhausted = false };
        }
        return item;
    }
}
