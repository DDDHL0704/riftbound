using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace Riftbound.GodotClient;

public sealed record PlayerSessionSettings(
    string Handle,
    string RoomId,
    string PlayerKey,
    string? ReconnectToken = null,
    string? LastDeckId = null)
{
    public const string DefaultHandle = "godot";
    public const string DefaultRoomId = "godot-local";

    public static PlayerSessionSettings CreateDefault()
    {
        return new PlayerSessionSettings(DefaultHandle, DefaultRoomId, GeneratePlayerKey());
    }

    public static PlayerSessionSettings WithUsableKey(PlayerSessionSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.PlayerKey) || settings.PlayerKey.Trim().Length < 16
            ? settings with { PlayerKey = GeneratePlayerKey() }
            : settings;
    }

    private static string GeneratePlayerKey()
    {
        return $"pk_{Guid.NewGuid():N}{Guid.NewGuid():N}";
    }
}

public sealed class PlayerSessionStore
{
    private const string DefaultSessionPath = "user://session.json";
    private readonly string _sessionPath;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public PlayerSessionStore(string? sessionPath = null)
    {
        _sessionPath = string.IsNullOrWhiteSpace(sessionPath)
            ? DefaultSessionPath
            : sessionPath.Trim();
    }

    public async Task<PlayerSessionSettings> LoadAsync()
    {
        var path = ResolvePath(_sessionPath);
        if (!File.Exists(path))
        {
            var created = PlayerSessionSettings.CreateDefault();
            await SaveAsync(created);
            return created;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<PlayerSessionSettings>(stream, JsonOptions);
            return PlayerSessionSettings.WithUsableKey(settings ?? PlayerSessionSettings.CreateDefault());
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Unable to read session settings. Creating a fresh local identity. {ex.Message}");
            var created = PlayerSessionSettings.CreateDefault();
            await SaveAsync(created);
            return created;
        }
    }

    public async Task SaveAsync(PlayerSessionSettings settings)
    {
        var path = ResolvePath(_sessionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    private static string ResolvePath(string sessionPath)
    {
        return sessionPath.StartsWith("user://", StringComparison.Ordinal)
            || sessionPath.StartsWith("res://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(sessionPath)
            : Path.GetFullPath(sessionPath);
    }
}
