using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static partial class RestrictedManaSqlAcceptance
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static readonly string[] Cases = ["ogs", "sp6"];
    public static async Task PrepareAsync(NpgsqlDataSource source, string output)
    { foreach (var kind in Cases) await Create(source, Path.Combine(output, kind), kind); }

    private static async Task Create(NpgsqlDataSource source, string folder, string kind)
    {
        Check(!Directory.Exists(folder), "Use a new restricted-mana fixture directory."); Directory.CreateDirectory(folder);
        var suffix = Guid.NewGuid().ToString("N"); var room = "restricted-mana-" + suffix;
        var actor = "restricted-a-" + suffix[..12]; var peer = "restricted-b-" + suffix[..12];
        var session = new MatchSession(room, new CoreRuleEngine(), new PostgresMatchJournal(source), new PostgresMatchPlayerStore(source));
        var tokens = new Dictionary<string, string> { [actor] = (await session.EnsurePlayerAsync(actor, default)).ReconnectToken,
            [peer] = (await session.EnsurePlayerAsync(peer, default)).ReconnectToken };
        var scenario = "restricted-mana-lux-" + kind;
        var seeded = await session.SeedScenarioAsync(actor, "restricted-original-qa-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
        Check(seeded.Accepted, seeded.ErrorMessage);
        var frame = await Load(source, room);
        Check(frame.Commands.Count == 3 && frame.Commands[0].CommandType == "ROOM_SEAT" && frame.Commands[1].CommandType == "ROOM_SEAT"
            && frame.ReplayInitialState is { Tick: 0, Seats.Count: 0 }, "Keep the original empty-room birth and two accepted seats.");
        Check(frame.AuthoritativeState!.RunePools[actor] is { Mana: 1, SpellOnlyMana: 0, TotalPower: 0 }, "Start with ordinary mana only.");
        await Save(folder, "network-fixture.json", new { room, actor, peer, tokens, kind, scenario,
            card = kind == "ogs" ? "OGS·014/024" : "VEN·SP6", controlledQaPosition = true, ordinaryOpeningCertified = false });
        await Save(folder, "original-prepared-frame.json", frame);
    }

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, "sql", kind); await Create(source, folder, kind);
            using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-fixture.json")));
            var root = fixture.RootElement; var room = root.GetProperty("room").GetString()!;
            var actor = root.GetProperty("actor").GetString()!; var peer = root.GetProperty("peer").GetString()!;
            var session = Restore(await Load(source, room));
            var coldRestores = 1;
            await Act(actor, new ActivateAbilityCommand("QA-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
            var before = await Load(source, room);
            var first = new PlayCardCommand("QA-FIRST", "OGN·268/298", ["QA-FIELD"]);
            var quote = new CoreRuleEngine().PreviewPlayCard(before.AuthoritativeState!, actor,
                new("restricted-sql-quote", session.PromptFor(actor).PromptId!, before.CurrentTick, first));
            Check(quote.CanPay && quote.Cost is { AvailableMana: 3, AvailableSpellOnlyMana: 2, RemainingMana: 2, RemainingSpellOnlyMana: 1 },
                "The authorized mixed quote must preserve one ordinary and one restricted mana.");
            await Act(actor, first); await Act(actor, new PassPriorityCommand());
            await Act(peer, new ActivateAbilityCommand("QA-PEER-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
            await Act(peer, new PassPriorityCommand()); await DecideResource();
            var pending = (await Load(source, room)).AuthoritativeState!;
            Check(pending.RunePools[actor].SpellOnlyMana == 1 && pending.PendingRuleChoice!.Request.Preview!.RunePools[actor].SpellOnlyMana == 3,
                "Retain the real frozen board and independently replayed resource preview.");
            await Decide("SPEND_POWER:0");
            await Act(actor, new PlayCardCommand("QA-SECOND", "OGN·268/298", ["QA-FIELD"]));
            await Act(actor, new PassPriorityCommand()); await Act(peer, new PassPriorityCommand()); await Decide("SPEND_POWER:0");
            var remaining = (await Load(source, room)).AuthoritativeState!;
            Check(remaining.RunePools[actor] is { Mana: 1, SpellOnlyMana: 2 } && remaining.RunePools[peer].SpellOnlyMana == 2,
                "Keep both players' genuine restricted balances across two spells.");
            await Act(actor, new PlayCardCommand("QA-UNIT", "OGN·044/298", []), false);
            await Act(actor, new EndTurnCommand());
            var final = await Load(source, room);
            Check(final.AuthoritativeState!.RunePools.Values.All(p => p == RunePool.Empty)
                && final.AuthoritativeState is { Status: MatchStatuses.InProgress, Phase: MatchPhases.Main }
                && final.AuthoritativeState.ActivePlayerId == peer, "Clear both players and normally enter the opponent's main phase.");
            await Save(folder, "sql-original-replay.json", new { status = "RESTRICTED_MANA_SQL_PASS", quote,
                commands = final.Commands.Count, events = final.Events.Count, sevenTableDigest = await Digest(source, room),
                birth = final.Commands.Take(3).Select(c => c.CommandType), fullOriginalReplay = true, actualColdRestores = coldRestores });

            MatchSession Restore(MatchRecoveryFrame frame) => MatchSession.Restore(frame, new CoreRuleEngine(),
                new PostgresMatchJournal(source), new PostgresMatchPlayerStore(source));
            async Task Act(string player, GameCommand command, bool accepted = true)
            {
                var frame = await Load(source, room); var prompt = session.PromptFor(player);
                var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
                node["promptId"] = prompt.PromptId; node["snapshotTick"] = frame.CurrentTick;
                var raw = JsonSerializer.SerializeToElement(node); var id = "restricted-sql-" + frame.Commands.Count;
                var result = await session.SubmitAsync(player, id, command, raw, default);
                Check(result.Accepted == accepted, result.ErrorMessage);
                if (!accepted) Check(result.ErrorCode == ErrorCodes.InsufficientCost && result.Events.Count == 0,
                    "A unit cannot spend restricted mana; its rejected action has no effects.");
                var cold = await Load(source, room); var digest = await Digest(source, room);
                if (accepted) { session = Restore(cold); coldRestores++; }
                var duplicate = await session.SubmitAsync(player, id, command, raw, default);
                Check(duplicate.Accepted == accepted && await Digest(source, room) == digest, "A cold duplicate must preserve every original table.");
            }
            async Task Decide(string choice)
            {
                var request = (await Load(source, room)).AuthoritativeState!.PendingRuleChoice!.Request;
                await Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice]));
            }
            async Task DecideResource()
            {
                var request = (await Load(source, room)).AuthoritativeState!.PendingRuleChoice!.Request;
                await Decide(request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-SECOND-LUX:")).Id);
            }
        }
    }

    public static async Task VerifyAsync(NpgsqlDataSource source, string output, string stage)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, kind);
            using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-fixture.json")));
            var root = fixture.RootElement; var room = root.GetProperty("room").GetString()!;
            var actor = root.GetProperty("actor").GetString()!; var peer = root.GetProperty("peer").GetString()!;
            var frame = await Load(source, room); var state = frame.AuthoritativeState!;
            Check(frame.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT") && frame.Commands[2].CommandType.StartsWith("DEV_SEED_SCENARIO:"),
                "The restart must use original room birth rather than a fabricated seed snapshot.");
            Check(frame.Commands.Count(c => !c.Accepted) == (stage == "final" ? 3 : 2), "Keep every rejection and exclude duplicate/spoof journal writes.");
            if (stage != "final")
            {
                Check(state.RunePools[actor] is { Mana: 1, SpellOnlyMana: 1 } && state.RunePools[peer].SpellOnlyMana == 2,
                    "Keep ordinary and restricted resources distinct in the uncommitted original board.");
                Check(state.PendingRuleChoice is { Answers.Count: 1, Request.Kind: "INSTRUCTION_POWER_PAYMENT" }
                    && state.PendingRuleChoice.Request.Preview!.RunePools[actor] is { Mana: 1, SpellOnlyMana: 3, TotalPower: 0 },
                    "Validate the instruction resource transcript and projected balance across process restart.");
                Check(frame.Events.Count(e => e.Event.Kind == "MANA_GAINED") == 2, "The second Lux gain is not committed before the instruction finishes.");
            }
            else
            {
                Check(state.PendingRuleChoice is null && state.StackItems.Count == 0 && state.RunePools.Values.All(p => p == RunePool.Empty)
                    && state.Status == MatchStatuses.InProgress && state.Phase == MatchPhases.Main && state.ActivePlayerId == peer,
                    "The native finish must spend remaining restricted mana and actually clear both players at turn end.");
                Check(frame.Events.Count(e => e.Event.Kind == "MANA_GAINED") == 3 && state.CardObjects["QA-TARGET"].Damage == 0,
                    "Commit each genuine Lux gain once, without instruction damage or restricted instruction spending.");
            }
            var digest = await Digest(source, room);
            await PaymentReconnectEvidence.VerifyCheckpoint(source, room, folder, stage);
            await Save(folder, "sql-" + stage + ".json", new { status = "RESTRICTED_MANA_SQL_ORIGINAL_REPLAY_PASS", stage, room,
                commands = frame.Commands.Count, events = frame.Events.Count, sevenTableDigest = digest, fullOriginalReplay = true,
                stateHash = MatchStateHasher.Hash(state), frame.PlayerViews, frame.SpectatorReplayFrame });
        }
    }

    internal static async Task<MatchRecoveryFrame> Load(NpgsqlDataSource source, string room)
    {
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing original restricted-mana history.");
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(frame.IsConsistent && errors.Count == 0, string.Join("; ", frame.ValidationErrors.Concat(errors))); return frame;
    }
    internal static async Task<string> Digest(NpgsqlDataSource source, string room)
    {
        var rows = new StringBuilder(); await using var connection = await source.OpenConnectionAsync();
        foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
        {
            await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text), '') from {table} t where match_id=@room", connection);
            query.Parameters.AddWithValue("room", room); rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
    }
    internal static Task Save(string folder, string name, object value) => File.WriteAllTextAsync(Path.Combine(folder, name), JsonSerializer.Serialize(value, value.GetType(), Json));
    internal static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
