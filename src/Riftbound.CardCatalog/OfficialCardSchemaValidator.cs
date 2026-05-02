namespace Riftbound.CardCatalog;

public sealed record CatalogSchemaValidationResult(
    int OfficialEntries,
    bool IsValid,
    IReadOnlyList<CatalogSchemaViolation> Violations);

public sealed record CatalogSchemaViolation(
    string CardNo,
    string Field,
    string Reason);

public static class OfficialCardSchemaValidator
{
    private static readonly HashSet<string> KnownCategories = new(StringComparer.Ordinal)
    {
        "单位",
        "英雄单位",
        "法术",
        "装备",
        "符文",
        "传奇",
        "战场",
        "专属单位",
        "专属法术",
        "专属装备",
        "指示物单位",
        "指示物装备",
        "指示物战场"
    };

    public static CatalogSchemaValidationResult Validate(OfficialCardCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var violations = new List<CatalogSchemaViolation>();
        ValidateCatalogHeader(catalog, violations);

        var seenIds = new HashSet<long>();
        var seenCardNos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in catalog.Cards)
        {
            ValidateCard(card, violations);

            if (!seenIds.Add(card.Id))
            {
                violations.Add(new CatalogSchemaViolation(card.CardNo, nameof(card.Id), "Official id must be unique."));
            }

            if (!seenCardNos.Add(card.CardNo))
            {
                violations.Add(new CatalogSchemaViolation(card.CardNo, nameof(card.CardNo), "Official card number must be unique."));
            }
        }

        return new CatalogSchemaValidationResult(
            catalog.Cards.Count,
            violations.Count == 0,
            violations.OrderBy(violation => violation.CardNo, StringComparer.Ordinal)
                .ThenBy(violation => violation.Field, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ValidateCatalogHeader(
        OfficialCardCatalog catalog,
        List<CatalogSchemaViolation> violations)
    {
        AddIfBlank(violations, "_catalog", nameof(catalog.Source), catalog.Source);
        AddIfBlank(violations, "_catalog", nameof(catalog.Api), catalog.Api);
        AddIfBlank(violations, "_catalog", nameof(catalog.FetchedAt), catalog.FetchedAt);

        if (catalog.Total != catalog.Cards.Count)
        {
            violations.Add(new CatalogSchemaViolation(
                "_catalog",
                nameof(catalog.Total),
                $"Catalog total {catalog.Total} must match loaded card count {catalog.Cards.Count}."));
        }
    }

    private static void ValidateCard(
        OfficialCard card,
        List<CatalogSchemaViolation> violations)
    {
        var cardNo = string.IsNullOrWhiteSpace(card.CardNo) ? $"id:{card.Id}" : card.CardNo;

        if (card.Id <= 0)
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.Id), "Official id must be positive."));
        }

        AddIfBlank(violations, cardNo, nameof(card.CardCategory), card.CardCategory);
        AddIfBlank(violations, cardNo, nameof(card.CardCategoryName), card.CardCategoryName);
        AddIfBlank(violations, cardNo, nameof(card.CardNo), card.CardNo);
        AddIfBlank(violations, cardNo, nameof(card.CardName), card.CardName);
        AddIfBlank(violations, cardNo, nameof(card.Rarity), card.Rarity);
        AddIfBlank(violations, cardNo, nameof(card.RarityName), card.RarityName);
        AddIfBlank(violations, cardNo, nameof(card.ExtendRarity), card.ExtendRarity);
        AddIfBlank(violations, cardNo, nameof(card.ExtendRarityName), card.ExtendRarityName);
        AddIfBlank(violations, cardNo, nameof(card.FrontImage), card.FrontImage);

        if (!KnownCategories.Contains(card.CardCategoryName))
        {
            violations.Add(new CatalogSchemaViolation(
                cardNo,
                nameof(card.CardCategoryName),
                $"Unknown official category '{card.CardCategoryName}'."));
        }

        if (card.CardColorList.Count == 0)
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.CardColorList), "At least one color value is required."));
        }

        if (card.ListSort is null)
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.ListSort), "List sort must be present."));
        }

        if (card.Status is null)
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.Status), "Official status must be present."));
        }

        if (!Uri.TryCreate(card.FrontImage, UriKind.Absolute, out _))
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.FrontImage), "Front image must be an absolute URL."));
        }

        if (!string.IsNullOrWhiteSpace(card.BackImage)
            && !Uri.TryCreate(card.BackImage, UriKind.Absolute, out _))
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.BackImage), "Back image must be empty or an absolute URL."));
        }

        if (RequiresEnergy(card) && card.Energy is null)
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.Energy), "Playable non-token cards must include an energy cost."));
        }

        if (card.CardCategoryName.Contains("单位", StringComparison.Ordinal) && card.Power is null)
        {
            violations.Add(new CatalogSchemaViolation(cardNo, nameof(card.Power), "Unit cards must include a power value."));
        }
    }

    private static bool RequiresEnergy(OfficialCard card)
    {
        if (card.CardCategoryName.StartsWith("指示物", StringComparison.Ordinal))
        {
            return false;
        }

        return card.CardCategoryName.Contains("单位", StringComparison.Ordinal)
            || card.CardCategoryName.Contains("法术", StringComparison.Ordinal)
            || card.CardCategoryName.Contains("装备", StringComparison.Ordinal);
    }

    private static void AddIfBlank(
        List<CatalogSchemaViolation> violations,
        string cardNo,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            violations.Add(new CatalogSchemaViolation(cardNo, field, "Required field must not be blank."));
        }
    }
}
