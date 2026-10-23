using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

internal static class OptionalAdditionalCostReductionNetworkAcceptance
{
    public static async Task RunAsync(string url, string output, bool recover)
    {
        foreach (var kind in new[] { "echo", "copied" })
            await RunCaseAsync(url, Path.Combine(output, kind), recover);
    }

    private static async Task RunCaseAsync(string url, string output, bool recover)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = deadline.Token;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json"), token));
        var root = fixture.RootElement;
        var room = root.GetProperty("room").GetString()!;
        var actor = root.GetProperty("actor").GetString()!;
        var kind = root.GetProperty("expectedKind").GetString()!;
        var players = root.GetProperty("tokens").EnumerateObject().ToDictionary(pair => pair.Name, pair => pair.Value.GetString()!);
        var snapshots = new ConcurrentDictionary<string, JsonElement>();
        var prompts = new ConcurrentDictionary<string, ActionPromptDto>();
        var traces = new ConcurrentQueue<object>();
        var connections = new Dictionary<string, HubConnection>();
        try
        {
            foreach (var (player, reconnect) in players)
            {
                var client = new HubConnectionBuilder().WithUrl(url + "/hubs/game").AddJsonProtocol(options =>
                    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Build();
                connections[player] = client;
                foreach (var method in new[] { "Joined", "Snapshot", "Prompt", "Events", "Error" })
                    client.On<WsServerMessage>(method, message => {
                        traces.Enqueue(new { recipient = player, method, message });
                        if (message.Payload is not JsonElement payload) return;
                        if (method == "Snapshot") snapshots[player] = payload.Clone();
                        if (method == "Prompt") prompts[player] = payload.Deserialize<ActionPromptDto>(json)!;
                    });
                await client.StartAsync(token);
                var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, "optional-cost-key-" + player, token);
                Check(auth.Authenticated, "Dedicated optional-cost audit authentication failed.");
                await client.InvokeAsync("Reconnect", room, player, reconnect, token);
                await client.InvokeAsync("RequestSnapshot", room, player, token);
            }
            await WaitForViews();
            if (!recover)
            {
                var opponent = players.Keys.Single(player => player != actor);
                var source = root.GetProperty("command").GetProperty("sourceObjectId").GetString()!;
                Check(!snapshots[opponent].GetRawText().Contains(source, StringComparison.Ordinal)
                    && !JsonSerializer.Serialize(prompts[opponent], json).Contains(source, StringComparison.Ordinal),
                    "A hidden hand identity must not escape through a cost choice or its origin metadata.");
                var raw = CurrentRaw("command");
                var command = raw.Deserialize<PlayCardCommand>(json)!;
                var candidate = prompts[actor].Candidates!.Single(item => item.Action == CommandTypes.PlayCard);
                var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                var requirement = metadata.GetProperty("sourceRequirements").EnumerateArray()
                    .Single(item => item.GetProperty("sourceObjectId").GetString() == source);
                var choices = requirement.GetProperty("optionalCostChoices").EnumerateArray()
                    .Select(item => item.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
                Check(command.OptionalCosts!.All(choices.Contains), "The live prompt must expose every selected original fee and exact reducer instance.");
                var selected = command.OptionalCosts!.Single(id => id.StartsWith("OPTIONAL_COST_REDUCTION:", StringComparison.Ordinal));
                var group = requirement.GetProperty("optionalCostReductionGroups").EnumerateArray().Single(item =>
                    item.GetProperty("choiceIds").EnumerateArray().Any(choice => choice.GetString() == selected));
                Check(group.GetProperty("sourceObjectId").GetString() == root.GetProperty("reducer").GetString()
                    && group.GetProperty("sourceGeneration").GetInt64() == root.GetProperty("reducerGeneration").GetInt64()
                    && group.GetProperty("minSelections").GetInt32() == 0 && group.GetProperty("maxSelections").GetInt32() == 1
                    && group.GetProperty("sourceInstanceId").GetString() is { Length: > 0 },
                    "Live metadata must bind the instance and its independent optional cardinality.");
                if (kind == "copied")
                {
                    var origin = group.GetProperty("ruleTextOrigin");
                    Check(origin.ValueKind == JsonValueKind.Object && origin.GetProperty("revision").GetInt64() > 0,
                        "The copied reduction must carry its actual captured origin.");
                    foreach (var id in new[] { origin.GetProperty("unit").GetProperty("objectId").GetString()!,
                        origin.GetProperty("equipment").GetProperty("objectId").GetString()! })
                        Check(snapshots.Values.All(snapshot => snapshot.GetRawText().Contains(JsonSerializer.Serialize(id), StringComparison.Ordinal)),
                            "Every disclosed captured origin must name a publicly visible field object for both players.");
                }
                var quote = await connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
                    new PlayCostPreviewRequestDto("network-optional-quote", prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, command), token);
                Check(quote.CanPay && quote.Cost!.Mana == root.GetProperty("expectedMana").GetInt32()
                    && quote.Cost.GenericPower == 0 && quote.Cost.PowerByTrait.Count == 0 && quote.Cost.RemainingMana == 0,
                    "The live quote must retain base mana and reduce only the declared optional fee.");
                var bad = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "invalid-optional-generation", CurrentRaw("forgedCommand"), token);
                Check(!bad.Accepted && bad.ErrorCode == ErrorCodes.InvalidTarget && bad.ServerTick == prompts[actor].SnapshotTick,
                    "The actual API must reject a forged generation without advancing state.");
                var forbidden = await connections[opponent].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "wrong-optional-actor", raw, token);
                Check(!forbidden.Accepted, "A peer cannot submit another actor's discount choice.");
                var receipt = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "network-optional-play", raw, token);
                Check(receipt.Accepted, receipt.Message);
                await WaitForViews(receipt.ServerTick);
                var stale = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "network-optional-stale", raw, token);
                Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == receipt.ServerTick,
                    "A stale discount selection must not charge resources or resolve another effect.");
                await File.WriteAllTextAsync(Path.Combine(output, "network-resume.json"), JsonSerializer.Serialize(new {
                    raw, tick = receipt.ServerTick, snapshots, prompts, quote, bad, forbidden, receipt, stale
                }, json), token);
            }
            else
            {
                using var saved = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-resume.json"), token));
                var before = saved.RootElement;
                foreach (var player in players.Keys)
                {
                    Check(JsonNode.DeepEquals(JsonNode.Parse(before.GetProperty("snapshots").GetProperty(player).GetRawText()),
                        JsonNode.Parse(snapshots[player].GetRawText())), "Actual API restart changed a private snapshot.");
                    Check(JsonNode.DeepEquals(JsonNode.Parse(before.GetProperty("prompts").GetProperty(player).GetRawText()),
                        JsonSerializer.SerializeToNode(prompts[player], json)), "Actual API restart changed a cost prompt or captured origin.");
                }
                var duplicate = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "network-optional-play", before.GetProperty("raw"), token);
                Check(duplicate.Accepted && duplicate.ServerTick == before.GetProperty("tick").GetInt64(),
                    "A cold duplicate must return the original accepted payment.");
            }
            await File.WriteAllTextAsync(Path.Combine(output, recover ? "network-after-restart.json" : "network-before-restart.json"),
                JsonSerializer.Serialize(new { status = "REAL_DUAL_PLAYER_SIGNALR_OPTIONAL_REDUCTION_PASS", kind, room, recover,
                    snapshots, prompts, traces = traces.ToArray(), scope = "Actual authenticated API; opaque printed/copied choices, quote, invalid generation, peer/stale rejection, private origins and same original intent after process restart. No whole-card or UI certification." }, json), token);
        }
        finally { foreach (var connection in connections.Values) await connection.DisposeAsync(); }

        JsonElement CurrentRaw(string property)
        {
            var node = JsonNode.Parse(root.GetProperty(property).GetRawText())!.AsObject();
            node["promptId"] = prompts[actor].PromptId; node["snapshotTick"] = prompts[actor].SnapshotTick;
            return JsonSerializer.SerializeToElement(node);
        }
        async Task WaitForViews(long? tick = null)
        {
            while (players.Keys.Any(player => !snapshots.TryGetValue(player, out var snapshot)
                || !prompts.TryGetValue(player, out var prompt) || tick is not null
                && (snapshot.GetProperty("tick").GetInt64() != tick || prompt.SnapshotTick != tick)))
                await Task.Delay(20, token);
            foreach (var snapshot in snapshots.Values)
                Check(!snapshot.GetRawText().Contains("reconnectToken", StringComparison.Ordinal)
                    && !snapshot.GetRawText().Contains("openingSeed", StringComparison.Ordinal), "Server-only identity data escaped in a snapshot.");
        }
    }
    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
