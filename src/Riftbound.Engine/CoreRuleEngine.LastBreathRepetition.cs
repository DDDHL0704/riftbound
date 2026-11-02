using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// Authority-only evidence. Each occurrence remains an independent skill;
// it never repeats an already resolving instruction or changes its draw count.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LastBreathRepetitionContext(int Occurrence, LastKnownMightFrame Frame);

public sealed partial class CoreRuleEngine
{
    private static int LastBreathOccurrenceCount(LastKnownMightFrame frame, CardObjectState source)
    {
        var controller = EffectiveFieldControllerId(frame.PlayerZones, source.ObjectId, source);
        var count = 1;
        foreach (var permanent in frame.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (permanent.IsFaceDown || !permanent.Tags.Any(t => t is CardObjectTags.UnitCard or CardObjectTags.EquipmentCard)
                || !IsObjectOnField(frame.PlayerZones, permanent.ObjectId)
                || EffectiveFieldControllerId(frame.PlayerZones, permanent.ObjectId, permanent) != controller
                || !IsSameFieldLocation(frame.PlayerZones, frame.ObjectLocations, source.ObjectId, permanent.ObjectId)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, permanent))
                if (CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var spec))
                    count = checked(count + Math.Max(0, spec.FriendlyLastBreathAdditionalTriggersHere));
        }
        return count;
    }

    private static LastBreathRepetitionContext?[] CaptureLastBreathRepetitions(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState source)
    {
        // The same pre-departure frame is used for every member of a simultaneous
        // batch. Replacement/resource children finish before this frame is taken.
        var frame = RuleChoices.Value?.CurrentDestructionDrawFrame
            ?? CapturePublicMightFrame(state, zones, cards, RuleChoices.Value?.CurrentDestructionLocations);
        if (!frame.CardObjects.TryGetValue(source.ObjectId, out var before)
            || before.ObjectGeneration != source.ObjectGeneration) return [null];
        var count = LastBreathOccurrenceCount(frame, before);
        return count == 1 ? [null] : Enumerable.Range(0, count)
            .Select(index => (LastBreathRepetitionContext?)new(index, frame)).ToArray();
    }

    private static string LastBreathOccurrenceSuffix(LastBreathRepetitionContext? repetition)
        => repetition is null ? "" : $":occurrence-{repetition.Occurrence}";

    private static bool ValidLastBreathRepetition(MatchState state, LastBreathRepetitionContext? repetition,
        ObjectBinding source, string controller, string cardNo, string instanceId)
    {
        if (repetition is null) return true;
        try
        {
            var frame = repetition.Frame;
            if (repetition.Occurrence < 0 || frame is null || frame.CardObjects is null
                || frame.PlayerZones is null || frame.ObjectLocations is null || frame.BattleDeclarations is null
                || !ValidMightFrameExperience(frame)
                || frame.BattleDeclarations.Any(e => e is null || !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal))
                || frame.PlayerZones.Any(p => !state.Seats.ContainsKey(p.Key) || p.Value is null
                    || p.Value.Hand.Count != 0 || p.Value.MainDeck.Count != 0 || p.Value.RuneDeck.Count != 0)
                || frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                    || (state.CardObjects.TryGetValue(p.Key, out var current)
                        ? current.ObjectGeneration < p.Value.ObjectGeneration
                            || current.ObjectGeneration == p.Value.ObjectGeneration && current.CardNo != p.Value.CardNo
                        : !IsTokenObject(p.Value)))
                || !frame.CardObjects.TryGetValue(source.ObjectId, out var before)
                || before.ObjectGeneration != source.Generation || !IsObjectOnField(frame.PlayerZones, source.ObjectId)
                || EffectiveFieldControllerId(frame.PlayerZones, source.ObjectId, before) != controller
                || !AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, before)
                    .Any(i => i.CardNo == cardNo && i.Id == instanceId)) return false;
            var count = LastBreathOccurrenceCount(frame, before);
            return count > 1 && repetition.Occurrence < count;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }
}
