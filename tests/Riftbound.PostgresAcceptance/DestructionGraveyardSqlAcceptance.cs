using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class DestructionGraveyardSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Never overwrite an original destruction-family SQL run.");
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "destruction-graveyard-openings.json")));
        var recipe = fixture.RootElement;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var results = new List<object>();
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        foreach (var profile in new[] { "SELF_PAY", "DECREE_PAY", "DECREE_DECLINE" })
        {
            var room = "postgres-destruction-graveyard-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original blank seed0.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var state = initial; var count = 0; var colds = 0; var originalNext = 0; var rejected = 0; var afterCold = false;
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("destruction-sql-" + player), default);
            foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            {
                var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
                if (count == 6) await Cold();
                await Submit(entry.GetProperty("playerId").GetString()!, command);
            }
            var phoenix = recipe.GetProperty("phoenixObjectId").GetString()!;
            await Intent(recipe.GetProperty("selfRayIntent")); await Top();
            Check(state.PendingPayment is { ManaCost: 1, PowerCost: 1 } && state.StackItems.Single().GraveyardPlay?.Source == new ObjectBinding(phoenix, 3), "Self-destroyed ordinary Phoenix earns its captured graveyard incarnation and leading payment.");
            await Cold();
            if (profile == "SELF_PAY")
            {
                await Pay(true); await Top(); await Cold();
                Check(state.PendingEffectPlay is not null, "Paid trigger resolves through shared effect play.");
                await Submit("P1", new PlayCardCommand(phoenix, "OGN·037/298", []));
            }
            else
            {
                await Pay(false);
                foreach (var entry in recipe.GetProperty("afterSelfTriggerExplicitlyDeclinedIntents").EnumerateArray()) await Intent(entry);
                await Cold(); await Intent(recipe.GetProperty("decreeRayIntent")); await Top();
                var elder = recipe.GetProperty("elderObjectId").GetString()!;
                Check(state.CardObjects[elder].Damage == 3 && state.PlayerZones["P2"].Battlefields.Contains(elder)
                    && state.StackItems.Single().DamageDestruction?.CardNo == "OGN·221/298" && state.PendingPayment is null,
                    "Nonlethal actual Ray queues the original Decree's independent destroy skill.");
                await Cold(); await Top();
                Check(state.PlayerZones["P2"].Graveyard.Contains(elder) && state.StackItems.Single().GraveyardPlay?.Attribution.CardNo == "OGN·221/298", "Delayed destruction keeps the spell source and earns Phoenix.");
                await Cold(); var pay = profile == "DECREE_PAY"; await Pay(pay);
                if (pay) { await Top(); await Cold(); await Submit("P1", new PlayCardCommand(phoenix, "OGN·037/298", [])); }
            }
            Check(state.StackItems.Count == 0 && state.TriggerQueue.Count == 0 && state.PendingPayment is null && state.PendingEffectPlay is null,
                "Every active instruction has completed without history replacement.");
            Check(state.PlayerZones["P1"].Graveyard.Contains(phoenix) == (profile == "DECREE_DECLINE"), "Exact accepted payment decision controls the original graveyard play.");
            if (profile != "DECREE_DECLINE") Check(state.PlayerZones["P1"].Base.Contains(phoenix) && state.CardObjects[phoenix].ObjectGeneration == 4, "The original physical card enters base as a new incarnation.");
            var final = await Cold();
            var path = Path.Combine(output, $"destruction-{format}-{profile}-original-frame.json.gz");
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize)) await JsonSerializer.SerializeAsync(zip, final, json);
            results.Add(new { room, format = format.ToString(), profile, commands = count, events = final.Events.Count,
                coldRestores = colds, originalNextCommandsMatched = originalNext, atomicRejections = rejected,
                originalOpeningCommands = recipe.GetProperty("openingIntents").GetArrayLength(), originalBlankSeed = 0,
                sourceCardNo = "OGN·037/298", delayedSourceCardNo = "OGN·221/298", ruleInputs = state.RuleInputs,
                originalFullReplayMatched = true, originalPrivateViewsAndPromptsExact = true, noStateSeedOrHistoryInjection = true,
                completeGameOrWholeCardCertification = false });

            Task Intent(JsonElement entry) => Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
            async Task Top() { await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
            async Task Pay(bool pay)
            {
                var payment = state.PendingPayment ?? throw new InvalidOperationException("Captured leading payment is required.");
                Check(payment.ManaCost == 1 && payment.PowerCost == 1 && payment.PowerCostByTrait.GetValueOrDefault("red") == 1, "The printed one mana and one red power are not waived.");
                await Reject("P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
                await Reject("P1", new PayCostCommand("stale-payment", payment.PaymentWindow, ["DECLINE"]));
                if (pay)
                {
                    if (state.RunePools["P1"].Mana == 0) await Submit("P1", new TapRuneCommand(state.PlayerZones["P1"].Base.First(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && !state.CardObjects[id].IsExhausted)));
                    if (state.RunePools["P1"].PowerByTrait.GetValueOrDefault("red") == 0) await Submit("P1", new RecycleRuneCommand(state.PlayerZones["P1"].Base.First(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard) && state.CardObjects[id].Tags.Contains("COLOR:red"))));
                }
                await Submit("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
                await Cold();
            }
            async Task Submit(string actor, GameCommand command)
            {
                if (command is SubmitDeckCommand deck)
                    foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, checkFormat).IsValid, "Both declared engine formats require legal original decks.");
                var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var intent = "original-destruction-" + count;
                var expected = afterCold ? await engine.ResolveAsync(state, new(intent, actor, command.CmdType), command, default) : null;
                var result = command switch { SubmitDeckCommand deckCommand => await session.SubmitDeckAsync(actor, intent, deckCommand, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default), _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage);
                if (afterCold)
                {
                    Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)), "Original next command/state/events match after cold recovery.");
                    originalNext++;
                }
                state = result.State; count++; afterCold = false;
            }
            async Task Reject(string actor, GameCommand command)
            {
                var digest = await Digest(); var hash = MatchStateHasher.Hash(state);
                var result = await engine.ResolveAsync(state, new("rejected-destruction", actor, command.CmdType), command, default);
                Check(!result.Accepted && result.Events.Count == 0 && MatchStateHasher.Hash(result.State) == hash && await Digest() == digest, "Independent invalid actor intent cannot change state or any original SQL row.");
                rejected++;
            }
            async Task<MatchRecoveryFrame> Cold()
            {
                var digest = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("A real original SQL frame is required.");
                Check(frame.IsConsistent && frame.Commands.Count == count && frame.OpeningSeed == 0
                    && frame.ReplayInitialState is { Tick: 0 } blank && blank.CardObjects.Count == 0
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(state), "Original blank seed, complete command history and authoritative state match.");
                // A fresh production registry mandates the full original action/event/state
                // replay before restoring this same SQL history. Auditing it here as well
                // would replay every accepted command twice at each cold boundary.
                var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players, options).GetOrCreateAsync(room, default);
                foreach (var player in initial.Seats.Keys)
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))), "Both original private views and prompts survive recovery exactly.");
                Check(await Digest() == digest, "Cold read/replay cannot modify any of the seven original tables.");
                session = restored; colds++; afterCold = true; return frame;
            }
            async Task<string> Digest()
            {
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    digest.AppendData(Encoding.UTF8.GetBytes(table + "\n")); var any = false;
                    await using var command = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection);
                    command.Parameters.AddWithValue("room", room); await using var rows = await command.ExecuteReaderAsync();
                    while (await rows.ReadAsync()) { any = true; digest.AppendData(Encoding.UTF8.GetBytes(rows.GetString(0) + "\n")); }
                    if (!any) digest.AppendData(Encoding.UTF8.GetBytes("\n"));
                }
                return Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_ORIGINAL_DESTRUCTION_GRAVEYARD_SIX_BRANCHES_COLD_REPLAY_PASS",
            regionalTournamentEligibilityCertified = false, results }, json));
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
