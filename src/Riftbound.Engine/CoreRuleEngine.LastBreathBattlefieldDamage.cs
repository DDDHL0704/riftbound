using System.Text;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LastBreathBattlefieldDamageContext(string DeathId, string CardNo, string SourceCardNo,
    string Kind, string ControllerId, ObjectBinding Source, string InstanceId, int TriggerIndex,
    string SourceZone, string? BattlefieldObjectId, int TurnNumber, long CreatedAtTick,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceTokenFactoryCardNo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    private static string LastBreathBattlefieldDamageTriggerId(LastBreathBattlefieldDamageContext c)
        => $"TRIGGER-{c.DeathId}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(c.InstanceId))}:{c.TriggerIndex}-{c.Source.ObjectId}-{c.Kind}"
            + (c.BattlefieldObjectId is { } field ? SourceBattlefieldTriggerContextMarker + field : "");

    private static LastBreathBattlefieldDamageContext[] CaptureLastBreathBattlefieldDamage(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState before, string deathId)
    {
        if (!IsFaceUpNonStandbyUnit(before) || string.IsNullOrWhiteSpace(before.CardNo)) return [];
        var frame = RuleChoices.Value?.CurrentDestructionDrawFrame
            ?? CapturePublicMightFrame(state, zones, cards, RuleChoices.Value?.CurrentDestructionLocations);
        if (!frame.CardObjects.TryGetValue(before.ObjectId, out var source) || source.ObjectGeneration != before.ObjectGeneration) return [];
        var sourceZone = frame.PlayerZones.Values.Any(z => z.Base.Contains(source.ObjectId)) ? "BASE" : "BATTLEFIELD";
        string? battlefield = null;
        if (sourceZone == "BATTLEFIELD")
        {
            // Unknown legacy battlefield placement is not a legitimate base death.
            if (!frame.ObjectLocations.TryGetValue(source.ObjectId, out var location) || location.Zone != "BATTLEFIELD"
                || string.IsNullOrWhiteSpace(location.BattlefieldObjectId)
                || !TryGetBattlefieldCardObject(frame.PlayerZones, frame.CardObjects, location.BattlefieldObjectId, out _, out _)) return [];
            battlefield = location.BattlefieldObjectId;
        }
        var controller = EffectiveFieldControllerId(frame.PlayerZones, source.ObjectId, source);
        var result = new List<LastBreathBattlefieldDamageContext>();
        foreach (var instance in AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, source))
        {
            var specs = UnitDestroyedTriggerSpecRules.LastBreathBattlefieldDamageTriggers(instance.CardNo);
            for (var index = 0; index < specs.Count; index++)
                result.Add(new(deathId, instance.CardNo, before.CardNo, specs[index].Kind, controller,
                    new(source.ObjectId, source.ObjectGeneration), instance.Id, index, sourceZone, battlefield,
                    state.TurnNumber, state.Tick + 1, source.TokenFactoryCardNo, instance.Origin));
        }
        return result.ToArray();
    }

    private static bool TryGetLastBreathBattlefieldDamageSpec(LastBreathBattlefieldDamageContext c, out TriggerSpec spec)
    {
        spec = default!;
        var specs = UnitDestroyedTriggerSpecRules.LastBreathBattlefieldDamageTriggers(c.CardNo);
        if (c.TriggerIndex < 0 || c.TriggerIndex >= specs.Count) return false;
        spec = specs[c.TriggerIndex]; return spec.Kind == c.Kind;
    }

    internal static bool ValidLastBreathBattlefieldDamage(MatchState state, LastBreathBattlefieldDamageContext c)
    {
        if (c.Source is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(c.Source.ObjectId)
            || string.IsNullOrWhiteSpace(c.SourceCardNo) || string.IsNullOrWhiteSpace(c.ControllerId) || !state.Seats.ContainsKey(c.ControllerId)
            || string.IsNullOrWhiteSpace(c.DeathId) || !c.DeathId.StartsWith("death-", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(c.InstanceId) || c.TurnNumber != state.TurnNumber || c.CreatedAtTick <= 0 || c.CreatedAtTick > state.Tick
            || !TryGetLastBreathBattlefieldDamageSpec(c, out _) || c.SourceZone is not ("BASE" or "BATTLEFIELD")
            || (c.SourceZone == "BASE" ? c.BattlefieldObjectId is not null
                : string.IsNullOrWhiteSpace(c.BattlefieldObjectId)
                    || !TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, c.BattlefieldObjectId, out _, out _))) return false;
        if (c.Origin is { } binding && (binding.Unit is null || binding.Equipment is null
            || string.IsNullOrWhiteSpace(binding.Unit.ObjectId) || string.IsNullOrWhiteSpace(binding.Equipment.ObjectId))) return false;
        var instance = c.Origin is { } origin ? $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
            : $"PRINTED:{c.Source.ObjectId}:{c.Source.Generation}";
        if (c.InstanceId != instance || !AttachedRuleTextRules.ValidCaptured(state, c.Origin, c.Source.ObjectId, c.Source.Generation, c.CardNo)) return false;
        if (state.CardObjects.TryGetValue(c.Source.ObjectId, out var current))
            return current.ObjectGeneration > c.Source.Generation && current.TokenFactoryCardNo == c.SourceTokenFactoryCardNo
                && (current.ObjectGeneration != c.Source.Generation + 1 || current.CardNo == c.SourceCardNo);
        return c.SourceTokenFactoryCardNo is not null && P6TokenFactoryCatalog.TryGetByCardNo(c.SourceTokenFactoryCardNo, out var token)
            && token.Tags.Contains(CardObjectTags.UnitCard);
    }

    internal static bool ValidLastBreathBattlefieldDamageStack(MatchState state, StackItemState item)
        => item.LastBreathBattlefieldDamage is { } c && ValidLastBreathBattlefieldDamage(state, c)
            && item.StackItemId == "ordered-" + LastBreathBattlefieldDamageTriggerId(c)
            && item.ControllerId == c.ControllerId && item.SourceObjectId == c.Source.ObjectId && item.CardNo == c.CardNo
            && item.EffectKind == c.Kind && item.TargetObjectIds is { Count: 0 } && item.TargetGenerations is null;

    internal static bool ValidLastBreathBattlefieldDamageTrigger(MatchState state, TriggerQueueItemState item)
        => item.LastBreathBattlefieldDamage is { } c && ValidLastBreathBattlefieldDamage(state, c)
            && item.TriggerId == LastBreathBattlefieldDamageTriggerId(c)
            && item.ControllerId == c.ControllerId && item.SourceObjectId == c.Source.ObjectId && item.SourceCardNo == c.CardNo
            && item.EffectKind == c.Kind && item.TriggeredByEventKind == TriggerTimings.UnitDestroyed;

    private static TriggerQueueItemState LastBreathBattlefieldDamageTrigger(LastBreathBattlefieldDamageContext c)
        => new(LastBreathBattlefieldDamageTriggerId(c), c.ControllerId, c.Source.ObjectId, c.Kind, TriggerTimings.UnitDestroyed)
            { SourceCardNo = c.CardNo, LastBreathBattlefieldDamage = c };

    private static StackResolutionResult ResolveCapturedLastBreathBattlefieldDamage(MatchState state, StackItemState item)
    {
        if (item.LastBreathBattlefieldDamage is not { } c || !ValidLastBreathBattlefieldDamageStack(state, item)
            || !TryGetLastBreathBattlefieldDamageSpec(c, out var spec)) throw new InvalidOperationException("Invalid captured last-breath battlefield damage.");
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { BuildTriggerResolvedEvent(LastBreathBattlefieldDamageTrigger(c)) };
        if (c.BattlefieldObjectId is { } field)
            foreach (var target in GetBattlefieldUnitObjectIdsAtBattlefield(zones, cards, ReconcileObjectLocations(state.ObjectLocations, zones), field))
            {
                var damage = ApplyDamageToCardObject(state, cards, c.ControllerId, target, spec.DamageAmount!.Value);
                var payload = BuildDamagePayload(c.Source.ObjectId, target, damage); payload["battlefieldObjectId"] = field;
                events.Add(new("DAMAGE_APPLIED", "绝念范围伤害", payload));
            }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }
}
