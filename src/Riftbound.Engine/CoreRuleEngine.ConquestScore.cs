using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConquestScoreContext(ObjectBinding Source, string CardNo, string ControllerId, string Kind,
    string BattlefieldId, string Reason, long CapturedTick, int TurnNumber, long FaceRevision, string RuleTextInstanceId,
    bool? SourceWasEmpowered, IReadOnlyList<string>? ConfirmedPlayIds,
    string? SourceTokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    internal const string ConquestScoreEffect = "CAPTURED_UNIT_CONQUEST_SCORE";
    private static TriggerSpec? ConquestScoreSpec(string? cardNo)
        => UnitConquestTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(t =>
            t.Timing == TriggerTimings.UnitConquest && t.ScoreAmount is > 0
            && t.Kind is TriggerKinds.UnitConquestEmpoweredGainScore or TriggerKinds.UnitConquestPlayedUnitEquipmentSpellGainScore);

    private static IEnumerable<TriggerQueueItemState> CaptureConquestScores(MatchState state, string player,
        string battlefield, IReadOnlyList<string> units, long tick, string reason)
    {
        foreach (var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.IsFaceDown
                || !source.Tags.Contains(CardObjectTags.UnitCard) || !SourceObjectControlledByPlayerOrLegacyOwned(source, player)
                || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != battlefield) continue;
            foreach (var (instance, ordinal) in AttachedRuleTextRules.Instances(state, source).Select((value, ordinal) => (value, ordinal)))
            {
                var spec = ConquestScoreSpec(instance.CardNo);
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
                var context = new ConquestScoreContext(new(id, source.ObjectGeneration), instance.CardNo, player, spec.Kind,
                    battlefield, reason, tick, state.TurnNumber, instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
                    instance.Id, spec.RequiresSourceEmpowered == true ? true : null, playIds, source.TokenFactoryCardNo, instance.Origin);
                var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects, state.ObjectLocations,
                    player, battlefield, reason);
                for (var i = 0; i < repeats; i++)
                    yield return new TriggerQueueItemState($"conquest-score-{tick}-{battlefield}-{id}-{ordinal}-{i}",
                        player, id, ConquestScoreEffect, reason) { ConquestScore = context, SourceCardNo = instance.CardNo };
            }
        }
    }

    internal static bool ValidConquestScore(MatchState state, ConquestScoreContext? c,
        string effect, string sourceId, string controller, string? cardNo)
    {
        if (c is null) return effect != ConquestScoreEffect;
        var spec = ConquestScoreSpec(c.CardNo);
        if (effect != ConquestScoreEffect || c.CardNo != cardNo || spec is null || spec.Kind != c.Kind
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

    internal static bool ValidConquestScoreTrigger(MatchState state, TriggerQueueItemState trigger)
        => ValidConquestScore(state, trigger.ConquestScore, trigger.EffectKind, trigger.SourceObjectId,
            trigger.ControllerId, trigger.SourceCardNo)
            && (trigger.ConquestScore is null || trigger.TriggeredByEventKind == trigger.ConquestScore.Reason
                && trigger.TriggerId.StartsWith($"conquest-score-{trigger.ConquestScore.CapturedTick}-", StringComparison.Ordinal));

    internal static bool ValidConquestScoreStack(MatchState state, StackItemState item)
        => ValidConquestScore(state, item.ConquestScore, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            && (item.ConquestScore is null || item.StackItemId.StartsWith($"ordered-conquest-score-{item.ConquestScore.CapturedTick}-", StringComparison.Ordinal)
                && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
                && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed);

    private static StackResolutionResult ResolveConquestScore(MatchState state, StackItemState item)
    {
        if (!ValidConquestScoreStack(state, item)) throw new InvalidOperationException("Invalid captured conquest score.");
        var context = item.ConquestScore!; var amount = ConquestScoreSpec(context.CardNo)!.ScoreAmount!.Value;
        var scores = NormalizeScoresForSeats(state); scores[item.ControllerId] += amount;
        var publicSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        return NoopStackResolutionResult(state) with { PlayerScores = scores, Events = [
            new("UNIT_TRIGGER_RESOLVED", "征服触发技能结算", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = publicSource,
                ["battlefieldId"] = context.BattlefieldId, ["trigger"] = context.Kind }),
            new("SCORE_GAINED", "征服触发技能获得分数", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = publicSource,
                ["amount"] = amount, ["score"] = scores[item.ControllerId], ["reason"] = context.Kind }) ] };
    }
}
