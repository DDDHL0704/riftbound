using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record TurnUnitPlayGrant(string Marker, int TurnNumber, string ControllerId, ObjectBinding Source,
    string CardNo, string StackItemId, int ExecutionIndex, string Kind);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TurnUnitPlayBoonContext(TurnUnitPlayGrant Grant, ObjectBinding Unit, string UnitCardNo,
    string UnitControllerId, long FaceRevision = -1, long CapturedTick = -1, string? TokenFactoryCardNo = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TurnUnitPlayGrantUse(TurnUnitPlayGrant Grant, ObjectBinding Unit, string UnitCardNo,
    long CapturedTick, long FaceRevision = -1, string? TokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string TurnUnitPlayGrantPrefix = "TURN_UNIT_PLAY_GRANT|";
    internal const string TurnUnitPlayGrantUsePrefix = "TURN_UNIT_PLAY_GRANT_USED|";
    internal const string TurnUnitPlayBoonEffect = "DELAYED_FRIENDLY_UNIT_PLAYED_BOON";
    private const string TurnUnitPlayObservationKey = "turnUnitPlayObservation";
    private sealed record TurnUnitPlayObservation(IReadOnlyList<TurnUnitPlayBoonContext> Boons);
    private static string TurnGrantIdentity(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static string? ReadTurnGrantIdentity(string value)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return null; }
    }
    private static string TurnGrantMarker(int turn, string player, string source, long generation,
        string cardNo, string stack, int execution, string kind) => string.Join('|', "TURN_UNIT_PLAY_GRANT",
        turn.ToString(CultureInfo.InvariantCulture), TurnGrantIdentity(player), TurnGrantIdentity(source),
        generation.ToString(CultureInfo.InvariantCulture), TurnGrantIdentity(cardNo), TurnGrantIdentity(stack),
        execution.ToString(CultureInfo.InvariantCulture), kind);

    internal static TurnUnitPlayGrant? ReadTurnUnitPlayGrant(string marker)
    {
        var f = marker.Split('|');
        if (f.Length != 9 || f[0] != "TURN_UNIT_PLAY_GRANT" || !int.TryParse(f[1], out var turn) || turn <= 0
            || ReadTurnGrantIdentity(f[2]) is not { Length: > 0 } player
            || ReadTurnGrantIdentity(f[3]) is not { Length: > 0 } source
            || !long.TryParse(f[4], out var generation) || generation < 0
            || ReadTurnGrantIdentity(f[5]) is not { Length: > 0 } card
            || ReadTurnGrantIdentity(f[6]) is not { Length: > 0 } stack
            || !int.TryParse(f[7], out var execution) || execution < 0 || f[8] is not ("READY_ENTRY" or "PLAY_BOON" or "PLAY_DRAW")
            || marker != TurnGrantMarker(turn, player, source, generation, card, stack, execution, f[8])) return null;
        return new(marker, turn, player, new(source, generation), card, stack, execution, f[8]);
    }

    internal static bool ValidTurnUnitPlayGrant(MatchState state, TurnUnitPlayGrant grant)
        => grant is not null && grant.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(grant.Marker)
            && ReadTurnUnitPlayGrant(grant.Marker) == grant
            && grant.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(grant.ControllerId)
            && CardBehaviorRegistry.TryGetByCardNo(grant.CardNo, out var b) && b.TurnUnitPlayGrantKind == grant.Kind
            && state.CardObjects.TryGetValue(grant.Source.ObjectId, out var source) && source.ObjectGeneration >= grant.Source.Generation
            && (source.ObjectGeneration != grant.Source.Generation || source.CardNo == grant.CardNo)
            && grant.StackItemId.StartsWith("STACK-", StringComparison.Ordinal) && grant.StackItemId.EndsWith("-" + grant.Source.ObjectId, StringComparison.Ordinal)
            && grant.StackItemId.Length > grant.Source.ObjectId.Length + 7
            && long.TryParse(grant.StackItemId[6..^(grant.Source.ObjectId.Length + 1)], out var tick) && tick > 0 && tick <= state.Tick
            && grant.StackItemId == "STACK-" + tick.ToString(CultureInfo.InvariantCulture) + "-" + grant.Source.ObjectId;

    internal static bool ValidCurrentTurnUnitPlayGrants(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(e => e is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(TurnUnitPlayGrantPrefix, StringComparison.Ordinal)).ToArray();
        var uses = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(TurnUnitPlayGrantUsePrefix, StringComparison.Ordinal))
            .Select(ReadTurnUnitPlayGrantUse).ToArray();
        return markers.Length == markers.Distinct(StringComparer.Ordinal).Count()
            && markers.All(m => ReadTurnUnitPlayGrant(m) is { } grant && ValidTurnUnitPlayGrant(state, grant))
            && uses.All(use => use is { Grant: not null, Unit.ObjectId.Length: > 0, Unit.Generation: >= 0,
                    UnitCardNo.Length: > 0, CapturedTick: > 0, FaceRevision: >= 0 }
                && use.CapturedTick <= state.Tick && markers.Contains(use.Grant.Marker, StringComparer.Ordinal)
                && ValidTurnUnitPlayGrant(state, use.Grant)
                && long.TryParse(use.Grant.StackItemId[6..^(use.Grant.Source.ObjectId.Length + 1)], out var grantTick)
                && use.CapturedTick > grantTick
                && CardBehaviorRegistry.TryGetByCardNo(use.Grant.CardNo, out var b) && b.TurnUnitPlayGrantNextOnly
                && TryPredictEnteredFace(use.UnitCardNo, out var entered) && entered.Tags.Contains(CardObjectTags.UnitCard)
                && PredictBindingMatches(state, use.Unit, use.UnitCardNo, use.FaceRevision,
                    use.CapturedTick, use.TokenFactoryCardNo))
            && uses.Select(use => use!.Grant.Marker).Distinct(StringComparer.Ordinal).Count() == uses.Length;
    }

    private static string TurnUnitPlayGrantUseMarker(TurnUnitPlayGrantUse use)
        => TurnUnitPlayGrantUsePrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(use));

    private static TurnUnitPlayGrantUse? ReadTurnUnitPlayGrantUse(string marker)
    {
        if (!marker.StartsWith(TurnUnitPlayGrantUsePrefix, StringComparison.Ordinal)) return null;
        try {
            var use = JsonSerializer.Deserialize<TurnUnitPlayGrantUse>(Convert.FromBase64String(marker[TurnUnitPlayGrantUsePrefix.Length..]));
            return use is { Grant: not null, Unit: not null } && marker == TurnUnitPlayGrantUseMarker(use) ? use : null;
        }
        catch (Exception error) when (error is JsonException or FormatException or ArgumentException) { return null; }
    }

    private static IReadOnlyList<TurnUnitPlayGrant> ActiveUnitPlayGrants(IReadOnlyList<string>? effects, string player, string kind)
        => effects is null ? [] : effects.Where(e => e.StartsWith(TurnUnitPlayGrantPrefix, StringComparison.Ordinal))
            .Select(ReadTurnUnitPlayGrant).OfType<TurnUnitPlayGrant>().Where(g => g.ControllerId == player && g.Kind == kind
                && !effects.Select(ReadTurnUnitPlayGrantUse).OfType<TurnUnitPlayGrantUse>().Any(use => use.Grant.Marker == g.Marker)).ToArray();

    internal static bool GrantedUnitPlayBattlefieldAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, string destination)
        => behavior.PlaysSourceToBaseAsUnit && destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && BattlefieldLocalRules.Battlefield(state, destination[12..]) is { } field
            && !field.IsFaceDown && IsObjectOnField(state.PlayerZones, field.ObjectId)
            && ActiveUnitPlayGrants(state.UntilEndOfTurnEffects, player, "PLAY_BOON").Any(GrantsAnyUnitPlayBattlefield);

    private static bool GrantsAnyUnitPlayBattlefield(TurnUnitPlayGrant grant)
        => CardBehaviorRegistry.TryGetByCardNo(grant.CardNo, out var behavior)
            && behavior.TurnUnitPlayGrantAnyBattlefield && (!behavior.TurnUnitPlayGrantNextOnly
                || RuleChoices.Value?.ConsumedUnitPlayGrants.Contains(grant.Marker) != true);

    private static string UnitPlayDrawGroup(TurnUnitPlayGrant grant)
    {
        var count = CardBehaviorRegistry.TryGetByCardNo(grant.CardNo, out var behavior)
            ? Math.Max(1, behavior.TurnUnitPlayGrantCount) : 1;
        // A repeated Big Horn effect has one three-play window per execution.
        // Keep those windows independent while the three markers within one
        // window compete for the same unit play.
        return string.Join('|', grant.Source.ObjectId, grant.StackItemId,
            (grant.ExecutionIndex / count).ToString(CultureInfo.InvariantCulture));
    }

    private static void AddTurnUnitPlayGrants(MatchState state, StackItemState item, CardBehaviorDefinition b,
        List<string> effects, List<GameEvent> events)
    {
        if (b.TurnUnitPlayGrantKind.Length == 0) return;
        var source = state.CardObjects[item.SourceObjectId];
        var perExecution = b.TurnUnitPlayGrantCount > 0 ? b.TurnUnitPlayGrantCount : 1;
        var repeatCount = Math.Max(1, item.EffectRepeatCount);
        var grantCount = checked(perExecution * repeatCount);
        var executionBase = b.TurnUnitPlayGrantCount > 0
            ? checked(item.CompletedRepeatExecutions * perExecution) : item.CompletedRepeatExecutions;
        for (var index = 0; index < grantCount; index++)
        {
            var marker = TurnGrantMarker(state.TurnNumber, item.ControllerId, item.SourceObjectId, source.ObjectGeneration,
                item.CardNo!, item.StackItemId, executionBase + index, b.TurnUnitPlayGrantKind);
            effects.Add(marker); events.Add(new("TURN_UNIT_PLAY_GRANT_CREATED", "创建本回合单位打出效果",
                new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["controllerId"] = item.ControllerId,
                    ["effectId"] = marker, ["grantKind"] = b.TurnUnitPlayGrantKind, ["duration"] = "UNTIL_END_OF_TURN" }));
        }
    }

    private static GameEvent CaptureTurnUnitPlayGrants(GameEvent ev, Dictionary<string, CardObjectState> cards,
        CardObjectState unit, string player, IReadOnlyList<string>? effects)
    {
        if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")) return ev;
        if (ActiveUnitPlayGrants(effects, player, "READY_ENTRY").Count > 0)
        {
            unit = unit with { IsExhausted = false }; cards[unit.ObjectId] = unit;
            ev = ev with { Payload = new Dictionary<string, object?>(ev.Payload) { ["isExhausted"] = false,
                ["turnUnitPlayReady"] = true } };
        }
        var controller = unit.ControllerId ?? player;
        var contexts = ActiveUnitPlayGrants(effects, controller, "PLAY_BOON")
            .Where(grant => !CardBehaviorRegistry.TryGetByCardNo(grant.CardNo, out var behavior)
                || !behavior.TurnUnitPlayGrantNextOnly
                || RuleChoices.Value?.ConsumedUnitPlayGrants.Contains(grant.Marker) != true)
            .Select(g => new TurnUnitPlayBoonContext(g, new(unit.ObjectId, unit.ObjectGeneration), unit.CardNo ?? "",
                controller, unit.CopyFaceTransitions?.Count ?? 0, TokenFactoryCardNo: unit.TokenFactoryCardNo))
            .Concat(ActiveUnitPlayGrants(effects, controller, "PLAY_DRAW")
                .GroupBy(UnitPlayDrawGroup, StringComparer.Ordinal)
                .Select(group => group.OrderBy(g => g.ExecutionIndex).First())
                .Select(g => new TurnUnitPlayBoonContext(g, new(unit.ObjectId, unit.ObjectGeneration), unit.CardNo ?? "",
                    controller, unit.CopyFaceTransitions?.Count ?? 0, TokenFactoryCardNo: unit.TokenFactoryCardNo)))
            .ToArray();
        foreach (var context in contexts)
            if (CardBehaviorRegistry.TryGetByCardNo(context.Grant.CardNo, out var behavior) && behavior.TurnUnitPlayGrantNextOnly)
                RuleChoices.Value?.ConsumedUnitPlayGrants.Add(context.Grant.Marker);
        return contexts.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [TurnUnitPlayObservationKey] = new TurnUnitPlayObservation(contexts) } };
    }

    private static ResolutionResult QueueTurnUnitPlayBoons(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var effects = result.State.UntilEndOfTurnEffects.ToList();
        var events = new List<GameEvent>();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault(TurnUnitPlayObservationKey) is not TurnUnitPlayObservation observation) { events.Add(ev); continue; }
            events.Add(ev with { Payload = ev.Payload.Where(p => p.Key != TurnUnitPlayObservationKey).ToDictionary(p => p.Key, p => p.Value) });
            foreach (var captured in observation.Boons)
            {
                // A PLAY_DRAW marker is a single use of one of the source's
                // remaining three opportunities. It is removed when the unit
                // play is captured, so a response or a counter cannot refund it.
                if (captured.Grant.Kind == "PLAY_DRAW" && !effects.Remove(captured.Grant.Marker)) continue;
                var context = captured with { CapturedTick = result.State.Tick };
                if (before.CardObjects.TryGetValue(context.Unit.ObjectId, out var old) && !IsObjectOnField(before.PlayerZones, old.ObjectId))
                    context = context with { Unit = context.Unit with { Generation = Math.Max(context.Unit.Generation, old.ObjectGeneration + 1) } };
                if (CardBehaviorRegistry.TryGetByCardNo(context.Grant.CardNo, out var behavior) && behavior.TurnUnitPlayGrantNextOnly)
                {
                    // Multiple tokens can enter during one instruction. Each
                    // one-shot grant observes only the first actual play.
                    if (effects.Select(ReadTurnUnitPlayGrantUse).OfType<TurnUnitPlayGrantUse>()
                        .Any(use => use.Grant.Marker == context.Grant.Marker)) continue;
                    effects.Add(TurnUnitPlayGrantUseMarker(new(context.Grant, context.Unit, context.UnitCardNo,
                        context.CapturedTick, context.FaceRevision, context.TokenFactoryCardNo)));
                }
                var trigger = new TriggerQueueItemState($"turn-unit-boon-{result.State.Tick}-{index}-{context.Grant.ExecutionIndex}-{context.Grant.StackItemId}-{context.Unit.ObjectId}",
                    context.Grant.ControllerId, context.Grant.Source.ObjectId, TurnUnitPlayBoonEffect, ev.Kind) {
                        SourceCardNo = context.Grant.CardNo,
                        TurnUnitPlayBoon = context.Grant.Kind == "PLAY_BOON" ? context : null,
                        TurnUnitPlayDraw = context.Grant.Kind == "PLAY_DRAW" ? context : null };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        var state = result.State with { TriggerQueue = queue, UntilEndOfTurnEffects = effects };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidTurnUnitPlayContext(MatchState state, TurnUnitPlayBoonContext? c, string effect,
        string sourceId, string controller, string? cardNo, string kind)
        => c is null ? effect != TurnUnitPlayBoonEffect
            : effect == TurnUnitPlayBoonEffect && c.Grant is not null && c.Grant.Kind == kind && ValidTurnUnitPlayGrant(state, c.Grant)
                && c.Grant.Source.ObjectId == sourceId && c.Grant.ControllerId == controller && c.Grant.CardNo == cardNo
                && c.Unit is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(c.Unit.ObjectId)
                && c.FaceRevision >= 0 && c.CapturedTick > 0 && c.CapturedTick <= state.Tick
                && c.UnitControllerId == controller
                && TryPredictEnteredFace(c.UnitCardNo, out var entered) && entered.Tags.Contains(CardObjectTags.UnitCard)
                && PredictBindingMatches(state, c.Unit, c.UnitCardNo, c.FaceRevision, c.CapturedTick, c.TokenFactoryCardNo);

    internal static bool ValidTurnUnitPlayBoon(MatchState state, TurnUnitPlayBoonContext? c, string effect,
        string sourceId, string controller, string? cardNo)
        => ValidTurnUnitPlayContext(state, c, effect, sourceId, controller, cardNo, "PLAY_BOON");

    internal static bool ValidTurnUnitPlayDraw(MatchState state, TurnUnitPlayBoonContext? c, string effect,
        string sourceId, string controller, string? cardNo)
        => ValidTurnUnitPlayContext(state, c, effect, sourceId, controller, cardNo, "PLAY_DRAW");

    private static StackResolutionResult ResolveTurnUnitPlayBoon(MatchState state, StackItemState item)
    {
        var c = item.TurnUnitPlayBoon!;
        if (!ValidTurnUnitPlayBoon(state, c, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
            return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var b = CardBehaviorRegistry.GetAll().Single(b => b.CardNo == c.Grant.CardNo);
        if (!state.CardObjects.TryGetValue(c.Unit.ObjectId, out var unit) || unit.ObjectGeneration != c.Unit.Generation
            || !IsFieldUnitObject(state, unit.ObjectId)) return NoopStackResolutionResult(state);
        cards[unit.ObjectId] = ApplyBoon(unit, b, item, unit.ObjectId, out var events, state.PlayerZones, cards);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }

    private static StackResolutionResult ResolveTurnUnitPlayDraw(MatchState state, StackItemState item)
    {
        var c = item.TurnUnitPlayDraw!;
        if (!ValidTurnUnitPlayDraw(state, c, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
            return NoopStackResolutionResult(state);
        var behavior = CardBehaviorRegistry.GetAll().Single(b => b.CardNo == c.Grant.CardNo);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var drawCount = Math.Max(1, behavior.DrawCount);
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", $"{behavior.DisplayName}触发抽牌",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["sourceCardNo"] = c.Grant.CardNo, ["effectKind"] = TurnUnitPlayBoonEffect, ["drawCount"] = drawCount }) };
        var draw = ApplyDrawToPlayer(state, zones, cards, NormalizeScoresForSeats(state), item.ControllerId,
            drawCount, state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
