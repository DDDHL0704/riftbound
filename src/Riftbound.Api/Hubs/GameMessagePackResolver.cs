using System.Globalization;
using System.Text.Json;
using MessagePack;
using MessagePack.Formatters;
using Riftbound.Contracts;

namespace Riftbound.Api.Hubs;

internal sealed class GameMessagePackResolver(JsonSerializerOptions jsonOptions, IFormatterResolver fallback)
    : IFormatterResolver
{
    internal static MessagePackSerializerOptions Configure(
        MessagePackSerializerOptions options, JsonSerializerOptions jsonOptions)
        => options.WithResolver(new GameMessagePackResolver(jsonOptions, options.Resolver))
            .WithSecurity(options.Security.WithMaximumObjectGraphDepth(jsonOptions.MaxDepth == 0 ? 64 : jsonOptions.MaxDepth));

    public IMessagePackFormatter<T>? GetFormatter<T>() => FormatterCache<T>.UsesGameContract
        ? new GameContractFormatter<T>(jsonOptions)
        : fallback.GetFormatter<T>();

    private static class FormatterCache<T>
    {
        internal static readonly bool UsesGameContract = typeof(T) == typeof(JsonElement)
            || typeof(T).Assembly == typeof(WsServerMessage).Assembly;
    }

    // The browser sees the same payload shape in either hub protocol. JsonIgnore
    // rules and metadata containing DTOs or JsonElement values apply recursively.
    private sealed class GameContractFormatter<T>(JsonSerializerOptions jsonOptions) : IMessagePackFormatter<T>
    {
        public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        {
            var json = JsonSerializer.Serialize(value, jsonOptions);
            MessagePackSerializer.ConvertFromJson(json, ref writer, options);
        }

        public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            using var json = new StringWriter(CultureInfo.InvariantCulture);
            MessagePackSerializer.ConvertToJson(ref reader, json, options);
            return JsonSerializer.Deserialize<T>(json.ToString(), jsonOptions)!;
        }
    }
}
