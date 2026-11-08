using Riftbound.Contracts;

namespace Riftbound.Engine;

// A lower box belongs to the equipped unit. Keep the granting equipment and
// the host's captured face separate from a copied printed-text instance.
internal static class ArmamentRuleTextRules
{
    internal static IEnumerable<RuleTextInstance> Instances(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState host, Func<string, bool> supported)
    {
        bool Field(string id) => zones.Values.Any(z => z.Base.Contains(id) || z.Battlefields.Contains(id));
        if (host.IsFaceDown || !host.Tags.Contains(CardObjectTags.UnitCard) || !Field(host.ObjectId)) yield break;
        foreach (var gear in cards.Values.Where(g => !g.IsFaceDown && g.Tags.Contains(CardObjectTags.EquipmentCard)
            && g.AttachedToObjectId == host.ObjectId && Field(g.ObjectId) && g.CardNo is not null && supported(g.CardNo))
            .OrderBy(g => g.ObjectId, StringComparer.Ordinal)) {
            var origin = new RuleTextInstanceBinding(gear.CardNo!, new(host.ObjectId, host.ObjectGeneration),
                new(gear.ObjectId, gear.ObjectGeneration), gear.AttachmentRevision, 0, host.CopyFaceTransitions?.Count ?? 0,
                ArmamentGranted: true, ArmamentHostCardNo: AttachedRuleTextRules.Face(host),
                ArmamentEquipmentFactory: TokenObjectRules.IsToken(gear) ? gear.TokenFactoryCardNo ?? gear.CardNo : null);
            yield return new($"ARMAMENT:{gear.ObjectId}:{gear.ObjectGeneration}", gear.CardNo!, origin);
        }
    }

    internal static bool ValidCaptured(MatchState state, RuleTextInstanceBinding origin, string hostId,
        long generation, string no, Func<string, bool> supported)
    {
        if (!origin.ArmamentGranted || origin.CardNo != no || !supported(no)
            || origin.Unit is not { Generation: >= 0 } unit || unit.ObjectId != hostId || unit.Generation != generation
            || origin.Equipment is not { Generation: >= 0 } equipment || equipment.ObjectId == hostId
            || origin.Revision < 0 || origin.CapturedTick is not >= 0 || origin.CapturedTick > state.Tick
            || origin.FaceRevision is not >= 0 || string.IsNullOrWhiteSpace(origin.ArmamentHostCardNo)
            || !CopyCharacteristics.HasFace(origin.ArmamentHostCardNo)) return false;
        if (state.CardObjects.TryGetValue(equipment.ObjectId, out var gear)) {
            if (gear.ObjectGeneration < equipment.Generation
                || gear.ObjectGeneration == equipment.Generation && (gear.CardNo != no
                    || gear.AttachmentRevision < origin.Revision
                    || (TokenObjectRules.IsToken(gear) ? gear.TokenFactoryCardNo ?? gear.CardNo : null) != origin.ArmamentEquipmentFactory)) return false;
        } else if (origin.ArmamentEquipmentFactory is null
            || !P6TokenFactoryCatalog.TryGetByCardNo(origin.ArmamentEquipmentFactory, out _)) return false;
        return !state.CardObjects.TryGetValue(hostId, out var host) || host.ObjectGeneration > generation
            || host.ObjectGeneration == generation
                && CopyFaceTimeline.FaceAt(host, origin.FaceRevision, origin.CapturedTick) == origin.ArmamentHostCardNo;
    }
}
