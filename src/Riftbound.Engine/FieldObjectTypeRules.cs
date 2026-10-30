namespace Riftbound.Engine;

internal static class FieldObjectTypeRules
{
    // CN 178: acquiring another type never removes the permissions of UNIT.
    // Untyped objects remain supported by the engine's small rule fixtures.
    internal static bool IsVisibleUnit(CardObjectState card)
        => !card.IsFaceDown
            && (card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !card.Tags.Any(tag => tag is CardObjectTags.EquipmentCard or CardObjectTags.SpellCard
                    or CardObjectTags.RuneCard or "CARD_TYPE:BATTLEFIELD" or "CARD_TYPE:LEGEND"));

    // CN 178 / 323.6: cleanup recalls unattached non-unit equipment, except
    // deployed equipment (830.1.b.3) and equipment retained by standby play.
    // Use the same predicate for actual cleanup and the pending-task projection.
    internal static bool RequiresUnattachedEquipmentRecall(CardObjectState card, ObjectLocationState? location)
        => card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && string.IsNullOrWhiteSpace(card.AttachedToObjectId)
            && !card.IsFaceDown
            && !card.Tags.Contains(CardEquipmentKeywordNames.Deploy, StringComparer.Ordinal)
            && !StandbyRules.AllowsBattlefieldEquipment(card, location);
}
