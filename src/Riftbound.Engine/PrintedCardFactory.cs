using Riftbound.CardCatalog;

namespace Riftbound.Engine;

// A real card remains identifiable outside play. Recreate its printed form so
// damage, control, attachments and temporary effects do not survive CN 124.1.
internal static class PrintedCardFactory
{
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .ToDictionary(card => card.CardNo, StringComparer.Ordinal));

    internal static bool TryRestoreOutsidePlay(CardObjectState previous, string owner, out CardObjectState restored)
    {
        restored = previous;
        if (string.IsNullOrWhiteSpace(previous.CardNo) || P6TokenFactoryCatalog.IsTokenFactory(previous.CardNo)
            || previous.Tags.Contains("映像", StringComparer.Ordinal)
            || !Cards.Value.TryGetValue(previous.CardNo, out var card)) return false;
        restored = Create(previous.ObjectId, owner, card);
        return true;
    }

    internal static CardObjectState Create(string objectId, string playerId, OfficialCard card)
    {
        return new CardObjectState(
            objectId,
            power: Math.Max(0, card.Power ?? 0),
            tags: OfficialCardTags(card),
            manaCost: Math.Max(0, card.Energy ?? 0),
            cardNo: card.CardNo,
            ownerId: playerId,
            controllerId: card.CardCategoryName == "战场" ? null : playerId);
    }

    private static IReadOnlyList<string> OfficialCardTags(OfficialCard card)
    {
        var tags = new List<string>();
        if (card.CardCategoryName.Contains("单位", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.UnitCard);
        }
        else if (card.CardCategoryName.Contains("装备", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.EquipmentCard);
        }
        else if (card.CardCategoryName.Contains("法术", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.SpellCard);
        }
        else if (string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
        {
            tags.Add(CardObjectTags.RuneCard);
        }
        else if (string.Equals(card.CardCategoryName, "传奇", StringComparison.Ordinal))
        {
            tags.Add("CARD_TYPE:LEGEND");
        }
        else if (string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
        {
            tags.Add("CARD_TYPE:BATTLEFIELD");
        }

        if (!string.IsNullOrWhiteSpace(card.CardCategoryName))
        {
            tags.Add($"CARD_CATEGORY:{card.CardCategoryName.Trim()}");
        }

        if (string.Equals(card.CardCategoryName, "英雄单位", StringComparison.Ordinal))
        {
            tags.Add("CARD_TYPE:HERO");
        }

        if (!string.IsNullOrWhiteSpace(card.Hero))
        {
            tags.Add($"HERO:{card.Hero}");
        }

        tags.AddRange(card.CardColorList
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => $"COLOR:{color.Trim()}"));
        tags.AddRange(card.Tag.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return tags.Distinct(StringComparer.Ordinal).ToArray();
    }

}
