using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

// Current CN Annie + four Teemo printings + Void Gate; only performed counted defense damage.
internal static class ControllerDamageBonusSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh controller-bonus counted-defense SQL output required.");
        Directory.CreateDirectory(output);
        foreach (var printing in new[] { "OGN·121/298", "OGN·121a/298", "SFD·230/221", "SFD·230*/221" })
        foreach (var count in new[] { 0, 1, 2 }) await Run(printing, count);

        async Task Run(string printing, int printedCount)
        {
            var folder = Path.Combine(output, printing.Replace('·', '-').Replace('/', '-').Replace('*', 's'), "count-" + printedCount);
            Directory.CreateDirectory(folder);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            var room = "controller-counted-defense-" + Guid.NewGuid().ToString("N");
            var deck = new[] { "TOP1", "TOP2", "TOP3", "TOP4", "TOP5", "LOWER" };
            var cards = new Dictionary<string, CardObjectState> {
                ["ATTACKER"] = Unit("ATTACKER", "P1", "SFD·125/221", 7),
                ["ANNIE"] = Unit("ANNIE", "P2", "OGS·001/024", 4),
                ["DEFENDER"] = Unit("DEFENDER", "P2", printing, 2),
                ["FIELD"] = new("FIELD", cardNo: "OGN·296/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2")
            };
            foreach (var (id, index) in deck.Select((id, index) => (id, index)))
                cards[id] = Unit(id, "P2", index < printedCount ? "OGN·121/298" : "SFD·125/221", index < printedCount ? 2 : 3);
            var locations = deck.ToDictionary(id => id, _ => new ObjectLocationState("P2", "MAIN_DECK"));
            locations["ATTACKER"] = new("P1", "BASE");
            locations["ANNIE"] = new("P2", "BASE");
            locations["DEFENDER"] = new("P2", "BATTLEFIELD", "FIELD");
            locations["FIELD"] = new("P2", "BATTLEFIELD", "FIELD");
            var initial = new MatchState(room, 0, 7, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
                status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
                playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Base = ["ATTACKER"] }, ["P2"] = PlayerZones.Empty with { Base = ["ANNIE"], Battlefields = ["FIELD", "DEFENDER"], MainDeck = deck } },
                cardObjects: cards, objectLocations: locations, untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("FIELD")],
                playerScores: new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 }, runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 10), ["P2"] = new(20, 10) });
            await File.WriteAllTextAsync(Path.Combine(folder, "explicit-original-controlled-initial.json"), JsonSerializer.Serialize(initial, json));
            var engine = new CoreRuleEngine();
            var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source);
            var store = new PostgresMatchRecoveryStore(source);
            foreach (var actor in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))), default);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var state = initial; var commands = 0; var cold = 0; var rejected = 0; var structural = 0;
            var originals = new List<object>();
            await Submit("P1", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:FIELD", []));
            var choice = state.PendingCardChoice ?? throw new Exception("Actual defense target confirmation required.");
            Check(choice.PlayerId == "P2" && choice.RequiredCount == 1 && choice.LegalObjectIds.SequenceEqual(new[] { "ATTACKER" }) && state.PlayerZones["P2"].MainDeck.SequenceEqual(deck), "Literal defender-only target choice before any reveal or damage.");
            var pending = await store.LoadAsync(room, default) ?? throw new Exception("Real pending SQL frame required.");
            Check(pending.Events.All(e => e.Event.Kind is not ("MAIN_DECK_CARDS_REVEALED" or "DAMAGE_APPLIED" or "CARDS_RECYCLED")), "No premature observed-card or damage action.");
            foreach (var bad in new[] { choice with { PlayerId = "P1" }, choice with { LegalObjectIds = ["DEFENDER"] }, choice with { SourceObjectId = "ATTACKER" }, choice with { RequiredCount = 0 } })
            {
                Check(MatchRecoveryValidator.Validate(room, pending.LastEventSequence, pending.Commands, pending.Events, pending.PlayerViews, state with { PendingCardChoice = bad }, pending.CurrentTick, pending.SpectatorReplayFrame).Count > 0, "Independent wrong actor, target, source or mandatory count rejects against all eight original frame inputs.");
                structural++;
            }
            await Reject("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ATTACKER"]));
            await Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DEFENDER"]));
            await Reject("P2", new ChooseCardsCommand(choice.ChoiceId + "-stale", choice.ChoiceWindow, ["ATTACKER"]));
            await Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            await Submit("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ATTACKER"]));
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            var final = await store.LoadAsync(room, default) ?? throw new Exception("Real final SQL frame required.");
            var reveals = final.Events.Where(e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED").ToArray();
            Check(reveals.Length == 1 && JsonSerializer.SerializeToElement(reveals[0].Event.Payload["damageAmount"], json).GetInt32() == printedCount, "Printed count is distinct from subsequently modified damage.");
            var damage = final.Events.Where(e => e.Event.Kind == "DAMAGE_APPLIED").ToArray();
            Check(damage.Length == (printedCount == 0 ? 0 : 1) && state.CardObjects["ATTACKER"].Damage == (printedCount == 0 ? 0 : printedCount + 2), "Zero count executes no damage action; a positive counted action receives one current controller bonus and one local bonus.");
            Check(printedCount == 0 || JsonSerializer.SerializeToElement(damage.Single().Event.Payload["damage"], json).GetInt32() == printedCount + 2, "Exact original one-target damage event amount.");
            Check(state.PlayerZones["P2"].MainDeck[0] == "LOWER" && state.PlayerZones["P2"].MainDeck.Skip(1).Order().SequenceEqual(deck.Take(5).Order()) && Same(state.RunePools, initial.RunePools), "All five original revealed cards recycle after the untouched lower card; no payment changes.");
            await File.WriteAllTextAsync(Path.Combine(folder, "all-original-accepted-intents.json"), JsonSerializer.Serialize(originals, json));
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new { status = "REAL_SQL_CURRENT_ANNIE_TEEMO_COUNTED_DEFENSE_CONTROLLER_AND_VOID_BONUSES_ONLY", room, printing, printedCount, controllerBonusSourceCardNo = "OGS·001/024", commands, events = final.Events.Count, coldRecoveries = cold, directAtomicRejections = rejected, independentStructuralRejections = structural, actualRuleInputs = state.RuleInputs, rootIntegration = true, wholePrintingCertification = false }, json));
            Console.WriteLine($"CONTROLLER_COUNTED_DAMAGE_SQL_PASS {printing} count={printedCount} {commands} commands {final.Events.Count} events {cold} cold {rejected} direct {structural} structural");

            async Task Submit(string actor, GameCommand command)
            {
                var intent = "controller-counted-" + commands;
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                raw["promptId"] = session.PromptFor(actor).PromptId; raw["snapshotTick"] = state.Tick;
                var element = JsonSerializer.SerializeToElement(raw, json);
                var accepted = await session.SubmitAsync(actor, intent, command, element, default);
                Check(accepted.Accepted, accepted.ErrorMessage ?? "Original command rejected.");
                state = accepted.State; commands++; originals.Add(new { actor, rawCommand = element });
                var before = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new Exception("Real persisted frame required.");
                Check(frame.IsConsistent && frame.Commands.Count == commands && Same(frame.AuthoritativeState, state), "Entire accepted state persists exactly.");
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, state, engine, default, frame.Events);
                Check(replay.IsMatch, string.Join(';', replay.Errors));
                var restored = MatchSession.Restore(frame, engine, journal, players);
                foreach (var viewer in initial.Seats.Keys) Check(Same(session.SnapshotFor(viewer), restored.SnapshotFor(viewer)) && Same(session.PromptFor(viewer), restored.PromptFor(viewer)), "Every cold private snapshot/prompt field and null is exact.");
                session = restored; cold++;
                Check((await session.SubmitAsync(actor, intent, command, element, default)).Accepted && await Digest() == before, "Duplicate after cold restoration changes none of seven tables.");
                await using var file = new FileStream(Path.Combine(folder, $"step-{commands:D2}-original-frame.json.gz"), FileMode.CreateNew);
                await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
                await JsonSerializer.SerializeAsync(zip, frame, json);
            }
            async Task Reject(string actor, GameCommand command)
            {
                var before = await Digest();
                var result = await engine.ResolveAsync(state, new("invalid-controller-counted", actor, command.CmdType), command, default);
                Check(!result.Accepted && result.Events.Count == 0 && Same(result.State, state) && await Digest() == before, "Invalid selection rejects atomically without touching seven original SQL tables.");
                rejected++;
            }
            async Task<string> Digest()
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    hash.AppendData(Encoding.UTF8.GetBytes(table + "\n"));
                    await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection);
                    query.Parameters.AddWithValue("room", room);
                    await using var rows = await query.ExecuteReaderAsync();
                    while (await rows.ReadAsync()) hash.AppendData(Encoding.UTF8.GetBytes(rows.GetString(0) + "\n"));
                }
                return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
            bool Same<T>(T left, T right) => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(left, json), JsonSerializer.SerializeToNode(right, json));
        }
    }
    private static CardObjectState Unit(string id, string owner, string printing, int power) => new(id, cardNo: printing, power: power, tags: [CardObjectTags.UnitCard], ownerId: owner, controllerId: owner);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
