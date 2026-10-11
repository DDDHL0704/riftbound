using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public sealed record AttachedRuleTextState(ObjectBinding Unit, string CardNo, long Revision);
public sealed record RuleTextInstanceBinding(string CardNo, ObjectBinding Unit, ObjectBinding Equipment, long Revision);
internal sealed record RuleTextInstance(string Id, string CardNo, RuleTextInstanceBinding? Origin = null);

// Enumerate printed text sets, never the host's materialized tags, temporary
// effects, or text granted by another armament (current core 477/719).
internal static class AttachedRuleTextRules
{
    internal static bool CopiesPrintedText(string? no) => OfficialArmamentCatalog.Find(no)?.CopiesHostPrintedRulesText == true;
    internal static string? Face(CardObjectState card) => card.CardNo ?? card.TokenFactoryCardNo;
    internal static bool Field(MatchState state, string id) => Field(state.PlayerZones, id);
    private static bool Field(IReadOnlyDictionary<string, PlayerZones> zones, string id) => zones.Values
        .Any(z => z.Base.Contains(id, StringComparer.Ordinal) || z.Battlefields.Contains(id, StringComparer.Ordinal));

    internal static bool Bound(MatchState state, CardObjectState gear)
        => Bound(state.PlayerZones, state.CardObjects, gear);

    private static bool Bound(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState gear)
        => gear.AttachedRuleText is { Unit: not null } copy && copy.Revision > 0 && copy.Revision == gear.AttachmentRevision
            && CopiesPrintedText(gear.CardNo)
            && gear.Tags.Contains(CardObjectTags.EquipmentCard) && Field(zones, gear.ObjectId)
            && gear.AttachedToObjectId == copy.Unit.ObjectId
            && cards.TryGetValue(copy.Unit.ObjectId, out var unit) && unit.ObjectGeneration == copy.Unit.Generation
            && unit.Tags.Contains(CardObjectTags.UnitCard)
            && Field(zones, unit.ObjectId) && Face(unit) == copy.CardNo && CopyCharacteristics.HasFace(copy.CardNo);

    internal static IEnumerable<RuleTextInstance> Copies(MatchState state, CardObjectState unit)
        => Copies(state.PlayerZones, state.CardObjects, unit);

    private static IEnumerable<RuleTextInstance> Copies(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState unit)
    {
        if (unit.IsFaceDown || unit.Tags.Contains(CardObjectTags.Standby)
            || !unit.Tags.Contains(CardObjectTags.UnitCard) || !Field(zones, unit.ObjectId)) yield break;
        foreach (var gear in cards.Values.Where(c => c.AttachedToObjectId == unit.ObjectId)
            .OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (!Bound(zones, cards, gear) || gear.IsFaceDown || gear.Tags.Contains(CardObjectTags.Standby)) continue;
            var copy = gear.AttachedRuleText!;
            var origin = new RuleTextInstanceBinding(copy.CardNo, copy.Unit, new(gear.ObjectId, gear.ObjectGeneration), copy.Revision);
            yield return new($"ATTACHED:{gear.ObjectId}:{gear.ObjectGeneration}:{copy.Revision}", copy.CardNo, origin);
        }
    }

    internal static IEnumerable<RuleTextInstance> Instances(MatchState state, CardObjectState unit)
        => Instances(state.PlayerZones, state.CardObjects, unit);

    internal static IEnumerable<RuleTextInstance> Instances(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState unit)
    {
        if (Face(unit) is { } no) yield return new($"PRINTED:{unit.ObjectId}:{unit.ObjectGeneration}", no);
        foreach (var copy in Copies(zones, cards, unit)) yield return copy;
    }

    internal static IEnumerable<RuleTextInstance> ActivatedInstances(MatchState state, CardObjectState source,
        P4ActivatedAbilityDefinition ability)
        => Instances(state, source).Where(i => (i.Origin is null || ability.SupportsCopiedRuleText)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, i.CardNo));

    internal static bool SelectActivatedInstance(MatchState state, CardObjectState source, P4ActivatedAbilityDefinition ability,
        string? id, out RuleTextInstance instance)
    {
        instance = ActivatedInstances(state, source, ability).FirstOrDefault(i =>
            string.IsNullOrWhiteSpace(id) ? i.Origin is null : i.Id == id)!;
        return instance is not null;
    }

    internal static string[] PrintedKeywords(string no)
        => CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)
            ? behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : P6TokenFactoryCatalog.TryGetByCardNo(no, out var token)
                ? token.CreateObject("definition", "P1", "P1").Tags.ToArray() : [];

    internal static bool ValidState(MatchState state, CardObjectState gear)
    {
        if (gear.AttachmentRevision < 0 || gear.AttachmentRevision > 0 && !CopiesPrintedText(gear.CardNo)) return false;
        if (gear.AttachedRuleText is not null) return Bound(state, gear);
        return !CopiesPrintedText(gear.CardNo) || gear.AttachedToObjectId is null || !Field(state, gear.ObjectId);
    }

    // A captured ability remains independent when the granting equipment leaves.
    // A new attachment/version cannot retroactively become its source.
    internal static bool ValidCaptured(MatchState state, RuleTextInstanceBinding? origin,
        string unitId, long generation, string cardNo)
    {
        if (origin is null) return true;
        if (origin.Unit is null || origin.Equipment is null || origin.CardNo != cardNo || origin.Unit.ObjectId != unitId || origin.Unit.Generation != generation
            || generation < 0 || origin.Revision <= 0 || origin.Equipment.Generation < 0
            || origin.Equipment.ObjectId == unitId || !CopyCharacteristics.HasFace(cardNo)
            || !state.CardObjects.TryGetValue(origin.Equipment.ObjectId, out var gear)
            || origin.Equipment.Generation > gear.ObjectGeneration || !CopiesPrintedText(gear.CardNo)) return false;
        if (gear.ObjectGeneration == origin.Equipment.Generation && origin.Revision > gear.AttachmentRevision) return false;
        return !state.CardObjects.TryGetValue(unitId, out var unit) || generation < unit.ObjectGeneration
            || generation == unit.ObjectGeneration && Face(unit) == cardNo;
    }
}
