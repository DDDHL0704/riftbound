using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

internal static class PaymentResourceSurplusNetworkAcceptance
{
    // Run "play", restart the dedicated API process, then run "recover" against
    // the same fixture/database. The SQL companion verifies the original log.
    public static async Task RunAsync(string url, string output, bool recover)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = deadline.Token;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json"), token));
        var root = fixture.RootElement;
        var room = root.GetProperty("room").GetString()!;
        var actor = root.GetProperty("actor").GetString()!;
        var initialTokens = root.GetProperty("tokens").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        await using var audit = await PaymentReconnectEvidence.Begin(room, output, recover ? "recover" : "play", initialTokens);
        var players = audit.Tokens;
        var traces = new ConcurrentQueue<object>();
        var snapshots = new ConcurrentDictionary<string, JsonElement>();
        var prompts = new ConcurrentDictionary<string, ActionPromptDto>();
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
                        if (!audit.Observe(player, method, message)) deadline.Cancel();
                        if (message.Payload is not JsonElement payload) return;
                        if (method == "Snapshot") snapshots[player] = payload.Clone();
                        if (method == "Prompt") prompts[player] = payload.Deserialize<ActionPromptDto>(json)!;
                    });
                await client.StartAsync(token);
                var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, "payment-surplus-key-" + player, token);
                Check(auth.Authenticated, "Dedicated audit identity authentication failed.");
                await client.InvokeAsync("Reconnect", room, player, reconnect, token); await audit.RequireJoined(player, token);
                await client.InvokeAsync("RequestSnapshot", room, player, token);
            }
            await WaitForViews(); await audit.Connected();
            var sourceId = root.GetProperty("command").GetProperty("sourceObjectId").GetString()!;
            if (!recover)
            {
                var opponent = players.Keys.Single(p => p != actor);
                Check(!snapshots[opponent].GetRawText().Contains(sourceId, StringComparison.Ordinal)
                    && !JsonSerializer.Serialize(prompts[opponent], json).Contains(sourceId, StringComparison.Ordinal),
                    "The opponent must not receive the actor's hidden hand identity before the play.");
                var commandNode = JsonNode.Parse(root.GetProperty("command").GetRawText())!.AsObject();
                commandNode["promptId"] = prompts[actor].PromptId;
                commandNode["snapshotTick"] = prompts[actor].SnapshotTick;
                var raw = JsonSerializer.SerializeToElement(commandNode);
                var command = raw.Deserialize<PlayCardCommand>(json)!;
                var candidate = prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PlayCard);
                var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                var requirement = metadata.GetProperty("sourceRequirements").EnumerateArray()
                    .Single(r => r.GetProperty("sourceObjectId").GetString() == sourceId);
                var resources = requirement.GetProperty("paymentResourceChoices").EnumerateArray()
                    .Select(r => r.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
                Check(command.OptionalCosts!.All(resources.Contains),
                    "The live mana-only card prompt must quote both voluntary rune acquisitions, even though neither pays a power fee.");
                var quote = await connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
                    new PlayCostPreviewRequestDto("network-surplus-quote", prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, command), token);
                Check(quote.CanPay && quote.Cost!.Mana == 2 && quote.Cost.GenericPower == 0
                    && quote.Cost.PowerByTrait.Count == 0 && quote.Cost.RemainingPowerByTrait!.Count == 2
                    && quote.Cost.RemainingPowerByTrait.GetValueOrDefault("red") == 1
                    && quote.Cost.RemainingPowerByTrait.GetValueOrDefault("blue") == 1,
                    "Live API quote must preserve both voluntary power while charging only two mana.");
                var forbidden = await audit.Reject(opponent, actor, ErrorCodes.IdentityMismatch, () => connections[opponent].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "wrong-authenticated-actor", raw, token), token);
                Check(!forbidden.Accepted, "A peer must not submit another player's payment choice.");
                var receipt = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "network-surplus-play", raw, token);
                Check(receipt.Accepted, receipt.Message);
                await WaitForViews(receipt.ServerTick);
                var stale = await audit.Reject(actor, actor, ErrorCodes.PromptExpired, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "network-surplus-stale", raw, token), token);
                Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == receipt.ServerTick,
                    "The stale plan must be rejected without a second acquisition.");
                await File.WriteAllTextAsync(Path.Combine(output, "network-resume.json"), JsonSerializer.Serialize(new {
                    raw, tick = receipt.ServerTick, snapshots, prompts, quote, receipt, stale, forbidden
                }, json), token);
            }
            else
            {
                using var before = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-resume.json"), token));
                var previous = before.RootElement;
                var tick = previous.GetProperty("tick").GetInt64();
                foreach (var player in players.Keys)
                {
                    Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("snapshots").GetProperty(player).GetRawText()),
                        JsonNode.Parse(snapshots[player].GetRawText())), "Actual API process restart changed a private snapshot.");
                    Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("prompts").GetProperty(player).GetRawText()),
                        JsonSerializer.SerializeToNode(prompts[player], json)), "Actual API restart changed a player's authoritative prompt.");
                }
                var duplicate = await audit.WithoutWrites("duplicate-" + prompts[actor].SnapshotTick, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "network-surplus-play", previous.GetProperty("raw"), token));
                Check(duplicate.Accepted && duplicate.ServerTick == tick, "Cold API duplicate must resolve to the original accepted action.");
            }
            await audit.Complete();
            await File.WriteAllTextAsync(Path.Combine(output, recover ? "network-after-restart.json" : "network-before-restart.json"),
                JsonSerializer.Serialize(new { status = "REAL_DUAL_PLAYER_SIGNALR_PAYMENT_SURPLUS_PASS", room, recover,
                    snapshots, prompts, traces = traces.ToArray(), scope = "Dedicated API process; authenticated peer boundary, quote, explicit acquisition, stale rejection and same original action after process restart." }, json), token);
            Console.WriteLine(recover ? "NETWORK_PAYMENT_SURPLUS_COLD_RECONNECT_AND_DUPLICATE_PASS" : "NETWORK_PAYMENT_SURPLUS_PLAY_QUOTE_PEER_AND_STALE_PASS");
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(output, "network-trace-" + (recover ? "recover" : "play") + ".json"), JsonSerializer.Serialize(traces.ToArray(), json));

            foreach (var connection in connections.Values) await connection.DisposeAsync();
        }

        async Task WaitForViews(long? tick = null)
        {
            while (players.Keys.Any(player => !snapshots.TryGetValue(player, out var snapshot)
                || !prompts.TryGetValue(player, out var prompt) || tick is not null
                && (snapshot.GetProperty("tick").GetInt64() != tick || prompt.SnapshotTick != tick)))
                { audit.Healthy(); await Task.Delay(20, token); }
            foreach (var snapshot in snapshots.Values)
                Check(!snapshot.GetRawText().Contains("reconnectToken", StringComparison.Ordinal)
                    && !snapshot.GetRawText().Contains("openingSeed", StringComparison.Ordinal), "Private server identity data escaped in a player snapshot.");
        }
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
