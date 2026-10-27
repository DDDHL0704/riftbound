using System.Text;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LastBreathDrawContext(string DeathId, string CardNo, string Kind,
    string ControllerId, ObjectBinding Source, string InstanceId, int TriggerIndex,
    bool SourceWasEmpowered, bool SourceWasAlone, int TurnNumber, long CreatedAtTick,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceTokenFactoryCardNo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    private static string LastBreathDrawTriggerId(LastBreathDrawContext context)
        => $"TRIGGER-{context.DeathId}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(context.InstanceId))}:{context.TriggerIndex}-{context.Source.ObjectId}-{context.Kind}";

    private static LastBreathDrawContext[] CaptureLastBreathDraws(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState before, string deathId)
    {
        if (!IsFaceUpNonStandbyUnit(before)) return [];
        // CN 383.2.c.3: retain the position and text before any unreplaced
        // simultaneous death changes zones. Replacement/resource children finish first.
        var frame = RuleChoices.Value?.CurrentDestructionDrawFrame ?? CapturePublicMightFrame(state, zones, cards);
        if (!frame.CardObjects.TryGetValue(before.ObjectId, out var source)
            || source.ObjectGeneration != before.ObjectGeneration) return [];
        var controller = EffectiveFieldControllerId(frame.PlayerZones, source.ObjectId, source);
        var alone = !HasOtherFriendlyUnitAtSamePosition(frame.PlayerZones, frame.CardObjects,
            frame.ObjectLocations, source.ObjectId, controller);
        var result = new List<LastBreathDrawContext>();
        foreach (var instance in AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, source))
        {
            var specs = UnitDestroyedTriggerSpecRules.SelfDrawTriggers(instance.CardNo);
            for (var index = 0; index < specs.Count; index++)
            {
                var context = new LastBreathDrawContext(deathId, instance.CardNo, specs[index].Kind, controller,
                    new(source.ObjectId, source.ObjectGeneration), instance.Id, index, source.IsEmpowered, alone,
                    state.TurnNumber, state.Tick + 1, source.TokenFactoryCardNo, instance.Origin);
                if (TryGetLastBreathDrawSpec(context, out _)) result.Add(context);
            }
        }
        return result.ToArray();
    }

    private static bool TryGetLastBreathDrawSpec(LastBreathDrawContext context, out TriggerSpec spec)
    {
        spec = default!;
        var specs = UnitDestroyedTriggerSpecRules.SelfDrawTriggers(context.CardNo);
        if (context.TriggerIndex < 0 || context.TriggerIndex >= specs.Count) return false;
        spec = specs[context.TriggerIndex];
        return spec.Kind == context.Kind
            && (spec.RequiresSourceEmpowered is not { } required || context.SourceWasEmpowered == required)
            && (spec.RequiresNoOtherFriendlyUnitAtSamePosition != true || context.SourceWasAlone)
            && (spec.RequiresOtherFriendlyUnitAtSamePosition != true || !context.SourceWasAlone);
    }

    internal static bool ValidLastBreathDraw(MatchState state, LastBreathDrawContext context)
    {
        if (context.Source is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(context.Source.ObjectId)
            || string.IsNullOrWhiteSpace(context.ControllerId) || !state.Seats.ContainsKey(context.ControllerId)
            || string.IsNullOrWhiteSpace(context.DeathId) || !context.DeathId.StartsWith("death-", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(context.InstanceId) || context.TurnNumber != state.TurnNumber
            || context.CreatedAtTick <= 0 || context.CreatedAtTick > state.Tick
            || !TryGetLastBreathDrawSpec(context, out _)) return false;
        if (context.Origin is { } binding && (binding.Unit is null || binding.Equipment is null
            || string.IsNullOrWhiteSpace(binding.Unit.ObjectId) || string.IsNullOrWhiteSpace(binding.Equipment.ObjectId))) return false;
        var expectedInstance = context.Origin is { Unit: not null, Equipment: not null } origin
            ? $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
            : $"PRINTED:{context.Source.ObjectId}:{context.Source.Generation}";
        if (context.InstanceId != expectedInstance
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, context.Source.ObjectId,
                context.Source.Generation, context.CardNo)) return false;
        if (state.CardObjects.TryGetValue(context.Source.ObjectId, out var current))
            return current.ObjectGeneration > context.Source.Generation
                && current.TokenFactoryCardNo == context.SourceTokenFactoryCardNo
                && (context.Origin is not null || current.ObjectGeneration != context.Source.Generation + 1 || current.CardNo == context.CardNo);
        return context.SourceTokenFactoryCardNo is not null
            && P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
            && token.Tags.Contains(CardObjectTags.UnitCard);
    }

    internal static bool ValidLastBreathDrawStack(MatchState state, StackItemState item)
        => item.LastBreathDraw is { } context && ValidLastBreathDraw(state, context)
            && item.StackItemId == "ordered-" + LastBreathDrawTriggerId(context)
            && item.ControllerId == context.ControllerId && item.SourceObjectId == context.Source.ObjectId
            && item.CardNo == context.CardNo && item.EffectKind == context.Kind
            && item.TargetObjectIds is { Count: 0 } && item.TargetGenerations is null;

    internal static bool ValidLastBreathDrawTrigger(MatchState state, TriggerQueueItemState item)
        => item.LastBreathDraw is { } context && ValidLastBreathDraw(state, context)
            && item.TriggerId == LastBreathDrawTriggerId(context)
            && item.ControllerId == context.ControllerId && item.SourceObjectId == context.Source.ObjectId
            && item.SourceCardNo == context.CardNo && item.EffectKind == context.Kind
            && item.TriggeredByEventKind == TriggerTimings.UnitDestroyed;

    private static TriggerQueueItemState LastBreathDrawTrigger(LastBreathDrawContext context)
        => new(LastBreathDrawTriggerId(context), context.ControllerId, context.Source.ObjectId,
            context.Kind, TriggerTimings.UnitDestroyed) { SourceCardNo = context.CardNo, LastBreathDraw = context };

    private static StackResolutionResult ResolveCapturedLastBreathDraw(MatchState state, StackItemState item)
    {
        if (item.LastBreathDraw is not { } context || !ValidLastBreathDrawStack(state, item)
            || !TryGetLastBreathDrawSpec(context, out var spec))
            throw new InvalidOperationException("Invalid captured last-breath draw.");
        var count = state.Phase == MatchPhases.TurnStart && state.TurnPlayerId == item.ControllerId
            && spec.OwnBeginningDrawCount is { } own ? own : spec.DrawCount!.Value;
        return ResolveLastBreathDrawStackItem(state, item, count);
    }
}
