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
            LeadingManaCost = mode.LeadingManaCost, CreatedTokenFactoryCardNo = mode.CreatedTokenFactoryCardNo,
            ConsumesSourceBoonDuringResolution = mode.ConsumesSourceBoonDuringResolution };
    }

    private static bool NeedsUnitEventMode(StackItemState item) => item.UnitEvent is { Mode: null } context
        && UnitEventSpec(context.CardNo)?.Modes is { Count: > 0 };

    private static RuleTextModeUse UnitEventModeUse(StackItemState item, string mode)
        => new(new(item.SourceObjectId, item.UnitEvent!.SourceGeneration), item.UnitEvent.CardNo, mode, item.UnitEvent.Origin, item.UnitEvent.TokenFactoryCardNo);

    private static bool UnitEventModeAlreadyUsed(MatchState state, StackItemState item, string mode)
        => UnitEventSpec(item.UnitEvent!.CardNo)?.DistinctModesPerTurn == true
            && state.RuleTextModeUses is { } ledger && ledger.TurnNumber == state.TurnNumber
            && ledger.Uses.Contains(UnitEventModeUse(item, mode));

    private static PendingCardChoiceState UnitEventModeChoice(MatchState state, StackItemState item)
        => new("UNIT-EVENT-MODE:" + item.StackItemId, UnitEventModeWindow, item.ControllerId, 1, 1,
            UnitEventSpec(item.UnitEvent!.CardNo)!.Modes!.Where(m => !UnitEventModeAlreadyUsed(state, item, m.Id)
                && (m.Effect != "STUN_ENEMY" || UnitEventTargets(state, item with {
                    UnitEvent = item.UnitEvent with { Mode = m.Id } }).Length > 0)).Select(m => m.Id).ToArray(), [item.SourceObjectId],
            "选择一个技能效果；确认后双方可以响应。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareUnitEventMode(ResolutionResult result, StackItemState item)
    {
        var choice = UnitEventModeChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var next = result.State with { PendingCardChoice = choice,
            ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidUnitEventModeChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsUnitEventMode(item) || item.TriggerCost is not null
            || !ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId)) return false;
        var expected = UnitEventModeChoice(state, item);
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
        var ledger = state.RuleTextModeUses;
        if (UnitEventSpec(item.UnitEvent!.CardNo)!.DistinctModesPerTurn)
            ledger = new(state.TurnNumber, [..(ledger?.TurnNumber == state.TurnNumber ? ledger.Uses : []),
                UnitEventModeUse(item, selected.Single())]);
        var next = RestoreAfterTriggerConfirmation(state with { Tick = state.Tick + 1, PendingCardChoice = null,
            RuleTextModeUses = ledger,
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? changed : i).ToArray() }, item);
        var events = new List<GameEvent> { new("TRIGGER_MODE_CONFIRMED", "已确认单位触发技能的效果", new Dictionary<string, object?> {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(next, item.SourceObjectId, ProjectionViewers.Spectator),
                ["mode"] = selected.Single() }) };
        if (UnitEventSpec(item.UnitEvent!.CardNo)!.DistinctModesPerTurn)
            events.Add(new("UNIT_EVENT_MODE_USED", "记录本回合技能模式使用", new Dictionary<string, object?> {
                ["modeUse"] = UnitEventModeUse(item, selected.Single()), ["playerId"] = item.ControllerId }));
        return PrepareTriggerConfirmation(new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)));
    }
}
