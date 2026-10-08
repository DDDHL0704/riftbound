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
    string? LastDeckId = null,
    string? ServerUrl = null)
{
    public const string DefaultHandle = "godot";
    public const string DefaultRoomId = "godot-local";

    public static PlayerSessionSettings CreateDefault()
    {
        return new PlayerSessionSettings($"玩家-{Guid.NewGuid().ToString("N")[..8]}",
            $"RB-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", GeneratePlayerKey());
    }

    public static PlayerSessionSettings WithUsableKey(PlayerSessionSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.PlayerKey) || settings.PlayerKey.Trim().Length < 16
            ? settings with { PlayerKey = GeneratePlayerKey() }
            : settings;
    }

    public static PlayerSessionSettings WithConnectionTarget(PlayerSessionSettings settings,
        string handle, string roomId, string? serverUrl)
    {
        var sameTarget = string.Equals(settings.Handle.Trim(), handle.Trim(), StringComparison.OrdinalIgnoreCase)
            && settings.RoomId == roomId
            && string.Equals(settings.ServerUrl?.TrimEnd('/'), serverUrl?.TrimEnd('/'), StringComparison.Ordinal);
        return settings with { Handle = handle, RoomId = roomId, ServerUrl = serverUrl,
            ReconnectToken = sameTarget ? settings.ReconnectToken : null };
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
