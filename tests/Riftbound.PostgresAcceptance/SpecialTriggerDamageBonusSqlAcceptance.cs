using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

// Two existing damage-skill paths, with literal controlled positions and original SQL histories.
internal static class SpecialTriggerDamageBonusSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh special-trigger damage SQL output required.");
        Directory.CreateDirectory(output);
        foreach (var printing in new[] { "UNL-118/219", "UNL-118a/219" })
        foreach (var copies in new[] { 0, 2 }) await Run(printing, true, copies, 0);
        foreach (var printing in new[] { "SFD·116/221", "SFD·233/221", "SFD·233*/221" })
        foreach (var copies in new[] { 0, 2 })
        foreach (var power in new[] { 0, 5 }) await Run(printing, false, copies, power);

        async Task Run(string printing, bool perLocation, int copies, int power)
        {
            var folder = Path.Combine(output, printing.Replace('·', '-').Replace('/', '-').Replace('*', 's'), $"copies-{copies}-power-{power}");
            Directory.CreateDirectory(folder);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            var room = "special-trigger-damage-" + Guid.NewGuid().ToString("N");
            var cards = new Dictionary<string, CardObjectState> {
                ["SOURCE"] = Unit("SOURCE", "P1", printing, perLocation ? 10 : power),
                ["TARGET"] = Unit("TARGET", "P2", "SFD·125/221", 20),
                ["FIELD"] = new("FIELD", cardNo: "OGN·296/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: perLocation ? "P2" : "P1", controllerId: perLocation ? "P2" : null),
                ["SECRET_HAND"] = Unit("SECRET_HAND", "P2", "SFD·125/221", 3),
                ["SECRET_DECK"] = Unit("SECRET_DECK", "P2", "SFD·125/221", 3)
            };
            if (perLocation) { cards["BASE_TARGET"] = Unit("BASE_TARGET", "P2", "SFD·125/221", 20); cards["UNSELECTED"] = Unit("UNSELECTED", "P2", "SFD·125/221", 20); }
            var annies = Enumerable.Range(0, copies).Select(i => "ANNIE" + i).ToArray();
            foreach (var id in annies) cards[id] = Unit(id, "P1", "OGS·001/024", 4);
            var locations = annies.ToDictionary(id => id, _ => new ObjectLocationState("P1", "BASE"));
            locations["SOURCE"] = new("P1", perLocation ? "HAND" : "BASE");
            locations["FIELD"] = new(perLocation ? "P2" : "P1", "BATTLEFIELD", "FIELD");
            locations["TARGET"] = new("P2", perLocation ? "BATTLEFIELD" : "BASE", perLocation ? "FIELD" : null);
            locations["SECRET_HAND"] = new("P2", "HAND"); locations["SECRET_DECK"] = new("P2", "MAIN_DECK");
            if (perLocation) { locations["BASE_TARGET"] = new("P2", "BASE"); locations["UNSELECTED"] = new("P2", "BATTLEFIELD", "FIELD"); }
            var initial = new MatchState(room, 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
                status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
                playerZones: new Dictionary<string, PlayerZones> {
                    ["P1"] = PlayerZones.Empty with { Hand = perLocation ? ["SOURCE"] : [], Base = perLocation ? annies : ["SOURCE", ..annies], Battlefields = perLocation ? [] : ["FIELD"] },
                    ["P2"] = PlayerZones.Empty with { Hand = ["SECRET_HAND"], MainDeck = ["SECRET_DECK"], Base = perLocation ? ["BASE_TARGET"] : ["TARGET"], Battlefields = perLocation ? ["FIELD", "TARGET", "UNSELECTED"] : [] } },
                cardObjects: cards, objectLocations: locations, playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
                runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) });
            await File.WriteAllTextAsync(Path.Combine(folder, "explicit-original-controlled-initial.json"), JsonSerializer.Serialize(initial, json));
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))), default);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var state = initial; var commands = 0; var cold = 0; var rejected = 0; var structural = 0; var originals = new List<object>();
            if (perLocation) await Submit("P1", new PlayCardCommand("SOURCE", printing, []));
            else
            {
                await Submit("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:FIELD", []));
                await Submit(state.FocusPlayerId!, new PassFocusCommand());
                await Submit(state.FocusPlayerId!, new PassFocusCommand());
            }
            var choice = state.PendingCardChoice ?? throw new Exception("Actual skill target confirmation required.");
            Check(choice.PlayerId == "P1" && choice.RequiredCount == (perLocation ? 0 : 1) && choice.MaxCount == (perLocation ? 2 : 1), "Exact optional per-location or mandatory base-target selection.");
            Check(choice.LegalObjectIds.Order().SequenceEqual(perLocation ? new[] { "BASE_TARGET", "TARGET", "UNSELECTED" } : new[] { "TARGET" }), "Only the original legal enemy locations/targets.");
            var pending = await store.LoadAsync(room, default) ?? throw new Exception("Real pending SQL frame required.");
            var pendingCommandCount = commands; var paidResources = state.RunePools;
            Check(pending.Events.All(e => e.Event.Kind != "DAMAGE_APPLIED"), "Target confirmation precedes damage.");
            foreach (var bad in new[] { choice with { PlayerId = "P2" }, choice with { LegalObjectIds = ["SOURCE"] }, choice with { SourceObjectId = "TARGET" }, choice with { RequiredCount = perLocation ? 1 : 0 } })
            {
                Check(MatchRecoveryValidator.Validate(room, pending.LastEventSequence, pending.Commands, pending.Events, pending.PlayerViews, state with { PendingCardChoice = bad }, pending.CurrentTick, pending.SpectatorReplayFrame).Count > 0, "Independent wrong actor/target/source/count rejects against all original frame inputs.");
                structural++;
            }
            string[] targets = perLocation ? ["TARGET", "BASE_TARGET"] : ["TARGET"];
            await Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
            await Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, perLocation ? ["TARGET", "UNSELECTED"] : ["SOURCE"]));
            await Reject("P1", new ChooseCardsCommand(choice.ChoiceId + "-stale", choice.ChoiceWindow, targets));
            await Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, perLocation ? ["SOURCE"] : []));
            await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            var final = await store.LoadAsync(room, default) ?? throw new Exception("Real final SQL frame required.");
            var damage = final.Events.Where(e => e.Event.Kind == "DAMAGE_APPLIED").ToArray();
            if (perLocation)
            {
                Check(damage.Length == 2 && Amount("TARGET") == 2 + copies && Amount("BASE_TARGET") == 1 + copies, "Each location gets the controller bonus, and only the battlefield target gets one local bonus.");
                Check(state.PlayerZones["P2"].Graveyard.Contains("TARGET") && state.PlayerZones["P2"].Graveyard.Contains("BASE_TARGET") && state.PlayerZones["P2"].Battlefields.Contains("UNSELECTED") && state.CardObjects["UNSELECTED"].Damage == 0, "Original selected targets die through Elder; unselected same-location target is untouched.");
            }
            else
            {
                Check(damage.Length == (power + copies == 0 ? 0 : 1) && state.CardObjects["TARGET"].Damage == power + copies, "Declared current Might damage receives the current controller bonus before the zero-damage guard, without a source-battlefield local bonus.");
                Check(power + copies == 0 || Amount("TARGET") == power + copies, "Exact one-target damage event.");
                Check(state.PlayerZones["P2"].Base.Contains("TARGET") && state.PlayerScores["P1"] == 1, "Original base target and ordinary one-point conquest remain.");
            }
            Check(Same(state.RunePools, paidResources) && state.PlayerZones["P2"].Hand.SequenceEqual(new[] { "SECRET_HAND" }) && state.PlayerZones["P2"].MainDeck.SequenceEqual(new[] { "SECRET_DECK" }), "No response payment or unrelated hidden-zone changes.");
            Check(commands == (perLocation ? 4 : 6) && cold == commands && rejected == 4 && structural == 4 && state.StackItems.Count == 0 && state.PendingCardChoice is null, "Finite original command/cold/rejection counts and completed skill.");
            await File.WriteAllTextAsync(Path.Combine(folder, "all-original-accepted-intents.json"), JsonSerializer.Serialize(originals, json));
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new { status = "REAL_SQL_CURRENT_PER_LOCATION_OR_CONQUEST_SKILL_DAMAGE_BONUSES", room, printing, perLocation, copies, power, pendingCommandCount, commands, events = final.Events.Count, coldRecoveries = cold, directAtomicRejections = rejected, independentStructuralRejections = structural, actualRuleInputs = state.RuleInputs, rootIntegration = true, wholePrintingCertification = false }, json));
            Console.WriteLine($"SPECIAL_TRIGGER_DAMAGE_SQL_PASS {printing} copies={copies} power={power} {commands} commands {final.Events.Count} events {cold} cold {rejected} direct {structural} structural");

            int Amount(string target) => JsonSerializer.SerializeToElement(damage.Single(e => JsonSerializer.SerializeToElement(e.Event.Payload, json).GetProperty("targetObjectId").GetString() == target).Event.Payload["damage"], json).GetInt32();
            async Task Submit(string actor, GameCommand command)
            {
                var intent = "special-trigger-damage-" + commands; var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = state.Tick; var element = JsonSerializer.SerializeToElement(raw, json);
                var accepted = await session.SubmitAsync(actor, intent, command, element, default); Check(accepted.Accepted, accepted.ErrorMessage ?? "Original command rejected.");
                state = accepted.State; commands++; var original = new { actor, rawCommand = element }; originals.Add(original);
                await File.WriteAllTextAsync(Path.Combine(folder, $"step-{commands:D2}-original-intent.json"), JsonSerializer.Serialize(original, json));
                var before = await Digest(); var frame = await store.LoadAsync(room, default) ?? throw new Exception("Real persisted frame required.");
                Check(frame.IsConsistent && frame.Commands.Count == commands && Same(frame.AuthoritativeState, state), "Entire original accepted state persists exactly.");
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, state, engine, default, frame.Events); Check(replay.IsMatch, string.Join(';', replay.Errors));
                var restored = MatchSession.Restore(frame, engine, journal, players);
                foreach (var viewer in initial.Seats.Keys) Check(Same(session.SnapshotFor(viewer), restored.SnapshotFor(viewer)) && Same(session.PromptFor(viewer), restored.PromptFor(viewer)), "Every original cold private field, null and omission is exact.");
                session = restored; cold++; Check((await session.SubmitAsync(actor, intent, command, element, default)).Accepted && await Digest() == before, "Cold duplicate changes none of seven original tables.");
                await using var file = new FileStream(Path.Combine(folder, $"step-{commands:D2}-original-frame.json.gz"), FileMode.CreateNew); await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, frame, json);
            }
            async Task Reject(string actor, GameCommand command)
            {
                var before = await Digest(); var result = await engine.ResolveAsync(state, new("invalid-special-trigger-damage", actor, command.CmdType), command, default);
                Check(!result.Accepted && result.Events.Count == 0 && Same(result.State, state) && await Digest() == before, "Invalid target confirmation rejects atomically without changing seven original SQL tables."); rejected++;
            }
            async Task<string> Digest()
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    hash.AppendData(Encoding.UTF8.GetBytes(table + "\n")); await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection); query.Parameters.AddWithValue("room", room);
                    await using var rows = await query.ExecuteReaderAsync(); while (await rows.ReadAsync()) hash.AppendData(Encoding.UTF8.GetBytes(rows.GetString(0) + "\n"));
                }
                return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
            bool Same<T>(T left, T right) => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(left, json), JsonSerializer.SerializeToNode(right, json));
        }
    }
    private static CardObjectState Unit(string id, string owner, string printing, int power) => new(id, cardNo: printing, power: power, tags: [CardObjectTags.UnitCard], ownerId: owner, controllerId: owner);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
