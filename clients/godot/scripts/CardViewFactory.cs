using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Riftbound.GodotClient;

internal sealed class CardViewFactory
{
    private readonly OfficialCardImageLoader _imageLoader;

    public CardViewFactory(OfficialCardImageLoader imageLoader)
    {
        _imageLoader = imageLoader;
    }

    public async Task<CardViewData> BuildAsync(
        SnapshotCardRef card,
        IReadOnlyDictionary<string, CardCatalogEntry> officialCatalog,
        CancellationToken cancellationToken)
    {
        if (!card.Visible || !officialCatalog.TryGetValue(card.CardNo, out var entry))
        {
            return new CardViewData(
                card.ObjectId,
                card.CardNo,
                string.Empty,
                string.Empty,
                -1,
                -1,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                card.Visible,
                card.FaceDown,
                ImagePath: string.Empty);
        }

        var imagePath = await _imageLoader.LoadOfficialFrontImagePathAsync(entry, cancellationToken);
        return new CardViewData(
            card.ObjectId,
            entry.CardNo,
            entry.CardName,
            entry.CardCategoryName,
            entry.Energy ?? -1,
            entry.Power ?? -1,
            entry.Trait,
            entry.EffectText,
            entry.RarityName,
            entry.ColorText,
            Visible: true,
            FaceDown: false,
            imagePath ?? string.Empty);
    }
}
