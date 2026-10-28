using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PowerfulPlayedRunePublicContext(string CardNo, bool Confirmed);

public sealed partial class CoreRuleEngine
{
    internal const string PowerfulPlayedRuneEffect = "POWERFUL_PLAYED_LEGEND_CHANNEL";
    // FND uses the current CN catalog wording as a runtime candidate; its older
    // physical face differs and has no separately verified erratum here.
    private static bool HasPowerfulPlayedRuneAbility(string? cardNo) => cardNo is "OGN·249/298" or "FND-249/298";

    private sealed record PowerfulPlayedRuneObservation(string ControllerId, string SourceId, string CardNo,
        long SourceGeneration, ObjectBinding Unit, string InstanceId, RuleTextInstanceBinding? Origin,
        int Might, LastKnownMightFrame Frame);

    private static GameEvent CapturePowerfulPlayedRune(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, CardObjectState entered, string player)
    {
        if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")
            || entered.IsFaceDown || !zones.TryGetValue(player, out var own)) return ev;
        var sources = own.LegendZone.Where(id => cards.TryGetValue(id, out var c) && !c.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(c, player))
            .SelectMany(id => AttachedRuleTextRules.Instances(zones, cards, cards[id])
                .Where(instance => HasPowerfulPlayedRuneAbility(instance.CardNo))
                .Select(instance => (Source: cards[id], Instance: instance))).ToArray();
        if (sources.Length == 0 || (RuleChoices.Value?.ResolutionState ?? RuleChoices.Value?.Origin) is not { } state) return ev;
        var frame = CapturePublicMightFrame(state, zones, cards);
        if (!frame.CardObjects.TryGetValue(entered.ObjectId, out var target)
            || !IsFaceUpNonStandbyUnit(target) || !IsObjectOnField(frame.PlayerZones, target.ObjectId)) return ev;
        var might = ResolveCurrentFieldUnitPower(MightFrameState(frame), target);
        if (might < PowerfulUnitPowerThreshold) return ev;
        var observations = sources.Select(x => new PowerfulPlayedRuneObservation(player, x.Source.ObjectId,
            x.Instance.CardNo, x.Source.ObjectGeneration, new(target.ObjectId, target.ObjectGeneration),
            x.Instance.Id, x.Instance.Origin, might, frame)).ToArray();
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) { ["powerfulPlayedRuneObservation"] = observations } };
    }

    private static ResolutionResult QueuePowerfulPlayedRune(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i))) {
            if (ev.Payload.GetValueOrDefault("powerfulPlayedRuneObservation") is not PowerfulPlayedRuneObservation[] observations) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != "powerfulPlayedRuneObservation").ToDictionary(p => p.Key, p => p.Value) };
            var ordinal = 0;
            foreach (var observation in observations) {
                var unit = observation.Unit;
                if (before.CardObjects.TryGetValue(unit.ObjectId, out var old) && !IsObjectOnField(before.PlayerZones, unit.ObjectId))
                    unit = unit with { Generation = Math.Max(unit.Generation, old.ObjectGeneration + 1) };
                var cards = new Dictionary<string, CardObjectState>(observation.Frame.CardObjects) {
                    [unit.ObjectId] = observation.Frame.CardObjects[unit.ObjectId] with { ObjectGeneration = unit.Generation } };
                var frame = observation.Frame with { CardObjects = cards };
                // This context represents a played event, not a Might transition:
                // both numeric fields record the same value at the entry event.
                var context = new MightySkillContext(observation.CardNo, PowerfulPlayedRuneEffect,
                    new(observation.SourceId, observation.SourceGeneration), unit, observation.ControllerId,
                    observation.Might, observation.Might, frame, frame, observation.InstanceId, observation.Origin);
                var trigger = new TriggerQueueItemState($"powerful-played-{result.State.Tick}-{index}-{ordinal++}",
                    observation.ControllerId, observation.SourceId, PowerfulPlayedRuneEffect, ev.Kind) {
                        SourceCardNo = observation.CardNo, MightySkill = context };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (events.Count == result.Events.Count) return result;
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static PowerfulPlayedRunePublicContext? PublicPowerfulPlayedRune(MightySkillContext? context)
        => context?.Kind == PowerfulPlayedRuneEffect ? new(context.CardNo, context.Confirmed) : null;

    private static bool ValidPowerfulPlayedRuneFrame(MightySkillContext context)
    {
        try {
            var frame = context.After;
            if (!HasPowerfulPlayedRuneAbility(context.CardNo) || context.Kind != PowerfulPlayedRuneEffect
                || context.Source is null || context.Unit is null || context.Source.Generation < 0 || context.Unit.Generation < 0
                || context.BeforeMight != context.AfterMight || context.AfterMight < PowerfulUnitPowerThreshold
                || MatchStateHasher.HashValue(context.Before) != MatchStateHasher.HashValue(frame)
                || frame is null || !ValidMightFrameExperience(frame) || frame.CardObjects is null || frame.ObjectLocations is null
                || frame.BattleDeclarations is null || frame.PlayerZones.Values.Any(z => z is null || z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                || frame.BattleDeclarations.Any(e => e is null || !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal))
                || !frame.CardObjects.TryGetValue(context.Source.ObjectId, out var source) || source.ObjectGeneration != context.Source.Generation
                || !SourceObjectControlledByPlayerOrLegacyOwned(source, context.ControllerId) || source.IsFaceDown
                || !frame.PlayerZones.TryGetValue(context.ControllerId, out var own) || !own.LegendZone.Contains(source.ObjectId)
                || !frame.CardObjects.TryGetValue(context.Unit.ObjectId, out var unit) || unit.ObjectGeneration != context.Unit.Generation
                || !IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(frame.PlayerZones, unit.ObjectId)
                || !AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, source).Any(i =>
                    i.Id == context.InstanceId && i.CardNo == context.CardNo && i.Origin == context.Origin)) return false;
            var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
            if (frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown || !publicIds.Contains(p.Key))
                || frame.ObjectLocations.Keys.Any(id => !frame.CardObjects.ContainsKey(id))) return false;
            return ResolveCurrentFieldUnitPower(MightFrameState(frame), unit) == context.AfterMight;
        } catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }
}
