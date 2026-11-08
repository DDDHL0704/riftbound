using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EquipmentSkillTriggerHash { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string EquipmentSkillCompletedTiming = "FRIENDLY_EQUIPMENT_SKILL_RESOLVED";
    private const string EquipmentCompletionCaptureKey = "internalEquipmentSkillCompletionCaptured";

    internal static bool EquipmentSkillObservationShape(UnitEventContext context)
        => context.Timing == EquipmentSkillCompletedTiming
            ? context.EquipmentSkillTriggerHash is { Length: 64 } hash
                && hash.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f')
            : context.EquipmentSkillTriggerHash is null;

    // Bind immutable observation facts. Mode/target confirmation can change
    // after queue admission, so it is not part of this event receipt.
    internal static string EquipmentSkillObservationHash(UnitEventContext context, string source, string controller)
        => MatchStateHasher.HashValue(new { source, controller, context.CardNo, context.SourceGeneration,
            context.FaceRevision, context.CapturedTick, context.Origin, context.TokenFactoryCardNo,
            context.Timing, context.EquipmentSkillTriggerHash });

    // Same completion boundary as the existing CN377.2.a high-cost observer:
    // after instructions, before post-resolution state cleanup.
    private static UnitEventObservation[] CaptureCompletedEquipmentSkill(MatchState before,
        StackItemState item, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards)
    {
        if ((item.Equip is null && !item.StackItemId.StartsWith("STACK-", StringComparison.Ordinal))
            || item.SourceConfirmed || !StackObjectRules.IsAbility(item)
            || item.UnitEvent is not null || item.Tempered is not null
            || !EquipmentSkillSource(before, item)) return [];
        return CaptureEquipmentSkillObservers(zones, cards, item.ControllerId);
    }

    private static bool EquipmentSkillSource(MatchState state, StackItemState item)
    {
        if (state.CardObjects.TryGetValue(item.SourceObjectId, out var source))
            return source.Tags.Contains(CardObjectTags.EquipmentCard);
        // A token that ceased when paying its own instruction cost still has
        // the typed original source receipt; do not fabricate a live card.
        var no = item.SimpleActivation?.CeasedSource?.TokenFactoryCardNo
            ?? item.NativeUnitActivation?.CeasedSource?.TokenFactoryCardNo;
        return no is not null && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory)
            && factory.Tags.Contains(CardObjectTags.EquipmentCard);
    }

    private static UnitEventObservation[] CaptureEquipmentSkillObservers(
        IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string player)
        => CaptureUnitEvents(zones, cards, cards.Values.Where(card =>
            EffectiveFieldControllerId(zones, card.ObjectId, card) == player)
            .Select(card => card.ObjectId), EquipmentSkillCompletedTiming);

    private static GameEvent WithEquipmentSkillCompletion(GameEvent ev,
        IReadOnlyList<UnitEventObservation> observations)
        => WithUnitEvents(ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [EquipmentCompletionCaptureKey] = true } }, observations);

    private static ResolutionResult RecordEquipmentSkillCompletions(MatchState before,
        ResolutionResult result, int eventOffset)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToArray();
        for (var index = eventOffset; index < events.Length; index++)
        {
            var ev = events[index];
            if (ev.Payload.ContainsKey(EquipmentCompletionCaptureKey))
            {
                events[index] = ev with { Payload = ev.Payload.Where(pair =>
                    pair.Key != EquipmentCompletionCaptureKey).ToDictionary(pair => pair.Key, pair => pair.Value) };
                continue;
            }
            UnitEventObservation[] observations = [];
            if (ev.Kind == "STACK_ITEM_RESOLVED"
                && ev.Payload.GetValueOrDefault("stackItemId") is string id
                && before.StackItems.Concat(before.PendingEffectPlay?.Parent is { } parent ? [parent] : [])
                    .FirstOrDefault(item => item.StackItemId == id) is { } finished
                && result.State.StackItems.All(item => item.StackItemId != id)
                && result.State.PendingEffectPlay?.Parent?.StackItemId != id)
                observations = CaptureCompletedEquipmentSkill(before, finished,
                    result.State.PlayerZones, result.State.CardObjects);
            if (ev.Kind is "ABILITY_ACTIVATED" or "LEGEND_ABILITY_ACTIVATED"
                && ev.Payload.GetValueOrDefault("sourceObjectId") is string sourceId
                && ev.Payload.GetValueOrDefault("abilityId") is string abilityId
                && before.CardObjects.TryGetValue(sourceId, out var source)
                && source.Tags.Contains(CardObjectTags.EquipmentCard)
                && ev.Payload.GetValueOrDefault("playerId") is string player
                && (P4ActivatedAbilityCatalog.TryGetByAbilityId(abilityId, out var ability) && ability.IsResourceSkill
                    || TryGetLegendAbility(abilityId, out var legend) && legend.TimingKind == LegendAbilityTimingKinds.ResourceAction))
                observations = CaptureEquipmentSkillObservers(result.State.PlayerZones, result.State.CardObjects, player);
            if (result.State.Status == MatchStatuses.InProgress && observations.Length > 0)
                events[index] = WithUnitEvents(ev, observations);
        }
        return result with { Events = events };
    }
}
