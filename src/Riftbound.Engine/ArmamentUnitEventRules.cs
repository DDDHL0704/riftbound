using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore] public bool GrantedByArmament { get; init; }
}

// Lower-box text is granted to the host. It is neither the equipment's own
// printed text nor an extra printed-text instance for copies such as Song.
internal static class ArmamentUnitEventRules
{
    internal const string FixedDamageEffect = "GRANTED_FIXED_DAMAGE_HERE";
    internal const string AreaDamageEffect = "DAMAGE_ENEMIES_HERE";
    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> Specs = new(() =>
        OfficialArmamentCatalog.All.Select(definition => {
            var match = Regex.Match(definition.EffectText.Trim(),
                @"^当我进攻或防守时[，,]\s*对此处的一名敌方单位造成(?<amount>[1-9][0-9]*)点伤害[。.]?$",
                RegexOptions.CultureInvariant);
            var area = Regex.Match(definition.EffectText.Trim(),
                @"^当我进攻(?:及|或)防守时[，,]\s*对此处的所有敌方单位各造成(?<amount>[1-9][0-9]*)点伤害[。.]?$",
                RegexOptions.CultureInvariant);
            var spec = match.Success
                ? new UnitEventAbilitySpec("ATTACK_OR_DEFEND", FixedDamageEffect, int.Parse(match.Groups["amount"].Value))
                : area.Success ? new UnitEventAbilitySpec("ATTACK_OR_DEFEND", AreaDamageEffect, int.Parse(area.Groups["amount"].Value)) : null;
            if (spec is null && TriggerParser.Parse(definition.EffectText).FirstOrDefault(t =>
                    t.Kind == TriggerKinds.UnitBattlefieldHeldCreateEquipment && t.Timing == TriggerTimings.BattlefieldHeld
                    && t.TargetScope == TriggerTargetScopes.SourceUnit && t.CreatedTokenCount > 0
                    && t.CreatedTokenExhausted == true && t.CreatedTokenDestination == TriggerTokenDestinations.OwnerBase) is { } held
                && P6TokenFactoryCatalog.GetAll().FirstOrDefault(f => f.Tags.Contains(CardObjectTags.EquipmentCard)
                    && f.TokenFamilyName == held.CreatedTokenName) is { } factory)
                spec = new("HOLD", "CREATE_EQUIPMENT_TOKEN", held.CreatedTokenCount!.Value,
                    CreatedTokenFactoryCardNo: factory.CardNo);
            return (definition.CardNo, Spec: spec is null ? null : spec with { GrantedByArmament = true });
        }).Where(row => row.Spec is not null).ToDictionary(row => row.CardNo, row => row.Spec!, StringComparer.Ordinal));

    internal static UnitEventAbilitySpec? Spec(string no) => Specs.Value.GetValueOrDefault(no);

    internal static IEnumerable<RuleTextInstance> Instances(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState host)
    {
        bool Field(string id) => zones.Values.Any(z => z.Base.Contains(id) || z.Battlefields.Contains(id));
        if (host.IsFaceDown || !host.Tags.Contains(CardObjectTags.UnitCard) || !Field(host.ObjectId)) yield break;
        foreach (var gear in cards.Values.Where(g => !g.IsFaceDown && g.Tags.Contains(CardObjectTags.EquipmentCard)
            && g.AttachedToObjectId == host.ObjectId && Field(g.ObjectId) && g.CardNo is not null && Spec(g.CardNo) is not null)
            .OrderBy(g => g.ObjectId, StringComparer.Ordinal)) {
            var origin = new RuleTextInstanceBinding(gear.CardNo!, new(host.ObjectId, host.ObjectGeneration),
                new(gear.ObjectId, gear.ObjectGeneration), gear.AttachmentRevision, 0, host.CopyFaceTransitions?.Count ?? 0,
                ArmamentGranted: true, ArmamentHostCardNo: AttachedRuleTextRules.Face(host),
                ArmamentEquipmentFactory: TokenObjectRules.IsToken(gear) ? gear.TokenFactoryCardNo ?? gear.CardNo : null);
            yield return new($"ARMAMENT:{gear.ObjectId}:{gear.ObjectGeneration}", gear.CardNo!, origin);
        }
    }

    internal static bool ValidCaptured(MatchState state, RuleTextInstanceBinding origin, string hostId,
        long generation, string no)
    {
        if (!origin.ArmamentGranted || origin.CardNo != no || Spec(no) is null
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

    internal static string CaptureHash(UnitEventContext context) => MatchStateHasher.HashValue(new {
        context.CardNo, context.SourceGeneration, context.FaceRevision, context.CapturedTick,
        context.Timing, context.TokenFactoryCardNo, context.Origin
    });
}
