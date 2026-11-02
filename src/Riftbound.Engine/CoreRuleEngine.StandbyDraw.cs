using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StandbyDrawReceipt(string ControllerId, int TurnNumber, long CapturedTick,
    long FaceRevision, string InstanceId, string? SourceTokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string StandbyPlayedDrawEffect = TriggerKinds.ControllerFaceDownPlayDrawOutsideTurnDiscard;
    internal const string StandbyDrawUsePrefix = "STANDBY_DRAW_USED|";

    private static string StandbyDrawMarker(StandbyPlayedObserverContext context)
        => StandbyDrawUsePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(context)));

    private static StandbyPlayedObserverContext? ReadStandbyDrawMarker(string marker)
    {
        try
        {
            if (!marker.StartsWith(StandbyDrawUsePrefix, StringComparison.Ordinal)) return null;
            var context = JsonSerializer.Deserialize<StandbyPlayedObserverContext>(
                Encoding.UTF8.GetString(Convert.FromBase64String(marker[StandbyDrawUsePrefix.Length..])));
            return context?.Draw is not null && marker == StandbyDrawMarker(context) ? context : null;
        }
        catch (Exception error) when (error is FormatException or JsonException or ArgumentException) { return null; }
    }

    private static bool UseStandbyDraw(List<string> effects, StandbyPlayedObserverContext context)
    {
        // CN 383.3.e: the mandatory trigger uses its allowance on confirmation,
        // even if an opponent later counters it. Each text instance is separate.
        if (effects.Select(ReadStandbyDrawMarker).OfType<StandbyPlayedObserverContext>().Any(old =>
            old.Source == context.Source && old.CardNo == context.CardNo
            && old.Draw!.InstanceId == context.Draw!.InstanceId && old.Draw.FaceRevision == context.Draw.FaceRevision)) return false;
        effects.Add(StandbyDrawMarker(context));
        return true;
    }

    private static bool ValidStandbyDrawReceipt(MatchState state, StandbyPlayedObserverContext c, string? controller)
    {
        if (c.Draw is not { } birth || StandbyRules.PlayedDrawEffect(c.CardNo) is null
            || !state.Seats.ContainsKey(birth.ControllerId) || controller is not null && controller != birth.ControllerId
            || birth.TurnNumber != state.TurnNumber || birth.CapturedTick <= 0 || birth.CapturedTick > state.Tick
            || birth.FaceRevision < 0) return false;
        return c.Origin is null
            ? birth.InstanceId == $"PRINTED:{c.Source.ObjectId}:{c.Source.Generation}"
                && PredictBindingMatches(state, c.Source, c.CardNo, birth.FaceRevision, birth.CapturedTick, birth.SourceTokenFactoryCardNo)
            : c.Origin.Equipment is { } gear
                && birth.InstanceId == $"ATTACHED:{gear.ObjectId}:{gear.Generation}:{c.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, c.Origin, c.Source.ObjectId, c.Source.Generation, c.CardNo);
    }

    internal static bool ValidStandbyDrawUses(MatchState state)
    {
        var uses = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(StandbyDrawUsePrefix, StringComparison.Ordinal))
            .Select(ReadStandbyDrawMarker).ToArray();
        return uses.All(c => c is { Source: not null } && ValidStandbyObserver(state, c, StandbyPlayedDrawEffect, c.Source.ObjectId))
            && uses.Select(c => (c!.Source, c.CardNo, c.Draw!.InstanceId, c.Draw.FaceRevision)).Distinct().Count() == uses.Length;
    }

    internal static bool ValidStandbyDrawChoice(MatchState state)
    {
        if (state.PendingHandChoice is not { } choice) return true;
        if (choice.StandbyObserver is not { } observer)
            return choice.EffectKind != StandbyPlayedDrawEffect && choice.ChoiceWindow != StandbyPlayedDrawEffect;
        return observer.Draw is { } birth && birth.ControllerId != state.TurnPlayerId
            && ValidStandbyObserver(state, observer, StandbyPlayedDrawEffect, choice.SourceObjectId, controller: choice.PlayerId)
            && choice.ChoiceId.StartsWith("CHOICE-ordered-standby-play-", StringComparison.Ordinal)
            && choice.ChoiceWindow == StandbyPlayedDrawEffect && choice.EffectKind == StandbyPlayedDrawEffect
            && choice.Reason == StandbyPlayedDrawEffect && choice.RequiredCount == 1 && choice.MaxCount == 1
            && choice.DrawCount == 0 && choice.MovementObserver is null && choice.LastBreathDraw is null && choice.LegendDiscardDraw is null
            && state.PlayerZones.TryGetValue(choice.PlayerId, out var own)
            && choice.LegalObjectIds.Order(StringComparer.Ordinal).SequenceEqual(own.Hand.Order(StringComparer.Ordinal));
    }

    private static StackResolutionResult ResolveStandbyDraw(MatchState state, StackItemState item)
    {
        var c = item.StandbyObserver!;
        if (!ValidStandbyObserver(state, c, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId))
            throw new InvalidOperationException("Invalid captured face-down play draw observer.");
        var definition = StandbyRules.PlayedDrawEffect(c.CardNo)!;
        var draw = ResolveLastBreathDrawStackItem(state, item, definition.DrawCount);
        draw = draw with { Events = draw.Events.Select(ev => ev.Kind == "TRIGGER_RESOLVED"
            ? ev with { Payload = new Dictionary<string, object?>(ev.Payload) { ["triggeredByEventKind"] = "CARD_PLAYED_FROM_FACE_DOWN" } }
            : ev).ToArray() };
        if (draw.WinnerPlayerId is not null || state.TurnPlayerId == item.ControllerId) return draw;

        // "Then, if it is not your turn" is checked after the draw. The new card
        // participates in this private choice; no hand identity enters the event.
        var hand = draw.PlayerZones[item.ControllerId].Hand.Where(id =>
            IsCardObjectControlledByPlayerOrLegacyOwned(draw.CardObjects, item.ControllerId, id)).ToArray();
        var count = Math.Min(definition.OutsideTurnDiscardCount, hand.Length);
        if (count == 0) return draw;
        var choice = new PendingHandChoiceState("CHOICE-" + item.StackItemId, StandbyPlayedDrawEffect,
            item.ControllerId, count, count, hand, StandbyPlayedDrawEffect, item.SourceObjectId,
            StandbyPlayedDrawEffect, 0) { StandbyObserver = c };
        return draw with { PendingHandChoice = choice, Events = draw.Events.Append(new GameEvent(
            "HAND_CHOICE_REQUESTED", "已抽牌，在对手回合选择一张手牌弃置", new Dictionary<string, object?> {
                ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow, ["playerId"] = choice.PlayerId,
                ["sourceObjectId"] = choice.SourceObjectId, ["effectKind"] = choice.EffectKind,
                ["requiredCount"] = count, ["maxCount"] = count, ["legalCount"] = hand.Length })).ToArray() };
    }
}
