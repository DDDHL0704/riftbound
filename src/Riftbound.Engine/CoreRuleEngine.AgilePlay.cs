using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentKeywordGrantOrigin(ObjectBinding Source, string CardNo,
    long FaceRevision, RuleTextInstanceBinding? Origin,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TokenFactoryCardNo = null);

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EquipmentKeywordGrantOrigin? AgilePlayGrant { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string AgilePlayTiming = "AGILE_PLAY";
    internal const string AgilePlayAttachEffect = "ATTACH_PLAYED_SOURCE_EQUIPMENT";
    private static readonly UnitEventAbilitySpec AgilePlaySpec = new(AgilePlayTiming, AgilePlayAttachEffect);

    // The text owner is used outside play; field control is used after entry
    // (CN191.4-191.5). The prospective actor is not an off-field controller.
    private static IEnumerable<EquipmentKeywordGrantOrigin> AllPlacesEquipmentGrantOrigins(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState equipment, string keyword)
    {
        if (equipment.IsFaceDown || !equipment.Tags.Contains(CardObjectTags.EquipmentCard)) yield break;
        var controller = IsObjectOnField(zones, equipment.ObjectId)
            ? EffectiveFieldControllerId(zones, equipment.ObjectId, equipment) : equipment.OwnerId;
        foreach (var id in PublicStaticAuraSourceObjectIds(zones).Order(StringComparer.Ordinal))
        {
            if (!cards.TryGetValue(id, out var source) || source.IsFaceDown
                || EffectiveFieldControllerId(zones, id, source) != controller) continue;
            // Active copied/attached text survives loss of the unit type
            // (CN137.3.b); the grant does not impose a unit-source condition.
            foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, source))
                // CN135.4: attached cards' own printed text is inactive;
                // added text, including a bound copy, remains active.
                if ((source.AttachedToObjectId is null || instance.Origin is not null)
                    && EquipmentKeywordGrants.Value.TryGetValue(instance.CardNo, out var grants)
                    && grants.Any(g => g.AllPlaces && g.GrantedKeyword == keyword
                        && equipment.Tags.Contains(g.RequiredKeyword)))
                    yield return new(new(id, source.ObjectGeneration), instance.CardNo,
                        source.CopyFaceTransitions?.Count ?? 0, instance.Origin, source.TokenFactoryCardNo);
        }
    }

    private static IReadOnlyDictionary<string, string[]> AllPlacesEquipmentKeywords(MatchState state)
        => state.CardObjects.Values.Where(card => AllPlacesEquipmentGrantOrigins(state.PlayerZones,
                state.CardObjects, card, CardEquipmentKeywordNames.Agile).Any())
            .ToDictionary(card => card.ObjectId, _ => new[] { CardEquipmentKeywordNames.Agile }, StringComparer.Ordinal);

    private static UnitEventObservation[] CaptureGrantedAgilePlay(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState equipment, string player)
    {
        // CN819.2: printed Agile and one or more granted instances produce a
        // single skill. Existing printed representatives already earn theirs.
        if (equipment.IsFaceDown || !equipment.Tags.Contains(CardObjectTags.EquipmentCard)
            || !IsObjectOnField(zones, equipment.ObjectId)
            || EffectiveFieldControllerId(zones, equipment.ObjectId, equipment) != player
            || CardBehaviorRegistry.TryGetByCardNo(equipment.CardNo ?? "", out var behavior)
                && IsAgileDirectPlayAttachRepresentative(behavior)
            || AllPlacesEquipmentGrantOrigins(zones, cards, equipment, CardEquipmentKeywordNames.Agile)
                .FirstOrDefault() is not { } grant) return [];
        return [new(equipment.ObjectId, player,
            new(equipment.CardNo!, equipment.ObjectGeneration, equipment.CopyFaceTransitions?.Count ?? 0,
                0, AgilePlayTiming, equipment.TokenFactoryCardNo) { AgilePlayGrant = grant })];
    }

    private static bool ValidAgilePlayGrant(MatchState state, UnitEventContext context, string source, string? controller)
    {
        if (context.Timing != AgilePlayTiming) return context.AgilePlayGrant is null;
        if (context.AgilePlayGrant is not { Source: { Generation: >= 0 } binding, FaceRevision: >= 0 } grant
            || string.IsNullOrWhiteSpace(binding.ObjectId) || string.IsNullOrWhiteSpace(grant.CardNo)
            || string.IsNullOrWhiteSpace(context.CardNo)
            || !EquipmentKeywordGrants.Value.TryGetValue(grant.CardNo, out var definitions)
            || !definitions.Any(g => g.AllPlaces && g.RequiredKeyword == CardEquipmentKeywordNames.Weapon
                && g.GrantedKeyword == CardEquipmentKeywordNames.Agile)
            || !CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var gear)
            || !gear.PlaysSourceToBaseAsEquipment || OfficialArmamentCatalog.Find(context.CardNo) is null
                && !ParseDelimitedValues(gear.SourceEquipmentTags).Contains(CardEquipmentKeywordNames.Weapon)
            || (grant.Origin is { } origin
                ? !AttachedRuleTextRules.ValidCaptured(state, origin, binding.ObjectId, binding.Generation, grant.CardNo)
                : !PredictBindingMatches(state, binding, grant.CardNo, grant.FaceRevision, context.CapturedTick, grant.TokenFactoryCardNo))) return false;
        // Entry and the original resolved play authenticate the new equipment
        // incarnation. The immutable accepted observation also authenticates
        // the entitlement and its retirement, even after either source leaves.
        return state.CardPlays.Resolved.Any(play => play.Source == new ObjectBinding(source, context.SourceGeneration)
            && play.CardNo == context.CardNo && play.FaceRevision == context.FaceRevision
            && play.Tick == context.CapturedTick && (play.Types & 2) != 0
            && (controller is null || play.PlayerId == controller));
    }

    private static StackResolutionResult ResolveAgilePlayAttachment(MatchState state, StackItemState item)
    {
        if (item.TargetObjectIds.SingleOrDefault() is not { } host
            || !StackObjectRules.ReferencesCurrentObject(state, item, host)
            || !UnitEventTargets(state, item).Contains(host)
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var gear)
            || gear.ObjectGeneration != item.UnitEvent!.SourceGeneration
            || gear.IsFaceDown || !gear.Tags.Contains(CardObjectTags.EquipmentCard)
            || !IsObjectOnField(state.PlayerZones, gear.ObjectId)) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        TryAttachEquipment(state, zones, cards, state.ObjectLocations, item.ControllerId, gear.ObjectId,
            gear.ObjectId, host, "AGILE_DIRECT_PLAY_ATTACH", events, out var locations);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, Events = events };
    }
}
