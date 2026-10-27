using System.Text;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LastBreathUnitCreationContext(string DeathId, string CardNo, string Kind,
    string ControllerId, ObjectBinding Source, string InstanceId, int TriggerIndex,
    bool SourceWasEmpowered, int TurnNumber, long CreatedAtTick,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceTokenFactoryCardNo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    private static string LastBreathUnitCreationTriggerId(LastBreathUnitCreationContext context)
        => $"TRIGGER-{context.DeathId}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(context.InstanceId))}:{context.TriggerIndex}-{context.Source.ObjectId}-{context.Kind}";

    private static string? ResolveUnitLastBreathCreateBaseUnitPlayerId(CardObjectState before,
        TriggerSpec spec, string fieldController)
        => IsFaceUpNonStandbyUnit(before) && (spec.RequiresSourceEmpowered is not { } required || before.IsEmpowered == required)
            ? fieldController : null;

    private static LastBreathUnitCreationContext[] CaptureLastBreathUnitCreations(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState before, string deathId)
    {
        if (!IsFaceUpNonStandbyUnit(before)) return [];
        var result = new List<LastBreathUnitCreationContext>();
        var controller = EffectiveFieldControllerId(zones, before.ObjectId, before);
        foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, before))
        {
            var specs = UnitDestroyedTriggerSpecRules.BaseUnitCreationTriggers(instance.CardNo);
            for (var index = 0; index < specs.Count; index++)
            {
                var spec = specs[index];
                if (ResolveUnitLastBreathCreateBaseUnitPlayerId(before, spec, controller) is null) continue;
                result.Add(new(deathId, instance.CardNo, spec.Kind, controller,
                    new(before.ObjectId, before.ObjectGeneration), instance.Id, index, before.IsEmpowered,
                    state.TurnNumber, state.Tick + 1, before.TokenFactoryCardNo, instance.Origin));
            }
        }
        return result.ToArray();
    }

    private static bool TryGetLastBreathUnitCreationSpec(LastBreathUnitCreationContext context, out TriggerSpec spec)
    {
        spec = default!;
        var specs = UnitDestroyedTriggerSpecRules.BaseUnitCreationTriggers(context.CardNo);
        if (context.TriggerIndex < 0 || context.TriggerIndex >= specs.Count) return false;
        spec = specs[context.TriggerIndex];
        return spec.Kind == context.Kind
            && (spec.RequiresSourceEmpowered is not { } required || context.SourceWasEmpowered == required);
    }

    internal static bool ValidLastBreathUnitCreation(MatchState state, LastBreathUnitCreationContext context)
    {
        if (context.Source is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(context.Source.ObjectId)
            || string.IsNullOrWhiteSpace(context.ControllerId) || !state.Seats.ContainsKey(context.ControllerId)
            || string.IsNullOrWhiteSpace(context.DeathId) || !context.DeathId.StartsWith("death-", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(context.InstanceId) || context.TurnNumber != state.TurnNumber
            || context.CreatedAtTick <= 0 || context.CreatedAtTick > state.Tick
            || !TryGetLastBreathUnitCreationSpec(context, out _)) return false;
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

    internal static bool ValidLastBreathUnitCreationStack(MatchState state, StackItemState item)
        => item.LastBreathUnitCreation is { } context && ValidLastBreathUnitCreation(state, context)
            && item.StackItemId == "ordered-" + LastBreathUnitCreationTriggerId(context)
            && item.ControllerId == context.ControllerId && item.SourceObjectId == context.Source.ObjectId
            && item.CardNo == context.CardNo && item.EffectKind == context.Kind
            && item.TargetObjectIds is { Count: 0 } && item.TargetGenerations is null;

    internal static bool ValidLastBreathUnitCreationTrigger(MatchState state, TriggerQueueItemState item)
        => item.LastBreathUnitCreation is { } context && ValidLastBreathUnitCreation(state, context)
            && item.TriggerId == LastBreathUnitCreationTriggerId(context)
            && item.ControllerId == context.ControllerId && item.SourceObjectId == context.Source.ObjectId
            && item.SourceCardNo == context.CardNo && item.EffectKind == context.Kind
            && item.TriggeredByEventKind == TriggerTimings.UnitDestroyed;

    private static TriggerQueueItemState LastBreathUnitCreationTrigger(LastBreathUnitCreationContext context)
        => new(LastBreathUnitCreationTriggerId(context), context.ControllerId, context.Source.ObjectId,
            context.Kind, TriggerTimings.UnitDestroyed) { SourceCardNo = context.CardNo, LastBreathUnitCreation = context };
}
