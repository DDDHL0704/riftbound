using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class OptionalAdditionalCostReductionSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var receipts = new List<object>();
        foreach (var scenario in new[] {
            (Name: "149-echo", Card: "SFD·149/221", Haste: false, Copied: false),
            (Name: "149a-echo", Card: "SFD·149a/221", Haste: false, Copied: false),
            (Name: "149-haste", Card: "SFD·149/221", Haste: true, Copied: false),
            (Name: "149a-haste", Card: "SFD·149a/221", Haste: true, Copied: false),
            (Name: "attached-revision", Card: "SFD·149/221", Haste: false, Copied: true) })
        {
            var room = "postgres-optional-reduction-" + Guid.NewGuid().ToString("N");
            var directory = Path.Combine(output, scenario.Name);
            Directory.CreateDirectory(directory);
            var initial = await PositionAsync(room, scenario.Card, scenario.Haste, scenario.Copied);
            var engine = new CoreRuleEngine();
            var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source);
            var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            foreach (var actor in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("optional-reduction-" + actor), default, initial.Seed);
            var current = initial;
            var submitted = 0;
            var checkpoints = new List<object>();
            string? oldCopiedChoice = null;
            if (scenario.Copied)
            {
                await Attach("ATTACH");
                oldCopiedChoice = CopyChoice(session.PromptFor("P1"));
                var generation = current.CardObjects["GEAR"].ObjectGeneration;
                var revision = current.CardObjects["GEAR"].AttachmentRevision;
                await Attach("DETACH");
                Check(current.CardObjects["GEAR"].AttachedRuleText is null, "The actual detach must remove the copied passive.");
                await Attach("REATTACH");
                Check(current.CardObjects["GEAR"].ObjectGeneration == generation
                    && current.CardObjects["GEAR"].AttachmentRevision > revision
                    && CopyChoice(session.PromptFor("P1")) != oldCopiedChoice,
                    "A new attachment revision must have a distinct choice, even with the same physical generations.");
            }
            var reduction = "OPTIONAL_COST_REDUCTION:EZREAL:0:" + (scenario.Haste ? "ADDITIONAL:POWER-red" : "ECHO:MANA");
            var fees = scenario.Haste ? new[] { HasteOptionalCostNames.HasteReady } : ["ECHO"];
            var selected = fees.Append(reduction).ToList();
            if (scenario.Copied) selected.Add(CopyChoice(session.PromptFor("P1")));
            var command = new PlayCardCommand("CARD", scenario.Haste ? "SFD·029/221" : "SFD·066/221",
                scenario.Haste ? [] : ["TARGET"], OptionalCosts: selected);
            var invalid = command with { OptionalCosts = fees.Append(oldCopiedChoice
                ?? "OPTIONAL_COST_REDUCTION:EZREAL:1:" + (scenario.Haste ? "ADDITIONAL:POWER-red" : "ECHO:MANA")).ToArray() };
            var before = MatchStateHasher.Hash(current);
            var rejected = await session.SubmitAsync("P1", "invalid-reduction-source", invalid, Raw("P1", invalid), default);
            Check(!rejected.Accepted && rejected.ErrorCode == ErrorCodes.InvalidTarget && rejected.Events.Count == 0
                && MatchStateHasher.Hash(rejected.State) == before, "A stale generation or detached/replaced instance must reject atomically.");
            await Cold("invalid-instance-rejected");
            var quote = await session.PreviewPlayCardAsync("P1", new("sql-optional-quote", session.PromptFor("P1").PromptId!, current.Tick, command), default);
            var expectedMana = scenario.Haste ? 4 : scenario.Copied ? 2 : 3;
            Check(quote.CanPay && quote.Cost?.Mana == expectedMana
                && quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum() == 0,
                "SQL session quote must reduce only the selected optional fee and preserve the base fee.");
            var raw = Raw("P1", command);
            var paid = await session.SubmitAsync("P1", "explicit-optional-reduction", command, raw, default);
            Check(paid.Accepted, paid.ErrorMessage);
            Check(paid.State.RunePools["P1"].Mana == current.RunePools["P1"].Mana - expectedMana
                && paid.State.RunePools["P1"].TotalPower == current.RunePools["P1"].TotalPower,
                "Committed resources must match the authoritative quote without spending any power for the reduced optional fee.");
            current = paid.State;
            Check(((string[])paid.Events.Single(ev => ev.Kind == "COST_PAID").Payload["optionalCosts"]!).SequenceEqual(fees),
                "A zero fee must retain its original declaration in the committed effect facts.");
            if (scenario.Haste) Check(!current.CardObjects["CARD"].IsExhausted, "The fully reduced Haste fee must still ready the unit.");
            else Check(current.StackItems.Single().EffectRepeatCount == 2, "The selected Echo must still repeat when its fee is reduced to zero.");
            var frame = await Cold("paid-optional-fee-and-original-replay");
            var digest = await DigestAsync(source, room);
            var duplicate = await session.SubmitAsync("P1", "explicit-optional-reduction", command, raw, default);
            Check(duplicate.Accepted && MatchStateHasher.Hash(duplicate.State) == MatchStateHasher.Hash(current)
                && await DigestAsync(source, room) == digest, "A cold duplicate must not charge or write any of the seven tables twice.");
            await Cold("cold-duplicate-unchanged");
            var stale = await session.SubmitAsync("P1", "expired-optional-reduction", command, raw, default);
            Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.Events.Count == 0
                && MatchStateHasher.Hash(stale.State) == MatchStateHasher.Hash(current), "The stale payment selection must remain inert.");
            frame = await Cold("expired-prompt-rejected");
            await File.WriteAllTextAsync(Path.Combine(directory, "explicit-initial.json"), JsonSerializer.Serialize(initial, Json));
            await File.WriteAllTextAsync(Path.Combine(directory, "commands.json"), JsonSerializer.Serialize(frame.Commands, Json));
            await File.WriteAllTextAsync(Path.Combine(directory, "events.json"), JsonSerializer.Serialize(frame.Events, Json));
            receipts.Add(new { scenario = new { scenario.Name, scenario.Card, scenario.Haste, scenario.Copied },
                room, checkpoints, commands = frame.Commands.Count, events = frame.Events.Count,
                finalHash = MatchStateHasher.Hash(current), sevenTableDigest = await DigestAsync(source, room),
                originalReplayMatched = true, coldDuplicateUnchanged = true, oldCopiedChoice });

            async Task Attach(string id)
            {
                await Submit("P1", new PlayCardCommand(id, "SFD·011/221", ["EZREAL", "GEAR"]));
                while (current.StackItems.Count > 0)
                    await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            }
            async Task Submit(string actor, GameCommand action)
            {
                var result = await session.SubmitAsync(actor, "optional-setup-" + submitted++, action, Raw(actor, action), default);
                Check(result.Accepted, result.ErrorMessage); current = result.State;
                await Cold(action.CmdType);
            }
            JsonElement Raw(string actor, GameCommand action) => CommandRaw(session, current, actor, action);
            async Task<MatchRecoveryFrame> Cold(string label)
            {
                var previousDigest = await DigestAsync(source, room);
                var recovered = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing optional-cost SQL frame.");
                Check(recovered.IsConsistent && MatchStateHasher.Hash(recovered.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    string.Join("; ", recovered.ValidationErrors));
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, recovered.Commands,
                    recovered.AuthoritativeState!, engine, default, recovered.Events);
                Check(replay.IsMatch, string.Join("; ", replay.Errors));
                session = MatchSession.Restore(recovered, engine, journal, players);
                foreach (var actor in initial.Seats.Keys)
                {
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(actor), Json),
                        JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(current)[actor], Json)), "Cold private snapshot mismatch.");
                    var view = JsonSerializer.Serialize(new { snapshot = session.SnapshotFor(actor), prompt = session.PromptFor(actor) }, Json);
                    Check(!view.Contains("HIDDEN-DECK", StringComparison.Ordinal), "A private deck identity escaped in a cost origin or prompt.");
                }
                Check(await DigestAsync(source, room) == previousDigest, "Cold restoration must leave all original rows unchanged.");
                checkpoints.Add(new { label, current.Tick, commands = recovered.Commands.Count, events = recovered.Events.Count,
                    originalActionEventReplayMatched = true, privateSnapshotsMatched = true, sevenTablesUnchanged = true });
                return recovered;
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_OPTIONAL_COST_REDUCTION_COLD_REPLAY_PASS", executedAtUtc = DateTimeOffset.UtcNow, receipts,
            scope = "Five fresh explicit current-rule fixtures. Two printings, mana/power zero fees, paid effect facts, actual attach/detach/reattach instance expiry, cold duplicates, stale selection and complete original replay. Ordinary startup registry/network gate is separate; no whole-card certification."
        }, Json));
    }

    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in new[] { "echo", "copied" })
            await PrepareNetworkCaseAsync(source, Path.Combine(output, kind), kind == "copied");
    }

    private static async Task PrepareNetworkCaseAsync(NpgsqlDataSource source, string output, bool copied)
    {
        Directory.CreateDirectory(output);
        var room = "optional-reduction-api-" + Guid.NewGuid().ToString("N");
        var seats = new Dictionary<string, string> { ["fee-reduction-a"] = "P1", ["fee-reduction-b"] = "P2" };
        var initial = MatchReplayInitialStateBuilder.FromSeats(room, seats);
        var players = new PostgresMatchPlayerStore(source);
        var tokens = seats.Keys.ToDictionary(actor => actor, actor => "optional-cost-reconnect-" + Guid.NewGuid().ToString("N"));
        foreach (var actor in seats.Keys)
            await players.SavePlayerSessionAsync(room, actor, seats[actor], ReconnectTokenHasher.Hash(tokens[actor]), default, initial.Seed);
        var engine = new CoreRuleEngine();
        var session = new MatchSession(initial, engine, new PostgresMatchJournal(source), players);
        var opening = copied ? await OptionalCostReductionCopiedOrdinaryOpening.BuildAsync(initial, session)
            : await OptionalCostReductionOrdinaryOpening.BuildAsync(initial, session, "SFD·149/221");
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing ordinary opening.");
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default);
        Check(frame.IsConsistent && errors.Count == 0, string.Join("; ", frame.ValidationErrors.Concat(errors)));
        await File.WriteAllTextAsync(Path.Combine(output, "network-fixture.json"), JsonSerializer.Serialize(new {
            room, tokens, opening.Actor, opening.Reducer, opening.ReducerGeneration, opening.OpeningCommands,
            opening.Command, opening.ForgedCommand, beforeHash = MatchStateHasher.Hash(opening.State),
            initialHash = MatchStateHasher.Hash(initial), beforeTick = opening.State.Tick,
            expectedMana = copied ? 2 : 3, expectedKind = copied ? "copied" : "echo"
        }, Json));
        await File.WriteAllTextAsync(Path.Combine(output, "opening-original-commands.json"), JsonSerializer.Serialize(frame.Commands, Json));
    }

    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        foreach (var kind in new[] { "echo", "copied" })
            await VerifyNetworkCaseAsync(source, Path.Combine(output, kind), stage);
    }

    private static async Task VerifyNetworkCaseAsync(NpgsqlDataSource source, string output, string stage)
    {
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var fixture = document.RootElement;
        var room = fixture.GetProperty("room").GetString()!;
        var actor = fixture.GetProperty("actor").GetString()!;
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing network SQL frame.");
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(frame.IsConsistent && errors.Count == 0, string.Join("; ", frame.ValidationErrors.Concat(errors)));
        var current = frame.AuthoritativeState!;
        var copied = fixture.GetProperty("expectedKind").GetString() == "copied";
        var playedSource = fixture.GetProperty("command").GetProperty("sourceObjectId").GetString()!;
        Check(current.RunePools[actor].Mana == 0 && current.RunePools[actor].TotalPower == 0,
            "The live API must consume exactly the quoted resources.");
        if (copied)
        {
            var goalTick = fixture.GetProperty("beforeTick").GetInt64() + 1;
            var goalEvents = frame.Events.Where(item => item.Tick == goalTick).Select(item => item.Event).ToArray();
            var paid = JsonSerializer.SerializeToElement(goalEvents.Single(item => item.Kind == "COST_PAID").Payload, Json);
            Check(current.PlayerZones[actor].Base.Contains(playedSource)
                && paid.GetProperty("sourceObjectId").GetString() == playedSource
                && paid.GetProperty("optionalCosts").EnumerateArray().Select(item => item.GetString()!).SequenceEqual(new[] { "SPEND_POWER:green:1" })
                && paid.GetProperty("optionalAdditionalPowerReduction").GetInt32() == 1
                && goalEvents.Where(item => item.Kind == "CARD_DRAWN").Any(item => {
                    var draw = JsonSerializer.SerializeToElement(item.Payload, Json);
                    return draw.GetProperty("playerId").GetString() == actor && draw.GetProperty("count").GetInt32() == 1; }),
                "The copied source must retain the declared zero-power fee and produce the actual entered unit's draw effect.");
        }
        else Check(current.StackItems.Single().EffectRepeatCount == 2, "The mana reduction must retain the declared Echo repeat.");
        var opening = fixture.GetProperty("openingCommands").GetInt32();
        Check(frame.Commands.Count == opening + 3 && frame.Commands.Count(command => command.Accepted) == opening + 1,
            "Keep the complete ordinary opening, one invalid generation, one paid play and one stale rejection; duplicate and peer calls write no extra commands.");
        var digest = await DigestAsync(source, room);
        await PaymentReconnectEvidence.VerifyCheckpoint(source, room, output, stage);
        await File.WriteAllTextAsync(Path.Combine(output, "sql-" + stage + ".json"), JsonSerializer.Serialize(new {
            status = "REAL_API_OPTIONAL_COST_SQL_ORIGINAL_REPLAY_PASS", stage, room, commands = frame.Commands.Count,
            events = frame.Events.Count, finalHash = MatchStateHasher.Hash(current), sevenTableDigest = digest,
            completeOriginalReplayMatched = true, freshCurrentInputs = true, originalRowsRetained = true
        }, Json));
        await File.WriteAllTextAsync(Path.Combine(output, "api-original-commands.json"), JsonSerializer.Serialize(frame.Commands, Json));
        await File.WriteAllTextAsync(Path.Combine(output, "api-original-events.json"), JsonSerializer.Serialize(frame.Events, Json));
    }

    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in new[] { "echo", "copied" })
            await VerifyNetworkTamperCaseAsync(source, Path.Combine(output, kind));
    }

    private static async Task VerifyNetworkTamperCaseAsync(NpgsqlDataSource source, string output)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var room = fixture.RootElement.GetProperty("room").GetString()!;
        var actor = fixture.RootElement.GetProperty("actor").GetString()!;
        await VerifyReductionResultForgeryAsync(source, room, actor, output);
        await VerifyNetworkCaseAsync(source, output, "after-restart");
    }

    // A single-room leaf also allows an actual CI failure to be diagnosed
    // without rerunning the two-room network/restart orchestration.
    internal static async Task VerifyReductionResultForgeryAsync(NpgsqlDataSource source, string room, string actor, string output)
    {
        var store = new PostgresMatchRecoveryStore(source); var engine = new CoreRuleEngine();
        var original = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing original optional-reduction frame.");
        Check(original.IsConsistent && (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(original, engine, default)).Count == 0,
            "Start with the complete consistent original optional-reduction log.");
        var state = original.AuthoritativeState!;
        Check(state.RunePools[actor].Mana == 0 && state.RunePools[actor].TotalPower == 0,
            "This saved optional-reduction result has paid all of its quoted resources.");
        var forged = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            [actor] = state.RunePools[actor] with { Mana = 1 } } };
        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
        var historyHash = MatchStateHasher.HashValue(new { original.Commands, original.Events });
        var before = await PaymentReconnectEvidence.Capture(source, room);
        var rows = new List<(string Table, long Id, string? Player, string Payload)>();
        var authoritative = before.Tables["state_snapshots"].Select(row => row!)
            .Where(row => row["last_event_sequence"]!.GetValue<long>() == original.LastEventSequence)
            .OrderByDescending(row => row["state_tick"]!.GetValue<long>()).ThenByDescending(row => row["id"]!.GetValue<long>()).First();
        rows.Add(("state_snapshots", authoritative["id"]!.GetValue<long>(), null, authoritative["payload"]!.ToJsonString()));
        foreach (var (table, tick) in new[] { ("snapshots", "snapshot_tick"), ("action_prompts", "prompt_tick") })
        foreach (var player in state.Seats.Keys)
        {
            var row = before.Tables[table].Select(row => row!).Where(row => row["player_id"]!.GetValue<string>() == player)
                .OrderByDescending(row => row["last_event_sequence"]!.GetValue<long>()).ThenByDescending(row => row[tick]!.GetValue<long>())
                .ThenByDescending(row => row["id"]!.GetValue<long>()).First();
            rows.Add((table, row["id"]!.GetValue<long>(), player, row["payload"]!.ToJsonString()));
        }
        Check(rows.Count == 5, "Capture one authoritative row and both players' latest snapshot and prompt rows.");
        await File.WriteAllTextAsync(Path.Combine(output, "registry-forged-reduction-original-frame.json"), JsonSerializer.Serialize(original, Json));
        var structuralError = await Attack(false);
        var error = await Attack(true);
        await File.WriteAllTextAsync(Path.Combine(output, "registry-forged-reduction-result.json"), JsonSerializer.Serialize(new {
            status = "REAL_REGISTRY_REJECTED_FORGED_REDUCTION_RESULT", room, error, structuralError,
            stateOnlyStructuralControlPassed = true, coordinatedForgeryShapePassed = true,
            originalPayloadRestored = true, originalAcceptedHistoryNeverRewritten = true, sevenTablesRestored = true,
            rejectedSessionsNeverCached = true, originalReplayMatchedAfterEachRestoration = true
        }, Json));

        async Task<string> Attack(bool coordinated)
        {
            var selected = coordinated ? rows : rows.Where(row => row.Table == "state_snapshots").ToList();
            var registry = new InMemoryMatchSessionRegistry(engine, new PostgresMatchJournal(source), store, new PostgresMatchPlayerStore(source));
            string? error = null, code = null, unexpected = null, restoreError = null;
            bool? shapeConsistent = null, returned = null, cached = null;
            IReadOnlyList<string>? shapeErrors = null; var restored = false;
            await using var connection = await source.OpenConnectionAsync();
            try
            {
                foreach (var row in selected)
                    await Write(row, JsonSerializer.Serialize(row.Table == "state_snapshots" ? (object)forged
                        : row.Table == "snapshots" ? snapshots[row.Player!] : prompts[row.Player!], Json));
                var loaded = await store.LoadAsync(room, default);
                shapeConsistent = loaded?.IsConsistent; shapeErrors = loaded?.ValidationErrors;
                Check(loaded is not null && MatchStateHasher.HashValue(new { loaded.Commands, loaded.Events }) == historyHash,
                    "Neither mutation may change an original accepted or rejected command, event or RuleInputs.");
                try { await registry.GetOrCreateAsync(room, default); returned = true; }
                catch (MatchSessionException exception) { returned = false; code = exception.Code; error = exception.ToString(); }
                cached = await registry.FindAsync(room, default) is not null;
            }
            catch (Exception exception) { unexpected = exception.ToString(); }
            finally
            {
                try
                {
                    foreach (var row in selected) await Write(row, row.Payload);
                    PaymentReconnectEvidence.ValidateExact(before, await PaymentReconnectEvidence.Capture(source, room));
                    restored = true;
                }
                catch (Exception exception) { restoreError = exception.ToString(); }
                await File.WriteAllTextAsync(Path.Combine(output, coordinated
                    ? "registry-forged-reduction-coherent-diagnostic.json" : "registry-forged-reduction-state-only-diagnostic.json"),
                    JsonSerializer.Serialize(new { room, actor, coordinated, shapeConsistent, shapeErrors,
                        registryReturned = returned, cached, code, error, unexpected, restoreError,
                        exactSevenTablesRestored = restored, mutatedRows = selected.Select(row => new { row.Table, row.Id, row.Player }) }, Json));
            }
            Check(restored && restoreError is null, "Restore exact original seven-table rows: " + restoreError);
            Check(unexpected is null, "Unexpected optional-reduction tamper failure: " + unexpected);
            Check(returned == false && code == ErrorCodes.RecoveryInconsistent && cached == false,
                "The fresh registry must reject without caching or writing a forged optional-reduction result: " + error);
            if (coordinated)
                Check(shapeConsistent == true && (shapeErrors?.Count ?? -1) == 0
                    && error?.Contains("action-log audit failed", StringComparison.Ordinal) == true
                    && error.Contains("replayed final state hash", StringComparison.Ordinal),
                    "The real registry must independently reject a shape-valid forged result using the complete original log: " + error);
            else
                Check(shapeConsistent == false && shapeErrors?.Any(e => e.Contains("rune pool ordinary mana", StringComparison.Ordinal)) == true
                    && error?.Contains("match recovery validation failed", StringComparison.Ordinal) == true
                    && !error.Contains("action-log audit failed", StringComparison.Ordinal),
                    "Changing only the authoritative mana must retain the independent structural/view rejection: " + error);
            var recovered = await registry.GetOrCreateAsync(room, default);
            foreach (var player in state.Seats.Keys)
            {
                Check(ProtocolViewHash(recovered.SnapshotFor(player)) == ProtocolViewHash(original.PlayerViews[player].Snapshot)
                    && ProtocolViewHash(recovered.PromptFor(player)) == ProtocolViewHash(original.PlayerViews[player].Prompt),
                    "The same registry must cold-recover both exact original player views after restoration.");
            }
            var normal = await store.LoadAsync(room, default);
            Check(normal is not null && normal.IsConsistent && MatchStateHasher.Hash(normal.AuthoritativeState!) == MatchStateHasher.Hash(state)
                && (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(normal, engine, default)).Count == 0,
                "The restored original history must still replay independently.");
            PaymentReconnectEvidence.ValidateExact(before, await PaymentReconnectEvidence.Capture(source, room));
            return error!;

            async Task Write((string Table, long Id, string? Player, string Payload) row, string payload)
            {
                await using var update = new NpgsqlCommand($"update {row.Table} set payload=@payload::jsonb where match_id=@room and id=@id", connection);
                update.Parameters.AddWithValue("payload", payload); update.Parameters.AddWithValue("room", room); update.Parameters.AddWithValue("id", row.Id);
                Check(await update.ExecuteNonQueryAsync() == 1, "Mutate and restore exactly one captured row in this test room.");
            }
        }
    }

    // Stored metadata is JsonElement; fresh nested cost choices are typed records.
    // Compare both through the actual Web JSON contract, preserving every field
    // and array order instead of hashing the records with default JSON casing.
    private static string ProtocolViewHash(object? view)
        => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(view, Json));

    private static JsonElement CommandRaw(IMatchSession session, MatchState state, string actor, GameCommand action)
    {
        var node = JsonSerializer.SerializeToNode(action, action.GetType(), Json)!.AsObject();
        node["promptId"] = session.PromptFor(actor).PromptId; node["snapshotTick"] = state.Tick;
        return JsonSerializer.SerializeToElement(node);
    }

    private static string CopyChoice(ActionPromptDto prompt)
    {
        var candidate = prompt.Candidates!.Single(item => item.Action == CommandTypes.PlayCard);
        var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, Json);
        var row = metadata.GetProperty("sourceRequirements").EnumerateArray().Single(item => item.GetProperty("sourceObjectId").GetString() == "CARD");
        var group = row.GetProperty("optionalCostReductionGroups").EnumerateArray().Single(item => item.GetProperty("ruleTextOrigin").ValueKind != JsonValueKind.Null);
        return group.GetProperty("choiceIds").EnumerateArray().Single().GetString()!;
    }

    private static async Task<MatchState> PositionAsync(string room, string reducer, bool haste, bool copied)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        CardObjectState Card(string id, string no, string actor, bool field = false)
        {
            var definition = catalog.Cards.Single(item => item.CardNo == no);
            var tags = definition.CardColorList.Select(color => "COLOR:" + color).Concat(definition.Tag.Split('|', StringSplitOptions.RemoveEmptyEntries)).ToList();
            tags.Add(definition.CardCategoryName.Contains("单位", StringComparison.Ordinal) ? CardObjectTags.UnitCard
                : definition.CardCategoryName.Contains("装备", StringComparison.Ordinal) ? CardObjectTags.EquipmentCard : CardObjectTags.SpellCard);
            if (OfficialArmamentCatalog.Find(no) is not null) tags.Add(CardEquipmentKeywordNames.Weapon);
            if (field && CardBehaviorRegistry.TryGetByCardNo(no, out var behavior)) tags.AddRange(behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries));
            return new(id, power: definition.Power ?? 0, tags: tags.Distinct(StringComparer.Ordinal).ToArray(),
                manaCost: definition.Energy ?? 0, cardNo: no, ownerId: actor, controllerId: actor);
        }
        var cards = new Dictionary<string, CardObjectState> {
            ["CARD"] = Card("CARD", haste ? "SFD·029/221" : "SFD·066/221", "P1"),
            ["EZREAL"] = Card("EZREAL", reducer, "P1", field: true),
            ["TARGET"] = Card("TARGET", "OGN·142/298", "P2", field: true),
            ["HIDDEN-DECK"] = Card("HIDDEN-DECK", "OGN·003/298", "P2") };
        var locations = new Dictionary<string, ObjectLocationState> {
            ["CARD"] = new("P1", "HAND"), ["EZREAL"] = new("P1", "BASE"),
            ["TARGET"] = new("P2", "BASE"), ["HIDDEN-DECK"] = new("P2", "MAIN_DECK") };
        var hand = new List<string> { "CARD" }; var sourceBase = new List<string> { "EZREAL" }; var deck = new List<string>();
        if (copied)
        {
            cards["GEAR"] = Card("GEAR", "SFD·059/221", "P1"); locations["GEAR"] = new("P1", "BASE"); sourceBase.Add("GEAR");
            foreach (var id in new[] { "ATTACH", "DETACH", "REATTACH" })
            { cards[id] = Card(id, "SFD·011/221", "P1"); locations[id] = new("P1", "HAND"); hand.Add(id); }
            foreach (var id in new[] { "DRAW-ONE", "DRAW-TWO", "DRAW-THREE" })
            { cards[id] = Card(id, "OGN·003/298", "P1"); locations[id] = new("P1", "MAIN_DECK"); deck.Add(id); }
        }
        return new(room, 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(copied ? 40 : haste ? 4 : 3, copied ? 20 : 0), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = hand, Base = sourceBase, MainDeck = deck },
                ["P2"] = PlayerZones.Empty with { Base = ["TARGET"], MainDeck = ["HIDDEN-DECK"] } },
            cardObjects: cards, objectLocations: locations);
    }

    private static async Task<string> DigestAsync(NpgsqlDataSource source, string room)
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
    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
