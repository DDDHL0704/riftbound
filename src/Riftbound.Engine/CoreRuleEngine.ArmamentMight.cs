using Riftbound.CardCatalog;
using Riftbound.Contracts;
namespace Riftbound.Engine;

internal sealed record ArmamentMightContribution(string SourceId, string SourceCardNo, int BaseBonus, int Multiplier)
{
    public int Delta => BaseBonus * Multiplier;
}

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<ArmamentMightContribution> ArmamentMightContributions(
        MatchState state, IReadOnlyDictionary<string, PlayerZones> zones, CardObjectState host)
    {
        if (host.IsFaceDown || !host.Tags.Contains(CardObjectTags.UnitCard)
            || !IsObjectOnField(zones, host.ObjectId)) return [];
        var multiplier = CardStaticAbilitySpecRules.TryGetStaticAbility(host.CardNo,
            a => a.Kind == StaticAbilityKinds.AttachedArmamentBaseMightMultiplier && a.Amount > 0, out var ability) ? ability.Amount : 1;
        return state.CardObjects.Values.Where(gear => gear.ObjectId != host.ObjectId && gear.AttachedToObjectId == host.ObjectId
            && !gear.IsFaceDown && gear.Tags.Contains(CardObjectTags.EquipmentCard)
            && IsObjectOnField(zones, gear.ObjectId))
            .OrderBy(gear => gear.ObjectId, StringComparer.Ordinal)
            .Select(gear => (gear, definition: OfficialArmamentCatalog.Find(gear.CardNo)))
            .Where(pair => pair.definition is not null)
            .Select(pair => new ArmamentMightContribution(pair.gear.ObjectId, pair.gear.CardNo!, pair.definition!.BaseMightBonus, multiplier)).ToArray();
    }
}
