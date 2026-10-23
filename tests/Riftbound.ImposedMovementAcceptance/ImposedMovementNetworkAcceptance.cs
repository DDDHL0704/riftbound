using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;
using Riftbound.Engine;
using static ImposedMovementSqlAcceptance;

internal static class ImposedMovementNetworkAcceptance
{
    public static async Task RunAsync(string url, string output, string mode)
    { foreach (var kind in Cases) await RunCase(url, Path.Combine(output, kind), kind, mode); }

    private static async Task RunCase(string url, string folder, string kind, string mode)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = deadline.Token;
        var json = new JsonSerializerOptions(Json); json.Converters.Add(new JsonStringEnumConverter());
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-fixture.json"), token));
        var root = fixture.RootElement; var room = root.GetProperty("room").GetString()!;
        var actor = root.GetProperty("actor").GetString()!; var peer = root.GetProperty("peer").GetString()!;
        var initialTokens = root.GetProperty("tokens").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        await using var audit = await PaymentReconnectEvidence.Begin(room, folder, mode, initialTokens);
        var players = audit.Tokens;
        var snapshots = new ConcurrentDictionary<string, JsonElement>(); var prompts = new ConcurrentDictionary<string, ActionPromptDto>();
        var traces = new ConcurrentQueue<object>(); var connections = new Dictionary<string, HubConnection>();
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
                var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, "imposed-key-" + player, token);
                Check(auth.Authenticated, "Imposed-movement authentication failed.");
                await client.InvokeAsync("Reconnect", room, player, reconnect, token); await audit.RequireJoined(player, token); await client.InvokeAsync("RequestSnapshot", room, player, token);
            }
            await Views(); await audit.Connected();
            if (mode == "play")
            {
                Check(!snapshots[peer].GetRawText().Contains("QA-SPELL", StringComparison.Ordinal), "The peer must not see the actor's hidden spell.");
                PlayCostQuoteDto? quote = null;
                if (kind.StartsWith("effect", StringComparison.Ordinal))
                {
                    var spell = new PlayCardCommand("QA-SPELL", "UNL-054/219", ["QA-D", "QA-D2"], Destination: "BATTLEFIELD:QA-FIELD2");
                    quote = await connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
                        new PlayCostPreviewRequestDto("imposed-native-quote", prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, spell), token);
                    Check(quote.CanPay, "Printed play costs are payable before the later imposed movement cost.");
                    await Submit(actor, "imposed-play", Raw(actor, spell));
                    await Submit(actor, "imposed-pass-a", Raw(actor, new PassPriorityCommand()));
                    await Submit(peer, "imposed-pass-b", Raw(peer, new PassPriorityCommand()));
                }
                else await Submit(actor, "imposed-move", Raw(actor,
                    new MoveUnitCommand("QA-A", "BASE", "BATTLEFIELD:QA-FIELD2", SourceObjectIds: ["QA-A", "QA-B"])));
                Check(!Choices().Contains("PAY"), "No power remains; spell-only mana cannot waive the imposed power cost.");
                var bad = await audit.Reject(actor, actor, ErrorCodes.InvalidTarget, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "imposed-unavailable", Payment("PAY"), token), token);
                Check(!bad.Accepted && bad.ErrorCode == ErrorCodes.InvalidTarget, "Unavailable payment rejects.");
                await Submit(actor, "imposed-lux-resource", Payment(Choices().Single(id => id.StartsWith("RESOURCE:QA-LUX:", StringComparison.Ordinal))));
                Check(PoolAmount("spellOnlyMana") == 2 && !Choices().Contains("PAY"), "Lux is voluntary acquisition, not a power payment.");
                await Submit(actor, "imposed-stele-amount", Payment(Choices().Single(id => id.StartsWith("RESOURCE:QA-STELE:", StringComparison.Ordinal))));
                var rawResource = Payment(RangeOption(65));
                var forbidden = await audit.Reject(peer, actor, ErrorCodes.IdentityMismatch, () => connections[peer].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "imposed-peer-spoof", rawResource, token), token);
                Check(!forbidden.Accepted, "The final PASS submitter cannot impersonate the actual movement executor.");
                var receipt = await Submit(actor, "imposed-payment-resource", rawResource);
                var stale = await audit.Reject(actor, actor, ErrorCodes.PromptExpired, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "imposed-old-prompt", rawResource, token), token);
                Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == receipt.ServerTick,
                    "A resource prompt cannot be reused under another intent.");
                Check(PoolAmount("spellOnlyMana") == 2 && PoolAmount("mana") == 66 && PoolTotalPower() == 0
                    && Choices().Count == 2 && Choices().All(id => id.StartsWith("QUANTITY:", StringComparison.Ordinal)),
                    "Restart in the uncommitted amount range after exactly the first binary answer, with the real rune untouched.");
                await Save(folder, "network-resume.json", new { rawResource, tick = receipt.ServerTick, snapshots, prompts, quote, bad, forbidden, stale });
            }
            else if (mode == "recover")
            {
                using var saved = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-resume.json"), token));
                var before = saved.RootElement;
                foreach (var player in players.Keys)
                {
                    Check(JsonNode.DeepEquals(JsonNode.Parse(before.GetProperty("snapshots").GetProperty(player).GetRawText()), JsonNode.Parse(snapshots[player].GetRawText())),
                        "Actual process restart changed a private snapshot.");
                    Check(JsonNode.DeepEquals(JsonNode.Parse(before.GetProperty("prompts").GetProperty(player).GetRawText()), JsonSerializer.SerializeToNode(prompts[player], json)),
                        "Actual process restart changed the imposed movement continuation.");
                }
                var duplicate = await audit.WithoutWrites("duplicate-" + prompts[actor].SnapshotTick, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "imposed-payment-resource", before.GetProperty("rawResource"), token));
                Check(duplicate.Accepted && duplicate.ServerTick == before.GetProperty("tick").GetInt64(), "Cold duplicate returns the original resource receipt.");
            }
            else
            {
                var pay = kind.EndsWith("-pay", StringComparison.Ordinal);
                await Quantity(65, "conversion");
                Check(PoolAmount("spellOnlyMana") == 2 && PoolAmount("mana") == 1 && PoolTotalPower() == 65,
                    "Commit exactly the chosen ordinary-mana conversion before deciding movement.");
                await Submit(actor, "imposed-final-decision", Payment(pay ? "PAY" : "DECLINE"));
                if (kind.StartsWith("effect", StringComparison.Ordinal))
                {
                    await Submit(actor, "imposed-tail-spell", Raw(actor, new PlayCardCommand("QA-BULLET", "OGN·268/298", ["QA-FIELD1"])));
                    await Submit(actor, "imposed-tail-pass-a", Raw(actor, new PassPriorityCommand()));
                    await Submit(peer, "imposed-tail-pass-b", Raw(peer, new PassPriorityCommand()));
                    await Submit(actor, "imposed-tail-amount", Payment("SPEND_POWER:CHOOSE_AMOUNT")); await Quantity(0, "instruction");
                }
                Check(PoolAmount("spellOnlyMana") == (kind.StartsWith("effect", StringComparison.Ordinal) ? 1 : 2)
                    && PoolAmount("mana") == 1 && PoolTotalPower() == (pay ? 64 : 65),
                    "Final payment or decline retains actual acquired resources and debits power exactly once.");
            }
            await audit.Complete();
            await Save(folder, "network-" + mode + ".json", new { status = "REAL_SIGNALR_IMPOSED_MOVEMENT_PASS", room, kind, mode,
                snapshots, prompts, traces = traces.ToArray(), scope = "Original empty-room birth and finite QA position. Restart is external; no ordinary-opening, UI or whole-card certification." });
        }
        finally {
            await File.WriteAllTextAsync(Path.Combine(folder, "network-trace-" + (mode) + ".json"), JsonSerializer.Serialize(traces.ToArray(), json));
 foreach (var client in connections.Values) await client.DisposeAsync(); }

        JsonElement Pool() => snapshots[actor].GetProperty("players").GetProperty(actor).GetProperty("runePool");
        int PoolAmount(string key) => Pool().TryGetProperty(key, out var amount) ? amount.GetInt32() : 0;
        int PoolTotalPower() => PoolAmount("power");
        JsonElement Raw(string player, GameCommand command)
        {
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
            node["promptId"] = prompts[player].PromptId; node["snapshotTick"] = prompts[player].SnapshotTick;
            return JsonSerializer.SerializeToElement(node);
        }
        HashSet<string> Choices() => prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PayCost).SelectionSteps!
            .SelectMany(s => s.Choices).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        JsonElement Payment(string option)
        {
            var candidate = prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PayCost);
            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
            return Raw(actor, new PayCostCommand(metadata.GetProperty("paymentId").GetString()!, metadata.GetProperty("paymentWindow").GetString()!, [option]));
        }
        string RangeOption(int amount) => Choices().Single(option => {
            var parts = option.Split(':'); return int.Parse(parts[^2]) <= amount && amount <= int.Parse(parts[^1]); });
        async Task Quantity(int amount, string label)
        {
            var count = 0;
            while (prompts[actor].Candidates?.Any(c => c.Action == CommandTypes.PayCost) == true)
            {
                var choices = Choices();
                if (!choices.All(id => id.StartsWith("QUANTITY:", StringComparison.Ordinal))) break;
                Check(choices.Count == 2 && ++count <= 31, "Keep each exact bounded amount selection finite.");
                await Submit(actor, "imposed-" + label + "-range-" + count, Payment(RangeOption(amount)));
            }
        }
        async Task<CommandReceiptDto> Submit(string player, string id, JsonElement raw)
        {
            var receipt = await connections[player].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, player, id, raw, token);
            Check(receipt.Accepted, receipt.Message); await Views(receipt.ServerTick); return receipt;
        }
        async Task Views(long? tick = null)
        {
            while (players.Keys.Any(player => !snapshots.TryGetValue(player, out var snapshot) || !prompts.TryGetValue(player, out var prompt)
                || tick is not null && (snapshot.GetProperty("tick").GetInt64() != tick || prompt.SnapshotTick != tick))) { audit.Healthy(); await Task.Delay(20, token); }
            foreach (var snapshot in snapshots.Values) Check(!snapshot.GetRawText().Contains("reconnectToken", StringComparison.Ordinal)
                && !snapshot.GetRawText().Contains("openingSeed", StringComparison.Ordinal), "Server-only authentication or opening data escaped.");
            Check(!snapshots[actor].GetRawText().Contains("QA-PRIVATE", StringComparison.Ordinal)
                && !JsonSerializer.Serialize(prompts[actor], json).Contains("QA-PRIVATE", StringComparison.Ordinal), "Payment choices must not reveal an opponent private identity.");
        }
    }
}
