namespace Riftbound.Engine;

internal static class FieldObjectTypeRules
{
    // CN 178: acquiring another type never removes the permissions of UNIT.
    // Untyped objects remain supported by the engine's small rule fixtures.
    internal static bool IsVisibleUnit(CardObjectState card)
        => !card.IsFaceDown && !card.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            && (card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !card.Tags.Any(tag => tag is CardObjectTags.EquipmentCard or CardObjectTags.SpellCard
                    or CardObjectTags.RuneCard or "CARD_TYPE:BATTLEFIELD" or "CARD_TYPE:LEGEND"));

    // CN 178.1.a.1.a / 323.7: battlefield cleanup recalls only non-unit equipment.
    internal static bool IsUnattachedNonUnitEquipment(CardObjectState card)
        => card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            && !card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && string.IsNullOrWhiteSpace(card.AttachedToObjectId)
            && !card.IsFaceDown && !card.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal);
}
