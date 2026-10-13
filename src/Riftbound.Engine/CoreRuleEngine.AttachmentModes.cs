using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record RuleTextModeUse(ObjectBinding Source, string CardNo, string Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);
public sealed record RuleTextModeUseLedger(int TurnNumber, IReadOnlyList<RuleTextModeUse> Uses);

public sealed partial class CoreRuleEngine
{
    internal const string AttachmentModeWindow = "ATTACHMENT_MODE_CONFIRMATION";
    internal const string AttachmentModeTargetWindow = "ATTACHMENT_TARGET_CONFIRMATION";
    private static TriggerSpec? AttachmentModeSpec(string? card)
        => UnitTriggerPaymentSpecRules.TryGetTrigger(card, t => t.Kind == TriggerKinds.UnitArmamentAttachedChooseUnusedMode
            && t.Timing == TriggerTimings.UnitArmamentAttached && t.DistinctModesPerTurn == true && t.Modes is { Count: > 0 }, out var spec) ? spec : null;

    private static bool SameModeInstance(RuleTextModeUse use, string source, AttachmentObserverContext context)
        => use.Source.ObjectId == source && use.Source.Generation == context.SourceGeneration && use.CardNo == context.CardNo
            && use.Origin == context.Origin;

    private static string[] AvailableAttachmentModes(MatchState state, StackItemState item)
        => AttachmentModeSpec(item.CardNo)!.Modes!.Where(mode => (state.RuleTextModeUses is not { } ledger
            || ledger.TurnNumber != state.TurnNumber || !ledger.Uses.Any(use => SameModeInstance(use, item.SourceObjectId, item.AttachmentObserver!) && use.Mode == mode.Id))
            && (mode.RuneCallCount is not null || AttachmentModeTargets(state, item, mode).Length >= (mode.RuneReadyCount ?? 1)))
            .Select(mode => mode.Id).ToArray();

    private static TriggerModeSpec? SelectedAttachmentMode(AttachmentObserverContext context)
        => AttachmentModeSpec(context.CardNo)?.Modes?.FirstOrDefault(mode => mode.Id == context.Mode);

    internal static string AttachmentModeLabel(MatchState state, PendingCardChoiceState choice, string id)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { } item
            ? AttachmentModeSpec(item.CardNo)?.Modes?.FirstOrDefault(mode => mode.Id == id)?.Label ?? id : id;

    internal static string AttachmentObserverLabel(AttachmentObserverContext context)
        => AttachmentModeSpec(context.CardNo) is null ? "贴附武装：支付费用后双方响应，结算时抽牌"
            : SelectedAttachmentMode(context)?.Label ?? "贴附武装：确认本回合尚未选过的模式和目标后双方响应";

    private static bool ValidAttachmentModeShape(AttachmentObserverContext context)
    {
        if (AttachmentDrawSpec(context.CardNo) is not null) return context.Mode is null && context.References is null;
        if (AttachmentModeSpec(context.CardNo) is null) return false;
        if (context.Mode is null) return context.References is null;
        var mode = SelectedAttachmentMode(context); if (mode is null) return false;
        return context.References is null || context.References.All(b => b is not null && !string.IsNullOrWhiteSpace(b.ObjectId) && b.Generation >= 0)
            && context.References.Select(b => b.ObjectId).Distinct(StringComparer.Ordinal).Count() == context.References.Count
            && (mode.RuneCallCount is not null ? context.References.Count == 0
                : mode.BoonCount is not null ? context.References.Count == 1 : context.References.Count == mode.RuneReadyCount);
    }

    private static bool NeedsAttachmentModeConfirmation(StackItemState item)
        => item.AttachmentObserver is { References: null } && AttachmentModeSpec(item.CardNo) is not null;

    private static string[] AttachmentModeTargets(MatchState state, StackItemState item)
        => AttachmentModeTargets(state, item, SelectedAttachmentMode(item.AttachmentObserver!));

