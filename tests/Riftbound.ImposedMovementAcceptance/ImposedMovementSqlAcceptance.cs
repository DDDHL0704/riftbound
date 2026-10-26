using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static partial class ImposedMovementSqlAcceptance
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static readonly string[] Cases = ["standard-pay", "standard-decline", "effect-pay", "effect-decline"];
    public static async Task PrepareAsync(NpgsqlDataSource source, string output)
    { foreach (var kind in Cases) await Create(source, Path.Combine(output, kind), kind); }

    private static async Task<(string Room, string Actor, string Peer)> Create(NpgsqlDataSource source, string folder, string kind)
    {
        Check(!Directory.Exists(folder), "Use a new imposed-movement fixture directory."); Directory.CreateDirectory(folder);
        var suffix = Guid.NewGuid().ToString("N"); var room = "imposed-movement-" + suffix;
        var actor = "imposed-a-" + suffix[..12]; var peer = "imposed-b-" + suffix[..12];
        var session = new MatchSession(room, new CoreRuleEngine(), new PostgresMatchJournal(source), new PostgresMatchPlayerStore(source));
        var tokens = new Dictionary<string, string> { [actor] = (await session.EnsurePlayerAsync(actor, default)).ReconnectToken,
            [peer] = (await session.EnsurePlayerAsync(peer, default)).ReconnectToken };
        var scenario = "imposed-movement-" + kind;
        var seeded = await session.SeedScenarioAsync(actor, "imposed-original-qa-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
        Check(seeded.Accepted, seeded.ErrorMessage);
        var frame = await Load(source, room);
        Check(frame.Commands.Count == 3 && frame.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT")
            && frame.ReplayInitialState is { Tick: 0, Seats.Count: 0 }, "Preserve actual empty-room birth and accepted seats.");
        await Save(folder, "network-fixture.json", new { room, actor, peer, tokens, kind, scenario,
            controlledQaPosition = true, ordinaryOpeningCertified = false });
        await Save(folder, "original-prepared-frame.json", frame);
        return (room, actor, peer);
    }

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, "sql", kind);
            var (room, actor, peer) = await Create(source, folder, kind);
            await DriveSql(source, folder, kind, room, actor, peer, complete: true);
        }
    }

    private static async Task DriveSql(NpgsqlDataSource source, string folder, string kind,
        string room, string actor, string peer, bool complete)
    {
        var session = Restore(await Load(source, room)); var coldRestores = 1;
        if (kind.StartsWith("effect", StringComparison.Ordinal))
        {
            await Act(actor, new PlayCardCommand("QA-SPELL", "UNL-054/219", ["QA-D", "QA-D2"], Destination: "BATTLEFIELD:QA-FIELD2"));
            await Act(actor, new PassPriorityCommand()); await Act(peer, new PassPriorityCommand());
        }
        else await Act(actor, new MoveUnitCommand("QA-A", "BASE", "BATTLEFIELD:QA-FIELD2", SourceObjectIds: ["QA-A", "QA-B"]));
        await Decide("PAY", false);
        var request = (await Load(source, room)).AuthoritativeState!.PendingRuleChoice!.Request;
        Check(request.PlayerId == actor && request.Kind == "IMPOSED_MOVEMENT_POWER_PAYMENT" && request.PowerCost == 1, "Correct executor and current imposed cost.");
        await Decide(request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-LUX:", StringComparison.Ordinal)).Id);
        request = (await Load(source, room)).AuthoritativeState!.PendingRuleChoice!.Request;
        await Decide(request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-STELE:", StringComparison.Ordinal)).Id);
        request = (await Load(source, room)).AuthoritativeState!.PendingRuleChoice!.Request;
        await Decide(QuantityOption(request, 65));
        var pending = await Load(source, room); AssertPending(pending.AuthoritativeState!, actor, peer, kind);
        await Save(folder, "sql-pending-original-frame.json", pending);
        if (complete)
        {
            await FinishSequence(async () => (await Load(source, room)).AuthoritativeState!, actor, peer, kind, (player, command) => Act(player, command));
            var final = await Load(source, room); AssertFinal(final.AuthoritativeState!, actor, kind);
            await Save(folder, "sql-original-replay.json", new { status = "IMPOSED_MOVEMENT_SQL_PASS", kind,
                commands = final.Commands.Count, events = final.Events.Count, sevenTableDigest = await Digest(source, room),
                originalBirth = final.Commands.Take(3).Select(c => c.CommandType), fullOriginalReplay = true, actualColdRestores = coldRestores });
        }
        MatchSession Restore(MatchRecoveryFrame frame) => MatchSession.Restore(frame, new CoreRuleEngine(),
            new PostgresMatchJournal(source), new PostgresMatchPlayerStore(source));
        async Task Act(string player, GameCommand command, bool accepted = true)
        {
            var frame = await Load(source, room); var beforeHash = MatchStateHasher.Hash(frame.AuthoritativeState!);
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = frame.CurrentTick;
            var raw = JsonSerializer.SerializeToElement(node); command = GameCommandJsonMapper.Map(raw);
            var id = "imposed-sql-" + frame.Commands.Count;
            var result = await session.SubmitAsync(player, id, command, raw, default);
            Check(result.Accepted == accepted, result.ErrorMessage);
            if (!accepted) Check(result.ErrorCode == ErrorCodes.InvalidTarget && result.Events.Count == 0
                && MatchStateHasher.Hash(result.State) == beforeHash, "Unavailable pay choice rejects atomically.");
            var cold = await Load(source, room); session = Restore(cold); coldRestores++;
            var digest = await Digest(source, room);
            var duplicate = await session.SubmitAsync(player, id, command, raw, default);
            Check(duplicate.Accepted == accepted && await Digest(source, room) == digest, "Cold duplicate preserves all seven tables.");
        }
        async Task Decide(string option, bool accepted = true)
        {
            var request = (await Load(source, room)).AuthoritativeState!.PendingRuleChoice!.Request;
            await Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]), accepted);
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
            var frame = await Load(source, room);
            Check(frame.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT") && frame.Commands[2].CommandType == "DEV_SEED_SCENARIO:imposed-movement-" + kind,
                "Recover from actual original birth rather than an imported board.");
            Check(frame.Commands.Count(c => !c.Accepted) == 2, "Keep unavailable and expired refusals; spoof and duplicate write no command.");
            if (stage == "final") AssertFinal(frame.AuthoritativeState!, actor, kind);
            else AssertPending(frame.AuthoritativeState!, actor, peer, kind);
            var digest = await Digest(source, room);
            await PaymentReconnectEvidence.VerifyCheckpoint(source, room, folder, stage);
            await Save(folder, "sql-" + stage + ".json", new { status = "IMPOSED_MOVEMENT_SQL_ORIGINAL_REPLAY_PASS", stage, room,
                commands = frame.Commands.Count, events = frame.Events.Count, sevenTableDigest = digest, fullOriginalReplay = true,
                stateHash = MatchStateHasher.Hash(frame.AuthoritativeState!), frame.PlayerViews, frame.SpectatorReplayFrame });
        }
    }

    internal static void AssertPending(MatchState state, string actor, string peer, string kind)
    {
        Check(state.PendingRuleChoice is { Answers.Count: 3, Request.Kind: "RESOURCE_CONVERSION_AMOUNT" } p
            && p.Request.PlayerId == actor && p.Request.Options.Count == 2
            && p.Request.Options.All(o => o.Id.StartsWith("QUANTITY:", StringComparison.Ordinal))
            && p.Request.Preview!.RunePools[actor] is { SpellOnlyMana: 2, Mana: 66, TotalPower: 0 }
            && p.Request.Preview.ObjectLocations["QA-RUNE"].Zone == "BASE"
            && !p.Request.Preview.CardObjects["QA-STELE"].IsExhausted
            && state.RunePools[actor] == new RunePool(66, 0, spellOnlyMana: 2) && state.CardObjects["QA-LUX"].IsExhausted
            && !state.CardObjects["QA-STELE"].IsExhausted
            && p.Origin.RunePools[actor] == new RunePool(66, 0) && !p.Origin.CardObjects["QA-LUX"].IsExhausted,
            "Keep the confirmed Lux gain, original movement checkpoint, exact transcript and uncommitted conversion.");
        if (kind.StartsWith("effect", StringComparison.Ordinal)) Check(state.PendingRuleChoice!.Origin.PriorityPlayerId == peer,
            "The final PASS submitter is not the movement executor/payer.");
    }
    internal static void AssertFinal(MatchState state, string actor, string kind)
    {
        var pay = kind.EndsWith("-pay", StringComparison.Ordinal); var effect = kind.StartsWith("effect", StringComparison.Ordinal);
        Check(state.PendingRuleChoice is null && state.RunePools[actor].Mana == 1
            && state.RunePools[actor].SpellOnlyMana == (effect ? 1 : 2)
            && state.RunePools[actor].TotalPower == (pay ? 64 : 65) && state.CardObjects["QA-LUX"].IsExhausted
            && state.CardObjects["QA-STELE"].IsExhausted && state.ObjectLocations["QA-RUNE"].Zone == "BASE",
            "Retain acquisitions and debit exactly once only when paying; keep the untouched real rune.");
        foreach (var id in effect ? new[] { "QA-D", "QA-D2" } : ["QA-A", "QA-B"])
            Check(state.ObjectLocations[id].BattlefieldObjectId == (pay ? "QA-FIELD2" : effect ? "QA-FIELD1" : null)
                && state.CardObjects[id].IsExhausted == (!effect && pay), "Move/exhaust only the actually paid group.");
        if (effect) Check(state.PlayerZones[actor].Graveyard.Contains("QA-SPELL") && state.PlayerZones[actor].Graveyard.Contains("QA-BULLET")
            && new[] { "QA-D", "QA-D2" }.All(id => state.CardObjects[id].Damage == 0),
            "Declining movement never refunds the original spell; later S5 chooses zero and preserves power and targets.");
    }
    internal static string QuantityOption(RuleChoiceRequest request, int amount)
    {
        Check(request.Options.Count == 2 && request.Options.All(o => o.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)), "Require the genuine bounded quantity options.");
        return request.Options.Single(option => {
            var parts = option.Id.Split(':'); return int.Parse(parts[^2]) <= amount && amount <= int.Parse(parts[^1]); }).Id;
    }
    internal static async Task FinishSequence(Func<Task<MatchState>> current, string actor, string peer, string kind, Func<string, GameCommand, Task> act)
    {
        await Quantity(65, "RESOURCE_CONVERSION_AMOUNT");
        await Decide(kind.EndsWith("-pay", StringComparison.Ordinal) ? "PAY" : "DECLINE");
        if (kind.StartsWith("effect", StringComparison.Ordinal))
        {
            await act(actor, new PlayCardCommand("QA-BULLET", "OGN·268/298", ["QA-FIELD1"]));
            await act(actor, new PassPriorityCommand()); await act(peer, new PassPriorityCommand());
            await Decide("SPEND_POWER:CHOOSE_AMOUNT"); await Quantity(0, "INSTRUCTION_POWER_PAYMENT");
        }
        async Task Decide(string option)
        {
            var request = (await current()).PendingRuleChoice!.Request;
            await act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        async Task Quantity(int amount, string expectedKind)
        {
            var count = 0;
            while ((await current()).PendingRuleChoice is { Request: var request }
                && request.Kind == expectedKind && request.Options.All(o => o.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)))
            { Check(++count <= 31, "Bound every amount selection."); await Decide(QuantityOption(request, amount)); }
        }
    }
    internal static async Task<MatchRecoveryFrame> Load(NpgsqlDataSource source, string room)
    {
        var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new InvalidOperationException("Missing original imposed-movement history.");
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(frame.IsConsistent && errors.Count == 0, string.Join("; ", frame.ValidationErrors.Concat(errors))); return frame;
    }
    internal static async Task<string> Digest(NpgsqlDataSource source, string room)
    {
        var rows = await PaymentReconnectEvidence.Capture(source, room);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows.Tables, Json)))).ToLowerInvariant();
    }
    internal static Task Save(string folder, string name, object value) => File.WriteAllTextAsync(Path.Combine(folder, name), JsonSerializer.Serialize(value, value.GetType(), Json));
    internal static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
