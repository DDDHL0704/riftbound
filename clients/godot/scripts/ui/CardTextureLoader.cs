using System.IO;
using Godot;

namespace Riftbound.GodotClient.Ui;

internal static class CardTextureLoader
{
    public static Texture2D? Load(string imagePath, bool rotateCounterclockwise = false)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

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

        return ImageTexture.CreateFromImage(image);
    }
}
