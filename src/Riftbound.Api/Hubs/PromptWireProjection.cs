using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Riftbound.Contracts;

namespace Riftbound.Api.Hubs;

internal static class PromptWireProjection
{
    internal static void Configure(JsonSerializerOptions options)
    {
        var resolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        var naming = options.PropertyNamingPolicy;
        var diagnosticProperties = new[]
        {
            nameof(ActionPromptDto.Inspection),
            nameof(ActionPromptDto.ObjectContexts),
            nameof(ActionPromptDto.ServerFlow)
        }.Select(name => naming?.ConvertName(name) ?? name).ToHashSet(StringComparer.Ordinal);

        // Apply only to hub serialization. The same projected prompt remains available
        // for diagnostics, command validation, persistence and replay.
        options.TypeInfoResolver = resolver.WithAddedModifier(info =>
        {
            if (info.Type != typeof(ActionPromptDto))
                return;
            foreach (var property in info.Properties.Where(property => diagnosticProperties.Contains(property.Name)))
                property.ShouldSerialize = (_, _) => false;
        });
    }
}
