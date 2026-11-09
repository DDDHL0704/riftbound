namespace Riftbound.Engine;

internal static class FieldObjectTypeRules
{
    // CN 178: acquiring another type never removes the permissions of UNIT.
    // Untyped objects remain supported by the engine's small rule fixtures.
    internal static bool IsVisibleUnit(CardObjectState card)
        => !card.IsFaceDown && HasUnitType(card);

    // Type and visibility are distinct: defensive cleanup can inspect the
    // numeric state of a malformed hidden fixture without enabling targeting.
    internal static bool HasUnitType(CardObjectState card)
        => card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !card.Tags.Any(tag => tag is CardObjectTags.EquipmentCard or CardObjectTags.SpellCard
                    or CardObjectTags.RuneCard or "CARD_TYPE:BATTLEFIELD" or "CARD_TYPE:LEGEND");

    // CN 149.3 / 323.6: loose non-unit equipment cannot stay in another
    // controller's base. Deploy/standby only exempt its battlefield presence.
    // Use the same predicate for actual cleanup and the pending-task projection.
    internal static bool RequiresUnattachedEquipmentRecall(CardObjectState card, ObjectLocationState? location)
        => card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && string.IsNullOrWhiteSpace(card.AttachedToObjectId)
            && !card.IsFaceDown
            && (location?.Zone == "BASE"
                ? (card.ControllerId ?? card.OwnerId) is { Length: > 0 } controller && location.PlayerId != controller
                : !card.Tags.Contains(CardEquipmentKeywordNames.Deploy, StringComparer.Ordinal)
                    && !StandbyRules.AllowsBattlefieldEquipment(card, location));
}
