using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class StandbyCapacityRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal));

    internal static int Capacity(MatchState state, string player, string fieldId)
    {
        if (!BattlefieldLocalRules.ControlledBy(state, player).Any(f => f.ObjectId == fieldId)
            || BattlefieldLocalRules.Battlefield(state, fieldId) is not { } field) return 0;
        var capacity = 1 + (BattlefieldLocalRules.HasAbility(field,
            BattlefieldStaticAbilitySpecRules.IsBattlefieldExtraStandbyDestinationAbility) ? 1 : 0);
        foreach (var source in state.CardObjects.Values)
        {
            if (source.IsFaceDown || source.ObjectId == fieldId
                || !source.Tags.Contains(CardObjectTags.UnitCard) && !source.Tags.Contains(CardObjectTags.EquipmentCard)
                || !AttachedRuleTextRules.Field(state, source.ObjectId)
                || !state.ObjectLocations.TryGetValue(source.ObjectId, out var at)
                || at.Zone != "BATTLEFIELD" || at.BattlefieldObjectId != fieldId
                || source.ControllerId != player && !PlayerRelations.AreAllies(state, source.ControllerId, player)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                if (Cards.Value.TryGetValue(instance.CardNo, out var printed))
                    capacity += StandbyParser.ExtraCapacityForBattlefieldController(printed.CardEffect);
        }
        return capacity;
    }

    internal static string[] Occupants(MatchState state, string fieldId)
        => state.CardObjects.Values.Where(card => card.IsFaceDown && AttachedRuleTextRules.Field(state, card.ObjectId)
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var at)
            && at.Zone == "BATTLEFIELD" && at.BattlefieldObjectId == fieldId)
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
}
