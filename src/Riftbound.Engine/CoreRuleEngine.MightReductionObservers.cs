using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MightReductionFact(ObjectBinding Target, string Actor,
    LastKnownMightFrame Before, LastKnownMightFrame After,
    ObjectBinding? AuraSource = null, string? AuraInstance = null, int? AuraIndex = null);

public sealed partial class CoreRuleEngine
{
    private const string EnemyMightReducedHere = "ENEMY_MIGHT_REDUCED_HERE";

    private static IEnumerable<UnitEventObservation> CaptureMightReduction(MightReductionFact fact)
    {
        var after = MightFrameState(fact.After);
        if (!after.CardObjects.TryGetValue(fact.Target.ObjectId, out var target)
            || !IsEnemyFieldUnit(after.PlayerZones, after.CardObjects, fact.Actor, target.ObjectId)) yield break;
        foreach (var observation in CaptureUnitEvents(after.PlayerZones, after.CardObjects,
            after.CardObjects.Values.Where(c => EffectiveFieldControllerId(after.PlayerZones, c.ObjectId, c) == fact.Actor
                && IsSameFieldLocation(after.PlayerZones, after.ObjectLocations, c.ObjectId, target.ObjectId))
                .Select(c => c.ObjectId), EnemyMightReducedHere))
            yield return observation with { Context = observation.Context with { MightReduction = fact } };
    }

    private static GameEvent CaptureDirectMightReduction(GameEvent ev, MatchState? state,
        CardObjectState before, CardObjectState after, string actor)
    {
        if (state is null || after.Power >= before.Power) return ev;
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) { [before.ObjectId] = before };
        var prior = CapturePublicMightFrame(state, state.PlayerZones, cards);
        cards[after.ObjectId] = after;
        var current = CapturePublicMightFrame(state, state.PlayerZones, cards);
        return WithUnitEvents(ev, CaptureMightReduction(new(new(after.ObjectId, after.ObjectGeneration), actor, prior, current)).ToArray());
    }

    // Continuous effects can newly apply a negative amount even when a positive
    // effect keeps the final Might unchanged (Radiance FAQ, Ahri / Leona).
    private static IEnumerable<UnitEventObservation> CaptureAuraMightReductions(LastKnownMightFrame before, LastKnownMightFrame after)
    {
        if (!after.CardObjects.Values.Any(c => IsObjectOnField(after.PlayerZones, c.ObjectId)
            && AttachedRuleTextRules.Instances(after.PlayerZones, after.CardObjects, c)
                .Any(i => UnitEventSpec(i.CardNo)?.Timing == EnemyMightReducedHere))) yield break;
        var prior = MightFrameState(before); var current = MightFrameState(after);
        foreach (var target in after.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c)
            && IsObjectOnField(after.PlayerZones, c.ObjectId))) {
            var previous = before.CardObjects.TryGetValue(target.ObjectId, out var old)
                && old.ObjectGeneration == target.ObjectGeneration
                ? ConditionalPowerAuraContributions(prior, old) : [];
            foreach (var aura in ConditionalPowerAuraContributions(current, target)) {
                if (aura.Delta >= 0) continue;
                var source = after.CardObjects[aura.SourceId];
                var priorDelta = before.CardObjects.TryGetValue(source.ObjectId, out var oldSource)
                    && oldSource.ObjectGeneration == source.ObjectGeneration
                    ? previous.FirstOrDefault(a => a.SourceId == aura.SourceId && a.Instance.Id == aura.Instance.Id && a.Index == aura.Index)?.Delta ?? 0 : 0;
                // Recalculating an already-active negative passive is the same
                // effect, not a new application (official Ahri FAQ: 3 -> 5).
                if (priorDelta < 0) continue;
                var actor = EffectiveFieldControllerId(after.PlayerZones, source.ObjectId, source);
                var fact = new MightReductionFact(new(target.ObjectId, target.ObjectGeneration), actor, before, after,
                    new(source.ObjectId, source.ObjectGeneration), aura.Instance.Id, aura.Index);
                foreach (var observation in CaptureMightReduction(fact)) yield return observation;
            }
        }
    }

    private static bool ValidMightReduction(MatchState state, UnitEventContext context, string source)
    {
        try {
            if (context.MightReduction is not { Target: { Generation: >= 0 } binding } fact
                || !state.Seats.ContainsKey(fact.Actor)) return false;
            foreach (var frame in new[] { fact.Before, fact.After }) {
                if (!ValidMightFrameExperience(frame) || !frame.PlayerZones.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Seats.Keys)
                    || frame.PlayerZones.Values.Any(z => z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                    || frame.BattleDeclarations.Any(e => !e.StartsWith(BattleResponseDeclarationContextPrefix, StringComparison.Ordinal))) return false;
                var ids = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                    .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
                if (frame.CardObjects.Any(p => p.Key != p.Value.ObjectId || p.Value.IsFaceDown || !ids.Contains(p.Key)
                    || (state.CardObjects.TryGetValue(p.Key, out var now) ? !ValidHistoricalMightyFace(state, now, p.Value) : !IsTokenObject(p.Value)))
                    || frame.ObjectLocations.Keys.Any(id => !frame.CardObjects.ContainsKey(id))) return false;
            }
            var current = MightFrameState(fact.After);
            if (!fact.After.CardObjects.TryGetValue(source, out var observer) || observer.ObjectGeneration != context.SourceGeneration
                || (observer.CopyFaceTransitions?.Count ?? 0) != context.FaceRevision
                || !IsObjectOnField(fact.After.PlayerZones, source)
                || EffectiveFieldControllerId(fact.After.PlayerZones, source, observer) != fact.Actor
                || !AttachedRuleTextRules.Instances(current, observer).Any(i => i.CardNo == context.CardNo && i.Origin == context.Origin)
                || !fact.After.CardObjects.TryGetValue(binding.ObjectId, out var target) || target.ObjectGeneration != binding.Generation
                || !IsEnemyFieldUnit(fact.After.PlayerZones, fact.After.CardObjects, fact.Actor, target.ObjectId)
                || !IsSameFieldLocation(fact.After.PlayerZones, fact.After.ObjectLocations, source, target.ObjectId)) return false;
            if (fact.AuraSource is not null)
                return CaptureAuraMightReductions(fact.Before, fact.After).Any(o => o.Source == source
                    && o.Context.CardNo == context.CardNo && o.Context.Origin == context.Origin
                    && o.Context.MightReduction!.Target == binding && o.Context.MightReduction.AuraSource == fact.AuraSource
                    && o.Context.MightReduction.AuraInstance == fact.AuraInstance && o.Context.MightReduction.AuraIndex == fact.AuraIndex
                    && o.Context.MightReduction.Actor == fact.Actor);
            if (fact.AuraInstance is not null || fact.AuraIndex is not null
                || !fact.Before.CardObjects.TryGetValue(binding.ObjectId, out var old) || old.ObjectGeneration != binding.Generation) return false;
            var added = target.UntilEndOfTurnPowerModifiers.Where(m => !old.UntilEndOfTurnPowerModifiers.Any(p => p.EffectId == m.EffectId)).ToArray();
            return added.Length == 1 && added[0].PowerDelta < 0 && target.Power - old.Power == added[0].PowerDelta
                && target.UntilEndOfTurnPowerModifier - old.UntilEndOfTurnPowerModifier == added[0].PowerDelta;
        } catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }
}
