using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public sealed record DestructionAttribution(ObjectBinding Source, string CardNo, string ControllerId,
    string InstanceId, string ResponsiblePlayerId, bool FromSpell);

public sealed partial class CoreRuleEngine
{
    private sealed record AppliedDamageFact(ObjectBinding Target, DestructionAttribution? Attribution);
    private static readonly Lazy<HashSet<string>> PrintedSpellSources = new(() => OfficialCardCatalog.LoadDefaultAsync()
        .GetAwaiter().GetResult().Cards.Where(c => c.CardCategoryNameList.Any(category => category.Contains("法术", StringComparison.Ordinal)))
        .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));

    private static CardObjectState? AttributionSource(string id)
        => RuleChoices.Value?.ResolutionState?.CardObjects.GetValueOrDefault(id)
            ?? RuleChoices.Value?.Origin.CardObjects.GetValueOrDefault(id);

    private static DestructionAttribution? StackDestructionAttribution(StackItemState item, string? responsible = null)
    {
        if (item.DamageDestruction is { } damage)
            return new(damage.Source, damage.CardNo, damage.ControllerId, item.StackItemId,
                responsible ?? damage.ControllerId, true);
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
        frame.AppliedDamages.Add(new(damage.Target, attribution));
    }

    private static DestructionAttribution? ActualDestructionAttribution(StackItemState item,
        CapturedUnitDeath death, string? reason)
        => reason == "LETHAL_DAMAGE"
            ? RuleChoices.Value!.AppliedDamages.LastOrDefault(d => d.Target.ObjectId == death.Unit.ObjectId
                && d.Target.Generation == death.Unit.Generation)?.Attribution
            : StackDestructionAttribution(item);

    internal static bool ValidDestructionAttribution(MatchState state, DestructionAttribution c)
        => c.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(c.Source.ObjectId)
            && !string.IsNullOrWhiteSpace(c.CardNo) && !string.IsNullOrWhiteSpace(c.InstanceId)
            && !string.IsNullOrWhiteSpace(c.ControllerId) && !string.IsNullOrWhiteSpace(c.ResponsiblePlayerId)
            && state.Seats.ContainsKey(c.ControllerId) && state.Seats.ContainsKey(c.ResponsiblePlayerId)
            && (!c.FromSpell || PrintedSpellSources.Value.Contains(c.CardNo) && c.ControllerId == c.ResponsiblePlayerId
                && state.CardObjects.ContainsKey(c.Source.ObjectId))
            && (!state.CardObjects.TryGetValue(c.Source.ObjectId, out var source)
                || source.ObjectGeneration >= c.Source.Generation
                    && (source.ObjectGeneration != c.Source.Generation || source.CardNo == c.CardNo));
}
