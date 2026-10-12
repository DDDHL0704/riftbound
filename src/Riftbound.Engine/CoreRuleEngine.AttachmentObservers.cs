using Riftbound.CardCatalog;
using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record AttachmentObserverContext(string CardNo, long SourceGeneration,
    ObjectBinding Equipment, string EquipmentCardNo, string AttachingPlayerId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool CopiedInstance = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Mode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ObjectBinding>? References = null);
internal sealed record CapturedAttachmentObserver(string SourceId, string ControllerId, AttachmentObserverContext Context);

public sealed partial class CoreRuleEngine
{
    internal const string AttachmentObserverEffect = "CAPTURED_ARMAMENT_ATTACHMENT_OBSERVER";
    private const string CapturedAttachmentEvent = "INTERNAL_ATTACHMENT_OBSERVERS";

    private static TriggerSpec? AttachmentDrawSpec(string? no) => UnitTriggerPaymentSpecRules.TryGetTrigger(no,
        UnitTriggerPaymentSpecRules.IsUnitArmamentAttachedPayDrawTrigger, out var spec) ? spec : null;

    internal static bool ValidAttachmentObserver(AttachmentObserverContext? context, string effect,
        string controller, string source, string? cardNo = null)
        => context is null ? effect != AttachmentObserverEffect
            : effect == AttachmentObserverEffect && context.SourceGeneration >= 0
                && context.Equipment is { Generation: >= 0 } equipment && !string.IsNullOrWhiteSpace(equipment.ObjectId)
                && source != equipment.ObjectId && !string.IsNullOrWhiteSpace(source)
                && controller == context.AttachingPlayerId && !string.IsNullOrWhiteSpace(controller)
                && (cardNo is null || cardNo == context.CardNo) && ValidAttachmentModeShape(context)
                && context.CopiedInstance == (context.Origin is not null)
                && OfficialArmamentCatalog.Find(context.EquipmentCardNo) is not null;

