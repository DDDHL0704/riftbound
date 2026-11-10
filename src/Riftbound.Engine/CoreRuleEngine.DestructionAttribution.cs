using Riftbound.CardCatalog;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record DestructionAttribution(ObjectBinding Source, string CardNo, string ControllerId,
    string InstanceId, string ResponsiblePlayerId, bool FromSpell)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CapturedDestructionSourceIdentity? CapturedSource { get; init; }
}
public sealed record CapturedDestructionSourceIdentity(long FaceRevision, long CapturedTick,
    string? TokenFactoryCardNo, RuleTextInstanceBinding? Origin);

public sealed partial class CoreRuleEngine
{
    private sealed record AppliedDamageFact(ObjectBinding Target, DestructionAttribution? Attribution,
        CapturedSpellInstructionSource? ReflexiveSource = null);
    private static readonly Lazy<HashSet<string>> PrintedSpellSources = new(() => OfficialCardCatalog.LoadDefaultAsync()
        .GetAwaiter().GetResult().Cards.Where(c => c.CardCategoryNameList.Any(category => category.Contains("法术", StringComparison.Ordinal)))
        .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));

    private static CardObjectState? AttributionSource(string id)
        => RuleChoices.Value?.ResolutionState?.CardObjects.GetValueOrDefault(id)
            ?? RuleChoices.Value?.Origin.CardObjects.GetValueOrDefault(id);

    private static DestructionAttribution? StackDestructionAttribution(StackItemState item, string? responsible = null)
    {
        if (item.LastBreathBattlefieldDamage is { } area)
            return new(area.Source, area.SourceCardNo, area.ControllerId, item.StackItemId,
                responsible ?? area.ControllerId, false);
        if (item.DamageDestruction is { } damage)
            return new(damage.Source, damage.CardNo, damage.ControllerId, item.StackItemId,
                responsible ?? damage.ControllerId, true);
        // A captured skill belongs to its original field object even after
        // that physical card moves to a private zone or returns to the field.
        if (item.FieldContext is { } field)
            return new(new(item.SourceObjectId, field.SourceGeneration), field.CardNo, item.ControllerId,
                item.StackItemId, responsible ?? item.ControllerId, false);
        if (item.UnitEvent is { } captured)
            return new(new(item.SourceObjectId, captured.SourceGeneration), captured.CardNo, item.ControllerId,
                item.StackItemId, responsible ?? item.ControllerId, false) {
                    CapturedSource = new(captured.FaceRevision, captured.CapturedTick, captured.TokenFactoryCardNo, captured.Origin) };
        var source = AttributionSource(item.SourceObjectId);
        if (source is null || string.IsNullOrWhiteSpace(item.CardNo)) return null;
        return new(new(source.ObjectId, source.ObjectGeneration), item.CardNo, item.ControllerId,
            item.StackItemId, responsible ?? item.ControllerId, PrintedSpellSources.Value.Contains(item.CardNo));
    }

    // The executed damage boundary supplies the responsible player. CN418.6.b.3:
    // a named unit's damage is not damage from the spell instructing that unit.
    private static void CaptureAppliedDamage(string sourceId, string unitSourceId, DamageApplicationResult damage)
    {
        if (damage.DamageAmount <= 0 || damage.Target is null || RuleChoices.Value is not { } frame) return;
        DestructionAttribution? attribution;
        if (!string.IsNullOrEmpty(unitSourceId) || frame.ResolutionItem is null)
        {
            var source = AttributionSource(string.IsNullOrEmpty(unitSourceId) ? sourceId : unitSourceId);
            attribution = source?.CardNo is { } no ? new(new(source.ObjectId, source.ObjectGeneration), no,
                damage.ControllerId, frame.ResolutionItem?.StackItemId ?? "COMBAT", damage.ControllerId, false) : null;
        }
        else attribution = StackDestructionAttribution(frame.ResolutionItem, damage.ControllerId);
        frame.AppliedDamages.Add(new(damage.Target, attribution, CaptureSpellInstructionSource(frame.ResolutionItem)));
    }

    private static DestructionAttribution? ActualDestructionAttribution(StackItemState item,
        CapturedUnitDeath death, string? reason, string? responsiblePlayerId = null)
        => reason == "LETHAL_DAMAGE"
            ? RuleChoices.Value!.AppliedDamages.LastOrDefault(d => d.Target.ObjectId == death.Unit.ObjectId
                && d.Target.Generation == death.Unit.Generation)?.Attribution
            : StackDestructionAttribution(item, responsiblePlayerId);

    internal static bool ValidDestructionAttribution(MatchState state, DestructionAttribution c)
        => c.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(c.Source.ObjectId)
            && !string.IsNullOrWhiteSpace(c.CardNo) && !string.IsNullOrWhiteSpace(c.InstanceId)
            && !string.IsNullOrWhiteSpace(c.ControllerId) && !string.IsNullOrWhiteSpace(c.ResponsiblePlayerId)
            && state.Seats.ContainsKey(c.ControllerId) && state.Seats.ContainsKey(c.ResponsiblePlayerId)
            && (!c.FromSpell || PrintedSpellSources.Value.Contains(c.CardNo)
                && (c.ControllerId == c.ResponsiblePlayerId
                    || CardBehaviorRegistry.TryGetByCardNo(c.CardNo, out var behavior) && behavior.EachPlayerDestroysOwnUnit)
                && state.CardObjects.ContainsKey(c.Source.ObjectId))
            && (c.CapturedSource is { } identity
                ? !c.FromSpell && identity.FaceRevision >= 0 && identity.CapturedTick > 0 && identity.CapturedTick <= state.Tick
                    && (identity.Origin is null
                        ? PredictBindingMatches(state, c.Source, c.CardNo, identity.FaceRevision, identity.CapturedTick, identity.TokenFactoryCardNo)
                        : AttachedRuleTextRules.ValidCaptured(state, identity.Origin, c.Source.ObjectId, c.Source.Generation, c.CardNo))
                : !state.CardObjects.TryGetValue(c.Source.ObjectId, out var source)
                    || source.ObjectGeneration >= c.Source.Generation
                        && (source.ObjectGeneration != c.Source.Generation || source.CardNo == c.CardNo));
}
