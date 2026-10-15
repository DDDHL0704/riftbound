using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class CardPlayHistorySqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var branch in new[] { "resolved", "countered", "control" })
            await RunBranch(source, Path.Combine(output, branch), branch);
    }

    private static async Task RunBranch(NpgsqlDataSource source, string output, string branch)
    {
        Directory.CreateDirectory(output);
        var room = "typed-play-" + Guid.NewGuid().ToString("N");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var initial = Initial(room, branch == "control");
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var current = initial; var checks = new List<object>(); var submitted = 0; long sequence = 0;
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("audit-" + player), default);
        await File.WriteAllTextAsync(Path.Combine(output, "explicit-fixture-initial.json"), JsonSerializer.Serialize(initial, json));
        try
        {
            await Submit("P1", new PlayCardCommand("SPELL", branch == "control" ? "OGN·095/298" : "OGN·048/298",
                branch == "control" ? ["TARGET"] : [], OptionalCosts: []));
            Check(current.CardPlays.Finalized.Count == 1 && current.CardPlays.Resolved.Count == 0,
                "Confirmation must not fabricate a completed resolution.");
            await RejectMutation("missing-history", "payload - 'cardPlays'", "invalid or missing recorded rule input identity/payload");
            await RejectMutation("forged-type", "jsonb_set(payload,'{cardPlays,finalized,0,types}','7'::jsonb)", "typed card play history");
            if (branch != "resolved")
            {
                var target = current.StackItems.Single().StackItemId;
                await Submit("P1", new PassPriorityCommand());
                await Submit("P2", new PlayCardCommand("COUNTER", branch == "control" ? "VEN·152" : "OGN·064/298", [target], OptionalCosts: []));
            }
            for (var i = 0; i < 2; i++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            if (branch == "control")
            {
                var choice = current.PendingRuleChoice!;
                Check(choice.Request.Kind == "OPTIONAL_SPELL_CONTROL_PAYMENT", "Expected real Rebuttal payment.");
                await Submit("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["PAY:ANY"]));
                choice = current.PendingRuleChoice!;
                Check(choice.Request.Kind == "SPELL_CHOICE_REMAKE", "Expected real spell choice continuation.");
                await Submit("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
                for (var i = 0; i < 2; i++) await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            }
            var confirmed = current.CardPlays.Finalized.Single(e => e.Source.ObjectId == "SPELL");
            var completed = current.CardPlays.Resolved.SingleOrDefault(e => e.Source.ObjectId == "SPELL");
            Check(confirmed.PlayerId == "P1" && confirmed.Types == 4, "Original confirmation actor/type must remain exact.");
            Check(branch == "countered" ? completed is null : completed?.PlayerId == (branch == "control" ? "P2" : "P1"),
                "Successful resolution must use the current controller, while countered spells have no resolution.");
            Check(current.StackItems.Count == 0, "The complete chain must finish normally.");
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_TYPED_PLAY_HISTORY_COLD_AND_FULL_REPLAY_PASS", branch, room, submitted,
                events = sequence, coldRecoveries = checks.Count, structuralTamperRejections = 2,
                finalized = current.CardPlays.Finalized, resolved = current.CardPlays.Resolved, checks,
                finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                scope = "Explicit rules fixture with actual SQL commands/events/states and full original-initial replay. History absence and type tampering reject; whole-history causal forgery is not claimed closed."
            }, json));
            Console.WriteLine($"POSTGRES_TYPED_PLAY_HISTORY_PASS: {branch}, {submitted} commands, {sequence} events, {checks.Count} cold replays");
        }
        finally
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
            cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }

        async Task Submit(string actor, GameCommand command)
        {
            var id = "typed-history-" + submitted; var prompt = session.PromptFor(actor);
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), json))!;
            fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var result = await session.SubmitAsync(actor, id, command, raw, default);
            Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
            Check((await session.SubmitAsync(actor, id, command, raw, default)).Accepted, "Immediate duplicate must be idempotent.");
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL frame.");
            Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
            Check(frame.Commands.Count == submitted && frame.Events.Count == sequence
                && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "SQL history/state mismatch.");
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!, engine, default, frame.Events);
            Check(replay.IsMatch, string.Join("; ", replay.Errors));
            session = MatchSession.Restore(frame, engine, journal, players);
            Check((await session.SubmitAsync(actor, id, command, raw, default)).Accepted, "Cold duplicate must remain idempotent.");
            var duplicate = await store.LoadAsync(room, default);
            Check(duplicate?.Commands.Count == submitted && duplicate.Events.Count == sequence, "Duplicates must not append SQL rows.");
            checks.Add(new { current.Tick, sequence, finalized = current.CardPlays.Finalized.Count,
                resolved = current.CardPlays.Resolved.Count, replay.IsMatch });
        }

        async Task RejectMutation(string name, string expression, string diagnostic)
        {
            await using var connection = await source.OpenConnectionAsync();
            await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
            read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
            try
            {
                await using var forge = new NpgsqlCommand("update state_snapshots set payload=" + expression
                    + " where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
                var bad = await store.LoadAsync(room, default);
                Check(bad is not null && !bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains(diagnostic, StringComparison.OrdinalIgnoreCase)),
                    name + ": " + string.Join("; ", bad?.ValidationErrors ?? []));
                if (name == "missing-history") Check(bad!.AuthoritativeState is null, "A required-field omission must not become an empty-history fallback.");
            }
            finally
            {
                await using var restore = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                restore.Parameters.AddWithValue("room", room); restore.Parameters.AddWithValue("payload", saved); await restore.ExecuteNonQueryAsync();
            }
            Check((await store.LoadAsync(room, default))?.IsConsistent == true, "Restored SQL snapshot must be consistent.");
        }
    }

    private static MatchState Initial(string room, bool control)
    {
        var zones = new Dictionary<string, PlayerZones> {
            ["P1"] = PlayerZones.Empty with { Hand = ["SPELL"], MainDeck = ["D1", "D2"] },
            ["P2"] = PlayerZones.Empty with { Hand = ["COUNTER"], MainDeck = ["E1", "E2"], Base = control ? ["TARGET"] : [] } };
        var cards = new Dictionary<string, CardObjectState>();
        foreach (var (player, own) in zones)
        foreach (var id in own.Hand.Concat(own.MainDeck))
            cards[id] = new(id, cardNo: id == "COUNTER" ? control ? "VEN·152" : "OGN·064/298"
                : id == "SPELL" && control ? "OGN·095/298" : "OGN·048/298",
                tags: [CardObjectTags.SpellCard], ownerId: player, controllerId: player);
        if (control) cards["TARGET"] = new("TARGET", cardNo: "OGN·001/298", power: 9,
            tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2");
        var locations = new Dictionary<string, ObjectLocationState>();
        foreach (var (player, own) in zones)
        {
            foreach (var id in own.Hand) locations[id] = new(player, "HAND");
            foreach (var id in own.MainDeck) locations[id] = new(player, "MAIN_DECK");
            foreach (var id in own.Base) locations[id] = new(player, "BASE");
        }
        return new(room, 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) });
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
