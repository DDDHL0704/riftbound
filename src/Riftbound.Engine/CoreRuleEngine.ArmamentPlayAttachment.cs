using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record CardBehaviorDefinition
{
    public bool EffectPlayRequiresArmament { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string PlayHandArmamentThenAttachEffect = "PLAY_HAND_ARMAMENT_THEN_ATTACH_SOURCE";
    private const string AttachPlayedArmamentEffect = "ATTACH_CAPTURED_ARMAMENT_TO_SOURCE";
    internal const string PlayedArmamentAttachmentCreatedEvent = "PLAYED_ARMAMENT_ATTACHMENT_CREATED";

    internal static bool HasArmamentPlayAttachment(UnitEventContext? context)
        => context is not null && UnitEventSpecForContext(context)?.Effect == PlayHandArmamentThenAttachEffect;

    private static bool IsArmamentPlayAttachmentPermission(StackItemState parent)
        => parent.EffectKind == UnitEventEffect && HasArmamentPlayAttachment(parent.UnitEvent)
            && parent.UnitEvent!.ReflexiveTick is null;

    private static bool IsOptionalArmamentPlayEvent(UnitEventContext context)
        => SelectedUnitEventSpec(context) is { Optional: true, Effect: PlayHandArmamentThenAttachEffect };

    private static bool TryGetArmamentPlayAttachmentDefinition(StackItemState parent, out CardBehaviorDefinition definition)
    {
        definition = null!;
        if (!IsArmamentPlayAttachmentPermission(parent)) return false;
        definition = new(parent.CardNo, "从手牌打出武装，随后触发贴附", 0, parent.EffectKind, 0, 0,
            EffectPlaySourceZone: "HAND", EffectPlayMaximumManaCost: UnitEventSpec(parent.CardNo)!.Amount,
            EffectPlayIgnoreBaseMana: true, EffectPlayIgnoreBasePower: true,
            EffectPlayOptional: false, EffectPlayAllowsAnyCard: true) { EffectPlayRequiresArmament = true };
        return true;
    }

    private static bool ArmamentEffectPlaySourceAllowed(CardObjectState card, CardBehaviorDefinition definition)
        => !card.IsFaceDown && !IsTokenObject(card) && card.Tags.Contains(CardObjectTags.EquipmentCard)
            && OfficialArmamentCatalog.Find(card.CardNo) is not null
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var gear) && gear.PlaysSourceToBaseAsEquipment
            && (definition.EffectPlayMaximumManaCost <= 0 || gear.ManaCost <= definition.EffectPlayMaximumManaCost)
            && EffectPlayCandidateCostAllowed(card, definition);

    internal static bool ValidArmamentPlayAttachmentPermission(MatchState state, PendingEffectPlayState pending)
    {
        var parent = pending.Parent;
        if (!IsArmamentPlayAttachmentPermission(parent)
            || !ValidUnitEvent(state, parent.UnitEvent, parent.EffectKind, parent.SourceObjectId, parent.CardNo, parent.ControllerId)
            || parent.TriggerCost is null || !ValidTriggerCostReceipt(state, parent)
            || !ValidUnitEventTargets(parent) || parent.TargetObjectIds.Count != 0 || parent.EffectPlayCompleted
            || pending.PlayerId != parent.ControllerId || !state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)
            || pending.SourceZone != "HAND" || pending.DestinationPolicy != "ANY" || pending.Optional
            || !pending.IgnoreBaseMana || !pending.IgnoreBasePower || pending.IgnoreAllCosts || pending.ManaReduction != 0
            || pending.SourcePlayerId is not null || pending.SourceOwners is not null || pending.SourceReplay is not null
            || pending.OriginalLocation is not null || pending.OriginalBattlefield is not null || pending.ViewedCardIds is not null
            || pending.RevealedHand is not null || pending.MaximumUnitPower is not null
            || !TryGetArmamentPlayAttachmentDefinition(parent, out var definition)) return false;
        var expected = zones.Hand.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && EffectPlaySourceMatches(card, definition)).ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration);
        return expected.Count > 0 && expected.Count == pending.Sources.Count
            && expected.All(pair => pending.Sources.GetValueOrDefault(pair.Key, -1) == pair.Value);
    }

    private static bool ValidArmamentPlayAttachmentContext(MatchState state, UnitEventContext context, string? controller)
    {
        if (!HasArmamentPlayAttachment(context)) return true;
        if (context.ReflexiveTick is null) return context.Other is null;
        if (context.Other is not { TokenFactoryCardNo: null } gear || context.Mode is not null
            || context.ReflexiveTick <= context.CapturedTick || context.ReflexiveTick > state.Tick
            || !CardBehaviorRegistry.TryGetByCardNo(gear.CardNo, out var definition) || !definition.PlaysSourceToBaseAsEquipment
            || OfficialArmamentCatalog.Find(gear.CardNo) is null || definition.ManaCost > UnitEventSpecForContext(context)!.Amount
            || !PredictBindingMatches(state, gear.Binding, gear.CardNo, gear.FaceRevision, context.ReflexiveTick.Value, null)) return false;
        // CN420.4 / CN389: only a successfully played armament earns this
        // separate skill. Its original resolved play remains the authority.
        return state.CardPlays?.Resolved.Any(play => play.Source == gear.Binding && play.CardNo == gear.CardNo
            && play.FaceRevision == gear.FaceRevision && play.Tick == context.ReflexiveTick && (play.Types & 2) != 0
            && (controller is null || play.PlayerId == controller)) == true;
    }

    private static ResolutionResult CapturePlayedArmamentAttachment(ResolutionResult result, PendingEffectPlayState pending,
        PlayCardCommand play)
    {
        if (!IsArmamentPlayAttachmentPermission(pending.Parent)
            || !result.State.CardObjects.TryGetValue(play.SourceObjectId, out var gear)
            || !result.Events.Any(ev => ev.Kind is "EQUIPMENT_PLAYED_TO_BASE" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD"
                && Equals(ev.Payload.GetValueOrDefault("equipmentObjectId"), play.SourceObjectId))) return result;
        var context = pending.Parent.UnitEvent! with { ReflexiveTick = result.State.Tick,
            Other = new(new(gear.ObjectId, gear.ObjectGeneration), gear.CardNo!, gear.CopyFaceTransitions?.Count ?? 0) };
        var created = WithUnitEvents(new(PlayedArmamentAttachmentCreatedEvent, "成功打出武装，创建独立的后续贴附技能",
            new Dictionary<string, object?> { ["playerId"] = pending.PlayerId,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, pending.Parent.SourceObjectId, ProjectionViewers.Spectator),
                ["equipmentObjectId"] = gear.ObjectId }),
            [new(pending.Parent.SourceObjectId, pending.PlayerId, context)]);
        return result with { Events = result.Events.Append(created).ToArray() };
    }

    private static StackResolutionResult ResolvePlayedArmamentAttachment(MatchState state, StackItemState item)
    {
        var context = item.UnitEvent!;
        var gear = context.Other!;
        if (!state.CardObjects.TryGetValue(gear.Binding.ObjectId, out var currentGear)
            || currentGear.ObjectGeneration != gear.Binding.Generation
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var host)
            || host.ObjectGeneration != context.SourceGeneration) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        TryAttachEquipment(state, zones, cards, state.ObjectLocations, item.ControllerId, item.SourceObjectId,
            gear.Binding.ObjectId, item.SourceObjectId, "PLAYED_ARMAMENT_FOLLOWUP", events, out var locations);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, Events = events };
    }
}
