using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BeginningRuneContext(string CardNo, ObjectBinding Source, string ControllerId,
    long CapturedTick, string RuleTextInstanceId, LastKnownMightFrame Frame,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BeginningRunePublicContext(string CardNo, int Might, bool Ganking);

public sealed partial class CoreRuleEngine
{
    internal const string BeginningRuneEffect = "CAPTURED_BEGINNING_RUNE_MIGHT";

    private static (int Might, bool Ganking) BeginningRuneAbility(string? cardNo) => cardNo switch {
        "VEN·005" => (1, false),
        "VEN·006" => (2, true),
        _ => (0, false)
    };

    internal static BeginningRunePublicContext? PublicBeginningRune(BeginningRuneContext? context)
    {
        if (context is null) return null;
        var ability = BeginningRuneAbility(context.CardNo);
        return new(context.CardNo, ability.Might, ability.Ganking);
    }

    private static bool HasKnownHistoricalTokenFactory(CardObjectState card)
        => card.TokenFactoryCardNo is { } factory ? P6TokenFactoryCatalog.IsTokenFactory(factory)
            : P6TokenFactoryCatalog.IsTokenFactory(card.CardNo);

    internal static bool ValidCeasedBeginningRuneSource(MatchState state, BeginningRuneContext? context,
        string effect, string controller, string source, string? cardNo = null)
        => !state.CardObjects.ContainsKey(source) && context is { Frame.CardObjects: { } cards }
            && cards.TryGetValue(source, out var captured) && HasKnownHistoricalTokenFactory(captured)
            && CopyCharacteristics.HasFace(context.CardNo)
            && ValidBeginningRune(state, context, effect, controller, source, cardNo);

    private static bool HasFewerControlledRunes(MatchState state, string controller)
    {
        var own = EmpowerRules.ControlledRunes(state, controller);
        return state.Seats.Keys.Any(player => player != controller && EmpowerRules.ControlledRunes(state, player) > own);
    }

    private static MatchState QueueBeginningRuneAbilities(MatchState state, List<GameEvent> events)
    {
        var player = state.TurnPlayerId;
        if (!HasFewerControlledRunes(state, player)) return state;
        var frame = CapturePublicMightFrame(state, state.PlayerZones, state.CardObjects);
        var queue = state.TriggerQueue.ToList();
        var ordinal = 0;
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (source.ControllerId != player || !IsFaceUpNonStandbyUnit(source)
                || !IsObjectOnField(state.PlayerZones, source.ObjectId)
                || !IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, source.ObjectId)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (BeginningRuneAbility(instance.CardNo).Might == 0) continue;
                var context = new BeginningRuneContext(instance.CardNo, new(source.ObjectId, source.ObjectGeneration),
                    player, state.Tick, instance.Id, frame, instance.Origin);
                // A source may enter a private zone before its ability resolves.
                // Keep its object and attached-instance IDs out of the public trigger ID.
                var trigger = new TriggerQueueItemState($"beginning-rune-{state.TurnNumber}-{state.Tick}-{ordinal++}",
                    player, source.ObjectId, BeginningRuneEffect, "TURN_START_BEGAN") {
                        SourceCardNo = instance.CardNo, BeginningRune = context };
                queue.Add(trigger);
                events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        return state with { TriggerQueue = queue };
    }

    internal static bool ValidBeginningRune(MatchState state, BeginningRuneContext? context, string effect,
        string controller, string source, string? cardNo = null)
    {
        if (context is null) return effect != BeginningRuneEffect;
        try {
            if (effect != BeginningRuneEffect || BeginningRuneAbility(context.CardNo).Might == 0
                || context.Source is null || context.Source.ObjectId != source || context.Source.Generation < 0
                || context.ControllerId != controller || controller != state.TurnPlayerId
                || context.CapturedTick < 0 || context.CapturedTick > state.Tick
                || (cardNo is not null && cardNo != context.CardNo)
                || state.Phase != MatchPhases.TurnStart || state.TurnStartStep != "SCORE"
                || context.Frame is not { } frame || !ValidMightFrameExperience(frame)
                || frame.PlayerZones is null || frame.CardObjects is null || frame.ObjectLocations is null
                || frame.BattleDeclarations is null || !frame.PlayerZones.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Seats.Keys)
                || frame.PlayerZones.Values.Any(z => z is null || z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                || frame.BattleDeclarations.Any(e => e is null || !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal))
                || !frame.CardObjects.TryGetValue(source, out var captured) || captured.ObjectId != source
                || captured.ObjectGeneration != context.Source.Generation || captured.ControllerId != controller
                || !IsFaceUpNonStandbyUnit(captured) || !IsObjectOnField(frame.PlayerZones, source)
                || !IsFieldObjectControlledByZonePlayer(frame.PlayerZones, frame.CardObjects, source)
                || !AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, captured).Any(instance =>
                    instance.Id == context.RuleTextInstanceId && instance.CardNo == context.CardNo && instance.Origin == context.Origin)
                || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.Source.Generation, context.CardNo)) return false;
            var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
            if (frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                    || !publicIds.Contains(p.Key) || (state.CardObjects.TryGetValue(p.Key, out var current)
                        ? current.ObjectGeneration < p.Value.ObjectGeneration : !HasKnownHistoricalTokenFactory(p.Value)))
                || frame.ObjectLocations.Keys.Any(id => !frame.CardObjects.ContainsKey(id))) return false;
            return HasFewerControlledRunes(MightFrameState(frame), controller);
        } catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    private static StackResolutionResult ResolveBeginningRune(MatchState state, StackItemState item)
    {
        var context = item.BeginningRune!;
        if (!ValidBeginningRune(state, context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo))
            throw new InvalidOperationException("Invalid captured beginning rune ability.");
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (cards.TryGetValue(context.Source.ObjectId, out var source)
            && source.ObjectGeneration == context.Source.Generation && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId))
        {
            var (might, ganking) = BeginningRuneAbility(context.CardNo);
            var behavior = new CardBehaviorDefinition(context.CardNo, "开始阶段符文数量", 0, BeginningRuneEffect, 0, 0,
                PowerModifierAmount: might);
            cards[source.ObjectId] = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, might, out var modified);
            events.Add(modified);
            if (ganking) GrantLegendRoam(cards, source.ObjectId, item.ControllerId, item.SourceObjectId, item.StackItemId, events);
        }
        events.Add(new("TRIGGER_RESOLVED", "开始阶段符文数量技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
            ["controllerId"] = item.ControllerId, ["stackItemId"] = item.StackItemId }));
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
