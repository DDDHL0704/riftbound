using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Riftbound.GodotClient;

public sealed class OfficialCardCatalogService
{
    public async Task<IReadOnlyDictionary<string, CardCatalogEntry>> LoadSnapshotAsync(
        string snapshotPath,
        CancellationToken cancellationToken = default)
    {
        var globalPath = ProjectSettings.GlobalizePath(snapshotPath);
        await using var stream = File.OpenRead(globalPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var cards = document.RootElement.GetProperty("cards");
        var entries = new Dictionary<string, CardCatalogEntry>(StringComparer.Ordinal);

        foreach (var card in cards.EnumerateArray())
        {
            var cardNo = ReadString(card, "cardNo");
            if (string.IsNullOrWhiteSpace(cardNo))
            {
                continue;
            }

            entries[cardNo] = new CardCatalogEntry(
                cardNo,
                ReadString(card, "cardName"),
                ReadString(card, "cardCategoryName"),
                ReadString(card, "frontImage"),
                ReadString(card, "backImage"),
                ReadInt(card, "energy"),
                ReadInt(card, "power"));
        }

        return entries;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), out var number) => number,
            _ => null
        };
    }
}
