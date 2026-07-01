using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Riftbound.GodotClient;

public sealed class OfficialCardImageLoader
{
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    public async Task<Image?> LoadOfficialFrontImageAsync(
        CardCatalogEntry card,
        CancellationToken cancellationToken = default)
    {
        var path = await LoadOfficialFrontImagePathAsync(card, cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return LoadImageBytes(bytes, Path.GetExtension(path));
    }

    public async Task<string?> LoadOfficialFrontImagePathAsync(
        CardCatalogEntry card,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(card.FrontImage))
        {
            return null;
        }

        var cachePath = CachePathFor(card.FrontImage);
        var extension = Path.GetExtension(cachePath);
        try
        {
            return await EnsureCachedOrDownloadAsync(card, cachePath, extension, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Unable to cache official card image {card.CardNo} from {card.FrontImage}: {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> EnsureCachedOrDownloadAsync(
        CardCatalogEntry card,
        string cachePath,
        string extension,
        CancellationToken cancellationToken)
    {
        if (File.Exists(cachePath))
        {
            var cachedBytes = await File.ReadAllBytesAsync(cachePath, cancellationToken);
            var cachedImage = LoadImageBytes(cachedBytes, extension);
            if (cachedImage is not null)
            {
                cachedImage.Dispose();
                return cachePath;
            }

            TryDeleteBadCache(cachePath);
            GD.PushWarning($"Removed invalid official card image cache for {card.CardNo}: {cachePath}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var bytes = await HttpClient.GetByteArrayAsync(card.FrontImage, cancellationToken);
        var image = LoadImageBytes(bytes, extension);
        if (image is null)
        {
            GD.PushWarning($"Unable to decode official card image {card.CardNo} from {card.FrontImage}");
            return null;
        }

        await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
        image.Dispose();
        return cachePath;
    }

    private static Image? LoadImageBytes(byte[] bytes, string extension)
    {
        var image = new Image();
        var error = extension.ToLowerInvariant() switch
        {
            ".png" => image.LoadPngFromBuffer(bytes),
            ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
            ".webp" => image.LoadWebpFromBuffer(bytes),
            _ => Error.Unavailable
        };
        if (error != Error.Ok)
        {
            GD.PushWarning($"Unable to decode official card image bytes: {error}");
            return null;
        }

        return image;
    }

    private static void TryDeleteBadCache(string cachePath)
    {
        try
        {
            File.Delete(cachePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Unable to delete invalid official card cache {cachePath}: {ex.Message}");
        }
    }

    private static string CachePathFor(string url)
    {
        var extension = Path.GetExtension(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".img";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        var cacheRoot = ProjectSettings.GlobalizePath("user://official-card-cache");
        return Path.Combine(cacheRoot, $"{hash}{extension}");
    }
}
