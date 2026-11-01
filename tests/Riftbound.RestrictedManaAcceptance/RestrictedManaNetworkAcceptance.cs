using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;
using Riftbound.Engine;
using static RestrictedManaSqlAcceptance;

internal static class RestrictedManaNetworkAcceptance
{
    public static async Task RunAsync(string url, string output, string mode)
    { foreach (var kind in Cases) await RunCase(url, Path.Combine(output, kind), mode); }

    private static async Task RunCase(string url, string folder, string mode)
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
                var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, "restricted-key-" + player, token);
                Check(auth.Authenticated, "Restricted-mana authentication failed.");
                await client.InvokeAsync("Reconnect", room, player, reconnect, token); await audit.RequireJoined(player, token); await client.InvokeAsync("RequestSnapshot", room, player, token);
            }
            await Views(); await audit.Connected();
            if (mode == "play")
            {
                Check(!snapshots[peer].GetRawText().Contains("QA-FIRST", StringComparison.Ordinal)
                    && !JsonSerializer.Serialize(prompts[peer], json).Contains("QA-FIRST", StringComparison.Ordinal), "The peer must not see a hidden hand source.");
                await Submit(actor, "restricted-standalone", Raw(actor, new ActivateAbilityCommand("QA-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, [])));
                var command = new PlayCardCommand("QA-FIRST", "OGN·268/298", ["QA-FIELD"]);
                var quote = await Quote(command);
                Check(quote.CanPay && quote.Cost is { AvailableMana: 3, AvailableSpellOnlyMana: 2, RemainingMana: 2, RemainingSpellOnlyMana: 1 },
                    "Native mixed quote must expose one ordinary and one restricted mana after payment.");
                await Submit(actor, "restricted-first-play", Raw(actor, command));
                await Submit(actor, "restricted-first-pass-a", Raw(actor, new PassPriorityCommand()));
                await Submit(peer, "restricted-peer-gain", Raw(peer, new ActivateAbilityCommand("QA-PEER-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, [])));
                await Submit(peer, "restricted-first-pass-b", Raw(peer, new PassPriorityCommand()));
                var bad = await audit.Reject(actor, actor, ErrorCodes.InvalidTarget, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "restricted-unavailable", Payment("SPEND_POWER:99"), token), token);
                Check(!bad.Accepted && bad.ErrorCode == ErrorCodes.InvalidTarget, "An unavailable instruction quantity must reject.");
                var resource = Choices().Single(id => id.StartsWith("RESOURCE:QA-SECOND-LUX:")); var rawResource = Payment(resource);
                var forbidden = await audit.Reject(peer, actor, ErrorCodes.IdentityMismatch, () => connections[peer].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "restricted-peer-spoof", rawResource, token), token);
                Check(!forbidden.Accepted, "A peer cannot acquire or spend resources as the resolving controller.");
                var receipt = await Submit(actor, "restricted-instruction-resource", rawResource);
                var stale = await audit.Reject(actor, actor, ErrorCodes.PromptExpired, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "restricted-old-prompt", rawResource, token), token);
                Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == receipt.ServerTick,
                    "An old resource prompt cannot be reused under another intent.");
                Check(!Choices().Any(id => id.StartsWith("SPEND_MANA:") || id == "SPEND_POWER:1"), "Restricted mana cannot pay the power instruction.");
                Check(PoolAmount(actor, "spellOnlyMana") == 3 && PoolAmount(peer, "spellOnlyMana") == 2,
                    "Both visible balances are positive; the actor's resource child is confirmed while the instruction remains pending.");
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
                        "Actual process restart changed the resource continuation choices.");
                }
                var duplicate = await audit.WithoutWrites("duplicate-" + prompts[actor].SnapshotTick, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                    "restricted-instruction-resource", before.GetProperty("rawResource"), token));
                Check(duplicate.Accepted && duplicate.ServerTick == before.GetProperty("tick").GetInt64(), "A cold duplicate must return the old resource receipt.");
            }
            else
            {
                await Submit(actor, "restricted-first-zero", Payment("SPEND_POWER:0"));
                Check(PoolAmount(actor, "mana") == 1 && PoolAmount(actor, "spellOnlyMana") == 3, "Spend zero power while retaining the already confirmed resource.");
                var second = new PlayCardCommand("QA-SECOND", "OGN·268/298", ["QA-FIELD"]); var quote = await Quote(second);
                Check(quote.CanPay && quote.Cost is { AvailableMana: 4, AvailableSpellOnlyMana: 3, RemainingMana: 3, RemainingSpellOnlyMana: 2 },
                    "The next spell must spend actual retained restricted mana.");
                await Submit(actor, "restricted-second-play", Raw(actor, second));
                await Submit(actor, "restricted-second-pass-a", Raw(actor, new PassPriorityCommand()));
                await Submit(peer, "restricted-second-pass-b", Raw(peer, new PassPriorityCommand()));
                await Submit(actor, "restricted-second-zero", Payment("SPEND_POWER:0"));
                var unit = new PlayCardCommand("QA-UNIT", "OGN·044/298", []); var denied = await Quote(unit);
                Check(!denied.CanPay && denied.ErrorCode == ErrorCodes.InsufficientCost
                    && denied.Cost is { AvailableMana: 1, AvailableSpellOnlyMana: 0, RemainingSpellOnlyMana: null },
                    "Unit payment must not count the actor's two restricted mana.");
                Check(!(prompts[actor].Candidates ?? []).Any(c => c.Action == CommandTypes.PlayCard && c.Enabled),
                    "No card is affordable with the remaining unrestricted resources, so PLAY_CARD is not offered.");
                var unitRaw = Raw(actor, unit); var tick = prompts[actor].SnapshotTick;
                var refusal = await audit.Reject(actor, actor, ErrorCodes.PromptExpired, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "restricted-unit-rejected", unitRaw, token), token);
                Check(!refusal.Accepted && refusal.ErrorCode == ErrorCodes.PromptExpired && refusal.ServerTick == tick, "The server must reject the unoffered unit command without spending restricted mana.");
                var duplicate = await audit.WithoutWrites("duplicate-" + prompts[actor].SnapshotTick, () => audit.Reject(actor, actor, ErrorCodes.PromptExpired, () => connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "restricted-unit-rejected", unitRaw, token), token));
                Check(!duplicate.Accepted && duplicate.ServerTick == tick, "A rejected unit intent must retain its original cached receipt.");
                Check(PoolAmount(actor, "spellOnlyMana") == 2 && PoolAmount(peer, "spellOnlyMana") == 2, "Both genuine balances must remain positive before turn end.");
                await Save(folder, "network-before-clear.json", new { snapshots, prompts, quote, denied, refusal, duplicate });
                // Keep the same live authenticated sessions across rejection and the legal EndTurn.
                await Submit(actor, "restricted-end-turn", Raw(actor, new EndTurnCommand()));
                Check(PoolAmount(actor, "spellOnlyMana") == 0 && PoolAmount(peer, "spellOnlyMana") == 0, "EndTurn must clear both players' balances.");
                Check(snapshots[actor].GetProperty("timing").GetProperty("phase").GetString() == MatchPhases.Main
                    && snapshots[actor].GetProperty("activePlayerId").GetString() == peer, "The match must normally enter the opponent's main phase.");
            }
            await audit.Complete();
            await SaveNetworkReport(folder, mode, new { status = "REAL_SIGNALR_RESTRICTED_MANA_PASS", room, mode,
                snapshots, prompts, traces = traces.ToArray(), scope = "Original room seats and finite QA position; restart driven externally. No ordinary-opening, UI or whole-card certification." }, json);
        }
        finally {
            await File.WriteAllTextAsync(Path.Combine(folder, "network-trace-" + (mode) + ".json"), JsonSerializer.Serialize(traces.ToArray(), json));
            foreach (var client in connections.Values) await client.DisposeAsync();
        }

        int PoolAmount(string player, string key)
        {
            var pool = snapshots[actor].GetProperty("players").GetProperty(player).GetProperty("runePool");
            return pool.TryGetProperty(key, out var amount) ? amount.GetInt32() : 0;
        }
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
        Task<PlayCostQuoteDto> Quote(PlayCardCommand command) => connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
            new PlayCostPreviewRequestDto("restricted-quote-" + command.SourceObjectId, prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, command), token);
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
        }
    }

    private static Task SaveNetworkReport(string folder, string mode, object report, JsonSerializerOptions json)
        => File.WriteAllTextAsync(Path.Combine(folder, "network-" + mode + ".json"),
            JsonSerializer.Serialize(report, report.GetType(), json));
}
