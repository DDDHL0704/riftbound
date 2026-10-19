using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record CapturedGraveyardPlay(string CardNo, string ControllerId, ObjectBinding Source, ObjectBinding? Entering = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraveyardPlayContext(string CardNo, string ControllerId, ObjectBinding Source,
    string DeathId, DestroyedUnitFact Destroyed, DestructionAttribution Attribution, int TurnNumber, long CreatedAtTick);

public sealed partial class CoreRuleEngine
{
    internal const string GraveyardPlayEffect = "CAPTURED_GRAVEYARD_UNIT_PLAY";
    private static TriggerSpec? GraveyardPlaySpec(string? cardNo)
        => UnitDestroyedTriggerSpecRules.TryGetTrigger(cardNo, t => t.Kind == TriggerKinds.UnitSpellDestroyPlayFromGraveyard
            && t.Timing == TriggerTimings.UnitDestroyed && t.PlayOriginZone == "GRAVEYARD"
            && t.PlayCount == 1 && t.ManaCost is >= 0 && t.PowerCost == 1 && t.PowerCostTrait == "red", out var spec) ? spec : null;

    private static IReadOnlyList<CapturedGraveyardPlay> CaptureGraveyardPlays(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState dying)
    {
        var captured = new Dictionary<string, CapturedGraveyardPlay>(StringComparer.Ordinal);
        foreach (var (owner, zone) in zones)
            foreach (var id in zone.Graveyard)
                if (cards.TryGetValue(id, out var source) && GraveyardPlaySpec(source.CardNo) is not null && !IsTokenObject(source))
                    captured[id] = new(source.CardNo!, owner, new(id, source.ObjectGeneration));
        // Simultaneous entrants may observe their own and the other deaths. The
        // actual batch below must prove that each candidate really was destroyed.
        foreach (var id in RuleChoices.Value!.CurrentDestructions.Append(dying.ObjectId).Distinct())
            if (cards.TryGetValue(id, out var source) && !IsTokenObject(source) && IsObjectOnField(zones, id))
            {
                var physical = source.AttachmentUnitCopies?.Basis.CardNo ?? source.CardNo;
                if (GraveyardPlaySpec(physical) is null) continue;
                var owner = NonFieldDestinationOwner(zones, source, EffectiveFieldControllerId(zones, id, source));
                captured[id] = new(physical!, owner, new(id, checked(source.ObjectGeneration + 1)), new(id, source.ObjectGeneration));
            }
        return captured.Values.OrderBy(c => c.Source.ObjectId, StringComparer.Ordinal).ToArray();
    }

    private static void QueueGraveyardPlays(MatchState state, CapturedUnitDeath death, IReadOnlyList<CapturedUnitDeath> deaths,
        List<TriggerQueueItemState> queue, List<GameEvent> events)
    {
        if (death.Attribution is not { FromSpell: true } attribution || state.Status != MatchStatuses.InProgress) return;
        foreach (var candidate in death.GraveyardPlays.Where(c => c.ControllerId == attribution.ResponsiblePlayerId))
        {
            if (candidate.Entering is { } entering && !deaths.Any(d => d.Batch == death.Batch
                && d.Unit.ObjectId == entering.ObjectId && d.Unit.Generation == entering.Generation
                && d.Unit.OwnerId == candidate.ControllerId)) continue;
            var context = new GraveyardPlayContext(candidate.CardNo, candidate.ControllerId, candidate.Source,
                death.Id, death.Unit, attribution, death.TurnNumber, state.Tick);
            var trigger = new TriggerQueueItemState(GraveyardPlayTriggerId(context), candidate.ControllerId,
                candidate.Source.ObjectId, GraveyardPlayEffect, "UNIT_DESTROYED") { SourceCardNo = candidate.CardNo, GraveyardPlay = context };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
    }

    internal static string GraveyardPlayTriggerId(GraveyardPlayContext c)
        => $"{c.DeathId}:GRAVEYARD:{c.Source.ObjectId}:{c.Source.Generation}";

    internal static bool ValidGraveyardPlay(MatchState state, GraveyardPlayContext c,
        string? sourceId = null, string? actor = null, string? cardNo = null)
    {
        if (c.Source is not { Generation: >= 0 } || c.Destroyed is not { Generation: >= 0 } d || c.Attribution is null
            || string.IsNullOrEmpty(c.Source.ObjectId) || string.IsNullOrEmpty(d.ObjectId)
            || c.TurnNumber != state.TurnNumber || c.CreatedAtTick <= 0 || c.CreatedAtTick > state.Tick
            || string.IsNullOrWhiteSpace(c.ControllerId) || string.IsNullOrWhiteSpace(d.ControllerId) || string.IsNullOrWhiteSpace(d.OwnerId)
            || c.ControllerId != c.Attribution.ResponsiblePlayerId || !state.Seats.ContainsKey(c.ControllerId)
            || !state.Seats.ContainsKey(d.ControllerId) || !state.Seats.ContainsKey(d.OwnerId)
            || !c.Attribution.FromSpell || !ValidDestructionAttribution(state, c.Attribution)
            || GraveyardPlaySpec(c.CardNo) is null || (sourceId is not null && sourceId != c.Source.ObjectId)
            || (actor is not null && actor != c.ControllerId) || (cardNo is not null && cardNo != c.CardNo)
            || !state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) || source.ObjectGeneration < c.Source.Generation
            || source.OwnerId != c.ControllerId || source.ObjectGeneration == c.Source.Generation
                && (source.CardNo != c.CardNo || !state.PlayerZones[c.ControllerId].Graveyard.Contains(source.ObjectId))
            || (state.CardObjects.TryGetValue(d.ObjectId, out var victim) ? victim.ObjectGeneration <= d.Generation
                : !P6TokenFactoryCatalog.TryGetByCardNo(d.CardNo, out _))) return false;
        var id = Regex.Match(c.DeathId ?? "", @"^death-(0|[1-9][0-9]*)-(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant);
        return id.Success && long.TryParse(id.Groups[1].Value, out var tick) && tick < c.CreatedAtTick;
    }

    private static CardBehaviorDefinition GraveyardPlayBehavior(GraveyardPlayContext c)
        => new(c.CardNo, "不朽凤凰的废牌堆打出技能", 0, GraveyardPlayEffect, 0, 0,
            EffectPlaySourceZone: "GRAVEYARD", EffectPlayIgnoreBaseMana: true, EffectPlayIgnoreBasePower: true);

    private static StackResolutionResult ResolveGraveyardPlay(MatchState state, StackItemState item)
    {
        // The shared play continuation has already moved the card. Object
        // continuity is finalized when this command completes, after this parent.
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        var c = item.GraveyardPlay!;
        if (item.EffectKind != GraveyardPlayEffect || !ValidGraveyardPlay(state, c, item.SourceObjectId, item.ControllerId, item.CardNo))
            throw new InvalidOperationException("Invalid captured graveyard play.");
        var resolved = BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId, item.SourceObjectId,
            item.EffectKind, "UNIT_DESTROYED") { GraveyardPlay = c });
        if (item.TriggerCost is null || !state.PlayerZones[c.ControllerId].Graveyard.Contains(c.Source.ObjectId)
            || !state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) || source.ObjectGeneration != c.Source.Generation)
            return NoopStackResolutionResult(state) with { Events = [resolved] };
        var parent = item with { TargetObjectIds = [c.Source.ObjectId] };
        var pending = BeginEffectPlay(state, parent, GraveyardPlayBehavior(c));
        return pending with { Events = new[] { resolved }.Concat(pending.Events).ToArray() };
    }
}