    internal static bool ValidAttachmentGenerations(MatchState state, AttachmentObserverContext? context, string source)
        => context is null || (!state.CardObjects.TryGetValue(source, out var host) || context.SourceGeneration <= host.ObjectGeneration)
            && (!state.CardObjects.TryGetValue(context.Equipment.ObjectId, out var gear) || context.Equipment.Generation <= gear.ObjectGeneration)
            && AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.SourceGeneration, context.CardNo);

    // Capture the abilities present at the attachment event, before a subsequent
    // replacement instruction grants copied text (latest Vendetta FAQ, page 6).
    private static void CaptureAttachmentObservers(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string unitId, string gearId, string actor,
        List<GameEvent> events)
    {
        if (!cards.TryGetValue(unitId, out var unit) || !cards.TryGetValue(gearId, out var gear)
            || unit.IsFaceDown || unit.Tags.Contains(CardObjectTags.Standby) || !unit.Tags.Contains(CardObjectTags.UnitCard)
            || !gear.Tags.Contains(CardEquipmentKeywordNames.Weapon) || !IsObjectOnField(zones, unitId)
            || EffectiveFieldControllerId(zones, unitId, unit) != actor
            || OfficialArmamentCatalog.Find(gear.CardNo) is null) return;
        var observers = AttachedRuleTextRules.Instances(zones, cards, unit)
            .Where(instance => AttachmentDrawSpec(instance.CardNo) is not null || AttachmentModeSpec(instance.CardNo) is not null)
            .Select(instance => new CapturedAttachmentObserver(unitId, actor,
                new(instance.CardNo, unit.ObjectGeneration, new(gearId, gear.ObjectGeneration), gear.CardNo!, actor,
                    instance.Origin, instance.Origin is not null))).ToArray();
        if (observers.Length == 0) return;
        events.Add(new(CapturedAttachmentEvent, "捕获贴附时存在的技能", new Dictionary<string, object?> {
            ["observers"] = observers
        }));
    }

    private static bool TryAttachEquipment(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, IReadOnlyDictionary<string, ObjectLocationState> knownLocations,
        string actor, string sourceId, string gearId, string unitId, string reason, List<GameEvent> events,
        out IReadOnlyDictionary<string, ObjectLocationState> locations,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var nextLocations = ReconcileObjectLocations(knownLocations, zones).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        locations = nextLocations;
        if (!cards.TryGetValue(gearId, out var gear) || gear.IsFaceDown || !gear.Tags.Contains(CardObjectTags.EquipmentCard)
            || !cards.TryGetValue(unitId, out var unit) || unit.IsFaceDown || !unit.Tags.Contains(CardObjectTags.UnitCard)
            || !IsObjectOnField(zones, gearId) || !IsObjectOnField(zones, unitId)
            || !nextLocations.TryGetValue(unitId, out var target) || !nextLocations.TryGetValue(gearId, out var origin)) return false;
        if (gear.AttachedToObjectId == unitId) return false;
        var controller = EffectiveFieldControllerId(zones, gearId, gear);
        using (new MightInstructionScope(state, zones, cards, events, nextLocations))
        {
            if (!string.IsNullOrWhiteSpace(gear.AttachedToObjectId))
                events.Add(new("EQUIPMENT_DETACHED", "武装从原宿主卸除", new Dictionary<string, object?> {
                    ["sourceObjectId"] = sourceId, ["equipmentObjectId"] = gearId,
                    ["unitObjectId"] = gear.AttachedToObjectId, ["controllerId"] = controller,
                    ["ownerId"] = gear.OwnerId, ["reason"] = "REATTACH", ["attachingPlayerId"] = actor }));
            if (origin.Zone != target.Zone || origin.BattlefieldObjectId != target.BattlefieldObjectId)
            {
                foreach (var (player, zone) in zones.ToArray())
                    zones[player] = zone with { Base = zone.Base.Where(id => id != gearId).ToArray(),
                        Battlefields = zone.Battlefields.Where(id => id != gearId).ToArray() };
                var gearZones = zones[controller];
                zones[controller] = target.Zone == "BASE" ? gearZones with { Base = [..gearZones.Base, gearId] }
                    : gearZones with { Battlefields = [..gearZones.Battlefields, gearId] };
            }
            nextLocations[gearId] = new(controller, target.Zone, target.BattlefieldObjectId);
            cards[gearId] = gear with { AttachedToObjectId = unitId };
            var payload = new Dictionary<string, object?> {
                ["sourceObjectId"] = sourceId, ["equipmentObjectId"] = gearId, ["unitObjectId"] = unitId,
                ["controllerId"] = controller, ["ownerId"] = gear.OwnerId, ["attachedToObjectId"] = unitId,
                ["previousAttachedToObjectId"] = gear.AttachedToObjectId,
                ["equipmentCardNo"] = gear.CardNo, ["reason"] = reason, ["attachingPlayerId"] = actor };
            if (metadata is not null) foreach (var pair in metadata) payload[pair.Key] = pair.Value;
            events.Add(new("EQUIPMENT_ATTACHED", "武装贴附至单位", payload));
            CaptureAttachmentObservers(zones, cards, unitId, gearId, actor, events);
        }
        // Vendetta FAQ: attachment is observed first; the subsequent replacement
        // instruction creates one fresh printed text instance for this armament.
        if (AttachedRuleTextRules.CopiesPrintedText(gear.CardNo) && AttachedRuleTextRules.Face(unit) is { } face
            && CopyCharacteristics.HasFace(face))
        {
            using var copiedTextInstruction = new MightInstructionScope(state, zones, cards, events, nextLocations);
            var revision = checked(gear.AttachmentRevision + 1);
            cards[gearId] = cards[gearId] with { AttachmentRevision = revision,
                AttachedRuleText = new(new(unit.ObjectId, unit.ObjectGeneration), face, revision) };
            events.Add(new("RULE_TEXT_COPIED", "贴附后复制单位的印刷技能文字", new Dictionary<string, object?> {
                ["equipmentObjectId"] = gearId, ["unitObjectId"] = unitId, ["sourceCardNo"] = face,
                ["revision"] = revision, ["reason"] = "ATTACH_THEN_COPY_PRINTED_RULE_TEXT" }));
        }
        return true;
    }

    private static ResolutionResult RecordAttachmentObservers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Kind == CapturedAttachmentEvent)) return result;
        var events = new List<GameEvent>(); var queue = result.State.TriggerQueue.ToList();
        for (var index = 0; index < result.Events.Count; index++)
        {
            var ev = result.Events[index];
            if (ev.Kind != CapturedAttachmentEvent) { events.Add(ev); continue; }
            if (result.State.Status != MatchStatuses.InProgress) continue;
            foreach (var captured in (CapturedAttachmentObserver[])ev.Payload["observers"]!)
            {
                long Generation(string id, long generation) => before.CardObjects.TryGetValue(id, out var prior)
                    && !IsObjectOnField(before.PlayerZones, id) && IsObjectOnField(result.State.PlayerZones, id)
                        ? Math.Max(generation, checked(prior.ObjectGeneration + 1)) : generation;
                var context = captured.Context with {
                    SourceGeneration = Generation(captured.SourceId, captured.Context.SourceGeneration),
                    Equipment = captured.Context.Equipment with { Generation = Generation(captured.Context.Equipment.ObjectId, captured.Context.Equipment.Generation) }
                };
                var suffix = context.Origin is { } origin
                    ? $"-ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}" : "";
                var trigger = new TriggerQueueItemState($"attachment-{result.State.Tick}-{index}-{captured.SourceId}-{captured.Context.Equipment.ObjectId}{suffix}",
                    captured.ControllerId, captured.SourceId, AttachmentObserverEffect, "EQUIPMENT_ATTACHED")
                    { AttachmentObserver = context, SourceCardNo = context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveAttachmentObserver(MatchState state, StackItemState item)
    {
        if (AttachmentModeSpec(item.CardNo) is not null) return ResolveAttachmentMode(state, item);
        if (!ValidAttachmentObserver(item.AttachmentObserver, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
            || item.TriggerCost is null) throw new InvalidOperationException("Invalid captured attachment draw trigger.");
        var draw = ResolveLastBreathDrawStackItem(state, item, AttachmentDrawSpec(item.CardNo)!.DrawCount!.Value);
        var context = item.AttachmentObserver!;
        return draw with { Events = draw.Events.Select(e => e.Kind != "TRIGGER_RESOLVED" ? e : e with {
            Payload = new Dictionary<string, object?>(e.Payload) {
                ["trigger"] = RuntimeTriggerEffectKind(AttachmentDrawSpec(context.CardNo)!),
                ["triggeredByEventKind"] = "EQUIPMENT_ATTACHED",
                ["equipmentObjectId"] = context.Equipment.ObjectId, ["equipmentCardNo"] = context.EquipmentCardNo
            } }).ToArray() };
    }

    private static GameEvent AttachmentPaymentOpened(StackItemState item, PendingPaymentState payment)
        => new("PAYMENT_WINDOW_OPENED", "贴附触发：支付费用以确认技能，然后双方可响应", new Dictionary<string, object?> {
            ["paymentId"] = payment.PaymentId, ["paymentWindow"] = payment.PaymentWindow, ["playerId"] = item.ControllerId,
            ["trigger"] = RuntimeTriggerEffectKind(AttachmentDrawSpec(item.CardNo)!), ["sourceObjectId"] = item.SourceObjectId,
            ["equipmentObjectId"] = item.AttachmentObserver!.Equipment.ObjectId, ["equipmentCardNo"] = item.AttachmentObserver.EquipmentCardNo,
            ["mana"] = payment.ManaCost, ["power"] = 0, ["reason"] = RuntimeTriggerEffectKind(AttachmentDrawSpec(item.CardNo)!),
            ["paymentChoices"] = payment.LegalPaymentChoiceIds.ToArray() });
}
