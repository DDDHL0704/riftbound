using System.Text.Encodings.Web;
using System.Text.Json;

namespace Riftbound.ConformanceTests;

public static class CanonicalJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
