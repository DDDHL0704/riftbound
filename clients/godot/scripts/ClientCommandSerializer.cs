using System.Text.Json;

namespace Riftbound.GodotClient;

public static class ClientCommandSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static JsonElement Serialize(Dictionary<string, object?> payload)
        => JsonSerializer.SerializeToElement(payload, Options);
}
