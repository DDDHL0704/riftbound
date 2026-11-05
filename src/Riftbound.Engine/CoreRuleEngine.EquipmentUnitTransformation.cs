using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string EquipmentUnitTransformationEffect = "EQUIPMENT_AS_UNIT_UNTIL_NEXT_SELECTION";
    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> MainPhaseLegendEvents = new(() =>
        RadiantCardSources.GetAdmittedCards().Where(card => card.CardEffect ==
            "在你的主阶段开始时，你可以选择支付{{1}}，以此选择一件友方装备。该友方装备变为一名战力等同于其法力费用的单位装备，直到你用我选择另一件友方装备为止。")
            .ToDictionary(card => card.CardNo, _ => new UnitEventAbilitySpec("MAIN", EquipmentUnitTransformationEffect,
                LeadingManaCost: 1, Optional: true), StringComparer.Ordinal));

    private static UnitEventAbilitySpec? MainPhaseLegendEventSpec(string? cardNo)
        => cardNo is not null ? MainPhaseLegendEvents.Value.GetValueOrDefault(cardNo) : null;

    internal static IEnumerable<LegendEquipmentUnitState> EquipmentUnitTransformations(CardObjectState card)
        => card.LegendEquipmentUnit is { } latest
            ? (latest.EarlierEffects ?? []).Append(latest) : [];

    private static LegendEquipmentUnitState? LatestEquipmentUnitTransformation(IEnumerable<LegendEquipmentUnitState> effects)
    {
        var ordered = effects.Select(effect => effect with { EarlierEffects = null }).OrderBy(effect => effect.AppliedTick).ToArray();
        return ordered.Length == 0 ? null : ordered[^1] with {
            EarlierEffects = ordered.Length > 1 ? ordered[..^1] : null };
    }

    // Keep the printed/materialized Might intact. The continuous base-Might
    // change composes with boons, temporary modifiers, auras and later setters.
    private static int EquipmentUnitTransformationMightDelta(CardObjectState card)
        => card.LegendEquipmentUnit is null ? 0
            : card.CardNo is not null && P6TokenFactoryCatalog.TryGetByCardNo(card.CardNo, out var token)
                ? card.ManaCost - token.DefaultPower
                : PrintedCardFactory.TryCreate(card.ObjectId, card.OwnerId ?? "", card.CardNo, out var printed)
                    ? card.ManaCost - printed.Power : 0;

    internal static bool ValidLegendEquipmentUnit(MatchState state, CardObjectState card)
    {
        if (card.LegendEquipmentUnit is not { } transform) return true;
        if (!IsObjectOnField(state.PlayerZones, card.ObjectId)
            || !card.Tags.Contains(CardObjectTags.EquipmentCard) || !card.Tags.Contains(CardObjectTags.UnitCard)
            || transform.EarlierEffects is { Count: 0 }
            || transform.EarlierEffects?.Any(earlier => earlier is null || earlier.EarlierEffects is not null
                || earlier.AppliedTick >= transform.AppliedTick) == true) return false;
        var effects = EquipmentUnitTransformations(card).ToArray();
        return effects.Select(effect => effect.SourceLegend).Distinct().Count() == effects.Length
            && effects.All(effect => effect.SourceLegend is { Generation: >= 0, ObjectId.Length: > 0 }
                && MainPhaseLegendEventSpec(effect.SourceCardNo) is not null
                && effect.SourceFaceRevision >= 0 && effect.CapturedTick > 0 && effect.CapturedTick <= state.Tick
                && effect.AppliedTick >= effect.CapturedTick && effect.AppliedTick <= state.Tick
                && effect.TargetGeneration == card.ObjectGeneration
                && PredictBindingMatches(state, effect.SourceLegend, effect.SourceCardNo,
                    effect.SourceFaceRevision, effect.CapturedTick, null)
                && !state.CardObjects.Values.Any(other => other.ObjectId != card.ObjectId
                    && EquipmentUnitTransformations(other).Any(otherEffect => otherEffect is not null
                        && otherEffect.SourceLegend == effect.SourceLegend)));
    }

    // The duration ends when the leading instruction is paid and another
    // equipment is selected, before opponents respond to the new ability.
    private static MatchState EndPreviousEquipmentUnitTransformation(MatchState state, StackItemState item,
        List<GameEvent> events)
    {
        if (item.UnitEvent is not { } context || UnitEventSpec(context.CardNo)?.Effect != EquipmentUnitTransformationEffect
            || item.TriggerCost is null || item.TargetObjectIds.SingleOrDefault() is not { } selected) return state;
        var source = new ObjectBinding(item.SourceObjectId, context.SourceGeneration);
        var cards = state.CardObjects.ToDictionary();
        var previousUnits = cards.Values.Where(card => card.ObjectId != selected
            && EquipmentUnitTransformations(card).Any(effect => effect.SourceLegend == source)).ToArray();
        if (previousUnits.Length == 0) return state;
        var zones = NormalizeZonesForSeats(state);
        using (new MightInstructionScope(state, zones, cards, events))
        {
            foreach (var previous in previousUnits)
            {
                var face = AttachedRuleTextRules.Face(previous);
                IReadOnlyList<string> printedTags = face is not null && P6TokenFactoryCatalog.TryGetByCardNo(face, out var token)
                    ? token.Tags
                    : PrintedCardFactory.TryCreate(previous.ObjectId, previous.OwnerId ?? "", face, out var printed)
                        ? printed.Tags : [];
                var remaining = LatestEquipmentUnitTransformation(EquipmentUnitTransformations(previous)
                    .Where(effect => effect.SourceLegend != source));
                // Core 479.1/479.2: a copy may have replaced the equipment face.
                // When its last transformation ends, both types follow that face.
                cards[previous.ObjectId] = previous with { LegendEquipmentUnit = remaining,
                    Tags = previous.Tags.Where(tag => remaining is not null
                        || tag is not (CardObjectTags.UnitCard or CardObjectTags.EquipmentCard)
                        || printedTags.Contains(tag) || previous.GrantedTags?.Contains(tag) == true).ToArray() };
                events.Add(new("LEGEND_EQUIPMENT_UNIT_ENDED", "选择另一件装备，此传奇对原装备的单位转换结束",
                    new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = previous.ObjectId }));
            }
        }
        // CN 137.3.b / 323.6: the old duration ends during confirmation.
        // Run the same cleanup as a completed instruction before responses,
        // including equipment recall, dependent lethal damage and observers.
        var changed = state with { PlayerZones = zones, CardObjects = cards };
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var cleanup = RunStateBasedCleanupLoop(changed, zones, cards, item, state.RunePools,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return changed with { ObjectLocations = ReconcileObjectLocations(locations, zones),
            RunePools = cleanup.RunePools, TriggerQueue = state.TriggerQueue.Concat(cleanup.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn,
                cleanup.DestroyedUnitOwnerIds) };
    }

    private static StackResolutionResult ResolveEquipmentUnitTransformation(MatchState state, StackItemState item)
    {
        var context = item.UnitEvent!;
        if (item.TriggerCost is null || item.TargetObjectIds.SingleOrDefault() is not { } id
            || !StackObjectRules.ReferencesCurrentObject(state, item, id) || !UnitEventTargets(state, item).Contains(id))
            return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary();
        var target = cards[id];
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent>();
        using (new MightInstructionScope(state, zones, cards, events))
        {
            if (target.AttachedToObjectId is { } host)
                events.Add(new("EQUIPMENT_DETACHED", "装备成为单位，从原宿主卸除",
                    new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId,
                        ["equipmentObjectId"] = id, ["unitObjectId"] = host,
                        ["controllerId"] = target.ControllerId, ["ownerId"] = target.OwnerId,
                        ["reason"] = "BECAME_UNIT" }));
            var source = new ObjectBinding(item.SourceObjectId, context.SourceGeneration);
            var effects = EquipmentUnitTransformations(target).Where(effect => effect.SourceLegend != source)
                .Append(new(source, context.CardNo, context.FaceRevision, context.CapturedTick,
                    target.ObjectGeneration, state.Tick + 1));
            cards[id] = target with { AttachedToObjectId = null, AttachedRuleText = null,
                Tags = target.Tags.Append(CardObjectTags.UnitCard).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                LegendEquipmentUnit = LatestEquipmentUnitTransformation(effects) };
            events.Add(new("LEGEND_EQUIPMENT_UNIT_STARTED", "装备变为单位装备",
                    new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                        ["targetGeneration"] = target.ObjectGeneration,
                        ["power"] = target.ManaCost, ["manaCost"] = target.ManaCost }));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }
}
