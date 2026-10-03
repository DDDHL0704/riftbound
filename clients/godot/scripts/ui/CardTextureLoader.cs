using System.IO;
using System;
using System.Collections.Generic;
using Godot;

namespace Riftbound.GodotClient.Ui;

internal static class CardTextureLoader
{
    private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
    private static readonly Queue<string> TextureOrder = new();

    public static Texture2D? Load(string imagePath, bool rotateCounterclockwise = false)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        var key = $"{imagePath}:{File.GetLastWriteTimeUtc(imagePath).Ticks}:{rotateCounterclockwise}";
        if (Textures.TryGetValue(key, out var cached)) return cached;

        var bytes = File.ReadAllBytes(imagePath);
        using var image = new Image();
        var error = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => image.LoadPngFromBuffer(bytes),
            ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
            ".webp" => image.LoadWebpFromBuffer(bytes),
            _ => Error.Unavailable
        };
        if (error != Error.Ok)
        {
            return null;
        }

        if (rotateCounterclockwise)
        {
            image.Rotate90(ClockDirection.Counterclockwise);
        }

        image.GenerateMipmaps();
        var texture = ImageTexture.CreateFromImage(image);
        Textures[key] = texture;
        TextureOrder.Enqueue(key);
        while (TextureOrder.Count > 192) Textures.Remove(TextureOrder.Dequeue());
        return texture;
    }
}
