namespace Riftbound.GodotClient;

public sealed record CardCatalogEntry(
    string CardNo,
    string CardName,
    string CardCategoryName,
    string FrontImage,
    string BackImage,
    int? Energy,
    int? Power,
    string Trait,
    string EffectText,
    string RarityName,
    string ColorText);
