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
        if (string.IsNullOrWhiteSpace(card.FrontImage))
        {
            return null;
        }

        var cachePath = CachePathFor(card.FrontImage);
        if (!File.Exists(cachePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var bytes = await HttpClient.GetByteArrayAsync(card.FrontImage, cancellationToken);
            await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
        }

        return LoadImage(cachePath);
    }

    private static Image? LoadImage(string path)
    {
        var image = new Image();
        var error = image.Load(path);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Unable to load official card image {path}: {error}");
            return null;
        }

        return image;
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
