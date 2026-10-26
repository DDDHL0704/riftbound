using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class InstructionPowerPaymentSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Cases = ["zero", "spend"];

    public static async Task PrepareAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, kind);
            Check(!Directory.Exists(folder), "Instruction payment fixtures require fresh case directories.");
            Directory.CreateDirectory(folder);
            var suffix = Guid.NewGuid().ToString("N");
            var room = "instruction-payment-" + suffix;
            var actor = "instruction-" + kind + "-a-" + suffix[..12];
            var peer = "instruction-" + kind + "-b-" + suffix[..12];
            var players = new PostgresMatchPlayerStore(source);
            var session = new MatchSession(room, new CoreRuleEngine(), new PostgresMatchJournal(source), players);
            var tokens = new Dictionary<string, string> {
                [actor] = (await session.EnsurePlayerAsync(actor, default)).ReconnectToken,
                [peer] = (await session.EnsurePlayerAsync(peer, default)).ReconnectToken };
            const string scenario = "typed-power-payment-mixed-recycle";
            var seeded = await session.SeedScenarioAsync(actor, "instruction-original-qa-seed", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
            Check(seeded.Accepted, seeded.ErrorMessage);
            var frame = await Load(source, room);
            Check(frame.Commands.Count == 3 && frame.ReplayInitialState is { Tick: 0, Seats.Count: 0 }
                && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current,
                "Persist the actual empty room, two original seats and accepted finite QA command.");
            Check(frame.AuthoritativeState!.RunePools[actor].TotalPower == 1, "The original instruction pool must contain one red power.");
            await Save(folder, "network-fixture.json", new {
                room, actor, peer, tokens, kind, scenario, quantity = kind == "zero" ? 0 : 2,
                source = "P1-SPELL-BULLET-TIME", target = "P2-BULLET-TIME-UNIT-001",
                rune = "P1-RUNE-RED-PARTIAL-PAYMENT-001", field = "N54-SELECTED-FIELD",
                controlledQaPosition = true, ordinaryOpeningCertified = false, originalRuleInputs = RuleInputIdentity.Current });
            await Save(folder, "original-prepared-frame.json", frame);
        }
    }

    public static async Task VerifyAsync(NpgsqlDataSource source, string output, string stage)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, kind);
            using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-fixture.json")));
            var root = fixture.RootElement; var room = root.GetProperty("room").GetString()!;
            var actor = root.GetProperty("actor").GetString()!; var quantity = root.GetProperty("quantity").GetInt32();
            var frame = await Load(source, room); var state = frame.AuthoritativeState!;
            var final = stage == "final";
            Check(frame.Commands.Count == (final ? 10 : 9) && frame.Commands.Count(c => !c.Accepted) == 2,
                "Keep original seats/seed, play, two passes, resource, two rejections and final payment; duplicates and peer spoof add no rows.");
            Check(frame.Events.Count(e => e.Event.Kind == "RUNE_RECYCLED") == 1,
                "The confirmed resource commits once before the instruction quantity, without repetition after restart or final payment.");
            var resource = frame.Commands.Single(c => c.ClientIntentId == "instruction-resource");
            var recycled = frame.Events.Single(e => e.Event.Kind == "RUNE_RECYCLED");
            Check(resource.Accepted && recycled.Tick == resource.CompletedTick
                && recycled.Sequence > resource.StartedEventSequence && recycled.Sequence <= resource.CompletedEventSequence,
                "The recycle event belongs to the accepted resource confirmation row.");
            var rune = root.GetProperty("rune").GetString()!;
            Check(!state.PlayerZones[actor].Base.Contains(rune) && state.PlayerZones[actor].RuneDeck.Contains(rune),
                "The confirmed rune remains recycled before and after the actual API restart and final payment.");
            var play = frame.Events.Single(e => e.Event.Kind == "COST_PAID"
                && Payload(e.Event).GetProperty("paymentWindow").GetString() == "PLAY_CARD");
            Check(Payload(play.Event).GetProperty("power").GetInt32() == 0 && state.RunePools[actor].Mana == 0,
                "Declaration pays the printed mana and never prepays the resolution quantity.");
            if (!final)
            {
                var pending = state.PendingRuleChoice;
                Check(pending is { Request.Kind: "INSTRUCTION_POWER_PAYMENT", Answers.Count: 1 }
                    && pending.Request.PlayerId == actor && state.RunePools[actor].TotalPower == 2
                    && pending.Origin is not null && pending.Origin.RunePools[actor].TotalPower == 1
                    && pending.Origin.PlayerZones[actor].Base.Contains(rune)
                    && pending.Request.Preview!.RunePools[actor].TotalPower == 2
                    && pending.Request.Preview.CardObjects[root.GetProperty("target").GetString()!].Damage == 0,
                    "The durable state commits the resource while the unchanged original checkpoint and instruction remain independently validated.");
                Check(frame.Events.All(e => e.Event.Kind != "DAMAGE_APPLIED"), "No instruction damage is committed before choosing the quantity.");
            }
            else
            {
                Check(state.PendingRuleChoice is null && state.StackItems.Count == 0
                    && state.RunePools[actor].TotalPower == 2 - quantity
                    && state.CardObjects[root.GetProperty("target").GetString()!].Damage == quantity,
                    "After real restart, pay exactly zero/two and preserve the remaining resources and actual damage.");
                var paid = frame.Events.Single(e => e.Event.Kind == "COST_PAID"
                    && Payload(e.Event).GetProperty("paymentWindow").GetString() == "INSTRUCTION_POWER_PAYMENT");
                Check(Payload(paid.Event).GetProperty("totalPowerCost").GetInt32() == quantity,
                    "The real instruction receipt must bind the chosen quantity.");
            }
            var digest = await Digest(source, room);
            if (stage == "after-restart")
            {
                using var before = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "sql-before-restart.json")));
                Check(before.RootElement.GetProperty("sevenTableDigest").GetString() == digest,
                    "The actual API restart, authenticated reconnect and old resource duplicate do not alter any original table.");
            }
            await Save(folder, "sql-" + stage + ".json", new {
                status = "INSTRUCTION_PAYMENT_SQL_ORIGINAL_REPLAY_PASS", stage, room,
                commands = frame.Commands.Count, events = frame.Events.Count, sevenTableDigest = digest,
                finalHash = MatchStateHasher.Hash(state), currentRuleInputs = RuleInputIdentity.Current,
                controlledQaPosition = true, ordinaryOpeningCertified = false });
            await Save(folder, "original-" + stage + "-frame.json", frame);
        }
    }

    public static async Task TamperAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, kind);
            using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-fixture.json")));
            var room = fixture.RootElement.GetProperty("room").GetString()!;
            var actor = fixture.RootElement.GetProperty("actor").GetString()!;
            var digest = await Digest(source, room);
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room);
            var original = (string)(await read.ExecuteScalarAsync())!;
            var forged = JsonNode.Parse(original)!;
            forged["runePools"]![actor]!["mana"] = 99;
            string? refusal = null;
            try
            {
                await Write(forged.ToJsonString());
                var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source),
                    new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source));
                try { await registry.GetOrCreateAsync(room, default); }
                catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { refusal = error.Message; }
                Check(refusal is not null, "A fresh real registry must refuse the deliberately forged instruction resource result.");
            }
            finally { await Write(original); }
            Check(await Digest(source, room) == digest, "Restore the exact original payload and all seven tables after the negative test.");
            await Load(source, room);
            await Save(folder, "registry-forgery-refusal.json", new { room, refusal, originalPayloadRestored = true, sevenTableDigestRestored = true });

            async Task Write(string value)
            {
                await using var update = new NpgsqlCommand("update state_snapshots set payload=@value::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                update.Parameters.AddWithValue("room", room); update.Parameters.AddWithValue("value", value);
                Check(await update.ExecuteNonQueryAsync() == 1, "Corrupt only this acceptance room's single latest state.");
            }
        }
    }

    private static JsonElement Payload(GameEvent ev) => JsonSerializer.SerializeToElement(ev.Payload, Json);

    private static async Task<MatchRecoveryFrame> Load(NpgsqlDataSource source, string room)
    {
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default)
            ?? throw new InvalidOperationException("Missing original instruction payment history.");
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(frame.IsConsistent && errors.Count == 0, string.Join("; ", frame.ValidationErrors.Concat(errors)));
        return frame;
    }

    private static async Task<string> Digest(NpgsqlDataSource source, string room)
    {
        var rows = new StringBuilder();
        await using var connection = await source.OpenConnectionAsync();
        foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
        {
            await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text), '') from {table} t where match_id=@room", connection);
            query.Parameters.AddWithValue("room", room);
            rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
    }

    private static Task Save(string folder, string name, object value)
        => File.WriteAllTextAsync(Path.Combine(folder, name), JsonSerializer.Serialize(value, value.GetType(), Json));

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
