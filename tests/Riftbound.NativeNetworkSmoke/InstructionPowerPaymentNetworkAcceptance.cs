using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

internal static class InstructionPowerPaymentNetworkAcceptance
{
    public static async Task RunAsync(string url, string output, string mode)
    {
        foreach (var kind in new[] { "zero", "spend" })
            await RunCase(url, Path.Combine(output, kind), mode);
    }

    private static async Task RunCase(string url, string folder, string mode)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = deadline.Token;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-fixture.json"), token));
        var root = fixture.RootElement; var room = root.GetProperty("room").GetString()!;
        var actor = root.GetProperty("actor").GetString()!; var peer = root.GetProperty("peer").GetString()!;
        var quantity = root.GetProperty("quantity").GetInt32();
        var players = root.GetProperty("tokens").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
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
                var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, "instruction-key-" + player, token);
                Check(auth.Authenticated, "Instruction audit authentication failed.");
                await client.InvokeAsync("Reconnect", room, player, reconnect, token);
                await client.InvokeAsync("RequestSnapshot", room, player, token);
            }
            await Views();
            if (mode == "play")
            {
                var source = root.GetProperty("source").GetString()!;
                Check(!snapshots[peer].GetRawText().Contains(source, StringComparison.Ordinal)
                    && !JsonSerializer.Serialize(prompts[peer], json).Contains(source, StringComparison.Ordinal),
                    "The opponent must not receive the still-hidden hand source through the payment prompt.");
                var command = new PlayCardCommand(source, "OGN·268/298", [root.GetProperty("field").GetString()!]);
                var quote = await connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
                    new PlayCostPreviewRequestDto("instruction-quote", prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, command), token);
                Check(quote.CanPay && quote.Cost is { Mana: 1, GenericPower: 0, PowerByTrait.Count: 0 },
                    "The actual declaration quote includes printed mana and no resolution power.");
                await Submit(actor, "instruction-play", Raw(actor, command));
                for (var index = 0; index < 2; index++)
                {
                    var priority = snapshots[actor].GetProperty("timing").GetProperty("priorityPlayerId").GetString()!;
                    await Submit(priority, "instruction-pass-" + index, Raw(priority, new PassPriorityCommand()));
                }
                var bad = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "instruction-unavailable", Payment("SPEND_POWER:99"), token);
                Check(!bad.Accepted && bad.ErrorCode == ErrorCodes.InvalidTarget && bad.ServerTick == prompts[actor].SnapshotTick,
                    "A quantity absent from the live instruction choices must reject without advancing the state.");
                var resource = "RECYCLE_RUNE:" + root.GetProperty("rune").GetString();
                var rawResource = Payment(resource);
                var forbidden = await connections[peer].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "instruction-peer-spoof", rawResource, token);
                Check(!forbidden.Accepted, "The peer cannot pay or acquire resources as the resolving controller.");
                var receipt = await Submit(actor, "instruction-resource", rawResource);
                var stale = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "instruction-old-resource-prompt", rawResource, token);
                Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == receipt.ServerTick,
                    "Old resource selections must not be reused against the next instruction prompt.");
                Check(PaymentChoices().Contains("SPEND_POWER:red:2"), "The newly acquired red power must be offered in the actual next prompt.");
                await File.WriteAllTextAsync(Path.Combine(folder, "network-resume.json"), JsonSerializer.Serialize(new {
                    rawResource, tick = receipt.ServerTick, snapshots, prompts, quote, bad, forbidden, stale
                }, json), token);
            }
            else if (mode == "recover")
            {
                using var saved = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-resume.json"), token));
                var before = saved.RootElement;
                foreach (var player in players.Keys)
                {
                    Check(JsonNode.DeepEquals(JsonNode.Parse(before.GetProperty("snapshots").GetProperty(player).GetRawText()),
                        JsonNode.Parse(snapshots[player].GetRawText())), "An actual API restart changed the pending payment's private view.");
                    Check(JsonNode.DeepEquals(JsonNode.Parse(before.GetProperty("prompts").GetProperty(player).GetRawText()),
                        JsonSerializer.SerializeToNode(prompts[player], json)), "An actual API restart changed the validated instruction choices.");
                }
                var duplicate = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "instruction-resource", before.GetProperty("rawResource"), token);
                Check(duplicate.Accepted && duplicate.ServerTick == before.GetProperty("tick").GetInt64(),
                    "Replaying the pre-restart resource intent must return its original receipt.");
            }
            else
            {
                var raw = Payment("SPEND_POWER:" + quantity);
                var receipt = await Submit(actor, "instruction-final-quantity", raw);
                var duplicate = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "instruction-final-quantity", raw, token);
                Check(duplicate.Accepted && duplicate.ServerTick == receipt.ServerTick,
                    "The final zero/two payment must be idempotent after the restart.");
                Check(snapshots[actor].GetProperty("stack").GetArrayLength() == 0, "The paid instruction must finish the original spell.");
            }
            await File.WriteAllTextAsync(Path.Combine(folder, "network-" + mode + ".json"), JsonSerializer.Serialize(new {
                status = "REAL_SIGNALR_INSTRUCTION_PAYMENT_PASS", room, mode, quantity, snapshots, prompts, traces = traces.ToArray(),
                scope = "Two authenticated actors and finite logged QA position; actual restart is driven externally. No ordinary-opening, whole-card or UI certification."
            }, json), token);
        }
        finally { foreach (var connection in connections.Values) await connection.DisposeAsync(); }

        JsonElement Raw(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            node["promptId"] = prompts[player].PromptId; node["snapshotTick"] = prompts[player].SnapshotTick;
            return JsonSerializer.SerializeToElement(node);
        }
        HashSet<string> PaymentChoices() => prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PayCost)
            .SelectionSteps!.SelectMany(s => s.Choices).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        JsonElement Payment(string option)
        {
            var candidate = prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PayCost);
            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
            return Raw(actor, new PayCostCommand(metadata.GetProperty("paymentId").GetString()!,
                metadata.GetProperty("paymentWindow").GetString()!, [option]));
        }
        async Task<CommandReceiptDto> Submit(string player, string id, JsonElement raw)
        {
            var receipt = await connections[player].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, player, id, raw, token);
            Check(receipt.Accepted, receipt.Message);
            await Views(receipt.ServerTick);
            return receipt;
        }
        async Task Views(long? tick = null)
        {
            while (players.Keys.Any(player => !snapshots.TryGetValue(player, out var snapshot)
                || !prompts.TryGetValue(player, out var prompt) || tick is not null
                && (snapshot.GetProperty("tick").GetInt64() != tick || prompt.SnapshotTick != tick)))
                await Task.Delay(20, token);
            foreach (var snapshot in snapshots.Values)
                Check(!snapshot.GetRawText().Contains("reconnectToken", StringComparison.Ordinal)
                    && !snapshot.GetRawText().Contains("openingSeed", StringComparison.Ordinal), "Server-only data escaped in an instruction view.");
        }
    }
    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
