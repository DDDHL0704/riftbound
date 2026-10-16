using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class OpeningRandomnessSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var room = "postgres-private-opening-" + Guid.NewGuid().ToString("N");
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        var options = new MatchSessionOptions(false, OfficialDeckFormat.ChinaStandard20260724);
        InMemoryMatchSessionRegistry Registry() => new(engine, journal, store, players, options);
        IMatchSession session = await Registry().GetOrCreateAsync(room, default);
        var tokens = new Dictionary<string, string>();
        foreach (var player in new[] { "alice", "bob" })
            tokens[player] = (await session.EnsurePlayerAsync(player, default)).ReconnectToken;
        var waiting = await Load();
        Check(waiting.IsConsistent && waiting.Commands.Count == 0 && waiting.CurrentTick == 0,
            "Fresh production room must persist before its first command.");
        var initial = waiting.AuthoritativeState!; var current = initial; var seed = waiting.OpeningSeed;
        Check(seed != 0 && initial.Seed == seed && initial.CardObjects.Count == 0 && initial.Status == MatchStatuses.Seating,
            "Real private entropy must bind the original empty SEATING input.");
        var seedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed.ToString(CultureInfo.InvariantCulture)))).ToLowerInvariant();
        var checks = new List<object>(); var submitted = 0; var tampered = new List<string>();
        await Cold("before-first-command");
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).First(candidate =>
            OfficialDeckValidator.Validate(candidate.Decklist, catalog, OfficialDeckFormat.ChinaStandard20260724).IsValid).Decklist;
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo,
            deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Act("alice", submit); await Act("bob", submit);
        await Cold("both-decks-before-ready");
        await Ready("alice"); await Ready("bob");
        Check(current.Phase == MatchPhases.Mulligan && current.RngCursor == 7, "All four opening consumers must run.");
        await Cold("private-mulligan-hands");
        await Act(current.ActivePlayerId, new MulliganCommand([]));
        await Cold("second-mulligan-pending");
        await Act(current.OpeningSecondActionPlayerId!, new MulliganCommand([]));
        await Cold("first-turn-started");
        await Act(current.TurnPlayerId, new EndTurnCommand());
        await Cold("next-turn");
        foreach (var mutation in new[] { "missing", "malformed", "noncanonical", "wrong-value" })
        {
            await ChangeSeed(mutation switch {
                "missing" => null, "malformed" => "invalid-private-seed",
                "noncanonical" => " " + seed.ToString(CultureInfo.InvariantCulture),
                _ => (seed ^ 1).ToString(CultureInfo.InvariantCulture)
            });
            try
            {
                var bad = await Load();
                Check(!bad.IsConsistent && bad.ValidationErrors.Any(error => error.Contains("seed", StringComparison.Ordinal)),
                    "SQL-forged private opening input must be rejected independently.");
                var rejected = false;
                try { await Registry().GetOrCreateAsync(room, default); }
                catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { rejected = true; }
                Check(rejected, "Actual cold registry must reject forged SQL opening input.");
                Check(MatchStateHasher.Hash(bad.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    "Rejecting metadata tampering must not rewrite accepted history/state.");
                tampered.Add(mutation);
            }
            finally { await ChangeSeed(seed.ToString(CultureInfo.InvariantCulture)); }
        }
        await Cold("after-tamper-restored");
        var final = await Load();
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_PRIVATE_OPENING_SEED_COLD_FULL_ORIGINAL_REPLAY_TAMPER_AND_PROJECTION_PASS",
            room, submitted, events = final.Events.Count, originalTick = initial.Tick, originalCards = initial.CardObjects.Count,
            originalSeedSha256 = seedHash, privateSeedRecordedBeforeFirstCommand = true,
            allFourOpeningConsumers = true, coldCheckpoints = checks, tamperRejections = tampered,
            originalSeedAndAcceptedHistoryNeverRewritten = true, finalHash = MatchStateHasher.Hash(current),
            ruleInputs = current.RuleInputs, databaseRoomRetained = true,
            scope = "Real fresh production entry, CN legal decks, private seed, exact SQL recovery/replay and four metadata mutations; visible score victory and Windows remain separate."
        }, json));
        Console.WriteLine($"POSTGRES_PRIVATE_OPENING_PASS: {submitted} commands, {final.Events.Count} events, {checks.Count} cold checkpoints, {tampered.Count} SQL input mutations rejected; private seed omitted.");

        async Task<MatchRecoveryFrame> Load() => await store.LoadAsync(room, default)
            ?? throw new InvalidOperationException("Missing actual SQL private-opening frame.");
        async Task ChangeSeed(string? value)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var command = new NpgsqlCommand("update matches set seed=@seed where match_id=@room", connection);
            command.Parameters.AddWithValue("room", room);
            command.Parameters.AddWithValue("seed", NpgsqlDbType.Text, (object?)value ?? DBNull.Value);
            Check(await command.ExecuteNonQueryAsync() == 1, "Exactly one isolated SQL room must be mutated.");
        }
        async Task Cold(string label)
        {
            var frame = await Load();
            Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            Check(frame.OpeningSeed == seed && frame.AuthoritativeState!.Seed == seed
                && frame.Commands.Count == submitted && MatchStateHasher.Hash(frame.AuthoritativeState) == MatchStateHasher.Hash(current),
                "SQL must retain exact private seed, accepted commands and authoritative state.");
            if (submitted > 0)
            {
                Check(frame.ReplayInitialState!.Seed == seed && frame.ReplayInitialState.CardObjects.Count == 0,
                    "Replay must begin at the actual original empty input.");
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0,
                    "Independent full original action/event/state replay must match.");
            }
            session = await Registry().GetOrCreateAsync(room, default);
            foreach (var player in tokens.Keys.ToArray())
            {
                tokens[player] = (await session.ReconnectPlayerAsync(player, tokens[player], default)).ReconnectToken;
                var actual = JsonSerializer.Serialize(session.SnapshotFor(player), json);
                var expected = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(current)[player], json);
                var equal = JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected));
                if (actual != expected)
                    await File.WriteAllTextAsync(Path.Combine(output, label + "-" + player + "-snapshot-comparison.json"),
                        JsonSerializer.Serialize(new { equal, actual, expected,
                            comparison = "JSON object property order ignored; every array position/value retained" }, json));
                Check(equal, "Private snapshot changed on cold reconnect.");
            }
            checks.Add(new { label, tick = current.Tick, commands = frame.Commands.Count, events = frame.Events.Count,
                stateHash = MatchStateHasher.Hash(current), originalSeedSha256 = seedHash });
        }
        async Task Ready(string player)
        {
            var command = new ReadyCommand();
            Record(await session.ReadyAsync(player, "opening-" + submitted, Raw(command), default));
        }
        async Task Act(string player, GameCommand command)
        {
            var id = "opening-" + submitted; var raw = Raw(command);
            Record(command is SubmitDeckCommand deckCommand
                ? await session.SubmitDeckAsync(player, id, deckCommand, raw, default)
                : await session.SubmitAsync(player, id, command, raw, default));
        }
        JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), json);
        void Record(ResolutionResult result)
        {
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++;
            Check(current.Seed == seed, "Production seed must remain the original private input.");
            var publicPayload = JsonSerializer.Serialize(new { result.Snapshots, result.Prompts, result.Events,
                spectator = MatchReplayRedactor.BuildSpectatorFrame(room, current.Tick, 0, [], current) }, json);
            Check(!publicPayload.Contains(seed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), "Public projections exposed the private seed.");
            foreach (var player in current.Seats.Keys)
            {
                var view = JsonSerializer.Serialize(new { snapshot = result.Snapshots[player], prompt = result.Prompts[player] }, json);
                foreach (var other in current.Seats.Keys.Where(other => other != player))
                    foreach (var hidden in current.PlayerZones[other].Hand.Concat(current.PlayerZones[other].MainDeck).Concat(current.PlayerZones[other].RuneDeck))
                        Check(!view.Contains(hidden, StringComparison.Ordinal), "Player view exposed an opponent's hidden opening identity.");
            }
        }
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
