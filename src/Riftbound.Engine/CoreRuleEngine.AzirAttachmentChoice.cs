using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string AzirAttachmentWindow = "AZIR_ARMAMENT_ATTACHMENT";

    private static bool CanExecuteAzirAttachment(MatchState state, StackItemState item)
    {
        var context = item.AzirMovement!;
        return context.Equipment is { } binding && state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.Source.Generation && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var unit)
            && unit.ObjectGeneration == item.TargetGenerations![unit.ObjectId]
            && IsLegalAzirSwiftSwapTarget(state, item.ControllerId, item.SourceObjectId, unit.ObjectId)
            && state.CardObjects.TryGetValue(binding.ObjectId, out var gear) && gear.ObjectGeneration == binding.Generation
            && IsObjectOnField(state.PlayerZones, gear.ObjectId) && IsLegalAzirArmamentReattachChoice(state.CardObjects, unit.ObjectId, gear.ObjectId);
    }

    private static PendingCardChoiceState AzirAttachmentChoice(StackItemState item)
        => new("AZIR-ATTACH:" + item.StackItemId, AzirAttachmentWindow, item.ControllerId, 0, 1,
            [item.AzirMovement!.Equipment!.ObjectId], [item.SourceObjectId, ..item.TargetObjectIds],
            "移动指示已完成。可选择已确认的武装，将其贴附给阿兹尔；不选则跳过，不能更换武装目标。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    internal static bool ValidAzirAttachmentChoice(MatchState state, PendingCardChoiceState choice)
    {
        if (state.StackItems.LastOrDefault() is not { AzirMovement: { MovementsCompleted: true, AttachmentChosen: null } } item
            || !ValidAzirMovement(state, item) || !CanExecuteAzirAttachment(state, item)) return false;
        var expected = AzirAttachmentChoice(item);
        return choice.ChoiceWindow == expected.ChoiceWindow && choice.ChoiceId == expected.ChoiceId
            && choice.ResolvingStackItemId == item.StackItemId && choice.PlayerId == item.ControllerId
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.SourceObjectId == item.SourceObjectId
            && choice.EffectKind == item.EffectKind && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveAzirAttachmentChoice(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidAzirAttachmentChoice(state, choice)) return RejectWithCorePrompts(state, "阿兹尔的可选贴附上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems[^1];
        var next = state with { PendingCardChoice = null, StackItems = state.StackItems.Take(state.StackItems.Count - 1)
            .Append(item with { AzirMovement = item.AzirMovement! with { AttachmentChosen = selected.Count != 0 } }).ToArray() };
        return ResolvePassPriority(next, new("azir-attachment-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
    }

    private static StackResolutionResult ResolveAzirAttachmentAfterMovements(MatchState state, StackItemState item)
    {
        var context = item.AzirMovement!;
        if (context.AttachmentChosen is null) return NoopStackResolutionResult(state) with {
            PendingCardChoice = AzirAttachmentChoice(item), StackItems = state.StackItems };
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones); var events = new List<GameEvent>(); var attached = false;
        if (context.AttachmentChosen == true && CanExecuteAzirAttachment(state, item)) {
            attached = TryAttachEquipment(zones, cards, locations, item.ControllerId, item.SourceObjectId,
                context.Equipment!.ObjectId, item.SourceObjectId, item.EffectKind, events, out var changedLocations,
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["abilityId"] = P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
                    ["stackItemId"] = item.StackItemId, ["targetObjectId"] = item.TargetObjectIds[0] });
            locations = changedLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }
        events.Add(new("ABILITY_RESOLVED", "阿兹尔可选贴附决定完成", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId,
            ["abilityId"] = P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["effectKind"] = item.EffectKind,
            ["targetObjectIds"] = item.TargetObjectIds.ToArray(), ["selectedArmamentObjectId"] = context.Equipment!.ObjectId,
            ["armamentReattachChosen"] = context.AttachmentChosen, ["armamentReattachApplied"] = attached,
            ["armamentReattachPolicy"] = P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
    }
}
