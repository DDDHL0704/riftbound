using System.Globalization;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SelfMovementContext(ObjectBinding Source, long CapturedTick, long FaceRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceTokenFactoryCardNo = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CeasedSelfAbilitySource? CeasedSource { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal static bool ValidSelfMovement(MatchState state, StackItemState item)
    {
        var movesSelf = P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
            && ability.Kind == ActivatedAbilityKinds.SwiftMoveSelfToBase;
        if (item.SelfMovement is not { } c) return !movesSelf;
        return movesSelf && c.Source is not null && c.Source.ObjectId == item.SourceObjectId
            && c.CapturedTick > 0 && c.CapturedTick <= state.Tick && c.FaceRevision >= 0
            && item.StackItemId == $"STACK-{c.CapturedTick.ToString(CultureInfo.InvariantCulture)}-{item.SourceObjectId}-SELF-MOVE-BASE"
            && state.Seats.ContainsKey(item.ControllerId) && !item.SourceConfirmed
            && item.CardNo is not null && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, item.CardNo)
            && SelfAbilitySourceRules.Valid(state, c.Source, item.CardNo, c.FaceRevision, c.CapturedTick,
                c.SourceTokenFactoryCardNo, c.CeasedSource, c.Origin is null)
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.DamageAmount == 0
            && item.EffectRepeatCount == 1 && item.OptionalCosts.Count == 0 && item.RepeatExecutions is null
            && item.Destination.Length == 0 && !item.PermanentPlayTargetsPending
            && item.FieldContext is null && item.TargetedPowerAbility is null && item.DamageDistribution is null
            && (c.Origin is null
                || c.FaceRevision == c.Origin.FaceRevision && c.Origin.CapturedTick <= c.CapturedTick
                    && AttachedRuleTextRules.ValidCaptured(state, c.Origin, item.SourceObjectId, c.Source.Generation, item.CardNo));
    }
}
