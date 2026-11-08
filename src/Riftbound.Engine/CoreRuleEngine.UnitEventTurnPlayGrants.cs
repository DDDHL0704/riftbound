using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitEventTurnPlayGrantOrigin(UnitEventContext Context, BattlefieldTimingAlias? TimingAlias);

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventTurnPlayGrantEffect = "CREATE_TURN_UNIT_PLAY_GRANT";
    internal const string UnitEventReadyBoonGrantKind = "PLAY_READY_BOON";
    internal sealed record UnitEventTurnPlaySkill(string Id, string Controller, string Source, TurnUnitPlayBoonContext Context);

    private static UnitEventTurnPlayGrantOrigin? ReadUnitEventTurnPlayGrantOrigin(string encoded)
    {
        try { return JsonSerializer.Deserialize<UnitEventTurnPlayGrantOrigin>(Convert.FromBase64String(encoded)); }
        catch (Exception error) when (error is JsonException or FormatException or ArgumentException) { return null; }
    }

    private static bool ValidUnitEventTurnPlayGrant(MatchState state, TurnUnitPlayGrant grant, CardBehaviorDefinition b)
        => grant.UnitEventOrigin is { Context: { } context } origin && grant.Kind == UnitEventReadyBoonGrantKind
            && b.TurnUnitPlayGrantNextOnly && grant.ExecutionIndex == 0
            && b.UnitEventAbility is { Effect: UnitEventTurnPlayGrantEffect } spec
            && UnitEventTimingMatches(spec.Timing, "HOLD") && context.CardNo == grant.CardNo
            && context.SourceGeneration == grant.Source.Generation
            && grant.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
            && ValidUnitEvent(state, context, UnitEventEffect, grant.Source.ObjectId, grant.CardNo, grant.ControllerId)
            && ValidTimingAlias(state, origin.TimingAlias, grant.Source.ObjectId, grant.ControllerId, context);

    private static bool ValidUnitEventTurnPlayGrantStack(StackItemState item)
        => item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && !item.SourceConfirmed
            && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && item.OptionalCosts.Count == 0
            && item.Destination.Length == 0 && item.TriggerCost is null;

    private static StackResolutionResult ResolveUnitEventTurnPlayGrant(MatchState state, StackItemState item)
    {
        if (!ValidUnitEventTurnPlayGrantStack(item) || item.UnitEvent is not { } context
            || !CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior))
            throw new InvalidOperationException("Invalid captured unit-event turn play grant.");
        var origin = new UnitEventTurnPlayGrantOrigin(context, item.TimingAlias);
        // An earned skill uses its original source incarnation even if that
        // source returned to hand, changed faces, or actually ceased to exist.
        var marker = TurnGrantMarker(state.TurnNumber, item.ControllerId, item.SourceObjectId, context.SourceGeneration,
            context.CardNo, item.StackItemId, 0, behavior.TurnUnitPlayGrantKind, origin);
        var grant = ReadTurnUnitPlayGrant(marker)!;
        if (!ValidTurnUnitPlayGrant(state, grant)) throw new InvalidOperationException("Invalid unit-event turn play grant.");
        return NoopStackResolutionResult(state) with {
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, marker) };
    }

    internal static TurnUnitPlayGrant[] UnitEventTurnPlayGrants(MatchState state)
        => (state.UntilEndOfTurnEffects ?? []).OfType<string>().Select(ReadTurnUnitPlayGrant).OfType<TurnUnitPlayGrant>()
            .Where(g => g.UnitEventOrigin is not null).ToArray();

    internal static TurnUnitPlayGrantUse[] UnitEventTurnPlayGrantUses(MatchState state)
        => (state.UntilEndOfTurnEffects ?? []).OfType<string>().Select(ReadTurnUnitPlayGrantUse).OfType<TurnUnitPlayGrantUse>()
            .Where(u => u.Grant?.UnitEventOrigin is not null).ToArray();

    internal static UnitEventTurnPlaySkill[] UnitEventTurnPlaySkills(MatchState state)
    {
        return new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>().SelectMany(frame => {
            var stack = frame.StackItems.Concat(frame.PendingEffectPlay?.Parent is { } parent ? [parent] : []);
            return stack.Where(i => i.TurnUnitPlayBoon?.Grant?.UnitEventOrigin is not null)
                .Select(i => new UnitEventTurnPlaySkill(i.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                    ? i.StackItemId[8..] : i.StackItemId, i.ControllerId, i.SourceObjectId, i.TurnUnitPlayBoon!))
                .Concat(frame.TriggerQueue.Where(i => i.TurnUnitPlayBoon?.Grant?.UnitEventOrigin is not null)
                    .Select(i => new UnitEventTurnPlaySkill(i.TriggerId, i.ControllerId, i.SourceObjectId, i.TurnUnitPlayBoon!)));
        }).DistinctBy(skill => MatchStateHasher.HashValue(skill)).ToArray();
    }

    internal static TurnUnitPlayGrantUse UnitEventTurnPlaySkillUse(TurnUnitPlayBoonContext c)
        => new(c.Grant, c.Unit, c.UnitCardNo, c.CapturedTick, c.FaceRevision, c.TokenFactoryCardNo);

    // Opaque receipts bind new delayed effects to the original accepted
    // captures and uses. Comparing before/after also retires countered skills.
    private static ResolutionResult RecordUnitEventTurnPlayHistory(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList();
        void Record(string kind, string key, string hash, int turn, string label, string? grantHash = null)
        {
            if (events.Any(ev => ev.Kind == kind && Equals(ev.Payload.GetValueOrDefault(key), hash))) return;
            var payload = new Dictionary<string, object?> { [key] = hash, ["turnNumber"] = turn };
            if (grantHash is not null) payload["grantHash"] = grantHash;
            events.Add(new(kind, label, payload));
        }
        var oldGrants = UnitEventTurnPlayGrants(before).Select(value => MatchStateHasher.HashValue(value)).ToHashSet(StringComparer.Ordinal);
        foreach (var grant in UnitEventTurnPlayGrants(result.State))
        {
            var hash = MatchStateHasher.HashValue(grant);
            if (!oldGrants.Contains(hash)) Record("UNIT_EVENT_TURN_PLAY_GRANT_CREATED", "grantHash", hash,
                grant.TurnNumber, "创建单位打出延迟效果");
        }
        var oldUses = UnitEventTurnPlayGrantUses(before).Select(value => MatchStateHasher.HashValue(value)).ToHashSet(StringComparer.Ordinal);
        foreach (var use in UnitEventTurnPlayGrantUses(result.State))
        {
            var hash = MatchStateHasher.HashValue(use);
            if (!oldUses.Contains(hash)) Record("UNIT_EVENT_TURN_PLAY_GRANT_USED", "useHash", hash,
                use.Grant.TurnNumber, "捕获下一次单位打出", MatchStateHasher.HashValue(use.Grant));
        }
        var oldSkills = UnitEventTurnPlaySkills(before).ToDictionary(value => MatchStateHasher.HashValue(value), StringComparer.Ordinal);
        var newSkills = UnitEventTurnPlaySkills(result.State).ToDictionary(value => MatchStateHasher.HashValue(value), StringComparer.Ordinal);
        foreach (var (hash, skill) in newSkills)
            if (!oldSkills.ContainsKey(hash)) Record("UNIT_EVENT_TURN_PLAY_SKILL_CAPTURED", "skillHash", hash,
                skill.Context.Grant.TurnNumber, "捕获独立单位打出技能");
        foreach (var (hash, skill) in oldSkills)
            if (!newSkills.ContainsKey(hash)) Record("UNIT_EVENT_TURN_PLAY_SKILL_RETIRED", "skillHash", hash,
                skill.Context.Grant.TurnNumber, "结束独立单位打出技能");
        return result with { Events = events };
    }
}
