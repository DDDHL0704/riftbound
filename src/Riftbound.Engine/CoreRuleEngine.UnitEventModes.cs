using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventModeWindow = "UNIT_EVENT_MODE_CONFIRMATION";

    // Modes are fixed during trigger confirmation (CN403.2), before leading
    // instruction costs and before any player receives response priority.
    private static UnitEventAbilitySpec? SelectedUnitEventSpec(UnitEventContext context)
    {
        var spec = UnitEventSpec(context.CardNo);
        if (context.Mode is null) return spec;
        if (spec?.Modes?.SingleOrDefault(m => m.Id == context.Mode) is not { } mode) return null;
        return spec with { Effect = mode.Effect, Amount = mode.Amount, Modes = null,
            LeadingPowerCost = mode.LeadingPowerCost, LeadingPowerTrait = mode.LeadingPowerTrait,
            LeadingManaCost = mode.LeadingManaCost, CreatedTokenFactoryCardNo = mode.CreatedTokenFactoryCardNo };
    }

    private static bool NeedsUnitEventMode(StackItemState item) => item.UnitEvent is { Mode: null } context
        && UnitEventSpec(context.CardNo)?.Modes is { Count: > 0 };

    private static PendingCardChoiceState UnitEventModeChoice(StackItemState item)
        => new("UNIT-EVENT-MODE:" + item.StackItemId, UnitEventModeWindow, item.ControllerId, 1, 1,
            UnitEventSpec(item.UnitEvent!.CardNo)!.Modes!.Select(m => m.Id).ToArray(), [item.SourceObjectId],
            "选择一个技能效果；确认后双方可以响应。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareUnitEventMode(ResolutionResult result, StackItemState item)
    {
        var next = result.State with { PendingCardChoice = UnitEventModeChoice(item),
            ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidUnitEventModeChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsUnitEventMode(item) || item.TriggerCost is not null
            || !ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo)) return false;
        var expected = UnitEventModeChoice(item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.RequiredCount == 1 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    internal static string UnitEventModeLabel(MatchState state, PendingCardChoiceState choice, string mode)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId)?.UnitEvent is { } context
            ? UnitEventSpec(context.CardNo)?.Modes?.FirstOrDefault(m => m.Id == mode)?.Label ?? mode : mode;

    private static ResolutionResult ResolveUnitEventMode(MatchState state, PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        if (!ValidUnitEventModeChoice(state, choice))
            return RejectWithCorePrompts(state, "技能模式选择已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var changed = item with { UnitEvent = item.UnitEvent! with { Mode = selected.Single() } };
        var next = RestoreAfterTriggerConfirmation(state with { Tick = state.Tick + 1, PendingCardChoice = null,
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? changed : i).ToArray() }, item);
        return PrepareTriggerConfirmation(new(true, null, next,
            [new("TRIGGER_MODE_CONFIRMED", "已确认单位触发技能的效果", new Dictionary<string, object?> {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(next, item.SourceObjectId, "__spectator__"),
                ["mode"] = selected.Single() })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)));
    }
}
