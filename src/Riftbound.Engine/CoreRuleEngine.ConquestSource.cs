using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConquestSourceContext(ObjectBinding Source, string CardNo, string ControllerId, string Kind,
    string BattlefieldId, string Reason, long CapturedTick, int TurnNumber, long FaceRevision, string RuleTextInstanceId,
    bool? SourceWasEmpowered, IReadOnlyList<string>? ConfirmedPlayIds,
    string? SourceTokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    internal const string ConquestSourceEffect = "CAPTURED_UNIT_CONQUEST_SOURCE_ACTION";
    private static TriggerSpec? ConquestSourceSpec(string? cardNo)
        => UnitConquestTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(t =>
            t.Timing == TriggerTimings.UnitConquest && (t.Kind switch {
                TriggerKinds.UnitConquestEmpoweredGainScore or TriggerKinds.UnitConquestPlayedUnitEquipmentSpellGainScore => t.ScoreAmount is > 0,
                TriggerKinds.UnitConquestDrawOne => t.DrawCount is > 0,
                TriggerKinds.UnitConquestGrantSelfBoon => t.BoonCount is > 0,
                TriggerKinds.UnitConquestGainResources => t.ManaDelta is >= 0 && t.GeneratedPower is >= 0
                    && (t.ManaDelta > 0 || t.GeneratedPower > 0),
                _ => false
            }));

    private static IEnumerable<TriggerQueueItemState> CaptureConquestSources(MatchState state, string player,
        string battlefield, IReadOnlyList<string> units, long tick, string reason)
    {
        foreach (var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.IsFaceDown
                || !source.Tags.Contains(CardObjectTags.UnitCard) || !SourceObjectControlledByPlayerOrLegacyOwned(source, player)
                || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != battlefield) continue;
            foreach (var (instance, ordinal) in AttachedRuleTextRules.Instances(state, source).Select((value, ordinal) => (value, ordinal)))
            {
                var spec = ConquestSourceSpec(instance.CardNo);
                if (spec is null || spec.RequiresSourceEmpowered == true && !source.IsEmpowered) continue;
                string[]? playIds = null;
                if (spec.Kind == TriggerKinds.UnitConquestPlayedUnitEquipmentSpellGainScore)
                {
                    var plays = state.CardPlays.TurnNumber == state.TurnNumber
                        ? state.CardPlays.Finalized.Where(p => p.PlayerId == player && p.Tick <= tick && (p.Types & 7) != 0).ToArray()
                        : [];
                    if ((plays.Aggregate(0, (types, play) => types | play.Types) & 7) != 7) continue;
                    playIds = plays.Select(play => play.PlayId).ToArray();
                }
                var context = new ConquestSourceContext(new(id, source.ObjectGeneration), instance.CardNo, player, spec.Kind,
                    battlefield, reason, tick, state.TurnNumber, instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
                    instance.Id, spec.RequiresSourceEmpowered == true ? true : null, playIds, source.TokenFactoryCardNo, instance.Origin);
                var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects, state.ObjectLocations,
                    player, battlefield, reason);
                var binding = MatchStateHasher.HashValue(context);
                for (var i = 0; i < repeats; i++)
                    yield return new TriggerQueueItemState($"conquest-source-{tick}-{binding}-{ordinal}-{i}",
                        player, id, ConquestSourceEffect, reason) { ConquestSource = context, SourceCardNo = instance.CardNo };
            }
        }
    }

    internal static bool ValidConquestSource(MatchState state, ConquestSourceContext? c,
        string effect, string sourceId, string controller, string? cardNo)
    {
        if (c is null) return effect != ConquestSourceEffect;
        var spec = ConquestSourceSpec(c.CardNo);
        if (effect != ConquestSourceEffect || c.CardNo != cardNo || spec is null || spec.Kind != c.Kind
            || c.Source is not { Generation: >= 0 } || c.Source.ObjectId != sourceId
            || c.ControllerId != controller || !state.Seats.ContainsKey(controller)
            || string.IsNullOrWhiteSpace(c.BattlefieldId) || !state.BattlefieldStates.ContainsKey(c.BattlefieldId)
            || c.Reason is not ("BATTLEFIELD_CONQUERED" or "BATTLEFIELD_HELD")
            || c.CapturedTick < 0 || c.CapturedTick > state.Tick || c.TurnNumber != state.TurnNumber || c.FaceRevision < 0
            || (spec.RequiresSourceEmpowered == true ? c.SourceWasEmpowered != true : c.SourceWasEmpowered is not null)) return false;
        if (c.Kind == TriggerKinds.UnitConquestPlayedUnitEquipmentSpellGainScore)
        {
            if (c.ConfirmedPlayIds is not { Count: > 0 } ids || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count
                || state.CardPlays.TurnNumber != c.TurnNumber) return false;
            var entries = ids.Select(id => state.CardPlays.Finalized.FirstOrDefault(p => p.PlayId == id)).ToArray();
            if (entries.Any(p => p is null || p.PlayerId != controller || p.Tick > c.CapturedTick)
                || (entries.Aggregate(0, (types, play) => types | play!.Types) & 7) != 7) return false;
        }
        else if (c.ConfirmedPlayIds is not null) return false;
        return c.Origin is null
            ? c.RuleTextInstanceId == $"PRINTED:{c.Source.ObjectId}:{c.Source.Generation}"
                && PredictBindingMatches(state, c.Source, c.CardNo, c.FaceRevision, c.CapturedTick, c.SourceTokenFactoryCardNo)
            : c.Origin.Equipment is { } equipment
                && c.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{c.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, c.Origin, sourceId, c.Source.Generation, c.CardNo);
    }

    internal static bool ValidConquestSourceTrigger(MatchState state, TriggerQueueItemState trigger)
        => ValidConquestSource(state, trigger.ConquestSource, trigger.EffectKind, trigger.SourceObjectId,
            trigger.ControllerId, trigger.SourceCardNo)
            && (trigger.ConquestSource is null || trigger.TriggeredByEventKind == trigger.ConquestSource.Reason
                && trigger.TriggerId.StartsWith($"conquest-source-{trigger.ConquestSource.CapturedTick}-", StringComparison.Ordinal));

    internal static bool ValidConquestSourceStack(MatchState state, StackItemState item)
        => ValidConquestSource(state, item.ConquestSource, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            && item.ConquestSource?.Kind != TriggerKinds.UnitConquestGainResources
            && (item.ConquestSource is null || item.StackItemId.StartsWith($"ordered-conquest-source-{item.ConquestSource.CapturedTick}-", StringComparison.Ordinal)
                && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
                && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed);

    private static StackResolutionResult ResolveConquestSource(MatchState state, StackItemState item)
    {
        if (!ValidConquestSourceStack(state, item)) throw new InvalidOperationException("Invalid captured conquest source action.");
        var context = item.ConquestSource!; var spec = ConquestSourceSpec(context.CardNo)!;
        var publicSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var events = new List<GameEvent> { new("UNIT_TRIGGER_RESOLVED", "征服触发技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = publicSource,
            ["battlefieldId"] = context.BattlefieldId, ["trigger"] = context.Kind }) };
        if (context.Kind == TriggerKinds.UnitConquestDrawOne)
        {
            var zones = NormalizeZonesForSeats(state);
            var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId,
                spec.DrawCount!.Value, state.RngCursor, events, deferNormalVictory: true);
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores,
                WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        if (context.Kind == TriggerKinds.UnitConquestGrantSelfBoon)
        {
            var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.Source.Generation
                && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, source.ObjectId))
                GrantLegendBoon(cards, source.ObjectId, item.ControllerId, source.ObjectId, context.Kind, events);
            return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
        }
        var scores = NormalizeScoresForSeats(state); var amount = spec.ScoreAmount!.Value;
        scores[item.ControllerId] += amount;
        events.Add(new("SCORE_GAINED", "征服触发技能获得分数", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = publicSource,
            ["amount"] = amount, ["score"] = scores[item.ControllerId], ["reason"] = context.Kind }));
        return NoopStackResolutionResult(state) with { PlayerScores = scores, Events = events };
    }
}
