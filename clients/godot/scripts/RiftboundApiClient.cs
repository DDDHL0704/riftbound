using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Riftbound.GodotClient;

public sealed class RiftboundApiClient
{
    private static readonly System.Net.Http.HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _serverUrl;

    public RiftboundApiClient(string serverUrl)
    {
        _serverUrl = serverUrl.TrimEnd('/');
    }

    public async Task<IReadOnlyList<PreconstructedDeck>> GetPreconstructedDecksAsync(
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"{_serverUrl}/decks/preconstructed");
        return await HttpClient.GetFromJsonAsync<List<PreconstructedDeck>>(uri, JsonOptions, cancellationToken)
            ?? [];
    }
}
