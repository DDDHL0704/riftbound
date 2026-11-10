using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<EquipmentKeywordGrantSpec>>> EquipmentKeywordGrants = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(
            card => card.CardNo, card => EquipmentKeywordGrantParser.Parse(card.CardEffect), StringComparer.Ordinal));

    private static bool EquipmentHasKeyword(MatchState state, CardObjectState card, string keyword)
        => card.Tags.Contains(CardObjectTags.EquipmentCard) && !card.IsFaceDown
            && (card.Tags.Contains(keyword) || AttachedRuleTextRules.Instances(state, card).Any(instance =>
                CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior)
                    && ParseDelimitedValues(behavior.SourceEquipmentTags).Contains(keyword)));

    private static bool HasFriendlyEquipmentKeywordHere(MatchState state, string player, string battlefield, string keyword)
        => state.CardObjects.Values.Any(card => EquipmentHasKeyword(state, card, keyword)
            && EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) == player
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
            && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == battlefield);

    private static CardBehaviorDefinition EquipmentGrantedKeywordBehavior(MatchState state, string player,
        string source, CardBehaviorDefinition behavior)
    {
        if (!behavior.PlaysSourceToBaseAsEquipment || !state.CardObjects.TryGetValue(source, out var equipment)) return behavior;
        if (AllPlacesEquipmentGrantOrigins(state.PlayerZones, state.CardObjects, equipment, CardEquipmentKeywordNames.Agile).Any())
            behavior = behavior with { CanPlayDuringPriority = true, CanPlayDuringSpellDuel = true };
        if (equipment.OwnerId != player) return behavior;
        var required = ParseDelimitedValues(behavior.SourceEquipmentTags);
        var swift = PublicStaticAuraSourceObjectIds(state.PlayerZones).Any(id =>
            state.CardObjects.TryGetValue(id, out var grantor) && IsFaceUpNonStandbyUnit(grantor)
            && EffectiveFieldControllerId(state.PlayerZones, id, grantor) == player
            && AttachedRuleTextRules.Instances(state, grantor).Any(instance =>
                EquipmentKeywordGrants.Value.TryGetValue(instance.CardNo, out var grants)
                && grants.Any(grant => !grant.AllPlaces && required.Contains(grant.RequiredKeyword)
                    && grant.GrantedKeyword == CardPermissionKeywordNames.Swift)));
        return swift ? behavior with { CanPlayDuringSpellDuel = true } : behavior;
    }
}
