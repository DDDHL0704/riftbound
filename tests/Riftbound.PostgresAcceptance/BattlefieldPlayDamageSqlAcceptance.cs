using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class BattlefieldPlayDamageSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "battlefield-merfolk-current-opening.json"));
        using var fixture = JsonDocument.Parse(bytes);
        var data = fixture.RootElement;
        Check(data.GetProperty("openingSeed").GetInt64() == 0, "Keep the original explicit seed0.");
        var room = "postgres-battlefield-damage-" + Guid.NewGuid().ToString("N");
        var initial = MatchReplayInitialStateBuilder.FromSeats(room, data.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source);
        var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("battlefield-damage-" + player), default);
        var current = initial;
        var checkpoints = new List<object>();
        var index = 0;
        var originalNextAfterCold = false;
        foreach (var entry in data.GetProperty("acceptedCommands").EnumerateArray())
        {
            var actor = entry.GetProperty("playerId").GetString()!;
            var fields = JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject();
            fields["promptId"] = session.PromptFor(actor).PromptId;
            fields["snapshotTick"] = current.Tick;
            var raw = JsonSerializer.SerializeToElement(fields);
            var command = GameCommandJsonMapper.Map(raw);
            var intent = entry.GetProperty("clientIntentId").GetString()!;
            var expected = originalNextAfterCold ? await engine.ResolveAsync(current, new(intent, actor, command.CmdType), command, default) : null;
            var result = command switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(actor, intent, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                _ => await session.SubmitAsync(actor, intent, command, raw, default)
            };
            Check(result.Accepted, result.ErrorMessage);
            if (originalNextAfterCold)
                Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)),
                    "The original next command and events must match after a pending SQL cold restore.");
            originalNextAfterCold = false;
            current = result.State;
            index++;
            if (index == data.GetProperty("pendingChoiceCommandCount").GetInt32()
                || index == data.GetProperty("confirmedCommandCount").GetInt32())
            {
                var pending = index == data.GetProperty("pendingChoiceCommandCount").GetInt32();
                Check(pending ? current.PendingCardChoice?.ChoiceWindow == "TRIGGER_CONFIRMATION"
                    && current.PendingCardChoice.LegalObjectIds.SequenceEqual([data.GetProperty("expectedTarget").GetString()!])
                    : current.PendingCardChoice is null && current.PriorityPlayerId == data.GetProperty("expectedActor").GetString(),
                    "Retain the exact pending target or confirmed response window.");
                var frame = await Cold();
                checkpoints.Add(new { commandCount = index, pendingChoice = pending, commands = frame.Commands.Count,
                    events = frame.Events.Count, fullOriginalReplay = true, sevenSqlTablesUnchanged = true,
                    source = AssertItem().PermanentPlaySource });
                originalNextAfterCold = true;
            }
        }
        Check(checkpoints.Count == 2, "Both pending checkpoints must actually run.");
        var target = data.GetProperty("expectedTarget").GetString()!;
        var opponent = data.GetProperty("expectedOpponent").GetString()!;
        Check(current.PlayerZones[opponent].Graveyard.Contains(target), "The real2 Might enemy must be destroyed by damage cleanup.");
        var final = await Cold();
        var damage = final.Events.Where(e => e.Event.Kind == "DAMAGE_APPLIED")
            .Select(e => JsonSerializer.SerializeToElement(e.Event.Payload)).Single(e => e.GetProperty("targetObjectId").GetString() == target);
        Check(damage.GetProperty("damage").GetInt32() == 2, "The official printed damage is2.");
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_BATTLEFIELD_PLAY_DAMAGE_PENDING_COLD_ORIGINAL_REPLAY_PASS", executedAtUtc = DateTimeOffset.UtcNow,
            room, originalExplicitSeed = 0, commands = final.Commands.Count, events = final.Events.Count,
            checkpoints, originalNextCommandsMatched = 2, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
            fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), databaseRoomRetained = true,
            scope = "One actual blank opening, legal decks and original42 commands; target-choice and confirmed-response SQL cold restoration, original next commands, damage2/destruction and complete original replay. Visible native and Windows acceptance remain separate."
        }, json));
        Console.WriteLine("POSTGRES_BATTLEFIELD_PLAY_DAMAGE_PASS: actual opening,2 pending SQL cold restores,2 original next commands, damage2 and complete original replay.");

        StackItemState AssertItem()
            => current.StackItems.Single(item => item.SourceObjectId == data.GetProperty("expectedSource").GetString());

        async Task<MatchRecoveryFrame> Cold()
        {
            var before = await Digest();
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual SQL frame.");
            Check(frame.IsConsistent && frame.OpeningSeed == 0 && frame.ReplayInitialState is { Tick: 0, Status: MatchStatuses.Seating }
                && frame.ReplayInitialState.CardObjects.Count == 0 && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current),
                "Exact original opening and state must survive SQL restore: " + string.Join("; ", frame.ValidationErrors));
            Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Full original replay must match.");
            var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
            foreach (var player in current.Seats.Keys)
                Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))),
                    "Both exact player snapshots and prompts must survive.");
            Check(await Digest() == before, "Restoring may not rewrite any accepted SQL row.");
            session = restored;
            return frame;
        }

        async Task<string> Digest()
        {
            var rows = new StringBuilder();
            await using var connection = await source.OpenConnectionAsync();
            foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
            {
                await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text),'') from {table} t where match_id=@room", connection);
                query.Parameters.AddWithValue("room", room);
                rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
        }
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
