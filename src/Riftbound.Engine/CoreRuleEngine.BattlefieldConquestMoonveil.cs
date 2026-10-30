using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestReadyEquipmentContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string ConquestSourceObjectId,
    [property: JsonRequired] long ConquestTick)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReadyCompleted { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DetachChosen { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetTokenFactoryCardNo { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestReadyEquipmentPublicContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] long ConquestTick,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ReadyCompleted = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? DetachChosen = null);

public sealed partial class CoreRuleEngine
{
    internal const string BattlefieldConquestEquipmentDetachWindow = "BATTLEFIELD_CONQUEST_EQUIPMENT_DETACH";

    private static IReadOnlyList<TriggerQueueItemState> CaptureBattlefieldConquestReadyEquipment(
        MatchState state, string controller, string battlefield, string conquestSourceObjectId, long tick)
    {
        if (!TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battlefield, out var id, out var card)
            || !BattlefieldTriggerSpecRules.TryGetTrigger(card.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerReadyEquipmentTrigger, out var spec)) return [];
        var context = new BattlefieldConquestReadyEquipmentContext(new(id, card.ObjectGeneration), card.CardNo!,
            controller, conquestSourceObjectId, tick);
        return [new($"CONQUEST-READY-EQUIPMENT:{tick}:{id}", controller, id, spec.Kind, "BATTLEFIELD_CONQUERED")
        { SourceCardNo = card.CardNo, EnqueuedAtTick = tick, BattlefieldConquestReadyEquipment = context }];
    }

    internal static BattlefieldConquestReadyEquipmentPublicContext? PublicBattlefieldConquestReadyEquipment(
        BattlefieldConquestReadyEquipmentContext? context)
        => context is null ? null : new(context.Source, context.CardNo, context.ControllerId, context.ConquestTick,
            context.ReadyCompleted, context.DetachChosen);

    internal static bool ValidBattlefieldConquestReadyEquipmentFrame(BattlefieldConquestReadyEquipmentContext context)
        => context.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && !string.IsNullOrWhiteSpace(context.CardNo) && !string.IsNullOrWhiteSpace(context.ControllerId)
            && !string.IsNullOrWhiteSpace(context.ConquestSourceObjectId) && context.ConquestTick >= 0
            && (context.DetachChosen is null || context.ReadyCompleted)
            && (context.TargetTokenFactoryCardNo is null || P6TokenFactoryCatalog.IsTokenFactory(context.TargetTokenFactoryCardNo))
            && BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerReadyEquipmentTrigger, out _);

    internal static bool ValidBattlefieldConquestReadyEquipmentContext(MatchState state,
        BattlefieldConquestReadyEquipmentContext context, string effect, string source, string controller, string? cardNo)
        => ValidBattlefieldConquestReadyEquipmentFrame(context)
            && effect == TriggerKinds.BattlefieldConquerReadyEquipment && source == context.Source.ObjectId
            && controller == context.ControllerId && cardNo == context.CardNo && state.Seats.ContainsKey(controller)
            && context.ConquestTick <= state.Tick && state.CardObjects.TryGetValue(source, out var battlefield)
            && (battlefield.CardNo == context.CardNo || ReplacedBattlefieldCard(state, battlefield)?.CardNo == context.CardNo)
            && battlefield.ObjectGeneration >= context.Source.Generation
            && (state.BattlefieldResolutions.Any(r => r.Kind == "CONQUERED" && r.Tick == context.ConquestTick
                    && r.BattlefieldObjectId == source && r.PlayerId == controller
                    && r.SourceObjectId == context.ConquestSourceObjectId)
                || state.BattlefieldResolutions.Count == 12
                    && state.BattlefieldResolutions.All(r => r.Tick > context.ConquestTick));

    private static string[] BattlefieldConquestReadyEquipmentTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(card => card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                && !card.IsFaceDown && IsPlayerControlledFieldObject(state, item.ControllerId, card.ObjectId)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static string VisibleMoonveilConquestSource(MatchState state, string objectId)
        => state.PlayerZones.Values.Any(z => z.Hand.Contains(objectId) || z.MainDeck.Contains(objectId) || z.RuneDeck.Contains(objectId))
            || state.CardObjects.TryGetValue(objectId, out var source) && source.IsFaceDown ? "HIDDEN" : objectId;

    private static bool NeedsBattlefieldConquestReadyEquipmentConfirmation(StackItemState item)
        => item.BattlefieldConquestReadyEquipment is { ReadyCompleted: false } && item.TargetGenerations is null;

    private static PendingCardChoiceState BattlefieldConquestReadyEquipmentTargetChoice(MatchState state, StackItemState item)
        => new("TRIGGER-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 0, 1,
            BattlefieldConquestReadyEquipmentTargets(state, item), [item.SourceObjectId],
            "可选择一件友方装备确认此技能，或不选择以放弃；确认目标后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareBattlefieldConquestReadyEquipmentConfirmation(ResolutionResult result, StackItemState item)
    {
        var choice = BattlefieldConquestReadyEquipmentTargetChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var next = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidBattlefieldConquestReadyEquipmentTargetChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.BattlefieldConquestReadyEquipment is not { ReadyCompleted: false } context
            || !NeedsBattlefieldConquestReadyEquipmentConfirmation(item) || !ValidBattlefieldConquestReadyEquipmentContext(state, context,
                item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)) return false;
        var expected = BattlefieldConquestReadyEquipmentTargetChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId
            && choice.EffectKind == item.EffectKind && choice.RequiredCount == 0 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static bool LegalCapturedBattlefieldEquipment(MatchState state, StackItemState item, out CardObjectState equipment)
    {
        equipment = default!;
        if (item.TargetObjectIds.Count != 1 || item.TargetGenerations is null || item.TargetGenerations.Count != 1) return false;
        var id = item.TargetObjectIds[0];
        return item.TargetGenerations.TryGetValue(id, out var generation)
            && state.CardObjects.TryGetValue(id, out equipment!) && equipment.ObjectGeneration == generation
            && BattlefieldConquestReadyEquipmentTargets(state, item).Contains(id, StringComparer.Ordinal);
    }

    private static PendingCardChoiceState BattlefieldConquestReadyEquipmentDetachChoice(StackItemState item)
        => new("CONQUEST-DETACH:" + item.StackItemId, BattlefieldConquestEquipmentDetachWindow,
            item.ControllerId, 0, 1, [item.TargetObjectIds.Single()], [item.SourceObjectId],
            "装备已处理。可选择卸除此武装，或不选择以保持贴附。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    internal static bool ValidBattlefieldConquestReadyEquipmentDetachChoice(MatchState state, PendingCardChoiceState choice)
    {
        if (state.StackItems.LastOrDefault() is not { BattlefieldConquestReadyEquipment: { ReadyCompleted: true, DetachChosen: null } context } item
            || !ValidBattlefieldConquestReadyEquipmentContext(state, context, item.EffectKind,
                item.SourceObjectId, item.ControllerId, item.CardNo)
            || !LegalCapturedBattlefieldEquipment(state, item, out var equipment)
            || !equipment.Tags.Contains("武装", StringComparer.Ordinal) || string.IsNullOrWhiteSpace(equipment.AttachedToObjectId)) return false;
        var expected = BattlefieldConquestReadyEquipmentDetachChoice(item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.ResolvingStackItemId == item.StackItemId && choice.PlayerId == item.ControllerId
            && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static ResolutionResult ResolveBattlefieldConquestReadyEquipmentDetachChoice(
        MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidBattlefieldConquestReadyEquipmentDetachChoice(state, choice))
            return RejectWithCorePrompts(state, "月帷祭坛的可选卸除上下文已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems[^1];
        var changed = item with { BattlefieldConquestReadyEquipment = item.BattlefieldConquestReadyEquipment! with
            { DetachChosen = selected.Count != 0 } };
        var next = state with { PendingCardChoice = null, StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(changed).ToArray() };
        return ResolvePassPriority(next, new("battlefield-equipment-detach-continuation", item.ControllerId, CommandTypes.PassPriority), forceResolve: true);
    }

    private static StackResolutionResult ResolveBattlefieldConquestReadyEquipment(MatchState state, StackItemState item)
    {
        var context = item.BattlefieldConquestReadyEquipment!;
        if (!ValidBattlefieldConquestReadyEquipmentContext(state, context, item.EffectKind, item.SourceObjectId,
                item.ControllerId, item.CardNo)) throw new InvalidOperationException("Invalid captured battlefield equipment skill.");
        if (context.ReadyCompleted && context.DetachChosen is null)
            throw new InvalidOperationException("The suspended equipment choice has not been completed.");
        if (!LegalCapturedBattlefieldEquipment(state, item, out var equipment)) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var target = equipment.ObjectId;
        if (!context.ReadyCompleted)
        {
            if (equipment.IsExhausted)
            {
                using var readyInstruction = new MightInstructionScope(state, zones, cards, events);
                AddReadiedObservation(zones, cards, equipment, events);
                cards[target] = equipment with { IsExhausted = false };
                events.Add(new GameEvent("EQUIPMENT_READIED", "所选装备变为活跃状态",
                    new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = target,
                        ["reason"] = item.EffectKind, ["wasExhausted"] = true, ["isExhausted"] = false }));
            }
            if (equipment.Tags.Contains("武装", StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(equipment.AttachedToObjectId))
            {
                var changed = item with { BattlefieldConquestReadyEquipment = context with { ReadyCompleted = true } };
                var choice = BattlefieldConquestReadyEquipmentDetachChoice(changed);
                events.Add(new GameEvent("CARD_CHOICE_REQUESTED", "选择是否卸除已确认的武装",
                    new Dictionary<string, object?> { ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow,
                        ["playerId"] = item.ControllerId, ["sourceObjectId"] = context.Source.ObjectId,
                        ["targetObjectIds"] = item.TargetObjectIds.ToArray() }));
                return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                    StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? changed : i).ToArray(),
                    PendingCardChoice = choice, Events = events };
            }
        }
        var detached = false;
        if (context.ReadyCompleted && context.DetachChosen == true && equipment.Tags.Contains("武装", StringComparer.Ordinal)
            && !string.IsNullOrWhiteSpace(equipment.AttachedToObjectId))
        {
            using var detachInstruction = new MightInstructionScope(state, zones, cards, events);
            cards[target] = equipment with { AttachedToObjectId = null };
            detached = true;
            events.Add(new GameEvent("EQUIPMENT_DETACHED", "所选武装已卸除",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = context.Source.ObjectId,
                    ["abilityId"] = item.EffectKind, ["unitObjectId"] = equipment.AttachedToObjectId,
                    ["equipmentObjectId"] = target, ["controllerId"] = item.ControllerId,
                    ["ownerId"] = string.IsNullOrWhiteSpace(equipment.OwnerId) ? item.ControllerId : equipment.OwnerId,
                    ["previousAttachedToObjectId"] = equipment.AttachedToObjectId }));
        }
        events.Add(new GameEvent("BATTLEFIELD_TRIGGER_RESOLVED", "征服战场的装备技能已结算",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["battlefieldId"] = context.Source.ObjectId,
                ["battlefieldObjectId"] = context.Source.ObjectId, ["battlefieldCardNo"] = context.CardNo,
                ["trigger"] = item.EffectKind, ["sourceObjectId"] = VisibleMoonveilConquestSource(state, context.ConquestSourceObjectId),
                ["equipmentObjectId"] = target, ["detachesArmament"] = detached }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }
}
