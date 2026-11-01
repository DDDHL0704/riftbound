using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NextUnitPlayGrant(int TurnNumber, string ControllerId, ObjectBinding Source,
    string CardNo, long ConquestTick, string Kind);

public sealed partial class CoreRuleEngine
{
    internal const string NextUnitPlayGrantEffect = "CONQUEST_NEXT_UNIT_PLAY_GRANT";
    internal const string NextUnitPlayGrantPrefix = "NEXT_UNIT_PLAY_GRANT:";
    internal const string NextUnitPlayConsumedKey = "nextUnitPlayGrantsConsumed";
    private static string NextUnitPlayMarker(NextUnitPlayGrant grant) => NextUnitPlayGrantPrefix + JsonSerializer.Serialize(grant);
    internal static NextUnitPlayGrant? ReadNextUnitPlayGrant(string marker)
    {
        try { return marker.StartsWith(NextUnitPlayGrantPrefix, StringComparison.Ordinal)
            ? JsonSerializer.Deserialize<NextUnitPlayGrant>(marker[NextUnitPlayGrantPrefix.Length..]) : null; }
        catch (JsonException) { return null; }
    }

    private static bool HasNextUnitHasteSpec(string? cardNo) => BattlefieldTriggerSpecRules.TryGetTrigger(cardNo,
        t => t.Kind == TriggerKinds.BattlefieldConquerNextUnitHaste && t.Timing == TriggerTimings.BattlefieldConquered
            && t.Duration == TriggerDurations.UntilEndOfTurn && t.GrantedKeyword == CardPermissionKeywordNames.Haste, out _);

    internal static bool ValidNextUnitPlayGrant(MatchState state, NextUnitPlayGrant? grant, string effect,
        string source, string controller, string? cardNo)
        => grant is null ? effect != NextUnitPlayGrantEffect
            : effect == NextUnitPlayGrantEffect && grant.Kind == "HASTE" && grant.Source is { Generation: >= 0 }
                && !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(controller)
                && grant.Source.ObjectId == source && grant.ControllerId == controller && grant.CardNo == cardNo
                && grant.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(controller)
                && grant.ConquestTick > 0 && grant.ConquestTick <= state.Tick && HasNextUnitHasteSpec(grant.CardNo)
                && state.CardObjects.TryGetValue(source, out var field) && field.ObjectGeneration >= grant.Source.Generation
                && (field.ObjectGeneration != grant.Source.Generation || field.CardNo == grant.CardNo)
                && field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag);

    internal static bool ValidCurrentNextUnitPlayGrants(MatchState state)
        => state.UntilEndOfTurnEffects.Where(m => m.StartsWith(NextUnitPlayGrantPrefix, StringComparison.Ordinal)).All(m =>
            ReadNextUnitPlayGrant(m) is { Source: not null } grant && NextUnitPlayMarker(grant) == m
                && ValidNextUnitPlayGrant(state, grant, NextUnitPlayGrantEffect, grant.Source.ObjectId, grant.ControllerId, grant.CardNo));

    private static string[] ActiveNextUnitPlayGrants(IReadOnlyList<string> effects, string player)
        => effects.Where(m => m.StartsWith(NextUnitPlayGrantPrefix, StringComparison.Ordinal)
            && ReadNextUnitPlayGrant(m) is { Kind: "HASTE" } grant && grant.ControllerId == player
            && RuleChoices.Value?.ConsumedNextUnitPlayGrants.Contains(m) != true).ToArray();

    private static bool HasNextUnitHasteGrant(MatchState state, string player)
        => ActiveNextUnitPlayGrants(state.UntilEndOfTurnEffects, player).Length > 0;

    private static GameEvent CaptureNextUnitPlayGrants(GameEvent ev, string player, IReadOnlyList<string> effects)
    {
        if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")) return ev;
        var grants = ActiveNextUnitPlayGrants(effects, player);
        if (grants.Length == 0) return ev;
        RuleChoices.Value?.ConsumedNextUnitPlayGrants.UnionWith(grants);
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) { [NextUnitPlayConsumedKey] = grants } };
    }

    private static ResolutionResult RecordNextUnitPlayGrants(ResolutionResult result)
    {
        var consumed = result.Events.SelectMany(e => e.Payload.GetValueOrDefault(NextUnitPlayConsumedKey) as string[] ?? []).ToHashSet(StringComparer.Ordinal);
        if (consumed.Count == 0) return result;
        var state = result.State with { UntilEndOfTurnEffects = result.State.UntilEndOfTurnEffects.Where(m => !consumed.Contains(m)).ToArray() };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static IReadOnlyList<TriggerQueueItemState> CaptureConquestNextUnitPlayGrant(MatchState state, string player, string battlefield, long tick)
    {
        if (BattlefieldLocalRules.Battlefield(state, battlefield) is not { } field || !HasNextUnitHasteSpec(field.CardNo)) return [];
        var grant = new NextUnitPlayGrant(state.TurnNumber, player, new(field.ObjectId, field.ObjectGeneration), field.CardNo!, tick, "HASTE");
        return [new($"CONQUEST-NEXT-UNIT:{tick}:{battlefield}", player, battlefield, NextUnitPlayGrantEffect, "BATTLEFIELD_CONQUERED") {
            SourceCardNo = field.CardNo, NextUnitPlayGrant = grant }];
    }

    private static StackResolutionResult ResolveNextUnitPlayGrant(MatchState state, StackItemState item)
    {
        var grant = item.NextUnitPlayGrant!;
        if (!ValidNextUnitPlayGrant(state, grant, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
            throw new InvalidOperationException("Invalid captured next unit play grant.");
        var marker = NextUnitPlayMarker(grant);
        return NoopStackResolutionResult(state) with { UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, marker),
            Events = [new("NEXT_UNIT_PLAY_GRANT_CREATED", "本回合打出的下一名单位具有急速", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["grant"] = grant, ["effectId"] = marker })] };
    }
}
