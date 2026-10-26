using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Riftbound.Engine;

// Input identity follows JSON numeric values across jsonb storage. State and
// event hashing retain their existing representation and never use this path.
internal static class CommandInputFingerprint
{
    internal static string Hash(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(Number(value.GetRawText()));
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(value.GetBoolean());
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported command JSON value kind {value.ValueKind}.");
        }
    }

    private static string Number(string raw)
    {
        var negative = raw[0] == '-';
        var start = negative ? 1 : 0;
        var exponentAt = raw.IndexOfAny(['e', 'E']);
        var end = exponentAt < 0 ? raw.Length : exponentAt;
        var dot = raw.IndexOf('.', start, end - start);
        var fractionDigits = dot < 0 ? 0 : end - dot - 1;
        var digits = raw[start..end].Replace(".", "", StringComparison.Ordinal);
        var first = 0;
        while (first < digits.Length && digits[first] == '0') first++;
        if (first == digits.Length) return "0";
        var last = digits.Length - 1;
        while (digits[last] == '0') last--;
        var exponent = exponentAt < 0 ? BigInteger.Zero
            : BigInteger.Parse(raw.AsSpan(exponentAt + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        exponent += digits.Length - last - 1 - fractionDigits;
        return (negative ? "-" : "") + digits[first..(last + 1)] + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }
}
