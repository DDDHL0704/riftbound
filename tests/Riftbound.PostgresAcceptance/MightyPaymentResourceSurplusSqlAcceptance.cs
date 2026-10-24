using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class MightyPaymentResourceSurplusSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var receipts = new List<object>();
        foreach (var trait in new[] { RuneTrait.Blue, RuneTrait.Yellow })
        foreach (var inline in new[] { true, false })
        {
            var room = "postgres-mighty-surplus-" + Guid.NewGuid().ToString("N");
            var directory = Path.Combine(output, trait + (inline ? "-inline" : "-separate"));
            Directory.CreateDirectory(directory);
            var initial = Position(room, trait);
            var engine = new CoreRuleEngine();
            var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source);
            var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            foreach (var actor in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("mighty-surplus-" + actor), default, initial.Seed);
            var current = initial;
            var submitted = 0;
            var checkpoints = new List<object>();
            await Submit("P1", new PlayCardCommand("MERCY", "OGN·053/298", ["TARGET"]));
            await Submit("P1", new PassPriorityCommand());
            await Submit("P2", new PassPriorityCommand());
            var payment = current.PendingPayment ?? throw new InvalidOperationException("Missing real Mighty payment.");
            Check(payment.PowerCostByTrait.GetValueOrDefault(RuneTrait.Yellow) == 1
                && payment.PaymentResourceActionIds.SequenceEqual(new[] { "RECYCLE_RUNE:EXTRA" }),
                "The actual payment must quote the legal rune even when its trait is unrelated to the yellow fee.");
            var before = MatchStateHasher.Hash(current);
            var invalid = new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
                ["RECYCLE_RUNE:FOREIGN", "SPEND_POWER:yellow:1"]);
            var rejected = await session.SubmitAsync("P1", "foreign-mighty-resource", invalid, Raw("P1", invalid), default);
            Check(!rejected.Accepted && rejected.ErrorCode == ErrorCodes.InvalidTarget && rejected.Events.Count == 0
                && MatchStateHasher.Hash(rejected.State) == before, "A foreign resource must not enter an affordable payment.");
            submitted++;
            await Cold("foreign-resource-rejected");
            if (!inline)
            {
                await Submit("P1", new RecycleRuneCommand("EXTRA"));
                Check(current.PendingPayment?.PaymentId == payment.PaymentId
                    && current.PendingPayment.PowerCostByTrait.GetValueOrDefault(RuneTrait.Yellow) == 1
                    && current.PendingPayment.PaymentResourceActionIds.Count == 0,
                    "A separate acquisition must refresh only the remaining resources, keeping the same fee and window.");
            }
            var choices = inline ? new[] { "RECYCLE_RUNE:EXTRA", "SPEND_POWER:yellow:1" } : new[] { "SPEND_POWER:yellow:1" };
            var command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, choices);
            var raw = Raw("P1", command);
            await Submit("P1", command, "mighty-surplus-pay", raw);
            Check(current.PendingPayment is null && current.CardObjects["TARGET"].IsExhausted
                && current.RunePools["P1"].TotalPower == 1 && current.RunePools["P1"].PowerByTrait.GetValueOrDefault(trait) == 1,
                "Pay exactly one yellow power and leave the acquired surplus; do not ready the target before responses.");
            var paidFrame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing paid SQL frame.");
            var duplicate = await session.SubmitAsync("P1", "mighty-surplus-pay", command, raw, default);
            var duplicateFrame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing duplicate frame.");
            Check(duplicate.Accepted && MatchStateHasher.Hash(duplicate.State) == MatchStateHasher.Hash(current)
                && duplicateFrame.Commands.Count == paidFrame.Commands.Count && duplicateFrame.Events.Count == paidFrame.Events.Count,
                "A cold duplicate must not acquire another rune or append commands/events.");
            await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            Check(!current.CardObjects["TARGET"].IsExhausted && current.StackItems.Count == 0
                && current.PlayerZones["P1"].RuneDeck.SequenceEqual(new[] { "EXTRA" }),
                "The confirmed skill must resolve only after both responses, without a second rune acquisition.");
            var final = await Cold("resolved-and-original-log-retained");
            await File.WriteAllTextAsync(Path.Combine(directory, "explicit-initial.json"), JsonSerializer.Serialize(initial, json));
            await File.WriteAllTextAsync(Path.Combine(directory, "commands.json"), JsonSerializer.Serialize(final.Commands, json));
            await File.WriteAllTextAsync(Path.Combine(directory, "events.json"), JsonSerializer.Serialize(final.Events, json));
            receipts.Add(new { room, trait, inline, submitted, events = final.Events.Count, checkpoints,
                finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                foreignResourceRejected = true, coldDuplicateDidNotAppend = true, originalRowsRetained = true });

            async Task Submit(string actor, GameCommand action, string? intent = null, JsonElement? suppliedRaw = null)
            {
                var result = await session.SubmitAsync(actor, intent ?? "mighty-surplus-" + submitted, action,
                    suppliedRaw ?? Raw(actor, action), default);
                Check(result.Accepted, result.ErrorMessage);
                current = result.State; submitted++;
                await Cold("after-" + action.CmdType);
            }

            JsonElement Raw(string actor, GameCommand action)
            {
                var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    JsonSerializer.Serialize(action, action.GetType(), json))!;
                fields["promptId"] = JsonSerializer.SerializeToElement(session.PromptFor(actor).PromptId);
                fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
                return JsonSerializer.SerializeToElement(fields);
            }

            async Task<MatchRecoveryFrame> Cold(string label)
            {
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL frame.");
                Check(frame.IsConsistent && frame.Commands.Count == submitted
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    "SQL state/history mismatch: " + string.Join("; ", frame.ValidationErrors));
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, frame.AuthoritativeState!,
                    engine, default, frame.Events);
                Check(replay.IsMatch, string.Join("; ", replay.Errors));
                session = MatchSession.Restore(frame, engine, journal, players);
                foreach (var actor in initial.Seats.Keys)
                    Check(MatchStateHasher.HashValue(session.SnapshotFor(actor)) == MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(current)[actor]),
                        "Cold private snapshot mismatch.");
                checkpoints.Add(new { label, current.Tick, commands = frame.Commands.Count, events = frame.Events.Count,
                    originalReplayMatched = true, privateSnapshotsMatched = true });
                return frame;
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_MIGHTY_VOLUNTARY_TRAIT_INLINE_SEPARATE_AND_COLD_REPLAY_PASS", receipts,
            scope = "Four explicit current-rule fixture rooms: blue/yellow resource, inline/separate acquisition, foreign rejection, cold duplicate, unchanged fee, confirmation/response ordering and complete original replay. No whole-card certification."
        }, json));
        Console.WriteLine("POSTGRES_MIGHTY_SURPLUS_PASS: 4 rooms; blue/yellow, inline/separate acquisition, exact fee and cold original replay.");
    }

    private static MatchState Position(string room, string trait)
    {
        var cards = new Dictionary<string, CardObjectState> {
            ["MERCY"] = new("MERCY", cardNo: "OGN·053/298", manaCost: 3, tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1"),
            ["FIORA"] = new("FIORA", cardNo: "SFD·180/221", power: 3, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["TARGET"] = new("TARGET", cardNo: "SFD·125/221", power: 4, isExhausted: true, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["DUMMY"] = new("DUMMY", cardNo: "SFD·125/221", power: 2, tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2"),
            ["EXTRA"] = new("EXTRA", cardNo: trait == RuneTrait.Blue ? "OGN·089/298" : "OGN·214/298",
                isExhausted: true, tags: [CardObjectTags.RuneCard, "COLOR:" + trait], ownerId: "P1", controllerId: "P1"),
            ["FOREIGN"] = new("FOREIGN", cardNo: "OGN·089/298", tags: [CardObjectTags.RuneCard, "COLOR:blue"], ownerId: "P2", controllerId: "P2") };
        return new(room, 0, 1, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, seed: 424242,
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1",
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(3, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["MERCY"], Base = ["FIORA", "TARGET", "EXTRA"] },
                ["P2"] = PlayerZones.Empty with { Base = ["DUMMY", "FOREIGN"] } }, cardObjects: cards);
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
