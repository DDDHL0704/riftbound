using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record EmpoweredTransitionContext(ObjectBinding Source, string ControllerId, long CapturedTick, long FaceRevision, string RuleTextInstanceId)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CeasedEmpoweredSource? CeasedSource { get; init; }
}
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record CeasedEmpoweredSource(long Tick, string CardNo);

public sealed partial class CoreRuleEngine
{
    internal const string EmpoweredBanishKind = "BECAME_EMPOWERED_BANISH";
    private const string EmpoweredBanishCaptureKey = "empoweredBanishSources";
    private sealed record CapturedEmpoweredBanish(string InstanceId, FieldTriggerContext Context);
    private static readonly Lazy<IReadOnlyDictionary<string, int>> EmpoweredBanishFaces = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Select(card => (card.CardNo, Match: Regex.Match(card.CardEffect,
                @"(?:^|\n)当我变为\{\{已强化\}\}时，放逐战场上一名不高于(?<might>[0-9]+)\{\{S\}\}的敌方单位。(?:$|\r?\n)")))
            .Where(row => row.Match.Success).ToDictionary(row => row.CardNo, row => int.Parse(row.Match.Groups["might"].Value)));

    private static CapturedEmpoweredBanish[] CaptureEmpoweredBanish(MatchState state,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState source)
    {
        if (source.IsFaceDown || !source.Tags.Contains(CardObjectTags.UnitCard)) return [];
        var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source);
        return AttachedRuleTextRules.Instances(state.PlayerZones, cards, source)
            .Where(instance => EmpoweredBanishFaces.Value.ContainsKey(instance.CardNo))
            .Select(instance => new CapturedEmpoweredBanish(instance.Id,
                new(instance.CardNo, EmpoweredBanishKind, source.ObjectGeneration) {
                    Origin = instance.Origin, SourceTokenFactoryCardNo = source.TokenFactoryCardNo,
                    EmpoweredTransition = new(new(source.ObjectId, source.ObjectGeneration), controller, 0,
                        source.CopyFaceTransitions?.Count ?? 0, instance.Id) })).ToArray();
    }

    private static ResolutionResult QueueEmpoweredBanish(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = new List<GameEvent>(); var found = false;
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index))) {
            events.Add(ev with { Payload = ev.Payload.Where(p => p.Key != EmpoweredBanishCaptureKey).ToDictionary(p => p.Key, p => p.Value) });
            if (ev.Kind != "OBJECT_EMPOWERED" || ev.Payload.GetValueOrDefault(EmpoweredBanishCaptureKey) is not CapturedEmpoweredBanish[] sources) continue;
            found = true;
            if (result.State.Status != MatchStatuses.InProgress) continue;
            foreach (var (source, ordinal) in sources.Select((source, ordinal) => (source, ordinal))) {
                var birth = source.Context.EmpoweredTransition! with { CapturedTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick };
                var context = source.Context with { EmpoweredTransition = birth };
                var trigger = new TriggerQueueItemState($"empowered-banish-{birth.CapturedTick}-{index}-{ordinal}-{source.InstanceId}",
                    birth.ControllerId, birth.Source.ObjectId, FieldTriggerEffect, "OBJECT_EMPOWERED") {
                    FieldContext = context, SourceCardNo = context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (!found) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidEmpoweredBanishShape(FieldTriggerContext context, string effect, string? cardNo)
        => context.Kind == EmpoweredBanishKind && effect == FieldTriggerEffect
            && (cardNo is null || cardNo == context.CardNo) && context.SourceGeneration >= 0
            && !string.IsNullOrWhiteSpace(context.CardNo) && EmpoweredBanishFaces.Value.ContainsKey(context.CardNo)
            && context.EmpoweredTransition is { Source: { Generation: >= 0 } source, CapturedTick: >= 0, FaceRevision: >= 0 } birth
            && !string.IsNullOrWhiteSpace(source.ObjectId) && source.Generation == context.SourceGeneration
            && !string.IsNullOrWhiteSpace(birth.ControllerId)
            && context.BattlefieldId is null && context.ReturnFocusPlayerId is null && context.SourceLocation is null
            && context.RecycledBoon is null && context.AffectedUnit is null && !context.AffectedUnitCeasedToExist
            && (context.Origin is null ? birth.RuleTextInstanceId == $"PRINTED:{source.ObjectId}:{source.Generation}"
                : context.Origin.Unit == source && context.Origin.CardNo == context.CardNo && context.Origin.Equipment is not null
                    && context.Origin.CapturedTick <= birth.CapturedTick && context.Origin.FaceRevision <= birth.FaceRevision
                    && birth.RuleTextInstanceId == $"ATTACHED:{context.Origin.Equipment.ObjectId}:{context.Origin.Equipment.Generation}:{context.Origin.Revision}")
            && (context.SourceTokenFactoryCardNo is null
                ? !context.SourceCeasedToExist && birth.CeasedSource is null
                : P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                    && token.Tags.Contains(CardObjectTags.UnitCard)
                    && (context.SourceCeasedToExist ? birth.CeasedSource is { Tick: >= 0 } : birth.CeasedSource is null));

    private static bool ValidEmpoweredBanishSource(MatchState state, FieldTriggerContext context, string source, string controller)
    {
        var birth = context.EmpoweredTransition!;
        return birth.Source == new ObjectBinding(source, context.SourceGeneration) && birth.ControllerId == controller
            && state.Seats.ContainsKey(controller) && birth.CapturedTick <= state.Tick
            && (context.SourceCeasedToExist
                ? !state.CardObjects.ContainsKey(source) && birth.CeasedSource is { } ceased
                    && ceased.Tick >= birth.CapturedTick && ceased.Tick <= state.Tick && ceased.CardNo == context.CardNo
                : state.CardObjects.TryGetValue(source, out var current) && current.ObjectGeneration >= context.SourceGeneration
                    && current.TokenFactoryCardNo == context.SourceTokenFactoryCardNo)
            && (context.Origin is null
                ? PredictBindingMatches(state, birth.Source, context.CardNo, birth.FaceRevision, birth.CapturedTick, context.SourceTokenFactoryCardNo)
                : AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.SourceGeneration, context.CardNo));
    }

    private static bool IsEmpoweredBanishTarget(MatchState state, StackItemState item, CardObjectState card)
        => EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) != item.ControllerId
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var location) && location.Zone == "BATTLEFIELD"
            && ResolveCurrentFieldUnitPower(state, card) <= EmpoweredBanishFaces.Value[item.FieldContext!.CardNo];

    private static StackResolutionResult ResolveEmpoweredBanish(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        var target = item.TargetObjectIds.SingleOrDefault();
        if (target is not null && ValidFieldContextState(state, item.FieldContext!, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            && item.TargetGenerations?.GetValueOrDefault(target, -1) == cards.GetValueOrDefault(target)?.ObjectGeneration
            && FieldTriggerTargets(state, item).Contains(target, StringComparer.Ordinal)
            && TryRemoveFieldTargetCore(zones, cards, target, true, out var removal)) {
            // Banish is an ordinary departure, not destruction or a destruction replacement (CN 428.2).
            events.Add(new("UNIT_BANISHED", "放逐目标单位", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target,
                ["playerId"] = removal.OwnerPlayerId, ["destinationZone"] = removal.DestinationZone,
                ["detachedEquipmentObjectIds"] = removal.DetachedEquipmentObjectIds.ToArray() }));
        }
        events.Add(new("TRIGGER_RESOLVED", "触发技能结算完成", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
