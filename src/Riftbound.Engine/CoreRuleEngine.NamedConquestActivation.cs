using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NamedConquestActivationOrigin(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string ParentStackItemId);

public sealed partial class CoreRuleEngine
{
    internal static bool ValidNamedConquestActivation(MatchState state, NamedConquestActivationOrigin origin, string field)
        => origin.Source is { Generation: >= 0 } source && source.ObjectId == field
            && !string.IsNullOrWhiteSpace(origin.ControllerId) && state.Seats.ContainsKey(origin.ControllerId)
            && !string.IsNullOrWhiteSpace(origin.ParentStackItemId)
            && HeldDefinition(origin.CardNo) is { Kind: "ACTIVATE_CONQUEST" }
            && state.CardObjects.TryGetValue(source.ObjectId, out var current) && current.ObjectGeneration >= source.Generation
            && (current.ObjectGeneration > source.Generation || current.CardNo == origin.CardNo);

    private static readonly IReadOnlySet<string> IndependentConquestKinds = new HashSet<string>(StringComparer.Ordinal) {
        TriggerKinds.UnitConquestCreateUnitToken, TriggerKinds.UnitConquestCreateDormantGold,
        TriggerKinds.UnitConquestPlayLowCostGraveyardSpellRecycle,
        TriggerKinds.UnitConquestRecycleFriendlyPlayGraveyardMechanicalUnit };

    // CN383.4.g: activate the current units' own abilities, not a fictitious
    // conquest by their controller, battlefield, legend, or graveyard cards.
    private static IEnumerable<TriggerQueueItemState> CaptureNamedUnitConquestAbilities(
        MatchState state, StackItemState parent, long tick)
    {
        var held = parent.HeldContext!;
        var field = held.BattlefieldObjectId;
        var origin = new NamedConquestActivationOrigin(new(parent.SourceObjectId, held.SourceGeneration),
            held.CardNo, parent.ControllerId, parent.StackItemId);
        foreach (var group in HeldUnitsAt(state, field).GroupBy(id =>
            EffectiveFieldControllerId(state.PlayerZones, id, state.CardObjects[id])).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var player = group.Key; var units = group.ToArray();
            var captures = CaptureConquestSources(state, player, field, units, tick, "BATTLEFIELD_HELD")
                .Concat(CaptureConquestFlow(state, player, field, units, tick, "BATTLEFIELD_HELD"))
                .Concat(CaptureUnitConquestReturns(state, player, field, units, tick, origin))
                .Concat(CaptureConquestRecasts(state, player, field, units, tick, "BATTLEFIELD_HELD"))
                .Concat(CaptureConquestTokens(state, player, field, units, tick, "BATTLEFIELD_HELD"))
                .Concat(CaptureConquestDamage(state, state, player, field, units, tick, "BATTLEFIELD_HELD"))
                .Concat(CaptureGrantedConquestReturns(state, player, field, units, tick, "BATTLEFIELD_HELD"))
                .Concat(CaptureUnitEvents(state.PlayerZones, state.CardObjects, units, "CONQUEST")
                    .Select(o => CaptureUnitEventFacts(state, o))
                    .Select((o, ordinal) => UnitEventTrigger(o, tick, ordinal, "BATTLEFIELD_HELD")));
            foreach (var trigger in captures) yield return trigger;
            foreach (var id in units)
            {
                if (CardResourceKeywordRules.HuntAmountFromTags(state.CardObjects[id].Tags) is > 0 and var hunt)
                    yield return new($"activated-conquest-hunt-{tick}-{id}-{parent.StackItemId}", player, id,
                        "HOLD_EXPERIENCE", "BATTLEFIELD_HELD") {
                        HeldContext = new(state.CardObjects[id].CardNo!, field, "EXPERIENCE", hunt,
                            state.CardObjects[id].ObjectGeneration) };
                if (UnitConquestTriggerSpecRules.TriggersForCard(state.CardObjects[id].CardNo)
                    .Any(spec => UnitConquestTriggerSpecRules.IsSupportedUnitConquestTrigger(spec)
                        && !IndependentConquestKinds.Contains(spec.Kind)))
                    yield return new($"activate-conquest-{tick}-{id}-{parent.StackItemId}", player, id,
                        "HOLD_CONQUEST_UNIT", "BATTLEFIELD_HELD") {
                        HeldContext = new(state.CardObjects[id].CardNo!, field, "CONQUEST_UNIT", 0,
                            state.CardObjects[id].ObjectGeneration) };
            }
        }
    }
}