    private static string[] AttachmentModeTargets(MatchState state, StackItemState item, TriggerModeSpec? mode)
    {
        if (mode?.RuneReadyCount is not null) return state.PlayerZones[item.ControllerId].Base.Where(id =>
            state.CardObjects.TryGetValue(id, out var card) && card.ControllerId == item.ControllerId && !card.IsFaceDown
            && card.Tags.Contains(CardObjectTags.RuneCard)).Order(StringComparer.Ordinal).ToArray();
        if (mode?.BoonCount is not null) return state.CardObjects.Values.Where(card => card.ControllerId == item.ControllerId
            && card.Tags.Contains(CardObjectTags.UnitCard) && !card.IsFaceDown
            && AttachedRuleTextRules.Field(state, card.ObjectId) && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
        return [];
    }

    private static PendingCardChoiceState AttachmentModeChoice(MatchState state, StackItemState item)
    {
        var context = item.AttachmentObserver!; var choosingMode = context.Mode is null;
        var legal = choosingMode ? AvailableAttachmentModes(state, item) : AttachmentModeTargets(state, item);
        var count = choosingMode ? 1 : SelectedAttachmentMode(context)!.RuneReadyCount ?? 1;
        return new("ATTACHMENT-MODE:" + item.StackItemId, choosingMode ? AttachmentModeWindow : AttachmentModeTargetWindow,
            item.ControllerId, count, count, legal, [item.SourceObjectId], choosingMode
                ? "选择此技能实例本回合尚未选过的效果；确认引用目标后双方可以响应。" : "确认本次效果引用的友方对象，然后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PrepareAttachmentMode(ResolutionResult result, StackItemState item)
    {
        var choice = AttachmentModeChoice(result.State, item);
        if (item.AttachmentObserver!.Mode is null && choice.LegalObjectIds.Count == 0)
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        if (item.AttachmentObserver.Mode is not null && SelectedAttachmentMode(item.AttachmentObserver)!.RuneCallCount is not null)
            return CompleteAttachmentMode(result.State, item, []);
        var next = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidAttachmentModeChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsAttachmentModeConfirmation(item)) return false;
        var expected = AttachmentModeChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == item.ControllerId
            && choice.RequiredCount == expected.RequiredCount && choice.MaxCount == expected.MaxCount
            && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveAttachmentModeChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidAttachmentModeChoice(state, choice)) return RejectWithCorePrompts(state, "贴附技能的模式或引用上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        if (choice.ChoiceWindow == AttachmentModeWindow) {
            var changed = item with { AttachmentObserver = item.AttachmentObserver! with { Mode = selected.Single() } };
            var next = state with { Tick = state.Tick + 1, PendingCardChoice = null, StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? changed : i).ToArray() };
            return PrepareAttachmentMode(new(true, null, next, [], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)), changed);
        }
        return CompleteAttachmentMode(state with { Tick = state.Tick + 1 }, item, selected.Select(id => new ObjectBinding(id, state.CardObjects[id].ObjectGeneration)).ToArray());
    }

    private static ResolutionResult CompleteAttachmentMode(MatchState state, StackItemState item, IReadOnlyList<ObjectBinding> references)
    {
        var context = item.AttachmentObserver!; var changed = item with { AttachmentObserver = context with { References = references },
            TargetObjectIds = references.Select(b => b.ObjectId).ToArray(),
            TargetGenerations = references.Count == 0 ? null : references.ToDictionary(b => b.ObjectId, b => b.Generation, StringComparer.Ordinal) };
        var uses = state.RuleTextModeUses is { } ledger && ledger.TurnNumber == state.TurnNumber ? ledger.Uses.ToList() : [];
        uses.Add(new(new(item.SourceObjectId, context.SourceGeneration), context.CardNo, context.Mode!, context.Origin));
        var next = state with { PendingCardChoice = null, RuleTextModeUses = new(state.TurnNumber, uses),
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? changed : i).ToArray(),
            PriorityPlayerId = state.StackItems.Last().ControllerId, PassedPriorityPlayerIds = [], TimingState = TimingStates.NeutralOpen };
        return PrepareTriggerConfirmation(new(true, null, next, [new("ATTACHMENT_MODE_CONFIRMED", "已确认本回合技能模式及引用对象", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["mode"] = context.Mode, ["targetObjectIds"] = references.Select(b => b.ObjectId).ToArray()
        })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)));
    }

    private static StackResolutionResult ResolveAttachmentMode(MatchState state, StackItemState item)
    {
        var context = item.AttachmentObserver!; var mode = SelectedAttachmentMode(context);
        if (mode is null || context.References is null) throw new InvalidOperationException("Attachment mode has not been confirmed.");
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        foreach (var binding in context.References) {
            if (!cards.TryGetValue(binding.ObjectId, out var target) || target.ObjectGeneration != binding.Generation
                || target.ControllerId != item.ControllerId || target.IsFaceDown || !AttachedRuleTextRules.Field(state, target.ObjectId)) continue;
            if (mode.RuneReadyCount is not null && target.Tags.Contains(CardObjectTags.RuneCard)) {
                if (!target.IsExhausted) continue; cards[target.ObjectId] = target with { IsExhausted = false };
                events.Add(new("RUNE_READIED", "贴附技能让符文活跃", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target.ObjectId }));
            } else if (mode.BoonCount is not null && target.Tags.Contains(CardObjectTags.UnitCard))
                GrantLegendBoon(cards, target.ObjectId, item.ControllerId, item.SourceObjectId, item.EffectKind, events);
        }
        if (mode.RuneCallCount is { } count) {
            var called = CallRunes(zones, cards, item.ControllerId, count);
            if (called.CalledRuneObjectIds.Count > 0) events.Add(new("RUNES_CALLED", "贴附技能召出休眠符文", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["playerId"] = item.ControllerId,
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds }));
        }
        var baseResult = NoopStackResolutionResult(state);
        return baseResult with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }

    internal static bool ValidRuleTextModeLedger(MatchState state)
    {
        if (state.RuleTextModeUses is not { } ledger) return true;
        if (ledger.TurnNumber != state.TurnNumber || ledger.Uses is null || ledger.Uses.Count == 0) return false;
        return ledger.Uses.All(use => use is not null && use.Source is not null && use.Source.Generation >= 0
            && state.CardObjects.TryGetValue(use.Source.ObjectId, out var source) && use.Source.Generation <= source.ObjectGeneration
            && AttachmentModeSpec(use.CardNo)?.Modes?.Any(m => m.Id == use.Mode) == true
            && AttachedRuleTextRules.ValidCaptured(state, use.Origin, use.Source.ObjectId, use.Source.Generation, use.CardNo))
            && ledger.Uses.Distinct().Count() == ledger.Uses.Count;
    }

    internal static bool ValidConfirmedAttachmentMode(MatchState state, StackItemState item)
    {
        if (item.AttachmentObserver is not { } context || AttachmentModeSpec(item.CardNo) is null) return true;
        if (!ValidAttachmentModeShape(context)) return false;
        if (context.References is null) return item.TargetObjectIds.Count == 0 && item.TargetGenerations is null;
        var mode = SelectedAttachmentMode(context)!;
        return item.TargetObjectIds.SequenceEqual(context.References.Select(b => b.ObjectId))
            && (context.References.Count == 0 ? item.TargetGenerations is null : item.TargetGenerations is not null
                && item.TargetGenerations.Count == context.References.Count && context.References.All(b => item.TargetGenerations.TryGetValue(b.ObjectId, out var generation) && generation == b.Generation))
            && context.References.All(binding => !state.CardObjects.TryGetValue(binding.ObjectId, out var card)
                || binding.Generation <= card.ObjectGeneration && (binding.Generation != card.ObjectGeneration
                    || card.Tags.Contains(mode.RuneReadyCount is not null ? CardObjectTags.RuneCard : CardObjectTags.UnitCard)))
            && state.RuleTextModeUses is { TurnNumber: var turn } ledger && turn == state.TurnNumber
            && ledger.Uses.Any(use => SameModeInstance(use, item.SourceObjectId, context) && use.Mode == context.Mode);
    }
}
