using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static partial class ImposedMovementSqlAcceptance
{
    private sealed record StoredRow(string Table, long Id, string Payload);
    public static async Task TamperAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, "tamper", kind);
            var (room, actor, peer) = await Create(source, folder, kind);
            await DriveSql(source, folder, kind, room, actor, peer, complete: false);
            await AttackPending();
            // Complete the genuine transcript using a fresh registry/session before
            // the structurally coherent positive-balance attack. Never rewrite birth.
            await FinishSequence(async () => (await Load(source, room)).AuthoritativeState!, actor, peer, kind, async (player, command) => {
                var pending = await Load(source, room);
                var session = MatchSession.Restore(pending, new CoreRuleEngine(), new PostgresMatchJournal(source), new PostgresMatchPlayerStore(source));
                var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
                node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = pending.CurrentTick;
                var raw = JsonSerializer.SerializeToElement(node); command = Riftbound.Contracts.GameCommandJsonMapper.Map(raw);
                var id = "imposed-tamper-genuine-" + pending.Commands.Count;
                Check((await session.SubmitAsync(player, id, command, raw, default)).Accepted, "Complete the genuine quantity and payment transcript.");
                var cold = await Load(source, room); var digest = await Digest(source, room);
                session = MatchSession.Restore(cold, new CoreRuleEngine(), new PostgresMatchJournal(source), new PostgresMatchPlayerStore(source));
                Check((await session.SubmitAsync(player, id, command, raw, default)).Accepted && await Digest(source, room) == digest, "Genuine cold duplicates do not write.");
            });
            AssertFinal((await Load(source, room)).AuthoritativeState!, actor, kind);
            await AttackFinal();

            async Task AttackPending() => await Attacks(["payer", "cost", "preview", "transcript", "range", "kind"], false);
            async Task AttackFinal() => await Attacks(["coherent-forged-power"], true);
            async Task Attacks(string[] attacks, bool final)
            {
                var original = await Load(source, room); var digest = await Digest(source, room);
                var rows = new List<StoredRow>(); await using var connection = await source.OpenConnectionAsync();
                foreach (var (table, tickColumn) in new[] { ("state_snapshots", "state_tick"), ("snapshots", "snapshot_tick"), ("action_prompts", "prompt_tick") })
                {
                    await using var query = new NpgsqlCommand($"select id,payload::text from {table} where match_id=@room and {tickColumn}=(select max({tickColumn}) from {table} where match_id=@room) order by id", connection);
                    query.Parameters.AddWithValue("room", room); await using var reader = await query.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) rows.Add(new(table, reader.GetInt64(0), reader.GetString(1)));
                }
                Check(rows.Count == 5, "Capture one authoritative row and both exact private snapshot/prompt rows.");
                var outcomes = new List<object>();
                foreach (var attack in attacks)
                {
                    string? refusal = null; bool? shapeConsistent = null;
                    try
                    {
                        if (final)
                        {
                            var state = original.AuthoritativeState!;
                            var forged = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                                [actor] = state.RunePools[actor] with { Power = 99 } } };
                            var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
                            foreach (var row in rows)
                            {
                                if (row.Table == "state_snapshots") { await Write(row, JsonSerializer.Serialize(forged, Json)); continue; }
                                var player = await Owner(row);
                                await Write(row, JsonSerializer.Serialize(row.Table == "snapshots" ? (object)snapshots[player] : prompts[player], Json));
                            }
                            var coherent = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default);
                            Check(coherent is not null && coherent.IsConsistent, "Regenerated coherent views must pass shape consistency before original replay rejects forged power.");
                            shapeConsistent = true;
                            Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(coherent!, new CoreRuleEngine(), default)).Count > 0,
                                "Original birth/actions alone must disprove the coherent resource forgery.");
                        }
                        else
                        {
                            var row = rows.Single(r => r.Table == "state_snapshots"); var node = JsonNode.Parse(row.Payload)!;
                            var choice = node["pendingRuleChoice"]!;
                            if (attack == "payer") choice["request"]!["playerId"] = peer;
                            if (attack == "cost") choice["request"]!["powerCost"] = 99;
                            if (attack == "preview") choice["request"]!["preview"]!["runePools"]![actor]!["power"] = 99;
                            if (attack == "transcript") choice["answers"]![0]!["optionId"] = "FREE";
                            if (attack == "range") choice["request"]!["options"]![0]!["id"] = "QUANTITY:FORGED";
                            if (attack == "kind") choice["request"]!["kind"] = "IMPOSED_MOVEMENT_POWER_PAYMENT";
                            await Write(row, node.ToJsonString());
                        }
                        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source), new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source));
                        try { await registry.GetOrCreateAsync(room, default); }
                        catch (MatchSessionException error) when (error.Code == Riftbound.Contracts.ErrorCodes.RecoveryInconsistent) { refusal = error.Message; }
                        Check(refusal is not null, "A fresh registry must refuse forged imposed-movement state: " + attack);
                    }
                    finally { foreach (var row in rows) await Write(row, row.Payload); }
                    Check(await Digest(source, room) == digest, "Restore all original SQL payloads and seven table digests after " + attack);
                    await Load(source, room); outcomes.Add(new { attack, shapeConsistent, refusal, sevenTableDigestRestored = true });
                }
                await Save(folder, "registry-forgery-refusals-" + (final ? "final" : "pending") + ".json", new { room, outcomes, originalBirthReplayRestored = true });
                async Task Write(StoredRow row, string value)
                {
                    await using var update = new NpgsqlCommand($"update {row.Table} set payload=@value::jsonb where id=@id and match_id=@room", connection);
                    update.Parameters.AddWithValue("value", value); update.Parameters.AddWithValue("id", row.Id); update.Parameters.AddWithValue("room", room);
                    Check(await update.ExecuteNonQueryAsync() == 1, "Mutate only a captured current row of this new room.");
                }
                async Task<string> Owner(StoredRow row)
                {
                    await using var query = new NpgsqlCommand($"select player_id from {row.Table} where id=@id and match_id=@room", connection);
                    query.Parameters.AddWithValue("id", row.Id); query.Parameters.AddWithValue("room", room); return (string)(await query.ExecuteScalarAsync())!;
                }
            }
        }
    }
}
