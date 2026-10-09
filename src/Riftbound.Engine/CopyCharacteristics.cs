namespace Riftbound.Engine;

public static class TokenObjectRules
{
    // CardNo is the current face. An Image keeps its intrinsic token origin when
    // its face becomes a printed card; tags are changeable characteristics.
    public static bool IsToken(CardObjectState card) => card.TokenFactoryCardNo is not null
        || P6TokenFactoryCatalog.IsTokenFactory(card.CardNo);
}

internal static class CopyCharacteristics
{
    internal static bool HasFace(string? cardNo) => P6TokenFactoryCatalog.IsTokenFactory(cardNo)
        || PrintedCardFactory.TryCreate("", "", cardNo, out _);

    // CN 477.1.b: copy the printed/copied face, never a materialized combat value,
    // buffs, granted keywords, damage, attachments, controller or status. CardNo
    // already resolves rules text/cost/name/traits for a copy of another copy.
    internal static CardObjectState CreateImage(string objectId, string controller, CardObjectState source)
    {
        CardObjectState copied;
        if (source.CardNo is not null && P6TokenFactoryCatalog.TryGetByCardNo(source.CardNo, out var token))
            copied = token.CreateObject(objectId, controller, controller);
        else if (PrintedCardFactory.TryCreate(objectId, controller, source.CardNo, out var printed))
            copied = printed;
        else
            throw new InvalidOperationException("Copy source has no authoritative card face.");

        var printedKeywords = source.CardNo is not null && CardBehaviorRegistry.TryGetByCardNo(source.CardNo, out var behavior)
            ? behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        return copied with {
            TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
            Tags = copied.Tags.Concat(printedKeywords).Append(CardObjectTags.Ephemeral)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            IsExhausted = false
        };
    }
}
