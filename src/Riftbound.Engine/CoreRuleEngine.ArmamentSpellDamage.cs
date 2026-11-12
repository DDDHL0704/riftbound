using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string ArmamentSpellSkillDamageBonusKind = "ATTACHED_ARMAMENT_SPELL_SKILL_DAMAGE_BONUS";
    internal const string ArmamentDamageCaptureHashKey = "armamentDamageCaptureHash";
    internal const string DiscardedArmamentDamageHashKey = "discardedArmamentDamageCaptureHash";

    private static IEnumerable<DistributedDamageBonus> AttachedArmamentSpellSkillDamageBonuses(
        MatchState state, string controller)
    {
        foreach (var gear in state.CardObjects.Values.OrderBy(card => card.ObjectId, StringComparer.Ordinal))
        {
            var face = AttachedRuleTextRules.Face(gear);
            var amount = OfficialArmamentCatalog.Find(face)?.SpellSkillDamageBonus ?? 0;
            if (amount <= 0 || gear.IsFaceDown || !gear.Tags.Contains(CardObjectTags.EquipmentCard)
                || gear.AttachedToObjectId is not { } hostId
                || !IsObjectOnField(state.PlayerZones, gear.ObjectId)
                || !state.CardObjects.TryGetValue(hostId, out var host) || host.IsFaceDown
                || !(host.Tags.Contains(CardObjectTags.UnitCard) || host.Tags.Contains(CardObjectTags.EquipmentCard))
                || !IsObjectOnField(state.PlayerZones, hostId)
                || !IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, gear.ObjectId, hostId)
                || EffectiveFieldControllerId(state.PlayerZones, hostId, host) != controller) continue;
            yield return new(face!, ArmamentSpellSkillDamageBonusKind, amount,
                new(gear.ObjectId, gear.ObjectGeneration));
        }
    }

    internal static bool RequiresArmamentDamageHistory(DamageDistributionContext? context)
        => context?.InitialBonuses?.Any(bonus => bonus is { Kind: ArmamentSpellSkillDamageBonusKind }) == true;

    // A response can change current "here" and the eventual damage pool.
    // The immutable birth receipt records only the original captured inputs.
    internal static string ArmamentDamageCaptureHash(DamageDistributionContext context, string source, string controller)
        => MatchStateHasher.HashValue(new {
            context.CardNo, context.Source, context.InitialDamage, context.InitialBonuses,
            context.Origin, context.ReturnFocusPlayerId, sourceObjectId = source, controllerId = controller });
}
