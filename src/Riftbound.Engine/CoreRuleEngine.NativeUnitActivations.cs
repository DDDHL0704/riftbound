using System.Text.Json.Serialization;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeUnitActivationContext(string AbilityId, ObjectBinding Source, string CardNo,
    string ControllerId, long AcquiredTick, long FaceRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TokenFactoryCardNo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CeasedSelfAbilitySource? CeasedSource = null);

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult BindNativeUnitActivationSource(MatchState before, ResolutionResult result,
        P4ActivatedAbilityDefinition ability)
    {
        if (!result.Accepted || !ability.BindsUnitSource) return result;
        var existing = before.StackItems.Select(item => item.StackItemId).ToHashSet(StringComparer.Ordinal);
        var stack = result.State.StackItems.Select(item => {
            if (existing.Contains(item.StackItemId) || item.EffectKind != ability.EffectKind) return item;
            if (!before.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                || !IsFaceUpNonStandbyUnit(source) || !IsObjectOnField(before.PlayerZones, source.ObjectId))
                throw new InvalidOperationException("A native unit activation must capture its field source.");
            return item with { NativeUnitActivation = new(ability.AbilityId,
                new(source.ObjectId, source.ObjectGeneration), item.CardNo!, item.ControllerId,
                result.State.Tick, source.CopyFaceTransitions?.Count ?? 0, source.TokenFactoryCardNo) };
        }).ToArray();
        var next = result.State with { StackItems = stack };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidNativeUnitActivation(MatchState state, StackItemState item)
    {
        var recognized = P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
            && ability.BindsUnitSource;
        if (item.NativeUnitActivation is not { } context) return !recognized;
        if (!recognized || context.AbilityId != ability.AbilityId || context.Source is not { Generation: >= 0 }
            || context.Source.ObjectId != item.SourceObjectId || context.ControllerId != item.ControllerId
            || string.IsNullOrWhiteSpace(context.ControllerId) || string.IsNullOrWhiteSpace(context.CardNo)
            || !state.Seats.ContainsKey(context.ControllerId) || context.CardNo != item.CardNo
            || !P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, context.CardNo)
            || !SelfAbilitySourceRules.Valid(state, context.Source, context.CardNo, context.FaceRevision,
                context.AcquiredTick, context.TokenFactoryCardNo, context.CeasedSource, usesPrintedFace: true)
            || item.TargetObjectIds.Count != ability.RequiredTargetCount
            || item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() != ability.RequiredTargetCount
            || (item.TargetObjectIds.Count == 0 ? item.TargetGenerations is not null
                : item.TargetGenerations is null || item.TargetGenerations.Count != item.TargetObjectIds.Count
                    || item.TargetObjectIds.Any(id => item.TargetGenerations.GetValueOrDefault(id, -1) < 0))) return false;
        var expected = new StackItemState($"STACK-{context.AcquiredTick}-{context.Source.ObjectId}-ABILITY",
            context.ControllerId, context.Source.ObjectId, ability.EffectKind, context.CardNo,
            item.TargetObjectIds, ability.DamageAmount, 1, []) {
                NativeUnitActivation = context, TargetGenerations = item.TargetGenerations,
                DepartedTokenTargets = item.DepartedTokenTargets };
        return MatchStateHasher.HashValue(item) == MatchStateHasher.HashValue(expected);
    }

    private static bool NativeUnitActivationSourceStillOnField(MatchState state, StackItemState item)
        => item.NativeUnitActivation is { } context
            && state.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
            && source.ObjectGeneration == context.Source.Generation && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId);

    private static StackResolutionResult RedactNativeUnitActivationSource(MatchState state, StackItemState item,
        StackResolutionResult result)
    {
        if (item.NativeUnitActivation is null) return result;
        var visible = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator);
        if (visible == item.SourceObjectId) return result;
        return result with { Events = result.Events.Select(ev => ev with { Payload = ev.Payload.ToDictionary(pair => pair.Key,
            pair => (pair.Key is "sourceObjectId" or "unitSourceObjectId") && Equals(pair.Value, item.SourceObjectId)
                ? (object?)visible : pair.Value) }).ToArray() };
    }
}
