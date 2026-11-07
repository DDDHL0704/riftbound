using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NamedConquestActivationOrigin(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string ParentStackItemId);

internal sealed record NamedConquestCaptureInfo(NamedConquestActivationOrigin? Activation,
    long SourceGeneration, string CardNo, string BattlefieldId, string ContextHash, long? CapturedTick = null);

public sealed partial class CoreRuleEngine
{
    internal static string ConquestCaptureReason(NamedConquestActivationOrigin? activation)
        => activation is null ? "BATTLEFIELD_CONQUERED" : "BATTLEFIELD_HELD";

    internal static NamedConquestCaptureInfo? NamedConquestCaptureOf(ConquestTokenContext? token,
        UnitConquestContext? unit, RecastTriggerContext? recast)
        => token is not null ? new(token.Activation, token.SourceGeneration, token.CardNo, token.BattlefieldId, MatchStateHasher.HashValue(token), token.CapturedTick)
            : unit is not null ? new(unit.Activation, unit.SourceGeneration, unit.CardNo, unit.BattlefieldId, MatchStateHasher.HashValue(unit))
            : recast is not null ? new(recast.Activation, recast.SourceGeneration, recast.CardNo, recast.BattlefieldId, MatchStateHasher.HashValue(recast)) : null;

    private static void AddNamedConquestActivationPayload(Dictionary<string, object?> payload,
        NamedConquestActivationOrigin activation)
    {
        payload["activationStackItemId"] = activation.ParentStackItemId;
        payload["activationSourceObjectId"] = activation.Source.ObjectId;
        payload["activationSourceGeneration"] = activation.Source.Generation;
        payload["activationSourceCardNo"] = activation.CardNo;
        payload["activationControllerId"] = activation.ControllerId;
    }

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

    private sealed record NamedConquestCaptures(IReadOnlyList<TriggerQueueItemState> Triggers,
        IReadOnlyList<DelayedResourceGain> DelayedResourceGains, IReadOnlyList<GameEvent> AdditionalEvents);

    // CN383.4.g: activate the current units' own abilities, retaining all
    // conditions beyond the named timing. This does not create a conquest.
    private static NamedConquestCaptures CaptureNamedUnitConquestAbilities(
        MatchState state, StackItemState parent, long tick)
    {
        var held = parent.HeldContext!;
        var field = held.BattlefieldObjectId;
        var origin = new NamedConquestActivationOrigin(new(parent.SourceObjectId, held.SourceGeneration),
            held.CardNo, parent.ControllerId, parent.StackItemId);
        var triggers = new List<TriggerQueueItemState>();
        var extraEvents = new List<GameEvent>();
        var current = state;
        foreach (var group in HeldUnitsAt(state, field).GroupBy(id =>
            EffectiveFieldControllerId(state.PlayerZones, id, state.CardObjects[id])).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var player = group.Key; var units = group.ToArray();
            triggers.AddRange(CaptureCurrentUnitConquestAbilities(state, player, field, units, tick,
                "BATTLEFIELD_HELD", origin));
            var captureEvents = new List<GameEvent>();
            var withAliases = QueueHeldAliasesForConquest(current, player, field, units, captureEvents, origin);
            triggers.AddRange(withAliases.TriggerQueue.Skip(current.TriggerQueue.Count));
            extraEvents.AddRange(captureEvents.Where(e => e.Kind == "DELAYED_ABILITY_CREATED"));
            current = withAliases;
        }
        return new(triggers, current.DelayedResourceGains, extraEvents);
    }

    private static IEnumerable<TriggerQueueItemState> CaptureCurrentUnitConquestAbilities(MatchState state,
        string player, string field, IReadOnlyList<string> units, long tick, string eventKind,
        NamedConquestActivationOrigin? activation = null, bool includeHunt = true)
    {
        var captures = CaptureConquestSources(state, player, field, units, tick, eventKind)
            .Concat(CaptureConquestFlow(state, player, field, units, tick, eventKind))
            .Concat(CaptureUnitConquestReturns(state, player, field, units, tick, activation, eventKind))
            .Concat(CaptureConquestRecasts(state, player, field, units, tick, activation, eventKind))
            .Concat(CaptureConquestTokens(state, player, field, units, tick, activation, eventKind))
            .Concat(CaptureConquestDamage(state, state, player, field, units, tick, activation, eventKind))
            .Concat(CaptureGrantedConquestReturns(state, player, field, units, tick, activation, eventKind))
            .Concat(CaptureConquestEventObservations(state, units)
                .Where(o => includeHunt || !UnitEventTimingMatches(o.Context.Timing, "HOLD"))
                .Select(o => CaptureUnitEventFacts(state, o))
                .Select((o, ordinal) => UnitEventTrigger(o, tick, ordinal, eventKind)));
        foreach (var trigger in captures) yield return trigger;
        foreach (var id in units)
        {
            if (includeHunt && CardResourceKeywordRules.HuntAmountFromTags(state.CardObjects[id].Tags) is > 0 and var hunt)
                yield return new($"activated-conquest-hunt-{tick}-{id}-{activation?.ParentStackItemId}", player, id,
                    "HOLD_EXPERIENCE", eventKind) {
                    HeldContext = new(state.CardObjects[id].CardNo!, field, "EXPERIENCE", hunt,
                        state.CardObjects[id].ObjectGeneration) };
            if (UnitConquestTriggerSpecRules.TriggersForCard(state.CardObjects[id].CardNo)
                .Any(spec => UnitConquestTriggerSpecRules.IsSupportedUnitConquestTrigger(spec)
                    && !IndependentConquestKinds.Contains(spec.Kind)
                    && !CapturedConquestInstructionKinds.Contains(spec.Kind)
                    && spec.RequiredOverkillDamage is null or <= 0))
                yield return new($"activate-conquest-{tick}-{id}-{activation?.ParentStackItemId}", player, id,
                    "HOLD_CONQUEST_UNIT", eventKind) {
                    HeldContext = new(state.CardObjects[id].CardNo!, field, "CONQUEST_UNIT", 0,
                        state.CardObjects[id].ObjectGeneration) };
        }
    }
}
